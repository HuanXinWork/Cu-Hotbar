using System;
using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>反射桥接 Casualties: Together 联机状态、聊天框交互与物品原子提交事务。</summary>
    internal static class MultiplayerCompat
    {
        private static bool _resolved;
        private static Func<bool> _isRunning;
        private static Func<bool> _isServer;
        private static FieldInfo _chatFocusedField;
        private static FieldInfo _chatMouseHoverField;
        private static Action _movePush;
        private static Action<Item> _moveEndDrag;

        internal static bool IsRunning
        {
            get
            {
                EnsureResolved();
                try { return _isRunning != null && _isRunning(); }
                catch { return false; }
            }
        }

        internal static bool IsClient
        {
            get
            {
                if (!IsRunning) return false;
                try { return _isServer == null || !_isServer(); }
                catch { return false; }
            }
        }

        internal static bool IsChatFocused
        {
            get
            {
                if (!IsRunning) return false;
                try { return _chatFocusedField?.GetValue(null) is bool focused && focused; }
                catch { return false; }
            }
        }

        internal static bool IsCursorOverChat
        {
            get
            {
                if (!IsRunning) return false;
                try { return _chatMouseHoverField?.GetValue(null) is bool hovered && hovered; }
                catch { return false; }
            }
        }

        /// <summary>开启联机物品搬移事务以避免中途 DropItem 误报丢地；返回 true 时须在 finally 中配对调用 EndMoveGesture。</summary>
        internal static bool BeginMoveGesture()
        {
            if (!IsRunning) return false;
            EnsureResolved();
            if (_movePush == null || _moveEndDrag == null) return false;
            try
            {
                _movePush();
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void EndMoveGesture(Item touchedItem)
        {
            try { _moveEndDrag?.Invoke(touchedItem); }
            catch { }
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                var netType = AccessTools.TypeByName("Together.Net");
                if (netType == null)
                {
                    ModLog.Debug("Interop", "未检测到 Together.Net，运行于单机模式");
                    return;
                }

                var isRunningGetter = AccessTools.PropertyGetter(netType, "IsRunning");
                if (isRunningGetter != null)
                    _isRunning = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isRunningGetter);

                var isServerGetter = AccessTools.PropertyGetter(netType, "IsServer");
                if (isServerGetter != null)
                    _isServer = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isServerGetter);

                var chatType = AccessTools.TypeByName("Together.Chat");
                if (chatType != null)
                {
                    _chatFocusedField = AccessTools.Field(chatType, "CHAT_textbox_input_focused");
                    _chatMouseHoverField = AccessTools.Field(chatType, "mouse_is_in_chat_area");
                }

                var commitType = AccessTools.TypeByName("Together.MoveCommit");
                if (commitType != null)
                {
                    var pushMethod = AccessTools.Method(commitType, "Push", Type.EmptyTypes);
                    var endDragMethod = AccessTools.Method(commitType, "EndDrag", new[] { typeof(Item) });
                    if (pushMethod != null && endDragMethod != null)
                    {
                        _movePush = (Action)Delegate.CreateDelegate(typeof(Action), pushMethod);
                        _moveEndDrag = (Action<Item>)Delegate.CreateDelegate(typeof(Action<Item>), endDragMethod);
                    }
                }

                ModLog.Info("Interop", "检测到 Casualties: Together，联机兼容桥已启用");
                ModLog.Debug("Interop", $"Together 绑定详情：Net={_isRunning != null && _isServer != null} "
                    + $"Chat={_chatFocusedField != null} MoveCommit={_movePush != null && _moveEndDrag != null}");
            }
            catch (Exception ex)
            {
                ModLog.Warning("Interop", $"解析联机模组接口失败：{ex.Message}");
            }
        }
    }
}
