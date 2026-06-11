// StopMe — Dashboard page (3 layout variants) + active-session card (3 timer variants)
// Receives full app state via props from the shell.

/* ---------- Service status banner (connecting / error states) ---------- */
function ServiceBanner({ service, onRetry, onDiagnostics, onInstallExt, retrying }) {
  if (service === "connected") return null;
  const configs = {
    connecting: {
      tone: "neutral",
      icon: <Spinner size={18} />,
      title: "Connecting to StopMe service…",
      body: "This usually takes a couple of seconds.",
      actions: null,
    },
    "connecting-long": {
      tone: "warning",
      icon: <Spinner size={18} color="var(--warning)" />,
      title: "Still connecting…",
      body: "The StopMe service is taking longer than usual to respond.",
      actions: (
        <>
          <Btn size="sm" onClick={onRetry}>Retry</Btn>
          <Btn size="sm" variant="ghost" onClick={onDiagnostics}>Run diagnostics</Btn>
        </>
      ),
    },
    down: {
      tone: "error",
      icon: <Icon name="alert" size={19} />,
      title: "StopMe service is not running.",
      body: "Blocking won't work until the service is back. Try restarting it, or run diagnostics to find the problem.",
      actions: (
        <>
          <Btn size="sm" variant="primary" onClick={onRetry}>{retrying ? <Spinner size={14} color="#fff" /> : null}Retry</Btn>
          <Btn size="sm" onClick={onDiagnostics}>Run diagnostics</Btn>
          <Btn size="sm" variant="ghost" icon="external">Installation guide</Btn>
        </>
      ),
    },
    auth: {
      tone: "error",
      icon: <Icon name="alert" size={19} />,
      title: "StopMe could not authenticate with the local service.",
      body: "Your app and the background service are out of sync. Repairing the connection fixes this in most cases.",
      actions: (
        <>
          <Btn size="sm" variant="primary" onClick={onRetry}>Repair connection</Btn>
          <Btn size="sm" variant="ghost" onClick={onDiagnostics}>Run diagnostics</Btn>
        </>
      ),
    },
    ext: {
      tone: "warning",
      icon: <Icon name="puzzle" size={19} />,
      title: "Browser extension not connected.",
      body: "Apps will still be blocked, but websites won't be until the extension is installed and enabled.",
      actions: (
        <>
          <Btn size="sm" variant="primary" onClick={onInstallExt}>Install extension</Btn>
          <Btn size="sm" variant="ghost" onClick={onDiagnostics}>Details</Btn>
        </>
      ),
    },
  };
  const c = configs[service];
  if (!c) return null;
  const toneColors = {
    neutral: { bg: "var(--bg-primary)", border: "var(--border)", iconColor: "var(--text-secondary)" },
    warning: { bg: "var(--warning-soft)", border: "#EFDCB2", iconColor: "var(--warning)" },
    error: { bg: "var(--error-soft)", border: "#F2CBD2", iconColor: "var(--error)" },
  };
  const t = toneColors[c.tone];
  return (
    <div style={{
      display: "flex", alignItems: "center", gap: 14, padding: "14px 18px",
      borderRadius: 16, background: t.bg, border: `1px solid ${t.border}`,
      marginBottom: 24, animation: "sm-slide-up 0.3s ease",
    }}>
      <span style={{ color: t.iconColor, display: "flex" }}>{c.icon}</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 700, fontSize: 14 }}>{c.title}</div>
        <div style={{ color: "var(--text-secondary)", fontSize: 13, marginTop: 2, textWrap: "pretty" }}>{c.body}</div>
      </div>
      {c.actions ? <div style={{ display: "flex", gap: 8, flexShrink: 0 }}>{c.actions}</div> : null}
    </div>
  );
}

/* ---------- Service status mini-card ---------- */
function ServiceStatusCard({ service, onRetry, onDiagnostics }) {
  const ok = service === "connected";
  const rows = [
    { label: "Service", ok: !["down", "auth", "connecting", "connecting-long"].includes(service), pending: service.startsWith("connecting") },
    { label: "Browser extension", ok: !["ext", "down"].includes(service), pending: service.startsWith("connecting") },
  ];
  return (
    <Card pad={20}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 14 }}>
        <span style={{ fontWeight: 700, fontSize: 14 }}>Status</span>
        {ok
          ? <Badge tone="success" icon="check">Connected</Badge>
          : service.startsWith("connecting")
            ? <Badge tone="neutral">Connecting…</Badge>
            : <Badge tone="error">Issue detected</Badge>}
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {rows.map((r) => (
          <div key={r.label} style={{ display: "flex", alignItems: "center", gap: 9, fontSize: 13 }}>
            {r.pending ? <Spinner size={13} color="var(--text-tertiary)" /> : <StatusDot tone={r.ok ? "success" : "error"} pulse={!r.ok} />}
            <span style={{ color: "var(--text-secondary)", flex: 1 }}>{r.label}</span>
            <span style={{ fontWeight: 600, color: r.pending ? "var(--text-tertiary)" : r.ok ? "var(--success)" : "var(--error)" }}>
              {r.pending ? "Checking" : r.ok ? "Running" : "Not connected"}
            </span>
          </div>
        ))}
        <div style={{ display: "flex", alignItems: "center", gap: 9, fontSize: 13 }}>
          <Icon name="history" size={13} style={{ color: "var(--text-tertiary)" }} />
          <span style={{ color: "var(--text-secondary)", flex: 1 }}>Last check</span>
          <span style={{ color: "var(--text-tertiary)", fontWeight: 600 }}>{ok ? "Just now" : "2 min ago"}</span>
        </div>
      </div>
      {!ok && !service.startsWith("connecting") ? (
        <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
          <Btn size="sm" onClick={onRetry} style={{ flex: 1 }}>Retry</Btn>
          <Btn size="sm" variant="ghost" onClick={onDiagnostics} style={{ flex: 1 }}>Diagnostics</Btn>
        </div>
      ) : null}
    </Card>
  );
}

/* ---------- Active session card — 3 timer variants ---------- */
function ActiveSessionCard({ S, big }) {
  const { session, now, timerVariant } = S;
  const [shake, setShake] = React.useState(false);
  const [lockMsg, setLockMsg] = React.useState(false);
  if (!session) return null;
  const totalSec = session.durationMins * 60;
  const leftSec = Math.max(0, Math.round((session.endsAt - now) / 1000));
  const progress = 1 - leftSec / totalSec;
  const blockedApps = RUNNING_APPS.filter((a) => session.apps.includes(a.id));

  const tryStop = () => {
    if (session.strict) {
      setShake(true); setLockMsg(true);
      setTimeout(() => setShake(false), 500);
      setTimeout(() => setLockMsg(false), 3200);
    } else {
      S.requestStop();
    }
  };

  const chips = (light) => (
    <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
      {blockedApps.slice(0, 4).map((a) => (
        <span key={a.id} style={{
          display: "inline-flex", alignItems: "center", gap: 6, padding: "4px 10px 4px 5px",
          borderRadius: 99, fontSize: 12, fontWeight: 600,
          background: light ? "rgba(255,255,255,0.10)" : "var(--bg-secondary)",
          color: light ? "rgba(255,255,255,0.92)" : "var(--text-primary)",
        }}>
          <AppTile name={a.name} color={a.color} size={18} radius={6} />{a.name}
        </span>
      ))}
      {session.sites.length > 0 ? (
        <span style={{
          padding: "4px 10px", borderRadius: 99, fontSize: 12, fontWeight: 600,
          background: light ? "rgba(255,255,255,0.10)" : "var(--bg-secondary)",
          color: light ? "rgba(255,255,255,0.75)" : "var(--text-secondary)",
        }}>+ {session.sites.length} websites</span>
      ) : null}
    </div>
  );

  const stopBtn = (light) => (
    <div style={{ position: "relative", display: "inline-flex", flexDirection: "column", alignItems: "center", gap: 8 }}>
      {session.strict ? (
        <button onClick={tryStop} className={shake ? "sm-shake" : ""} style={{
          display: "inline-flex", alignItems: "center", gap: 8, padding: "10px 18px",
          borderRadius: 12, fontSize: 13.5, fontWeight: 600, border: "none", cursor: "not-allowed",
          background: light ? "rgba(255,255,255,0.12)" : "var(--bg-tertiary)",
          color: light ? "rgba(255,255,255,0.65)" : "var(--text-tertiary)",
        }}>
          <Icon name="lock" size={15} /> Locked until {fmtEndsAt(session.endsAt)}
        </button>
      ) : (
        <Btn variant={light ? "secondary" : "ghost"} icon="stop" onClick={tryStop} danger>Stop session</Btn>
      )}
      {lockMsg ? (
        <div style={{
          position: "absolute", top: "calc(100% + 6px)", whiteSpace: "nowrap", zIndex: 5,
          background: "var(--text-primary)", color: "#fff", fontSize: 12, fontWeight: 600,
          padding: "7px 12px", borderRadius: 9, animation: "sm-pop-in 0.2s ease",
        }}>Strict mode can't be stopped before the timer ends.</div>
      ) : null}
    </div>
  );

  const header = (light) => (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", width: "100%" }}>
      <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
        <StatusDot tone={light ? "accent" : "success"} pulse />
        <span style={{ fontWeight: 700, fontSize: 14, color: light ? "#fff" : "var(--text-primary)" }}>{session.name}</span>
      </div>
      {session.strict
        ? <Badge tone={light ? "accent" : "strict"} icon="lock">STRICT</Badge>
        : <Badge tone={light ? "accent" : "neutral"}>NORMAL</Badge>}
    </div>
  );

  /* --- Variant: ring --- */
  if (timerVariant === "ring") {
    return (
      <Card pad={28} style={{ border: session.strict ? "1.5px solid var(--text-primary)" : "1px solid var(--accent-soft-2)", animation: "sm-pop-in 0.3s ease" }}>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 18 }}>
          {header(false)}
          <ProgressRing size={big ? 210 : 178} stroke={11} progress={progress}
            color={session.strict ? "var(--text-primary)" : "var(--accent)"}
            trackColor={session.strict ? "var(--bg-tertiary)" : "var(--accent-soft-2)"}>
            <div style={{ fontSize: big ? 40 : 34, fontWeight: 700, letterSpacing: "-0.03em", fontVariantNumeric: "tabular-nums" }}>{fmtCountdown(leftSec)}</div>
            <div style={{ fontSize: 12, color: "var(--text-tertiary)", fontWeight: 600, marginTop: 2 }}>remaining</div>
          </ProgressRing>
          <div style={{ fontSize: 13, color: "var(--text-secondary)", fontWeight: 600 }}>
            StopMe is protecting your focus until {fmtEndsAt(session.endsAt)}
          </div>
          {chips(false)}
          {stopBtn(false)}
        </div>
      </Card>
    );
  }

  /* --- Variant: bar (big numbers + progress bar) --- */
  if (timerVariant === "bar") {
    return (
      <Card pad={28} style={{ border: session.strict ? "1.5px solid var(--text-primary)" : "1px solid var(--accent-soft-2)", animation: "sm-pop-in 0.3s ease" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {header(false)}
          <div>
            <div style={{ fontFamily: "var(--font-serif)", fontSize: big ? 76 : 62, lineHeight: 1, letterSpacing: "-0.01em", fontVariantNumeric: "tabular-nums" }}>
              {fmtCountdown(leftSec)}
            </div>
            <div style={{ fontSize: 13, color: "var(--text-secondary)", fontWeight: 600, marginTop: 8 }}>
              remaining · ends at {fmtEndsAt(session.endsAt)}
            </div>
          </div>
          <div style={{ height: 8, borderRadius: 99, background: session.strict ? "var(--bg-tertiary)" : "var(--accent-soft-2)", overflow: "hidden" }}>
            <div style={{
              height: "100%", borderRadius: 99, width: `${progress * 100}%`,
              background: session.strict ? "var(--text-primary)" : "var(--accent)",
              transition: "width 0.8s ease",
            }}></div>
          </div>
          {chips(false)}
          <div>{stopBtn(false)}</div>
        </div>
      </Card>
    );
  }

  /* --- Variant: dark (immersive) --- */
  return (
    <div style={{
      borderRadius: "var(--radius-card)", padding: 28, position: "relative", overflow: "hidden",
      background: session.strict
        ? "linear-gradient(145deg, #17151F 0%, #211D30 100%)"
        : "linear-gradient(145deg, #221C45 0%, #3A2E7E 100%)",
      boxShadow: "var(--shadow-raised)", animation: "sm-pop-in 0.3s ease",
    }}>
      <div style={{
        position: "absolute", top: -70, right: -70, width: 230, height: 230, borderRadius: "50%",
        background: "radial-gradient(circle, rgba(155,141,240,0.35), transparent 70%)",
        animation: "sm-breathe 5s ease-in-out infinite",
      }}></div>
      <div style={{ position: "relative", display: "flex", flexDirection: "column", gap: 16 }}>
        {header(true)}
        <div>
          <div style={{ fontFamily: "var(--font-serif)", fontSize: big ? 80 : 64, lineHeight: 1, color: "#fff", fontVariantNumeric: "tabular-nums" }}>
            {fmtCountdown(leftSec)}
          </div>
          <div style={{ fontSize: 13, color: "rgba(255,255,255,0.65)", fontWeight: 600, marginTop: 8 }}>
            StopMe is protecting your focus · ends at {fmtEndsAt(session.endsAt)}
          </div>
        </div>
        <div style={{ height: 7, borderRadius: 99, background: "rgba(255,255,255,0.14)", overflow: "hidden" }}>
          <div style={{ height: "100%", borderRadius: 99, width: `${progress * 100}%`, background: "#fff", transition: "width 0.8s ease" }}></div>
        </div>
        {chips(true)}
        <div>{stopBtn(true)}</div>
      </div>
    </div>
  );
}

/* ---------- No active session card ---------- */
function NoSessionCard({ S }) {
  return (
    <Card pad={24} style={{ background: "var(--bg-secondary)", boxShadow: "none", border: "1px dashed var(--border-strong)" }}>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", gap: 4, padding: "14px 0" }}>
        <div style={{
          width: 46, height: 46, borderRadius: 14, background: "var(--bg-primary)", border: "1px solid var(--border)",
          display: "flex", alignItems: "center", justifyContent: "center", color: "var(--text-tertiary)", marginBottom: 8,
        }}><Icon name="moon" size={21} /></div>
        <div style={{ fontWeight: 700, fontSize: 14.5 }}>No active blocking session</div>
        <p style={{ color: "var(--text-secondary)", fontSize: 13, maxWidth: 240, textWrap: "pretty" }}>
          Choose what to block and start a session.
        </p>
      </div>
    </Card>
  );
}

/* ---------- Selection summary chips ---------- */
function SummaryChips({ S, center }) {
  const items = [
    { icon: "apps", label: `${S.selectedApps.length} app${S.selectedApps.length === 1 ? "" : "s"}`, empty: S.selectedApps.length === 0, page: "apps" },
    { icon: "globe", label: `${S.totalSites} website${S.totalSites === 1 ? "" : "s"}`, empty: S.totalSites === 0, page: "websites" },
    { icon: "clock", label: fmtDuration(S.duration), page: null },
    { icon: S.strict ? "lock" : "unlock", label: S.strict ? "Strict" : "Normal", page: null },
  ];
  return (
    <div style={{ display: "flex", gap: 8, flexWrap: "wrap", justifyContent: center ? "center" : "flex-start" }}>
      {items.map((it, i) => (
        <button key={i} onClick={it.page ? () => S.nav(it.page) : undefined}
          style={{
            display: "inline-flex", alignItems: "center", gap: 7, padding: "7px 14px",
            borderRadius: 99, fontSize: 13, fontWeight: 600,
            background: "var(--bg-primary)", border: "1px solid var(--border)",
            color: it.empty ? "var(--text-tertiary)" : "var(--text-primary)",
            whiteSpace: "nowrap",
            cursor: it.page ? "pointer" : "default",
          }}>
          <Icon name={it.icon} size={14} style={{ color: it.empty ? "var(--text-tertiary)" : "var(--accent)" }} />
          {it.label}
          {it.page ? <Icon name="chevronRight" size={12} style={{ color: "var(--text-tertiary)" }} /> : null}
        </button>
      ))}
    </div>
  );
}

/* ---------- Hero block (start flow) ---------- */
function HeroBlock({ S, center }) {
  const nothingSelected = S.selectedApps.length === 0 && S.totalSites === 0;
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20, alignItems: center ? "center" : "flex-start", textAlign: center ? "center" : "left" }}>
      <div>
        <h1 style={{ fontFamily: "var(--font-serif)", fontSize: center ? 44 : 38, fontWeight: 400, lineHeight: 1.08, letterSpacing: "-0.01em", marginBottom: 12 }}>
          What do you want to block?
        </h1>
        <p style={{ color: "var(--text-secondary)", fontSize: 15, lineHeight: 1.5, maxWidth: 460, marginInline: center ? "auto" : 0, textWrap: "pretty" }}>
          Select apps and websites, choose a duration, and StopMe will keep them blocked.
        </p>
      </div>
      <SummaryChips S={S} center={center} />
      <div style={{ display: "flex", flexDirection: "column", gap: 8, alignItems: center ? "center" : "flex-start" }}>
        <span style={{ fontSize: 12, fontWeight: 700, color: "var(--text-tertiary)", letterSpacing: "0.06em", textTransform: "uppercase" }}>Duration</span>
        <DurationSelector mins={S.duration} onChange={S.setDuration} />
      </div>
      <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap", justifyContent: center ? "center" : "flex-start" }}>
        <Btn variant="primary" size="lg" icon="play" onClick={S.requestStart} disabled={S.service !== "connected" && S.service !== "ext"}>
          Start Blocking
        </Btn>
        <Btn size="lg" onClick={S.openCreateSession}>Create a custom session</Btn>
        <StrictToggle on={S.strict} onChange={S.setStrict} />
      </div>
      {nothingSelected ? (
        <div style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, color: "var(--text-secondary)" }}>
          <Icon name="info" size={14} style={{ color: "var(--accent)" }} />
          Nothing selected yet — pick apps or websites first, or start with your saved blocklist.
        </div>
      ) : null}
    </div>
  );
}

/* ---------- Dashboard page (3 variants) ---------- */
function DashboardPage({ S }) {
  const banner = (
    <ServiceBanner service={S.service} retrying={S.retrying}
      onRetry={S.retryService} onDiagnostics={S.openDiagnostics} onInstallExt={() => S.nav("settings")} />
  );

  /* Variant B — split: hero left, session/status right */
  if (S.dashVariant === "split") {
    return (
      <div>
        {banner}
        <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: 24, alignItems: "start" }}>
          <Card pad={36}>
            <HeroBlock S={S} />
          </Card>
          <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            {S.session ? <ActiveSessionCard S={S} /> : <NoSessionCard S={S} />}
            <ServiceStatusCard service={S.service} onRetry={S.retryService} onDiagnostics={S.openDiagnostics} />
          </div>
        </div>
      </div>
    );
  }

  /* Variant C — calm: everything centered, minimal */
  if (S.dashVariant === "calm") {
    return (
      <div style={{ maxWidth: 640, margin: "0 auto" }}>
        {banner}
        {S.session ? (
          <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <ActiveSessionCard S={S} big />
            <div style={{ display: "flex", justifyContent: "center" }}>
              <ServiceInlineStatus service={S.service} onDiagnostics={S.openDiagnostics} />
            </div>
          </div>
        ) : (
          <div style={{ paddingTop: 28 }}>
            <HeroBlock S={S} center />
            <div style={{ display: "flex", justifyContent: "center", marginTop: 36 }}>
              <ServiceInlineStatus service={S.service} onDiagnostics={S.openDiagnostics} />
            </div>
          </div>
        )}
      </div>
    );
  }

  /* Variant A (default) — hero card on top, session + status below */
  return (
    <div>
      {banner}
      <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
        {S.session ? (
          <div style={{ display: "grid", gridTemplateColumns: "1.4fr 1fr", gap: 24, alignItems: "start" }}>
            <ActiveSessionCard S={S} big />
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <Card pad={24}>
                <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>Up next</div>
                {SCHEDULED_SESSIONS.map((s) => (
                  <div key={s.id} style={{ display: "flex", alignItems: "center", gap: 10, fontSize: 13 }}>
                    <Icon name="calendar" size={15} style={{ color: "var(--accent)" }} />
                    <span style={{ fontWeight: 600 }}>{s.name}</span>
                    <span style={{ color: "var(--text-secondary)" }}>{s.when}</span>
                  </div>
                ))}
              </Card>
              <ServiceStatusCard service={S.service} onRetry={S.retryService} onDiagnostics={S.openDiagnostics} />
            </div>
          </div>
        ) : (
          <>
            <Card pad={36} style={{ position: "relative", overflow: "hidden" }}>
              <div style={{
                position: "absolute", top: -90, right: -50, width: 320, height: 320, borderRadius: "50%",
                background: "radial-gradient(circle, var(--accent-soft) 0%, transparent 68%)", pointerEvents: "none",
              }}></div>
              <div style={{ position: "relative" }}>
                <HeroBlock S={S} />
              </div>
            </Card>
            <div style={{ display: "grid", gridTemplateColumns: "1.4fr 1fr", gap: 24, alignItems: "start" }}>
              <NoSessionCard S={S} />
              <ServiceStatusCard service={S.service} onRetry={S.retryService} onDiagnostics={S.openDiagnostics} />
            </div>
          </>
        )}
      </div>
    </div>
  );
}

/* small inline status row for the calm variant */
function ServiceInlineStatus({ service, onDiagnostics }) {
  const ok = service === "connected";
  return (
    <button onClick={onDiagnostics} style={{
      display: "inline-flex", alignItems: "center", gap: 8, padding: "8px 16px",
      borderRadius: 99, border: "1px solid var(--border)", background: "var(--bg-primary)",
      fontSize: 12.5, fontWeight: 600, color: "var(--text-secondary)",
    }}>
      {service.startsWith("connecting")
        ? <Spinner size={13} color="var(--text-tertiary)" />
        : <StatusDot tone={ok ? "success" : "error"} pulse={!ok} />}
      {ok ? "Service connected" : service.startsWith("connecting") ? "Connecting to StopMe service…" : "Service issue — click for details"}
    </button>
  );
}

Object.assign(window, { DashboardPage, ActiveSessionCard, ServiceBanner, ServiceStatusCard, SummaryChips });
