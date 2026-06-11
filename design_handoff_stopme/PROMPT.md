# Prompt for Claude Code — Implement the StopMe redesign

Copy-paste everything below into Claude Code, run from the root of the StopMe repository,
with the `design_handoff_stopme/` folder placed at the repo root (or adjust the path).

---

I'm giving you a complete design handoff for a full redesign of **StopMe**, my Windows desktop
app that blocks distracting apps and websites for a chosen duration.

## Your mission
Implement this design **exactly** — pixel-faithful, state-faithful, copy-faithful — in this
codebase. Replace the current UI entirely. Do not invent your own visual style, spacing, colors,
or copy: every value you need is specified in the handoff.

## Source of truth (in priority order)
1. `design_handoff_stopme/README.md` — the full spec: design tokens, app shell, all 15 screens,
   every state (empty / loading / error / active), interactions, state management, microcopy.
   Read it ENTIRELY before writing any code.
2. `design_handoff_stopme/prototype/` — a working HTML/React prototype of the design. Open
   `StopMe.html` in a browser to see and interact with the real thing. When the README is
   ambiguous, read the prototype source (`stopme.css` for tokens/keyframes, `js/stopme-ui.jsx`
   for component anatomy, `js/page-*.jsx` for layouts, `js/overlays.jsx` for modals/toast/blocked
   page, `js/app.jsx` for state and flows). The prototype is a **reference to recreate**, not
   code to copy — re-implement it idiomatically in this codebase's stack.

## Locked configuration (build exactly this — ignore the other variants in the prototype)
- Accent: violet **#7C5CFF** (hover #6A4AF0, active #5C3CE0, soft #F1ECFF, soft-2 #E4DCFF,
  text-on-soft #6442E8)
- Dashboard layout: **"calm"** — single centered column, max-width 640px
- Active-session timer: **"ring"** — circular progress ring, countdown centered inside
- Sidebar: **"tinted"** — 224px sidebar on the app background, main content in a white
  rounded-top-left panel
- Light theme only. Fonts: **Schibsted Grotesk** (UI) + **Instrument Serif** (hero titles,
  display moments). Bundle them; do not substitute without asking.
- The prototype's "Tweaks panel" and the non-locked variants (hero/split dashboards, bar/dark
  timers, white/rail sidebars) are design exploration — DO NOT implement them.

## Non-negotiable behaviors
- Primary flow reachable in under 20 seconds: select apps/websites → choose duration → Start
  Blocking. Selection state is shared across pages.
- Strict mode: confirmation modal before start ("I'm sure — lock me in"); during a strict
  session the stop control is a locked button that shakes and shows "Strict mode can't be
  stopped before the timer ends." There must be NO code path that ends a strict session early.
- The session countdown is driven by an `endsAt` timestamp, persisted so it survives app
  restart. Sidebar mini-card, Dashboard ring, and Sessions page all read the same state.
- Service connectivity is never silent: connecting → after 5 s escalate to "still connecting"
  with Retry/Diagnostics → distinct actionable states for "service not running",
  "authentication issue", "extension disconnected", exactly as specified (banner + status
  card + sidebar chip + diagnostics modal with sequential checks).
- All empty states, loading states, and microcopy come verbatim from the README.

## How to work
1. First, explore the existing codebase and tell me: the UI stack you found, how the current
   screens map to the new ones, and your implementation plan (design tokens → shell/nav →
   shared components → Dashboard → Block Apps → Block Websites → Sessions → Statistics →
   Settings → modals/overlays). Wait for my OK if anything is ambiguous.
2. Centralize the design tokens (colors, radii 20/12/999, shadows, type scale 26/17/15/14/12.5/11,
   8px spacing scale) in ONE place and use them everywhere — no hardcoded magic values.
3. Build the shared components first (buttons primary/secondary/ghost/soft/dark, toggle, pill,
   badge, card, inputs, status dot, duration selector, strict toggle, progress ring, modal,
   setting row, empty state, toast) and reuse them on every screen.
4. Wire real data where the app already has it (running processes, service status, extension
   status); keep the prototype's demo data only as placeholders/fixtures where the backend
   doesn't exist yet, clearly marked with TODOs.
5. After each major screen, show me a screenshot or run the app so I can compare against the
   prototype.
6. Respect `prefers-reduced-motion`; animations are subtle (specs in README).

If the repo has no UI project yet, propose the best fit for a Windows desktop app (WinUI 3 /
Windows App SDK recommended — mapping notes are at the end of the README) and scaffold it
before implementing.

Acceptance check: put the prototype (`design_handoff_stopme/prototype/StopMe.html`) side by side
with the running app — a reviewer should not be able to tell the layouts, colors, type, spacing,
states or copy apart.
