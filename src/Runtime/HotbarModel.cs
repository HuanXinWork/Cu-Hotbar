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
        private readonly Queue<Transform> _scanQueue = new Queue<Transform>();
        private readonly Queue<Transform> _extQueue = new Queue<Transform>();
        private int _snapshotFrame = -1;
        private Body _snapshotBody;

        private readonly Item[] _items = new Item[HotbarConfig.MaxSlots];
        private readonly string[] _ids = new string[HotbarConfig.MaxSlots];
        private readonly bool[] _trackedFromBody = new bool[HotbarConfig.MaxSlots];
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

        /// <summary>解析第 index 槽可用物品；随身实例耗尽或离身时按 AutoRefill 重绑同种或清除。</summary>
        internal Item GetItem(int index)
        {
            if (index < 0 || index >= _slotCount) return null;
            var body = CurrentBody();
            SyncTrackedBody(body);
            var cur = _items[index];

            // 用 ReferenceEquals 区分尚未解析的真 null 与曾绑定但已 Destroy 的 Unity fake-null
            if (!ReferenceEquals(cur, null) && body != null)
            {
                if (cur != null && InBody(body, cur))
                {
                    _trackedFromBody[index] = true;
                    return cur;
                }
                if (cur != null && IsInOpenExternalContainer(body, cur))
                {
                    _trackedFromBody[index] = false;
                    return cur;
                }

                bool wasFromBody = _trackedFromBody[index];
                cur = null;
                _items[index] = null;
                _trackedFromBody[index] = false;
                // 仅当随身跟踪的实例耗尽或离身时才在 AutoRefill=false 下清槽，避免关闭外部容器界面误清绑定
                if (wasFromBody && !_cfg.AutoRefill.Value)
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
                    _trackedFromBody[index] = true;
                    return found;
                }
                var extFound = FindInOpenExternalContainer(body, _ids[index], requireHoldable: false);
                if (extFound != null)
                {
                    _items[index] = extFound;
                    _trackedFromBody[index] = false;
                    return extFound;
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

            Item newTracked = null;
            bool fromBody = false;
            if (item != null && body != null && ItemMover.CanBeHeld(body, item))
            {
                if (InBody(body, item))
                {
                    newTracked = item;
                    fromBody = true;
                }
                else if (IsInOpenExternalContainer(body, item))
                {
                    newTracked = item;
                    fromBody = false;
                }
            }

            if (_ids[index] == newId && _items[index] == newTracked && _trackedFromBody[index] == fromBody) return;

            if (item != null)
            {
                for (int i = 0; i < HotbarConfig.MaxSlots; i++)
                {
                    if (i == index) continue;
                    if (_items[i] == item || (!string.IsNullOrEmpty(newId) && _ids[i] == newId))
                    {
                        _items[i] = null;
                        _ids[i] = "";
                        _trackedFromBody[i] = false;
                        if (_activeIndex == i) ClearActive();
                    }
                }
            }

            if (_activeIndex == index && _activeItem != newTracked)
                ClearActive();

            // 指向地面散落物品或正穿戴在身上的物品时仅记录 ID，待存在可手持实例时再绑定引用
            _items[index] = newTracked;
            _ids[index] = newId;
            _trackedFromBody[index] = fromBody;
            ModLog.Flow("Slot", "绑定更新", $"槽位 #{index + 1} -> id='{newId}' 实例就绪={newTracked != null}");
            SaveForCurrentRun();
        }

        /// <summary>当鼠标悬停在同种已绑定物品上时，优先将该具体实例设为当前槽位目标。</summary>
        internal void PreferHoveredInstance(int index, Item hover)
        {
            if (index < 0 || index >= _slotCount || hover == null) return;
            var body = CurrentBody();
            if (body == null) return;
            SyncTrackedBody(body);
            if (string.IsNullOrEmpty(hover.id) || _ids[index] != hover.id || !ItemMover.CanBeHeld(body, hover))
                return;

            if (InBody(body, hover))
            {
                _items[index] = hover;
                _trackedFromBody[index] = true;
            }
            else if (IsInOpenExternalContainer(body, hover))
            {
                _items[index] = hover;
                _trackedFromBody[index] = false;
            }
        }

        internal void Clear(int index)
        {
            if (index < 0 || index >= _slotCount) return;
            _items[index] = null;
            _ids[index] = "";
            _trackedFromBody[index] = false;
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
                _trackedFromBody[i] = false;
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

                var targetOrigin = ItemMover.Probe(body, target);
                Item handItem = body.GetItem(body.handSlot);
                bool ok = handItem != null && handItem != target
                    ? ItemMover.StashAndGrab(body, handItem, target)
                    : ItemMover.GrabToHand(body, target);
                if (!ok)
                {
                    ClearActive();
                    return false;
                }

                _activeOrigin = targetOrigin;
                _activeIndex = index;
                _activeItem = target;
                _items[index] = target;
                _trackedFromBody[index] = true;
                return true;
            }
            finally
            {
                if (inGesture) MultiplayerCompat.EndMoveGesture(target);
                InvalidateSnapshot();
            }
        }

        /// <summary>该槽物品仍由本模组留在主手时再次按键收起归还原位；返回是否真正执行了收起。</summary>
        internal bool TryPutAway(int index)
        {
            if (index < 0 || index >= _slotCount || _activeIndex != index) return false;
            var body = CurrentBody();
            if (body == null)
            {
                ClearActive();
                return false;
            }
            SyncTrackedBody(body);
            if (_activeIndex != index) return false;

            // 主手换成了同种另一把或已被原版挪用时不接管，交由 Activate 重新解析，避免收起错物品
            if (_activeItem == null || body.GetItem(body.handSlot) != _activeItem)
            {
                ClearActive();
                return false;
            }

            bool inGesture = MultiplayerCompat.BeginMoveGesture();
            try
            {
                RestorePrevious(body);
            }
            finally
            {
                if (inGesture) MultiplayerCompat.EndMoveGesture(null);
                InvalidateSnapshot();
            }
            return true;
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
                    var origin = ItemMover.Probe(body, same);
                    Item residualHand = body.GetItem(body.handSlot);
                    bool grabbed = residualHand != null && residualHand != same
                        ? ItemMover.StashAndGrab(body, residualHand, same)
                        : ItemMover.GrabToHand(body, same);
                    if (grabbed)
                    {
                        _activeOrigin = origin;
                        _items[_activeIndex] = same;
                        _trackedFromBody[_activeIndex] = true;
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
                    _trackedFromBody[_activeIndex] = false;
                    if (!_cfg.AutoRefill.Value)
                    {
                        ModLog.Debug("Slot", $"还原槽位 #{_activeIndex + 1} 失败且 AutoRefill 关闭，已清除绑定");
                        Clear(_activeIndex);
                    }
                }
                else
                {
                    InvalidateSnapshot();
                    _trackedFromBody[_activeIndex] = InBody(body, prev);
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

        // 替换仅扫一层的 Body.GetAllItemsThorough，通过 BFS + HashSet 防环完整覆盖多层嵌套容器
        private void EnsureBodySnapshot(Body body)
        {
            int frame = Time.frameCount;
            if (_snapshotFrame == frame && _snapshotBody == body) return;

            _snapshotFrame = frame;
            _snapshotBody = body;
            _bodyItems.Clear();
            _bodyItemSet.Clear();
            _bodyIdCounts.Clear();
            _scanQueue.Clear();

            if (body.slots != null)
            {
                for (int i = 0; i < body.slots.Length; i++)
                    AddCarriedRootAndEnqueue(body.GetItem(i));
            }

            var wearables = body.GetAllWearables();
            if (wearables != null)
            {
                for (int i = 0; i < wearables.Count; i++)
                    AddCarriedRootAndEnqueue(wearables[i]);
            }

            while (_scanQueue.Count > 0)
            {
                var contTf = _scanQueue.Dequeue();
                if (contTf == null) continue;
                int childCount = contTf.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var child = contTf.GetChild(i);
                    if (child != null && child.TryGetComponent<Item>(out var sub) && sub != null && _bodyItemSet.Add(sub))
                    {
                        RecordSnapshotItem(sub);
                        if (sub.TryGetComponent<Container>(out _))
                            _scanQueue.Enqueue(sub.transform);
                    }
                }
            }
        }

        private void AddCarriedRootAndEnqueue(Item root)
        {
            if (root == null || !_bodyItemSet.Add(root)) return;
            RecordSnapshotItem(root);
            if (root.TryGetComponent<Container>(out _))
                _scanQueue.Enqueue(root.transform);
        }

        private void RecordSnapshotItem(Item it)
        {
            _bodyItems.Add(it);
            if (!string.IsNullOrEmpty(it.id))
            {
                _bodyIdCounts.TryGetValue(it.id, out int n);
                _bodyIdCounts[it.id] = n + 1;
            }
        }

        /// <summary>获取玩家当前打开且处于合法拾取距离与视线内的非随身外部容器。</summary>
        internal static bool TryGetOpenExternalContainer(Body body, HashSet<Item> bodySet, out Container extCont)
        {
            extCont = null;
            var cam = PlayerCamera.main;
            if (body == null || cam == null || cam.containerMenu == null || !cam.containerMenu.activeSelf || cam.currentContainer == null)
                return false;

            var cont = cam.currentContainer;
            if (cont.TryGetComponent<Item>(out var contItem))
            {
                if ((bodySet == null || !bodySet.Contains(contItem))
                    && !body.HoldingItem(contItem)
                    && !body.HasWearable(contItem)
                    && body.DoPickupCheck(contItem, noAlerts: true))
                {
                    extCont = cont;
                    return true;
                }
                return false;
            }

            if (Vector2.Distance(cont.transform.position, body.transform.position) < 10f
                && !Physics2D.Linecast(body.transform.position, cont.transform.position, LayerMask.GetMask("Ground")))
            {
                extCont = cont;
                return true;
            }
            return false;
        }

        private Item FindInOpenExternalContainer(Body body, string id, bool requireHoldable)
        {
            if (body == null || string.IsNullOrEmpty(id)) return null;
            EnsureBodySnapshot(body);
            if (!TryGetOpenExternalContainer(body, _bodyItemSet, out var extCont)) return null;

            _extQueue.Clear();
            _extQueue.Enqueue(extCont.transform);
            Item fallback = null;
            while (_extQueue.Count > 0)
            {
                var tf = _extQueue.Dequeue();
                if (tf == null) continue;
                int childCount = tf.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var child = tf.GetChild(i);
                    if (child == null || !child.TryGetComponent<Item>(out var sub) || sub == null) continue;
                    if (sub.id == id)
                    {
                        if (ItemMover.CanBeHeld(body, sub))
                        {
                            _extQueue.Clear();
                            return sub;
                        }
                        if (!requireHoldable && fallback == null)
                            fallback = sub;
                    }
                    if (sub.TryGetComponent<Container>(out _))
                        _extQueue.Enqueue(sub.transform);
                }
            }
            return fallback;
        }

        private bool IsInOpenExternalContainer(Body body, Item target)
        {
            if (body == null || target == null) return false;
            EnsureBodySnapshot(body);
            if (!TryGetOpenExternalContainer(body, _bodyItemSet, out var extCont)) return false;

            _extQueue.Clear();
            _extQueue.Enqueue(extCont.transform);
            while (_extQueue.Count > 0)
            {
                var tf = _extQueue.Dequeue();
                if (tf == null) continue;
                int childCount = tf.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var child = tf.GetChild(i);
                    if (child == null || !child.TryGetComponent<Item>(out var sub) || sub == null) continue;
                    if (sub == target)
                    {
                        _extQueue.Clear();
                        return true;
                    }
                    if (sub.TryGetComponent<Container>(out _))
                        _extQueue.Enqueue(sub.transform);
                }
            }
            return false;
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
                _trackedFromBody[i] = false;
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
                if (!string.IsNullOrEmpty(ids[i])) filled++;
            ModLog.Debug("Slot", $"载入 runId={runId}：文件 {ids.Count} 条，非空 {filled} 条，当前槽数 {_slotCount}");
        }

        internal void SaveForCurrentRun()
        {
            if (_runId == 0) _runId = HotbarStore.CurrentRunId();
            if (_runId == 0) return;

            int lastNonEmpty = -1;
            for (int i = HotbarConfig.MaxSlots - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(_ids[i])) { lastNonEmpty = i; break; }
            }

            // 保留高槽位历史非空绑定的同时裁掉多余空尾，防止临时缩减槽数再保存时抹除尾部绑定
            int saveLen = Mathf.Max(_slotCount, lastNonEmpty + 1);
            var list = new List<string>(saveLen);
            for (int i = 0; i < saveLen; i++) list.Add(_ids[i] ?? "");
            _store.Save(_runId, list);
        }

        private Item Resolve(int index, Body body) => ResolveCore(index, body, requireHoldable: false);

        private Item ResolveHoldable(int index, Body body) => ResolveCore(index, body, requireHoldable: true);

        private Item ResolveCore(int index, Body body, bool requireHoldable)
        {
            if (index < 0 || index >= _slotCount || body == null) return null;
            var cached = _items[index];
            if (cached != null && (!requireHoldable || ItemMover.CanBeHeld(body, cached)))
            {
                if (InBody(body, cached)) { _trackedFromBody[index] = true; return cached; }
                if (IsInOpenExternalContainer(body, cached)) { _trackedFromBody[index] = false; return cached; }
                if (!requireHoldable) { _items[index] = null; _trackedFromBody[index] = false; }
            }

            string id = _ids[index];
            if (string.IsNullOrEmpty(id)) return null;
            var found = requireHoldable ? FindHoldableInBody(body, id) : FindCandidateInBody(body, id);
            if (found != null)
            {
                _items[index] = found;
                _trackedFromBody[index] = true;
                return found;
            }
            var extFound = FindInOpenExternalContainer(body, id, requireHoldable);
            if (extFound != null)
            {
                _items[index] = extFound;
                _trackedFromBody[index] = false;
            }
            return extFound;
        }

        private void Resize(int count)
        {
            _slotCount = Mathf.Clamp(count, 1, HotbarConfig.MaxSlots);
            // 仅释放超出当前槽数的实例引用而不清空 _ids，确保缩减后再调大槽数可恢复原绑定
            for (int i = _slotCount; i < HotbarConfig.MaxSlots; i++)
            {
                _items[i] = null;
                _trackedFromBody[i] = false;
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
                _trackedFromBody[i] = false;
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
