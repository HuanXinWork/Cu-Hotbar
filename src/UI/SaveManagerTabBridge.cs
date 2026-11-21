using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>反射对接 CuSaveManager 侧栏扩展点、统一缩放协议与宿主面板打开阻挡态。</summary>
    internal static class SaveManagerTabBridge
    {
        private static bool _registered;
        private static bool _hostStateResolved;
        private static Func<bool> _isHostBlocking;
        private static FieldInfo _pluginInstanceField;
        private static FieldInfo _windowField;
        private static PropertyInfo _windowOpenProp;

        private static bool _hostScaleResolved;
        private static PropertyInfo _hostScaleProp;
        private static Func<int> _hostScaleGetter;
        private static Action<int> _hostScaleSetter;

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

        internal static int GetEffectiveUiScalePercent(HotbarConfig cfg)
        {
            EnsureHostScaleResolved();
            if (_hostScaleProp != null)
            {
                try
                {
                    int hostVal = _hostScaleGetter != null
                        ? _hostScaleGetter()
                        : (int)_hostScaleProp.GetValue(null, null);
                    return Mathf.Clamp(hostVal, 75, 150);
                }
                catch { }
            }
            int localVal = cfg?.UiScalePercent != null ? cfg.UiScalePercent.Value : 100;
            return Mathf.Clamp(localVal, 75, 150);
        }

        internal static void SetEffectiveUiScalePercent(HotbarConfig cfg, int percent)
        {
            int clamped = Mathf.Clamp(percent, 75, 150);
            EnsureHostScaleResolved();
            if (_hostScaleProp != null && _hostScaleProp.CanWrite)
            {
                try
                {
                    if (_hostScaleSetter != null) _hostScaleSetter(clamped);
                    else _hostScaleProp.SetValue(null, clamped, null);
                    return;
                }
                catch (Exception ex)
                {
                    ModLog.Warning("Interop", $"写入宿主 UiScalePercent 失败：{ex.Message}");
                }
            }
            cfg?.SetUiScalePercentAndSave(clamped);
        }

        // 缓存静态属性强类型委托，避免 IMGUI 每帧查询缩放比例时触发反射装箱
        private static void EnsureHostScaleResolved()
        {
            if (_hostScaleResolved) return;
            _hostScaleResolved = true;

            try
            {
                var type = AccessTools.TypeByName("CasualtiesUnknown.SaveManager.ExternalTabRegistry");
                if (type == null) return;

                var prop = type.GetProperty("UiScalePercent", BindingFlags.Public | BindingFlags.Static);
                if (prop == null || prop.PropertyType != typeof(int) || !prop.CanRead) return;

                _hostScaleProp = prop;
                var getMethod = prop.GetGetMethod(nonPublic: false);
                if (getMethod != null)
                    _hostScaleGetter = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), getMethod);

                if (prop.CanWrite)
                {
                    var setMethod = prop.GetSetMethod(nonPublic: false);
                    if (setMethod != null)
                        _hostScaleSetter = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), setMethod);
                }
            }
            catch (Exception ex)
            {
                ModLog.Debug("Interop", $"解析宿主 UiScalePercent 失败：{ex.Message}");
            }
        }
    }
}
