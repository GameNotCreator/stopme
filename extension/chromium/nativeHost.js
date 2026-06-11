// Wraps chrome.runtime.connectNative with auto-reconnect and a request-id
// correlation table. Earlier versions resolved promises in FIFO order; on a
// timeout the queue shifted and the next response landed on the wrong tab's
// promise (a misblock under high latency). We now generate a per-request id,
// echo it through the native host (see ExtensionMessageDispatcher), and look
// up the matching pending entry on response.

const HOST_NAME = 'com.stopme.nativehost';
const RECONNECT_DELAY_MS = 2000;

let _nextRequestId = 1;
function nextRequestId() {
  return `r${(_nextRequestId++).toString(36)}`;
}

class NativeHost {
  constructor() {
    this._port = null;
    this._pending = new Map();   // id → { resolve, reject, timer }
    this._connecting = null;
  }

  async send(message, timeoutMs = 4000) {
    await this._ensureConnected();
    const id = nextRequestId();
    const tagged = { ...message, requestId: id };
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this._pending.delete(id);
        reject(new Error(`Native host response timed out (requestId=${id}).`));
      }, timeoutMs);
      this._pending.set(id, { resolve, reject, timer });
      try {
        this._port.postMessage(tagged);
      } catch (err) {
        clearTimeout(timer);
        this._pending.delete(id);
        reject(err);
      }
    });
  }

  _ensureConnected() {
    if (this._port) return Promise.resolve();
    if (this._connecting) return this._connecting;

    this._connecting = new Promise((resolve, reject) => {
      try {
        const port = chrome.runtime.connectNative(HOST_NAME);
        this._port = port;

        port.onMessage.addListener((msg) => {
          // Look up by id when the host echoes it back. Fall back to
          // first-pending only if the message has no id at all (older
          // host builds) — newer host always sets it.
          const id = msg?.requestId;
          let entry;
          if (id && this._pending.has(id)) {
            entry = this._pending.get(id);
            this._pending.delete(id);
          } else if (!id) {
            const firstId = this._pending.keys().next().value;
            if (firstId !== undefined) {
              entry = this._pending.get(firstId);
              this._pending.delete(firstId);
            }
          }
          if (entry) {
            clearTimeout(entry.timer);
            entry.resolve(msg);
          }
        });

        port.onDisconnect.addListener(() => {
          const err = chrome.runtime.lastError;
          this._port = null;
          for (const entry of this._pending.values()) {
            clearTimeout(entry.timer);
            entry.reject(new Error(err?.message || 'Native host disconnected.'));
          }
          this._pending.clear();
          setTimeout(() => { this._connecting = null; }, RECONNECT_DELAY_MS);
        });

        resolve();
      } catch (err) {
        this._connecting = null;
        reject(err);
      }
    });
    return this._connecting;
  }
}

export const nativeHost = new NativeHost();

export const Messages = Object.freeze({
  CheckUrl: 'CHECK_URL',
  GetStatus: 'GET_STATUS',
  GetActiveSessions: 'GET_ACTIVE_SESSIONS',
  Heartbeat: 'HEARTBEAT',
});

export function checkUrl(url, tabId, browser = 'chromium') {
  return nativeHost.send({ type: Messages.CheckUrl, url, tabId, browser });
}

export function getStatus() {
  return nativeHost.send({ type: Messages.GetStatus });
}

export function getActiveSessions() {
  return nativeHost.send({ type: Messages.GetActiveSessions });
}

export function heartbeat(browser, extensionId, extensionVersion) {
  return nativeHost.send({
    type: Messages.Heartbeat,
    browser,
    extensionId,
    extensionVersion,
  }, 2500);
}
