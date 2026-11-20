using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>检测暂停菜单、文本输入焦点与界面悬停，提供快捷栏统一输入门控。</summary>
    internal static class PauseMenuCompat
    {
        private static readonly FieldInfo BrightnessPanelField =
            typeof(PlayerCamera).GetField("brightnessPanel", BindingFlags.Public | BindingFlags.Instance);

        private static readonly Dictionary<Type, PropertyInfo> _inputFocusedProps
            = new Dictionary<Type, PropertyInfo>();
        private static readonly List<RaycastResult> _scrollRayResults = new List<RaycastResult>();

        internal static bool IsPauseMenuOpen()
        {
            if (PlayerCamera.main == null) return false;
            try
            {
                if (PauseHandler.paused) return true;
            }
            catch { }
            return IsPauseContainerOrFallbackActive();
        }

        /// <summary>当前是否处于禁止快捷栏交互的状态。</summary>
        internal static bool IsGameplayInputBlocked() => GetBlockReason() != null;

        /// <summary>返回当前阻断快捷栏输入的具体原因；未阻断返回 null。</summary>
        internal static string GetBlockReason()
        {
            if (UiBlocker.IsBlocking) return "独立设置面板打开";
            if (HotbarSettingsContent.AnyCapturing) return "正在录制快捷键";
            if (SaveManagerTabBridge.IsHostInputBlocked()) return "宿主SaveManager面板打开";
            if (IsPauseMenuOpen()) return "暂停菜单打开";
            if (SettingsMenu.instance != null) return "原版设置菜单打开";
            if (ConsoleScript.instance != null && ConsoleScript.instance.active) return "游戏控制台打开";
            if (MultiplayerCompat.IsChatFocused) return "联机聊天框输入中";
            if (MinigameBase.main != null && MinigameBase.main.currentMinigame != null) return "医疗小游戏进行中";
            if (LiquidTransfer.instance != null) return "液体转移界面打开";
            if (ScrollableText.textExists) return "剧情文本覆盖层显示中";

            var cam = PlayerCamera.main;
            if (cam == null || cam.body == null) return "角色未就绪";
            if (!cam.body.conscious) return "角色昏迷或死亡";
            if (cam.blockItemUsage) return "原版禁用物品使用";
            if (cam.itemCustomizer != null && cam.itemCustomizer.activeSelf) return "物品改名界面打开";
            if (IsAnyUiInputFieldFocused()) return "UGUI输入框聚焦";

            return null;
        }

        /// <summary>判断当前光标是否位于可滚动面板、容器格子或聊天框上，决定是否让行鼠标滚轮。</summary>
        internal static bool IsScrollBlockedByUi()
        {
            var cam = PlayerCamera.main;
            if (cam == null) return true;
            if (cam.craftingPanel != null && cam.craftingPanel.activeSelf) return true;
            if (cam.tradeMenu != null && cam.tradeMenu.activeSelf) return true;
            if (MultiplayerCompat.IsCursorOverChat) return true;

            var es = EventSystem.current;
            if (es == null) return false;

            int uiLayer = LayerMask.NameToLayer("UI");
            var ped = new PointerEventData(es) { position = Input.mousePosition };
            _scrollRayResults.Clear();
            es.RaycastAll(ped, _scrollRayResults);

            for (int i = 0; i < _scrollRayResults.Count; i++)
            {
                var go = _scrollRayResults[i].gameObject;
                if (go == null) continue;
                if (go.GetComponentInParent<HotbarSlotMarker>() != null) return false;
                if (uiLayer >= 0 && go.layer != uiLayer) continue;

                if (go.GetComponentInParent<ScrollRect>() != null) return true;
                if (go.CompareTag("ContainerBack")) return true;
                if (go.TryGetComponent<InvButton>(out var btn) && !btn.isBody && btn.Overlaps(_scrollRayResults))
                    return true;
            }
            return false;
        }

        private static bool IsAnyUiInputFieldFocused()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null || !selected.activeInHierarchy) return false;

            if (selected.TryGetComponent<InputField>(out var legacyInput) && legacyInput.isFocused)
                return true;

            // 通过反射兼容 TMP_InputField.isFocused，避免直接绑定 Unity.TextMeshPro 程序集引用
            var behaviours = selected.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name.IndexOf("InputField", StringComparison.Ordinal) < 0) continue;

                if (!_inputFocusedProps.TryGetValue(type, out var prop))
                {
                    prop = type.GetProperty("isFocused", BindingFlags.Public | BindingFlags.Instance);
                    _inputFocusedProps[type] = prop;
                }
                if (prop != null && prop.PropertyType == typeof(bool))
                {
                    try { if ((bool)prop.GetValue(mb, null)) return true; }
                    catch { }
                }
            }
            return false;
        }

        private static bool IsPauseContainerOrFallbackActive()
        {
            try
            {
                var main = PauseHandler.main;
                if (main != null)
                {
                    var pauseContainer = main.pauseContainer;
                    return pauseContainer != null && pauseContainer.activeSelf;
                }
            }
            catch
            {
            }

            if (BrightnessPanelField == null) return false;
            try
            {
                var panel = BrightnessPanelField.GetValue(PlayerCamera.main) as GameObject;
                return panel != null && panel.activeSelf;
            }
            catch
            {
                return false;
            }
        }
    }
}
