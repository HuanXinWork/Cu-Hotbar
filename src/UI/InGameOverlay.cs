using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>独立模式下暂停菜单打开时绘制的游戏内设置面板唤起按钮。</summary>
    internal sealed class InGameOverlay
    {
        private const float ButtonWidth = 240f;
        private const float ButtonHeight = 64f;
        private const float MarginRight = 32f;
        private const float MarginBottom = 32f;

        internal static bool ShouldShow()
        {
            if (SettingsMenu.instance != null || UiBlocker.IsBlocking) return false;
            return PauseMenuCompat.IsPauseMenuOpen();
        }

        internal void Draw(System.Action onClick)
        {
            if (!ShouldShow()) return;

            float x = Screen.width - ButtonWidth - MarginRight;
            float y = Screen.height - ButtonHeight - MarginBottom;
            var rect = new Rect(x, y, ButtonWidth, ButtonHeight);

            BlackWhiteSkin.Push();
            try
            {
                if (GUI.Button(rect, HotbarI18n.T("app.menu_button"), BlackWhiteSkin.TabStyle))
                    onClick?.Invoke();
                BlackWhiteSkin.DrawBorder(rect, 2f);
            }
            finally
            {
                BlackWhiteSkin.Pop();
            }
        }
    }
}
