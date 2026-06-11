// StopMe — modals, toasts, blocked-app notification, browser blocked page

/* ============ Strict mode confirmation modal ============ */
function StrictConfirmModal({ S }) {
  const apps = RUNNING_APPS.filter((a) => S.selectedApps.includes(a.id));
  return (
    <Modal open={S.modal === "strict-confirm"} onClose={S.closeModal} width={460}>
      <ModalHeader icon="lock" iconTone="strict"
        title="Start a strict session?"
        subtitle="Strict mode can't be stopped before the timer ends. Not by closing StopMe, not by restarting Windows." />
      <div style={{
        background: "var(--bg-secondary)", borderRadius: 14, padding: 16, marginBottom: 20,
        display: "flex", flexDirection: "column", gap: 10,
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13.5 }}>
          <span style={{ color: "var(--text-secondary)" }}>Duration</span>
          <span style={{ fontWeight: 700 }}>{fmtDuration(S.duration)} — until {fmtEndsAt(Date.now() + S.duration * 60000)}</span>
        </div>
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13.5 }}>
          <span style={{ color: "var(--text-secondary)" }}>Apps blocked</span>
          <span style={{ fontWeight: 700 }}>{S.selectedApps.length + S.manualRules.length}{apps.length ? ` — ${apps.slice(0, 3).map((a) => a.name).join(", ")}${apps.length > 3 ? "…" : ""}` : ""}</span>
        </div>
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13.5 }}>
          <span style={{ color: "var(--text-secondary)" }}>Websites blocked</span>
          <span style={{ fontWeight: 700 }}>{S.totalSites}</span>
        </div>
      </div>
      <div style={{ display: "flex", gap: 10, justifyContent: "flex-end" }}>
        <Btn onClick={S.closeModal}>Cancel</Btn>
        <Btn variant="dark" icon="lock" onClick={() => S.startSession(true)}>I'm sure — lock me in</Btn>
      </div>
    </Modal>
  );
}

/* ============ Stop (soft) confirmation ============ */
function StopConfirmModal({ S }) {
  if (!S.session) return null;
  const leftSec = Math.max(0, Math.round((S.session.endsAt - S.now) / 1000));
  return (
    <Modal open={S.modal === "stop-confirm"} onClose={S.closeModal} width={420}>
      <ModalHeader icon="stop" iconTone="warning"
        title="Stop this session?"
        subtitle={`${fmtCountdown(leftSec)} still remaining. Blocked apps and sites will be available again immediately.`} />
      <div style={{ display: "flex", gap: 10, justifyContent: "flex-end" }}>
        <Btn variant="primary" onClick={S.closeModal}>Keep focusing</Btn>
        <Btn variant="ghost" danger onClick={S.stopSession}>Stop session</Btn>
      </div>
    </Modal>
  );
}

/* ============ Create custom session modal ============ */
function CreateSessionModal({ S }) {
  const { useState } = React;
  const [name, setName] = useState("Deep work");
  const [start, setStart] = useState("now");
  const [time, setTime] = useState("19:00");
  const open = S.modal === "create-session";
  if (!open) return null;
  return (
    <Modal open onClose={S.closeModal} width={560}>
      <ModalHeader onClose={S.closeModal} icon="sparkle" iconTone="accent"
        title="Create a session" subtitle="Name it, choose what to block, and start now or schedule it." />
      <div style={{ display: "flex", flexDirection: "column", gap: 18 }}>
        <div>
          <label style={{ fontSize: 12, fontWeight: 700, color: "var(--text-tertiary)", letterSpacing: "0.06em", textTransform: "uppercase", display: "block", marginBottom: 8 }}>Session name</label>
          <TextInput value={name} onChange={setName} placeholder="e.g. Deep work" />
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <button onClick={() => S.nav("apps") || S.closeModal()} style={{
            border: "1px solid var(--border-strong)", background: "var(--bg-primary)", borderRadius: 14,
            padding: "14px 16px", textAlign: "left", display: "flex", alignItems: "center", gap: 12,
          }}>
            <Icon name="apps" size={19} style={{ color: "var(--accent)" }} />
            <span style={{ flex: 1 }}>
              <span style={{ display: "block", fontWeight: 700, fontSize: 13.5 }}>Apps to block</span>
              <span style={{ display: "block", fontSize: 12.5, color: "var(--text-secondary)" }}>{S.selectedApps.length + S.manualRules.length} selected</span>
            </span>
            <Icon name="chevronRight" size={15} style={{ color: "var(--text-tertiary)" }} />
          </button>
          <button onClick={() => S.nav("websites") || S.closeModal()} style={{
            border: "1px solid var(--border-strong)", background: "var(--bg-primary)", borderRadius: 14,
            padding: "14px 16px", textAlign: "left", display: "flex", alignItems: "center", gap: 12,
          }}>
            <Icon name="globe" size={19} style={{ color: "var(--accent)" }} />
            <span style={{ flex: 1 }}>
              <span style={{ display: "block", fontWeight: 700, fontSize: 13.5 }}>Websites to block</span>
              <span style={{ display: "block", fontSize: 12.5, color: "var(--text-secondary)" }}>{S.totalSites} selected</span>
            </span>
            <Icon name="chevronRight" size={15} style={{ color: "var(--text-tertiary)" }} />
          </button>
        </div>
        <div>
          <label style={{ fontSize: 12, fontWeight: 700, color: "var(--text-tertiary)", letterSpacing: "0.06em", textTransform: "uppercase", display: "block", marginBottom: 8 }}>Duration</label>
          <DurationSelector mins={S.duration} onChange={S.setDuration} />
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 16, flexWrap: "wrap" }}>
          <div style={{ display: "flex", background: "var(--bg-secondary)", borderRadius: 11, padding: 3 }}>
            {[["now", "Start now"], ["schedule", "Schedule"]].map(([v, l]) => (
              <button key={v} onClick={() => setStart(v)} style={{
                padding: "8px 16px", borderRadius: 9, fontSize: 13, fontWeight: 600, border: "none",
                background: start === v ? "var(--bg-primary)" : "transparent",
                color: start === v ? "var(--text-primary)" : "var(--text-secondary)",
                boxShadow: start === v ? "0 1px 3px rgba(23,21,31,0.12)" : "none",
              }}>{l}</button>
            ))}
          </div>
          {start === "schedule" ? (
            <input type="time" value={time} onChange={(e) => setTime(e.target.value)} style={{
              padding: "9px 12px", borderRadius: 11, border: "1px solid var(--border-strong)",
              fontSize: 13.5, fontWeight: 600, fontFamily: "inherit",
            }} />
          ) : null}
          <StrictToggle on={S.strict} onChange={S.setStrict} />
        </div>
        <div style={{ display: "flex", gap: 10, justifyContent: "flex-end", borderTop: "1px solid var(--border)", paddingTop: 18 }}>
          <Btn variant="ghost" onClick={S.closeModal}>Cancel</Btn>
          <Btn variant="primary" icon={start === "now" ? "play" : "calendar"}
            disabled={S.selectedApps.length + S.manualRules.length + S.totalSites === 0}
            onClick={() => {
              if (start === "now") { S.closeModal(); S.requestStart(name); }
              else { S.closeModal(); S.toast(`"${name}" scheduled for ${time}`); }
            }}>
            {start === "now" ? "Create & start" : "Schedule session"}
          </Btn>
        </div>
      </div>
    </Modal>
  );
}

/* ============ Service diagnostics modal ============ */
function DiagnosticsModal({ S }) {
  const { useState, useEffect } = React;
  const open = S.modal === "diagnostics";
  const [step, setStep] = useState(0);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    if (open) { setStep(0); setRunning(true); }
  }, [open]);

  useEffect(() => {
    if (!running || !open) return;
    if (step >= DIAGNOSTIC_CHECKS.length) { setRunning(false); return; }
    const t = setTimeout(() => setStep(step + 1), 450);
    return () => clearTimeout(t);
  }, [running, step, open]);

  if (!open) return null;

  const failIds = S.service === "down" ? ["service", "ipc"]
    : S.service === "auth" ? ["auth"]
    : S.service === "ext" ? ["ext-chrome"]
    : [];
  const warnIds = ["ext-edge"];
  const doneAll = step >= DIAGNOSTIC_CHECKS.length;

  return (
    <Modal open onClose={S.closeModal} width={520}>
      <ModalHeader onClose={S.closeModal} icon="monitor" iconTone={failIds.length ? "error" : "accent"}
        title="Service diagnostics"
        subtitle="Checking every part of the blocking pipeline." />
      <div style={{ display: "flex", flexDirection: "column", gap: 2, marginBottom: 20 }}>
        {DIAGNOSTIC_CHECKS.map((c, i) => {
          const state = i < step ? (failIds.includes(c.id) ? "fail" : warnIds.includes(c.id) ? "warn" : "ok") : i === step && running ? "running" : "pending";
          return (
            <div key={c.id} style={{
              display: "flex", alignItems: "center", gap: 12, padding: "11px 14px", borderRadius: 12,
              background: state === "fail" ? "var(--error-soft)" : "transparent",
              opacity: state === "pending" ? 0.45 : 1, transition: "all 0.2s ease",
            }}>
              <span style={{ width: 20, display: "flex", justifyContent: "center" }}>
                {state === "running" ? <Spinner size={15} /> :
                  state === "ok" ? <Icon name="check" size={16} style={{ color: "var(--success)" }} /> :
                  state === "warn" ? <Icon name="alert" size={15} style={{ color: "var(--warning)" }} /> :
                  state === "fail" ? <Icon name="x" size={15} style={{ color: "var(--error)" }} /> :
                  <span style={{ width: 7, height: 7, borderRadius: 99, background: "var(--border-strong)", display: "inline-block" }}></span>}
              </span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 600, fontSize: 13.5 }}>{c.label}</div>
                <div style={{ fontSize: 12, color: "var(--text-tertiary)", fontFamily: "Consolas, monospace" }}>{c.detail}</div>
              </div>
              {state === "fail" ? <Badge tone="error">Failed</Badge> : null}
              {state === "warn" ? <Badge tone="warning">Optional</Badge> : null}
            </div>
          );
        })}
      </div>
      {doneAll ? (
        <div style={{
          padding: "13px 16px", borderRadius: 13, marginBottom: 18, fontSize: 13.5, display: "flex", gap: 10,
          background: failIds.length ? "var(--error-soft)" : "var(--success-soft)",
          color: failIds.length ? "var(--error)" : "var(--success)", fontWeight: 600,
        }}>
          <Icon name={failIds.length ? "alert" : "shieldCheck"} size={17} style={{ flexShrink: 0 }} />
          <span style={{ textWrap: "pretty" }}>
            {failIds.length
              ? S.service === "down"
                ? "The background service is not running. Restarting it usually fixes this."
                : S.service === "auth"
                  ? "Authentication with the local service failed. Repair the connection to issue a new token."
                  : "Chrome extension is not responding. Reinstall or enable it in your browser."
              : "Everything looks good. StopMe is ready to block."}
          </span>
        </div>
      ) : null}
      <div style={{ display: "flex", gap: 10, justifyContent: "space-between", alignItems: "center" }}>
        <Btn variant="ghost" icon="file" size="sm">Open logs</Btn>
        <div style={{ display: "flex", gap: 10 }}>
          <Btn onClick={() => { setStep(0); setRunning(true); }} icon="refresh">Run again</Btn>
          {doneAll && failIds.length ? (
            <Btn variant="primary" onClick={() => { S.closeModal(); S.fixService(); }}>
              {S.service === "auth" ? "Repair connection" : S.service === "ext" ? "Install extension" : "Restart service"}
            </Btn>
          ) : <Btn variant="primary" onClick={S.closeModal}>Done</Btn>}
        </div>
      </div>
    </Modal>
  );
}

/* ============ Windows toast — blocked app notification ============ */
function BlockedAppToast({ visible, onClose, appName = "Discord", endsAt }) {
  if (!visible) return null;
  return (
    <div style={{
      position: "fixed", right: 20, bottom: 64, zIndex: 300, width: 372,
      background: "rgba(32,30,42,0.97)", backdropFilter: "blur(20px)",
      borderRadius: 10, boxShadow: "0 8px 32px rgba(0,0,0,0.45)", overflow: "hidden",
      animation: "sm-toast-in 0.35s cubic-bezier(0.16,1,0.3,1)", color: "#fff",
      border: "1px solid rgba(255,255,255,0.08)",
    }}>
      <div style={{ display: "flex", gap: 13, padding: "14px 14px 12px" }}>
        <div style={{
          width: 40, height: 40, borderRadius: 10, flexShrink: 0,
          background: "linear-gradient(140deg, var(--accent), #4F3DC4)",
          display: "flex", alignItems: "center", justifyContent: "center",
        }}><Icon name="shield" size={20} /></div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 700, fontSize: 13.5 }}>{appName} is blocked</div>
          <div style={{ fontSize: 12.5, color: "rgba(255,255,255,0.72)", marginTop: 2, textWrap: "pretty" }}>
            StopMe is protecting your focus. {appName} will be available again at {endsAt}.
          </div>
        </div>
        <button onClick={onClose} style={{ border: "none", background: "transparent", color: "rgba(255,255,255,0.6)", height: 22, display: "flex", alignItems: "center" }}>
          <Icon name="x" size={14} />
        </button>
      </div>
      <div style={{ display: "flex", borderTop: "1px solid rgba(255,255,255,0.10)" }}>
        <button onClick={onClose} style={{ flex: 1, padding: "9px 0", border: "none", background: "transparent", color: "rgba(255,255,255,0.85)", fontSize: 12.5, fontWeight: 600 }}>
          Back to work
        </button>
        <button onClick={onClose} style={{ flex: 1, padding: "9px 0", border: "none", background: "transparent", color: "rgba(255,255,255,0.55)", fontSize: 12.5, fontWeight: 600, borderLeft: "1px solid rgba(255,255,255,0.10)" }}>
          View session
        </button>
      </div>
      <div style={{ position: "absolute", left: 14, bottom: 42, fontSize: 10.5, color: "rgba(255,255,255,0.40)", letterSpacing: "0.04em" }}>StopMe · now</div>
    </div>
  );
}

/* ============ Browser blocked page (full overlay with browser chrome) ============ */
function BrowserBlockedPage({ visible, onClose, site = "youtube.com", endsAt }) {
  if (!visible) return null;
  return (
    <div style={{ position: "fixed", inset: 0, zIndex: 400, background: "rgba(23,21,31,0.5)", animation: "sm-fade-in 0.2s ease", display: "flex", alignItems: "center", justifyContent: "center", padding: 40 }} onClick={onClose}>
      <div onClick={(e) => e.stopPropagation()} style={{
        width: "min(960px, 100%)", height: "min(640px, 100%)", borderRadius: 14, overflow: "hidden",
        boxShadow: "var(--shadow-modal)", display: "flex", flexDirection: "column", background: "#fff",
        animation: "sm-pop-in 0.25s cubic-bezier(0.16,1,0.3,1)",
      }}>
        {/* browser chrome */}
        <div style={{ background: "#DEE1E6", padding: "8px 12px 0", display: "flex", alignItems: "flex-end", gap: 8 }}>
          <div style={{
            background: "#F1F3F4", borderRadius: "10px 10px 0 0", padding: "8px 16px", fontSize: 12.5,
            display: "flex", alignItems: "center", gap: 8, width: 220, color: "#3C4043",
          }}>
            <Icon name="shield" size={13} style={{ color: "var(--accent)" }} />
            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>Blocked by StopMe</span>
            <Icon name="x" size={11} style={{ marginLeft: "auto", color: "#80868B" }} />
          </div>
          <button onClick={onClose} style={{ marginLeft: "auto", marginBottom: 6, border: "none", background: "transparent", color: "#5F6368", display: "flex" }}><Icon name="x" size={16} /></button>
        </div>
        <div style={{ background: "#F1F3F4", padding: "7px 14px", display: "flex", alignItems: "center", gap: 10, borderBottom: "1px solid #DADCE0" }}>
          <span style={{ color: "#5F6368", display: "flex", gap: 10 }}>
            <Icon name="chevronRight" size={15} style={{ transform: "rotate(180deg)" }} /><Icon name="chevronRight" size={15} /><Icon name="refresh" size={14} />
          </span>
          <div style={{ flex: 1, background: "#fff", borderRadius: 99, padding: "6px 16px", fontSize: 12.5, color: "#3C4043", border: "1px solid #DADCE0" }}>
            {site}
          </div>
        </div>
        {/* blocked content */}
        <div style={{
          flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
          gap: 10, textAlign: "center", padding: 40,
          background: "linear-gradient(170deg, #FBFAFE 0%, var(--accent-soft) 100%)",
        }}>
          <div style={{
            width: 64, height: 64, borderRadius: 20, marginBottom: 8,
            background: "linear-gradient(140deg, var(--accent), #4F3DC4)", color: "#fff",
            display: "flex", alignItems: "center", justifyContent: "center",
            boxShadow: "0 8px 24px rgba(109,90,230,0.35)",
          }}><Icon name="shield" size={30} /></div>
          <div style={{ fontFamily: "var(--font-serif)", fontSize: 40, letterSpacing: "-0.01em" }}>
            This one can wait.
          </div>
          <p style={{ color: "var(--text-secondary)", fontSize: 15, maxWidth: 400, textWrap: "pretty" }}>
            <strong style={{ color: "var(--text-primary)" }}>{site}</strong> is blocked while StopMe protects your focus.
            It will be available again at <strong style={{ color: "var(--text-primary)" }}>{endsAt}</strong>.
          </p>
          <div style={{
            marginTop: 14, padding: "10px 22px", borderRadius: 99, background: "var(--bg-primary)",
            border: "1px solid var(--accent-soft-2)", fontSize: 13.5, fontWeight: 600, color: "var(--accent-text)",
            display: "flex", alignItems: "center", gap: 8,
          }}>
            <Icon name="clock" size={15} /> Back to it at {endsAt}
          </div>
          <p style={{ marginTop: 22, fontFamily: "var(--font-serif)", fontStyle: "italic", fontSize: 17, color: "var(--text-tertiary)" }}>
            “You will never reach your destination if you stop and throw stones at every dog that barks.”
          </p>
        </div>
      </div>
    </div>
  );
}

/* ============ Tiny feedback toast (in-app) ============ */
function MiniToast({ msg }) {
  if (!msg) return null;
  return (
    <div style={{
      position: "fixed", bottom: 24, left: "50%", transform: "translateX(-50%)", zIndex: 500,
      background: "var(--text-primary)", color: "#fff", padding: "10px 20px", borderRadius: 99,
      fontSize: 13, fontWeight: 600, boxShadow: "var(--shadow-raised)",
      animation: "sm-slide-up 0.25s ease", display: "flex", alignItems: "center", gap: 8,
    }}>
      <Icon name="check" size={14} />{msg}
    </div>
  );
}

Object.assign(window, {
  StrictConfirmModal, StopConfirmModal, CreateSessionModal, DiagnosticsModal,
  BlockedAppToast, BrowserBlockedPage, MiniToast,
});
