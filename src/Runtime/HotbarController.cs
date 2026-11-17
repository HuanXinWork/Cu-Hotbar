using System.Diagnostics;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>驱动快捷栏帧刷新与热键/滚轮输入分发。</summary>
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
            bool inputBlocked = PauseMenuCompat.IsGameplayInputBlocked();
            if (!inputBlocked) _model.RefillActiveIfNeeded();
            _model.Refresh();
            _view.Tick();
            if (_cfg.AutoReload.Value && !inputBlocked)
            {
                var cam = PlayerCamera.main;
                if (cam != null && cam.body != null)
                    AmmoLoader.TryAutoReloadHandGun(cam.body);
            }
        }

        private void LateUpdate()
        {
            if (_cfg == null) return;
            string blockReason = PauseMenuCompat.GetBlockReason();
            if (blockReason != null)
            {
                LogBlockedKeyIfPressed(blockReason);
                return;
            }

            _model.InvalidateSnapshot();
            HandleKeys();
            HandleUseItem();
            HandleScroll();
        }

        private void HandleUseItem()
        {
            if (!HotbarConfig.TriggeredThisFrame(_cfg.UseItemHotkey)) return;
            if (_view.TryGetSlotIndexAt(Input.mousePosition, out _)) return;
            int index = _view.Selected;
            Item item = _model.GetItem(index);
            _model.UseSelected(index);
            LogSlotOp("触发使用键", index, item, "使用键");
        }

        private void HandleScroll()
        {
            if (!_cfg.EnableScroll.Value || _model.SlotCount <= 0) return;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            if (PauseMenuCompat.IsScrollBlockedByUi()) return;

            int n = _model.SlotCount;
            int start = _view.Selected < 0 ? 0 : _view.Selected;
            int dir = scroll > 0f ? -1 : 1;
            int idx = ((start + dir) % n + n) % n;

            if (_cfg.SafeQuickUse.Value && _model.IsConsumableSlot(idx))
            {
                _view.Selected = idx;
                LogSlotOp("仅选中消耗品", idx, _model.GetItem(idx), "滚轮");
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
                ModLog.Debug("Slot", $"角色切换 runId {_lastRunId} -> {runId}，重载快捷栏绑定");
                _lastRunId = runId;
                _model.LoadForCurrentRun();
                _view.Selected = -1;
            }
        }

        private void HandleKeys()
        {
            for (int i = 0; i < _model.SlotCount; i++)
            {
                if (!HotbarConfig.TriggeredThisFrame(_cfg.SlotHotkey(i))) continue;

                var hover = HoverItemLocator.ItemUnderMouse();
                if (hover != null)
                {
                    _model.Link(i, hover);
                    LogSlotOp("悬停绑定", i, hover, "槽位热键");
                    return;
                }
                if (_cfg.SafeQuickUse.Value && _model.IsConsumableSlot(i))
                {
                    _view.Selected = i;
                    LogSlotOp("仅选中消耗品", i, _model.GetItem(i), "槽位热键");
                    return;
                }
                bool activated = _model.Activate(i);
                if (activated) _view.Selected = i;
                LogSlotOp(activated ? "切换到主手" : "切主手失败", i, _model.GetItem(i), "槽位热键");
                return;
            }
        }

        private void OnDestroy()
        {
            _view?.Dispose();
        }

        [Conditional("DEBUG")]
        private void LogSlotOp(string action, int index, Item item, string source)
        {
            ModLog.DebugThrottled("slot", "Slot",
                $"{action} 槽位 #{index + 1}（索引 {index}）物品={HotbarRuntime.DescribeItem(item)} 来源={source}");
        }

        [Conditional("DEBUG")]
        private void LogBlockedKeyIfPressed(string reason)
        {
            if (HotbarConfig.TriggeredThisFrame(_cfg.UseItemHotkey))
            {
                ModLog.DebugThrottled("gate", "Gate", $"使用键已被门控屏蔽：原因={reason}");
                return;
            }
            for (int i = 0; i < _model.SlotCount; i++)
            {
                if (HotbarConfig.TriggeredThisFrame(_cfg.SlotHotkey(i)))
                {
                    ModLog.DebugThrottled("gate", "Gate", $"槽位 #{i + 1} 热键已被门控屏蔽：原因={reason}");
                    return;
                }
            }
        }
    }
}
