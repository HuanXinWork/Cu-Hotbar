using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>接管快捷栏槽位的物品拖出起点与拖入绑定落点。</summary>
    [HarmonyPatch(typeof(PlayerCamera))]
    internal static class DragPatch
    {
        private static int _dragStartSlot = -1;

        [HarmonyPatch("HandleStartDragging")]
        [HarmonyPrefix]
        private static bool StartPrefix(PlayerCamera __instance)
        {
            if (__instance != PlayerCamera.main) return true;
            _dragStartSlot = -1;
            if (!HotbarRuntime.Ready) return true;
            if (UiBlocker.IsBlocking || SaveManagerTabBridge.IsHostInputBlocked()) return false;
            if (!HotbarRuntime.View.TryGetSlotIndexAt(Input.mousePosition, out int index)) return true;
            if (PauseMenuCompat.IsGameplayInputBlocked()) return false;

            if (__instance.body == null || !__instance.body.conscious) return false;
            // Alt+右键用于解绑槽位，此处拦截起拖以防与解绑手势冲突
            if (HotbarConfig.IsAltOnlyHeld())
            {
                __instance.dragItem = null;
                return false;
            }
            __instance.dragItem = null;
            __instance.clickPos = Input.mousePosition;
            if (!__instance.body.allowUseItem)
            {
                __instance.UseFailUnhappiness();
                ModLog.Debug("UI", $"拖拽开始被拒绝：角色情绪低落（槽位 #{index + 1}）");
                return false;
            }

            var item = HotbarRuntime.Model.GetItem(index);
            if (item != null)
            {
                _dragStartSlot = index;
                ModLog.Flow("UI", "拖拽起点", $"从槽位 #{index + 1}（索引 {index}）拿起 {HotbarRuntime.DescribeItem(item)}");
                BeginDragFromSlot(__instance, item);
            }
            // 命中快捷栏槽位后一律拦截原版逻辑，防止空槽穿透抓取背后地面物品或展开环形菜单
            return false;
        }

        private static void BeginDragFromSlot(PlayerCamera cam, Item item)
        {
            cam.dragItem = item;
            var dragImage = cam.dragImage;
            if (dragImage != null)
            {
                var sr = item.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    dragImage.enabled = true;
                    dragImage.transform.localScale = Vector2.one * 1.75f;
                    dragImage.sprite = sr.sprite;
                    dragImage.rectTransform.sizeDelta = PlayerCamera.ImageSizeDelta(sr.sprite.texture, 5f, 128f);
                }
            }
            cam.PlayUISound(PlayerCamera.UISoundType.MiniClick);
        }

        // 优先级须低于 Together 默认 Prefix（先由其调用 MoveCommit.Push），返回 false 后由其 Postfix 配对 EndDrag
        [HarmonyPatch("HandleReleaseDragging")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool ReleasePrefix(PlayerCamera __instance)
        {
            if (__instance != PlayerCamera.main) return true;
            int startSlot = _dragStartSlot;
            _dragStartSlot = -1;
            if (!HotbarRuntime.Ready) return true;
            if (UiBlocker.IsBlocking || SaveManagerTabBridge.IsHostInputBlocked())
            {
                __instance.dragItem = null;
                if (__instance.dragImage != null) __instance.dragImage.enabled = false;
                return false;
            }

            var item = __instance.dragItem;
            if (item == null) return true;

            if (HotbarRuntime.View.TryGetSlotIndexAt(Input.mousePosition, out int index))
            {
                // 从同一槽位原地短按释放容器物品时交还原版打开容器面板，避免被重绑拦截吞掉打开动作
                if (index == startSlot
                    && Vector2.Distance(Input.mousePosition, __instance.clickPos) < 10f
                    && item.TryGetComponent<Container>(out _))
                {
                    return true;
                }

                HotbarRuntime.Model.Link(index, item);
                ModLog.DebugThrottled("slot", "UI",
                    $"拖拽绑定：{HotbarRuntime.DescribeItem(item)} -> 槽位 #{index + 1}（索引 {index}）");
                __instance.dragItem = null;
                if (__instance.dragImage != null) __instance.dragImage.enabled = false;
                __instance.PlayUISound(PlayerCamera.UISoundType.Click);
                return false;
            }

            ModLog.Debug("UI", $"拖拽释放：{HotbarRuntime.DescribeItem(item)} 未落在槽位上，交还原生逻辑");
            return true;
        }
    }
}
