using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>接管快捷栏槽位的物品拖出起点与拖入绑定落点。</summary>
    [HarmonyPatch(typeof(PlayerCamera))]
    internal static class DragPatch
    {
        [HarmonyPatch("HandleStartDragging")]
        [HarmonyPrefix]
        private static bool StartPrefix(PlayerCamera __instance)
        {
            if (!HotbarRuntime.Ready || __instance != PlayerCamera.main) return true;
            if (UiBlocker.IsBlocking || SaveManagerTabBridge.IsHostInputBlocked()) return false;
            if (!HotbarRuntime.View.TryGetSlotIndexAt(Input.mousePosition, out int index)) return true;
            if (PauseMenuCompat.IsGameplayInputBlocked()) return false;

            if (__instance.body == null || !__instance.body.conscious) return false;
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
            if (!HotbarRuntime.Ready || __instance != PlayerCamera.main) return true;
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
