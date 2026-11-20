using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>按配置偏好、游戏 Locale 或系统语言提供中英文文案翻译。</summary>
    internal static class HotbarI18n
    {
        private static readonly string[] ChineseKeywords =
        {
            "中文", "汉化", "简中", "简体", "繁中", "繁体", "繁體", "chinese"
        };

        private static readonly Dictionary<string, string> _zh = new Dictionary<string, string>
        {
            ["app.name"] = "CuHotbar",
            ["app.menu_button"] = "CuHotbar",
            ["tab.title"] = "快捷栏设置",
            ["tab.provided_by"] = "由 CuHotbar 提供",
            ["sec.display"] = "显示",
            ["sw.visible"] = "显示快捷栏",
            ["lbl.slot_count"] = "槽位数量",
            ["lbl.row_count"] = "排数",
            ["lbl.col_count"] = "列数",
            ["lbl.direction"] = "排列方向",
            ["opt.horizontal"] = "横排",
            ["opt.vertical"] = "竖排",
            ["lbl.scale"] = "界面缩放",
            ["lbl.bg_alpha"] = "背景透明度",
            ["sec.keys"] = "切换按键",
            ["sec.behavior"] = "交互与辅助",
            ["fmt.slot_hotkey_clean"] = "切换到槽位 {0}",
            ["lbl.use_item_clean"] = "使用主手物品",
            ["btn.clear_all_slots"] = "清空全部槽位",
            ["sw.enable_scroll"] = "滚轮切换选中槽",
            ["sw.auto_refill"] = "同种物品用尽前自动补充",
            ["sw.warn_on_drop"] = "物品因背包满掉落时提示",
            ["sw.safe_quick_use"] = "快捷安全使用（消耗品选中不切主手）",
            ["sw.auto_reload"] = "自动装填（主手持枪缺弹时从背包补匹配弹药）",
            ["sec.position"] = "位置",
            ["lbl.anchor_x"] = "锚点 X",
            ["lbl.anchor_y"] = "锚点 Y",
            ["lbl.offset_x"] = "偏移 X",
            ["lbl.offset_y"] = "偏移 Y",
            ["btn.reset_pos"] = "恢复默认位置",
            ["sec.hotkeys"] = "快捷键",
            ["lbl.hotkey_panel_clean"] = "打开 / 关闭设置面板",
            ["btn.press_a_key"] = "请按下新按键…",
            ["btn.clear"] = "清除",
            ["btn.unbound"] = "未绑定",
            ["sec.misc"] = "其他",
            ["lbl.language_clean"] = "界面语言",
            ["opt.language_auto"] = "跟随游戏",
            ["opt.language_zh"] = "中文",
            ["opt.language_en"] = "English",
            ["sw.show_log_in_console"] = "在游戏控制台显示模组日志",
            ["sw.accept_update_notice"] = "接受新版本更新提示",
            ["update.available"] = "CuHotbar 有新版本：{0}（点击打开 release 页）",
            ["alert.dropped"] = "背包已满，{0} 已掉落",
            ["sw.on"] = "开",
            ["sw.off"] = "关",
            ["hint.usage"] = "拖物品到槽位或指向物品按对应键绑定，右键点击槽位解绑；按切换键切到主手，按使用键（默认 Shift+右键）使用。",
            ["tab.settings"] = "设置",
            ["tab.about"] = "关于",
            ["about.title"] = "CuHotbar",
            ["about.desc"] = "MC 风快捷物品栏：拖物品到槽位 / 数字键切换 / 自动补充 / 安全使用消耗品。",
            ["about.version"] = "版本：{0}",
            ["about.sec_links"] = "链接",
            ["about.link_repo"] = "GitHub 仓库",
            ["about.link_release"] = "最新版本",
            ["about.sec_credits"] = "开发人员",
            ["about.sec_deps"] = "依赖与致谢",
        };

        private static readonly Dictionary<string, string> _en = new Dictionary<string, string>
        {
            ["app.name"] = "CuHotbar",
            ["app.menu_button"] = "CuHotbar",
            ["tab.title"] = "Hotbar Settings",
            ["tab.provided_by"] = "Provided by CuHotbar",
            ["sec.display"] = "Display",
            ["sw.visible"] = "Show hotbar",
            ["lbl.slot_count"] = "Slot count",
            ["lbl.row_count"] = "Rows",
            ["lbl.col_count"] = "Columns",
            ["lbl.direction"] = "Layout direction",
            ["opt.horizontal"] = "Horizontal",
            ["opt.vertical"] = "Vertical",
            ["lbl.scale"] = "UI scale",
            ["lbl.bg_alpha"] = "Background alpha",
            ["sec.keys"] = "Switch keys",
            ["sec.behavior"] = "Behavior & Assist",
            ["fmt.slot_hotkey_clean"] = "Switch to slot {0}",
            ["lbl.use_item_clean"] = "Use held item",
            ["btn.clear_all_slots"] = "Clear all slots",
            ["sw.enable_scroll"] = "Scroll wheel switches slot",
            ["sw.auto_refill"] = "Auto-refill slot while stock remains",
            ["sw.warn_on_drop"] = "Warn when an item is dropped (inventory full)",
            ["sw.safe_quick_use"] = "Safe quick use (consumables select without switching)",
            ["sw.auto_reload"] = "Auto reload (load matching ammo from inventory when held firearm is empty)",
            ["sec.position"] = "Position",
            ["lbl.anchor_x"] = "Anchor X",
            ["lbl.anchor_y"] = "Anchor Y",
            ["lbl.offset_x"] = "Offset X",
            ["lbl.offset_y"] = "Offset Y",
            ["btn.reset_pos"] = "Reset position",
            ["sec.hotkeys"] = "Hotkeys",
            ["lbl.hotkey_panel_clean"] = "Toggle settings panel",
            ["btn.press_a_key"] = "Press a new key...",
            ["btn.clear"] = "Clear",
            ["btn.unbound"] = "Unbound",
            ["sec.misc"] = "Misc",
            ["lbl.language_clean"] = "UI language",
            ["opt.language_auto"] = "Follow game",
            ["opt.language_zh"] = "Chinese",
            ["opt.language_en"] = "English",
            ["sw.show_log_in_console"] = "Show mod logs in game console",
            ["sw.accept_update_notice"] = "Accept update notifications",
            ["update.available"] = "CuHotbar update available: {0} (click to open release page)",
            ["alert.dropped"] = "Inventory full, {0} was dropped",
            ["sw.on"] = "ON",
            ["sw.off"] = "OFF",
            ["hint.usage"] = "Drag an item onto a slot or point at an item and press its key to bind, right-click a slot to unbind; press its key to hold, press the use key (default Shift+RightClick) to use.",
            ["tab.settings"] = "Settings",
            ["tab.about"] = "About",
            ["about.title"] = "CuHotbar",
            ["about.desc"] = "MC-style hotbar: drag to slot / number-key switch / auto-refill / safe consumable use.",
            ["about.version"] = "Version: {0}",
            ["about.sec_links"] = "Links",
            ["about.link_repo"] = "GitHub repository",
            ["about.link_release"] = "Latest release",
            ["about.sec_credits"] = "Developers",
            ["about.sec_deps"] = "Dependencies & thanks",
        };

        private static int _cachedFrame = -1;
        private static string _cachedLangPref;
        private static Dictionary<string, string> _cachedDict;

        // 按帧号与偏好配置缓存字典引用，消除 IMGUI 单帧数十次 T() 重复解析 Locale 与富文本的开销
        private static Dictionary<string, string> Current
        {
            get
            {
                int frame = Time.frameCount;
                string pref = HotbarRuntime.Config?.PreferredLanguage?.Value;
                if (_cachedDict != null && frame == _cachedFrame && string.Equals(pref, _cachedLangPref, StringComparison.Ordinal))
                    return _cachedDict;

                _cachedFrame = frame;
                _cachedLangPref = pref;
                _cachedDict = UseChinese(pref) ? _zh : _en;
                return _cachedDict;
            }
        }

        internal static string NormalizeLanguageMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return "auto";
            mode = mode.Trim().ToLowerInvariant();
            return mode == "zh" || mode == "en" ? mode : "auto";
        }

        private static bool UseChinese(string preferredLanguage)
        {
            switch (NormalizeLanguageMode(preferredLanguage))
            {
                case "zh":
                    return true;
                case "en":
                    return false;
                default:
                    string lang = ReadCurrentLanguageName();
                    if (!string.IsNullOrEmpty(lang)) return IsChineseLocaleName(lang);
                    var sys = Application.systemLanguage;
                    return sys == SystemLanguage.Chinese
                           || sys == SystemLanguage.ChineseSimplified
                           || sys == SystemLanguage.ChineseTraditional;
            }
        }

        private static string ReadCurrentLanguageName()
        {
            string name = null;
            try { name = Locale.currentLangName; } catch { }
            if (string.IsNullOrEmpty(name))
            {
                try { name = PlayerPrefs.GetString("locale"); } catch { }
            }
            return name;
        }

        private static bool IsChineseLocaleName(string name)
        {
            string normalized = StripRichText(name).Trim();
            if (string.IsNullOrEmpty(normalized)) return false;

            // "WC" 为社区汉化包与旧版中文语言包使用的标识前缀
            if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("WC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (int i = 0; i < ChineseKeywords.Length; i++)
            {
                if (normalized.IndexOf(ChineseKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('<') < 0)
                return value ?? string.Empty;

            var sb = new StringBuilder(value.Length);
            bool inTag = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(ch);
            }
            return sb.ToString();
        }

        internal static string T(string key)
        {
            return Current.TryGetValue(key, out var v) ? v : key;
        }

        internal static string F(string key, params object[] args)
        {
            string fmt = T(key);
            try { return string.Format(fmt, args); }
            catch (Exception ex)
            {
                ModLog.Warning("I18n", $"文案格式化失败 key={key}：{ex.Message}");
                return fmt;
            }
        }
    }
}
