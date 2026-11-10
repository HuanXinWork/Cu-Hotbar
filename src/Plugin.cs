using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>
    /// 快捷物品栏入口：装配存储、运行时模型、UGUI 视图、输入与设置面板。
    /// 检测到 SaveManager 时合并设置分页，否则注入独立菜单按钮与窗口。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(SaveManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.casualtiesUnknown.hotbar";
        internal const string PluginName = "CuHotbar";
        internal const string PluginVersion = "1.0.8";

        private const string SaveManagerGuid = "com.casualtiesUnknown.saveManager";

        private static ManualLogSource _log;

        private HotbarConfig _cfg;
        private HotbarStore _store;
        private HotbarModel _model;
        private HotbarView _view;
        private HotbarWindow _window;
        private InGameOverlay _overlay;
        private Harmony _harmony;
        private bool _standalone;

        private void Awake()
        {
            _log = Logger;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);
            ModLog.Init(_log);
            ModLog.Debug("[Build] Debug 构建，详细日志已启用");

            _cfg = new HotbarConfig(Config);
            LogConfigDump(_cfg);
            HotbarRuntime.Config = _cfg;
            ModLog.ShowInConsole = _cfg.ShowLogInConsole.Value;
            _cfg.ShowLogInConsole.SettingChanged += (_, __) => ModLog.ShowInConsole = _cfg.ShowLogInConsole.Value;
            UpdateChecker.Enabled = _cfg.AcceptUpdateNotice.Value;
            _cfg.AcceptUpdateNotice.SettingChanged += (_, __) => UpdateChecker.Enabled = _cfg.AcceptUpdateNotice.Value;

            _store = new HotbarStore();
            _model = new HotbarModel(_cfg, _store);
            _view = new HotbarView(_cfg, _model);

            var controller = gameObject.AddComponent<HotbarController>();
            controller.Init(_cfg, _model, _view);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            LogPatchSummary();
            gameObject.AddComponent<UpdateChecker>();

            _standalone = !Chainloader.PluginInfos.ContainsKey(SaveManagerGuid);
            ModLog.Debug(_standalone
                ? $"[Interop] 未检测到 CuSaveManager（{SaveManagerGuid}），走独立模式"
                : "[Interop] 检测到 CuSaveManager，尝试注册设置分页");
            if (!_standalone)
            {
                if (SaveManagerTabBridge.Register(_cfg))
                {
                    ModLog.Debug("[Interop] 设置分页注册成功");
                    ModLog.Info("检测到 SaveManager：设置已合并到其面板分页");
                }
                else
                {
                    _standalone = true;
                    ModLog.Debug("[Interop] 设置分页注册失败（无扩展点或反射异常），已回退独立模式");
                    ModLog.Info("SaveManager 无扩展点，回退独立模式");
                }
            }
            if (_standalone)
            {
                _window = new HotbarWindow(_cfg);
                _overlay = new InGameOverlay();
                MenuButtonInjector.Setup(() => _window.Toggle());
                ModLog.Info("独立模式：注入菜单按钮与设置窗口");
                ModLog.Debug("[Interop] InGameOverlay（暂停菜单浮层按钮）已创建，菜单按钮注入器已启用");
            }

            ModLog.Info($"{PluginName} v{PluginVersion} ready");
        }

        private void LateUpdate()
        {
            bool textInput = _standalone && _window != null && _window.ExpectsTextInput;
            ImGuiImeRecovery.TickUpdate(textInput);

            if (_standalone && HotbarConfig.TriggeredThisFrame(_cfg.ToggleSettingsHotkey))
            {
                _window.Toggle();
            }
            if (_standalone && _window != null && _window.Open && Input.GetKeyDown(KeyCode.Escape))
            {
                _window.Close();
            }
        }

        private void OnGUI()
        {
            bool textInput = _standalone && _window != null && _window.ExpectsTextInput;
            ImGuiImeRecovery.TickOnGui(textInput);
            if (_standalone) _overlay?.Draw(() => _window.OpenIfClosed());
            _window?.Draw();
        }

        private void OnDestroy()
        {
            try { MenuButtonInjector.Dispose(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
        }

        /// <summary>把配置实际生效值一次打完：排查“改了配置没生效”时先看这几条，而不是去猜绑定顺序。</summary>
        [Conditional("DEBUG")]
        private void LogConfigDump(HotbarConfig cfg)
        {
            ModLog.Debug($"[Config] 文件 {Config.ConfigFilePath}");
            ModLog.Debug($"[Config] [Hotbar] SlotCount={cfg.SlotCount.Value} RowCount={cfg.RowCount.Value} "
                + $"Vertical={cfg.Vertical.Value} Visible={cfg.Visible.Value} Scale={cfg.Scale.Value:0.00} "
                + $"BackgroundAlpha={cfg.BackgroundAlpha.Value:0.00} EnableScroll={cfg.EnableScroll.Value} "
                + $"AutoRefill={cfg.AutoRefill.Value} WarnOnDrop={cfg.WarnOnDrop.Value} "
                + $"SafeQuickUse={cfg.SafeQuickUse.Value} AutoReload={cfg.AutoReload.Value}");
            ModLog.Debug($"[Config] [Position] AnchorX={cfg.AnchorX.Value:0.00} AnchorY={cfg.AnchorY.Value:0.00} "
                + $"OffsetX={cfg.OffsetX.Value:0.0} OffsetY={cfg.OffsetY.Value:0.0}");
            ModLog.Debug($"[Config] [Hotkeys] UseItemHotkey={DescribeShortcut(cfg.UseItemHotkey.Value)} "
                + $"ToggleSettingsHotkey={DescribeShortcut(cfg.ToggleSettingsHotkey.Value)}");
            ModLog.Debug($"[Config] [Misc] ShowInConsole={cfg.ShowLogInConsole.Value} "
                + $"AcceptUpdateNotice={cfg.AcceptUpdateNotice.Value}");
            ModLog.Debug($"[Config] [I18n] PreferredLanguage={cfg.PreferredLanguage.Value}");
            LogSlotHotkeys(cfg);
        }

        /// <summary>槽位热键逐条列出并查重：撞键时只有一个槽位会吃到按键，必须能从日志看出冲突候选。</summary>
        [Conditional("DEBUG")]
        private static void LogSlotHotkeys(HotbarConfig cfg)
        {
            int count = Mathf.Max(cfg.SlotCount.Value, 1);
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
            ModLog.Debug("[Config] [Hotkeys] 槽位热键 " + string.Join(" ", parts));

            var conflicts = new List<string>();
            foreach (var pair in byKey)
            {
                if (pair.Value.Count > 1) conflicts.Add($"{pair.Key} → 槽位 {string.Join("/", pair.Value)}");
            }
            ModLog.Debug(conflicts.Count == 0
                ? "[Config] [Hotkeys] 冲突检查：无重复绑定"
                : "[Config] [Hotkeys] 冲突检查：重复绑定 " + string.Join("；", conflicts));
        }

        /// <summary>PatchAll 没有逐类 try，只能事后汇总：列出带 [HarmonyPatch] 的类，确认装载范围。</summary>
        [Conditional("DEBUG")]
        private static void LogPatchSummary()
        {
            var names = new List<string>();
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                names.Add(type.Name);
            }
            ModLog.Debug($"[Patches] PatchAll 汇总：{names.Count} 个补丁类（{string.Join("、", names)}）");
        }

        private static string DescribeShortcut(KeyboardShortcut sc)
        {
            return sc.MainKey == KeyCode.None ? "未绑定" : sc.ToString();
        }
    }
}
