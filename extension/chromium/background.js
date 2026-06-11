// MV3 service worker: intercepts top-frame navigations, asks the native host to
// evaluate the URL, and redirects the tab to the local blocked.html if the StopMe
// service says so. Sub-resources are NOT intercepted in Phase 4 — declarativeNetRequest
// rules driven by the snapshot will arrive in Phase 5.

import { checkUrl, heartbeat } from './nativeHost.js';

// Phase 5: send a heartbeat every minute so the service can flag strict-mode sessions
// running with a disabled / closed browser. chrome.alarms wakes the service worker even
// when it has been parked.
const HEARTBEAT_ALARM = 'stopme-heartbeat';
const HEARTBEAT_PERIOD_MIN = 1;
const BROWSER_NAME = (() => {
  const ua = (self.navigator?.userAgent ?? '').toLowerCase();
  if (ua.includes('edg/')) return 'edge';
  if (ua.includes('opr/')) return 'opera';
  if (ua.includes('brave')) return 'brave';
  return 'chrome';
})();

async function sendHeartbeat() {
  try {
    const manifest = chrome.runtime.getManifest();
    await heartbeat(BROWSER_NAME, chrome.runtime.id, manifest.version);
  } catch (err) {
    console.warn('[StopMe] heartbeat failed:', err.message);
  }
}

chrome.runtime.onStartup.addListener(() => {
  chrome.alarms.create(HEARTBEAT_ALARM, { periodInMinutes: HEARTBEAT_PERIOD_MIN });
  sendHeartbeat();
});
chrome.runtime.onInstalled.addListener(() => {
  chrome.alarms.create(HEARTBEAT_ALARM, { periodInMinutes: HEARTBEAT_PERIOD_MIN });
  sendHeartbeat();
});
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === HEARTBEAT_ALARM) sendHeartbeat();
});

const BLOCKED_PAGE = chrome.runtime.getURL('blocked/blocked.html');

// Soft cache to avoid spamming the host on a tight retry loop. We remember
// the most recent decision per tab (NOT just the URL+time) and reuse it: a
// cached "block" must still redirect — otherwise hitting the same URL twice
// within 5 s would let it through. The cache only short-circuits the IPC
// round-trip, never the enforcement.
const recentDecisions = new Map();
const RECENT_TTL_MS = 5_000;

function isHttp(url) {
  return typeof url === 'string' && (url.startsWith('http://') || url.startsWith('https://'));
}

function isOwnBlockedPage(url) {
  return typeof url === 'string' && url.startsWith(BLOCKED_PAGE);
}

function buildBlockedUrl(originalUrl, decision) {
  const params = new URLSearchParams();
  params.set('url', originalUrl);
  if (decision.sessionName) params.set('session', decision.sessionName);
  if (decision.reason) params.set('reason', decision.reason);
  if (decision.matchedRuleType) params.set('rule', decision.matchedRuleType);
  if (typeof decision.remainingSeconds === 'number') {
    params.set('remaining', String(decision.remainingSeconds));
  }
  return `${BLOCKED_PAGE}?${params.toString()}`;
}

chrome.webNavigation.onBeforeNavigate.addListener(async (details) => {
  if (details.frameId !== 0) return;        // top frame only
  if (!isHttp(details.url)) return;
  if (isOwnBlockedPage(details.url)) return;

  const cached = recentDecisions.get(details.tabId);
  if (cached && cached.url === details.url && Date.now() - cached.at < RECENT_TTL_MS) {
    // Cache hit. Reuse the decision instead of re-querying the host.
    if (cached.blocked) {
      await chrome.tabs.update(details.tabId, { url: buildBlockedUrl(details.url, cached.decision) });
    }
    return;
  }

  try {
    const response = await checkUrl(details.url, details.tabId, 'chromium');
    const blocked = response?.type === 'CHECK_URL_RESULT' && response.blocked === true;
    recentDecisions.set(details.tabId, {
      url: details.url,
      at: Date.now(),
      blocked,
      decision: blocked ? response : null,
    });
    if (blocked) {
      await chrome.tabs.update(details.tabId, { url: buildBlockedUrl(details.url, response) });
    }
  } catch (err) {
    // Don't fail-closed by accident: if the native host is unreachable, the user can
    // still browse. The popup will surface the disconnect state.
    console.warn('[StopMe] check_url failed:', err.message);
  }
});

chrome.tabs.onRemoved.addListener((tabId) => {
  recentDecisions.delete(tabId);
});
