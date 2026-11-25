using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>定位光标指向的物品：支持背包格与 Alt 物品标签，未被 UI 遮挡时查询地面物品。</summary>
    internal static class HoverItemLocator
    {
        private static readonly List<RaycastResult> _uiCasts = new List<RaycastResult>();

        internal static Item ItemUnderMouse()
        {
            var fromUi = ItemFromUI(out bool blockedByUi);
            if (fromUi != null) return fromUi;
            return blockedByUi ? null : ItemFromWorld();
        }

        private static Item ItemFromUI(out bool blockedByUi)
        {
            blockedByUi = false;
            var es = EventSystem.current;
            if (es == null) return null;

            var cam = PlayerCamera.main;
            var mainCanvas = cam != null ? cam.mainCanvas : null;
            int uiLayer = LayerMask.NameToLayer("UI");
            var ped = new PointerEventData(es) { position = Input.mousePosition };
            _uiCasts.Clear();
            es.RaycastAll(ped, _uiCasts);

            for (int i = 0; i < _uiCasts.Count; i++)
            {
                var go = _uiCasts[i].gameObject;
                if (go == null || PauseMenuCompat.IsCameraTransientOverlay(cam, go.transform)) continue;

                // 悬停在快捷栏自身槽位上时交由切换逻辑处理，不穿透绑定底层物品
                if (go.GetComponentInParent<HotbarSlotMarker>() != null)
                {
                    blockedByUi = true;
                    return null;
                }

                // 屏蔽未使用 "UI" 层的第三方覆盖面板穿透拾取背后世界物品
                if (PauseMenuCompat.IsThirdPartyOverlayCanvas(go, mainCanvas))
                {
                    blockedByUi = true;
                    return null;
                }

                var label = go.GetComponentInParent<ItemLabel>();
                if (label != null && label.refItem != null)
                    return label.refItem;

                if (go.TryGetComponent<InvButton>(out var btn))
                {
                    if (!btn.Overlaps(_uiCasts)) continue;
                    blockedByUi = true;
                    return btn.TryGetItem(out var item) ? item : null;
                }

                if (uiLayer < 0 || go.layer == uiLayer)
                    blockedByUi = true;
            }
            return null;
        }

        private static Item ItemFromWorld()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return null;
            Vector2 world = mainCam.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(world, LayerMask.GetMask("Item"));
            return hit != null && hit.TryGetComponent<Item>(out var item) ? item : null;
        }
    }
}
