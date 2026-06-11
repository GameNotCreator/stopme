// StopMe — Sessions, Statistics, Settings pages

/* ============ SESSIONS ============ */
function SessionRow({ icon, name, meta, badges, actions, dim }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 14, padding: "14px 20px", opacity: dim ? 0.62 : 1 }}>
      <div style={{
        width: 38, height: 38, borderRadius: 12, background: "var(--bg-secondary)",
        display: "flex", alignItems: "center", justifyContent: "center", color: "var(--text-secondary)", flexShrink: 0,
      }}><Icon name={icon} size={18} /></div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <span style={{ fontWeight: 700, fontSize: 14 }}>{name}</span>
          {badges}
        </div>
        <div style={{ color: "var(--text-secondary)", fontSize: 12.5, marginTop: 2 }}>{meta}</div>
      </div>
      <div style={{ display: "flex", gap: 6, flexShrink: 0 }}>{actions}</div>
    </div>
  );
}

function SessionsPage({ S }) {
  return (
    <div data-screen-label="Sessions">
      <PageHeader title="Sessions" subtitle="Create, schedule, and review your blocking sessions."
        right={<Btn variant="primary" icon="plus" onClick={S.openCreateSession}>New session</Btn>} />

      <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
        {/* Active */}
        <section>
          <SectionTitle>Active</SectionTitle>
          {S.session ? (
            <Card pad={0}>
              <SessionRow icon="play" name={S.session.name}
                badges={S.session.strict ? <Badge tone="strict" icon="lock">STRICT</Badge> : <Badge tone="success">RUNNING</Badge>}
                meta={`${fmtCountdown(Math.max(0, Math.round((S.session.endsAt - S.now) / 1000)))} remaining · ${S.session.apps.length} apps · ${S.session.sites.length} websites`}
                actions={S.session.strict
                  ? <Btn size="sm" variant="ghost" icon="lock" disabled>Locked</Btn>
                  : <Btn size="sm" variant="ghost" danger icon="stop" onClick={S.requestStop}>Stop</Btn>} />
            </Card>
          ) : (
            <Card pad={0} style={{ background: "var(--bg-secondary)", boxShadow: "none", border: "1px dashed var(--border-strong)" }}>
              <EmptyState icon="moon" title="No active session"
                body="Start one from the Dashboard, or create a custom session here."
                action={<Btn variant="soft" icon="plus" onClick={S.openCreateSession}>Create session</Btn>} />
            </Card>
          )}
        </section>

        {/* Scheduled */}
        <section>
          <SectionTitle>Scheduled</SectionTitle>
          <Card pad={0}>
            {SCHEDULED_SESSIONS.map((s) => (
              <SessionRow key={s.id} icon="calendar" name={s.name}
                badges={s.strict ? <Badge tone="strict" icon="lock">STRICT</Badge> : null}
                meta={`${s.when} · ${fmtDuration(s.duration)} · ${s.apps} apps · ${s.sites} websites`}
                actions={<>
                  <Btn size="sm" variant="ghost" icon="edit">Edit</Btn>
                  <Btn size="sm" variant="ghost" danger icon="trash"></Btn>
                </>} />
            ))}
          </Card>
        </section>

        {/* Recurring */}
        <section>
          <SectionTitle>Recurring</SectionTitle>
          <Card pad={0}>
            {RECURRING_SESSIONS.map((s, i) => (
              <div key={s.id} style={{ borderBottom: i < RECURRING_SESSIONS.length - 1 ? "1px solid var(--border)" : "none" }}>
                <SessionRow icon="repeat" name={s.name} dim={!s.enabled}
                  badges={<>
                    {s.strict ? <Badge tone="strict" icon="lock">STRICT</Badge> : null}
                    {!s.enabled ? <Badge tone="neutral">PAUSED</Badge> : null}
                  </>}
                  meta={`${s.days.join(", ")} · ${s.time} · ${s.apps} apps · ${s.sites} websites`}
                  actions={<Toggle on={s.enabled} onChange={() => {}} />} />
              </div>
            ))}
          </Card>
        </section>

        {/* Past */}
        <section>
          <SectionTitle right={<Btn size="sm" variant="ghost">Clear history</Btn>}>Past sessions</SectionTitle>
          <Card pad={0}>
            {PAST_SESSIONS.map((s, i) => (
              <div key={s.id} style={{ borderBottom: i < PAST_SESSIONS.length - 1 ? "1px solid var(--border)" : "none" }}>
                <SessionRow icon="history" name={s.name} dim
                  badges={s.completed
                    ? <Badge tone="success" icon="check">COMPLETED</Badge>
                    : <Badge tone="warning">STOPPED EARLY</Badge>}
                  meta={`${s.date} · ${s.completed ? fmtDuration(s.duration) : `${fmtDuration(s.stoppedAt)} of ${fmtDuration(s.duration)}`} · ${s.apps} apps · ${s.sites} websites`}
                  actions={<Btn size="sm" variant="ghost" icon="repeat">Repeat</Btn>} />
              </div>
            ))}
          </Card>
        </section>
      </div>
    </div>
  );
}

/* ============ STATISTICS ============ */
function MetricCard({ label, value, sub, icon }) {
  return (
    <Card pad={20}>
      <div style={{ display: "flex", alignItems: "center", gap: 8, color: "var(--text-secondary)", fontSize: 12.5, fontWeight: 600, marginBottom: 10 }}>
        <Icon name={icon} size={14} />{label}
      </div>
      <div style={{ fontSize: 26, fontWeight: 700, letterSpacing: "-0.02em" }}>{value}</div>
      {sub ? <div style={{ fontSize: 12, color: "var(--text-tertiary)", marginTop: 3 }}>{sub}</div> : null}
    </Card>
  );
}

function StatisticsPage({ S }) {
  const maxMins = Math.max(...STATS.focusByDay.map((d) => d.mins));
  const totalBlocks = STATS.blocksByCategory.reduce((n, c) => n + c.count, 0);
  return (
    <div data-screen-label="Statistics">
      <PageHeader title="Statistics" subtitle="What StopMe helped you avoid this week."
        right={<>
          <Btn icon="download" onClick={() => S.toast("Stats exported to stopme-stats.csv")}>Export</Btn>
          <Btn variant="ghost" danger onClick={() => S.toast("Statistics reset")}>Reset</Btn>
        </>} />

      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 16, marginBottom: 24 }}>
        <MetricCard icon="clock" label="Focus time" value={STATS.focusTimeWeek} sub="this week" />
        <MetricCard icon="check" label="Sessions completed" value={STATS.sessionsCompleted} sub="all time" />
        <MetricCard icon="flame" label="Current streak" value={`${STATS.streak} days`} sub="with at least one session" />
        <MetricCard icon="apps" label="App launches blocked" value={STATS.appsBlockedCount} sub="this week" />
        <MetricCard icon="globe" label="Site visits blocked" value={STATS.sitesBlockedCount} sub="this week" />
        <MetricCard icon="alert" label="Top distraction" value={STATS.topDistraction} sub="134 blocked attempts" />
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: 24, alignItems: "start" }}>
        {/* Focus by day */}
        <Card pad={24}>
          <SectionTitle>Focus time by day</SectionTitle>
          <div style={{ display: "flex", alignItems: "flex-end", gap: 14, height: 180, paddingTop: 8 }}>
            {STATS.focusByDay.map((d) => (
              <div key={d.day} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 8, height: "100%", justifyContent: "flex-end" }}>
                <span style={{ fontSize: 11.5, fontWeight: 700, color: d.mins ? "var(--accent-text)" : "var(--text-tertiary)" }}>
                  {d.mins ? `${Math.round(d.mins / 6) / 10}h` : "—"}
                </span>
                <div style={{
                  width: "100%", maxWidth: 44, borderRadius: 8,
                  height: `${Math.max(4, (d.mins / maxMins) * 120)}px`,
                  background: d.mins ? "var(--accent)" : "var(--bg-tertiary)",
                  opacity: d.mins ? 0.5 + 0.5 * (d.mins / maxMins) : 1,
                  transition: "height 0.4s ease",
                }}></div>
                <span style={{ fontSize: 11.5, fontWeight: 600, color: "var(--text-secondary)" }}>{d.day}</span>
              </div>
            ))}
          </div>
        </Card>

        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {/* Blocks by category */}
          <Card pad={20}>
            <SectionTitle>Blocks by category</SectionTitle>
            <div style={{ display: "flex", height: 10, borderRadius: 99, overflow: "hidden", gap: 2, marginBottom: 14 }}>
              {STATS.blocksByCategory.map((c) => (
                <div key={c.label} style={{ width: `${(c.count / totalBlocks) * 100}%`, background: c.color }}></div>
              ))}
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {STATS.blocksByCategory.map((c) => (
                <div key={c.label} style={{ display: "flex", alignItems: "center", gap: 9, fontSize: 13 }}>
                  <span style={{ width: 9, height: 9, borderRadius: 3, background: c.color, flexShrink: 0 }}></span>
                  <span style={{ color: "var(--text-secondary)", flex: 1 }}>{c.label}</span>
                  <span style={{ fontWeight: 700 }}>{c.count}</span>
                </div>
              ))}
            </div>
          </Card>

          {/* Top blocked */}
          <Card pad={20}>
            <SectionTitle>Top blocked</SectionTitle>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {STATS.topBlocked.map((t, i) => (
                <div key={t.name} style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <span style={{ fontSize: 12, fontWeight: 700, color: "var(--text-tertiary)", width: 14 }}>{i + 1}</span>
                  <AppTile name={t.name} color={t.color} size={26} radius={8} />
                  <span style={{ fontWeight: 600, fontSize: 13.5, flex: 1 }}>{t.name}</span>
                  <Badge tone="neutral">{t.kind}</Badge>
                  <span style={{ fontWeight: 700, fontSize: 13, fontVariantNumeric: "tabular-nums" }}>{t.count}</span>
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

/* ============ SETTINGS ============ */
function SettingsPage({ S }) {
  const { useState } = React;
  const [section, setSection] = useState("general");
  const [general, setGeneral] = useState({ boot: true, minimized: false, tray: true });
  const [statsMode, setStatsMode] = useState("readable");
  const sections = [
    { id: "general", label: "General", icon: "settings" },
    { id: "blocking", label: "Blocking", icon: "shield" },
    { id: "privacy", label: "Privacy", icon: "lock" },
    { id: "service", label: "Service", icon: "monitor" },
    { id: "extension", label: "Extension", icon: "puzzle" },
    { id: "about", label: "About", icon: "info" },
  ];
  const serviceOk = S.service === "connected";

  const segBtn = (val, cur, set, label) => (
    <button key={val} onClick={() => set(val)} style={{
      padding: "7px 14px", borderRadius: 9, fontSize: 13, fontWeight: 600, border: "none",
      background: cur === val ? "var(--bg-primary)" : "transparent",
      color: cur === val ? "var(--text-primary)" : "var(--text-secondary)",
      boxShadow: cur === val ? "0 1px 3px rgba(23,21,31,0.12)" : "none",
    }}>{label}</button>
  );

  return (
    <div data-screen-label="Settings">
      <PageHeader title="Settings" />
      <div style={{ display: "grid", gridTemplateColumns: "200px 1fr", gap: 28, alignItems: "start" }}>
        <nav style={{ display: "flex", flexDirection: "column", gap: 2, position: "sticky", top: 0 }}>
          {sections.map((s) => (
            <button key={s.id} onClick={() => setSection(s.id)} style={{
              display: "flex", alignItems: "center", gap: 10, padding: "9px 13px", borderRadius: 11,
              fontSize: 13.5, fontWeight: 600, border: "none", textAlign: "left",
              background: section === s.id ? "var(--bg-primary)" : "transparent",
              color: section === s.id ? "var(--text-primary)" : "var(--text-secondary)",
              boxShadow: section === s.id ? "var(--shadow-card)" : "none",
            }}>
              <Icon name={s.icon} size={16} />{s.label}
              {s.id === "service" && !serviceOk ? <StatusDot tone="error" pulse /> : null}
            </button>
          ))}
        </nav>

        <Card pad={28} style={{ minHeight: 420 }}>
          {section === "general" ? (
            <div>
              <SectionTitle>General</SectionTitle>
              <SettingRow title="Start StopMe at boot" body="Launch automatically when Windows starts."
                control={<Toggle on={general.boot} onChange={(v) => setGeneral({ ...general, boot: v })} />} />
              <SettingRow title="Start minimized" body="Open in the system tray instead of a window."
                control={<Toggle on={general.minimized} onChange={(v) => setGeneral({ ...general, minimized: v })} />} />
              <SettingRow title="Show tray icon" body="Quick access to the active session from the taskbar." last
                control={<Toggle on={general.tray} onChange={(v) => setGeneral({ ...general, tray: v })} />} />
            </div>
          ) : null}

          {section === "blocking" ? (
            <div>
              <SectionTitle>Blocking</SectionTitle>
              <SettingRow title="Default duration" body="Pre-selected when you start a quick session."
                control={<DurationSelector mins={S.duration} onChange={S.setDuration} compact />} />
              <SettingRow title="Default mode" body="Strict mode can't be stopped before the timer ends."
                control={
                  <div style={{ display: "flex", background: "var(--bg-secondary)", borderRadius: 11, padding: 3 }}>
                    {segBtn(false, S.strict, S.setStrict, "Normal")}
                    {segBtn(true, S.strict, S.setStrict, "Strict")}
                  </div>
                } />
              <SettingRow title="Unsupported browsers" body="What to do when a browser without the extension opens a blocked site." last
                control={
                  <select style={{ padding: "9px 12px", borderRadius: 11, border: "1px solid var(--border-strong)", fontSize: 13, fontWeight: 600, background: "var(--bg-primary)" }}>
                    <option>Close the browser tab</option>
                    <option>Block the whole browser</option>
                    <option>Allow (not recommended)</option>
                  </select>
                } />
              <div style={{
                marginTop: 18, padding: "13px 16px", borderRadius: 13, background: "var(--bg-secondary)",
                display: "flex", gap: 10, fontSize: 13, color: "var(--text-secondary)",
              }}>
                <Icon name="lock" size={16} style={{ flexShrink: 0, marginTop: 1 }} />
                <span style={{ textWrap: "pretty" }}><strong style={{ color: "var(--text-primary)" }}>About strict mode.</strong> Once a strict session starts, it cannot be stopped, edited, or bypassed by restarting StopMe or Windows. The timer always finishes.</span>
              </div>
            </div>
          ) : null}

          {section === "privacy" ? (
            <div>
              <SectionTitle>Privacy</SectionTitle>
              <SettingRow title="Statistics mode" body="Control how much detail StopMe keeps about blocked attempts."
                control={
                  <div style={{ display: "flex", background: "var(--bg-secondary)", borderRadius: 11, padding: 3 }}>
                    {segBtn("readable", statsMode, setStatsMode, "Readable")}
                    {segBtn("anon", statsMode, setStatsMode, "Anonymized")}
                    {segBtn("off", statsMode, setStatsMode, "Disabled")}
                  </div>
                } />
              <SettingRow title="Export data" body="Download all your sessions and statistics as CSV."
                control={<Btn size="sm" icon="download" onClick={() => S.toast("Data exported")}>Export</Btn>} />
              <SettingRow title="Delete all data" body="Erase sessions, blocklists and statistics. This can't be undone." last
                control={<Btn size="sm" variant="secondary" danger icon="trash">Delete</Btn>} />
            </div>
          ) : null}

          {section === "service" ? (
            <div>
              <SectionTitle right={serviceOk ? <Badge tone="success" icon="check">All good</Badge> : <Badge tone="error">Issue detected</Badge>}>Service</SectionTitle>
              <SettingRow title="Connection" body={serviceOk ? "App is connected to the background service." : "The app can't reach the background service."}
                control={<span style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 700, fontSize: 13, color: serviceOk ? "var(--success)" : "var(--error)" }}>
                  <StatusDot tone={serviceOk ? "success" : "error"} pulse={!serviceOk} />{serviceOk ? "Connected" : "Disconnected"}
                </span>} />
              <SettingRow title="Background service" body="stopme-svc.exe — handles blocking even when the app is closed."
                control={<span style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 700, fontSize: 13, color: serviceOk ? "var(--success)" : "var(--error)" }}>
                  <StatusDot tone={serviceOk ? "success" : "error"} pulse={!serviceOk} />{serviceOk ? "Running" : "Stopped"}
                </span>} />
              <SettingRow title="Troubleshooting" last
                control={<div style={{ display: "flex", gap: 8 }}>
                  <Btn size="sm" onClick={S.retryService}>Retry connection</Btn>
                  <Btn size="sm" variant="soft" onClick={S.openDiagnostics}>Run diagnostics</Btn>
                  <Btn size="sm" variant="ghost" icon="file">Open logs</Btn>
                </div>} />
            </div>
          ) : null}

          {section === "extension" ? (
            <div>
              <SectionTitle>Browser extension</SectionTitle>
              <p style={{ fontSize: 13.5, color: "var(--text-secondary)", marginBottom: 8, textWrap: "pretty" }}>
                StopMe blocks websites through a lightweight browser extension.
              </p>
              <SettingRow title="Chrome" body="StopMe for Chrome v2.4.1"
                control={S.service === "ext"
                  ? <Btn size="sm" variant="primary" icon="download">Install extension</Btn>
                  : <span style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: 700, fontSize: 13, color: "var(--success)" }}><StatusDot tone="success" />Connected</span>} />
              <SettingRow title="Edge" body="Not installed"
                control={<Btn size="sm" icon="download">Install</Btn>} />
              <SettingRow title="Other browsers" body="Firefox support is coming soon. Unsupported browsers follow your Blocking setting." last
                control={<Btn size="sm" variant="ghost" icon="external">Instructions</Btn>} />
            </div>
          ) : null}

          {section === "about" ? (
            <div>
              <SectionTitle>About</SectionTitle>
              <SettingRow title="Version" control={<span style={{ fontSize: 13, fontWeight: 600, color: "var(--text-secondary)" }}>StopMe 2.1.0 (stable)</span>} />
              <SettingRow title="Data location" control={<span style={{ fontSize: 12.5, fontFamily: "Consolas, monospace", color: "var(--text-secondary)" }}>C:\Users\you\AppData\Roaming\StopMe</span>} />
              <SettingRow title="Check for updates" last control={<Btn size="sm" icon="refresh">Check now</Btn>} />
            </div>
          ) : null}
        </Card>
      </div>
    </div>
  );
}

Object.assign(window, { SessionsPage, StatisticsPage, SettingsPage });
