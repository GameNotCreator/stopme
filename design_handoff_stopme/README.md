# Handoff: StopMe — Windows Desktop App Redesign

## Overview
**StopMe** is a Windows desktop productivity app that blocks digital distractions. The user
selects apps and/or websites to block, picks a duration, and starts a session. For that
duration StopMe prevents the selected apps/sites from being opened or used. A session can run
in **Strict mode**, which cannot be stopped before the timer ends.

This bundle is the full redesign: a new visual identity (white-dominant, near-black text,
violet accent), a clear 6-item navigation, the complete primary flow
(*select → duration → start*), and every required state (empty, active, loading, error).

The core flow must be obvious and reachable in **under 20 seconds**:
**select apps/websites → choose a duration → Start Blocking.**

---

## About the Design Files
The files in `prototype/` are **design references written in HTML/React (Babel-in-browser)** —
a working prototype that demonstrates the intended look, layout, copy, and behavior. **They are
not production code to ship as-is.** The task is to **recreate these designs in StopMe's real
codebase**.

StopMe is a Windows desktop app, so the recommended target is **WinUI 3 / Windows App SDK
(XAML + C#)**. Concrete WinUI mapping notes are in the *Implementation notes (WinUI)* section.
If the team has already chosen another stack (e.g. WPF, Electron + React, Flutter), recreate the
designs using that stack's established patterns instead — the visuals and behavior are what
matter, not the HTML.

The prototype uses React only to make the mock interactive; **do not port the React component
structure literally.** Use it to read exact values, copy, and state logic.

## Fidelity
**High-fidelity (hifi).** Final colors, typography, spacing, radii, shadows, copy, and
interactions. Recreate the UI faithfully using the codebase's native controls and styling.
Exact tokens are listed under *Design Tokens*.

---

## Locked design decisions (the version to build)
The prototype includes a Tweaks panel exposing layout variations for exploration. **For the
production build, ship this single configuration:**

| Decision | Value |
|---|---|
| **Accent color** | **Violet `#7C5CFF`** |
| **Dashboard layout** | **"Calm"** — single centered column, max-width 640px, everything centered |
| **Session timer** | **"Ring"** — circular progress ring with the countdown in the center |
| **Sidebar** | **"Tinted"** — sidebar sits on the app background; main content is a white rounded panel |

The other variants (split/hero dashboards, bar/dark timers, white/rail sidebars) are
exploration only and are **not** part of the deliverable. They can be deleted.

---

## App shell & navigation

**Window layout** (desktop, comfortable width ~1280–1440px, min ~1040px):
- **Custom title bar**, 38px tall, transparent, with right-aligned Windows caption buttons
  (minimize / maximize / close), each 46px wide. Caption icons in `--text-secondary`.
- **Sidebar** (left), 224px wide, sits directly on the app background (`--bg-app`), no border.
- **Main content panel** (right): white (`--bg-primary`), top-left corner rounded 20px, thin
  border `--border`, very soft shadow. Scrolls vertically. Inner content is centered with
  `max-width: 1060px` and `36px 40px 48px` padding.

**Sidebar contents (top → bottom):**
1. **Logo**: 30px rounded-9px violet gradient tile (gradient `--accent` → `--accent-active`)
   containing a white shield/hexagon glyph, + wordmark "StopMe" (17px, weight 700,
   letter-spacing −0.02em).
2. **Nav** (vertical, 3px gap). Six items, each a 12px-radius row, `10px 13px` padding,
   14px / weight 600:
   - Dashboard (icon: grid)
   - Block Apps (icon: app window)
   - Block Websites (icon: globe)
   - Sessions (icon: calendar-list)
   - Statistics (icon: bar chart)
   - Settings (icon: gear)
   - **Active item:** white background, `--shadow-card`, label in `--text-primary`, icon in
     `--accent`. **Inactive:** transparent, label `--text-secondary` (→ `--text-primary` on
     hover), icon inherits label color.
   - **Dashboard badge:** when a session is active, show a pill on the right of the Dashboard
     row with the live countdown (11px, weight 700, white text). Background `--accent` for
     normal sessions, `--text-primary` for strict.
   - **Settings badge:** when the service has an error, show a small pulsing red status dot.
3. **Active-session mini-card** (only when a session runs), above the footer: 16px-radius card,
   violet gradient (or near-black `--text-primary` if strict), white text. Shows a tiny label
   ("BLOCKING ACTIVE" / "STRICT SESSION" with a lock icon), the countdown (22px, weight 700,
   tabular-nums), and "until {endTime}". Clicking it navigates to Dashboard.
4. **Footer:** a "Service connected / Connecting… / Service issue" status chip (status dot +
   12.5px label, opens diagnostics) and a "Help" row.

---

## Screens / Views

### 1. Dashboard — empty state (no active session) — "Calm" layout
- **Container:** centered column, `max-width: 640px`, `margin: 0 auto`, top padding 28px.
- **Hero (centered):**
  - **Title:** "What do you want to block?" — **serif** (Instrument Serif), 44px, weight 400,
    letter-spacing −0.01em.
  - **Subtitle:** "Select apps and websites, choose a duration, and StopMe will keep them
    blocked." — 15px, `--text-secondary`, max-width 460px.
  - **Selection summary chips** (centered row, wrap): four pills, white bg, 1px `--border`,
    99px radius, `7px 14px`, 13px/600, icon in `--accent` (or `--text-tertiary` when the value
    is empty):
    - "{N} apps" (clickable → Block Apps; shows chevron)
    - "{N} websites" (clickable → Block Websites; shows chevron)
    - "{duration}" (e.g. "1 h")
    - "Normal" / "Strict" (lock icon when strict)
  - **Duration label** "DURATION" (12px, weight 700, `--text-tertiary`, uppercase, letter-spacing
    0.06em) + **Duration selector** (see component).
  - **Action row** (centered, wrap): **Primary button** "Start Blocking" (lg, play icon) +
    **Secondary button** "Create a custom session" (lg) + **Strict mode toggle** (label + lock
    icon + switch).
  - If nothing selected: an inline hint row — info icon (violet) + "Nothing selected yet — pick
    apps or websites first, or start with your saved blocklist."
- **Below hero:** a centered "Service connected" status chip (pill button; opens diagnostics).
- **No-session card** is *not* shown in the calm layout when empty — the hero is the focus.

> Note: the "No active blocking session" empty card (moon icon, title "No active blocking
> session", body "Choose what to block and start a session.") is used in the **Sessions** page
> and in the other dashboard variants; keep the component available.

### 2. Dashboard — active session — "Calm" layout + "Ring" timer
- Centered column, max-width 640px. Contains the **Active Session card** (ring variant) and a
  centered status chip below it.
- **Active Session card (Ring):** white card, 28px padding, 20px radius. **Border is the strict
  signal:** `1.5px solid --text-primary` when strict, else `1px solid --accent-soft-2`.
  - **Header row:** left = pulsing status dot (green normal / violet on dark) + session name
    (14px/700); right = badge "STRICT" (near-black bg, white text, lock icon) or "NORMAL"
    (neutral).
  - **Progress ring:** 210px diameter (big variant), 11px stroke. Track =
    `--accent-soft-2` (normal) / `--bg-tertiary` (strict). Progress arc = `--accent` (normal) /
    `--text-primary` (strict), round line caps, animates `stroke-dashoffset` over 0.8s.
    - **Center:** countdown (40px, weight 700, tabular-nums, letter-spacing −0.03em) + "remaining"
      (12px, `--text-tertiary`, weight 600).
  - **Caption:** "StopMe is protecting your focus until {endTime}" (13px, `--text-secondary`/600).
  - **Blocked chips:** up to 4 app chips (each = 18px app-icon tile + name, pill, `--bg-secondary`)
    + a "+ N websites" chip.
  - **Stop control:**
    - **Normal:** ghost danger button "Stop session" (stop icon) → opens Stop-confirm modal.
    - **Strict:** a non-actionable button "🔒 Locked until {endTime}" (`--bg-tertiary` bg,
      `--text-tertiary`). Clicking it **shakes** (0.45s) and shows a black tooltip:
      "Strict mode can't be stopped before the timer ends." (auto-hides ~3.2s).

### 3. Dashboard — connecting / error states
Shown as a **banner** at the top of the Dashboard content (16px radius, full width of the
column, 14px/18px padding, slide-up entrance). Tone-colored. States:
- **Connecting:** neutral bg (`--bg-primary`, `--border`). Spinner + "Connecting to StopMe
  service…" + "This usually takes a couple of seconds." No actions.
- **Connecting (long)** — after ~5s of connecting: warning tone. Spinner + "Still connecting…" +
  "The StopMe service is taking longer than usual to respond." Actions: "Retry", "Run diagnostics".
- **Service not running:** error tone (`--error-soft` bg, red border). Alert icon + "StopMe
  service is not running." + "Blocking won't work until the service is back…". Actions:
  **Retry** (primary), **Run diagnostics**, **Installation guide** (ghost, external-link icon).
- **Authentication issue:** error tone. "StopMe could not authenticate with the local service."
  + "Your app and the background service are out of sync…". Actions: **Repair connection**
  (primary), **Run diagnostics**.
- **Extension disconnected:** warning tone, puzzle icon. "Browser extension not connected." +
  "Apps will still be blocked, but websites won't be until the extension is installed and
  enabled." Actions: **Install extension** (primary), **Details**.

A **Service status mini-card** is also used (in split/hero variants and reusable elsewhere):
title "Status" + a Connected/Connecting/Issue badge; rows for Service, Browser extension (status
dot + Running/Not connected), Last check; Retry + Diagnostics buttons when there's an error.

**Behavior:** while "Connecting", escalate to the "long" banner after 5 seconds. **Retry**
shows a brief connecting state then resolves to connected (in the real app: actually re-attempt
the service handshake). The user must never be left on a silent "Connecting".

### 4. Block Apps — running apps list
- **Page header:** title "Block Apps" (26px/700), subtitle "Choose the applications StopMe
  should close and prevent from reopening.", right-aligned **Refresh apps** button (refresh icon).
- **Two-column grid** `1.6fr / 1fr`, 24px gap, items aligned to top.
- **Left — Running apps card** (white, 0 padding, dividers between rows):
  - Header strip: "Running apps" + a **search input** ("Search running apps", search icon).
  - **App row** (12px/20px padding, pointer): app-icon tile (36px) + human name (14px/600) +
    process name below (`discord.exe`, 12px, monospace, `--text-tertiary`) + right-side label
    ("Block" / "Blocked during session") + a **toggle**. **Selected rows** get `--accent-soft`
    background. *Human name is primary; process name is secondary — users never need to
    understand processes.*
  - Footer strip: "{N} apps detected".
  - **Empty state** (when no apps detected): app icon, "No running apps detected.", "Open the
    app you want to block, then click Refresh.", a **Refresh apps** button.
  - **Loading:** centered spinner + "Scanning running apps…" (Refresh triggers a ~900ms scan).
- **Right column:**
  - **Selected apps card:** title + count badge. Each selected item = 28px tile + name + a
    **rule-type badge** (Process / Path / Folder / Window title) + remove (×) button. Empty:
    "No apps selected yet. Toggle apps in the list to add them."
  - **Add manually card** (collapsible): intro "For apps that aren't running right now. Fill in
    any one field." + four inputs: Process name (`discord.exe`, mono), Executable path (mono),
    Folder path (mono), Window title contains… + **Add app rule** button (soft). The chosen
    field determines the rule-type badge.
- **Sticky action bar** (bottom, sticky, blurred white, 18px radius, raised shadow):
  Duration label + **Duration selector** (compact) + **Strict mode toggle** + **primary button**
  whose label is dynamic: "Select apps to block" (disabled, 0 selected) /
  "Start blocking N apps" / "Session in progress" (disabled while a session runs).

### 5. Block Apps — selected apps + duration
Same page; this is the state where apps are selected and the sticky bar reads
"Start blocking 3 apps". Strict toggle in the bar drives the strict-confirm modal on start.

### 6. Block Apps — empty state
The running-apps card empty state (see #4). The prototype has a tiny "preview empty state"
toggle in the card footer for demoing.

### 7. Block Websites — selected websites / categories
- **Page header:** "Block Websites" + "Block specific sites, whole categories, or anything
  matching a pattern."
- **Two-column grid** `1.6fr / 1fr`.
- **Left:**
  - **Add-site card:** large input ("Add a website, domain, or wildcard — e.g. youtube.com",
    globe icon) + **Add** primary button (Enter also adds). Input is normalized
    (strip `https://`, `www.`). Below: "Try:" + example chips `youtube.com`, `instagram.com`,
    `reddit.com/r/funny`, `google.com/*q=*game*` (monospace) that prefill the input.
  - **Blocked websites card:** title + count badge. Each row = 28px favicon-tile (color by
    domain) + domain (mono if it contains `*`) + a "Wildcard" badge when applicable + remove (×).
    Empty: "No websites added yet." / "Add a site above, or block a whole category in one click."
- **Right:**
  - **Quick categories card:** intro "Block a whole group of sites in one click." + selectable
    **pills** (icon + label + count badge) for: Social Media, Video Streaming, Gaming, News,
    Shopping, AI Chatbots. Selected pill = `--accent-soft` bg, violet text/border. Below: a
    summary "N sites covered by categories" (check icon).
  - **Exceptions card:** "Allow these websites even during a full block." + input
    ("e.g. docs.google.com") + **Allow** button. Allowed items render as green removable chips.
- **Sticky action bar:** same as Block Apps, noun = "website".

### 8. Sessions — list
- **Header:** "Sessions" + "Create, schedule, and review your blocking sessions." + **New
  session** primary button (plus icon).
- Four stacked sections, each a **SectionTitle** + a white card (0 padding, dividers). Row =
  38px rounded icon tile + name (14px/700) + badges + meta line (12.5px `--text-secondary`) +
  right-aligned actions.
  - **Active:** the running session (countdown + "N apps · N websites"), action = Stop (normal)
    or Locked (strict). Empty → the moon empty-state with a "Create session" button.
  - **Scheduled:** e.g. "Evening study" — "Today, 7:00 PM · 1 h 30 min · 5 apps · 10 websites",
    actions Edit / delete.
  - **Recurring:** rows with day chips + time range + an enable **toggle**; paused rows are dimmed
    and show a "PAUSED" badge.
  - **Past sessions:** dimmed rows with "COMPLETED" (green) or "STOPPED EARLY" (warning) badges,
    "Repeat" action. SectionTitle has a "Clear history" button.

### 9. Sessions — create session modal
560px modal. Header: sparkle icon (accent tone), "Create a session", "Name it, choose what to
block, and start now or schedule it."
- **Session name** input (default "Deep work").
- **Two picker buttons** "Apps to block" / "Websites to block", each showing "{N} selected" and a
  chevron; clicking navigates to the respective page (and closes the modal).
- **Duration** selector.
- **Start mode** segmented control: "Start now" / "Schedule" (Schedule reveals a time input).
- **Strict mode toggle.**
- Footer: "Cancel" (ghost) + primary "Create & start" (play) or "Schedule session" (calendar),
  disabled when nothing is selected. "Create & start" runs the normal start flow (strict →
  confirm modal first).

### 10. Statistics
- **Header:** "Statistics" + "What StopMe helped you avoid this week." + **Export** (download
  icon) and **Reset** (ghost danger) buttons.
- **Metric cards grid** `3 columns`, 16px gap. Each card: icon + label (12.5px/600
  `--text-secondary`), big value (26px/700), sub (12px `--text-tertiary`):
  Focus time ("14 h 20 min" / this week), Sessions completed ("23" / all time), Current streak
  ("6 days"), App launches blocked ("156"), Site visits blocked ("489"), Top distraction
  ("YouTube" / "134 blocked attempts").
- **Charts grid** `1.5fr / 1fr`:
  - **Focus time by day:** 7 vertical bars (Mon–Sun), violet, height ∝ minutes, value label above
    ("2.5h" / "—" for empty days), animated height.
  - **Blocks by category:** a segmented horizontal bar + a legend list (swatch + label + count):
    Video Streaming, Social Media, Gaming, Other.
  - **Top blocked:** ranked list (1–5) with favicon/app tile, name, an app/site badge, and count.
- Tone is **neutral and factual** (no motivational copy). Export shows a confirmation toast.

### 11. Settings
Two-column: a **left sub-nav** (200px, sticky) and a white content card (≥420px tall).
Sub-nav items (active = white bg + shadow; Service shows a red dot when there's an issue):
- **General:** toggles — "Start StopMe at boot" / "Start minimized" / "Show tray icon", each
  with a one-line explanation (use the **SettingRow** component: title + body + control).
- **Blocking:** Default duration (duration selector), Default mode (Normal/Strict segmented),
  Unsupported browsers (select: Close the browser tab / Block the whole browser / Allow), plus a
  callout box: "About strict mode. Once a strict session starts, it cannot be stopped, edited, or
  bypassed by restarting StopMe or Windows. The timer always finishes."
- **Privacy:** Statistics mode segmented (Readable / Anonymized / Disabled), Export data
  (button), Delete all data (danger button).
- **Service:** SectionTitle badge "All good"/"Issue detected"; rows for Connection and Background
  service (status dot + Connected/Running vs Disconnected/Stopped); Troubleshooting row with
  "Retry connection", "Run diagnostics" (soft), "Open logs" (ghost).
- **Extension:** intro + rows for Chrome ("StopMe for Chrome v2.4.1", Connected, or an "Install
  extension" button when disconnected), Edge ("Not installed", Install), Other browsers
  (Instructions). 
- **About:** Version ("StopMe 2.1.0 (stable)"), Data location
  (`C:\Users\you\AppData\Roaming\StopMe`, monospace), Check for updates (button).

### 12. Service diagnostics modal
520px modal. Header: monitor icon (error tone if failing), "Service diagnostics", "Checking
every part of the blocking pipeline." A checklist runs sequentially (~450ms per step), each row =
status glyph (spinner → check / alert / ×) + label + mono detail:
- StopMe background service (`stopme-svc.exe`)
- App ↔ service connection (Named pipe handshake)
- Service authentication (Local token exchange)
- Chrome extension (StopMe for Chrome v2.4.1)
- Edge extension (Not installed) → "Optional" warning
- Network filter (WFP filter driver)

Which rows fail depends on the service state (down → first two fail; auth → auth fails; ext →
Chrome fails). Failed rows get `--error-soft` bg + "Failed" badge. After completion, a summary
banner (success or error) explains the result and a primary CTA appears: "Restart service" /
"Repair connection" / "Install extension" (or "Done" when healthy). "Run again" replays.

### 13. Strict mode confirmation modal
460px modal. Header: lock icon (**strict tone = near-black**), "Start a strict session?",
"Strict mode can't be stopped before the timer ends. Not by closing StopMe, not by restarting
Windows." A summary box lists Duration ("1 h — until {endTime}"), Apps blocked (count + first 3
names), Websites blocked (count). Footer: "Cancel" + a **dark** button "I'm sure — lock me in"
(lock icon) which starts the session.

### 14. Blocked app notification (Windows toast)
A native-style Windows toast, bottom-right (right 20px, bottom 64px), 372px wide, dark glass
(`rgba(32,30,42,0.97)`, blur), 10px radius. Contents: 40px violet-gradient shield tile + "{App}
is blocked" (13.5px/700) + "StopMe is protecting your focus. {App} will be available again at
{endTime}." + close ×. A divided action row: "Back to work" / "View session". Small "StopMe ·
now" label. Slides in from the right. In the real app this is a Windows toast notification
(AppNotificationBuilder) — match the copy and the shield iconography.

### 15. Browser blocked page (full page)
A full-page replacement shown in the browser when a blocked site is opened (rendered by the
extension). Mock includes browser chrome (tab "Blocked by StopMe", address bar showing the
blocked domain) for context — **the extension only controls the page body**, so build that:
- Background: soft vertical gradient (`#FBFAFE` → `--accent-soft`).
- 64px violet-gradient rounded shield tile (drop shadow).
- **Headline (serif, 40px):** "This one can wait."
- Body: "{site} is blocked while StopMe protects your focus. It will be available again at
  {endTime}."
- A pill "🕐 Back to it at {endTime}" (white, `--accent-soft-2` border, accent text).
- A serif italic quote (encouragement), `--text-tertiary`.

---

## Interactions & Behavior
- **Primary flow:** selection persists across pages (apps, manual rules, sites, categories,
  exceptions, duration, strict). "Start Blocking" validates that ≥1 thing is selected (else a
  toast "Select apps or websites to block first"), blocks starting a second session, and — if
  strict — opens the confirm modal before starting.
- **Starting a session:** creates a session object (name, durationMins, strict, endsAt, apps[],
  sites[] with categories expanded), navigates to Dashboard, and ~1.6s later pops the blocked-app
  toast (demo) for ~5s.
- **Countdown:** ticks every second; ring/bar/sidebar all read from `endsAt`. On expiry the
  session clears and a toast "Session complete — nice work!" shows.
- **Stop (normal):** Stop → confirm modal ("Stop this session?" with remaining time) → confirm
  clears the session ("Session stopped"); "Keep focusing" dismisses.
- **Stop (strict):** the stop control is locked; clicking shakes it and shows the tooltip. There
  is no way to end a strict session early — enforce this in the service too.
- **Service simulation:** Connecting escalates to "long" after 5s. Retry/Repair shows a brief
  connecting state then resolves to Connected (real app: re-run handshake / re-issue token).
- **Animations:** card entrance `pop-in` (0.22s, cubic-bezier(0.16,1,0.3,1)); banners/toasts
  `slide-up` / `toast-in`; ring arc transitions 0.8s ease; strict-lock `shake` 0.45s; status dots
  pulse 1.6s; dark-timer glow `breathe` 5s. **Respect `prefers-reduced-motion`** — entrance
  from-states must resolve to fully visible.
- **Persistence:** the active session is stored in `localStorage` under `stopme-session` and
  restored on load if not expired. In the real app, persist session state in the service so it
  survives app restarts (critical for strict mode).

## State Management
Global app state (in the prototype, React state in `js/app.jsx`):
- `page` — current route (dashboard | apps | websites | sessions | stats | settings)
- `selectedApps: string[]`, `manualRules: {id,name,process,color,rule}[]`
- `selectedSites: string[]`, `selectedCats: string[]`, `exceptions: string[]`
- `duration: number` (minutes), `strict: boolean`
- `session: {name, durationMins, strict, endsAt, apps[], sites[]} | null`
- `now` (1s tick), `service` (connected | connecting | connecting-long | down | auth | ext),
  `modal` (strict-confirm | stop-confirm | create-session | diagnostics | null), transient toast.
- Derived: `totalSites = selectedSites.length + Σ category sites`.

## Design Tokens
**Colors**
| Token | Value | Use |
|---|---|---|
| `--bg-primary` | `#FFFFFF` | cards, panels |
| `--bg-secondary` | `#F6F5FA` | subtle fills, segmented tracks |
| `--bg-tertiary` | `#EFEDF6` | chips, strict ring track |
| `--bg-app` | `#F3F2F8` | app/window background |
| `--text-primary` | `#17151F` | primary text, strict accent |
| `--text-secondary` | `#6E6A7C` | secondary text |
| `--text-tertiary` | `#9B97AA` | hints, captions |
| `--accent` | **`#7C5CFF`** | primary accent |
| `--accent-hover` | `#6A4AF0` | button hover |
| `--accent-active` | `#5C3CE0` | gradients, pressed |
| `--accent-soft` | `#F1ECFF` | selected bg, soft button |
| `--accent-soft-2` | `#E4DCFF` | ring track, borders |
| `--accent-text` | `#6442E8` | text/icon on soft accent |
| `--success` / `--success-soft` | `#1F8A5B` / `#E5F4EC` | connected, completed |
| `--warning` / `--warning-soft` | `#B97B0A` / `#FBF2DF` | warnings, stopped-early |
| `--error` / `--error-soft` | `#CC4256` / `#FBEAED` | errors, danger |
| `--border` / `--border-strong` | `#E9E7F1` / `#DBD8E8` | card borders / input borders |

**Typography** — UI: **Schibsted Grotesk** (Google Fonts). Display/serif accents:
**Instrument Serif** (hero titles, big countdowns in bar/dark variants). Monospace: Consolas
(process names, paths, wildcards).
| Role | Size / weight |
|---|---|
| Page title | 26px / 700, −0.02em |
| Section title | 17px / 700, −0.01em |
| Card title | 15px / 700 |
| Body | 14px / 400–600 |
| Small label | 12.5px |
| Badge | 11px / 700, 0.02em |
| Hero (serif) | 40–44px / 400 |

**Spacing:** 8px base — 4 / 8 / 12 / 16 / 24 / 32 / 40 / 56.
**Radii:** cards 20px (lg 24px), buttons/inputs 12px, pills 999px.
**Shadows:** card `0 1px 2px rgba(23,21,31,.04), 0 4px 16px rgba(23,21,31,.04)`; raised
`0 2px 6px rgba(23,21,31,.06), 0 12px 32px rgba(23,21,31,.10)`; modal
`0 8px 24px rgba(23,21,31,.10), 0 24px 64px rgba(23,21,31,.18)`.

## Components (reusable)
Primary / Secondary / Ghost / Soft / Dark buttons · Toggle · Pill (selectable) · Badge
(neutral/accent/success/warning/error/strict) · Card · Text input · Search input · Status dot
(with pulse) · Spinner · App-icon tile (colored letter placeholder) · Section title · Page header
· Empty state · Duration selector (15m/30m/1h/2h/Custom) · Strict-mode toggle · Progress ring ·
Modal + Modal header · Setting row · Toast.

**Icons:** the prototype draws its own 1.7px-stroke line icons inline (`Icon` in
`js/stopme-ui.jsx`). In the real app use the platform/native icon set (or Lucide/Fluent) — match
the listed semantic names.

## Assets
No external image assets. App and website icons are **colored letter-tile placeholders**. In
production, fetch real app icons (from the executable) and website favicons. The StopMe logo is a
CSS gradient tile + a shield/hexagon glyph — replace with the real app icon.

## Implementation notes (WinUI 3 / Windows App SDK)
- **Window chrome:** extend content into the title bar (`ExtendsContentIntoTitleBar`), use
  Mica/your own background; custom caption buttons or system caption with a transparent drag
  region.
- **Layout:** `NavigationView` (left pane) maps cleanly to the sidebar; or hand-roll for the
  tinted look. Main content = a `Frame` per page. Cards = `Border` + `CornerRadius="20"` +
  `BorderBrush`/`Shadow` (use `ThemeShadow` sparingly; the soft look is fine with a subtle
  `DropShadowPanel`/composition shadow).
- **Tokens:** put colors/brushes/radii/type ramp in `ResourceDictionary` (App.xaml) using the
  table above. Define `AccentBrush = #7C5CFF` and derive hover/soft.
- **Fonts:** bundle Schibsted Grotesk + Instrument Serif (or pick the closest available — e.g.
  Segoe UI Variable for UI, a serif for hero) and reference via `FontFamily`.
- **Ring timer:** an `Arc`/`Path` whose sweep is bound to progress, animated with a
  `DispatcherTimer` (1s) recomputing from `endsAt`; tabular figures for the countdown.
- **Blocking engine (out of UI scope but referenced):** a background **Windows Service**
  (`stopme-svc.exe`) enforces app blocking (terminate + prevent relaunch by process / path /
  folder / window title) and website blocking via the browser extension + a WFP filter for
  unsupported browsers. The UI talks to it over a named pipe with a local auth token — surface
  every failure through the banner/diagnostics flows above. **Strict sessions must be enforced
  by the service** (survive app/Windows restart); the UI lock is only a reflection of that.
- **Browser extension:** Chrome/Edge MV3 extension blocks listed domains/wildcards + honors the
  exceptions list, and renders the "browser blocked page" (#15).

## Files (in `prototype/`)
- `StopMe.html` — entry point; loads fonts, `stopme.css`, and the JSX modules.
- `stopme.css` — design tokens (`:root`) + base styles + keyframes.
- `js/stopme-ui.jsx` — icons + all reusable UI primitives (buttons, inputs, ring, modal, etc.).
- `js/stopme-data.jsx` — demo data (running apps, site categories, sessions, stats, diagnostics).
- `js/page-dashboard.jsx` — Dashboard (3 variants), active-session card (3 timer variants),
  service banner + status card.
- `js/page-block.jsx` — Block Apps + Block Websites, sticky action bar.
- `js/page-other.jsx` — Sessions, Statistics, Settings.
- `js/overlays.jsx` — all modals, the Windows toast, the browser blocked page, in-app toast.
- `js/app.jsx` — app shell (title bar, sidebar), routing, global state. **`TWEAK_DEFAULTS` at the
  top holds the locked config (accent `#7C5CFF`, dashboard `calm`, timer `ring`, sidebar
  `tinted`).**
- `js/tweaks-panel.jsx` — the exploration Tweaks panel (not part of the production app).

> To preview: open `StopMe.html` in a browser. The Tweaks panel (toolbar) lets you flip the
> variants; production should ship only the locked config above.
