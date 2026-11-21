using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>插件入口：装配运行时组件，并按 SaveManager 在场状态挂载设置面板。</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(SaveManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(TogetherGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.casualtiesUnknown.hotbar";
        internal const string PluginName = "CuHotbar";
        internal const string PluginVersion = BuildInfo.Version;

        private const string SaveManagerGuid = "com.casualtiesUnknown.saveManager";
        private const string TogetherGuid = "CasualtiesMP";

        private static ManualLogSource _log;

        private HotbarConfig _cfg;
        private HotbarStore _store;
        private HotbarModel _model;
        private HotbarView _view;
        private HotbarWindow _window;
        private InGameOverlay _overlay;
        private Action _openWindowFromOverlay;
        private UpdateChecker _updateChecker;
        private Harmony _harmony;
        private bool _standalone;
        private bool _quitting;

        private void Awake()
        {
            _log = Logger;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);

            string dataRoot = HotbarStore.ResolveDataRoot();
            ModLog.Init(_log, dataRoot);
            ModLog.Debug("Build", $"Debug 诊断模式已启用，独立日志目录={dataRoot}");

            _cfg = new HotbarConfig(Config);
            HotbarRuntime.Config = _cfg;
            ModLog.ShowInConsole = _cfg.ShowLogInConsole.Value;
            _cfg.ShowLogInConsole.SettingChanged += (_, __) => ModLog.ShowInConsole = _cfg.ShowLogInConsole.Value;
            UpdateChecker.Enabled = _cfg.AcceptUpdateNotice.Value;
            _cfg.AcceptUpdateNotice.SettingChanged += (_, __) =>
                UpdateChecker.NotifySettingChanged(_updateChecker, _cfg.AcceptUpdateNotice.Value);
            LogConfigDump(_cfg);

            _store = new HotbarStore();
            _model = new HotbarModel(_cfg, _store);
            _view = new HotbarView(_cfg, _model);

            var controller = gameObject.AddComponent<HotbarController>();
            controller.Init(_cfg, _model, _view);

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                LogPatchSummary();
            }
            catch (Exception ex)
            {
                ModLog.Error("Patches", $"Harmony 补丁装载失败：{ex}");
            }

            _updateChecker = gameObject.AddComponent<UpdateChecker>();

            _standalone = !Chainloader.PluginInfos.ContainsKey(SaveManagerGuid);
            ModLog.Debug("Interop", _standalone
                ? $"未检测到 CuSaveManager（{SaveManagerGuid}），启用独立模式"
                : "检测到 CuSaveManager，尝试注册侧栏分页");
            if (!_standalone)
            {
                if (SaveManagerTabBridge.Register(_cfg))
                {
                    ModLog.Info("Interop", "设置已合并到 CuSaveManager 面板分页");
                }
                else
                {
                    _standalone = true;
                    ModLog.Warning("Interop", "CuSaveManager 无兼容扩展点，回退独立模式");
                }
            }
            if (_standalone)
            {
                _window = new HotbarWindow(_cfg);
                _openWindowFromOverlay = () => _window.OpenIfClosed();
                _overlay = new InGameOverlay();
                MenuButtonInjector.Setup(() => _window.Toggle());
                SceneManager.sceneLoaded += OnSceneLoaded;
                ModLog.Info("UI", "独立模式已启用");
            }

            if (Chainloader.PluginInfos.ContainsKey(TogetherGuid))
            {
                _ = MultiplayerCompat.IsRunning;
            }

            Application.quitting += OnApplicationQuitting;
            ModLog.Info($"{PluginName} v{PluginVersion} ({ModLog.BuildFlavor}) ready");
        }

        private void Update()
        {
            if (_quitting) return;
            if (_standalone) UiBlocker.EnforceBlocked();
        }

        private void LateUpdate()
        {
            if (_quitting) return;
            bool textInput = _standalone && _window != null && _window.ExpectsTextInput;
            ImGuiImeRecovery.TickUpdate(textInput);

            if (_standalone && _window != null && _window.Open && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_window.IsCapturingHotkey) _window.CancelKeyCapture();
                else _window.Close();
                return;
            }

            if (textInput) return;

            if (_standalone && _window != null && HotbarConfig.TriggeredThisFrame(_cfg.ToggleSettingsHotkey))
            {
                _window.Toggle();
            }
        }

        private void OnGUI()
        {
            if (_quitting) return;
            bool textInput = _standalone && _window != null && _window.ExpectsTextInput;
            ImGuiImeRecovery.TickOnGui(textInput);
            if (_standalone && (_window == null || !_window.Open))
                _overlay?.Draw(_openWindowFromOverlay);
            _window?.Draw();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ModLog.Debug("Scene", $"场景加载：{scene.name} ({mode})");
            _window?.Close();
        }

        private void OnApplicationQuitting() => Cleanup();

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_quitting) return;
            _quitting = true;
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            try { Application.quitting -= OnApplicationQuitting; } catch { }
            try { _window?.Close(); } catch { }
            try { UiBlocker.Unblock(); } catch { }
            try { MenuButtonInjector.Dispose(); } catch { }
            try { _view?.Dispose(); } catch { }
            try { HotbarView.DisposeSharedAssets(); } catch { }
            try { BlackWhiteSkin.Dispose(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
            ModLog.Shutdown();
        }

        [Conditional("DEBUG")]
        private void LogConfigDump(HotbarConfig cfg)
        {
            ModLog.Debug("Config", $"文件 {Config.ConfigFilePath}");
            ModLog.Debug("Config", $"[Hotbar] SlotCount={cfg.SlotCount.Value} RowCount={cfg.RowCount.Value} "
                + $"Vertical={cfg.Vertical.Value} Visible={cfg.Visible.Value} Scale={cfg.Scale.Value:0.00} "
                + $"BackgroundAlpha={cfg.BackgroundAlpha.Value:0.00} EnableScroll={cfg.EnableScroll.Value} "
                + $"AutoRefill={cfg.AutoRefill.Value} WarnOnDrop={cfg.WarnOnDrop.Value} "
                + $"SafeQuickUse={cfg.SafeQuickUse.Value} AutoReload={cfg.AutoReload.Value} "
                + $"SameKeyPutAway={cfg.SameKeyPutAway.Value}");
            ModLog.Debug("Config", $"[Position] AnchorX={cfg.AnchorX.Value:0.00} AnchorY={cfg.AnchorY.Value:0.00} "
                + $"OffsetX={cfg.OffsetX.Value:0.0} OffsetY={cfg.OffsetY.Value:0.0}");
            ModLog.Debug("Config", $"[Hotkeys] UseItemHotkey={DescribeShortcut(cfg.UseItemHotkey.Value)} "
                + $"ToggleSettingsHotkey={DescribeShortcut(cfg.ToggleSettingsHotkey.Value)}");
            ModLog.Debug("Config", $"[Misc] ShowInConsole={cfg.ShowLogInConsole.Value} "
                + $"AcceptUpdateNotice={cfg.AcceptUpdateNotice.Value} PreferredLanguage={cfg.PreferredLanguage.Value}");
            LogSlotHotkeys(cfg);
        }

        [Conditional("DEBUG")]
        private static void LogSlotHotkeys(HotbarConfig cfg)
        {
            int count = Mathf.Clamp(cfg.SlotCount.Value, 1, HotbarConfig.MaxSlots);
            var parts = new List<string>();
            var byKey = new Dictionary<string, List<int>>();
            for (int i = 0; i < count; i++)
            {
                var sc = cfg.SlotHotkey(i).Value;
                string text = DescribeShortcut(sc);
                parts.Add($"{i + 1}={text}");
                if (sc.MainKey == KeyCode.None) continue;
                if (!byKey.TryGetValue(text, out var slots))
                {
                    slots = new List<int>();
                    byKey[text] = slots;
                }
                slots.Add(i + 1);
            }
            ModLog.Debug("Config", "槽位热键 " + string.Join(" ", parts));

            var conflicts = new List<string>();
            foreach (var pair in byKey)
            {
                if (pair.Value.Count > 1) conflicts.Add($"{pair.Key} -> 槽位 {string.Join("/", pair.Value)}");
            }
            ModLog.Debug("Config", conflicts.Count == 0
                ? "热键冲突检查：无重复绑定"
                : "热键冲突检查：重复绑定 " + string.Join("；", conflicts));
        }

        [Conditional("DEBUG")]
        private static void LogPatchSummary()
        {
            var names = new List<string>();
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                names.Add(type.Name);
            }
            ModLog.Debug("Patches", $"PatchAll 完成：{names.Count} 个补丁类（{string.Join("、", names)}）");
        }

        private static string DescribeShortcut(KeyboardShortcut sc)
        {
            return sc.MainKey == KeyCode.None ? "未绑定" : sc.ToString();
        }
    }
}
