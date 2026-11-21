using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>管理主 Canvas 上快捷栏 UGUI 的构建、层级排序、布局与状态同步。</summary>
    internal sealed class HotbarView
    {
        private const float SlotSize = 64f;
        private const float SlotGap = 6f;
        private const float IconMaxSize = SlotSize - 16f;
        private const float SelFrameThickness = 3f;
        private const float StatusBarInset = 4f;
        private const float StatusBarHeight = 4f;
        private const int SlotLabelFontSize = 15;

        private static readonly Color SlotBg = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color SlotBorder = new Color(1f, 1f, 1f, 0.40f);
        private static readonly Color StatusTrackColor = new Color32(0, 0, 0, 150);
        private static readonly Color SelFrameColor = Color.white;
        private static readonly Color KeyUnselectedColor = new Color(1f, 1f, 1f, 0.78f);
        private static readonly Color TextOutlineColor = new Color(0f, 0f, 0f, 0.88f);
        private static readonly Vector2 TextOutlineDistance = new Vector2(1f, -1f);

        private static Font _builtinFont;
        private static bool _fontResolved;
        private static Texture2D _borderTex;
        private static Sprite _borderSprite;

        private readonly HotbarConfig _cfg;
        private readonly HotbarModel _model;
        private readonly HashSet<int> _subscribedSlots = new HashSet<int>();
        private readonly EventHandler _onSlotHotkeyChanged;

        private Canvas _canvas;
        private RectTransform _root;
        private readonly List<Slot> _slots = new List<Slot>();
        private int _builtCount = -1;
        private int _selected = -1;
        private int _hotkeyDirtyVersion = 1;

        internal HotbarView(HotbarConfig cfg, HotbarModel model)
        {
            _cfg = cfg;
            _model = model;
            _onSlotHotkeyChanged = (_, __) => _hotkeyDirtyVersion++;
        }

        internal int Selected
        {
            get => _selected;
            set => _selected = value;
        }

        internal void Tick()
        {
            var cam = PlayerCamera.main;
            if (!ShouldShow(cam))
            {
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }

            if (_canvas != cam.mainCanvas || _root == null || _builtCount != _model.SlotCount)
            {
                Rebuild(cam, cam.mainCanvas);
            }
            else
            {
                PlaceBeforeCanvasOverlays(cam, _canvas.transform, _root);
            }

            if (_selected >= _slots.Count)
                _selected = _slots.Count - 1;

            if (!_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);
            Layout();
            UpdateGraphics();
        }

        private bool ShouldShow(PlayerCamera cam)
        {
            if (!_cfg.Visible.Value) return false;
            if (cam == null || cam.mainCanvas == null || cam.body == null) return false;
            if (PauseMenuCompat.IsPauseMenuOpen()) return false;
            if (SettingsMenu.instance != null) return false;
            if (cam.didDeathScreen || (cam.endScreen != null && cam.endScreen.gameObject.activeInHierarchy)) return false;
            if (WorldGeneration.world != null && (WorldGeneration.world.generatingWorld || WorldGeneration.world.cutsceneActive))
                return false;
            return true;
        }

        internal bool TryGetSlotIndexAt(Vector2 screenPos, out int index)
        {
            index = -1;
            if (UiBlocker.IsBlocking || SaveManagerTabBridge.IsHostInputBlocked()) return false;
            if (_root == null || !_root.gameObject.activeInHierarchy) return false;
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera
                : null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_slots[i].Rect, screenPos, cam))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        internal void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            _canvas = null;
            _slots.Clear();
            _builtCount = -1;
        }

        internal static void DisposeSharedAssets()
        {
            if (_borderSprite != null)
            {
                try { Object.Destroy(_borderSprite); } catch { }
                _borderSprite = null;
            }
            if (_borderTex != null)
            {
                try { Object.Destroy(_borderTex); } catch { }
                _borderTex = null;
            }
        }

        private void Rebuild(PlayerCamera cam, Canvas canvas)
        {
            Dispose();
            _canvas = canvas;
            _builtCount = _model.SlotCount;

            int uiLayer = LayerMask.NameToLayer("UI");
            var rootGo = new GameObject("Hotbar_Root", typeof(RectTransform));
            if (uiLayer >= 0) rootGo.layer = uiLayer;
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            PlaceBeforeCanvasOverlays(cam, canvas.transform, _root);

            for (int i = 0; i < _builtCount; i++)
            {
                _slots.Add(BuildSlot(i, uiLayer));
            }

            var font = ResolveBuiltinFont();
            ModLog.Debug("UI", $"重建快捷栏 UGUI：槽数={_builtCount} UI层={uiLayer} sibling={_root.GetSiblingIndex()} 字体={(font != null ? font.name : "null")}");
        }

        private static void PlaceBeforeCanvasOverlays(PlayerCamera cam, Transform canvasTransform, Transform rootTransform)
        {
            if (canvasTransform == null || rootTransform == null || rootTransform.parent != canvasTransform) return;

            int minSibling = int.MaxValue;
            if (cam != null)
            {
                ConsiderOverlaySibling(cam.dragImage != null ? cam.dragImage.transform : null, canvasTransform, rootTransform, ref minSibling);
                ConsiderOverlaySibling(cam.endScreen != null ? cam.endScreen.transform : null, canvasTransform, rootTransform, ref minSibling);
                ConsiderOverlaySibling(cam.consciousnessFade != null ? cam.consciousnessFade.transform : null, canvasTransform, rootTransform, ref minSibling);
            }
            var pauseMain = PauseHandler.main;
            if (pauseMain != null && pauseMain.pauseContainer != null)
            {
                ConsiderOverlaySibling(pauseMain.pauseContainer.transform, canvasTransform, rootTransform, ref minSibling);
            }

            // 仅当 _root 排在任一顶层遮罩之后时前移；若已在遮罩之前，重复 SetSiblingIndex(minSibling) 会反向越过该遮罩
            if (minSibling < int.MaxValue && rootTransform.GetSiblingIndex() > minSibling)
            {
                rootTransform.SetSiblingIndex(minSibling);
            }
        }

        private static void ConsiderOverlaySibling(Transform candidate, Transform canvasTransform, Transform self, ref int minSibling)
        {
            if (candidate == null) return;
            while (candidate.parent != null && candidate.parent != canvasTransform)
            {
                candidate = candidate.parent;
            }
            if (candidate.parent != canvasTransform || candidate == self) return;
            int idx = candidate.GetSiblingIndex();
            if (idx < minSibling) minSibling = idx;
        }

        private Slot BuildSlot(int index, int uiLayer)
        {
            // 槽位必须置于 "UI" 层，供原版 UIUtil.IsPointerOverUIElement 识别以阻止左键点击穿透触发攻击
            var slotGo = new GameObject("Hotbar_Slot" + index, typeof(RectTransform), typeof(Image));
            if (uiLayer >= 0) slotGo.layer = uiLayer;
            var marker = slotGo.AddComponent<HotbarSlotMarker>();
            marker.Index = index;
            var rect = slotGo.GetComponent<RectTransform>();
            rect.SetParent(_root, false);
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);

            var bg = slotGo.GetComponent<Image>();
            bg.color = SlotBg;
            bg.raycastTarget = true;

            var border = NewImage("Border", rect, SlotBorder, uiLayer);
            StretchFull(border.rectTransform);
            border.sprite = EnsureBorderSprite();
            border.type = Image.Type.Sliced;
            border.fillCenter = true;

            // 图标上移 2px 并收窄至 SlotSize - 16f，避开底部 4px 状态条使视觉重心居中
            var icon = NewImage("Icon", rect, Color.white, uiLayer);
            icon.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            icon.preserveAspect = true;
            icon.enabled = false;

            float statusFullWidth = SlotSize - StatusBarInset * 2f;
            var statusTrack = NewImage("StatusTrack", rect, StatusTrackColor, uiLayer);
            var stRect = statusTrack.rectTransform;
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.zero;
            stRect.pivot = Vector2.zero;
            stRect.anchoredPosition = new Vector2(StatusBarInset, StatusBarInset);
            stRect.sizeDelta = new Vector2(statusFullWidth, StatusBarHeight);
            statusTrack.enabled = false;

            var statusBar = NewImage("Status", rect, Color.white, uiLayer);
            var sbRect = statusBar.rectTransform;
            sbRect.anchorMin = Vector2.zero;
            sbRect.anchorMax = Vector2.zero;
            sbRect.pivot = Vector2.zero;
            sbRect.anchoredPosition = new Vector2(StatusBarInset, StatusBarInset);
            sbRect.sizeDelta = new Vector2(statusFullWidth, StatusBarHeight);
            statusBar.enabled = false;

            var selFrame = BuildSelFrame(rect, uiLayer);

            var count = NewText("Count", rect, SlotLabelFontSize, TextAnchor.LowerRight, uiLayer);
            var cRect = count.rectTransform;
            cRect.anchorMin = new Vector2(1f, 0f);
            cRect.anchorMax = new Vector2(1f, 0f);
            cRect.pivot = new Vector2(1f, 0f);
            cRect.anchoredPosition = new Vector2(-5f, 9f);
            cRect.sizeDelta = new Vector2(SlotSize - 10f, 20f);
            count.text = "";

            var hotkeyEntry = _cfg.SlotHotkey(index);
            if (_subscribedSlots.Add(index))
                hotkeyEntry.SettingChanged += _onSlotHotkeyChanged;

            var key = NewText("Key", rect, SlotLabelFontSize, TextAnchor.UpperLeft, uiLayer);
            var kRect = key.rectTransform;
            kRect.anchorMin = new Vector2(0f, 1f);
            kRect.anchorMax = new Vector2(0f, 1f);
            kRect.pivot = new Vector2(0f, 1f);
            kRect.anchoredPosition = new Vector2(5f, -4f);
            kRect.sizeDelta = new Vector2(SlotSize - 10f, 20f);
            key.color = KeyUnselectedColor;
            key.text = FormatKeyLabel(hotkeyEntry.Value);

            return new Slot
            {
                Rect = rect,
                Bg = bg,
                Border = border,
                Icon = icon,
                StatusTrack = statusTrack,
                Status = statusBar,
                Count = count,
                Key = key,
                SelFrame = selFrame,
                LastCount = 0,
                AppliedVersion = _hotkeyDirtyVersion,
            };
        }

        private GameObject BuildSelFrame(RectTransform parent, int uiLayer)
        {
            var frameGo = new GameObject("SelFrame", typeof(RectTransform));
            if (uiLayer >= 0) frameGo.layer = uiLayer;
            var frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.SetParent(parent, false);
            StretchFull(frameRect);
            // 锚点与轴心严格向内对齐 0..3px 环带，避免向外溢出槽位及四角重叠
            AddEdge(frameRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, SelFrameThickness), uiLayer);
            AddEdge(frameRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, SelFrameThickness), uiLayer);
            AddEdge(frameRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(SelFrameThickness, -SelFrameThickness * 2f), uiLayer);
            AddEdge(frameRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(SelFrameThickness, -SelFrameThickness * 2f), uiLayer);
            frameGo.SetActive(false);
            return frameGo;
        }

        private static void AddEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, int uiLayer)
        {
            var edge = NewImage("Edge", parent, SelFrameColor, uiLayer);
            var r = edge.rectTransform;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
        }

        private void Layout()
        {
            int n = _slots.Count;
            float scale = _cfg.Scale.Value;
            _root.localScale = new Vector3(scale, scale, 1f);

            _root.anchorMin = _root.anchorMax = new Vector2(_cfg.AnchorX.Value, _cfg.AnchorY.Value);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(_cfg.OffsetX.Value, _cfg.OffsetY.Value);

            if (_cfg.Vertical.Value) LayoutVertical(n);
            else LayoutHorizontal(n);
        }

        private void LayoutHorizontal(int n)
        {
            int rows = Mathf.Clamp(_cfg.RowCount.Value, 1, Mathf.Max(1, n));
            int cols = Mathf.CeilToInt((float)n / rows);

            float fullW = cols * SlotSize + (cols - 1) * SlotGap;
            float totalH = rows * SlotSize + (rows - 1) * SlotGap;
            _root.sizeDelta = new Vector2(fullW, totalH);

            float bottomY = -totalH / 2f + SlotSize / 2f;
            for (int i = 0; i < n; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int rowCount = Mathf.Min(cols, n - row * cols);
                float rowW = rowCount * SlotSize + (rowCount - 1) * SlotGap;
                float startX = -rowW / 2f + SlotSize / 2f;
                float y = bottomY + (rows - 1 - row) * (SlotSize + SlotGap);

                _slots[i].Rect.anchorMin = _slots[i].Rect.anchorMax = new Vector2(0.5f, 0.5f);
                _slots[i].Rect.anchoredPosition = new Vector2(startX + col * (SlotSize + SlotGap), y);
            }
        }

        private void LayoutVertical(int n)
        {
            int cols = Mathf.Clamp(_cfg.RowCount.Value, 1, Mathf.Max(1, n));
            int perCol = Mathf.CeilToInt((float)n / cols);

            float fullW = cols * SlotSize + (cols - 1) * SlotGap;
            float totalH = perCol * SlotSize + (perCol - 1) * SlotGap;
            _root.sizeDelta = new Vector2(fullW, totalH);

            float rightX = fullW / 2f - SlotSize / 2f;
            float topY = totalH / 2f - SlotSize / 2f;
            for (int i = 0; i < n; i++)
            {
                int col = i / perCol;
                int row = i % perCol;
                float x = rightX - col * (SlotSize + SlotGap);
                float y = topY - row * (SlotSize + SlotGap);

                _slots[i].Rect.anchorMin = _slots[i].Rect.anchorMax = new Vector2(0.5f, 0.5f);
                _slots[i].Rect.anchoredPosition = new Vector2(x, y);
            }
        }

        private void UpdateGraphics()
        {
            float bgA = Mathf.Clamp01(_cfg.BackgroundAlpha.Value);
            float borderA = Mathf.Clamp01(SlotBorder.a * (bgA / SlotBg.a));
            int hotkeyVersion = _hotkeyDirtyVersion;

            for (int i = 0; i < _slots.Count; i++)
            {
                var item = _model.GetItem(i);
                var slot = _slots[i];
                if (item != null) UpdateItemSlot(slot, item, i);
                else ClearItemSlot(slot);

                slot.Bg.color = new Color(SlotBg.r, SlotBg.g, SlotBg.b, bgA);
                slot.Border.color = new Color(SlotBorder.r, SlotBorder.g, SlotBorder.b, borderA);

                // 仅当 SettingChanged 推进脏版本号时重算角标，消除每槽每帧 KeyboardShortcut.Equals 装箱与 LINQ 分配
                if (slot.AppliedVersion != hotkeyVersion)
                {
                    slot.Key.text = FormatKeyLabel(_cfg.SlotHotkey(i).Value);
                    slot.AppliedVersion = hotkeyVersion;
                }

                bool isSel = i == _selected;
                slot.Key.color = isSel ? Color.white : KeyUnselectedColor;
                if (slot.SelFrame.activeSelf != isSel)
                    slot.SelFrame.SetActive(isSel);
            }
        }

        private void UpdateItemSlot(Slot slot, Item item, int index)
        {
            var sr = item.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                if (slot.Icon.sprite != sr.sprite)
                {
                    slot.Icon.sprite = sr.sprite;
                    slot.Icon.rectTransform.sizeDelta = PlayerCamera.ImageSizeDelta(sr.sprite.texture, 3f, IconMaxSize);
                }
                slot.Icon.enabled = true;
            }
            else
            {
                slot.Icon.enabled = false;
            }

            ApplyStatusBar(slot, item);

            int count = _model.CountInBody(index);
            if (count != slot.LastCount)
            {
                slot.Count.text = count > 1 ? "x" + count : "";
                slot.LastCount = count;
            }
        }

        private static void ClearItemSlot(Slot slot)
        {
            slot.Icon.enabled = false;
            slot.StatusTrack.enabled = false;
            slot.Status.enabled = false;
            if (slot.LastCount != 0)
            {
                slot.Count.text = "";
                slot.LastCount = 0;
            }
        }

        private static void ApplyStatusBar(Slot slot, Item item)
        {
            float fill;
            if (item.battery != null && item.battery.hasBattery && item.battery.maxCharge > 0f)
            {
                fill = Mathf.Clamp01(item.battery.GetCharge() / item.battery.maxCharge);
            }
            else if (item.TryGetComponent<AmmoScript>(out var ammo)
                     && ammo.itemType == AmmoScript.AmmoItemType.Magazine && ammo.maxRounds > 0)
            {
                fill = Mathf.Clamp01((float)ammo.rounds / ammo.maxRounds);
            }
            else if (item.container != null && item.container.maxWeight > 0f)
            {
                fill = Mathf.Clamp01(item.container.GetHoldingWeight() / item.container.maxWeight);
            }
            else
            {
                fill = Mathf.Clamp01(item.condition);
            }

            float innerW = SlotSize - StatusBarInset * 2f;
            slot.StatusTrack.enabled = true;
            slot.Status.rectTransform.sizeDelta = new Vector2(innerW * fill, StatusBarHeight);
            slot.Status.color = Color.white;
            slot.Status.enabled = true;
        }

        private static string FormatKeyLabel(KeyboardShortcut sc)
        {
            if (sc.MainKey == KeyCode.None) return "";
            string label = ShortKey(sc.MainKey);
            foreach (var mod in sc.Modifiers)
            {
                label = ModPrefix(mod) + "+" + label;
            }
            return label;
        }

        private static string ShortKey(KeyCode key)
        {
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return ((int)(key - KeyCode.Keypad0)).ToString();
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            switch (key)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                default: return key.ToString();
            }
        }

        private static string ModPrefix(KeyCode mod)
        {
            switch (mod)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "S";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "C";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return "A";
                default: return mod.ToString();
            }
        }

        private static Sprite EnsureBorderSprite()
        {
            if (_borderSprite != null) return _borderSprite;

            const int borderPx = 1;
            const int size = borderPx * 2 + 4;
            _borderTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var edge = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isEdge = x < borderPx || x >= size - borderPx || y < borderPx || y >= size - borderPx;
                    pixels[y * size + x] = isEdge ? edge : clear;
                }
            }
            _borderTex.SetPixels32(pixels);
            _borderTex.Apply();

            _borderSprite = Sprite.Create(
                _borderTex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(borderPx, borderPx, borderPx, borderPx));
            _borderSprite.hideFlags = HideFlags.HideAndDontSave;
            return _borderSprite;
        }

        private static Font ResolveBuiltinFont()
        {
            if (_fontResolved) return _builtinFont;
            _fontResolved = true;
            // Unity 2022.2+ 内置默认字体名为 LegacyRuntime.ttf，旧版回退 Arial.ttf
            try { _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (_builtinFont == null)
            {
                try { _builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return _builtinFont;
        }

        private static Image NewImage(string name, RectTransform parent, Color color, int uiLayer)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            if (uiLayer >= 0) go.layer = uiLayer;
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text NewText(string name, RectTransform parent, int fontSize, TextAnchor anchor, int uiLayer)
        {
            // 挂载黑色半透明描边，确保角标叠在医疗箱等高亮度浅色物品图标上仍具高对比度
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            if (uiLayer >= 0) go.layer = uiLayer;
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = ResolveBuiltinFont();
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = TextOutlineColor;
            outline.effectDistance = TextOutlineDistance;
            return t;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private sealed class Slot
        {
            public RectTransform Rect;
            public Image Bg;
            public Image Border;
            public Image Icon;
            public Image StatusTrack;
            public Image Status;
            public Text Count;
            public Text Key;
            public GameObject SelFrame;
            public int LastCount;
            public int AppliedVersion;
        }
    }
}
