// StopMe — UI primitives & icon set
// Exposes everything on window at the end of the file.
const { useState, useEffect, useRef } = React;

/* ============ Icons (stroke, 1.7px, currentColor) ============ */
function Icon({ name, size = 18, style: iconStyle }) {
  const paths = {
    dashboard: <><rect x="3" y="3" width="7.5" height="7.5" rx="2"/><rect x="13.5" y="3" width="7.5" height="7.5" rx="2"/><rect x="3" y="13.5" width="7.5" height="7.5" rx="2"/><rect x="13.5" y="13.5" width="7.5" height="7.5" rx="2"/></>,
    apps: <><rect x="3.5" y="4.5" width="17" height="13" rx="2.5"/><path d="M3.5 8.5h17"/><circle cx="6.3" cy="6.5" r="0.2"/><circle cx="8.8" cy="6.5" r="0.2"/></>,
    globe: <><circle cx="12" cy="12" r="8.5"/><path d="M3.5 12h17M12 3.5c2.5 2.3 3.8 5.2 3.8 8.5s-1.3 6.2-3.8 8.5c-2.5-2.3-3.8-5.2-3.8-8.5s1.3-6.2 3.8-8.5z"/></>,
    sessions: <><rect x="3.5" y="5" width="17" height="15.5" rx="2.5"/><path d="M3.5 9.5h17M8 3v3.5M16 3v3.5"/></>,
    stats: <><path d="M4 20V10M9.3 20V4M14.6 20v-8M20 20V7"/></>,
    settings: <><circle cx="12" cy="12" r="3.2"/><path d="M12 2.8l1.2 2.4 2.6.5 1.9-1.9 1.5 1.5-1.9 1.9.5 2.6 2.4 1.2-1.2 2.4-2.4 1.2-.5 2.6 1.9 1.9-1.5 1.5-1.9-1.9-2.6.5-1.2 2.4-1.2-2.4-2.6-.5-1.9 1.9-1.5-1.5 1.9-1.9-.5-2.6-2.4-1.2 1.2-2.4 2.4-1.2.5-2.6-1.9-1.9 1.5-1.5 1.9 1.9 2.6-.5z" strokeLinejoin="round"/></>,
    lock: <><rect x="5" y="10.5" width="14" height="10" rx="2.5"/><path d="M8 10.5V7.5a4 4 0 0 1 8 0v3"/><circle cx="12" cy="15.5" r="1.2" fill="currentColor" stroke="none"/></>,
    unlock: <><rect x="5" y="10.5" width="14" height="10" rx="2.5"/><path d="M8 10.5V7.5a4 4 0 0 1 7.8-1.2"/></>,
    play: <path d="M7 5.3v13.4c0 .8.9 1.3 1.6.9l11-6.7c.7-.4.7-1.4 0-1.8l-11-6.7c-.7-.4-1.6.1-1.6.9z" strokeLinejoin="round"/>,
    stop: <rect x="6" y="6" width="12" height="12" rx="2.5"/>,
    search: <><circle cx="11" cy="11" r="6.5"/><path d="M16 16l4.5 4.5"/></>,
    refresh: <><path d="M20 12a8 8 0 1 1-2.5-5.8"/><path d="M20 3.5V7h-3.5"/></>,
    plus: <path d="M12 5v14M5 12h14"/>,
    x: <path d="M6 6l12 12M18 6L6 18"/>,
    check: <path d="M5 12.5l4.5 4.5L19 7"/>,
    chevronRight: <path d="M9 5.5l6.5 6.5L9 18.5"/>,
    chevronDown: <path d="M5.5 9l6.5 6.5L18.5 9"/>,
    clock: <><circle cx="12" cy="12" r="8.5"/><path d="M12 7v5.2l3.4 2"/></>,
    shield: <><path d="M12 3l7.5 3v5.5c0 4.6-3.1 7.9-7.5 9.5-4.4-1.6-7.5-4.9-7.5-9.5V6l7.5-3z" strokeLinejoin="round"/></>,
    shieldCheck: <><path d="M12 3l7.5 3v5.5c0 4.6-3.1 7.9-7.5 9.5-4.4-1.6-7.5-4.9-7.5-9.5V6l7.5-3z" strokeLinejoin="round"/><path d="M8.8 12.2l2.3 2.3 4.2-4.6"/></>,
    alert: <><path d="M12 3.5L21.5 20h-19L12 3.5z" strokeLinejoin="round"/><path d="M12 10v4.5"/><circle cx="12" cy="17.2" r="0.4" fill="currentColor"/></>,
    info: <><circle cx="12" cy="12" r="8.5"/><path d="M12 11v5"/><circle cx="12" cy="8" r="0.4" fill="currentColor"/></>,
    zap: <path d="M13 3L5 13.5h6L11 21l8-10.5h-6L13 3z" strokeLinejoin="round"/>,
    calendar: <><rect x="3.5" y="5" width="17" height="15.5" rx="2.5"/><path d="M3.5 9.5h17M8 3v3.5M16 3v3.5"/><circle cx="8" cy="13.5" r="0.5" fill="currentColor"/><circle cx="12" cy="13.5" r="0.5" fill="currentColor"/><circle cx="16" cy="13.5" r="0.5" fill="currentColor"/></>,
    repeat: <><path d="M4 9.5a4 4 0 0 1 4-4h11"/><path d="M16 2.5l3 3-3 3"/><path d="M20 14.5a4 4 0 0 1-4 4H5"/><path d="M8 21.5l-3-3 3-3"/></>,
    history: <><circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/></>,
    download: <path d="M12 4v11M7 11l5 5 5-5M5 20h14"/>,
    trash: <><path d="M4.5 6.5h15M9.5 6V4.5a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1V6M6.5 6.5l.8 12.5a1.5 1.5 0 0 0 1.5 1.4h6.4a1.5 1.5 0 0 0 1.5-1.4l.8-12.5"/></>,
    external: <><path d="M14 4h6v6"/><path d="M20 4l-9 9"/><path d="M19 13.5V18a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h4.5"/></>,
    flame: <path d="M12 3c1 3-3.5 4.8-3.5 9a5.5 5.5 0 0 0 11 0c0-2-1-3.6-2-4.7-.3 1-.8 1.8-1.7 2.3.4-2.8-1.3-5.6-3.8-6.6z" strokeLinejoin="round"/>,
    monitor: <><rect x="3" y="4.5" width="18" height="12.5" rx="2"/><path d="M9 21h6M12 17.5V21"/></>,
    puzzle: <><path d="M9.5 4.5a2 2 0 1 1 4 0H17a1.5 1.5 0 0 1 1.5 1.5v3.5a2 2 0 1 0 0 4V17a1.5 1.5 0 0 1-1.5 1.5h-3.5a2 2 0 1 0-4 0H6A1.5 1.5 0 0 1 4.5 17v-3.5a2 2 0 1 1 0-4V6A1.5 1.5 0 0 1 6 4.5h3.5z" strokeLinejoin="round"/></>,
    file: <><path d="M13.5 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8.5L13.5 3z" strokeLinejoin="round"/><path d="M13.5 3v5.5H19"/></>,
    pause: <path d="M8.5 5.5v13M15.5 5.5v13"/>,
    edit: <path d="M16.5 4.5l3 3L8 19l-4 1 1-4L16.5 4.5z" strokeLinejoin="round"/>,
    folder: <path d="M3.5 7A1.5 1.5 0 0 1 5 5.5h4.5l2 2.5H19A1.5 1.5 0 0 1 20.5 9.5V17A1.5 1.5 0 0 1 19 18.5H5A1.5 1.5 0 0 1 3.5 17V7z" strokeLinejoin="round"/>,
    minus: <path d="M5 12h14"/>,
    maximize: <rect x="5.5" y="5.5" width="13" height="13" rx="1"/>,
    moon: <path d="M20 13.5A8 8 0 0 1 10.5 4 8 8 0 1 0 20 13.5z" strokeLinejoin="round"/>,
    help: <><circle cx="12" cy="12" r="8.5"/><path d="M9.5 9.2A2.6 2.6 0 0 1 12 7.5c1.4 0 2.5 1 2.5 2.2 0 1.7-2.5 1.9-2.5 3.6"/><circle cx="12" cy="16.6" r="0.4" fill="currentColor"/></>,
    sparkle: <path d="M12 4l1.8 5.2L19 11l-5.2 1.8L12 18l-1.8-5.2L5 11l5.2-1.8L12 4z" strokeLinejoin="round"/>,
  };
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth="1.7" strokeLinecap="round" style={{ flexShrink: 0, ...iconStyle }}>
      {paths[name] || <circle cx="12" cy="12" r="8" />}
    </svg>
  );
}

/* ============ Buttons ============ */
function Btn({ variant = "secondary", size = "md", icon, children, disabled, danger, onClick, style: btnStyle, className }) {
  const [hover, setHover] = useState(false);
  const pad = size === "lg" ? "13px 24px" : size === "sm" ? "7px 14px" : "10px 18px";
  const fs = size === "lg" ? 15 : size === "sm" ? 13 : 14;
  let bg = "var(--bg-primary)", color = "var(--text-primary)", border = "1px solid var(--border-strong)", shadow = "none";
  if (variant === "primary") {
    bg = danger ? (hover ? "#B83549" : "var(--error)") : hover ? "var(--accent-hover)" : "var(--accent)";
    color = "#fff"; border = "1px solid transparent";
    shadow = hover ? "0 4px 14px rgba(109,90,230,0.35)" : "0 2px 8px rgba(109,90,230,0.25)";
  } else if (variant === "dark") {
    bg = hover ? "#2A2735" : "var(--text-primary)"; color = "#fff"; border = "1px solid transparent";
  } else if (variant === "secondary") {
    bg = hover ? "var(--bg-secondary)" : "var(--bg-primary)";
    if (danger) { color = "var(--error)"; }
  } else if (variant === "ghost") {
    bg = hover ? "var(--bg-secondary)" : "transparent"; border = "1px solid transparent";
    color = danger ? "var(--error)" : "var(--text-secondary)";
  } else if (variant === "soft") {
    bg = hover ? "var(--accent-soft-2)" : "var(--accent-soft)"; color = "var(--accent-text)"; border = "1px solid transparent";
  }
  return (
    <button onClick={onClick} disabled={disabled} className={className}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        display: "inline-flex", alignItems: "center", justifyContent: "center", gap: 8,
        padding: pad, fontSize: fs, fontWeight: 600, borderRadius: "var(--radius-btn)",
        background: bg, color, border, boxShadow: shadow,
        opacity: disabled ? 0.45 : 1, pointerEvents: disabled ? "none" : "auto",
        transition: "all 0.15s ease", whiteSpace: "nowrap", ...btnStyle,
      }}>
      {icon ? <Icon name={icon} size={size === "sm" ? 15 : 17} /> : null}
      {children}
    </button>
  );
}

/* ============ Toggle ============ */
function Toggle({ on, onChange, accent = "var(--accent)", disabled }) {
  return (
    <button onClick={() => !disabled && onChange(!on)} aria-pressed={on}
      style={{
        width: 42, height: 24, borderRadius: 99, border: "none", position: "relative",
        background: on ? accent : "#D8D5E4", transition: "background 0.18s ease",
        opacity: disabled ? 0.5 : 1, flexShrink: 0,
      }}>
      <span style={{
        position: "absolute", top: 3, left: on ? 21 : 3, width: 18, height: 18,
        borderRadius: 99, background: "#fff", boxShadow: "0 1px 3px rgba(0,0,0,0.25)",
        transition: "left 0.18s ease",
      }}></span>
    </button>
  );
}

/* ============ Pill (selectable) ============ */
function Pill({ selected, onClick, children, icon, count }) {
  const [hover, setHover] = useState(false);
  return (
    <button onClick={onClick}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        display: "inline-flex", alignItems: "center", gap: 7,
        padding: "8px 16px", borderRadius: 999, fontSize: 13.5, fontWeight: 600,
        border: selected ? "1px solid var(--accent)" : "1px solid var(--border-strong)",
        background: selected ? "var(--accent-soft)" : hover ? "var(--bg-secondary)" : "var(--bg-primary)",
        color: selected ? "var(--accent-text)" : "var(--text-primary)",
        transition: "all 0.15s ease",
      }}>
      {icon ? <Icon name={icon} size={15} /> : null}
      {children}
      {count != null ? (
        <span style={{
          fontSize: 11, fontWeight: 700, padding: "1px 7px", borderRadius: 99,
          background: selected ? "var(--accent-soft-2)" : "var(--bg-tertiary)",
          color: selected ? "var(--accent-text)" : "var(--text-secondary)",
        }}>{count}</span>
      ) : null}
    </button>
  );
}

/* ============ Badge ============ */
function Badge({ tone = "neutral", icon, children }) {
  const tones = {
    neutral: { bg: "var(--bg-tertiary)", color: "var(--text-secondary)" },
    accent: { bg: "var(--accent-soft)", color: "var(--accent-text)" },
    success: { bg: "var(--success-soft)", color: "var(--success)" },
    warning: { bg: "var(--warning-soft)", color: "var(--warning)" },
    error: { bg: "var(--error-soft)", color: "var(--error)" },
    strict: { bg: "var(--text-primary)", color: "#fff" },
  };
  const t = tones[tone] || tones.neutral;
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 5, padding: "3px 9px",
      borderRadius: 99, fontSize: "var(--fs-badge)", fontWeight: 700,
      letterSpacing: "0.02em", background: t.bg, color: t.color, whiteSpace: "nowrap",
    }}>
      {icon ? <Icon name={icon} size={12} /> : null}
      {children}
    </span>
  );
}

/* ============ Card ============ */
function Card({ children, pad = 24, style: cardStyle, onClick, hoverable }) {
  const [hover, setHover] = useState(false);
  return (
    <div onClick={onClick}
      onMouseEnter={() => hoverable && setHover(true)} onMouseLeave={() => hoverable && setHover(false)}
      style={{
        background: "var(--bg-primary)", borderRadius: "var(--radius-card)",
        border: "1px solid var(--border)", boxShadow: hover ? "var(--shadow-raised)" : "var(--shadow-card)",
        padding: pad, transition: "box-shadow 0.18s ease, transform 0.18s ease",
        transform: hover ? "translateY(-1px)" : "none",
        cursor: onClick ? "pointer" : "default", ...cardStyle,
      }}>
      {children}
    </div>
  );
}

/* ============ Inputs ============ */
function TextInput({ value, onChange, placeholder, icon, onKeyDown, autoFocus, style: inputStyle, mono }) {
  const [focus, setFocus] = useState(false);
  return (
    <div style={{
      display: "flex", alignItems: "center", gap: 10, padding: "0 14px",
      height: 42, borderRadius: "var(--radius-input)", background: "var(--bg-primary)",
      border: focus ? "1.5px solid var(--accent)" : "1px solid var(--border-strong)",
      boxShadow: focus ? "0 0 0 3px var(--accent-soft)" : "none",
      transition: "all 0.15s ease", ...inputStyle,
    }}>
      {icon ? <span style={{ color: "var(--text-tertiary)", display: "flex" }}><Icon name={icon} size={16} /></span> : null}
      <input value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder}
        onKeyDown={onKeyDown} autoFocus={autoFocus}
        onFocus={() => setFocus(true)} onBlur={() => setFocus(false)}
        style={{
          border: "none", outline: "none", background: "transparent", flex: 1,
          fontSize: 14, color: "var(--text-primary)",
          fontFamily: mono ? "Consolas, monospace" : "inherit",
        }} />
    </div>
  );
}

/* ============ Status dot ============ */
function StatusDot({ tone = "success", pulse }) {
  const colors = { success: "var(--success)", warning: "var(--warning)", error: "var(--error)", neutral: "var(--text-tertiary)", accent: "var(--accent)" };
  return (
    <span style={{
      width: 8, height: 8, borderRadius: 99, background: colors[tone], flexShrink: 0,
      animation: pulse ? "sm-pulse-dot 1.6s ease infinite" : "none",
      display: "inline-block",
    }}></span>
  );
}

/* ============ Spinner ============ */
function Spinner({ size = 18, color = "var(--accent)" }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
      style={{ animation: "sm-spin 0.9s linear infinite", flexShrink: 0 }}>
      <circle cx="12" cy="12" r="9" stroke="var(--border-strong)" strokeWidth="2.5" />
      <path d="M12 3a9 9 0 0 1 9 9" stroke={color} strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  );
}

/* ============ App icon tile (placeholder for real app icons) ============ */
function AppTile({ name, color, size = 36, radius }) {
  const letter = (name || "?").charAt(0).toUpperCase();
  return (
    <div style={{
      width: size, height: size, borderRadius: radius != null ? radius : Math.round(size * 0.28),
      background: color || "var(--bg-tertiary)",
      display: "flex", alignItems: "center", justifyContent: "center",
      color: "#fff", fontWeight: 700, fontSize: Math.round(size * 0.42),
      flexShrink: 0, letterSpacing: "-0.02em",
    }}>{letter}</div>
  );
}

/* ============ Section heading ============ */
function SectionTitle({ children, right, style: stStyle }) {
  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 14, ...stStyle }}>
      <h2 style={{ fontSize: "var(--fs-section-title)", fontWeight: 700, letterSpacing: "-0.01em" }}>{children}</h2>
      {right || null}
    </div>
  );
}

/* ============ Page header ============ */
function PageHeader({ title, subtitle, right }) {
  return (
    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16, marginBottom: 28 }}>
      <div>
        <h1 style={{ fontSize: "var(--fs-page-title)", fontWeight: 700, letterSpacing: "-0.02em", marginBottom: 6 }}>{title}</h1>
        {subtitle ? <p style={{ color: "var(--text-secondary)", fontSize: 14.5, maxWidth: 520, textWrap: "pretty" }}>{subtitle}</p> : null}
      </div>
      {right ? <div style={{ flexShrink: 0, display: "flex", gap: 10 }}>{right}</div> : null}
    </div>
  );
}

/* ============ Empty state ============ */
function EmptyState({ icon = "info", title, body, action }) {
  return (
    <div style={{
      display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
      textAlign: "center", padding: "44px 24px", gap: 6,
    }}>
      <div style={{
        width: 52, height: 52, borderRadius: 16, background: "var(--bg-secondary)",
        display: "flex", alignItems: "center", justifyContent: "center",
        color: "var(--text-tertiary)", marginBottom: 8,
      }}>
        <Icon name={icon} size={24} />
      </div>
      <div style={{ fontWeight: 700, fontSize: 15 }}>{title}</div>
      {body ? <p style={{ color: "var(--text-secondary)", fontSize: 13.5, maxWidth: 340, textWrap: "pretty" }}>{body}</p> : null}
      {action ? <div style={{ marginTop: 14 }}>{action}</div> : null}
    </div>
  );
}

/* ============ Duration selector ============ */
const DURATIONS = [
  { label: "15 min", mins: 15 },
  { label: "30 min", mins: 30 },
  { label: "1 h", mins: 60 },
  { label: "2 h", mins: 120 },
];
function DurationSelector({ mins, onChange, compact }) {
  const [customOpen, setCustomOpen] = useState(false);
  const [customVal, setCustomVal] = useState("45");
  const isPreset = DURATIONS.some((d) => d.mins === mins);
  return (
    <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
      {DURATIONS.map((d) => (
        <button key={d.mins} onClick={() => { onChange(d.mins); setCustomOpen(false); }}
          style={{
            padding: compact ? "7px 13px" : "9px 16px", borderRadius: 999,
            fontSize: compact ? 13 : 13.5, fontWeight: 600,
            border: mins === d.mins && !customOpen ? "1px solid var(--accent)" : "1px solid var(--border-strong)",
            background: mins === d.mins && !customOpen ? "var(--accent-soft)" : "var(--bg-primary)",
            color: mins === d.mins && !customOpen ? "var(--accent-text)" : "var(--text-primary)",
            whiteSpace: "nowrap",
            transition: "all 0.15s ease",
          }}>{d.label}</button>
      ))}
      {customOpen ? (
        <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
          <input autoFocus value={customVal}
            onChange={(e) => setCustomVal(e.target.value.replace(/\D/g, ""))}
            onKeyDown={(e) => { if (e.key === "Enter" && +customVal > 0) { onChange(+customVal); setCustomOpen(false); } }}
            style={{
              width: 58, padding: compact ? "7px 10px" : "9px 12px", borderRadius: 999, fontSize: 13.5, fontWeight: 600,
              border: "1.5px solid var(--accent)", outline: "none", textAlign: "center", color: "var(--accent-text)",
            }} />
          <span style={{ fontSize: 12.5, color: "var(--text-secondary)" }}>min</span>
          <button onClick={() => { if (+customVal > 0) { onChange(+customVal); } setCustomOpen(false); }}
            style={{ border: "none", background: "var(--accent)", color: "#fff", borderRadius: 99, width: 26, height: 26, display: "inline-flex", alignItems: "center", justifyContent: "center" }}>
            <Icon name="check" size={13} />
          </button>
        </span>
      ) : (
        <button onClick={() => setCustomOpen(true)}
          style={{
            padding: compact ? "7px 13px" : "9px 16px", borderRadius: 999, fontSize: compact ? 13 : 13.5, fontWeight: 600,
            border: !isPreset ? "1px solid var(--accent)" : "1px dashed var(--border-strong)",
            background: !isPreset ? "var(--accent-soft)" : "transparent",
            color: !isPreset ? "var(--accent-text)" : "var(--text-secondary)",
            whiteSpace: "nowrap",
          }}>{!isPreset ? `${mins} min` : "Custom"}</button>
      )}
    </div>
  );
}

/* ============ Strict mode toggle row ============ */
function StrictToggle({ on, onChange, compact }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
      <span style={{
        display: "flex", alignItems: "center", gap: 7, fontSize: compact ? 13 : 13.5, fontWeight: 600,
        color: on ? "var(--text-primary)" : "var(--text-secondary)",
      }}>
        <Icon name="lock" size={15} style={{ color: on ? "var(--text-primary)" : "var(--text-tertiary)" }} />
        Strict mode
      </span>
      <Toggle on={on} onChange={onChange} accent="var(--text-primary)" />
    </div>
  );
}

/* ============ Progress ring ============ */
function ProgressRing({ size = 180, stroke = 10, progress = 0.4, children, trackColor = "var(--accent-soft-2)", color = "var(--accent)" }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  return (
    <div style={{ position: "relative", width: size, height: size, flexShrink: 0 }}>
      <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={trackColor} strokeWidth={stroke} />
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={color} strokeWidth={stroke}
          strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - progress)}
          style={{ transition: "stroke-dashoffset 0.8s ease" }} />
      </svg>
      <div style={{
        position: "absolute", inset: 0, display: "flex", flexDirection: "column",
        alignItems: "center", justifyContent: "center",
      }}>{children}</div>
    </div>
  );
}

/* ============ Modal shell ============ */
function Modal({ open, onClose, width = 520, children, noPad }) {
  if (!open) return null;
  return (
    <div onClick={onClose} style={{
      position: "fixed", inset: 0, background: "rgba(23,21,31,0.40)", zIndex: 200,
      display: "flex", alignItems: "center", justifyContent: "center",
      animation: "sm-fade-in 0.18s ease", backdropFilter: "blur(2px)",
    }}>
      <div onClick={(e) => e.stopPropagation()} style={{
        width, maxWidth: "calc(100vw - 80px)", maxHeight: "calc(100vh - 80px)", overflowY: "auto",
        background: "var(--bg-primary)", borderRadius: "var(--radius-card-lg)",
        boxShadow: "var(--shadow-modal)", padding: noPad ? 0 : 28,
        animation: "sm-pop-in 0.22s cubic-bezier(0.16, 1, 0.3, 1)",
      }}>{children}</div>
    </div>
  );
}

function ModalHeader({ title, subtitle, onClose, icon, iconTone }) {
  const tones = {
    accent: { bg: "var(--accent-soft)", color: "var(--accent-text)" },
    strict: { bg: "var(--text-primary)", color: "#fff" },
    error: { bg: "var(--error-soft)", color: "var(--error)" },
    warning: { bg: "var(--warning-soft)", color: "var(--warning)" },
  };
  const t = tones[iconTone] || tones.accent;
  return (
    <div style={{ display: "flex", alignItems: "flex-start", gap: 14, marginBottom: 20 }}>
      {icon ? (
        <div style={{
          width: 42, height: 42, borderRadius: 13, background: t.bg, color: t.color,
          display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0,
        }}><Icon name={icon} size={20} /></div>
      ) : null}
      <div style={{ flex: 1 }}>
        <h2 style={{ fontSize: 18, fontWeight: 700, letterSpacing: "-0.01em" }}>{title}</h2>
        {subtitle ? <p style={{ color: "var(--text-secondary)", fontSize: 13.5, marginTop: 4, textWrap: "pretty" }}>{subtitle}</p> : null}
      </div>
      {onClose ? (
        <button onClick={onClose} style={{
          border: "none", background: "transparent", color: "var(--text-tertiary)",
          width: 30, height: 30, borderRadius: 8, display: "flex", alignItems: "center", justifyContent: "center",
        }}><Icon name="x" size={17} /></button>
      ) : null}
    </div>
  );
}

/* ============ Settings row ============ */
function SettingRow({ title, body, control, last }) {
  return (
    <div style={{
      display: "flex", alignItems: "center", justifyContent: "space-between", gap: 24,
      padding: "16px 0", borderBottom: last ? "none" : "1px solid var(--border)",
    }}>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontWeight: 600, fontSize: 14, marginBottom: body ? 3 : 0 }}>{title}</div>
        {body ? <div style={{ color: "var(--text-secondary)", fontSize: 13, textWrap: "pretty" }}>{body}</div> : null}
      </div>
      <div style={{ flexShrink: 0 }}>{control}</div>
    </div>
  );
}

/* ============ Helpers ============ */
function fmtCountdown(totalSec) {
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = Math.floor(totalSec % 60);
  if (h > 0) return `${h}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
  return `${m}:${String(s).padStart(2, "0")}`;
}
function fmtDuration(mins) {
  if (mins < 60) return `${mins} min`;
  const h = Math.floor(mins / 60), m = mins % 60;
  return m ? `${h} h ${m} min` : `${h} h`;
}
function fmtEndsAt(endsAtMs) {
  const d = new Date(endsAtMs);
  let h = d.getHours(), m = String(d.getMinutes()).padStart(2, "0");
  const ampm = h >= 12 ? "PM" : "AM";
  h = h % 12 || 12;
  return `${h}:${m} ${ampm}`;
}

Object.assign(window, {
  Icon, Btn, Toggle, Pill, Badge, Card, TextInput, StatusDot, Spinner, AppTile,
  SectionTitle, PageHeader, EmptyState, DurationSelector, StrictToggle, ProgressRing,
  Modal, ModalHeader, SettingRow, DURATIONS,
  fmtCountdown, fmtDuration, fmtEndsAt,
});
