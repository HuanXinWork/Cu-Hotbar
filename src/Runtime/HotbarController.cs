using System.Diagnostics;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>
    /// 快捷栏运行时驱动：逐帧刷新视图与指向、处理数字键切换、按角色加载与保存。
    /// </summary>
    internal sealed class HotbarController : MonoBehaviour
    {
        private HotbarConfig _cfg;
        private HotbarModel _model;
        private HotbarView _view;
        private int _lastRunId;

        internal void Init(HotbarConfig cfg, HotbarModel model, HotbarView view)
        {
            _cfg = cfg;
            _model = model;
            _view = view;
            HotbarRuntime.Config = cfg;
            HotbarRuntime.Model = model;
            HotbarRuntime.View = view;
        }

        private void Update()
        {
            if (_cfg == null) return;

            SyncRun();
            _model.RefillActiveIfNeeded();
            _model.Refresh();
            _view.Tick();
            if (_cfg.AutoReload.Value) AutoReload();
        }

        private void AutoReload()
        {
            var cam = PlayerCamera.main;
            if (cam == null || cam.body == null || !cam.body.conscious) return;
            AmmoLoader.TryAutoReloadHandGun(cam.body);
        }

        private void LateUpdate()
        {
            if (_cfg == null) return;
            HandleKeys();
            HandleUseItem();
            HandleScroll();
        }

        private void HandleUseItem()
        {
            if (!_cfg.UseItemHotkey.Value.IsDown()) return;
            if (_view.TryGetSlotIndexAt(Input.mousePosition, out _)) return;
            int index = _view.Selected;
            Item item = _model.GetItem(index);
            _model.UseSelected(index);
            LogSlotOp("使用物品", index, item, "使用键");
        }

        private void HandleScroll()
        {
            if (!_cfg.EnableScroll.Value) return;
            if (_model.SlotCount <= 0) return;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            var cam = PlayerCamera.main;
            if (cam == null || cam.body == null || !cam.body.conscious) return;
            if (PauseMenuCompat.IsPauseMenuOpen()) return;

            int n = _model.SlotCount;
            int start = _view.Selected < 0 ? 0 : _view.Selected;
            int dir = scroll > 0f ? -1 : 1;
            int idx = ((start + dir) % n + n) % n;
            if (_cfg.SafeQuickUse.Value && _model.IsConsumableSlot(idx))
            {
                _view.Selected = idx;
                LogSlotOp("仅选中（消耗品不切主手）", idx, _model.GetItem(idx), "滚轮");
                return;
            }
            bool activated = _model.Activate(idx);
            if (activated) _view.Selected = idx;
            LogSlotOp(activated ? "切换到主手" : "切主手失败", idx, _model.GetItem(idx), "滚轮");
        }

        private void SyncRun()
        {
            int runId = HotbarStore.CurrentRunId();
            if (runId != _lastRunId)
            {
                ModLog.Debug($"[Slot] 角色切换 runId {_lastRunId} → {runId}，重载快捷栏绑定");
                _lastRunId = runId;
                _model.LoadForCurrentRun();
                _view.Selected = -1;
            }
        }

        private void HandleKeys()
        {
            for (int i = 0; i < _model.SlotCount; i++)
            {
                if (_cfg.SlotHotkey(i).Value.IsDown())
                {
                    var hover = HoverItemLocator.ItemUnderMouse();
                    if (hover != null)
                    {
                        _model.Link(i, hover);
                        LogSlotOp("鼠标悬停绑定", i, hover, "槽位热键");
                        return;
                    }
                    if (_cfg.SafeQuickUse.Value && _model.IsConsumableSlot(i))
                    {
                        _view.Selected = i;
                        LogSlotOp("仅选中（消耗品不切主手）", i, _model.GetItem(i), "槽位热键");
                        return;
                    }
                    bool activated = _model.Activate(i);
                    if (activated) _view.Selected = i;
                    LogSlotOp(activated ? "切换到主手" : "切主手失败", i, _model.GetItem(i), "槽位热键");
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            _view?.Dispose();
        }

        /// <summary>栏位操作按秒节流：连点/长按滚轮会每帧重复，不节流会冲掉真正的状态变化。</summary>
        [Conditional("DEBUG")]
        private void LogSlotOp(string action, int index, Item item, string source)
        {
            ModLog.DebugThrottled("slot",
                $"[Slot] {action} 槽位 #{index + 1}（索引 {index}）物品={HotbarRuntime.DescribeItem(item)} 来源={source}");
        }
    }
}
