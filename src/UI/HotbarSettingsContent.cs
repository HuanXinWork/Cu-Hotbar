using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>可复用的快捷栏设置与关于页 IMGUI 内容，供独立窗口与 SaveManager 侧栏共用。</summary>
    internal sealed class HotbarSettingsContent
    {
        private const float RowH = 46f;
        private const float InlineBtnH = 40f;
        private const float ActionBtnH = 44f;
        private const float TabBtnH = 48f;
        private const float RowGap = 8f;
        private const float ConfirmDurationSeconds = 4f;

        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static readonly string[] SliderLabelKeys =
        {
            "lbl.scale",
            "lbl.bg_alpha",
            "lbl.anchor_x",
            "lbl.anchor_y",
            "lbl.offset_x",
            "lbl.offset_y",
        };

        private static HotbarSettingsContent _activeCapturingOwner;
        private static int _lastCapturingFrame = -10;

        private readonly HotbarConfig _cfg;
        private readonly bool _manageSkin;
        private ConfigEntry<KeyboardShortcut> _capturing;
        private int _lastDrawnFrame = -10;
        private int _layoutSlotCount = 1;
        private bool _layoutClearConfirm;
        private bool _layoutResetConfirm;
        private float _clearSlotsConfirmUntil;
        private float _resetPosConfirmUntil;
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

        internal void ResetTransientState()
        {
            CancelKeyCapture();
            _clearSlotsConfirmUntil = 0f;
            _resetPosConfirmUntil = 0f;
            _layoutClearConfirm = false;
            _layoutResetConfirm = false;
        }

        internal bool IsCapturingHotkey => _capturing != null;

        internal void Draw()
        {
            int frame = Time.frameCount;
            if (frame - _lastDrawnFrame > 2)
                ResetTransientState();
            _lastDrawnFrame = frame;

            if (_capturing != null)
            {
                _activeCapturingOwner = this;
                _lastCapturingFrame = frame;
            }

            // 在 Layout 阶段冻结动态控件分支快照，防止同帧点击切换状态导致 Repaint 控件计数失配
            if (Event.current == null || Event.current.type == EventType.Layout)
            {
                _layoutSlotCount = Mathf.Clamp(_cfg.SlotCount.Value, 1, HotbarConfig.MaxSlots);
                float now = Time.unscaledTime;
                _layoutClearConfirm = now < _clearSlotsConfirmUntil;
                _layoutResetConfirm = now < _resetPosConfirmUntil;
            }

            if (_manageSkin) BlackWhiteSkin.Push();
            else BlackWhiteSkin.EnsureStyles();

            try
            {
                DrawTabBar();
                GUILayout.Space(8f);
                // 显式关闭横向滚动条，确保嵌入宿主侧栏或独立窗时内部卡片始终贴合视口宽度不撑破横轴
                _scroll = GUILayout.BeginScrollView(
                    _scroll,
                    false,
                    false,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUI.skin.scrollView,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                try
                {
                    if (_tab == 1) DrawAbout();
                    else DrawSettings();
                }
                finally
                {
                    GUILayout.EndScrollView();
                }
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
                BlackWhiteSkin.MutedLabelStyle, GUILayout.Height(TabBtnH));
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            Rect lineRect = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            BlackWhiteSkin.DrawHLine(lineRect);
        }

        private void DrawTabBtn(string label, int idx)
        {
            var style = _tab == idx ? BlackWhiteSkin.TabActiveStyle : BlackWhiteSkin.TabStyle;
            float width = BlackWhiteSkin.CalcWidth(style, label, 160f, 16f);
            if (GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(TabBtnH)))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (_tab != idx)
                {
                    _scroll = Vector2.zero;
                    ResetTransientState();
                    _tab = idx;
                    GUILayout.EndHorizontal();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawAbout()
        {
            GUILayout.Space(4f);
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            GUILayout.Label(HotbarI18n.T("about.title"), BlackWhiteSkin.CenterTitleStyle,
                GUILayout.ExpandWidth(true), GUILayout.Height(52f));
            GUILayout.Space(6f);
            GUILayout.Label(HotbarI18n.T("about.desc"), BlackWhiteSkin.CenterLabelStyle,
                GUILayout.ExpandWidth(true), GUILayout.MinHeight(40f));
            GUILayout.Space(4f);
            GUILayout.Label(HotbarI18n.F("about.version", ModLog.FormatDisplayVersion(Plugin.PluginVersion)),
                BlackWhiteSkin.CenterLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(40f));
            GUILayout.EndVertical();

            GUILayout.Space(14f);
            GUILayout.Label(HotbarI18n.T("about.sec_links"), BlackWhiteSkin.CenterSubTitleStyle,
                GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            GUILayout.Space(8f);
            DrawDualLinkRow(
                HotbarI18n.T("about.link_repo"), "https://github.com/HuanXinWork/Cu-Hotbar",
                HotbarI18n.T("about.link_release"), "https://github.com/HuanXinWork/Cu-Hotbar/releases/latest");

            GUILayout.Space(14f);
            GUILayout.Label(HotbarI18n.T("about.sec_credits"), BlackWhiteSkin.CenterSubTitleStyle,
                GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            GUILayout.Space(8f);
            DrawSingleLinkRow("huanxin886", "https://github.com/huanxin886");

            GUILayout.Space(14f);
            GUILayout.Label(HotbarI18n.T("about.sec_deps"), BlackWhiteSkin.CenterSubTitleStyle,
                GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            GUILayout.Space(8f);
            DrawDualLinkRow(
                "BepInEx", "https://github.com/BepInEx/BepInEx",
                "CuSaveManager", "https://github.com/HuanXinWork/Cu-SaveManager");
            GUILayout.Space(6f);
        }

        private static void DrawDualLinkRow(string leftLabel, string leftUrl, string rightLabel, string rightUrl)
        {
            float leftW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnStyle, leftLabel, 240f, 16f);
            float rightW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnStyle, rightLabel, 240f, 16f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(leftLabel, BlackWhiteSkin.ActionBtnStyle, GUILayout.Width(leftW), GUILayout.Height(ActionBtnH)))
            {
                BlackWhiteSkin.ClearTextFocus();
                OpenExternalUrl(leftUrl);
            }
            GUILayout.Space(12f);
            if (GUILayout.Button(rightLabel, BlackWhiteSkin.ActionBtnStyle, GUILayout.Width(rightW), GUILayout.Height(ActionBtnH)))
            {
                BlackWhiteSkin.ClearTextFocus();
                OpenExternalUrl(rightUrl);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void DrawSingleLinkRow(string label, string url)
        {
            float width = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnStyle, label, 240f, 16f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, BlackWhiteSkin.ActionBtnStyle, GUILayout.Width(width), GUILayout.Height(ActionBtnH)))
            {
                BlackWhiteSkin.ClearTextFocus();
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
            float sliderLabelW = ComputeSliderLabelWidth();

            GUILayout.BeginVertical(BlackWhiteSkin.HintCardStyle);
            GUILayout.Label(HotbarI18n.T("hint.usage"), BlackWhiteSkin.HintLabelStyle,
                GUILayout.ExpandWidth(true), GUILayout.MinHeight(40f));
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
            DrawFloatSlider(HotbarI18n.T("lbl.scale"), _cfg.Scale, 0.5f, 2f, "0.00", sliderLabelW);
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.bg_alpha"), _cfg.BackgroundAlpha, 0f, 1f, "0.00", sliderLabelW);
            GUILayout.EndVertical();

            DrawSectionHeader(HotbarI18n.T("sec.keys"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);
            for (int i = 0; i < _layoutSlotCount; i++)
            {
                DrawHotkeyRow(HotbarI18n.F("fmt.slot_hotkey_clean", i + 1), _cfg.SlotHotkey(i));
                GUILayout.Space(RowGap);
            }
            DrawHotkeyRow(HotbarI18n.T("lbl.use_item_clean"), _cfg.UseItemHotkey);
            GUILayout.Space(RowGap);
            DrawConfirmableActionRow(
                HotbarI18n.T("btn.clear_all_slots"),
                HotbarI18n.T("btn.confirm_clear_all"),
                _layoutClearConfirm,
                ref _clearSlotsConfirmUntil,
                () => HotbarRuntime.Model?.ClearAll());
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
            DrawFloatSlider(HotbarI18n.T("lbl.anchor_x"), _cfg.AnchorX, 0f, 1f, "0.00", sliderLabelW);
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.anchor_y"), _cfg.AnchorY, 0f, 1f, "0.00", sliderLabelW);
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.offset_x"), _cfg.OffsetX, -960f, 960f, "0", sliderLabelW);
            GUILayout.Space(RowGap);
            DrawFloatSlider(HotbarI18n.T("lbl.offset_y"), _cfg.OffsetY, -540f, 540f, "0", sliderLabelW);
            GUILayout.Space(RowGap);
            DrawConfirmableActionRow(
                HotbarI18n.T("btn.reset_pos"),
                HotbarI18n.T("btn.confirm_reset_pos"),
                _layoutResetConfirm,
                ref _resetPosConfirmUntil,
                () =>
                {
                    if (_cfg.Vertical.Value) ApplyVerticalDefaults();
                    else ApplyHorizontalDefaults();
                });
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

        private static float ComputeSliderLabelWidth()
        {
            float width = 208f;
            var style = BlackWhiteSkin.SingleLineLabelStyle;
            for (int i = 0; i < SliderLabelKeys.Length; i++)
            {
                width = Mathf.Max(width, BlackWhiteSkin.CalcWidth(style, HotbarI18n.T(SliderLabelKeys[i]), 208f, 10f));
            }
            return width;
        }

        private static void DrawSectionHeader(string title)
        {
            GUILayout.Space(12f);
            GUILayout.Label(title, BlackWhiteSkin.SectionHeaderStyle, GUILayout.Height(42f));
            GUILayout.Space(4f);
        }

        // 用零外边距矩形槽统一定位行尾控件组，消除不同子控件个数下 IMGUI margin 累加导致的边线错位
        private static Rect ReserveRightSlot(float width, float height = InlineBtnH)
        {
            Rect slot = GUILayoutUtility.GetRect(width, RowH, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            float y = slot.y + Mathf.Max(0f, Mathf.Floor((slot.height - height) * 0.5f));
            return new Rect(slot.x, y, width, height);
        }

        private static void DrawConfirmableActionRow(
            string defaultLabel,
            string confirmLabel,
            bool confirming,
            ref float confirmUntil,
            Action onConfirmed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (!confirming)
            {
                float btnW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnStyle, defaultLabel, 208f, 16f);
                if (GUILayout.Button(defaultLabel, BlackWhiteSkin.ActionBtnStyle,
                    GUILayout.Width(btnW), GUILayout.Height(ActionBtnH)))
                {
                    BlackWhiteSkin.ClearTextFocus();
                    confirmUntil = Time.unscaledTime + ConfirmDurationSeconds;
                }
            }
            else
            {
                string cancelLabel = HotbarI18n.T("btn.cancel");
                float confirmW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnActiveStyle, confirmLabel, 156f, 16f);
                float cancelW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnStyle, cancelLabel, 108f, 16f);

                if (GUILayout.Button(confirmLabel, BlackWhiteSkin.ActionBtnActiveStyle,
                    GUILayout.Width(confirmW), GUILayout.Height(ActionBtnH)))
                {
                    BlackWhiteSkin.ClearTextFocus();
                    confirmUntil = 0f;
                    onConfirmed?.Invoke();
                }
                GUILayout.Space(8f);
                if (GUILayout.Button(cancelLabel, BlackWhiteSkin.ActionBtnStyle,
                    GUILayout.Width(cancelW), GUILayout.Height(ActionBtnH)))
                {
                    BlackWhiteSkin.ClearTextFocus();
                    confirmUntil = 0f;
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawSwitch(string label, ConfigEntry<bool> entry)
        {
            bool val = entry.Value;
            string onText = HotbarI18n.T("sw.on");
            string offText = HotbarI18n.T("sw.off");
            float btnW = Mathf.Max(96f, Mathf.Max(
                BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, onText, 96f, 8f),
                BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, offText, 96f, 8f)));
            float totalW = btnW * 2f + 8f;

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(totalW);
            if (GUI.Button(new Rect(r.x, r.y, btnW, r.height), onText,
                val ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (!val) entry.Value = true;
            }
            if (GUI.Button(new Rect(r.x + btnW + 8f, r.y, btnW, r.height), offText,
                val ? BlackWhiteSkin.ControlBtnStyle : BlackWhiteSkin.ControlBtnActiveStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (val) entry.Value = false;
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawIntStepper(string label, ConfigEntry<int> entry, int min, int max)
        {
            string valText = entry.Value.ToString();
            float boxW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ValueBoxStyle, valText, 78f, 8f);
            const float stepBtnW = 58f;
            float totalW = stepBtnW * 2f + boxW + 12f;

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(totalW);
            if (GUI.Button(new Rect(r.x, r.y, stepBtnW, r.height), "-", BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (entry.Value > min) entry.Value -= 1;
            }
            GUI.Label(new Rect(r.x + stepBtnW + 6f, r.y, boxW, r.height), valText, BlackWhiteSkin.ValueBoxStyle);
            if (GUI.Button(new Rect(r.x + stepBtnW + 6f + boxW + 6f, r.y, stepBtnW, r.height), "+", BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (entry.Value < max) entry.Value += 1;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDirectionRow()
        {
            bool vertical = _cfg.Vertical.Value;
            string hText = HotbarI18n.T("opt.horizontal");
            string vText = HotbarI18n.T("opt.vertical");
            float btnW = Mathf.Max(96f, Mathf.Max(
                BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, hText, 96f, 8f),
                BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, vText, 96f, 8f)));
            float totalW = btnW * 2f + 8f;

            GUILayout.BeginHorizontal();
            GUILayout.Label(HotbarI18n.T("lbl.direction"), BlackWhiteSkin.RowLabelStyle,
                GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(totalW);
            if (GUI.Button(new Rect(r.x, r.y, btnW, r.height), hText,
                vertical ? BlackWhiteSkin.ControlBtnStyle : BlackWhiteSkin.ControlBtnActiveStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (vertical) { _cfg.Vertical.Value = false; ApplyHorizontalDefaults(); }
            }
            if (GUI.Button(new Rect(r.x + btnW + 8f, r.y, btnW, r.height), vText,
                vertical ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (!vertical) { _cfg.Vertical.Value = true; ApplyVerticalDefaults(); }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawFloatSlider(string label, ConfigEntry<float> entry, float min, float max, string format, float labelWidth)
        {
            string text = format == "0" ? Mathf.RoundToInt(entry.Value).ToString() : entry.Value.ToString(format);
            float boxW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ValueBoxStyle, text, 88f, 8f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.SingleLineLabelStyle,
                GUILayout.Width(labelWidth), GUILayout.Height(RowH));
            GUILayout.Space(10f);
            // 禁止给 HorizontalSlider 传 GUILayout.Height，否则会覆盖 fixedHeight=14f 拉胖轨道
            float v = GUILayout.HorizontalSlider(entry.Value, min, max, GUILayout.ExpandWidth(true));
            if (Math.Abs(v - entry.Value) > 0.0001f)
            {
                BlackWhiteSkin.ClearTextFocus();
                entry.Value = v;
            }
            GUILayout.Space(10f);
            Rect valSlot = ReserveRightSlot(boxW);
            GUI.Label(valSlot, text, BlackWhiteSkin.ValueBoxStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawHotkeyRow(string label, ConfigEntry<KeyboardShortcut> entry)
        {
            bool capturing = ReferenceEquals(_capturing, entry);
            string shown = capturing
                ? HotbarI18n.T("btn.press_a_key")
                : (entry.Value.MainKey == KeyCode.None ? HotbarI18n.T("btn.unbound") : entry.Value.ToString());
            string clearText = HotbarI18n.T("btn.clear");

            GUIStyle bindStyle = capturing ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle;
            float bindW = Mathf.Clamp(BlackWhiteSkin.CalcWidth(bindStyle, shown, 220f, 12f), 220f, 360f);
            float clearW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, clearText, 88f, 8f);
            float totalW = bindW + 8f + clearW;

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(totalW);
            if (GUI.Button(new Rect(r.x, r.y, bindW, r.height), shown, bindStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                if (capturing) CancelKeyCapture();
                else
                {
                    _capturing = entry;
                    _activeCapturingOwner = this;
                    _lastCapturingFrame = Time.frameCount;
                }
            }
            if (GUI.Button(new Rect(r.x + bindW + 8f, r.y, clearW, r.height), clearText, BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                entry.Value = new KeyboardShortcut(KeyCode.None);
                ModLog.Debug("Config", $"已清除热键绑定：{entry.Definition.Key}");
                if (capturing) CancelKeyCapture();
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawLanguageModeRow(string label, ConfigEntry<string> entry)
        {
            string mode = HotbarI18n.NormalizeLanguageMode(entry.Value);
            string autoText = HotbarI18n.T("opt.language_auto");
            string zhText = HotbarI18n.T("opt.language_zh");
            string enText = HotbarI18n.T("opt.language_en");

            float autoW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, autoText, 116f, 8f);
            float zhW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, zhText, 88f, 8f);
            float enW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ControlBtnStyle, enText, 88f, 8f);
            float totalW = autoW + 8f + zhW + 8f + enW;

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.RowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(RowH));
            GUILayout.Space(12f);
            Rect r = ReserveRightSlot(totalW);
            if (GUI.Button(new Rect(r.x, r.y, autoW, r.height), autoText,
                mode == "auto" ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                entry.Value = "auto";
            }
            if (GUI.Button(new Rect(r.x + autoW + 8f, r.y, zhW, r.height), zhText,
                mode == "zh" ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
                entry.Value = "zh";
            }
            if (GUI.Button(new Rect(r.x + autoW + 8f + zhW + 8f, r.y, enW, r.height), enText,
                mode == "en" ? BlackWhiteSkin.ControlBtnActiveStyle : BlackWhiteSkin.ControlBtnStyle))
            {
                BlackWhiteSkin.ClearTextFocus();
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
