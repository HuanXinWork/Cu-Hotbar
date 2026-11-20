using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>维护快捷栏槽位绑定、随身物品快照、主手切换及自动补充状态。</summary>
    internal sealed class HotbarModel
    {
        private readonly HotbarConfig _cfg;
        private readonly HotbarStore _store;

        private readonly List<Item> _bodyItems = new List<Item>();
        private readonly HashSet<Item> _bodyItemSet = new HashSet<Item>();
        private readonly Dictionary<string, int> _bodyIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _snapshotFrame = -1;
        private Body _snapshotBody;

        private readonly Item[] _items = new Item[HotbarConfig.MaxSlots];
        private readonly string[] _ids = new string[HotbarConfig.MaxSlots];
        private int _slotCount;
        private int _runId;
        private Body _trackedBody;

        private int _activeIndex = -1;
        private Item _activeItem;
        private ItemMover.Origin _activeOrigin = ItemMover.Origin.None;

        internal HotbarModel(HotbarConfig cfg, HotbarStore store)
        {
            _cfg = cfg;
            _store = store;
            for (int i = 0; i < HotbarConfig.MaxSlots; i++)
            {
                _ids[i] = "";
            }
            Resize(cfg.SlotCount.Value);
            cfg.SlotCount.SettingChanged += (_, __) => Resize(cfg.SlotCount.Value);
        }

        internal int SlotCount => _slotCount;

        /// <summary>解析第 index 槽随身物品；已跟踪实例销毁或离身时按 AutoRefill 重绑同种或清除。</summary>
        internal Item GetItem(int index)
        {
            if (index < 0 || index >= _slotCount) return null;
            var body = CurrentBody();
            SyncTrackedBody(body);
            var cur = _items[index];

            // 用 ReferenceEquals 区分尚未解析的真 null 与曾绑定但已 Destroy 的 Unity fake-null
            if (!ReferenceEquals(cur, null) && body != null && (cur == null || !InBody(body, cur)))
            {
                cur = null;
                _items[index] = null;
                if (!_cfg.AutoRefill.Value)
                {
                    ModLog.Debug("Slot", $"槽位 #{index + 1}（索引 {index}）物品耗尽或离身且 AutoRefill 关闭，已清除绑定");
                    _ids[index] = "";
                    if (index == _activeIndex) ClearActive();
                    SaveForCurrentRun();
                    return null;
                }
            }

            if (cur == null && body != null && !string.IsNullOrEmpty(_ids[index]))
            {
                var found = FindCandidateInBody(body, _ids[index]);
                if (found != null)
                {
                    _items[index] = found;
                    cur = found;
                }
            }
            return cur;
        }

        internal string GetId(int index)
        {
            if (index < 0 || index >= _slotCount) return "";
            return _ids[index] ?? "";
        }

        internal int CountInBody(int index)
        {
            string id = GetId(index);
            if (string.IsNullOrEmpty(id)) return 0;
            var body = CurrentBody();
            if (body == null) return 0;
            EnsureBodySnapshot(body);
            return _bodyIdCounts.TryGetValue(id, out int count) ? count : 0;
        }

        /// <summary>绑定第 index 槽到指定物品（同种 ID 唯一占槽，自动移除旧槽重复绑定）。</summary>
        internal void Link(int index, Item item)
        {
            if (index < 0 || index >= _slotCount) return;

            string newId = item != null ? (item.id ?? "") : "";
            var body = CurrentBody();
            SyncTrackedBody(body);
            Item newTracked = item != null && body != null && InBody(body, item) && ItemMover.CanBeHeld(body, item)
                ? item
                : null;

            if (_ids[index] == newId && _items[index] == newTracked) return;

            if (item != null)
            {
                for (int i = 0; i < HotbarConfig.MaxSlots; i++)
                {
                    if (i == index) continue;
                    if (_items[i] == item || (!string.IsNullOrEmpty(newId) && _ids[i] == newId))
                    {
                        _items[i] = null;
                        _ids[i] = "";
                        if (_activeIndex == i) ClearActive();
                    }
                }
            }

            if (_activeIndex == index && _activeItem != newTracked)
                ClearActive();

            // 指向地面物品或正穿戴在身上的物品时仅记录 ID，待存在可手持实例时再绑定引用
            _items[index] = newTracked;
            _ids[index] = newId;
            ModLog.Flow("Slot", "绑定更新", $"槽位 #{index + 1} -> id='{newId}' 实例就绪={newTracked != null}");
            SaveForCurrentRun();
        }

        internal void Clear(int index)
        {
            if (index < 0 || index >= _slotCount) return;
            _items[index] = null;
            _ids[index] = "";
            if (_activeIndex == index) ClearActive();
            ModLog.Debug("Slot", $"已清空槽位 #{index + 1}（索引 {index}）");
            SaveForCurrentRun();
        }

        internal void ClearAll()
        {
            for (int i = 0; i < HotbarConfig.MaxSlots; i++)
            {
                _items[i] = null;
                _ids[i] = "";
            }
            ClearActive();
            ModLog.Flow("Slot", "清空全部槽位", $"runId={_runId} 当前槽数={_slotCount}");
            SaveForCurrentRun();
        }

        internal bool IsConsumableSlot(int index)
        {
            var body = CurrentBody();
            if (body == null) return false;
            SyncTrackedBody(body);
            Item target = Resolve(index, body);
            return target != null && ConsumableClassifier.IsConsumable(target);
        }

        /// <summary>触发使用键：优先直接服用选中的安全消耗品，否则使用主手物品。</summary>
        internal void UseSelected(int index)
        {
            var cam = PlayerCamera.main;
            if (cam != null && cam.blockItemUsage) return;

            var body = CurrentBody();
            if (body == null || !body.conscious) return;
            if (!body.allowUseItem)
            {
                if (cam != null) cam.UseFailUnhappiness();
                ModLog.Debug("Slot", "使用物品被拒绝：角色情绪低落");
                return;
            }

            bool fromConsumableSlot = false;
            Item target = body.GetItem(body.handSlot);
            if (_cfg.SafeQuickUse.Value && index >= 0)
            {
                Item slotItem = Resolve(index, body);
                if (slotItem != null && ConsumableClassifier.IsConsumable(slotItem))
                {
                    target = slotItem;
                    fromConsumableSlot = true;
                }
            }

            if (target == null || target.Stats == null || !target.Stats.usable) return;
            if (target.Stats.destroyAtZeroCondition && target.condition <= 0f) return;

            ModLog.Flow("Slot", fromConsumableSlot ? "服用消耗品" : "使用主手物品",
                $"目标={HotbarRuntime.DescribeItem(target)} 槽位={(index >= 0 ? $"#{index + 1}" : "无")}");
            body.UseItem(target);
            InvalidateSnapshot();
        }

        /// <summary>将第 index 槽物品切入主手；槽位无可用物品时仅收起当前主手快捷栏物品。</summary>
        internal bool Activate(int index)
        {
            if (index < 0 || index >= _slotCount) return false;
            var body = CurrentBody();
            if (body == null || !body.conscious) return false;
            SyncTrackedBody(body);

            Item target = ResolveHoldable(index, body);
            if (target == null)
            {
                if (!string.IsNullOrEmpty(GetId(index)) && Resolve(index, body) != null)
                {
                    ModLog.Debug("Slot", $"激活槽位 #{index + 1} 失败：物品处于穿戴态或不可手持");
                    return false;
                }

                bool inGestureEmpty = MultiplayerCompat.BeginMoveGesture();
                try
                {
                    RestorePrevious(body);
                }
                finally
                {
                    if (inGestureEmpty) MultiplayerCompat.EndMoveGesture(null);
                    InvalidateSnapshot();
                }
                return true;
            }

            if (_activeIndex == index && body.GetItem(body.handSlot) == target) return true;
            if (!ItemMover.CanHoldInHand(body))
            {
                ModLog.Debug("Slot", $"激活槽位 #{index + 1} 失败：当前主手无法抓握");
                return false;
            }

            bool inGesture = MultiplayerCompat.BeginMoveGesture();
            try
            {
                RestorePrevious(body);

                Item handItem = body.GetItem(body.handSlot);
                if (handItem != null && handItem != target) ItemMover.Stash(body, handItem);

                _activeOrigin = ItemMover.Probe(body, target);
                if (!ItemMover.GrabToHand(body, target))
                {
                    ClearActive();
                    return false;
                }
                _activeIndex = index;
                _activeItem = target;
                return true;
            }
            finally
            {
                if (inGesture) MultiplayerCompat.EndMoveGesture(target);
                InvalidateSnapshot();
            }
        }

        /// <summary>激活物品离身时按 AutoRefill 自动补同种，仍在身上但离手则解除跟踪。</summary>
        internal void RefillActiveIfNeeded()
        {
            if (_activeIndex < 0 || _activeIndex >= _slotCount)
            {
                ClearActive();
                return;
            }
            var body = CurrentBody();
            if (body == null || !body.conscious) return;
            SyncTrackedBody(body);
            if (_activeIndex < 0) return;

            if (_activeItem != null && InBody(body, _activeItem))
            {
                if (body.GetItem(body.handSlot) != _activeItem) ClearActive();
                return;
            }

            if (!_cfg.AutoRefill.Value || !ItemMover.CanHoldInHand(body))
            {
                ClearActive();
                return;
            }

            string id = _ids[_activeIndex];
            var same = !string.IsNullOrEmpty(id) ? FindHoldableInBody(body, id) : null;
            if (same != null)
            {
                bool inGesture = MultiplayerCompat.BeginMoveGesture();
                try
                {
                    Item residualHand = body.GetItem(body.handSlot);
                    if (residualHand != null && residualHand != same)
                        ItemMover.Stash(body, residualHand);

                    var origin = ItemMover.Probe(body, same);
                    if (ItemMover.GrabToHand(body, same))
                    {
                        _activeOrigin = origin;
                        _items[_activeIndex] = same;
                        _activeItem = same;
                        ModLog.Flow("Slot", "自动补货", $"槽位 #{_activeIndex + 1} 已补充同种物品={HotbarRuntime.DescribeItem(same)}");
                        return;
                    }
                }
                finally
                {
                    if (inGesture) MultiplayerCompat.EndMoveGesture(same);
                    InvalidateSnapshot();
                }
            }
            ClearActive();
        }

        private void RestorePrevious(Body body)
        {
            if (_activeIndex < 0 || _activeIndex >= _slotCount)
            {
                ClearActive();
                return;
            }
            Item prev = _activeItem;
            if (prev != null && body.GetItem(body.handSlot) == prev)
            {
                if (!ItemMover.Restore(body, prev, _activeOrigin))
                {
                    _items[_activeIndex] = null;
                    if (!_cfg.AutoRefill.Value)
                    {
                        ModLog.Debug("Slot", $"还原槽位 #{_activeIndex + 1} 失败且 AutoRefill 关闭，已清除绑定");
                        Clear(_activeIndex);
                    }
                }
            }
            ClearActive();
        }

        private void ClearActive()
        {
            _activeIndex = -1;
            _activeItem = null;
            _activeOrigin = ItemMover.Origin.None;
        }

        internal void Refresh()
        {
            var body = CurrentBody();
            SyncTrackedBody(body);
            if (body != null) EnsureBodySnapshot(body);
            for (int i = 0; i < _slotCount; i++)
            {
                GetItem(i);
            }
        }

        // 帧内快照仅在同一调度阶段有效；物品变动后或进入 LateUpdate 前须调用 InvalidateSnapshot
        internal void InvalidateSnapshot() => _snapshotFrame = -1;

        private bool InBody(Body body, Item item)
        {
            if (body == null || item == null) return false;
            EnsureBodySnapshot(body);
            return _bodyItemSet.Contains(item);
        }

        private void EnsureBodySnapshot(Body body)
        {
            int frame = Time.frameCount;
            if (_snapshotFrame == frame && _snapshotBody == body) return;

            _snapshotFrame = frame;
            _snapshotBody = body;
            _bodyItems.Clear();
            _bodyItemSet.Clear();
            _bodyIdCounts.Clear();

            var all = body.GetAllItemsThorough();
            for (int i = 0; i < all.Count; i++)
            {
                var it = all[i];
                if (it == null) continue;
                _bodyItems.Add(it);
                _bodyItemSet.Add(it);
                if (!string.IsNullOrEmpty(it.id))
                {
                    _bodyIdCounts.TryGetValue(it.id, out int n);
                    _bodyIdCounts[it.id] = n + 1;
                }
            }
        }

        private Item FindCandidateInBody(Body body, string id)
        {
            var holdable = FindHoldableInBody(body, id);
            if (holdable != null) return holdable;

            EnsureBodySnapshot(body);
            for (int i = 0; i < _bodyItems.Count; i++)
            {
                var it = _bodyItems[i];
                if (it != null && it.id == id) return it;
            }
            return null;
        }

        private Item FindHoldableInBody(Body body, string id)
        {
            EnsureBodySnapshot(body);
            for (int i = 0; i < _bodyItems.Count; i++)
            {
                var it = _bodyItems[i];
                if (it != null && it.id == id && ItemMover.CanBeHeld(body, it))
                    return it;
            }
            return null;
        }

        internal void LoadForCurrentRun()
        {
            _runId = HotbarStore.CurrentRunId();
            _trackedBody = CurrentBody();
            var ids = _store.Load(_runId);
            for (int i = 0; i < HotbarConfig.MaxSlots; i++)
            {
                _ids[i] = i < ids.Count ? (ids[i] ?? "") : "";
                _items[i] = null;
            }
            ClearActive();
            InvalidateSnapshot();
            LogLoadedLinks(_runId, ids);
        }

        [Conditional("DEBUG")]
        private void LogLoadedLinks(int runId, List<string> ids)
        {
            int filled = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrEmpty(ids[i])) filled++;
            }
            ModLog.Debug("Slot", $"载入 runId={runId}：文件 {ids.Count} 条，非空 {filled} 条，当前槽数 {_slotCount}");
        }

        internal void SaveForCurrentRun()
        {
            if (_runId == 0) _runId = HotbarStore.CurrentRunId();
            if (_runId == 0) return;

            int lastNonEmpty = -1;
            for (int i = HotbarConfig.MaxSlots - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(_ids[i]))
                {
                    lastNonEmpty = i;
                    break;
                }
            }

            // 保留高槽位历史非空绑定的同时裁掉多余空尾，防止临时缩减槽数再保存时抹除尾部绑定
            int saveLen = Mathf.Max(_slotCount, lastNonEmpty + 1);
            var list = new List<string>(saveLen);
            for (int i = 0; i < saveLen; i++)
            {
                list.Add(_ids[i] ?? "");
            }
            _store.Save(_runId, list);
        }

        private Item Resolve(int index, Body body)
        {
            if (index < 0 || index >= _slotCount || body == null) return null;
            var cached = _items[index];
            if (cached != null)
            {
                if (InBody(body, cached)) return cached;
                _items[index] = null;
            }
            if (string.IsNullOrEmpty(_ids[index])) return null;
            var found = FindCandidateInBody(body, _ids[index]);
            if (found != null) _items[index] = found;
            return found;
        }

        private Item ResolveHoldable(int index, Body body)
        {
            if (index < 0 || index >= _slotCount || body == null) return null;
            var cached = _items[index];
            if (cached != null && InBody(body, cached) && ItemMover.CanBeHeld(body, cached))
                return cached;

            string id = _ids[index];
            if (string.IsNullOrEmpty(id)) return null;
            var found = FindHoldableInBody(body, id);
            if (found != null) _items[index] = found;
            return found;
        }

        private void Resize(int count)
        {
            _slotCount = Mathf.Clamp(count, 1, HotbarConfig.MaxSlots);
            // 仅释放超出当前槽数的实例引用而不清空 _ids，确保缩减后再调大槽数可恢复原绑定
            for (int i = _slotCount; i < HotbarConfig.MaxSlots; i++)
            {
                _items[i] = null;
            }
            if (_activeIndex >= _slotCount) ClearActive();
        }

        // 同 runId 读档换躯体时重置已跟踪物品引用，防止 AutoRefill 关闭时将旧躯体销毁实例误判为耗尽清槽
        private void SyncTrackedBody(Body body)
        {
            if (body == _trackedBody) return;
            for (int i = 0; i < HotbarConfig.MaxSlots; i++)
            {
                _items[i] = null;
            }
            ClearActive();
            _trackedBody = body;
        }

        private static Body CurrentBody()
        {
            var cam = PlayerCamera.main;
            var body = cam != null ? cam.body : null;
            return body != null ? body : null;
        }
    }
}
