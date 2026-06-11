import { getStatus, getActiveSessions } from '../nativeHost.js';

const $ = (id) => document.getElementById(id);
const escape = (s) => String(s ?? '').replace(/[&<>"']/g, (c) =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function formatRemaining(secs) {
  if (!Number.isFinite(secs) || secs <= 0) return '—';
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  if (h > 0) return `${h}h ${m.toString().padStart(2, '0')}m`;
  if (m > 0) return `${m}m`;
  return `${secs}s`;
}

async function refresh() {
  const badge = $('connection-badge');
  const status = $('status');
  const list = $('sessions-list');
  badge.textContent = 'Checking…';
  status.innerHTML = '';
  list.textContent = 'Loading…';

  try {
    const [statusResp, sessionsResp] = await Promise.all([
      getStatus(),
      getActiveSessions().catch(() => ({ sessions: [] })),
    ]);

    if (statusResp?.connected) {
      badge.textContent = 'Connected';
      badge.style.background = 'var(--accent)';
      status.innerHTML = `
        <div class="row"><span>Service version</span><span class="code">${escape(statusResp.serviceVersion ?? '—')}</span></div>
        <div class="row"><span>Active sessions</span><span>${statusResp.activeSessionCount ?? 0}</span></div>
        <div class="row"><span>Compiled rules</span><span>${statusResp.compiledRuleCount ?? 0}</span></div>
      `;
    } else {
      badge.textContent = 'Disconnected';
      badge.style.background = 'var(--secondary)';
      status.innerHTML = `<div class="row muted">${escape(statusResp?.reason ?? 'Service unreachable.')}</div>`;
    }

    const sessions = sessionsResp?.sessions ?? [];
    if (sessions.length === 0) {
      list.innerHTML = '<span class="muted">No session is blocking right now.</span>';
    } else {
      list.innerHTML = sessions.map((s) => `
        <div class="session-row">
          <span class="name">${escape(s.name)}</span>
          <span class="meta">${escape(s.mode)} · ${escape(s.lockPolicy)} · ${formatRemaining(s.remainingSeconds)} remaining</span>
        </div>
      `).join('');
    }
  } catch (err) {
    badge.textContent = 'Error';
    badge.style.background = 'var(--destructive)';
    status.innerHTML = `<div class="row muted">${escape(err?.message ?? 'Unknown error')}</div>`;
    list.textContent = '';
  }
}

$('refresh').addEventListener('click', refresh);
refresh();
