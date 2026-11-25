using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>无状态物品搬移辅助：探测原位、归还原位、腾空主手与切入主手。</summary>
    internal static class ItemMover
    {
        // slots[2] 为角色嘴部槽，进食/呕吐会自动吐出且塞入灯泡会碎裂，常规搬移必须跳过
        private const int MouthSlotIndex = 2;

        private static bool _woodHandChecked;
        private static Func<InventorySlot, bool> _isWoodHand;

        internal struct Origin
        {
            public int BodySlot;
            public Container Cont;
            public static Origin None => new Origin { BodySlot = -1, Cont = null };
        }

        internal static bool CanHoldInHand(Body body)
        {
            if (body == null || body.slots == null) return false;
            int hand = body.handSlot;
            if (hand < 0 || hand >= body.slots.Length) return false;
            var slot = body.slots[hand];
            if (slot == null || !slot.canPickUp) return false;
            return slot.limb == null || slot.limb.gameObject.activeInHierarchy;
        }

        // 按物品父节点直接判定是否正穿在目标躯体肢体上，避开第三方穿戴补丁对非穿戴物空槽位键抛异常及同 ID 误判
        internal static bool IsWornBy(Body body, Item item)
        {
            if (body == null || item == null || item.Stats == null || !item.Stats.wearable) return false;
            var parent = item.transform.parent;
            return parent != null
                && parent.TryGetComponent<Limb>(out var limb)
                && limb.body == body
                && !parent.TryGetComponent<InventorySlot>(out _);
        }

        internal static bool CanBeHeld(Body body, Item item)
        {
            if (body == null || item == null) return false;
            if (item.Stats != null && item.Stats.wearable && !item.Stats.wearableCanBeHeld) return false;
            return !IsWornBy(body, item);
        }

        // 兼容 Prosthetics 木质义肢手规则：木手仅允许持握容器与盛水容器，前置拒绝以免先收起原主手容器却切不出目标物
        internal static bool CanHandSlotAccept(Body body, Item item)
        {
            if (body == null || item == null) return false;
            int hand = body.handSlot;
            if (body.slots == null || (hand != 0 && hand != 1) || hand >= body.slots.Length) return true;
            var slot = body.slots[hand];
            if (slot == null) return true;

            if (!_woodHandChecked)
            {
                _woodHandChecked = true;
                try
                {
                    var rulesType = AccessTools.TypeByName("WoodHandRules");
                    var method = rulesType != null
                        ? AccessTools.Method(rulesType, "IsWoodHand", new[] { typeof(InventorySlot) })
                        : null;
                    if (method != null && method.IsStatic && method.ReturnType == typeof(bool))
                        _isWoodHand = (Func<InventorySlot, bool>)Delegate.CreateDelegate(typeof(Func<InventorySlot, bool>), method);
                }
                catch (Exception ex)
                {
                    ModLog.Warning("Interop", $"解析 WoodHandRules.IsWoodHand 失败：{ex.Message}");
                }
            }

            if (_isWoodHand == null) return true;
            bool isWood;
            try { isWood = _isWoodHand(slot); }
            catch { return true; }
            if (!isWood) return true;

            return item.TryGetComponent<Container>(out _) || item.TryGetComponent<WaterContainerItem>(out _);
        }

        internal static int BodySlotOf(Body body, Item item)
        {
            if (body == null || body.slots == null || item == null) return -1;
            for (int i = 0; i < body.slots.Length; i++)
            {
                if (body.GetItem(i) == item) return i;
            }
            return -1;
        }

        /// <summary>记录物品当前所属的非主手身体槽或父容器。</summary>
        internal static Origin Probe(Body body, Item item)
        {
            var o = Origin.None;
            if (body == null || item == null) return o;
            int slot = BodySlotOf(body, item);
            if (slot >= 0 && slot != body.handSlot)
            {
                o.BodySlot = slot;
                return o;
            }
            if (slot < 0) o.Cont = item.ParentContainer();
            return o;
        }

        /// <summary>尽量将主手物品归还原位或随身空闲处，失败落地返回 false。</summary>
        internal static bool Restore(Body body, Item item, Origin origin)
        {
            if (body == null || item == null) return false;
            int hand = body.handSlot;

            if (CanMoveToBodySlot(body, item, origin.BodySlot, hand))
            {
                body.SwapSlots(hand, origin.BodySlot);
                if (body.GetItem(origin.BodySlot) == item)
                {
                    ModLog.Flow("Move", "归还", $"{HotbarRuntime.DescribeItem(item)} -> 原身体槽 #{origin.BodySlot}");
                    return true;
                }
            }

            if (TryStoreIntoContainer(body, origin.Cont, item, allowOpenExternal: true))
            {
                ModLog.Flow("Move", "归还", $"{HotbarRuntime.DescribeItem(item)} -> 原容器 {origin.Cont.name}");
                return true;
            }

            int empty = FindEmptySlotFor(body, item, hand);
            if (empty >= 0)
            {
                body.SwapSlots(hand, empty);
                if (body.GetItem(empty) == item)
                {
                    ModLog.Flow("Move", "归还", $"{HotbarRuntime.DescribeItem(item)} -> 空闲身体槽 #{empty}");
                    return true;
                }
            }

            var fallbackCont = FindStorableContainer(body, item);
            if (TryStoreIntoContainer(body, fallbackCont, item))
            {
                ModLog.Flow("Move", "归还", $"{HotbarRuntime.DescribeItem(item)} -> 随身容器 {fallbackCont.name}");
                return true;
            }

            if (body.HoldingItem(item))
            {
                body.DropItem(item);
                ModLog.Flow("Move", "归还掉落", $"{HotbarRuntime.DescribeItem(item)} 无可用空位与容器，已掉地");
                WarnDropped(item);
            }
            return false;
        }

        /// <summary>将主手物品腾空至可用身体槽或随身容器，无处存放则丢弃。</summary>
        internal static void Stash(Body body, Item item)
        {
            if (body == null || item == null) return;
            int hand = body.handSlot;
            if (body.GetItem(hand) != item) return;

            int empty = FindEmptySlotFor(body, item, hand);
            if (empty >= 0)
            {
                body.SwapSlots(hand, empty);
                if (body.GetItem(empty) == item)
                {
                    ModLog.Flow("Move", "腾空主手", $"{HotbarRuntime.DescribeItem(item)} -> 身体槽 #{empty}");
                    return;
                }
            }

            var cont = FindStorableContainer(body, item);
            if (TryStoreIntoContainer(body, cont, item))
            {
                ModLog.Flow("Move", "腾空主手", $"{HotbarRuntime.DescribeItem(item)} -> 容器 {cont.name}");
                return;
            }

            if (body.HoldingItem(item))
            {
                body.DropItem(item);
                ModLog.Flow("Move", "腾空掉落", $"{HotbarRuntime.DescribeItem(item)} 无处收纳，已掉地");
                WarnDropped(item);
            }
        }

        /// <summary>腾空主手并将 target 切入主手；当主手提着母袋子且全身满槽时智能换槽收纳避免把袋子丢地。</summary>
        internal static bool StashAndGrab(Body body, Item handItem, Item target)
        {
            if (body == null || target == null) return false;
            if (!CanBeHeld(body, target) || !CanHoldInHand(body)) return false;
            if (handItem == null || handItem == target) return GrabToHand(body, target);

            int hand = body.handSlot;
            if (body.GetItem(hand) != handItem) return GrabToHand(body, target);

            int empty = FindEmptySlotFor(body, handItem, hand);
            if (empty >= 0)
            {
                body.SwapSlots(hand, empty);
                if (body.GetItem(empty) == handItem)
                {
                    ModLog.Flow("Move", "腾空主手", $"{HotbarRuntime.DescribeItem(handItem)} -> 身体槽 #{empty}");
                    return GrabToHand(body, target);
                }
            }

            var cont = FindStorableContainer(body, handItem);
            if (TryStoreIntoContainer(body, cont, handItem))
            {
                ModLog.Flow("Move", "腾空主手", $"{HotbarRuntime.DescribeItem(handItem)} -> 容器 {cont.name}");
                return GrabToHand(body, target);
            }

            handItem.TryGetComponent<Container>(out var handCont);
            if (TrySwapOccupantIntoOtherContainer(body, hand, handItem, handCont, target, out bool grabbed4a))
                return grabbed4a;

            if (handCont != null
                && IsInContainerHierarchy(handCont, target)
                && TrySwapHandContainerWithOccupant(body, hand, handItem, handCont, target, out bool grabbed4b))
            {
                return grabbed4b;
            }

            int targetSlot = BodySlotOf(body, target);
            if (targetSlot >= 0 && targetSlot != hand && CanSlotAccept(body, handItem, targetSlot, hand))
            {
                body.SwapSlots(hand, targetSlot);
                if (body.GetItem(hand) == target)
                {
                    ModLog.Flow("Move", "满槽对调",
                        $"{HotbarRuntime.DescribeItem(handItem)} <-> {HotbarRuntime.DescribeItem(target)}（槽 #{targetSlot}）");
                    return true;
                }
            }

            Stash(body, handItem);
            return GrabToHand(body, target);
        }

        /// <summary>将可手持的物品切入主手（要求主手已腾空且具备抓握能力）。</summary>
        internal static bool GrabToHand(Body body, Item item)
        {
            if (!CanBeHeld(body, item))
            {
                ModLog.Debug("Move", $"切入主手拒绝：{HotbarRuntime.DescribeItem(item)} 为不可手持或正穿戴装备");
                return false;
            }
            int hand = body.handSlot;
            if (body.GetItem(hand) == item) return true;
            if (!CanHoldInHand(body) || body.HoldingItem(hand))
            {
                ModLog.Debug("Move", $"切入主手拒绝：主手槽 #{hand} 不可抓握或仍被占用");
                return false;
            }

            int srcSlot = BodySlotOf(body, item);
            if (srcSlot >= 0)
            {
                body.SwapSlots(srcSlot, hand);
                return body.GetItem(hand) == item;
            }

            bool hadParentContainer = item.TryGetParentContainer(out var parentCont);
            if (hadParentContainer && NeedsPreUnloadForSafePickup(body, parentCont))
            {
                if (!CanHandSlotAccept(body, item)) return false;

                parentCont.UnloadItem(item);
                if (PlayerCamera.main != null) PlayerCamera.main.PlayBackpackSound();
                body.PickUpItem(item, hand, force: true);
                if (body.GetItem(hand) != item)
                {
                    item.transform.position = parentCont.transform.position;
                    if (parentCont.TryGetComponent<Item>(out _))
                        parentCont.LoadItem(item);
                    else
                        LoadIntoFixtureContainer(parentCont, item);
                    return false;
                }

                RefreshContainerUiIfOpen();
                return true;
            }

            // 随身普通穿戴容器交由 PickUpItem 内部校验通过后自动 UnloadItem
            body.PickUpItem(item, hand, force: true);
            bool grabbed = body.GetItem(hand) == item;
            if (grabbed && hadParentContainer)
                RefreshContainerUiIfOpen();
            return grabbed;
        }

        // 非手持且缺少穿戴槽位键的父容器（外部袋/箱、嵌套子袋、场景固定箱）须先卸出再拾取，避开原版容器拾取校验触发第三方穿戴补丁空键异常
        private static bool NeedsPreUnloadForSafePickup(Body body, Container parentCont)
        {
            if (parentCont == null) return false;
            var parentItem = parentCont.GetComponent<Item>();
            if (parentItem != null && body.HoldingItem(parentItem)) return false;
            return parentItem == null
                || parentItem.Stats == null
                || !parentItem.Stats.wearable
                || string.IsNullOrEmpty(parentItem.Stats.wearSlotId);
        }

        private static bool TrySwapOccupantIntoOtherContainer(
            Body body, int hand, Item handItem, Container handCont, Item target, out bool grabbed)
        {
            grabbed = false;
            if (body.slots == null) return false;
            for (int s = 0; s < body.slots.Length; s++)
            {
                if (!CanSlotAccept(body, handItem, s, hand)) continue;
                Item occupant = body.GetItem(s);
                if (occupant == null || occupant == target) continue;
                if (!CanSlotAccept(body, occupant, hand, s)) continue;

                var occCont = FindStorableContainer(body, occupant, handCont);
                if (occCont == null) continue;

                body.SwapSlots(hand, s);
                if (body.GetItem(s) == handItem && TryStoreIntoContainer(body, occCont, occupant))
                {
                    ModLog.Flow("Move", "换槽收纳",
                        $"{HotbarRuntime.DescribeItem(handItem)} -> 槽 #{s}，原槽 {HotbarRuntime.DescribeItem(occupant)} -> 容器 {occCont.name}");
                    grabbed = GrabToHand(body, target);
                    return true;
                }
                if (body.GetItem(hand) == occupant && body.GetItem(s) == handItem)
                    body.SwapSlots(hand, s);
            }
            return false;
        }

        private static bool TrySwapHandContainerWithOccupant(
            Body body, int hand, Item handItem, Container handCont, Item target, out bool grabbed)
        {
            grabbed = false;
            if (body.slots == null) return false;
            for (int s = 0; s < body.slots.Length; s++)
            {
                if (!CanSlotAccept(body, handItem, s, hand)) continue;
                Item occupant = body.GetItem(s);
                if (occupant == null || occupant == target) continue;
                if (!CanSlotAccept(body, occupant, hand, s)) continue;
                if (!CanHoldAfterRemovingTarget(handCont, handItem, occupant, target)) continue;

                body.SwapSlots(hand, s);
                if (body.GetItem(s) != handItem) continue;

                if (handCont.CanHoldItem(occupant) && TryStoreIntoContainer(body, handCont, occupant))
                {
                    ModLog.Flow("Move", "换槽收纳",
                        $"手提容器 {HotbarRuntime.DescribeItem(handItem)} -> 槽 #{s}，原槽 {HotbarRuntime.DescribeItem(occupant)} 已收入该容器");
                    grabbed = GrabToHand(body, target);
                    return true;
                }

                // 须先取出 target 腾出袋内承重余量后再装入 occupant
                body.DropItem(occupant);
                occupant.transform.position = handCont.transform.position;
                grabbed = GrabToHand(body, target);
                occupant.transform.position = handCont.transform.position;
                handCont.LoadItem(occupant);
                RefreshContainerUiIfOpen();
                if (occupant.transform.parent != handCont.transform)
                {
                    ModLog.Flow("Move", "换槽掉落", $"{HotbarRuntime.DescribeItem(occupant)} 未能装入容器 {handCont.name}，已掉地");
                    WarnDropped(occupant);
                }
                else
                {
                    ModLog.Flow("Move", "换槽收纳",
                        $"手提容器 {HotbarRuntime.DescribeItem(handItem)} -> 槽 #{s}，取出目标后已装入 {HotbarRuntime.DescribeItem(occupant)}");
                }
                return true;
            }
            return false;
        }

        private static bool IsInContainerHierarchy(Container rootCont, Item target)
        {
            if (rootCont == null || target == null) return false;
            var rootTf = rootCont.transform;
            for (var tf = target.transform.parent; tf != null; tf = tf.parent)
            {
                if (tf == rootTf) return true;
                if (!tf.TryGetComponent<Container>(out _)) return false;
            }
            return false;
        }

        private static bool CanHoldAfterRemovingTarget(Container handCont, Item handItem, Item occupant, Item target)
        {
            if (handCont == null || handItem == null || occupant == null || target == null || occupant == handItem)
                return false;
            if (handItem.TryGetParentContainer(out _)) return false;
            if (occupant.TryGetComponent<Container>(out var subCont) && subCont.GetHoldingWeight() > 0f)
                return false;
            if (occupant.totalWeight > handCont.maxWeightPerItem)
                return false;

            // Container.GetHoldingWeight 仅累加直接子物体重量；target 若在嵌套子袋内则取出不减轻顶层袋重
            float freedWeight = target.transform.parent == handCont.transform ? target.totalWeight : 0f;
            if (handCont.GetHoldingWeight() - freedWeight + occupant.totalWeight > handCont.maxWeight)
                return false;

            if (handCont.tagRestriction != null && handCont.tagRestriction.Length > 0)
            {
                var itemTags = occupant.Stats != null ? occupant.Stats.GetTags() : null;
                if (!HasAnyMatchingTag(handCont.tagRestriction, itemTags))
                    return false;
            }
            return true;
        }

        private static bool HasAnyMatchingTag(string[] restrictions, string[] itemTags)
        {
            if (itemTags == null || itemTags.Length == 0) return false;
            for (int i = 0; i < restrictions.Length; i++)
            {
                string req = restrictions[i];
                for (int j = 0; j < itemTags.Length; j++)
                {
                    if (string.Equals(req, itemTags[j], StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static bool CanSlotAccept(Body body, Item item, int slotIndex, int excludeSlot)
        {
            if (body == null || body.slots == null) return false;
            if (slotIndex < 0 || slotIndex >= body.slots.Length || slotIndex == excludeSlot) return false;
            if (slotIndex == MouthSlotIndex) return false;
            if (!CanBeHeld(body, item)) return false;
            var slot = body.slots[slotIndex];
            if (slot == null || !slot.canPickUp) return false;
            if (slot.limb != null && !slot.limb.gameObject.activeInHierarchy) return false;
            if (item.Stats != null && item.Stats.onlyHoldInHands && !slot.isHand) return false;
            return true;
        }

        private static bool CanMoveToBodySlot(Body body, Item item, int slotIndex, int excludeSlot)
        {
            return CanSlotAccept(body, item, slotIndex, excludeSlot) && !body.HoldingItem(slotIndex);
        }

        private static int FindEmptySlotFor(Body body, Item item, int excludeSlot)
        {
            if (body == null || body.slots == null) return -1;
            for (int i = 0; i < body.slots.Length; i++)
            {
                if (CanMoveToBodySlot(body, item, i, excludeSlot)) return i;
            }
            return -1;
        }

        // 常规收纳禁止塞入外部容器；仅 Restore 归还原父容器时允许放回正打开且距离 < 5m 的外部容器
        private static bool CanStoreInContainer(Body body, Container cont, Item item, bool allowOpenExternal)
        {
            if (body == null || cont == null || item == null) return false;
            var contItem = cont.GetComponent<Item>();
            if (contItem == item) return false;

            bool carried = contItem != null && (body.HoldingItem(contItem) || IsWornBy(body, contItem));
            if (!carried)
            {
                if (!allowOpenExternal) return false;
                if (!HotbarModel.TryGetOpenExternalContainer(body, null, out var openExt) || openExt != cont)
                    return false;
                if (Vector2.Distance(body.transform.position, cont.transform.position) >= 5f)
                    return false;
            }

            if (contItem != null && contItem.TryGetParentContainer(out _)) return false;
            if (item.TryGetComponent<Container>(out var subCont) && subCont.GetHoldingWeight() > 0f) return false;
            return cont.CanHoldItem(item);
        }

        private static bool TryStoreIntoContainer(Body body, Container cont, Item item, bool allowOpenExternal = false)
        {
            if (!CanStoreInContainer(body, cont, item, allowOpenExternal)) return false;
            if (body.HoldingItem(item)) body.DropItem(item);
            item.transform.position = cont.transform.position;
            if (cont.TryGetComponent<Item>(out _))
                cont.LoadItem(item);
            else
                LoadIntoFixtureContainer(cont, item);
            if (item.transform.parent != cont.transform) return false;
            RefreshContainerUiIfOpen();
            return true;
        }

        // 原版 Container.LoadItem 未判空 mItem，无 Item 组件的场景固定箱体须按同逻辑直接入箱以防 NRE
        private static void LoadIntoFixtureContainer(Container cont, Item item)
        {
            item.transform.SetParent(cont.transform);
            if (item.rb != null) item.rb.simulated = false;
            item.transform.position = cont.transform.position;
            if (item.TryGetComponent<SpriteRenderer>(out var itemSr))
            {
                itemSr.enabled = cont.itemsVisible;
                if (cont.itemsVisible && cont.TryGetComponent<SpriteRenderer>(out var contSr))
                {
                    itemSr.sortingOrder = contSr.sortingOrder;
                    item.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
                }
            }
            if (item.TryGetComponent<Wearable>(out var wear))
                wear.ClearSprites();
            Container.UpdateItemLight(item.gameObject, !cont.itemsVisible);
        }

        private static Container FindStorableContainer(Body body, Item item, Container exclude = null)
        {
            if (body == null || item == null) return null;
            if (body.slots != null)
            {
                for (int i = 0; i < body.slots.Length; i++)
                {
                    var slotItem = body.GetItem(i);
                    if (slotItem == null || slotItem == item) continue;
                    if (slotItem.TryGetComponent<Container>(out var cont)
                        && cont != exclude
                        && CanStoreInContainer(body, cont, item, allowOpenExternal: false))
                    {
                        return cont;
                    }
                }
            }
            var wearables = body.GetAllWearables();
            if (wearables != null)
            {
                for (int i = 0; i < wearables.Count; i++)
                {
                    var wear = wearables[i];
                    if (wear == null || wear == item) continue;
                    if (wear.TryGetComponent<Container>(out var cont)
                        && cont != exclude
                        && CanStoreInContainer(body, cont, item, allowOpenExternal: false))
                    {
                        return cont;
                    }
                }
            }
            return null;
        }

        private static void RefreshContainerUiIfOpen()
        {
            var cam = PlayerCamera.main;
            if (cam != null && cam.containerMenu != null && cam.containerMenu.activeSelf)
            {
                cam.RepopulateContainer();
            }
        }

        private static void WarnDropped(Item item)
        {
            var cfg = HotbarRuntime.Config;
            if (cfg != null && !cfg.WarnOnDrop.Value) return;
            var cam = PlayerCamera.main;
            if (cam == null) return;
            string name = item.Stats != null ? item.fullName : item.id;
            cam.DoAlert(HotbarI18n.F("alert.dropped", name), true);
        }
    }
}
