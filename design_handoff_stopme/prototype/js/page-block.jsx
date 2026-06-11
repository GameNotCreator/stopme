// StopMe — Block Apps & Block Websites pages

/* ============ Shared sticky action bar ============ */
function StickyActionBar({ S, count, noun, onStart }) {
  return (
    <div style={{
      position: "sticky", bottom: 0, marginTop: 28, zIndex: 50,
      background: "rgba(255,255,255,0.92)", backdropFilter: "blur(14px)",
      border: "1px solid var(--border)", borderRadius: 18, padding: "14px 20px",
      boxShadow: "var(--shadow-raised)",
      display: "flex", alignItems: "center", gap: 18, flexWrap: "wrap",
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, flex: 1, minWidth: 260 }}>
        <span style={{ fontSize: 12, fontWeight: 700, color: "var(--text-tertiary)", letterSpacing: "0.06em", textTransform: "uppercase" }}>Duration</span>
        <DurationSelector mins={S.duration} onChange={S.setDuration} compact />
      </div>
      <StrictToggle on={S.strict} onChange={S.setStrict} compact />
      <Btn variant="primary" icon="play" onClick={onStart} disabled={count === 0 || S.session != null}>
        {S.session ? "Session in progress" : count === 0 ? `Select ${noun} to block` : `Start blocking ${count} ${noun}${count === 1 ? "" : "s"}`}
      </Btn>
    </div>
  );
}

/* ============ BLOCK APPS ============ */
function BlockAppsPage({ S }) {
  const { useState } = React;
  const [query, setQuery] = useState("");
  const [refreshing, setRefreshing] = useState(false);
  const [manualOpen, setManualOpen] = useState(false);
  const [manual, setManual] = useState({ process: "", path: "", folder: "", title: "" });
  const [showEmpty, setShowEmpty] = useState(false);

  const running = showEmpty ? [] : RUNNING_APPS.filter(
    (a) => a.name.toLowerCase().includes(query.toLowerCase()) || a.process.toLowerCase().includes(query.toLowerCase())
  );
  const selected = [
    ...RUNNING_APPS.filter((a) => S.selectedApps.includes(a.id)).map((a) => ({ ...a, rule: "Process" })),
    ...S.manualRules,
  ];

  const refresh = () => {
    setRefreshing(true);
    setTimeout(() => { setRefreshing(false); setShowEmpty(false); }, 900);
  };

  const addManualRule = () => {
    const label = manual.process || manual.title || manual.path.split("\\").pop() || manual.folder.split("\\").pop();
    if (!label) return;
    const rule = manual.process ? "Process" : manual.path ? "Path" : manual.folder ? "Folder" : "Window title";
    S.addManualRule({ id: "m-" + Date.now(), name: label.replace(/\.exe$/i, ""), process: manual.process || manual.title || "—", color: "#6E6A7C", rule });
    setManual({ process: "", path: "", folder: "", title: "" });
    setManualOpen(false);
  };

  return (
    <div data-screen-label="Block Apps">
      <PageHeader
        title="Block Apps"
        subtitle="Choose the applications StopMe should close and prevent from reopening."
        right={<Btn icon="refresh" onClick={refresh}>{refreshing ? "Refreshing…" : "Refresh apps"}</Btn>}
      />

      <div style={{ display: "grid", gridTemplateColumns: "1.6fr 1fr", gap: 24, alignItems: "start" }}>
        {/* --- Running apps --- */}
        <Card pad={0}>
          <div style={{ padding: "18px 20px 14px", borderBottom: "1px solid var(--border)", display: "flex", gap: 12, alignItems: "center" }}>
            <span style={{ fontWeight: 700, fontSize: 15, flexShrink: 0 }}>Running apps</span>
            <TextInput value={query} onChange={setQuery} placeholder="Search running apps" icon="search" style={{ flex: 1, height: 38 }} />
          </div>
          {refreshing ? (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 10, padding: 60, color: "var(--text-secondary)", fontSize: 13.5 }}>
              <Spinner size={18} /> Scanning running apps…
            </div>
          ) : running.length === 0 ? (
            <EmptyState icon="apps" title="No running apps detected."
              body="Open the app you want to block, then click Refresh."
              action={<Btn icon="refresh" onClick={refresh}>Refresh apps</Btn>} />
          ) : (
            <div>
              {running.map((a, i) => {
                const checked = S.selectedApps.includes(a.id);
                return (
                  <div key={a.id} onClick={() => S.toggleApp(a.id)} style={{
                    display: "flex", alignItems: "center", gap: 14, padding: "12px 20px",
                    borderBottom: i < running.length - 1 ? "1px solid var(--border)" : "none",
                    cursor: "pointer", background: checked ? "var(--accent-soft)" : "transparent",
                    transition: "background 0.15s ease",
                  }}>
                    <AppTile name={a.name} color={a.color} size={36} />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontWeight: 600, fontSize: 14 }}>{a.name}</div>
                      <div style={{ color: "var(--text-tertiary)", fontSize: 12, fontFamily: "Consolas, monospace" }}>{a.process}</div>
                    </div>
                    <span style={{ fontSize: 12.5, fontWeight: 600, color: checked ? "var(--accent-text)" : "var(--text-tertiary)" }}>
                      {checked ? "Blocked during session" : "Block"}
                    </span>
                    <Toggle on={checked} onChange={() => S.toggleApp(a.id)} />
                  </div>
                );
              })}
            </div>
          )}
          <div style={{ padding: "10px 20px", borderTop: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <button onClick={() => setShowEmpty(!showEmpty)} style={{
              border: "none", background: "transparent", color: "var(--text-tertiary)", fontSize: 11.5, fontWeight: 600, padding: 4,
            }}>{showEmpty ? "· restore demo apps" : "· preview empty state"}</button>
            <span style={{ fontSize: 12, color: "var(--text-tertiary)" }}>{running.length} apps detected</span>
          </div>
        </Card>

        {/* --- Selected + manual --- */}
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <Card pad={20}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
              <span style={{ fontWeight: 700, fontSize: 15 }}>Selected apps</span>
              <Badge tone={selected.length ? "accent" : "neutral"}>{selected.length}</Badge>
            </div>
            {selected.length === 0 ? (
              <p style={{ color: "var(--text-secondary)", fontSize: 13, textWrap: "pretty" }}>
                No apps selected yet. Toggle apps in the list to add them.
              </p>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {selected.map((a) => (
                  <div key={a.id} style={{ display: "flex", alignItems: "center", gap: 10, padding: "7px 0" }}>
                    <AppTile name={a.name} color={a.color} size={28} radius={8} />
                    <span style={{ fontWeight: 600, fontSize: 13.5, flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{a.name}</span>
                    <Badge tone="neutral">{a.rule}</Badge>
                    <button onClick={() => a.rule === "Process" && !a.id.startsWith("m-") ? S.toggleApp(a.id) : S.removeManualRule(a.id)}
                      style={{
                        border: "none", background: "transparent", color: "var(--text-tertiary)",
                        width: 26, height: 26, borderRadius: 7, display: "flex", alignItems: "center", justifyContent: "center",
                      }}><Icon name="x" size={14} /></button>
                  </div>
                ))}
              </div>
            )}
          </Card>

          <Card pad={20}>
            <button onClick={() => setManualOpen(!manualOpen)} style={{
              border: "none", background: "transparent", width: "100%", display: "flex",
              alignItems: "center", justifyContent: "space-between", padding: 0,
            }}>
              <span style={{ fontWeight: 700, fontSize: 15, color: "var(--text-primary)" }}>Add manually</span>
              <Icon name={manualOpen ? "chevronDown" : "chevronRight"} size={16} style={{ color: "var(--text-tertiary)" }} />
            </button>
            {manualOpen ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 14 }}>
                <p style={{ fontSize: 12.5, color: "var(--text-secondary)", textWrap: "pretty" }}>
                  For apps that aren't running right now. Fill in any one field.
                </p>
                <TextInput value={manual.process} onChange={(v) => setManual({ ...manual, process: v })} placeholder="Process name — e.g. discord.exe" mono />
                <TextInput value={manual.path} onChange={(v) => setManual({ ...manual, path: v })} placeholder="Executable path" mono />
                <TextInput value={manual.folder} onChange={(v) => setManual({ ...manual, folder: v })} placeholder="Folder path" mono />
                <TextInput value={manual.title} onChange={(v) => setManual({ ...manual, title: v })} placeholder="Window title contains…" />
                <Btn variant="soft" icon="plus" onClick={addManualRule}>Add app rule</Btn>
              </div>
            ) : (
              <p style={{ fontSize: 12.5, color: "var(--text-tertiary)", marginTop: 6 }}>Block by process, path, folder or window title.</p>
            )}
          </Card>
        </div>
      </div>

      <StickyActionBar S={S} count={selected.length} noun="app" onStart={S.requestStart} />
    </div>
  );
}

/* ============ BLOCK WEBSITES ============ */
function BlockWebsitesPage({ S }) {
  const { useState } = React;
  const [input, setInput] = useState("");
  const [exception, setException] = useState("");

  const addSite = () => {
    const v = input.trim().toLowerCase().replace(/^https?:\/\//, "").replace(/^www\./, "");
    if (v && !S.selectedSites.includes(v)) S.setSelectedSites([...S.selectedSites, v]);
    setInput("");
  };
  const addException = () => {
    const v = exception.trim().toLowerCase();
    if (v && !S.exceptions.includes(v)) S.setExceptions([...S.exceptions, v]);
    setException("");
  };

  return (
    <div data-screen-label="Block Websites">
      <PageHeader title="Block Websites" subtitle="Block specific sites, whole categories, or anything matching a pattern." />

      <div style={{ display: "grid", gridTemplateColumns: "1.6fr 1fr", gap: 24, alignItems: "start" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
          {/* add site */}
          <Card pad={24}>
            <div style={{ display: "flex", gap: 10 }}>
              <TextInput value={input} onChange={setInput} icon="globe" autoFocus
                placeholder="Add a website, domain, or wildcard — e.g. youtube.com"
                onKeyDown={(e) => e.key === "Enter" && addSite()} style={{ flex: 1 }} />
              <Btn variant="primary" icon="plus" onClick={addSite} disabled={!input.trim()}>Add</Btn>
            </div>
            <div style={{ display: "flex", gap: 6, marginTop: 12, flexWrap: "wrap", alignItems: "center" }}>
              <span style={{ fontSize: 12, color: "var(--text-tertiary)", fontWeight: 600 }}>Try:</span>
              {["youtube.com", "instagram.com", "reddit.com/r/funny", "google.com/*q=*game*"].map((ex) => (
                <button key={ex} onClick={() => setInput(ex)} style={{
                  border: "1px solid var(--border)", background: "var(--bg-secondary)", borderRadius: 99,
                  padding: "3px 10px", fontSize: 12, color: "var(--text-secondary)", fontFamily: "Consolas, monospace",
                }}>{ex}</button>
              ))}
            </div>
          </Card>

          {/* selected sites */}
          <Card pad={0}>
            <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontWeight: 700, fontSize: 15 }}>Blocked websites</span>
              <Badge tone={S.selectedSites.length ? "accent" : "neutral"}>{S.selectedSites.length}</Badge>
            </div>
            {S.selectedSites.length === 0 ? (
              <EmptyState icon="globe" title="No websites added yet."
                body="Add a site above, or block a whole category in one click." />
            ) : (
              <div>
                {S.selectedSites.map((site, i) => (
                  <div key={site} style={{
                    display: "flex", alignItems: "center", gap: 12, padding: "10px 20px",
                    borderBottom: i < S.selectedSites.length - 1 ? "1px solid var(--border)" : "none",
                  }}>
                    <AppTile name={site} color={SITE_COLORS[site] || "#8B87A0"} size={28} radius={8} />
                    <span style={{ flex: 1, fontWeight: 600, fontSize: 13.5, fontFamily: site.includes("*") ? "Consolas, monospace" : "inherit" }}>{site}</span>
                    {site.includes("*") ? <Badge tone="neutral">Wildcard</Badge> : null}
                    <button onClick={() => S.setSelectedSites(S.selectedSites.filter((s) => s !== site))}
                      style={{ border: "none", background: "transparent", color: "var(--text-tertiary)", width: 26, height: 26, borderRadius: 7, display: "flex", alignItems: "center", justifyContent: "center" }}>
                      <Icon name="x" size={14} />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {/* categories */}
          <Card pad={20}>
            <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 4 }}>Quick categories</div>
            <p style={{ fontSize: 12.5, color: "var(--text-secondary)", marginBottom: 14 }}>Block a whole group of sites in one click.</p>
            <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
              {SITE_CATEGORIES.map((c) => (
                <Pill key={c.id} icon={c.icon} count={c.sites.length}
                  selected={S.selectedCats.includes(c.id)}
                  onClick={() => S.toggleCat(c.id)}>
                  {c.label}
                </Pill>
              ))}
            </div>
            {S.selectedCats.length > 0 ? (
              <div style={{ marginTop: 14, fontSize: 12.5, color: "var(--text-secondary)", display: "flex", alignItems: "center", gap: 6 }}>
                <Icon name="check" size={13} style={{ color: "var(--success)" }} />
                {S.selectedCats.reduce((n, id) => n + SITE_CATEGORIES.find((c) => c.id === id).sites.length, 0)} sites covered by categories
              </div>
            ) : null}
          </Card>

          {/* exceptions */}
          <Card pad={20}>
            <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 4 }}>Exceptions</div>
            <p style={{ fontSize: 12.5, color: "var(--text-secondary)", marginBottom: 12, textWrap: "pretty" }}>
              Allow these websites even during a full block.
            </p>
            <div style={{ display: "flex", gap: 8 }}>
              <TextInput value={exception} onChange={setException} placeholder="e.g. docs.google.com"
                onKeyDown={(e) => e.key === "Enter" && addException()} style={{ flex: 1, height: 38 }} />
              <Btn size="sm" onClick={addException} disabled={!exception.trim()} style={{ height: 38 }}>Allow</Btn>
            </div>
            {S.exceptions.length > 0 ? (
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginTop: 12 }}>
                {S.exceptions.map((x) => (
                  <span key={x} style={{
                    display: "inline-flex", alignItems: "center", gap: 6, padding: "4px 6px 4px 11px",
                    borderRadius: 99, fontSize: 12.5, fontWeight: 600, background: "var(--success-soft)", color: "var(--success)",
                  }}>
                    {x}
                    <button onClick={() => S.setExceptions(S.exceptions.filter((e) => e !== x))}
                      style={{ border: "none", background: "transparent", color: "inherit", display: "flex", padding: 2 }}>
                      <Icon name="x" size={12} />
                    </button>
                  </span>
                ))}
              </div>
            ) : null}
          </Card>
        </div>
      </div>

      <StickyActionBar S={S} count={S.totalSites} noun="website" onStart={S.requestStart} />
    </div>
  );
}

Object.assign(window, { BlockAppsPage, BlockWebsitesPage, StickyActionBar });
