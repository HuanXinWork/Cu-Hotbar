using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>可复用的快捷栏设置与关于页 IMGUI 内容，供独立窗口与 SaveManager 侧栏共用。</summary>
    internal sealed class HotbarSettingsContent
    {
        private const float RowH = 38f;
        private const float RowGap = 8f;
        private const float SliderLabelW = 170f;
        private const float CompactGroupW = 200f;
        private const float WideGroupW = 296f;

        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static HotbarSettingsContent _activeCapturingOwner;
        private static int _lastCapturingFrame = -10;

        private readonly HotbarConfig _cfg;
        private readonly bool _manageSkin;
        private ConfigEntry<KeyboardShortcut> _capturing;
        private int _lastDrawnFrame = -10;
        private int _layoutSlotCount = 1;
        private int _tab;
        private Vector2 _scroll;

        internal HotbarSettingsContent(HotbarConfig cfg) : this(cfg, true)
        {
        }

        internal HotbarSettingsContent(HotbarConfig cfg, bool manageSkin)
        {
            _cfg = cfg;
            _manageSkin = manageSkin;
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

            // 嵌入 SaveManager 侧栏时自主压入黑白皮肤以启用可见滑条轨道，独立窗则复用外层已压入皮肤
            if (_manageSkin) BlackWhiteSkin.Push();
            else BlackWhiteSkin.EnsureStyles();

            try
            {
                DrawTabBar();
                GUILayout.Space(8f);
                _scroll = GUILayout.BeginScrollView(_scroll,
                    GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (_tab == 1) DrawAbout();
                else DrawSettings();
                GUILayout.EndScrollView();
                CaptureIfNeeded();
            }
            finally
            {
                if (_manageSkin) BlackWhiteSkin.Pop();
            }
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            DrawTabBtn(HotbarI18n.T("tab.settings"), 0);
            GUILayout.Space(8f);
            DrawTabBtn(HotbarI18n.T("tab.about"), 1);
            GUILayout.FlexibleSpace();
            GUILayout.Label(ModLog.FormatDisplayVersion(Plugin.PluginVersion),
                BlackWhiteSkin.MutedLabelStyle, GUILayout.Height(44f));
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            Rect lineRect = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            BlackWhiteSkin.DrawHLine(lineRect);
        }

        private void DrawTabBtn(string label, int idx)
        {
            var style = _tab == idx ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(150f), GUILayout.Height(44f)))
            {
                if (_tab != idx) _scroll = Vector2.zero;
                CancelKeyCapture();
                _tab = idx;
            }
        }

        private void DrawAbout()
        {
            GUILayout.Space(4f);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            GUILayout.Label(HotbarI18n.T("about.title"), BlackWhiteSkin.CenterTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            GUILayout.Label(HotbarI18n.T("about.desc"), BlackWhiteSkin.CenterLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(4f);
            GUILayout.Label(HotbarI18n.F("about.version", ModLog.FormatDisplayVersion(Plugin.PluginVersion)),
                BlackWhiteSkin.CenterLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();

            GUILayout.Space(14f);
            GUILayout.Label(HotbarI18n.T("about.sec_links"), BlackWhiteSkin.CenterSubTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);
            DrawDualLinkRow(
                HotbarI18n.T("about.link_repo"), "https://github.com/HuanXinWork/Cu-Hotbar",
                HotbarI18n.T("about.link_release"), "https://github.com/HuanXinWork/Cu-Hotbar/releases/latest",
                44f);

            GUILayout.Space(14f);
            GUILayout.Label(HotbarI18n.T("about.sec_credits"), BlackWhiteSkin.CenterSubTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);
            DrawSingleLinkRow("huanxin886", "https://github.com/huanxin886", 42f);

            GUILayout.Space(14f);
            GUILayout.Label(HotbarI18n.T("about.sec_deps"), BlackWhiteSkin.CenterSubTitleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);
            DrawDualLinkRow(
                "BepInEx", "https://github.com/BepInEx/BepInEx",
                "CuSaveManager", "https://github.com/HuanXinWork/Cu-SaveManager",
                44f);
            GUILayout.Space(6f);
        }

        private static void DrawDualLinkRow(string leftLabel, string leftUrl, string rightLabel, string rightUrl, float height)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(leftLabel, BlackWhiteSkin.TabStyle, GUILayout.Width(240f), GUILayout.Height(height)))
            {
                OpenExternalUrl(leftUrl);
            }
            GUILayout.Space(12f);
            if (GUILayout.Button(rightLabel, BlackWhiteSkin.TabStyle, GUILayout.Width(240f), GUILayout.Height(height)))
            {
                OpenExternalUrl(rightUrl);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void DrawSingleLinkRow(string label, string url, float height)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, BlackWhiteSkin.TabStyle, GUILayout.Width(240f), GUILayout.Height(height)))
            {
                OpenExternalUrl(url);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void OpenExternalUrl(string url)
        {
            try { Application.OpenURL(url); } catch { }
        }

        private void DrawSettings()
        {
            GUILayout.BeginVertical(BlackWhiteSkin.HintCardStyle);
            GUILayout.Label(HotbarI18n.T("hint.usage"), BlackWhiteSkin.HintLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.display"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawSwitch(HotbarI18n.T("sw.visible"), _cfg.Visible);
            GUILayout.Space(RowGap);
            DrawIntStepper(HotbarI18n.T("lbl.slot_count"), _cfg.SlotCount, 1, HotbarConfig.MaxSlots);
            GUILayout.Space(RowGap);
            DrawIntStepper(HotbarI18n.T(_cfg.Vertical.Value ? "lbl.col_count" : "lbl.row_count"),
                _cfg.RowCount, 1, HotbarConfig.MaxSlots);
            GUILayout.Space(RowGap);
            DrawDirectionRow();
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.scale"), _cfg.Scale, 0.5f, 2f, "0.00");
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.bg_alpha"), _cfg.BackgroundAlpha, 0f, 1f, "0.00");
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.keys"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            // 按 Layout 阶段锁定的槽数绘制，防止同帧点击 +/- 修改 SlotCount 导致 IMGUI 控件数不匹配异常
            for (int i = 0; i < _layoutSlotCount; i++)
            {
                DrawHotkeyRow(HotbarI18n.F("fmt.slot_hotkey_clean", i + 1), _cfg.SlotHotkey(i));
                GUILayout.Space(RowGap);
            }
            DrawHotkeyRow(HotbarI18n.T("lbl.use_item_clean"), _cfg.UseItemHotkey);
            GUILayout.Space(RowGap);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect clearAllRect = GUILayoutUtility.GetRect(CompactGroupW, RowH, GUILayout.Width(CompactGroupW), GUILayout.Height(RowH));
            if (GUI.Button(clearAllRect, HotbarI18n.T("btn.clear_all_slots"), BlackWhiteSkin.ControlBtnStyle))
            {
                HotbarRuntime.Model?.ClearAll();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.behavior"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawSwitch(HotbarI18n.T("sw.enable_scroll"), _cfg.EnableScroll);
            GUILayout.Space(RowGap);
            DrawSwitch(HotbarI18n.T("sw.same_key_put_away"), _cfg.SameKeyPutAway);
            GUILayout.Space(RowGap);
            DrawSwitch(HotbarI18n.T("sw.auto_refill"), _cfg.AutoRefill);
            GUILayout.Space(RowGap);
            DrawSwitch(HotbarI18n.T("sw.warn_on_drop"), _cfg.WarnOnDrop);
            GUILayout.Space(RowGap);
            DrawSwitch(HotbarI18n.T("sw.safe_quick_use"), _cfg.SafeQuickUse);
            GUILayout.Space(RowGap);
            DrawSwitch(HotbarI18n.T("sw.auto_reload"), _cfg.AutoReload);
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.position"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawFloatSlider(HotbarI18n.T("lbl.anchor_x"), _cfg.AnchorX, 0f, 1f, "0.00");
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.anchor_y"), _cfg.AnchorY, 0f, 1f, "0.00");
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.offset_x"), _cfg.OffsetX, -960f, 960f, "0");
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.offset_y"), _cfg.OffsetY, -540f, 540f, "0");
            GUILayout.Space(RowGap);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect resetRect = GUILayoutUtility.GetRect(CompactGroupW, RowH, GUILayout.Width(CompactGroupW), GUILayout.Height(RowH));
            if (GUI.Button(resetRect, HotbarI18n.T("btn.reset_pos"), BlackWhiteSkin.ControlBtnStyle))
            {
                if (_cfg.Vertical.Value) ApplyVerticalDefaults();
                else ApplyHorizontalDefaults();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.hotkeys"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawHotkeyRow(HotbarI18n.T("lbl.hotkey_panel_clean"), _cfg.ToggleSettingsHotkey);
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.misc"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            DrawSwitch(HotbarI18n.T("sw.show_log_in_console"), _cfg.ShowLogInConsole);
            GUILayout.Space(RowGap);
            DrawSwitch(HotbarI18n.T("sw.accept_update_notice"), _cfg.AcceptUpdateNotice);
            GUILayout.Space(RowGap);
            DrawLanguageModeRow(HotbarI18n.T("lbl.language_clean"), _cfg.PreferredLanguage);
            GUILayout.EndVertical();

            GUILayout.Space(8f);
        }

        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(12f);
            GUILayout.Label(title, BlackWhiteSkin.SectionHeaderStyle);
            GUILayout.Space(4f);
        }

        // 用零外边距矩形槽统一定位行尾控件组，消除不同子控件个数下 IMGUI margin 累加导致的边线错位
        private static Rect ReserveRightSlot(float width)
        {
            Rect slot = GUILayoutUtility.GetRect(width, RowH, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            float y = slot.y + Mathf.Max(0f, Mathf.Floor((slot.height - RowH) * 0.5f));
            return new Rect(slot.x, y, width, RowH);
        }

        private static void DrawSwitch(string label, ConfigEntry<bool> entry)
        {
            bool val = entry.Value;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(CompactGroupW);
            if (GUI.Button(new Rect(r.x, r.y, 96f, RowH), HotbarI18n.T("sw.on"),
                val ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                if (!val) entry.Value = true;
            }
            if (GUI.Button(new Rect(r.x + 104f, r.y, 96f, RowH), HotbarI18n.T("sw.off"),
                val ? BlackWhiteSkin.ControlBtnStyle : BlackWhiteSkin.ControlBtnActiveStyle))
            {
                if (val) entry.Value = false;
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawIntStepper(string label, ConfigEntry<int> entry, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(CompactGroupW);
            if (GUI.Button(new Rect(r.x, r.y, 56f, RowH), "-", BlackWhiteSkin.ControlBtnStyle))
            {
                if (entry.Value > min) entry.Value -= 1;
            }
            GUI.Label(new Rect(r.x + 62f, r.y, 76f, RowH), entry.Value.ToString(), BlackWhiteSkin.ValueBoxStyle);
            if (GUI.Button(new Rect(r.x + 144f, r.y, 56f, RowH), "+", BlackWhiteSkin.ControlBtnStyle))
            {
                if (entry.Value < max) entry.Value += 1;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDirectionRow()
        {
            bool vertical = _cfg.Vertical.Value;
            GUILayout.BeginHorizontal();
            GUILayout.Label(HotbarI18n.T("lbl.direction"), BlackWhiteSkin.RowLabelStyle,
                GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(CompactGroupW);
            if (GUI.Button(new Rect(r.x, r.y, 96f, RowH), HotbarI18n.T("opt.horizontal"),
                vertical ? BlackWhiteSkin.ControlBtnStyle : BlackWhiteSkin.ControlBtnActiveStyle))
            {
                if (vertical) { _cfg.Vertical.Value = false; ApplyHorizontalDefaults(); }
            }
            if (GUI.Button(new Rect(r.x + 104f, r.y, 96f, RowH), HotbarI18n.T("opt.vertical"),
                vertical ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                if (!vertical) { _cfg.Vertical.Value = true; ApplyVerticalDefaults(); }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawFloatSlider(string label, ConfigEntry<float> entry, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.Width(SliderLabelW), GUILayout.Height(RowH));
            GUILayout.Space(8f);
            float v = GUILayout.HorizontalSlider(entry.Value, min, max,
                GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true), GUILayout.Height(RowH));
            if (Math.Abs(v - entry.Value) > 0.0001f) entry.Value = v;
            GUILayout.Space(10f);
            Rect valSlot = GUILayoutUtility.GetRect(76f, RowH, GUILayout.Width(76f), GUILayout.Height(RowH));
            string text = format == "0" ? Mathf.RoundToInt(entry.Value).ToString() : entry.Value.ToString(format);
            GUI.Label(new Rect(valSlot.x, valSlot.y + 1f, 76f, 36f), text, BlackWhiteSkin.ValueBoxStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawHotkeyRow(string label, ConfigEntry<KeyboardShortcut> entry)
        {
            bool capturing = ReferenceEquals(_capturing, entry);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(WideGroupW);
            string shown = capturing
                ? HotbarI18n.T("btn.press_a_key")
                : (entry.Value.MainKey == KeyCode.None ? HotbarI18n.T("btn.unbound") : entry.Value.ToString());
            if (GUI.Button(new Rect(r.x, r.y, 204f, RowH), shown,
                capturing ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                if (capturing) CancelKeyCapture();
                else
                {
                    _capturing = entry;
                    _activeCapturingOwner = this;
                    _lastCapturingFrame = Time.frameCount;
                }
            }
            if (GUI.Button(new Rect(r.x + 212f, r.y, 84f, RowH), HotbarI18n.T("btn.clear"), BlackWhiteSkin.ControlBtnStyle))
            {
                entry.Value = new KeyboardShortcut(KeyCode.None);
                ModLog.Debug("Config", $"已清除热键绑定：{entry.Definition.Key}");
                if (capturing) CancelKeyCapture();
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawLanguageModeRow(string label, ConfigEntry<string> entry)
        {
            string mode = HotbarI18n.NormalizeLanguageMode(entry.Value);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(WideGroupW);
            if (GUI.Button(new Rect(r.x, r.y, 112f, RowH), HotbarI18n.T("opt.language_auto"),
                mode == "auto" ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                entry.Value = "auto";
            }
            if (GUI.Button(new Rect(r.x + 120f, r.y, 84f, RowH), HotbarI18n.T("opt.language_zh"),
                mode == "zh" ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                entry.Value = "zh";
            }
            if (GUI.Button(new Rect(r.x + 212f, r.y, 84f, RowH), HotbarI18n.T("opt.language_en"),
                mode == "en" ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                entry.Value = "en";
            }
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

        private void CaptureIfNeeded()
        {
            if (_capturing == null) return;
            var e = Event.current;
            if (e == null) return;

            KeyCode main = KeyCode.None;
            KeyCode[] mods = null;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && !IsModifier(e.keyCode))
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    CancelKeyCapture();
                    e.Use();
                    return;
                }
                main = e.keyCode;
                mods = CollectModifiers();
            }
            else if (e.type == EventType.MouseDown && e.mousePosition.y >= 0f)
            {
                mods = CollectModifiers();
                // 无修饰键的纯左键点击视为取消录制，防止点击空白处误绑 Mouse0 导致局内无法左键攻击
                if (e.button == 0 && mods.Length == 0)
                {
                    CancelKeyCapture();
                    e.Use();
                    return;
                }
                if (e.button >= 0 && e.button <= 6)
                    main = KeyCode.Mouse0 + e.button;
            }
            if (main == KeyCode.None) return;

            var target = _capturing;
            var shortcut = new KeyboardShortcut(main, mods);
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
    }
}
