# AGENTS

Project-specific guidance for AI coding agents.

## NeoBabylon UI context

This nested guide is subordinate to `D:\CODING\NeoBabylon\AGENTS.md` and
the accepted product/architecture documents. It applies only to this UI
directory and does not edit or replace the repository-root or global rules.

- The existing NeoBabylon shell is a deliberate React + local CSS composition.
  Do not redesign it wholesale to satisfy generic generated layout rules.
  Use Astryx components and its neutral theme for interactive controls and
  reusable UI additions; preserve the established shell and scoped visual
  language unless a specific task authorizes a broader redesign.
- The generic generated “no div/no raw px” rule is guidance for new
  standalone components, not authority to refactor the existing product shell.
  Follow the root AGENTS and accepted UI/UX reference when they conflict.
- Astryx core, neutral theme, StyleX peer runtime, and CLI are pinned in this
  package. Use `npm run astryx -- <command>` for scoped component/docs lookup;
  do not install or invoke a global or unrelated `astryx` executable.
- `main.tsx` owns the Astryx CSS imports and neutral `Theme` provider. Do not
  add external/CDN styles, or initialize Astryx in the repository root.

<!-- ASTRYX:START -->
Astryx v0.6.2 · 164 components
CLI: run every command as `npx astryx <cmd>` (shown below as `astryx ...`).

SETUP (once, in your app entry e.g. main.tsx) — without these, components render unstyled:
  import "@astryxdesign/core/reset.css";
  import "@astryxdesign/core/astryx.css";

WORKFLOW — discover, don't guess. Before writing UI:
1. `astryx build "<idea>"` — START HERE: returns a kit (closest [page] + [block]s + [component]s). No args = full playbook.
2. `astryx template <name> [--skeleton]` — scaffold the [page]/[block]s it named, or study their layout. Templates are reference code.
3. `astryx component <Name>` — props + examples for every component you use.

RULES:
- No <div> — components do all layout/spacing, page frame included.
- Frame first: read `astryx docs layout` before writing any page or screen — page frame, region widths, breakpoint behavior.
- Dense data = rows (Table, List/Item), never Card-wrapped list items; Card is for standalone widgets. Status = StatusDot/Token; Badge = counts only.
- Custom styling: component props first; else style/className with tokens — var(--color-*|--spacing-*|--radius-*). No raw hex/px. (No StyleX/Tailwind compiler here — don't use xstyle/utility classes.)
- Tokens for every value (`astryx docs tokens`). Brand/accent belongs in the theme (`astryx theme list` / `theme add <slug>`, or `astryx theme template` for a custom one) — never override --color-* in :root.
- SELF-CHECK before you finish: re-read the file and replace any raw <div>/<span> layout, imported .css/@apply, or hardcoded value (#hex, 16px) with the component or a token (var(--color-*|--spacing-*|…)). If unsure a component/prop exists, run `astryx component <Name>` / `astryx search "<thing>"`; don't hand-roll CSS.

MORE CLI:
  search "<query>"   find any component / hook / doc / template / block
  component --list   164 components by category
  template --list    page + block recipes
  docs <topic>       browser-support, cli-integrations, color, elevation, getting-started, icons, illustrations, internationalization, layout, migration, motion, principles, shape, spacing, styling-libraries, styling, theme, tokens, typography, working-with-ai
  swizzle <Name>     eject component source for deep customization
  upgrade --apply    run after any Astryx or integration dependency bump
<!-- ASTRYX:END -->
