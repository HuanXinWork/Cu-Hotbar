# CuHotbar Changelog

> 中文更新日志：见 [changes.md](changes.md)

## v1.1.3

### Fixed

- Fixed right-dragging an item out of the hotbar (such as dragging medical supplies onto the health panel) mistakenly clearing the slot binding on release, returning plain right-click completely to vanilla item dragging.
- Fixed pressing a slot's key while an open container UI is active and the cursor hovers over an item of the same bound ID getting swallowed by re-binding; hovering an item with the same `id` now prioritizes that instance and continues into the normal hold or quick-use flow.
- Fixed slot hotkeys failing to pull items from nested bags inside bags or from currently open, reachable external containers (dropped bags, medkits, and scene crates): body inventory scanning now uses breadth-first traversal, external container pickup and return also support fixture crates without an `Item` component, and closing an external container no longer misclears bindings when `AutoRefill` is disabled.
- Fixed pulling an item out of a non-empty bag held in the main hand dropping that parent bag onto the ground when all other body slots are full; it now swaps and stashes the parent bag before taking the target item, and refreshes any open container UI immediately after moving items.

### Changed

- **Slot unbind gesture & container click-to-open**: in-game quick unbind now uses plain middle-click or `Alt+Right-click` instead of plain right-click, and a short stationary right-click on a container item in the same hotbar slot passes through to vanilla to open its container panel. Existing `SafeQuickUse` behavior (moving only the selection frame and consuming directly from bags) remains unchanged.

## v1.1.2

### Added

- **Top-bar UI scale presets & shared scaling**: lifted the `1.0x` scale cap on high-DPI displays while keeping the window centered when resizing, and added `90% / 100% / 115% / 130%` preset buttons in the top bar; syncs with `CuSaveManager`'s shared scale when present, or persists locally under `UI.UiScalePercent` when standalone.
- **Two-step confirmation guards**: "Clear all slots" and "Reset position" now ask for inline confirmation (`Confirm` / `Cancel`) before clearing bindings or resetting coordinates.

### Changed

- **Unified translucent monochrome wireframe theme**: updated the settings panel, main-menu button, pause-menu launcher button (`320x84` non-overlapping slot), and update notice banner to match the translucent black-and-white line style across `CuSaveManager`, `SkinSync`, and `HwAssistive`.
- **Scheme A typography & in-game HUD refinement**: raised panel fonts to the bold Scheme A scale (`32px` header, `22px` section titles, `20px` controls/labels, `18px` descriptions/value boxes); bumped in-game slot key and stack badges to `15px`, and refined the bottom status bar to a neutral white fill over a dark translucent track.
- **Standardized CI & release pipeline**: bound plugin version to single-source `BuildInfo.Version` and migrated GitHub Actions to the unified auto-tag release and daily Nightly pre-release workflow publishing Release/Debug bundles, standalone DLLs, and `SHA256SUMS.txt`.

### Fixed

- Fixed label wrapping and control clipping on slider rows, horizontal/vertical direction segments, and hotkey capture rows in English and at `130%` scale, and narrowed the top-bar drag region so it does not intercept scale preset clicks.

## v1.1.1

### Added

- **Same-key put away**: new optional toggle "Same key puts the item away" (on by default). Pressing a slot's key again while its item is in your main hand puts it away in the "original spot → empty body slot → dropped" order, so you no longer need to keep a spare empty slot for holstering; turning it off restores the old behavior (extra presses do nothing). Put-away takes priority over bind-by-pointing, so rebind by pressing once more after the item is away.

## v1.1.0

### Added

- **Quick unbind & clear all**: right-click any hotbar slot in-game to clear its item binding directly, or click "Clear all slots" at the bottom of Settings → Switch keys.
- **Automated tag release workflow**: upgraded GitHub Actions to native Node 24 actions; pushing a `v*` / `V*` tag now builds the Release binary and publishes packaged assets to GitHub Releases automatically.

### Fixed

- Fixed slot switch keys, the use key, and the settings hotkey failing to trigger while holding movement keys (`WASD`, `Space`) or other non-modifier keys; shortcut matching now checks only `Shift` / `Ctrl` / `Alt` modifier groups.
- Hid the hotbar during pause menus (including fade-out transitions), vanilla settings, death screens, world generation, and cutscenes, and lowered the hotbar Canvas sorting order below dragged item icons and full-screen overlays.
- Preserved item bindings in higher slots when reducing the slot count so they restore automatically when increased again, and prevented body respawns within the same run from clearing bindings when `AutoRefill` is disabled.
- Added input and item-use gating for `PlayerCamera.blockItemUsage` and `LiquidTransfer`; added a scan cooldown when a held firearm needs ammo while no matching ammo exists in inventory, plus safe fallback for `gasTime` field lookup.
- Fixed the first scroll-wheel step skipping slot 1, made plain left-click cancel hotkey capture instead of binding primary attack, expanded the Offset Y slider range to `-540..540`, and switched binding saves to atomic temp-file writes.

### Changed

- **Settings & About layout overhaul**: resized the standalone settings window to `980x740` centered on first open and adjusted the pause-menu launcher button position; added bordered section cards, visible slider tracks with live value boxes, split key settings into "Switch keys" and "Behavior & Assist" (6 sections total) with right-aligned controls, and arranged the About tab into a single-screen two-column link layout.
- **In-game hotbar visuals & runtime polish**: added an inset `3px` selection frame, dark status-bar backplates, centered item icons, and 4-way black pixel outlines on key and stack badges; switched hotkey labels to event-driven dirty updates, cached per-frame locale resolution, and disabled `UpdateChecker.OnGUI` once version check finishes with no update.

## v1.0.9

### Fixed

- Fixed item switching remotely stashing held items into dropped ground backpacks, placing items into the mouth slot (`slots[2]`), or forcing items into dismembered/incapacitated hands; added guards for `onlyHoldInHands` tools and worn gear.
- Fixed destroyed consumables bypassing `AutoRefill = false` and rebinding another stack item, and eliminated per-frame inventory scan allocations.
- Fixed consumable detection misclassifying bleach, mindwipe, IV saline/Ringer's solution, empty bottles, and slot-replacing items as safe quick-use consumables; added `nondescriptcan` detection.
- Fixed auto-reload inserting 0-round magazines, consuming loose rounds during semi/full-auto gas cycling, and stacking reload audio without cooldown; switched to `Body.CombineItems` for multiplayer sync and local Milky direct-feed magazine support.
- Fixed left-clicking hotbar slots punching/firing through UI by assigning the `"UI"` layer, restored 9-slice slot borders, added container weight bars, supported `Alt` `ItemLabel` hover binding, and updated built-in font resolution for Unity 2022.3.
- Blocked background hotbar keys and scroll switching while standalone/CuSaveManager panels, console, multiplayer chat, or input fields are active; added standalone raycast blocking and `BlackWhiteSkin` texture cleanup.

### Changed

- Wrapped multiplayer item swaps in `Casualties: Together` `MoveCommit` gestures to prevent false ground-drop packets on clients, and stabilized client-side slot persistence per character type.
- Upgraded the tiered logging system: Release builds emit concise lifecycle/warning logs with all debug calls and strings stripped at compile time; Debug builds enable full scoped flow tracing, grey in-game console output, and a dedicated rotating `hotbarData/CuHotbar.debug.log` with millisecond timestamps and frame numbers; CI now publishes both Release and Debug artifacts.

## v1.0.8

### Fixed

The About page repo and release links now point at Cu-Hotbar's own repository; they previously pointed at Cu-SaveManager by mistake.

### Other

Added Debug-only logging; release builds contain no debug strings.

## v1.0.7

### Added

- **About tab**: settings panel gains an "About" tab in the SkinSync-style centered layout, listing version / repo / latest release / author / dependencies.
- **UI language switch**: Settings → "Misc" adds three options (auto / Chinese / English); written to `I18n.PreferredLanguage` in the config file and persisted across restarts.
- **Auto reload**: on by default (Settings → "Switch Keys" section). When the firearm in your main hand is short of ammo (no magazine / direct-feed magazine not full / racked but chamber empty), the mod scans your inventory for matching `ammoType` and loads one round / magazine; full magazines are preferred. No keypress and no hand swap; if conditions aren't met it stays out of the way.

### Fixed

- **Embedded panel scrolls**: fixed the settings panel being unable to scroll and getting bottom content cut off when embedded in CuSaveManager's sidebar. Pushed `ScrollView` down into the settings content itself, and removed the outer ScrollView from the standalone window so the two paths don't nest.

## v1.0.6

### Changed

- **Resolution-aware scaling**: the settings panel now scales uniformly to the screen resolution so it no longer overflows.

### Fixed

- Fixed the top-right X close icon rendering wrong when the panel is scaled.

## v1.0.5

### Fixed

- **No longer steals other IMGUI text focus**: `ImGuiImeRecovery` only clears focus when the settings panel closes, instead of calling `FocusControl(null)` whenever an external TextField (e.g. KrokMP connection form) has keyboard focus; removed the redundant per-frame IME tick from `HotbarController`.

## v1.0.4

### Fixed

- Fixed slot switch keys and the use key stopping after Chinese IME input in CuSaveManager's panel or other IMGUI UIs. Closing the panel clears the stuck IMGUI keyboard focus.

## v1.0.3

### Fixed

- **Latest game UI compatibility**: adapted standalone pause-menu entry and scroll-input blocking to the new `PauseHandler`-based pause UI after the game removed the old `brightnessPanel` host.

## v1.0.2

### Added

- **Horizontal / vertical layout**: choose the hotbar's orientation. Horizontal defaults to the bottom of the screen, vertical to the right side; switching orientation applies the matching default position automatically.
- **Multi-row / multi-column**: "Rows" means rows when horizontal and columns when vertical; slots are laid out in a grid.
- **Bind by pointing**: point the mouse at an inventory cell or a world item and press a slot's switch key to bind that item to the slot — no dragging needed.
- **Safe quick use**: optional toggle; when on, pressing a slot key or scrolling to an edible / drinkable consumable (detected by its actual use behavior — whether it eats / drinks, covering foods classified as "custom" like geofruit, including buffs with side effects) selects it without switching to the main hand, leaving the held item untouched; whether it's used is up to your "use key" (default Shift+Right-click). The use key branches by selected-slot type: for a consumable it uses the bound item (which may be in a backpack), for anything else it only uses the item in the main hand — explosives, magazines, syringes etc. are not recognized as consumables and won't be triggered by the use key.

### Changed

- When switching items, a held item with nowhere to go is no longer dropped immediately: it goes to an empty body slot first, then into a backpack container that can hold it; only when neither works is it dropped, with an on-screen notice (the notice can be turned off in settings).

## v1.0.1

### Added

- **Per-slot switch keys**: every slot is now individually bindable and supports key combos (e.g. `Ctrl+1`). The first 9 slots still default to Keypad 1-9; extra slots are unbound by default.
- **Key badge on slots**: each slot's top-left badge now shows its actual bound key and updates instantly when rebound; blank when unbound.
- **Custom use key with combos**: the old "right-click to use" toggle is now a configurable key with combo support, defaulting to Shift+Right-click; clear the binding to disable it.
- **Multi-row layout**: a new "Rows" setting lays slots out in a grid across multiple rows.
- **Unlimited slot count**: the 9-slot cap is removed; add as many as you need.

### Changed

- Reworked the "Switch keys" settings section: removed the keypad/main-row base toggle and the right-click-use toggle, replaced with per-slot and use-key binding rows; slot count and rows use -/+ steppers.

### Notes

- Switch keys and the use key accept mouse buttons and combos; click the button then press a new key to bind, or "Clear" to unbind.
- Slot count and rows can keep growing; very large values may overflow the screen — adjust rows and scale accordingly.
