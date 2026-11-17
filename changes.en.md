# CuHotbar Changelog

> 中文更新日志：见 [changes.md](changes.md)

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
