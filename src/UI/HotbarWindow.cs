using System;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>未合并到 SaveManager 时使用的半透明黑白风格独立模态设置窗口。</summary>
    internal sealed class HotbarWindow
    {
        private const int WindowId = 0x48425730;
        private const float BaseWinW = 1120f;
        private const float BaseWinH = 780f;
        private const float MinWinW = 960f;
        private const float MinWinH = 680f;
        private const float TitleBarHeight = 68f;
        private const float CloseBtnSize = 50f;
        private const float ScaleBtnH = 40f;

        private static readonly int[] ScalePresets = { 90, 100, 115, 130 };
        private static readonly string[] ScalePresetLabels = { "90%", "100%", "115%", "130%" };

        private readonly HotbarConfig _cfg;
        private readonly HotbarSettingsContent _content;
        private Rect _rect = new Rect(160f, 100f, BaseWinW, BaseWinH);
        private float _drawScale = 1f;
        private float _winW = BaseWinW;
        private float _winH = BaseWinH;
        private float _lastDrawScale = 1f;
        private float _lastWinW = BaseWinW;
        private float _lastWinH = BaseWinH;
        private bool _posInitialized;

        internal bool Open { get; private set; }

        internal HotbarWindow(HotbarConfig cfg)
        {
            _cfg = cfg;
            _content = new HotbarSettingsContent(cfg, manageSkin: false);
        }

        internal bool IsCapturingHotkey => Open && _content.IsCapturingHotkey;

        internal bool ExpectsTextInput => IsCapturingHotkey;

        internal void CancelKeyCapture() => _content.CancelKeyCapture();

        internal void Toggle()
        {
            if (Open) { Close(); return; }
            _content.ResetTransientState();
            Open = true;
            UiBlocker.Block();
            ModLog.Debug("UI", "设置面板已打开（快捷键 / 主菜单按钮）");
        }

        internal void OpenIfClosed()
        {
            if (Open) return;
            _content.ResetTransientState();
            Open = true;
            UiBlocker.Block();
            ModLog.Debug("UI", "设置面板已打开（暂停菜单按钮）");
        }

        internal void Close()
        {
            if (!Open) return;
            Open = false;
            _content.ResetTransientState();
            ImGuiImeRecovery.RequestClear();
            UiBlocker.Unblock();
            ModLog.Debug("UI", "设置面板已关闭");
        }

        internal void Draw()
        {
            if (!Open) return;
            ComputeWindowTransform();

            BlackWhiteSkin.Push();
            Matrix4x4 prev = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(_drawScale, _drawScale, 1f));
                _rect = GUI.ModalWindow(WindowId, _rect, DrawContent, "");
            }
            finally
            {
                GUI.matrix = prev;
                BlackWhiteSkin.Pop();
            }
        }

        private void ComputeWindowTransform()
        {
            int effectiveScalePercent = SaveManagerTabBridge.GetEffectiveUiScalePercent(_cfg);
            float sw = Mathf.Max(1f, Screen.width);
            float sh = Mathf.Max(1f, Screen.height);

            float dpiBase = Mathf.Min(sw / 1706.67f, sh / 1066.67f);
            float desired = dpiBase * (effectiveScalePercent / 100f);
            float maxScaleW = Mathf.Max(0.4f, (sw - 24f) / MinWinW);
            float maxScaleH = Mathf.Max(0.4f, (sh - 24f) / MinWinH);
            float drawScale = Mathf.Clamp(desired, 0.4f, Mathf.Min(maxScaleW, maxScaleH));
            float winW = Mathf.Clamp(Mathf.Floor((sw - 24f) / drawScale), MinWinW, BaseWinW);
            float winH = Mathf.Clamp(Mathf.Floor((sh - 24f) / drawScale), MinWinH, BaseWinH);

            if (!_posInitialized && Screen.width > 0 && Screen.height > 0)
            {
                // 首次展开按当前逻辑屏幕尺寸居中，后续保留用户拖拽后的窗口位置
                _rect = new Rect((sw / drawScale - winW) * 0.5f, (sh / drawScale - winH) * 0.5f, winW, winH);
                _posInitialized = true;
            }
            else
            {
                bool changed = Mathf.Abs(drawScale - _lastDrawScale) > 0.0001f
                    || Mathf.Abs(winW - _lastWinW) > 0.01f
                    || Mathf.Abs(winH - _lastWinH) > 0.01f;
                if (changed)
                {
                    // 缩放或逻辑尺寸变化时锚定旧窗口屏幕像素中心，防止切换档位后窗口漂移
                    float oldScreenCx = (_rect.x + _lastWinW * 0.5f) * _lastDrawScale;
                    float oldScreenCy = (_rect.y + _lastWinH * 0.5f) * _lastDrawScale;
                    _rect.x = oldScreenCx / drawScale - winW * 0.5f;
                    _rect.y = oldScreenCy / drawScale - winH * 0.5f;
                }
                _rect.width = winW;
                _rect.height = winH;
            }

            float maxX = Mathf.Max(0f, sw / drawScale - winW);
            float maxY = Mathf.Max(0f, sh / drawScale - winH);
            _rect.x = Mathf.Clamp(_rect.x, 0f, maxX);
            _rect.y = Mathf.Clamp(_rect.y, 0f, maxY);

            _drawScale = drawScale;
            _winW = winW;
            _winH = winH;
            _lastDrawScale = drawScale;
            _lastWinW = winW;
            _lastWinH = winH;
        }

        private void DrawContent(int id)
        {
            // GUI.ModalWindow 会静默吞掉普通异常，必须在回调内部兜底关窗以释放 UiBlocker 鼠标遮挡
            try
            {
                BlackWhiteSkin.DrawBorder(new Rect(0f, 0f, _winW, _winH), 5f);

                var closeRect = new Rect(_winW - CloseBtnSize - 12f, 9f, CloseBtnSize, CloseBtnSize);
                DrawScaleSelector(closeRect.x - 14f, out float scaleGroupLeftX);

                float titleW = Mathf.Max(160f, scaleGroupLeftX - 40f);
                BlackWhiteSkin.DrawShadowedLabel(
                    new Rect(28f, 8f, titleW, 52f),
                    HotbarI18n.T("app.name"),
                    BlackWhiteSkin.HeaderStyle);

                if (GUI.Button(closeRect, GUIContent.none, BlackWhiteSkin.IconBtnStyle))
                {
                    BlackWhiteSkin.ClearTextFocus();
                    Close();
                    GUIUtility.ExitGUI();
                }
                BlackWhiteSkin.DrawBorder(closeRect, 4f);
                BlackWhiteSkin.DrawCloseX(new Rect(closeRect.x + 14f, closeRect.y + 14f,
                    closeRect.width - 28f, closeRect.height - 28f), 5f);

                BlackWhiteSkin.DrawHLine(new Rect(0f, TitleBarHeight, _winW, 4f));

                var bodyRect = new Rect(24f, TitleBarHeight + 14f, _winW - 48f, _winH - TitleBarHeight - 30f);
                GUILayout.BeginArea(bodyRect);
                try
                {
                    _content.Draw();
                }
                finally
                {
                    GUILayout.EndArea();
                }

                // 拖拽区必须在缩放控件组左缘之前截止，避免抢占缩放档位与关闭按钮的鼠标点击
                float dragW = Mathf.Max(80f, scaleGroupLeftX - 8f);
                GUI.DragWindow(new Rect(0f, 0f, dragW, TitleBarHeight));
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ModLog.Warning("UI", $"HotbarWindow.DrawContent 异常：{ex}");
                Close();
            }
        }

        private void DrawScaleSelector(float rightX, out float groupLeftX)
        {
            const float btnGap = 6f;
            int currentPct = SaveManagerTabBridge.GetEffectiveUiScalePercent(_cfg);
            string scaleLabel = HotbarI18n.T("lbl.ui_scale");
            float labelW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.MutedLabelStyle, scaleLabel, 56f, 6f);

            float btnW0 = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ScaleBtnStyle, ScalePresetLabels[0], 64f, 10f);
            float btnW1 = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ScaleBtnStyle, ScalePresetLabels[1], 72f, 10f);
            float btnW2 = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ScaleBtnStyle, ScalePresetLabels[2], 72f, 10f);
            float btnW3 = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ScaleBtnStyle, ScalePresetLabels[3], 72f, 10f);

            float totalW = labelW + btnGap + btnW0 + btnGap + btnW1 + btnGap + btnW2 + btnGap + btnW3;
            groupLeftX = rightX - totalW;
            float y = (TitleBarHeight - ScaleBtnH) * 0.5f;

            GUI.Label(new Rect(groupLeftX, y, labelW, ScaleBtnH), scaleLabel, BlackWhiteSkin.MutedLabelStyle);

            float x = groupLeftX + labelW + btnGap;
            DrawScalePresetButton(ref x, y, btnW0, btnGap, ScalePresets[0], ScalePresetLabels[0], currentPct);
            DrawScalePresetButton(ref x, y, btnW1, btnGap, ScalePresets[1], ScalePresetLabels[1], currentPct);
            DrawScalePresetButton(ref x, y, btnW2, btnGap, ScalePresets[2], ScalePresetLabels[2], currentPct);
            DrawScalePresetButton(ref x, y, btnW3, btnGap, ScalePresets[3], ScalePresetLabels[3], currentPct);
        }

        private void DrawScalePresetButton(
            ref float x,
            float y,
            float width,
            float gap,
            int preset,
            string label,
            int currentPct)
        {
            bool active = Mathf.Abs(currentPct - preset) <= 2;
            GUIStyle style = active ? BlackWhiteSkin.ScaleBtnActiveStyle : BlackWhiteSkin.ScaleBtnStyle;
            if (GUI.Button(new Rect(x, y, width, ScaleBtnH), label, style))
            {
                BlackWhiteSkin.ClearTextFocus();
                SaveManagerTabBridge.SetEffectiveUiScalePercent(_cfg, preset);
                GUIUtility.ExitGUI();
            }
            x += width + gap;
        }
    }
}
