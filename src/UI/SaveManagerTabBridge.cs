using System;
using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>反射对接 CuSaveManager 侧栏扩展点并同步查询宿主面板打开阻挡态。</summary>
    internal static class SaveManagerTabBridge
    {
        private static bool _registered;
        private static bool _hostStateResolved;
        private static Func<bool> _isHostBlocking;
        private static FieldInfo _pluginInstanceField;
        private static FieldInfo _windowField;
        private static PropertyInfo _windowOpenProp;

        internal static bool Register(HotbarConfig cfg)
        {
            try
            {
                var type = AccessTools.TypeByName("CasualtiesUnknown.SaveManager.ExternalTabRegistry");
                if (type == null)
                {
                    ModLog.Warning("Interop", "SaveManager 无 ExternalTabRegistry 扩展点，跳过注册");
                    return false;
                }

                var content = new HotbarSettingsContent(cfg);
                Action draw = content.Draw;
                Func<string> title = () => HotbarI18n.T("tab.title");
                Func<string> status = () => HotbarI18n.T("tab.provided_by");

                var fn3 = AccessTools.Method(type, "Register", new[] { typeof(Func<string>), typeof(Action), typeof(Func<string>) });
                if (fn3 != null)
                {
                    fn3.Invoke(null, new object[] { title, draw, status });
                    _registered = true;
                    ModLog.Debug("Interop", "ExternalTabRegistry.Register(3) 注册成功");
                    return true;
                }

                var fn2 = AccessTools.Method(type, "Register", new[] { typeof(Func<string>), typeof(Action) });
                if (fn2 == null)
                {
                    ModLog.Debug("Interop", "ExternalTabRegistry 无兼容 Register 重载");
                    return false;
                }
                fn2.Invoke(null, new object[] { title, draw });
                _registered = true;
                ModLog.Debug("Interop", "ExternalTabRegistry.Register(2) 注册成功");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warning("Interop", $"注册 SaveManager 侧栏失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>宿主 SaveManager 主面板打开期间返回 true，以屏蔽后台快捷栏热键与滚轮误触。</summary>
        internal static bool IsHostInputBlocked()
        {
            if (!_registered) return false;
            EnsureHostStateResolved();
            try
            {
                if (_isHostBlocking != null) return _isHostBlocking();
                if (_pluginInstanceField != null && _windowField != null && _windowOpenProp != null)
                {
                    var plugin = _pluginInstanceField.GetValue(null) as UnityEngine.Object;
                    var window = plugin != null ? _windowField.GetValue(plugin) : null;
                    if (window != null && _windowOpenProp.GetValue(window, null) is bool open)
                        return open;
                }
            }
            catch { }
            return false;
        }

        private static void EnsureHostStateResolved()
        {
            if (_hostStateResolved) return;
            _hostStateResolved = true;

            try
            {
                var blockerType = AccessTools.TypeByName("CasualtiesUnknown.SaveManager.UiBlocker");
                var isBlockingGetter = blockerType != null ? AccessTools.PropertyGetter(blockerType, "IsBlocking") : null;
                if (isBlockingGetter != null)
                {
                    _isHostBlocking = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isBlockingGetter);
                    ModLog.Debug("Interop", "已绑定 CuSaveManager.UiBlocker.IsBlocking 状态查询");
                    return;
                }

                var pluginType = AccessTools.TypeByName("CasualtiesUnknown.SaveManager.Plugin");
                if (pluginType != null)
                {
                    _pluginInstanceField = AccessTools.Field(pluginType, "_instance");
                    _windowField = AccessTools.Field(pluginType, "_window");
                    if (_windowField != null)
                        _windowOpenProp = AccessTools.Property(_windowField.FieldType, "Open");
                    ModLog.Debug("Interop", $"已回退绑定 SaveManagerWindow.Open={_windowOpenProp != null}");
                }
            }
            catch { }
        }
    }
}
