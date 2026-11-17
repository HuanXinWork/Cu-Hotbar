using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>面板关闭后在 Layout 阶段释放 IMGUI 残留键盘焦点，防止输入法占用导致热键失效。</summary>
    internal static class ImGuiImeRecovery
    {
        private static bool _pendingClear;

        internal static void RequestClear() => _pendingClear = true;

        internal static void TickUpdate(bool textInputExpected)
        {
            if (textInputExpected) _pendingClear = false;
        }

        internal static void TickOnGui(bool textInputExpected)
        {
            if (textInputExpected || !_pendingClear) return;

            var ev = Event.current;
            if (ev == null || ev.type != EventType.Layout) return;

            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            _pendingClear = false;
        }
    }
}
