using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>管理快捷栏布局、行为开关与按键的持久化配置。</summary>
    internal sealed class HotbarConfig
    {
        internal const int DefaultKeyedSlots = 9;
        internal const int MaxSlots = 64;

        private static readonly KeyboardShortcut Unbound = new KeyboardShortcut(KeyCode.None);

        private readonly ConfigFile _config;
        private readonly Dictionary<int, ConfigEntry<KeyboardShortcut>> _slotHotkeys
            = new Dictionary<int, ConfigEntry<KeyboardShortcut>>();

        internal ConfigEntry<int> SlotCount { get; }
        internal ConfigEntry<int> RowCount { get; }
        internal ConfigEntry<bool> Vertical { get; }
        internal ConfigEntry<bool> Visible { get; }
        internal ConfigEntry<float> AnchorX { get; }
        internal ConfigEntry<float> AnchorY { get; }
        internal ConfigEntry<float> OffsetX { get; }
        internal ConfigEntry<float> OffsetY { get; }
        internal ConfigEntry<float> Scale { get; }
        internal ConfigEntry<float> BackgroundAlpha { get; }

        internal ConfigEntry<KeyboardShortcut> UseItemHotkey { get; }
        internal ConfigEntry<KeyboardShortcut> ToggleSettingsHotkey { get; }
        internal ConfigEntry<bool> ShowLogInConsole { get; }
        internal ConfigEntry<bool> AcceptUpdateNotice { get; }
        internal ConfigEntry<bool> EnableScroll { get; }
        internal ConfigEntry<bool> AutoRefill { get; }
        internal ConfigEntry<bool> WarnOnDrop { get; }
        internal ConfigEntry<bool> SafeQuickUse { get; }
        internal ConfigEntry<bool> AutoReload { get; }
        internal ConfigEntry<bool> SameKeyPutAway { get; }
        internal ConfigEntry<string> PreferredLanguage { get; }

        internal HotbarConfig(ConfigFile config)
        {
            _config = config;
            SlotCount = config.Bind("Hotbar", "SlotCount", 9,
                new ConfigDescription("快捷栏槽位数量。", new AcceptableValueRange<int>(1, MaxSlots)));
            RowCount = config.Bind("Hotbar", "RowCount", 1,
                new ConfigDescription("快捷栏排数；横排为行数，竖排为列数。", new AcceptableValueRange<int>(1, MaxSlots)));
            Vertical = config.Bind("Hotbar", "Vertical", false,
                "排列方向：false=横排（默认靠下），true=竖排（默认靠右）。");
            Visible = config.Bind("Hotbar", "Visible", true, "是否显示快捷栏。");
            Scale = config.Bind("Hotbar", "Scale", 1.0f,
                new ConfigDescription("快捷栏整体缩放。", new AcceptableValueRange<float>(0.5f, 2.0f)));
            BackgroundAlpha = config.Bind("Hotbar", "BackgroundAlpha", 0.55f,
                new ConfigDescription("槽位背景与边框透明度。", new AcceptableValueRange<float>(0f, 1f)));

            AnchorX = config.Bind("Position", "AnchorX", 0.5f,
                new ConfigDescription("屏幕锚点 X（0 左 1 右）。", new AcceptableValueRange<float>(0f, 1f)));
            AnchorY = config.Bind("Position", "AnchorY", 0f,
                new ConfigDescription("屏幕锚点 Y（0 下 1 上）。", new AcceptableValueRange<float>(0f, 1f)));
            OffsetX = config.Bind("Position", "OffsetX", 0f, "相对锚点的像素偏移 X。");
            OffsetY = config.Bind("Position", "OffsetY", 90f, "相对锚点的像素偏移 Y。");

            UseItemHotkey = config.Bind("Hotkeys", "UseItemHotkey",
                new KeyboardShortcut(KeyCode.Mouse1, KeyCode.LeftShift),
                "使用当前物品的快捷键，默认 Shift+鼠标右键；未绑定则关闭。");
            ToggleSettingsHotkey = config.Bind("Hotkeys", "ToggleSettingsHotkey", Unbound,
                "打开或关闭独立设置面板。默认未绑定。");

            ShowLogInConsole = config.Bind("Misc", "ShowInConsole", false,
                "是否把模组日志同步输出到游戏内控制台。");
            AcceptUpdateNotice = config.Bind("Misc", "AcceptUpdateNotice", true,
                "是否在启动时检查 GitHub 新版本并在主菜单提示。");
            EnableScroll = config.Bind("Hotbar", "EnableScroll", true,
                "鼠标滚轮切换当前选中槽。");
            AutoRefill = config.Bind("Hotbar", "AutoRefill", true,
                "槽内物品丢出或用尽后，身上仍有同种物品时自动补充。");
            WarnOnDrop = config.Bind("Hotbar", "WarnOnDrop", true,
                "切换时物品因无空槽与背包空间而掉落时弹出提示。");
            SafeQuickUse = config.Bind("Hotbar", "SafeQuickUse", true,
                "安全消耗品（食物/饮品/口服药）选中时不切主手，按使用键直接服用；其余物品切主手使用。");
            AutoReload = config.Bind("Hotbar", "AutoReload", true,
                "主手持枪缺弹时自动从身上匹配弹药装填。");
            SameKeyPutAway = config.Bind("Hotbar", "SameKeyPutAway", true,
                "再次按同一槽位键时把该槽物品收起（优先归还原位，其次身上空位），无需单独留一个空槽用于收起。");
            PreferredLanguage = config.Bind("I18n", "PreferredLanguage", "auto",
                "界面语言：auto=跟随游戏；zh=强制中文；en=强制英文。");
        }

        // 仅校验三组标准修饰键并放行 WASD/Space 等常规键，规避 BepInEx KeyboardShortcut.IsDown 全键盘排斥导致移动中热键失灵
        internal static bool TriggeredThisFrame(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null) return false;
            var sc = entry.Value;
            if (sc.MainKey == KeyCode.None || !Input.GetKeyDown(sc.MainKey)) return false;

            bool allowShift = IsShiftKey(sc.MainKey);
            bool allowCtrl = IsCtrlKey(sc.MainKey);
            bool allowAlt = IsAltKey(sc.MainKey);

            foreach (var mod in sc.Modifiers)
            {
                if (mod == KeyCode.None) continue;
                if (mod != sc.MainKey && !Input.GetKey(mod)) return false;
                if (IsShiftKey(mod)) allowShift = true;
                else if (IsCtrlKey(mod)) allowCtrl = true;
                else if (IsAltKey(mod)) allowAlt = true;
            }

            if (!allowShift && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) return false;
            if (!allowCtrl && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return false;
            if (!allowAlt && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return false;
            return true;
        }

        internal static bool IsAnyModifierHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private static bool IsShiftKey(KeyCode key) => key == KeyCode.LeftShift || key == KeyCode.RightShift;
        private static bool IsCtrlKey(KeyCode key) => key == KeyCode.LeftControl || key == KeyCode.RightControl;
        private static bool IsAltKey(KeyCode key) => key == KeyCode.LeftAlt || key == KeyCode.RightAlt;

        /// <summary>按需绑定第 index（0 基）槽热键，前 9 槽默认小键盘 1-9。</summary>
        internal ConfigEntry<KeyboardShortcut> SlotHotkey(int index)
        {
            if (_slotHotkeys.TryGetValue(index, out var entry)) return entry;
            var def = index < DefaultKeyedSlots
                ? new KeyboardShortcut(KeyCode.Keypad1 + index)
                : Unbound;
            entry = _config.Bind("Hotkeys", "SlotHotkey" + (index + 1), def,
                $"切换到第 {index + 1} 个槽位的按键，支持组合键。");
            _slotHotkeys[index] = entry;
            return entry;
        }
    }
}
