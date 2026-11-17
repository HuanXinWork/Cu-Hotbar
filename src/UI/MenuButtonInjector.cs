using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>独立模式下在主菜单克隆注入设置入口按钮，并保持文案随语言设置同步。</summary>
    internal static class MenuButtonInjector
    {
        private const string InjectedName = "Hotbar_MenuButton";

        private static Action _onClick;
        private static GameObject _injectedButton;

        internal static bool Active { get; private set; }
        internal static RectTransform InjectedRect { get; private set; }

        internal static void Setup(Action onClick)
        {
            _onClick = onClick;
            Active = true;
            ModLog.Debug("UI", "菜单按钮注入器已启用");
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (PreRunScript.instance != null) OnPreRunStarted(PreRunScript.instance);
        }

        internal static void Dispose()
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
            if (_injectedButton != null)
            {
                try { UnityEngine.Object.Destroy(_injectedButton); } catch { }
            }
            Active = false;
            _onClick = null;
            _injectedButton = null;
            InjectedRect = null;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "PreGen" || PreRunScript.instance == null) return;
            try { InjectOnce(PreRunScript.instance); }
            catch (Exception ex) { ModLog.Warning("UI", $"sceneLoaded 注入失败：{ex.Message}"); }
        }

        internal static void OnPreRunStarted(PreRunScript pre)
        {
            try { InjectOnce(pre); }
            catch (Exception ex) { ModLog.Warning("UI", $"菜单按钮注入失败：{ex.Message}"); }
        }

        private static void InjectOnce(PreRunScript pre)
        {
            if (_onClick == null || pre == null) return;
            if (pre.loadButton == null)
            {
                ModLog.Debug("UI", "PreRunScript.loadButton 为空，跳过主菜单按钮注入");
                return;
            }

            var parent = pre.transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                var existing = parent.GetChild(i);
                if (existing.name == InjectedName)
                {
                    _injectedButton = existing.gameObject;
                    InjectedRect = existing as RectTransform;
                    ReplaceText(_injectedButton, HotbarI18n.T("app.menu_button"));
                    return;
                }
            }

            var clone = UnityEngine.Object.Instantiate(pre.loadButton.gameObject, parent, false);
            clone.name = InjectedName;
            clone.SetActive(true);

            var btn = clone.GetComponent<Button>();
            if (btn != null)
            {
                int n = btn.onClick.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                {
                    btn.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                }
                btn.onClick.RemoveAllListeners();
                btn.interactable = true;
                btn.onClick.AddListener(InvokeOnClick);
            }

            DestroyLocalizers(clone);
            CleanupChildren(clone);
            ReplaceText(clone, HotbarI18n.T("app.menu_button"));
            clone.AddComponent<MenuButtonLabelSync>();

            var rt = clone.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(280f, 96f);
                rt.anchoredPosition = new Vector2(300f, -20f);
                rt.localScale = Vector3.one;
            }

            // 插入在主菜单全屏遮罩节点之前，避免遮挡开场警告、新局设置与制作人员页
            PlaceBeforeOverlayScreens(pre, parent, clone.transform);
            _injectedButton = clone;
            InjectedRect = rt;
            ModLog.Debug("UI", $"主菜单按钮注入成功（sibling={clone.transform.GetSiblingIndex()}）");
        }

        private static void InvokeOnClick()
        {
            try { _onClick?.Invoke(); }
            catch (Exception ex) { ModLog.Warning("UI", $"按钮点击异常：{ex.Message}"); }
        }

        private static void PlaceBeforeOverlayScreens(PreRunScript pre, Transform parent, Transform buttonTransform)
        {
            int minSibling = int.MaxValue;
            ConsiderSibling(pre.creditsScreen != null ? pre.creditsScreen.transform : null, parent, buttonTransform, ref minSibling);
            ConsiderSibling(pre.runSettingsScreen != null ? pre.runSettingsScreen.transform : null, parent, buttonTransform, ref minSibling);
            ConsiderSibling(pre.contentWarning != null ? pre.contentWarning.transform : null, parent, buttonTransform, ref minSibling);
            ConsiderSibling(pre.cover != null ? pre.cover.transform : null, parent, buttonTransform, ref minSibling);
            ConsiderSibling(pre.tutorialWarning != null ? pre.tutorialWarning.transform : null, parent, buttonTransform, ref minSibling);
            ConsiderSibling(parent.Find("Credits"), parent, buttonTransform, ref minSibling);

            if (minSibling < int.MaxValue) buttonTransform.SetSiblingIndex(minSibling);
            else buttonTransform.SetAsLastSibling();
        }

        private static void ConsiderSibling(Transform candidate, Transform parent, Transform self, ref int minSibling)
        {
            if (candidate == null || candidate == self || candidate.parent != parent) return;
            int idx = candidate.GetSiblingIndex();
            if (idx < minSibling) minSibling = idx;
        }

        private static void DestroyLocalizers(GameObject clone)
        {
            var components = clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            foreach (var c in components)
            {
                if (c == null) continue;
                var name = c.GetType().Name;
                if (name == null) continue;
                if (name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("LocaleText", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.Destroy(c);
                }
            }
        }

        private static void CleanupChildren(GameObject clone)
        {
            var rootTransform = clone.transform;
            Transform primaryTextChild = null;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                var child = rootTransform.GetChild(i);
                if (HasTextComponent(child))
                {
                    primaryTextChild = child;
                    break;
                }
            }

            for (int i = rootTransform.childCount - 1; i >= 0; i--)
            {
                var child = rootTransform.GetChild(i);
                if (child != primaryTextChild)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static bool HasTextComponent(Transform root)
        {
            if (root.GetComponentInChildren<Text>(true) != null) return true;
            var allMb = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < allMb.Length; i++)
            {
                var mb = allMb[i];
                var fn = mb != null ? mb.GetType().FullName : null;
                if (fn != null && fn.StartsWith("TMPro.TextMesh", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static void ReplaceText(GameObject clone, string newText)
        {
            bool first = true;
            foreach (var t in clone.GetComponentsInChildren<Text>(includeInactive: true))
            {
                if (t == null) continue;
                t.text = first ? newText : "";
                first = false;
            }
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.FullName == null || !type.FullName.StartsWith("TMPro.TextMesh", StringComparison.Ordinal)) continue;
                var prop = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite) continue;
                try { prop.SetValue(mb, first ? newText : "", null); first = false; }
                catch { }
            }
        }

        private sealed class MenuButtonLabelSync : MonoBehaviour
        {
            private string _appliedText;
            private float _nextCheckAt;

            private void OnEnable() => SyncNow();

            private void Update()
            {
                if (Time.unscaledTime < _nextCheckAt) return;
                _nextCheckAt = Time.unscaledTime + 0.5f;
                SyncNow();
            }

            private void SyncNow()
            {
                string expected = HotbarI18n.T("app.menu_button");
                if (expected == _appliedText) return;
                _appliedText = expected;
                ReplaceText(gameObject, expected);
            }
        }
    }

    [HarmonyPatch(typeof(PreRunScript), "Start")]
    internal static class HotbarPreRunStartPatch
    {
        private static void Postfix(PreRunScript __instance)
        {
            if (!MenuButtonInjector.Active) return;
            MenuButtonInjector.OnPreRunStarted(__instance);
        }
    }
}
