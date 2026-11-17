namespace CasualtiesUnknown.Hotbar
{
    /// <summary>无状态物品搬移辅助：探测原位、归还原位、腾空主手与切入主手。</summary>
    internal static class ItemMover
    {
        // slots[2] 为角色嘴部槽，进食/呕吐会自动吐出且塞入灯泡会碎裂，常规搬移必须跳过
        private const int MouthSlotIndex = 2;

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

        internal static bool CanBeHeld(Body body, Item item)
        {
            if (body == null || item == null) return false;
            if (item.Stats != null && item.Stats.wearable && !item.Stats.wearableCanBeHeld) return false;
            return !body.HasWearable(item);
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

        /// <summary>记录物品当前所属的非主手身体槽或随身容器。</summary>
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

            if (TryStoreIntoContainer(body, origin.Cont, item))
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

        /// <summary>将可手持的随身物品切入主手（要求主手已腾空且具备抓握能力）。</summary>
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

            // PickUpItem 内部在校验通过后会自动从父容器 UnloadItem，不可提前卸出以免失败时掉地
            body.PickUpItem(item, hand, force: true);
            return body.GetItem(hand) == item;
        }

        private static bool CanMoveToBodySlot(Body body, Item item, int slotIndex, int excludeSlot)
        {
            if (slotIndex < 0 || slotIndex >= body.slots.Length || slotIndex == excludeSlot) return false;
            if (slotIndex == MouthSlotIndex || body.HoldingItem(slotIndex)) return false;
            if (!CanBeHeld(body, item)) return false;
            var slot = body.slots[slotIndex];
            if (slot == null || !slot.canPickUp) return false;
            if (slot.limb != null && !slot.limb.gameObject.activeInHierarchy) return false;
            if (item.Stats != null && item.Stats.onlyHoldInHands && !slot.isHand) return false;
            return true;
        }

        private static int FindEmptySlotFor(Body body, Item item, int excludeSlot)
        {
            for (int i = 0; i < body.slots.Length; i++)
            {
                if (CanMoveToBodySlot(body, item, i, excludeSlot)) return i;
            }
            return -1;
        }

        private static bool CanStoreInCarriedContainer(Body body, Container cont, Item item)
        {
            if (cont == null || item == null) return false;
            var contItem = cont.GetComponent<Item>();
            if (contItem == null || contItem == item) return false;
            // 仅允许放入随身容器且禁止非空容器嵌套，对齐 Container.LoadItem 约束防止 DropItem 后装入被拒
            if (!body.HoldingItem(contItem) && !body.HasWearable(contItem)) return false;
            if (contItem.TryGetParentContainer(out _)) return false;
            if (item.TryGetComponent<Container>(out var subCont) && subCont.GetHoldingWeight() > 0f) return false;
            return cont.CanHoldItem(item);
        }

        private static bool TryStoreIntoContainer(Body body, Container cont, Item item)
        {
            if (!CanStoreInCarriedContainer(body, cont, item)) return false;
            if (body.HoldingItem(item)) body.DropItem(item);
            item.transform.position = cont.transform.position;
            cont.LoadItem(item);
            return item.transform.parent == cont.transform;
        }

        private static Container FindStorableContainer(Body body, Item item)
        {
            foreach (var it in body.GetAllItemsThorough())
            {
                if (it == null || it == item) continue;
                if (it.TryGetComponent<Container>(out var cont) && CanStoreInCarriedContainer(body, cont, item))
                    return cont;
            }
            return null;
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
