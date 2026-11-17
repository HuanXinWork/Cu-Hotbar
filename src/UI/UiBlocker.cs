using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>独立设置窗口打开期间禁用下层 GraphicRaycaster 并阻断主菜单与角色输入穿透。</summary>
    internal static class UiBlocker
    {
        private const float RescanIntervalSeconds = 0.5f;

        private static readonly List<(GraphicRaycaster gr, bool wasEnabled)> _disabled
            = new List<(GraphicRaycaster, bool)>();
        private static readonly HashSet<GraphicRaycaster> _tracked
            = new HashSet<GraphicRaycaster>();

        private static bool _isBlocking;
        private static float _nextRescanAt;

        internal static bool IsBlocking => _isBlocking;

        internal static void Block()
        {
            try
            {
                if (!_isBlocking)
                {
                    _disabled.Clear();
                    _tracked.Clear();
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    _isBlocking = true;
                }

                ScanAndDisableRaycasters();
                _nextRescanAt = Time.unscaledTime + RescanIntervalSeconds;
                ModLog.Debug("UI", $"UiBlocker 已开启：接管 {_disabled.Count} 个 GraphicRaycaster");
            }
            catch (System.Exception ex)
            {
                ModLog.Warning("UI", $"UiBlocker.Block 失败：{ex.Message}");
            }
        }

        internal static void EnforceBlocked()
        {
            if (!_isBlocking) return;
            try
            {
                if (Time.unscaledTime >= _nextRescanAt)
                {
                    PruneDestroyed();
                    ScanAndDisableRaycasters();
                    _nextRescanAt = Time.unscaledTime + RescanIntervalSeconds;
                    return;
                }

                for (int i = 0; i < _disabled.Count; i++)
                {
                    var gr = _disabled[i].gr;
                    if (gr != null && gr.enabled) gr.enabled = false;
                }
            }
            catch { }
        }

        internal static void Unblock()
        {
            if (!_isBlocking) return;
            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                int restored = _disabled.Count;
                for (int i = 0; i < _disabled.Count; i++)
                {
                    var (gr, was) = _disabled[i];
                    if (gr != null) gr.enabled = was;
                }
                _disabled.Clear();
                _tracked.Clear();
                _isBlocking = false;
                ModLog.Debug("UI", $"UiBlocker 已解除：恢复 {restored} 个 GraphicRaycaster");
            }
            catch (System.Exception ex)
            {
                ModLog.Warning("UI", $"UiBlocker.Unblock 失败：{ex.Message}");
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_isBlocking) return;
            try
            {
                PruneDestroyed();
                ScanAndDisableRaycasters();
                _nextRescanAt = Time.unscaledTime + RescanIntervalSeconds;
            }
            catch { }
        }

        private static void ScanAndDisableRaycasters()
        {
            var all = Object.FindObjectsOfType<GraphicRaycaster>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                var gr = all[i];
                if (gr == null) continue;
                if (_tracked.Add(gr)) _disabled.Add((gr, gr.enabled));
                gr.enabled = false;
            }
        }

        private static void PruneDestroyed()
        {
            for (int i = _disabled.Count - 1; i >= 0; i--)
            {
                if (_disabled[i].gr == null) _disabled.RemoveAt(i);
            }
            _tracked.RemoveWhere(gr => gr == null);
        }

        internal static bool IsPointerOverInjectedButton()
        {
            RectTransform rt = MenuButtonInjector.InjectedRect;
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            try
            {
                Canvas canvas = rt.GetComponentInParent<Canvas>();
                if (canvas != null) canvas = canvas.rootCanvas;
                Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam);
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(AdaptiveButton), "overlayActive", MethodType.Getter)]
    internal static class AdaptiveButtonOverlayGuardPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (UiBlocker.IsBlocking || UiBlocker.IsPointerOverInjectedButton())
                __result = true;
        }
    }

    [HarmonyPatch]
    internal static class AdaptiveButtonClickedGuardPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(AdaptiveButton), "Clicked");

        private static bool Prefix()
        {
            return !UiBlocker.IsBlocking && !UiBlocker.IsPointerOverInjectedButton();
        }
    }

    [HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
    internal static class PlayerCameraInputGuardPatch
    {
        private static bool Prefix()
        {
            if (!UiBlocker.IsBlocking) return true;
            try
            {
                if ((ConsoleScript.instance == null || !ConsoleScript.instance.active)
                    && PlayerCamera.main != null && PlayerCamera.main.body != null && Camera.main != null)
                {
                    PlayerCamera.main.body.targetLookPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                }
            }
            catch { }
            return false;
        }
    }
}
