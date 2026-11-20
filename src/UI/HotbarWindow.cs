using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>未合并到 SaveManager 时使用的黑白风格独立模态设置窗口。</summary>
    internal sealed class HotbarWindow
    {
        private const int WindowId = 0x48425730;
        private const float Width = 980f;
        private const float Height = 740f;
        private const float TitleBarHeight = 64f;
        private const float CloseBtnSize = 48f;

        private readonly HotbarSettingsContent _content;
        private Rect _rect = new Rect(160f, 110f, Width, Height);
        private float _drawScale = 1f;
        private bool _posInitialized;

        internal bool Open { get; private set; }

        internal HotbarWindow(HotbarConfig cfg)
        {
            // 独立窗口已由外层 Draw() 统一管理 BlackWhiteSkin.Push/Pop，避免嵌套覆盖 _previousSkin
            _content = new HotbarSettingsContent(cfg, manageSkin: false);
        }

        internal bool IsCapturingHotkey => Open && _content.IsCapturingHotkey;

        internal bool ExpectsTextInput => IsCapturingHotkey;

        internal void CancelKeyCapture() => _content.CancelKeyCapture();

        internal void Toggle()
        {
            if (Open) { Close(); return; }
            Open = true;
            UiBlocker.Block();
            ModLog.Debug("UI", "设置面板已打开（快捷键 / 主菜单按钮）");
        }

        internal void OpenIfClosed()
        {
            if (Open) return;
            Open = true;
            UiBlocker.Block();
            ModLog.Debug("UI", "设置面板已打开（暂停菜单按钮）");
        }

        internal void Close()
        {
            if (!Open) return;
            Open = false;
            _content.CancelKeyCapture();
            ImGuiImeRecovery.RequestClear();
            UiBlocker.Unblock();
            ModLog.Debug("UI", "设置面板已关闭");
        }

        internal void Draw()
        {
            if (!Open) return;
            _drawScale = ComputeScale();
            _rect.width = Width;
            _rect.height = Height;
            float maxX = Mathf.Max(0f, Screen.width / _drawScale - Width);
            float maxY = Mathf.Max(0f, Screen.height / _drawScale - Height);
            if (!_posInitialized && Screen.width > 0 && Screen.height > 0)
            {
                // 首次展开按当前逻辑屏幕尺寸居中，后续保留用户拖拽后的窗口位置
                _rect.x = (Screen.width / _drawScale - Width) * 0.5f;
                _rect.y = (Screen.height / _drawScale - Height) * 0.5f;
                _posInitialized = true;
            }
            _rect.x = Mathf.Clamp(_rect.x, 0f, maxX);
            _rect.y = Mathf.Clamp(_rect.y, 0f, maxY);

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

        private static float ComputeScale()
        {
            float fit = Mathf.Min(Screen.width / Width, Screen.height / Height) * 0.92f;
            return Mathf.Clamp(fit, 0.3f, 1f);
        }

        private void DrawContent(int id)
        {
            BlackWhiteSkin.DrawBorder(new Rect(0f, 0f, Width, Height), 4f);

            GUI.Label(new Rect(28f, 12f, Width - CloseBtnSize - 56f, 40f),
                HotbarI18n.T("app.name"), BlackWhiteSkin.HeaderStyle);

            var closeRect = new Rect(Width - CloseBtnSize - 12f, 8f, CloseBtnSize, CloseBtnSize);
            if (GUI.Button(closeRect, GUIContent.none, BlackWhiteSkin.IconBtnStyle))
            {
                Close();
                GUIUtility.ExitGUI();
            }
            BlackWhiteSkin.DrawCloseX(new Rect(closeRect.x + 14f, closeRect.y + 14f,
                closeRect.width - 28f, closeRect.height - 28f), 4f);

            BlackWhiteSkin.DrawHLine(new Rect(0f, TitleBarHeight, Width, 2f));

            var bodyRect = new Rect(24f, TitleBarHeight + 12f, Width - 48f, Height - TitleBarHeight - 28f);
            GUILayout.BeginArea(bodyRect);
            _content.Draw();
            GUILayout.EndArea();

            GUI.DragWindow(new Rect(0f, 0f, Width - CloseBtnSize - 24f, TitleBarHeight));
        }
    }
}
