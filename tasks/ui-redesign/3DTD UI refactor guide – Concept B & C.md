# 3DTD UI refactor guide – Concept B & C

Oct 3, 2026 · @Philipp

## Purpose and how to read the mockups

Rebuild the 3DTD UI to match the Concept B boards (B1–B6) and the Concept C build menu on the design canvas. Concept A is superseded and must be ignored. This guide covers only what a screenshot cannot tell you: intent, rules and which details are samples.

- **Source of truth:** the canvas "3DTD – UI Redesign", rows "Concept B" (B1–B6) and "Concept C · Build menu". Measure sizes and colours there; this doc names the rules behind them.
- **Binding:** layout and placement, visual hierarchy, shapes, colours, typography, component states and the interaction rules below.
- **Not binding:** every number, name and price shown is sample data unless the game already has it (section 5 lists them).
- **Reference resolution:** all boards are 1100 × 611 px, landscape. Treat px values as reference units and scale uniformly; never stretch.
- **Target platforms:** desktop and mobile (landscape). Touch targets are designed at ≥ 44 reference px and must stay that size.

## Visual language: "bevelled glass"

The style is semi-low-poly: smooth, translucent indigo plates with cut corners, lit like the game's low-poly models. It is not neon sci-fi (no scanlines, no cyan glow, no monospace caps) and not flat minimal.

- **Chamfer, not rounding.** Every plate, button, chip and tile cuts exactly two corners: top-left and bottom-right. No rounded rectangles anywhere except switch knobs and medal dots.
- **Rim + body.** A plate is a 1 px rim gradient (light lilac at top-left, fading out, warm orange at bottom-right) around a body gradient. Build them as 9-slice sprites, not shaders.
- **One facet of light.** The body carries a single hard-edged white sheen band at 118°, covering the first \~16 % of the width. Do not add more facets to panels.
- **Signature corner.** Top-level plates only get a 2 px orange line along the top-left chamfer. Buttons, chips and tiles never get it.
- **Faceted icons.** Resource gems, medals and the tower emblem are 3–6 polygon facets, lit top-left and dark bottom-right. All other icons are 1.8 px line icons.
- **Hexagon = world and progression.** Hexagons are reserved for the selection ring, skill-tree nodes, lock badges and the tower hero frame.

| Token | Value | Use |
| --- | --- | --- |
| Ground | #0B0D2A | Screen background behind plates |
| Plate body | rgba(48,47,112,0.90) → rgba(19,20,58,0.93), top to bottom | All panels |
| Inset light / shade | top 1 px white 16 % · bottom 2 px black 30 % | All plates |
| Drop shadow | 0 10 px 16 px rgba(2,3,18,0.55) | Floating plates |
| Accent | #FF8A3D (light #FFBE92, dark #EC6527, key band #9C3A12) | CTAs, selection, progress |
| Text on accent | #2A0E02 | Labels on orange |
| Accent text | #FFB98A | Orange numbers and labels on dark |
| Text / muted / dim | #F3F2FF / #B4B4E4 / #8A8BC0 | Primary, captions, locked |
| Can't afford | #FF7088 | Prices above current scrap |
| Secondary icon tint | #CFC5FF | Line icons, range stats |
| Impossible medal | #E15BB8 → #6A2AB0 facets, orange core | Only the Impossible medal |
| Display font | Sora 600–800 | Titles, numbers, buttons |
| Label font | Manrope 500–800 | Body, captions; caps at 9.5 px, tracking 0.14 em |
| Chamfer size | panels 18–20 · plates 14 · buttons 8–12 · chips 4–6 (ref px) | Scale with UI |

## Components and interaction rules

The key rule: **one orange key-cap CTA per screen** (Next wave, Start, Apply, Unlock). Everything else is lilac-rimmed glass, and orange elsewhere only marks selection or progress.

| Component | Default | Active / selected | Pressed | Disabled |
| --- | --- | --- | --- | --- |
| Key-cap CTA | Orange vertical gradient, 5 px darker bottom band, 118° sheen, soft radial glow behind (a sprite, not a blur) | — | Band shrinks to 2 px, label drops 3 px | Grey body, no glow |
| Glass button | Lilac rim, dark body, 3 px dark bottom band | Orange rim + left-to-right orange tint + trailing chevron (menu nav) | Band shrinks, body darkens | Dashed or 15 % rim, dim text |
| Segmented control | Items in a dark chamfered well, 7 % white | The active item becomes a mini key-cap | Same as CTA | Dim text |
| Switch | Chamfered dark track, grey knob left | Orange track, white knob right | — | 40 % opacity |
| Primary tabs | Grouped in a dark well, muted text | Lighter body + 2 px orange underline + orange icon | — | — |
| Category tabs | Underline tabs with a count chip | 3 px orange underline, faint orange wash | — | — |
| Tile (tower, level) | Lilac rim | 2 px orange rim + inner orange glow | — | 38 % image opacity, red price |
| Tooltip / popup | Plate with orange rim | — | — | — |

- **Affordability.** A price turns #FF7088 when cost > current scrap. Upgrade progress bars show current scrap ÷ cost, clamped to 100 %. They update live as scrap changes.
- **Medals.** Easy, Medium and Hard are three dots in that order: filled glowing orange when earned, a hollow ring when not. A thin divider follows, then the Impossible crystal: glowing when earned, a dashed hexagon outline when not.
- **World selection.** A selected tower gets a flat hexagon ring on the ground, a dashed range ellipse at its real range, and a small "Range X" tag below it.
- **Popups and tooltips** always have a triangular notch pointing at the element that opened them.
- **Numbers** use tabular figures so counters don't jitter.

## Screen-by-screen notes

In-game screens use B1/B2 for the HUD and C for the build menu. Menu screens (B3–B6) share one frame: the brand and nav column on the left, one large content plate filling the rest.

**B1 · In-game HUD**

- Top-left plate holds Scrap, Hull and Wave in that order. Pause is its own square plate beside it.
- Bottom row, left to right: speed (x1/x3/x5/x10), Next wave CTA centred, Auto-wave switch.
- The CTA's sub-label shows the upcoming wave (current + 1).
- The right side belongs to the Concept C build rail; ignore the old build menu visible in B1/B2.

**B2 · Tower selected**

- The panel opens top-left under the HUD and keeps a fixed order: header (emblem, name, tier pips, kills, close), target stepper, 4 × 2 stat grid, upgrades, sell.
- The target stepper cycles through the targeting modes. Its dots show the index and must match the mode count.
- The stat grid order is fixed: Damage, Fire rate, Range, Accuracy / Speed, Pierce, Burn, Capacity.
- The highlighted upgrade (orange rim) is the hovered or keyboard-focused one, not a purchased one.
- Sell is a quiet outline button showing the refund.

**B3 · Main menu and level select**

- The background is a live demo match running in a showcase level. It is not a preview of the selected level, so never swap it per level.
- Selection is three levels deep: Game mode (Classic / Prototype) → category tabs (Prototype / Space) → level cards.
- Card states: mastered (all 4 medals, pink-orange rim, "Mastered" tag), available, selected (orange rim, orange number badge), locked (dimmed path, lock hexagon, unlock condition).
- Picking a card opens the difficulty popup under it, with four difficulty tiles and the Start CTA. Locked difficulties show their unlock condition.
- The chip at the top right totals earned medals (Impossible count · dot count). Category tabs show earned / possible medals.
- Continue is a normal enabled button with a one-line save summary. Hide it when no save exists.

**B4 · Options**

- Tabs: General, Video, Audio, Controls. Each tab uses two columns of 58 px rows (label left, control right) under small orange section captions.
- Apply is the CTA and is enabled only when there are unsaved changes; show the count beside it. Reset to defaults is a quiet button.

**B5 · Upgrades**

- Category tabs (Towers, Economy, Hull, Global) sit top-left, the meta-currency counter top-right.
- Trees run top-down by tier with right-angle connectors.
- Node states: owned = filled orange; available = lilac outline; locked = dim with a lock icon.
- Connector states: owned path = solid orange; next unlockable = dashed lilac; rest = faint.
- The capstone node is larger with a pink-orange rim.
- Selecting a node fills the detail card: effects as icon + value rows, requirements, cost, Unlock CTA.

**B6 · Towers encyclopedia**

- A brochure spread: a thumbnail rail of all towers, then two "pages" split by a fold shadow with page numbers.
- The left page shows the hero render in a hexagon frame, number, name, cost and base stats as 10-segment meters.
- Meters are normalised against the highest value of that stat across all towers, not per tower.
- The right page shows one card per upgrade path with before → after stat chips. Keep text to chips, never paragraphs.

**C · Build menu**

- A right-hand rail, 2 columns × 5 rows of tiles, header with collapse button. Collapsed, it shrinks to a slim tab.
- Each tile shows the tower icon, its hotkey number (1–9) and its price below. Unaffordable tiles are dimmed with a red price.
- The selected tile opens a tooltip left of the rail with name, price, four key stats and a placement hint.
- While placing, the map shows a dashed hexagon ghost of the tower plus its range ellipse at the cursor or finger position.

## Placeholders: do not copy literally

Wire every item below to real game data or real assets; the mockups only show where it goes.

| In the mockups | Treat as | Replace with |
| --- | --- | --- |
| Tower prices (25–650), Mine Factory cost "\[cost\]" / 290 | Sample | Tower data |
| Stat changes on upgrade chips, meter lengths | Sample | Upgrade data; meters from cross-tower max |
| Skill-tree node names and effects, "Research" currency | Sample design | Real meta-progression design |
| Continue summary ("Level 01 · Hard · Wave 12"), Tier 1, kill counts | Sample | Save data and tower state |
| Level path drawings | Stand-in | The level path line-graph image per level |
| Tower icons (cropped from old screenshots, blue tile backgrounds) | Stand-in | Clean renders on transparent backgrounds |
| Which icon is the Mine Factory | Guess | Real tower-to-icon mapping |
| Video settings list in B4 | Suggestion | The settings the game actually supports |
| Backdrops (screenshots with old UI painted out) | Stand-in | Live scene |

- **Impossible unlock:** the mockups assume it unlocks after clearing Hard, and that a level unlocks after the previous one is cleared. Confirm with the designer before hard-coding. DESIGNER: confirmed.
- **Stat icons:** they reuse a few generic glyphs (Speed shares the bolt with Fire rate). Give each stat a distinct icon in production.

## Implementation checklist

Build the shared pieces first, then assemble screens from them; no screen should hand-style its own plate or button.

- [ ] Scale the UI uniformly from the 1100 × 611 reference, matched to screen height, inside device safe areas.
- [ ] Make 9-slice sprites for plate, glass button, key-cap CTA, tile and chip (rim + body + inset light, chamfered corners).
- [ ] Make reusable components with all states from section 3: CTA, glass button, segmented control, switch, primary tabs, category tabs, tile, tooltip with notch, medal row, slider.
- [ ] Import Sora and Manrope (both under the SIL Open Font License); set tabular figures for counters.
- [ ] Make glows and the signature corner line sprites, not runtime blur. Make plate background blur optional and fall back to a more opaque body on low-end mobile.
- [ ] Keep every touch target at ≥ 44 reference px, including tiles, stepper arrows and close buttons.
- [ ] Enforce one orange CTA per screen; selection and progress may use orange, nothing else.
- [ ] Bind affordability (red price, dimmed tiles, progress bars) to the live scrap value.
- [ ] Build the main menu backdrop as a live demo match independent of level selection.
- [ ] Check text contrast (≥ 4.5 : 1 for body text) on the translucent plates over the brightest part of the backdrop.
