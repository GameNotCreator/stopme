// StopMe — app shell: titlebar, sidebar (3 variants), routing, global state, tweaks
const { useState, useEffect, useRef } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "dashVariant": "calm",
  "timerVariant": "ring",
  "sidebarVariant": "tinted",
  "serviceState": "connected",
  "accent": "#7C5CFF"
}/*EDITMODE-END*/;

const ACCENT_PRESETS = {
  "#6D5AE6": { hover: "#5B48D6", active: "#4F3DC4", soft: "#EFECFD", soft2: "#E3DEFB", text: "#5747C9" },
  "#7C5CFF": { hover: "#6A4AF0", active: "#5C3CE0", soft: "#F1ECFF", soft2: "#E4DCFF", text: "#6442E8" },
  "#5B4FB8": { hover: "#4C41A6", active: "#413694", soft: "#EDECF7", soft2: "#DEDCF0", text: "#4C41A6" },
};

const NAV_ITEMS = [
  { id: "dashboard", label: "Dashboard", icon: "dashboard" },
  { id: "apps", label: "Block Apps", icon: "apps" },
  { id: "websites", label: "Block Websites", icon: "globe" },
  { id: "sessions", label: "Sessions", icon: "sessions" },
  { id: "stats", label: "Statistics", icon: "stats" },
  { id: "settings", label: "Settings", icon: "settings" },
];

/* ---------- Titlebar (Windows) ---------- */
function Titlebar() {
  return (
    <div style={{
      height: 38, display: "flex", alignItems: "center", justifyContent: "flex-end",
      flexShrink: 0, userSelect: "none",
    }}>
      <div style={{ display: "flex", height: "100%" }}>
        {["minus", "maximize", "x"].map((n) => (
          <div key={n} style={{
            width: 46, display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--text-secondary)",
          }}><Icon name={n} size={n === "maximize" ? 13 : 16} /></div>
        ))}
      </div>
    </div>
  );
}

/* ---------- Logo ---------- */
function Logo({ compact }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 10, padding: compact ? "0" : "0 6px", justifyContent: compact ? "center" : "flex-start" }}>
      <div style={{
        width: 30, height: 30, borderRadius: 9, flexShrink: 0,
        background: "linear-gradient(140deg, var(--accent), var(--accent-active, #4F3DC4))",
        display: "flex", alignItems: "center", justifyContent: "center", color: "#fff",
        boxShadow: "0 2px 8px rgba(109,90,230,0.30)",
      }}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M7.2 3.5h9.6l4.2 4.2v9.6l-4.2 4.2H7.2L3 17.3V7.7l4.2-4.2z" />
        </svg>
      </div>
      {!compact ? <span style={{ fontWeight: 700, fontSize: 17, letterSpacing: "-0.02em" }}>StopMe</span> : null}
    </div>
  );
}

/* ---------- Sidebar (3 variants) ---------- */
function Sidebar({ S }) {
  const v = S.sidebarVariant;
  const rail = v === "rail";
  const leftSec = S.session ? Math.max(0, Math.round((S.session.endsAt - S.now) / 1000)) : 0;
  return (
    <aside style={{
      width: rail ? 68 : 224, flexShrink: 0, display: "flex", flexDirection: "column",
      padding: rail ? "16px 10px" : "16px 14px",
      background: v === "white" ? "var(--bg-primary)" : "transparent",
      borderRight: v === "white" ? "1px solid var(--border)" : "none",
      transition: "width 0.2s ease",
    }}>
      <div style={{ marginBottom: 24, paddingTop: 4 }}><Logo compact={rail} /></div>

      <nav style={{ display: "flex", flexDirection: "column", gap: 3, flex: 1 }}>
        {NAV_ITEMS.map((item) => {
          const active = S.page === item.id;
          return (
            <button key={item.id} onClick={() => S.nav(item.id)} title={rail ? item.label : undefined}
              style={{
                display: "flex", alignItems: "center", gap: 11, justifyContent: rail ? "center" : "flex-start",
                padding: rail ? "11px 0" : "10px 13px", borderRadius: 12, border: "none", textAlign: "left",
                fontSize: 14, fontWeight: 600, position: "relative",
                background: active ? (v === "white" ? "var(--bg-secondary)" : "var(--bg-primary)") : "transparent",
                color: active ? "var(--text-primary)" : "var(--text-secondary)",
                boxShadow: active && v !== "white" ? "var(--shadow-card)" : "none",
                transition: "all 0.15s ease", cursor: "pointer",
              }}
              onMouseEnter={(e) => { if (!active) e.currentTarget.style.color = "var(--text-primary)"; }}
              onMouseLeave={(e) => { if (!active) e.currentTarget.style.color = "var(--text-secondary)"; }}>
              <Icon name={item.icon} size={18} style={{ color: active ? "var(--accent)" : "currentColor" }} />
              {!rail ? item.label : null}
              {item.id === "dashboard" && S.session && !rail ? (
                <span style={{
                  marginLeft: "auto", fontSize: 11, fontWeight: 700, padding: "2px 8px", borderRadius: 99,
                  background: S.session.strict ? "var(--text-primary)" : "var(--accent)", color: "#fff",
                  fontVariantNumeric: "tabular-nums",
                }}>{fmtCountdown(leftSec)}</span>
              ) : null}
              {item.id === "dashboard" && S.session && rail ? (
                <span style={{ position: "absolute", top: 7, right: 9, width: 8, height: 8, borderRadius: 99, background: "var(--accent)", animation: "sm-pulse-dot 1.6s ease infinite" }}></span>
              ) : null}
              {item.id === "settings" && S.service !== "connected" && !S.service.startsWith("connecting") ? (
                <span style={{ marginLeft: rail ? 0 : "auto", position: rail ? "absolute" : "static", top: 7, right: 9 }}><StatusDot tone="error" pulse /></span>
              ) : null}
            </button>
          );
        })}
      </nav>

      {/* bottom: session mini-card + status */}
      {!rail && S.session ? (
        <div onClick={() => S.nav("dashboard")} style={{
          background: S.session.strict ? "var(--text-primary)" : "linear-gradient(140deg, var(--accent), var(--accent-active, #4F3DC4))",
          borderRadius: 16, padding: 14, color: "#fff", marginBottom: 12, cursor: "pointer",
          animation: "sm-pop-in 0.3s ease",
        }}>
          <div style={{ display: "flex", alignItems: "center", gap: 7, fontSize: 11.5, fontWeight: 700, opacity: 0.85, letterSpacing: "0.04em" }}>
            {S.session.strict ? <Icon name="lock" size={12} /> : <StatusDot tone="success" pulse />}
            {S.session.strict ? "STRICT SESSION" : "BLOCKING ACTIVE"}
          </div>
          <div style={{ fontSize: 22, fontWeight: 700, marginTop: 5, fontVariantNumeric: "tabular-nums", letterSpacing: "-0.02em" }}>
            {fmtCountdown(leftSec)}
          </div>
          <div style={{ fontSize: 11.5, opacity: 0.75, marginTop: 2 }}>until {fmtEndsAt(S.session.endsAt)}</div>
        </div>
      ) : null}

      <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
        <button onClick={() => S.openDiagnostics()} title="Service status" style={{
          display: "flex", alignItems: "center", gap: 10, justifyContent: rail ? "center" : "flex-start",
          padding: rail ? "10px 0" : "9px 13px", borderRadius: 12, border: "none",
          background: "transparent", fontSize: 12.5, fontWeight: 600, color: "var(--text-tertiary)", cursor: "pointer",
        }}>
          {S.service.startsWith("connecting")
            ? <Spinner size={13} color="var(--text-tertiary)" />
            : <StatusDot tone={S.service === "connected" ? "success" : "error"} pulse={S.service !== "connected"} />}
          {!rail ? (S.service === "connected" ? "Service connected" : S.service.startsWith("connecting") ? "Connecting…" : "Service issue") : null}
        </button>
        <button title="Help" style={{
          display: "flex", alignItems: "center", gap: 10, justifyContent: rail ? "center" : "flex-start",
          padding: rail ? "10px 0" : "9px 13px", borderRadius: 12, border: "none",
          background: "transparent", fontSize: 13.5, fontWeight: 600, color: "var(--text-secondary)", cursor: "pointer",
        }}>
          <Icon name="help" size={17} />{!rail ? "Help" : null}
        </button>
      </div>
    </aside>
  );
}

/* ---------- Main App ---------- */
function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const [page, setPage] = useState("dashboard");
  const [selectedApps, setSelectedApps] = useState(["discord", "steam", "epic"]);
  const [manualRules, setManualRules] = useState([]);
  const [selectedSites, setSelectedSites] = useState(["youtube.com", "reddit.com"]);
  const [selectedCats, setSelectedCats] = useState(["social"]);
  const [exceptions, setExceptions] = useState(["docs.google.com"]);
  const [duration, setDuration] = useState(60);
  const [strict, setStrict] = useState(false);
  const [session, setSession] = useState(null);
  const [now, setNow] = useState(Date.now());
  const [modal, setModal] = useState(null);
  const [toastMsg, setToastMsg] = useState(null);
  const [blockedToast, setBlockedToast] = useState(false);
  const [blockedPage, setBlockedPage] = useState(false);
  const [svcOverride, setSvcOverride] = useState(null); // local override after retry/fix
  const [connectingLong, setConnectingLong] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const pendingName = useRef(null);

  /* accent → CSS vars */
  useEffect(() => {
    const p = ACCENT_PRESETS[t.accent] || ACCENT_PRESETS["#6D5AE6"];
    const r = document.documentElement.style;
    r.setProperty("--accent", t.accent);
    r.setProperty("--accent-hover", p.hover);
    r.setProperty("--accent-active", p.active);
    r.setProperty("--accent-soft", p.soft);
    r.setProperty("--accent-soft-2", p.soft2);
    r.setProperty("--accent-text", p.text);
  }, [t.accent]);

  /* service state: tweak + override + connecting-long escalation */
  useEffect(() => { setSvcOverride(null); setConnectingLong(false); }, [t.serviceState]);
  useEffect(() => {
    if (t.serviceState === "connecting" && !svcOverride) {
      const timer = setTimeout(() => setConnectingLong(true), 5000);
      return () => clearTimeout(timer);
    }
  }, [t.serviceState, svcOverride]);
  const service = svcOverride || (t.serviceState === "connecting" && connectingLong ? "connecting-long" : t.serviceState);

  /* clock tick */
  useEffect(() => {
    const i = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(i);
  }, []);

  /* session expiry */
  useEffect(() => {
    if (session && session.endsAt <= now) {
      setSession(null);
      localStorage.removeItem("stopme-session");
      setToastMsg("Session complete — nice work!");
      setTimeout(() => setToastMsg(null), 3500);
    }
  }, [now, session]);

  /* restore session from localStorage */
  useEffect(() => {
    try {
      const raw = localStorage.getItem("stopme-session");
      if (raw) {
        const s = JSON.parse(raw);
        if (s.endsAt > Date.now()) setSession(s);
        else localStorage.removeItem("stopme-session");
      }
    } catch (e) {}
  }, []);

  const toast = (msg) => { setToastMsg(msg); setTimeout(() => setToastMsg(null), 2800); };

  const totalSites = selectedSites.length + selectedCats.reduce((n, id) => {
    const c = SITE_CATEGORIES.find((x) => x.id === id);
    return n + (c ? c.sites.length : 0);
  }, 0);

  const startSession = (isStrict, name) => {
    const sessName = name || pendingName.current || "Focus session";
    pendingName.current = null;
    const s = {
      name: sessName, durationMins: duration, strict: isStrict,
      endsAt: Date.now() + duration * 60000,
      apps: [...selectedApps], sites: [
        ...selectedSites,
        ...selectedCats.flatMap((id) => (SITE_CATEGORIES.find((c) => c.id === id) || { sites: [] }).sites),
      ],
    };
    setSession(s);
    localStorage.setItem("stopme-session", JSON.stringify(s));
    setModal(null);
    setPage("dashboard");
    setTimeout(() => setBlockedToast(true), 1600);
    setTimeout(() => setBlockedToast(false), 7000);
  };

  const S = {
    // tweaks
    dashVariant: t.dashVariant, timerVariant: t.timerVariant, sidebarVariant: t.sidebarVariant,
    // state
    page, nav: setPage,
    selectedApps, toggleApp: (id) => setSelectedApps(selectedApps.includes(id) ? selectedApps.filter((x) => x !== id) : [...selectedApps, id]),
    manualRules,
    addManualRule: (r) => { setManualRules([...manualRules, r]); toast(`Rule added: ${r.name}`); },
    removeManualRule: (id) => setManualRules(manualRules.filter((r) => r.id !== id)),
    selectedSites, setSelectedSites,
    selectedCats, toggleCat: (id) => setSelectedCats(selectedCats.includes(id) ? selectedCats.filter((x) => x !== id) : [...selectedCats, id]),
    exceptions, setExceptions,
    totalSites,
    duration, setDuration, strict, setStrict,
    session, now, service, retrying,
    modal, closeModal: () => setModal(null),
    openCreateSession: () => setModal("create-session"),
    openDiagnostics: () => setModal("diagnostics"),
    requestStart: (name) => {
      if (selectedApps.length + manualRules.length + totalSites === 0) { toast("Select apps or websites to block first"); return; }
      if (session) { toast("A session is already running"); return; }
      if (typeof name === "string") pendingName.current = name;
      if (strict) setModal("strict-confirm");
      else startSession(false);
    },
    startSession: (isStrict) => startSession(isStrict),
    requestStop: () => setModal("stop-confirm"),
    stopSession: () => {
      setSession(null); localStorage.removeItem("stopme-session");
      setModal(null); toast("Session stopped");
    },
    retryService: () => {
      setRetrying(true); setSvcOverride("connecting");
      setTimeout(() => { setRetrying(false); setSvcOverride("connected"); toast("Service connected"); }, 1800);
    },
    fixService: () => {
      setSvcOverride("connecting");
      setTimeout(() => { setSvcOverride("connected"); toast("Service connected"); }, 1800);
    },
    toast,
  };

  const pages = {
    dashboard: <DashboardPage S={S} />,
    apps: <BlockAppsPage S={S} />,
    websites: <BlockWebsitesPage S={S} />,
    sessions: <SessionsPage S={S} />,
    stats: <StatisticsPage S={S} />,
    settings: <SettingsPage S={S} />,
  };

  const sessionEnds = session ? fmtEndsAt(session.endsAt) : fmtEndsAt(Date.now() + duration * 60000);

  return (
    <div style={{ height: "100vh", display: "flex", flexDirection: "column", overflow: "hidden" }}>
      <Titlebar />
      <div style={{ flex: 1, display: "flex", minHeight: 0 }}>
        <Sidebar S={S} />
        <main style={{
          flex: 1, overflowY: "auto", minWidth: 0,
          background: S.sidebarVariant === "white" ? "var(--bg-app)" : "var(--bg-primary)",
          borderRadius: S.sidebarVariant === "white" ? 0 : "20px 0 0 0",
          border: S.sidebarVariant === "white" ? "none" : "1px solid var(--border)",
          borderRight: "none", borderBottom: "none",
          boxShadow: S.sidebarVariant === "white" ? "none" : "0 0 24px rgba(23,21,31,0.04)",
        }}>
          <div key={page} style={{
            maxWidth: 1060, margin: "0 auto", padding: "36px 40px 48px",
            animation: "sm-fade-in 0.25s ease",
          }}>
            {S.sidebarVariant === "white"
              ? <div style={{ background: "var(--bg-primary)", borderRadius: "var(--radius-card-lg)", border: "1px solid var(--border)", padding: 32, minHeight: "calc(100vh - 140px)" }}>{pages[page]}</div>
              : pages[page]}
          </div>
        </main>
      </div>

      {/* overlays */}
      <StrictConfirmModal S={S} />
      <StopConfirmModal S={S} />
      <CreateSessionModal S={S} />
      <DiagnosticsModal S={S} />
      <BlockedAppToast visible={blockedToast} onClose={() => setBlockedToast(false)}
        appName={session && session.apps.length ? (RUNNING_APPS.find((a) => a.id === session.apps[0]) || {}).name || "Discord" : "Discord"}
        endsAt={sessionEnds} />
      <BrowserBlockedPage visible={blockedPage} onClose={() => setBlockedPage(false)}
        site={selectedSites[0] || "youtube.com"} endsAt={sessionEnds} />
      <MiniToast msg={toastMsg} />

      {/* Tweaks */}
      <TweaksPanel>
        <TweakSection label="Layout variants" />
        <TweakRadio label="Dashboard" value={t.dashVariant} options={["hero", "split", "calm"]}
          onChange={(v) => setTweak("dashVariant", v)} />
        <TweakRadio label="Session timer" value={t.timerVariant} options={["ring", "bar", "dark"]}
          onChange={(v) => setTweak("timerVariant", v)} />
        <TweakRadio label="Sidebar" value={t.sidebarVariant} options={["tinted", "white", "rail"]}
          onChange={(v) => setTweak("sidebarVariant", v)} />
        <TweakSection label="Brand" />
        <TweakColor label="Accent violet" value={t.accent} options={Object.keys(ACCENT_PRESETS)}
          onChange={(v) => setTweak("accent", v)} />
        <TweakSection label="Simulate states" />
        <TweakSelect label="Service" value={t.serviceState}
          options={[
            { value: "connected", label: "Connected" },
            { value: "connecting", label: "Connecting (stuck)" },
            { value: "down", label: "Service not running" },
            { value: "auth", label: "Authentication issue" },
            { value: "ext", label: "Extension disconnected" },
          ]}
          onChange={(v) => setTweak("serviceState", v)} />
        <TweakButton label="Show blocked-app toast" onClick={() => { setBlockedToast(true); setTimeout(() => setBlockedToast(false), 6000); }} />
        <TweakButton label="Show browser blocked page" onClick={() => setBlockedPage(true)} />
        <TweakButton label="End active session" onClick={() => { setSession(null); localStorage.removeItem("stopme-session"); }} />
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
