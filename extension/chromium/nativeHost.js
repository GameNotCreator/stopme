// Wraps chrome.runtime.connectNative with auto-reconnect and a FIFO request queue.
// Native host processes requests sequentially and replies in order, so we just keep a
// single in-flight queue and resolve them in arrival order.

const HOST_NAME = 'com.stopme.nativehost';
const RECONNECT_DELAY_MS = 2000;

class NativeHost {
  constructor() {
    this._port = null;
    this._queue = [];        // [{ resolve, reject, timer }]
    this._connecting = null;
    this._connectedAt = 0;
  }

  async send(message, timeoutMs = 4000) {
    await this._ensureConnected();
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        // Drop the oldest pending entry if it's ours — there isn't a way to identify
        // the message-to-response edge without a request id, but the queue is FIFO and
        // small, so falling back to "first pending" is acceptable.
        const idx = this._queue.findIndex(e => e.timer === timer);
        if (idx >= 0) this._queue.splice(idx, 1);
        reject(new Error('Native host response timed out.'));
      }, timeoutMs);
      this._queue.push({ resolve, reject, timer });
      try {
        this._port.postMessage(message);
      } catch (err) {
        clearTimeout(timer);
        const idx = this._queue.indexOf(this._queue[this._queue.length - 1]);
        if (idx >= 0) this._queue.splice(idx, 1);
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
        this._connectedAt = Date.now();

        port.onMessage.addListener((msg) => {
          const entry = this._queue.shift();
          if (entry) {
            clearTimeout(entry.timer);
            entry.resolve(msg);
          }
        });

        port.onDisconnect.addListener(() => {
          const err = chrome.runtime.lastError;
          this._port = null;
          // Reject any pending requests.
          for (const entry of this._queue) {
            clearTimeout(entry.timer);
            entry.reject(new Error(err?.message || 'Native host disconnected.'));
          }
          this._queue = [];
          // Auto-reconnect after a short delay.
          setTimeout(() => { this._connecting = null; }, RECONNECT_DELAY_MS);
        });

        // The port is considered usable immediately — Chrome buffers until the host attaches.
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
