using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>独立模式下在主菜单注入半透明黑白风格设置入口按钮，并保持文案随语言设置同步。</summary>
    internal static class MenuButtonInjector
    {
        private const string InjectedName = "Hotbar_MenuButton";

        private static Action _onClick;
        private static GameObject _injectedButton;

        private static Texture2D _normalTex;
        private static Texture2D _hoverTex;
        private static Texture2D _activeTex;
        private static Sprite _normalSprite;
        private static Sprite _hoverSprite;
        private static Sprite _activeSprite;

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
            DestroySpriteAndTex(ref _normalSprite, ref _normalTex);
            DestroySpriteAndTex(ref _hoverSprite, ref _hoverTex);
            DestroySpriteAndTex(ref _activeSprite, ref _activeTex);

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
                    ApplyMonochromeButtonStyle(_injectedButton);
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
            ApplyMonochromeButtonStyle(clone);
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
            // 释放 EventSystem 当前选中对象，防止点击后按空格/回车重复触发按钮
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            try { _onClick?.Invoke(); }
            catch (Exception ex) { ModLog.Warning("UI", $"按钮点击异常：{ex.Message}"); }
        }

        private static void ApplyMonochromeButtonStyle(GameObject buttonGo)
        {
            EnsureSpritesBuilt();

            var img = buttonGo.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = _normalSprite;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.color = Color.white;
            }

            var btn = buttonGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = new SpriteState
                {
                    highlightedSprite = _hoverSprite,
                    pressedSprite = _activeSprite,
                    selectedSprite = _normalSprite,
                    disabledSprite = _normalSprite,
                };
            }
        }

        private static void EnsureSpritesBuilt()
        {
            if (_normalSprite != null) return;
            _normalSprite = BuildSlicedMonoSprite(0, 150, 255, 220, 2, out _normalTex);
            _hoverSprite = BuildSlicedMonoSprite(60, 190, 255, 255, 2, out _hoverTex);
            _activeSprite = BuildSlicedMonoSprite(90, 210, 255, 255, 2, out _activeTex);
        }

        private static Sprite BuildSlicedMonoSprite(byte fillLum, byte fillAlpha, byte edgeLum, byte edgeAlpha, int borderPx, out Texture2D tex)
        {
            int size = borderPx * 2 + 4;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var fill = new Color32(fillLum, fillLum, fillLum, fillAlpha);
            var edge = new Color32(edgeLum, edgeLum, edgeLum, edgeAlpha);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isEdge = x < borderPx || x >= size - borderPx || y < borderPx || y >= size - borderPx;
                    pixels[y * size + x] = isEdge ? edge : fill;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(borderPx, borderPx, borderPx, borderPx));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DestroySpriteAndTex(ref Sprite sprite, ref Texture2D tex)
        {
            if (sprite != null)
            {
                try { UnityEngine.Object.Destroy(sprite); } catch { }
                sprite = null;
            }
            if (tex != null)
            {
                try { UnityEngine.Object.Destroy(tex); } catch { }
                tex = null;
            }
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

            // 原版 loadButton 子文本节点为左侧图标预留了偏移，克隆后必须重置为居中铺满以防文字偏歪裁切
            if (primaryTextChild is RectTransform textRt)
            {
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.offsetMin = new Vector2(14f, 8f);
                textRt.offsetMax = new Vector2(-14f, -8f);
                textRt.localScale = Vector3.one;
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
                t.color = Color.white;
                t.fontStyle = FontStyle.Bold;
                t.fontSize = Mathf.Max(t.fontSize, 24);
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.resizeTextForBestFit = false;
                first = false;
            }
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.FullName == null || !type.FullName.StartsWith("TMPro.TextMesh", StringComparison.Ordinal)) continue;
                var prop = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite) continue;
                try
                {
                    prop.SetValue(mb, first ? newText : "", null);
                    SetMemberIfPresent(type, mb, "color", Color.white);
                    SetMemberIfPresent(type, mb, "enableWordWrapping", false);
                    first = false;
                }
                catch { }
            }
        }

        private static void SetMemberIfPresent(Type type, object instance, string propName, object value)
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(instance, value, null); } catch { }
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
