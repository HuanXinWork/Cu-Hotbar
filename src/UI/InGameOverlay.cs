using System;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>独立模式下暂停菜单打开时绘制的游戏内设置面板唤起按钮。</summary>
    internal sealed class InGameOverlay
    {
        // 与 HwAssistive / SkinSync / CuSaveManager 共享 320x84 规格与底部第 0 堆叠槽（24px），避免多模组并存时按钮交叠
        private const float ButtonWidth = 320f;
        private const float ButtonHeight = 84f;
        private const float MarginRight = 24f;
        private const float MarginBottom = 24f;
        private const float BorderThickness = 5f;

        internal static bool ShouldShow()
        {
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
