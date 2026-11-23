using System;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>独立模式下暂停表盘按钮尚未挂载时的右下角兜底唤起入口。</summary>
    internal sealed class InGameOverlay
    {
        // 与 HwAssistive / SkinSync / CuSaveManager 共享 320x84 规格与底部第 0 堆叠槽（24px），避免极端回退时按钮交叠
        private const float ButtonWidth = 320f;
        private const float ButtonHeight = 84f;
        private const float MarginRight = 24f;
        private const float MarginBottom = 24f;
        private const float BorderThickness = 5f;

        internal static bool ShouldShow()
        {
            if (PauseDialButtonInjector.IsMounted) return false;
            if (SettingsMenu.instance != null || UiBlocker.IsBlocking) return false;
            return PauseMenuCompat.IsPauseMenuOpen();
        }

        internal void Draw(Action onClick)
        {
            if (!ShouldShow()) return;

            float x = Screen.width - ButtonWidth - MarginRight;
            float y = Mathf.Clamp(Screen.height - ButtonHeight - MarginBottom, 12f, Screen.height - ButtonHeight - 12f);
            var rect = new Rect(x, y, ButtonWidth, ButtonHeight);

            bool clicked = false;
            BlackWhiteSkin.Push();
            try
            {
                clicked = GUI.Button(rect, HotbarI18n.T("app.menu_button"), BlackWhiteSkin.OverlayBtnStyle);
                BlackWhiteSkin.DrawBorder(rect, BorderThickness);
            }
            finally
            {
                BlackWhiteSkin.Pop();
            }

            if (clicked)
            {
                BlackWhiteSkin.ClearTextFocus();
                onClick?.Invoke();
                GUIUtility.ExitGUI();
            }
        }
    }
}
