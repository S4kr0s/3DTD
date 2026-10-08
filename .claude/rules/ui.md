---
paths:
  - "3DTD/Assets/Scripts/UI/**"
  - "3DTD/Assets/Editor/UIRedesign/**"
  - "3DTD/Assets/Prefabs/UI/**"
  - "3DTD/Assets/Resources/UI/**"
  - "3DTD/Assets/Resources/Progress/**"
  - "tasks/ui-redesign/**"
  - "tasks/ui-redesign/**"
---

# UI (paths relative to the Unity project `3DTD/`)

## UI tooling
- UI tooling (`Assets/Editor/UIRedesign/`, all batch-mode `-executeMethod` entry points, project closed in the Editor):
  - `UIRedesignBuilder.BuildAll` (also menu 3DTD > UI Redesign > Rebuild UI) generates the UI prefabs, imports the UI sprites and fonts, writes the theme, level catalog and meta-upgrade assets and wires the prefabs into GAME_SETUP and `MainMenuLevel`. Re-running it overwrites manual edits to the UI prefabs; edit the builder instead. The theme keeps tuned styles unless `BevelStyles.Version` is bumped; the meta-upgrade tree is only written when missing.
  - `UICapture.Run` (needs graphics, so no `-nographics`) plays the level and the menu and writes review screenshots to `tasks/ui-redesign/screenshots/` (or `-captureOut <dir>`).
  - `UIPlaytest.Run` plays a game end to end (build, upgrade, targeting, speed, pause, wave, autosave, sell, Continue) and exits with the number of failed checks (`UIPLAYTEST PASS|FAIL` lines in the log).
  - Both back up and restore the player's progress (PlayerPrefs `3DTD.Progress`) and savegame.
  - Menu 3DTD > Progress grants every medal (and its Research) or resets progress and the savegame, for testing unlocks without playing through levels.

## UI architecture (`Scripts/UI/`, redesign of 2026-10)
The UI follows the "bevelled glass" design in `tasks/ui-redesign/` (concept B boards B1-B6, concept C build menu; the guide `3DTD UI refactor guide – Concept B & C.md` names the rules). It is uGUI + TextMesh Pro, built by `UIRedesignBuilder` into `Prefabs/UI/` (`HUD Canvas`, `Main Menu Canvas`, `Options Screen`).
- **Kit (`UI/Kit/`).** `UITheme` (asset in `Resources/UI/UITheme`, `UITheme.Current`) holds the colour tokens, the Sora/Manrope TMP fonts (`Assets/Fonts/`, OFL), sprites (`Sprites/UI/`, icons rendered from the mockup SVGs) and one `BevelStyleDef` per `BevelStyle`. `BevelGraphic` draws plates, buttons, chips and tiles as geometry (chamfer, rim gradient, body gradients, one sheen facet, inset lines, bottom band, 1 px anti-aliasing feather) with the default UI material. Controls: `BevelButton` (a `UnityEngine.UI.Selectable`; write it qualified because the game has its own global `Selectable`), `SegmentedControl` (also tabs, nav and difficulty tiles), `ToggleSwitch`, `Stepper`, `SimpleDropdown`, `MedalRow`, `PriceLabel` (red while unaffordable), `TooltipService` (one notched tooltip per canvas, on a layer above everything incl. the pause overlay; `TooltipService.For(element)` finds the element's) opened by `TooltipTrigger` (hover delay, static text or a `Provider` for live text; the builder's `UIBuild.Hint` adds one), `HitArea` (44 px touch targets, invisible raycast targets), `SafeAreaFitter`, `CanvasScaleOption` (1100 x 611 reference, `Expand`, times the HUD scale option). `UIFormat.Tabular` puts digits in `<mspace>` slots (TMP has no `tnum`). Hover feedback: `BevelButton` animates `BevelGraphic.Highlight` (lighter body, warm rim) on unscaled time and, per button kind (`UIBuild.HoverMotion`), scales the button or slides its content. The UI font assets carry no OpenType kerning/ligature/mark tables and have `getFontFeatures` off: the font engine imported wrong kerning pairs for Sora and Manrope that squished words ("al", "ad"); the builder's `DropFontFeatures` keeps it that way.
- **In game.** `GameStatDisplay` (resources, pause, speed, Next wave CTA, auto-wave), `BuildingManager` + `BuildTile` + `BuildTooltip` (rail), `UpgradePanelManager` + `UpgradeRow` (one per path) + `UpgradeCell` (one per module: owned / next with price and progress bar / later / locked by the cross-path limits, each with a tooltip) + `StatTile` (tower panel; stat grid order in `TowerStatInfo.Grid`, the mockups' "Burn" slot shows RADIUS), `SelectionIndicator` and `PlacementPreview` (world rings on runtime world-space canvases via `WorldRing`), `PauseMenu` (pause, game over, victory; resuming restores the previous speed).
- **Main menu.** `MainMenuScreen` (Continue, navigation), `LevelSelectScreen` + `LevelCard` + `DifficultyPopup` (levels from `Resources/Progress/LevelCatalog`, whose lane thumbnails the builder bakes from the scenes; a level unlocks when the previous one is cleared, Impossible after Hard; category tabs Beginner, Intermediate, Advanced, Expert, Space, defined in the builder's `LevelCategories`, the middle three still empty; the cards sit in a masked horizontal `ScrollRect` dragged with mouse, finger or wheel, and the popup follows the selected card), `EncyclopediaScreen` (palette read from the GAME_SETUP prefab; meters normalised against the highest value across towers), `MetaUpgradesScreen` (trees in `Resources/Progress/MetaUpgradeTree`; Research per first medal: 1/2/3/4 by difficulty), `OptionsScreen` + `OptionRow` (also opened from the pause menu).
