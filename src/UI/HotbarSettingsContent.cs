using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>可复用的快捷栏设置与关于页 IMGUI 内容，供独立窗口与 SaveManager 侧栏共用。</summary>
    internal sealed class HotbarSettingsContent
    {
        private const float LabelW = 220f;
        private const float RowH = 40f;

        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static HotbarSettingsContent _activeCapturingOwner;
        private static int _lastCapturingFrame = -10;

        private readonly HotbarConfig _cfg;
        private ConfigEntry<KeyboardShortcut> _capturing;
        private int _lastDrawnFrame = -10;
        private int _layoutSlotCount = 1;
        private int _tab;
        private Vector2 _scroll;

        internal HotbarSettingsContent(HotbarConfig cfg)
        {
            _cfg = cfg;
        }

        // 用 2 帧活跃窗口兜底宿主 SaveManager 关闭面板时无取消通知的场景，防止捕获态永久锁死输入
        internal static bool AnyCapturing =>
            _activeCapturingOwner != null
            && _activeCapturingOwner._capturing != null
            && Time.frameCount - _lastCapturingFrame <= 2;

        internal void CancelKeyCapture()
        {
            _capturing = null;
            if (ReferenceEquals(_activeCapturingOwner, this))
                _activeCapturingOwner = null;
        }

        internal bool IsCapturingHotkey => _capturing != null;

        internal void Draw()
        {
            int frame = Time.frameCount;
            if (_capturing != null && frame - _lastDrawnFrame > 2)
                CancelKeyCapture();
            _lastDrawnFrame = frame;

            if (_capturing != null)
            {
                _activeCapturingOwner = this;
                _lastCapturingFrame = frame;
            }

            if (Event.current == null || Event.current.type == EventType.Layout)
                _layoutSlotCount = Mathf.Clamp(_cfg.SlotCount.Value, 1, HotbarConfig.MaxSlots);

            BlackWhiteSkin.EnsureStyles();
            DrawTabBar();
            GUILayout.Space(8f);
            _scroll = GUILayout.BeginScrollView(_scroll,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_tab == 1) DrawAbout();
            else DrawSettings();
            GUILayout.EndScrollView();
            CaptureIfNeeded();
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            DrawTabBtn(HotbarI18n.T("tab.settings"), 0);
            GUILayout.Space(8f);
            DrawTabBtn(HotbarI18n.T("tab.about"), 1);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawTabBtn(string label, int idx)
        {
            var style = _tab == idx ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle;
            if (GUILayout.Button(label, style, GUILayout.MinWidth(180f), GUILayout.MinHeight(48f)))
            {
                CancelKeyCapture();
                _tab = idx;
            }
        }

        private void DrawAbout()
        {
            GUILayout.Space(12f);
            GUILayout.Label(HotbarI18n.T("about.title"), BlackWhiteSkin.CenterTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            GUILayout.Label(HotbarI18n.T("about.desc"), BlackWhiteSkin.CenterLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(HotbarI18n.F("about.version", ModLog.FormatDisplayVersion(Plugin.PluginVersion)),
                BlackWhiteSkin.CenterLabelStyle, GUILayout.ExpandWidth(true));

            GUILayout.Space(16f);
            GUILayout.Label(HotbarI18n.T("about.sec_links"), BlackWhiteSkin.CenterTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            DrawLinkButton(HotbarI18n.T("about.link_repo"), "https://github.com/HuanXinWork/Cu-Hotbar");
            DrawLinkButton(HotbarI18n.T("about.link_release"), "https://github.com/HuanXinWork/Cu-Hotbar/releases/latest");

            GUILayout.Space(16f);
            GUILayout.Label(HotbarI18n.T("about.sec_credits"), BlackWhiteSkin.CenterTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            DrawNameButton("huanxin886", "https://github.com/huanxin886");

            GUILayout.Space(16f);
            GUILayout.Label(HotbarI18n.T("about.sec_deps"), BlackWhiteSkin.CenterTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            DrawLinkButton("BepInEx", "https://github.com/BepInEx/BepInEx");
            DrawLinkButton("CuSaveManager", "https://github.com/HuanXinWork/Cu-SaveManager");
        }

        private static void DrawLinkButton(string label, string url)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, BlackWhiteSkin.TabStyle,
                GUILayout.MinWidth(560f), GUILayout.ExpandWidth(false), GUILayout.MinHeight(48f)))
            {
                try { Application.OpenURL(url); } catch { }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private static void DrawNameButton(string name, string url)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(name,
                GUILayout.MinWidth(280f), GUILayout.ExpandWidth(false), GUILayout.MinHeight(40f)))
            {
                try { Application.OpenURL(url); } catch { }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private void DrawSettings()
        {
            GUILayout.Label(HotbarI18n.T("hint.usage"), BlackWhiteSkin.HintLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);

            GUILayout.Label(HotbarI18n.T("sec.display"), BlackWhiteSkin.HeaderStyle);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            bool vis = DrawSwitch(HotbarI18n.T("sw.visible"), _cfg.Visible.Value);
            if (vis != _cfg.Visible.Value) _cfg.Visible.Value = vis;
            DrawIntStepper(HotbarI18n.F("fmt.slot_count", _cfg.SlotCount.Value), _cfg.SlotCount, 1, HotbarConfig.MaxSlots);
            DrawIntStepper(HotbarI18n.F(_cfg.Vertical.Value ? "fmt.col_count" : "fmt.row_count", _cfg.RowCount.Value), _cfg.RowCount, 1, HotbarConfig.MaxSlots);
            DrawDirectionRow();
            DrawFloatSlider(HotbarI18n.F("fmt.scale", _cfg.Scale.Value), _cfg.Scale, 0.5f, 2f);
            DrawFloatSlider(HotbarI18n.F("fmt.bg_alpha", _cfg.BackgroundAlpha.Value), _cfg.BackgroundAlpha, 0f, 1f);
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            GUILayout.Label(HotbarI18n.T("sec.keys"), BlackWhiteSkin.HeaderStyle);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            // 按 Layout 阶段锁定的槽数绘制，防止同帧点击 +/- 修改 SlotCount 导致 IMGUI 控件数不匹配异常
            for (int i = 0; i < _layoutSlotCount; i++)
            {
                DrawHotkeyRow(HotbarI18n.F("fmt.slot_hotkey", i + 1), _cfg.SlotHotkey(i));
            }
            DrawHotkeyRow(HotbarI18n.T("lbl.use_item"), _cfg.UseItemHotkey);
            bool sc = DrawSwitch(HotbarI18n.T("sw.enable_scroll"), _cfg.EnableScroll.Value);
            if (sc != _cfg.EnableScroll.Value) _cfg.EnableScroll.Value = sc;
            bool ar = DrawSwitch(HotbarI18n.T("sw.auto_refill"), _cfg.AutoRefill.Value);
            if (ar != _cfg.AutoRefill.Value) _cfg.AutoRefill.Value = ar;
            bool wod = DrawSwitch(HotbarI18n.T("sw.warn_on_drop"), _cfg.WarnOnDrop.Value);
            if (wod != _cfg.WarnOnDrop.Value) _cfg.WarnOnDrop.Value = wod;
            bool scu = DrawSwitch(HotbarI18n.T("sw.safe_quick_use"), _cfg.SafeQuickUse.Value);
            if (scu != _cfg.SafeQuickUse.Value) _cfg.SafeQuickUse.Value = scu;
            bool arl = DrawSwitch(HotbarI18n.T("sw.auto_reload"), _cfg.AutoReload.Value);
            if (arl != _cfg.AutoReload.Value) _cfg.AutoReload.Value = arl;
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            GUILayout.Label(HotbarI18n.T("sec.position"), BlackWhiteSkin.HeaderStyle);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawFloatSlider(HotbarI18n.F("fmt.anchor_x", _cfg.AnchorX.Value), _cfg.AnchorX, 0f, 1f);
            DrawFloatSlider(HotbarI18n.F("fmt.anchor_y", _cfg.AnchorY.Value), _cfg.AnchorY, 0f, 1f);
            DrawFloatSlider(HotbarI18n.F("fmt.offset_x", _cfg.OffsetX.Value), _cfg.OffsetX, -960f, 960f);
            DrawFloatSlider(HotbarI18n.F("fmt.offset_y", _cfg.OffsetY.Value), _cfg.OffsetY, 0f, 540f);
            if (GUILayout.Button(HotbarI18n.T("btn.reset_pos"), GUILayout.MaxWidth(260f), GUILayout.MinHeight(RowH)))
            {
                if (_cfg.Vertical.Value) ApplyVerticalDefaults();
                else ApplyHorizontalDefaults();
            }
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            GUILayout.Label(HotbarI18n.T("sec.hotkeys"), BlackWhiteSkin.HeaderStyle);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawHotkeyRow(HotbarI18n.T("lbl.hotkey_panel"), _cfg.ToggleSettingsHotkey);
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            GUILayout.Label(HotbarI18n.T("sec.misc"), BlackWhiteSkin.HeaderStyle);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            bool log = DrawSwitch(HotbarI18n.T("sw.show_log_in_console"), _cfg.ShowLogInConsole.Value);
            if (log != _cfg.ShowLogInConsole.Value) _cfg.ShowLogInConsole.Value = log;
            bool upd = DrawSwitch(HotbarI18n.T("sw.accept_update_notice"), _cfg.AcceptUpdateNotice.Value);
            if (upd != _cfg.AcceptUpdateNotice.Value) _cfg.AcceptUpdateNotice.Value = upd;
            DrawLanguageModeRow(HotbarI18n.T("lbl.language"), _cfg.PreferredLanguage);
            GUILayout.EndVertical();
        }

        private void DrawDirectionRow()
        {
            bool vertical = _cfg.Vertical.Value;
            GUILayout.BeginHorizontal();
            GUILayout.Label(HotbarI18n.T("lbl.direction"), BlackWhiteSkin.RowLabelStyle,
                GUILayout.MinWidth(LabelW), GUILayout.ExpandWidth(false), GUILayout.MinHeight(RowH));
            if (GUILayout.Button(HotbarI18n.T("opt.horizontal"),
                vertical ? BlackWhiteSkin.TabStyle : BlackWhiteSkin.TabActiveStyle,
                GUILayout.MinWidth(140f), GUILayout.MinHeight(RowH)))
            {
                if (vertical) { _cfg.Vertical.Value = false; ApplyHorizontalDefaults(); }
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(HotbarI18n.T("opt.vertical"),
                vertical ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle,
                GUILayout.MinWidth(140f), GUILayout.MinHeight(RowH)))
            {
                if (!vertical) { _cfg.Vertical.Value = true; ApplyVerticalDefaults(); }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void ApplyHorizontalDefaults()
        {
            _cfg.AnchorX.Value = 0.5f;
            _cfg.AnchorY.Value = 0f;
            _cfg.OffsetX.Value = 0f;
            _cfg.OffsetY.Value = 90f;
        }

        private void ApplyVerticalDefaults()
        {
            _cfg.AnchorX.Value = 1f;
            _cfg.AnchorY.Value = 0.5f;
            _cfg.OffsetX.Value = -90f;
            _cfg.OffsetY.Value = 0f;
        }

        private void DrawHotkeyRow(string label, ConfigEntry<KeyboardShortcut> entry)
        {
            bool capturing = ReferenceEquals(_capturing, entry);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.MinWidth(LabelW), GUILayout.ExpandWidth(false), GUILayout.MinHeight(RowH));
            string shown = capturing
                ? HotbarI18n.T("btn.press_a_key")
                : (entry.Value.MainKey == KeyCode.None ? HotbarI18n.T("btn.unbound") : entry.Value.ToString());
            if (GUILayout.Button(shown, GUILayout.MinWidth(200f), GUILayout.MinHeight(RowH)))
            {
                if (capturing) CancelKeyCapture();
                else
                {
                    _capturing = entry;
                    _activeCapturingOwner = this;
                    _lastCapturingFrame = Time.frameCount;
                }
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(HotbarI18n.T("btn.clear"), GUILayout.MinWidth(100f), GUILayout.MinHeight(RowH)))
            {
                entry.Value = new KeyboardShortcut(KeyCode.None);
                ModLog.Debug("Config", $"已清除热键绑定：{entry.Definition.Key}");
                if (capturing) CancelKeyCapture();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void CaptureIfNeeded()
        {
            if (_capturing == null) return;
            var e = Event.current;
            if (e == null) return;

            KeyCode main = KeyCode.None;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && !IsModifier(e.keyCode))
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    CancelKeyCapture();
                    e.Use();
                    return;
                }
                main = e.keyCode;
            }
            else if (e.type == EventType.MouseDown && e.mousePosition.y >= 0f)
            {
                main = KeyCode.Mouse0 + e.button;
            }
            if (main == KeyCode.None) return;

            var target = _capturing;
            var shortcut = new KeyboardShortcut(main, CollectModifiers());
            CancelKeyCapture();
            target.Value = shortcut;
            ModLog.Debug("Config", $"已更新热键绑定：{target.Definition.Key} = {shortcut}");
            e.Use();
        }

        private static KeyCode[] CollectModifiers()
        {
            var mods = new List<KeyCode>(ModifierKeys.Length);
            for (int i = 0; i < ModifierKeys.Length; i++)
            {
                if (Input.GetKey(ModifierKeys[i])) mods.Add(ModifierKeys[i]);
            }
            return mods.ToArray();
        }

        private static bool IsModifier(KeyCode key)
        {
            for (int i = 0; i < ModifierKeys.Length; i++)
            {
                if (ModifierKeys[i] == key) return true;
            }
            return false;
        }

        private static bool DrawSwitch(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.MinWidth(LabelW), GUILayout.ExpandWidth(false), GUILayout.MinHeight(RowH));
            GUILayout.Space(20f);
            bool result = value;
            if (GUILayout.Button(HotbarI18n.T("sw.on"),
                value ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle,
                GUILayout.MinWidth(110f), GUILayout.MinHeight(RowH)))
            {
                result = true;
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(HotbarI18n.T("sw.off"),
                value ? BlackWhiteSkin.TabStyle : BlackWhiteSkin.TabActiveStyle,
                GUILayout.MinWidth(110f), GUILayout.MinHeight(RowH)))
            {
                result = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return result;
        }

        private static void DrawLanguageModeRow(string label, ConfigEntry<string> entry)
        {
            string mode = HotbarI18n.NormalizeLanguageMode(entry.Value);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle,
                GUILayout.MinWidth(LabelW), GUILayout.ExpandWidth(false), GUILayout.MinHeight(RowH));
            GUILayout.Space(20f);
            if (GUILayout.Button(HotbarI18n.T("opt.language_auto"),
                mode == "auto" ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle,
                GUILayout.MinWidth(140f), GUILayout.MinHeight(RowH)))
            {
                entry.Value = "auto";
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(HotbarI18n.T("opt.language_zh"),
                mode == "zh" ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle,
                GUILayout.MinWidth(120f), GUILayout.MinHeight(RowH)))
            {
                entry.Value = "zh";
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(HotbarI18n.T("opt.language_en"),
                mode == "en" ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle,
                GUILayout.MinWidth(120f), GUILayout.MinHeight(RowH)))
            {
                entry.Value = "en";
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void DrawIntStepper(string label, ConfigEntry<int> entry, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.MinWidth(LabelW), GUILayout.ExpandWidth(false), GUILayout.MinHeight(RowH));
            GUILayout.Space(20f);
            if (GUILayout.Button("-", GUILayout.MinWidth(60f), GUILayout.MinHeight(RowH)))
            {
                if (entry.Value > min) entry.Value -= 1;
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("+", GUILayout.MinWidth(60f), GUILayout.MinHeight(RowH)))
            {
                if (entry.Value < max) entry.Value += 1;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void DrawFloatSlider(string label, ConfigEntry<float> entry, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.MinWidth(LabelW), GUILayout.ExpandWidth(false), GUILayout.MinHeight(RowH));
            float v = GUILayout.HorizontalSlider(entry.Value, min, max,
                GUILayout.MinWidth(280f), GUILayout.ExpandWidth(true));
            if (Math.Abs(v - entry.Value) > 0.0001f) entry.Value = v;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
