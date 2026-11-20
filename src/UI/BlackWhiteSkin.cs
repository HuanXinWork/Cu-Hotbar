using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>设置面板黑白线条风格 GUISkin 与基础几何绘制辅助。</summary>
    internal static class BlackWhiteSkin
    {
        private static GUISkin _skin;
        private static Texture2D _bgPanel;
        private static Texture2D _bgButton;
        private static Texture2D _bgButtonHover;
        private static Texture2D _bgCard;
        private static Texture2D _bgHintCard;
        private static Texture2D _bgTab;
        private static Texture2D _bgTabHover;
        private static Texture2D _bgTabActive;
        private static Texture2D _bgControlBtn;
        private static Texture2D _bgControlBtnHover;
        private static Texture2D _bgControlBtnActive;
        private static Texture2D _bgValueBox;
        private static Texture2D _bgSliderTrack;
        private static Texture2D _line;

        private static GUIStyle _hStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _hButtonStyle;
        private static GUIStyle _cardStyle;
        private static GUIStyle _hintCardStyle;
        private static GUIStyle _tabStyle;
        private static GUIStyle _tabActiveStyle;
        private static GUIStyle _controlBtnStyle;
        private static GUIStyle _controlBtnActiveStyle;
        private static GUIStyle _valueBoxStyle;
        private static GUIStyle _lineStyle;
        private static GUIStyle _iconBtnStyle;
        private static GUIStyle _rowLabelStyle;
        private static GUIStyle _hintLabelStyle;
        private static GUIStyle _mutedLabelStyle;
        private static GUIStyle _centerTitleStyle;
        private static GUIStyle _centerSubTitleStyle;
        private static GUIStyle _centerLabelStyle;

        internal static GUIStyle HeaderStyle => _hStyle;
        internal static GUIStyle SectionHeaderStyle => _sectionHeaderStyle;
        internal static GUIStyle HeaderButtonStyle => _hButtonStyle;
        internal static GUIStyle CardStyle => _cardStyle;
        internal static GUIStyle HintCardStyle => _hintCardStyle;
        internal static GUIStyle TabStyle => _tabStyle;
        internal static GUIStyle TabActiveStyle => _tabActiveStyle;
        internal static GUIStyle ControlBtnStyle => _controlBtnStyle;
        internal static GUIStyle ControlBtnActiveStyle => _controlBtnActiveStyle;
        internal static GUIStyle ValueBoxStyle => _valueBoxStyle;
        internal static GUIStyle LineStyle => _lineStyle;
        internal static GUIStyle IconBtnStyle => _iconBtnStyle;
        internal static GUIStyle RowLabelStyle => _rowLabelStyle;
        internal static GUIStyle HintLabelStyle => _hintLabelStyle;
        internal static GUIStyle MutedLabelStyle => _mutedLabelStyle;
        internal static GUIStyle CenterTitleStyle => _centerTitleStyle;
        internal static GUIStyle CenterSubTitleStyle => _centerSubTitleStyle;
        internal static GUIStyle CenterLabelStyle => _centerLabelStyle;
        internal static Texture2D LineTex => _line;

        private static GUISkin _previousSkin;

        internal static void EnsureStyles() => EnsureBuilt();

        internal static void Push()
        {
            EnsureBuilt();
            _previousSkin = GUI.skin;
            GUI.skin = _skin;
        }

        internal static void Pop()
        {
            GUI.skin = _previousSkin;
        }

        internal static void Dispose()
        {
            DestroyObject(ref _skin);
            DestroyTexture(ref _bgPanel);
            DestroyTexture(ref _bgButton);
            DestroyTexture(ref _bgButtonHover);
            DestroyTexture(ref _bgCard);
            DestroyTexture(ref _bgHintCard);
            DestroyTexture(ref _bgTab);
            DestroyTexture(ref _bgTabHover);
            DestroyTexture(ref _bgTabActive);
            DestroyTexture(ref _bgControlBtn);
            DestroyTexture(ref _bgControlBtnHover);
            DestroyTexture(ref _bgControlBtnActive);
            DestroyTexture(ref _bgValueBox);
            DestroyTexture(ref _bgSliderTrack);
            DestroyTexture(ref _line);

            _hStyle = null;
            _sectionHeaderStyle = null;
            _hButtonStyle = null;
            _cardStyle = null;
            _hintCardStyle = null;
            _tabStyle = null;
            _tabActiveStyle = null;
            _controlBtnStyle = null;
            _controlBtnActiveStyle = null;
            _valueBoxStyle = null;
            _lineStyle = null;
            _iconBtnStyle = null;
            _rowLabelStyle = null;
            _hintLabelStyle = null;
            _mutedLabelStyle = null;
            _centerTitleStyle = null;
            _centerSubTitleStyle = null;
            _centerLabelStyle = null;
            _previousSkin = null;
        }

        internal static void DrawHLine(Rect rect)
        {
            EnsureBuilt();
            GUI.DrawTexture(rect, _line, ScaleMode.StretchToFill);
        }

        internal static void DrawBorder(Rect rect) => DrawBorder(rect, 1f);

        internal static void DrawBorder(Rect rect, float thickness)
        {
            EnsureBuilt();
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _line, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _line, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _line, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _line, ScaleMode.StretchToFill);
        }

        internal static void DrawCloseX(Rect rect, float thickness = 2f)
        {
            EnsureBuilt();
            DrawDiagonalLine(rect.x, rect.y, rect.xMax, rect.yMax, thickness);
            DrawDiagonalLine(rect.x, rect.yMax, rect.xMax, rect.y, thickness);
        }

        internal static void DrawPencil(Rect rect, float thickness = 2f)
        {
            EnsureBuilt();
            float pad = thickness;
            float x1 = rect.x + pad, y1 = rect.yMax - pad;
            float x2 = rect.xMax - pad, y2 = rect.y + pad;
            DrawDiagonalLine(x1, y1, x2, y2, thickness);
            DrawDiagonalLine(x1, y1, x1 + rect.width * 0.18f, y1 - rect.height * 0.05f, thickness);
            DrawDiagonalLine(x1, y1, x1 + rect.width * 0.05f, y1 - rect.height * 0.18f, thickness);
            DrawDiagonalLine(x2, y2, x2 - rect.width * 0.18f, y2 + rect.height * 0.05f, thickness);
            DrawDiagonalLine(x2, y2, x2 - rect.width * 0.05f, y2 + rect.height * 0.18f, thickness);
        }

        private static void DrawDiagonalLine(float x1, float y1, float x2, float y2, float thickness)
        {
            EnsureBuilt();
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            // 沿对角线铺轴对齐小方块，避免旋转矩阵与外层缩放矩阵复合导致纹理走样
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.5f, thickness * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = x1 + dx * t;
                float y = y1 + dy * t;
                GUI.DrawTexture(new Rect(x - thickness * 0.5f, y - thickness * 0.5f, thickness, thickness),
                    _line, ScaleMode.StretchToFill);
            }
        }

        private static void EnsureBuilt()
        {
            if (_skin != null) return;

            _bgPanel = MakeColorTex(8, 8, 8, 224);
            _bgButton = MakeBorderedTex(14, 14, 14, 185, 255, 255, 255, 255, 2);
            _bgButtonHover = MakeBorderedTex(58, 58, 58, 215, 255, 255, 255, 255, 2);
            _bgCard = MakeBorderedTex(16, 16, 16, 170, 255, 255, 255, 55, 1);
            _bgHintCard = MakeBorderedTex(255, 255, 255, 16, 255, 255, 255, 42, 1);
            _bgTab = MakeBorderedTex(12, 12, 12, 180, 255, 255, 255, 220, 2);
            _bgTabHover = MakeBorderedTex(52, 52, 52, 215, 255, 255, 255, 255, 2);
            _bgTabActive = MakeBorderedTex(255, 255, 255, 240, 255, 255, 255, 255, 2);
            _bgControlBtn = MakeBorderedTex(20, 20, 20, 190, 255, 255, 255, 150, 1);
            _bgControlBtnHover = MakeBorderedTex(58, 58, 58, 220, 255, 255, 255, 240, 1);
            _bgControlBtnActive = MakeBorderedTex(255, 255, 255, 240, 255, 255, 255, 255, 1);
            _bgValueBox = MakeBorderedTex(255, 255, 255, 20, 255, 255, 255, 68, 1);
            _bgSliderTrack = MakeBorderedTex(30, 30, 30, 225, 255, 255, 255, 105, 1);
            _line = MakeColorTex(255, 255, 255, 255);

            _skin = ScriptableObject.CreateInstance<GUISkin>();
            _skin.hideFlags = HideFlags.HideAndDontSave;
            CopyDefaultsInto(_skin);

            ApplyMonochrome(_skin.box, _bgPanel, Color.white);
            ApplyMonochrome(_skin.window, _bgPanel, Color.white);
            ApplyMonochrome(_skin.button, _bgButton, Color.white, _bgButtonHover);
            ApplyMonochrome(_skin.label, null, Color.white);
            ApplyMonochrome(_skin.textField, _bgValueBox, Color.white);
            ApplyMonochrome(_skin.textArea, _bgValueBox, Color.white);
            ApplyMonochrome(_skin.toggle, null, Color.white);
            ApplyMonochrome(_skin.horizontalSlider, _bgSliderTrack, Color.white);
            ApplyMonochrome(_skin.horizontalSliderThumb, _line, Color.black);
            ApplyMonochrome(_skin.verticalScrollbar, _bgSliderTrack, Color.white);
            ApplyMonochrome(_skin.verticalScrollbarThumb, _line, Color.black);
            ClearBackgrounds(_skin.scrollView);

            // 独立高对比滑条导轨：14px 轨道配上下 -7 padding，使 28px 白滑块严格垂直居中跨骑在导轨上
            _skin.horizontalSlider.fixedHeight = 14f;
            _skin.horizontalSlider.border = new RectOffset(2, 2, 2, 2);
            _skin.horizontalSlider.margin = new RectOffset(4, 4, 12, 12);
            _skin.horizontalSlider.padding = new RectOffset(0, 0, -7, -7);
            _skin.horizontalSliderThumb.fixedWidth = 16f;
            _skin.horizontalSliderThumb.fixedHeight = 28f;
            _skin.horizontalSliderThumb.margin = new RectOffset(0, 0, 0, 0);
            _skin.horizontalSliderThumb.overflow = new RectOffset(0, 0, 0, 0);

            _skin.verticalScrollbar.fixedWidth = 14f;
            _skin.verticalScrollbar.border = new RectOffset(2, 2, 2, 2);
            _skin.verticalScrollbarThumb.fixedWidth = 14f;

            _skin.button.alignment = TextAnchor.MiddleCenter;
            _skin.button.fontSize = 19;
            _skin.button.fontStyle = FontStyle.Bold;
            _skin.button.padding = new RectOffset(12, 12, 8, 8);
            _skin.button.border = new RectOffset(2, 2, 2, 2);
            _skin.button.wordWrap = false;
            _skin.button.clipping = TextClipping.Overflow;
            _skin.label.fontSize = 19;
            _skin.label.fontStyle = FontStyle.Bold;
            _skin.label.wordWrap = false;
            _skin.label.clipping = TextClipping.Overflow;
            _skin.textField.fontSize = 19;
            _skin.textField.fontStyle = FontStyle.Bold;
            _skin.textField.border = new RectOffset(2, 2, 2, 2);
            _skin.textField.padding = new RectOffset(10, 10, 6, 6);
            _skin.toggle.fontSize = 19;
            _skin.toggle.fontStyle = FontStyle.Bold;
            _skin.box.fontSize = 19;
            _skin.box.fontStyle = FontStyle.Bold;

            _hStyle = new GUIStyle(_skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };

            _sectionHeaderStyle = new GUIStyle(_skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(2, 2, 2, 2),
            };

            _hButtonStyle = new GUIStyle(_skin.button)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(18, 18, 10, 10),
            };
            ApplyMonochrome(_hButtonStyle, _bgButton, Color.white, _bgButtonHover);

            _cardStyle = new GUIStyle(_skin.box)
            {
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(18, 18, 14, 14),
                margin = new RectOffset(0, 0, 2, 2),
            };
            ApplyMonochrome(_cardStyle, _bgCard, Color.white);

            _hintCardStyle = new GUIStyle(_skin.box)
            {
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(14, 14, 10, 10),
                margin = new RectOffset(0, 0, 2, 2),
            };
            ApplyMonochrome(_hintCardStyle, _bgHintCard, Color.white);

            _tabStyle = new GUIStyle(_skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(14, 14, 8, 8),
                margin = new RectOffset(0, 0, 0, 0),
            };
            ApplyMonochrome(_tabStyle, _bgTab, Color.white, _bgTabHover);

            _tabActiveStyle = new GUIStyle(_tabStyle);
            ApplyMonochrome(_tabActiveStyle, _bgTabActive, Color.black);

            // 水平 margin 置零，使行内多按钮组仅由显式 GUILayout.Space 控制间距，确保 200px/296px 网格像素级齐平
            _controlBtnStyle = new GUIStyle(_skin.button)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyMonochrome(_controlBtnStyle, _bgControlBtn, Color.white, _bgControlBtnHover);

            _controlBtnActiveStyle = new GUIStyle(_controlBtnStyle);
            ApplyMonochrome(_controlBtnActiveStyle, _bgControlBtnActive, Color.black);

            _valueBoxStyle = new GUIStyle(_skin.box)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyMonochrome(_valueBoxStyle, _bgValueBox, Color.white);

            _iconBtnStyle = new GUIStyle(_skin.button)
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(2, 2, 0, 0),
                border = new RectOffset(2, 2, 2, 2),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };
            ApplyMonochrome(_iconBtnStyle, _bgButton, Color.white, _bgButtonHover);

            _lineStyle = new GUIStyle(_skin.label)
            {
                fixedHeight = 1f,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
            };

            _rowLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 8, 2, 2),
            };

            _hintLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(2, 2, 2, 2),
            };
            ApplyMonochrome(_hintLabelStyle, null, new Color(1f, 1f, 1f, 0.82f));

            _mutedLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleRight,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(4, 2, 2, 2),
            };
            ApplyMonochrome(_mutedLabelStyle, null, new Color(1f, 1f, 1f, 0.65f));

            _centerTitleStyle = new GUIStyle(_skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            ApplyMonochrome(_centerTitleStyle, null, Color.white);

            _centerSubTitleStyle = new GUIStyle(_skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            ApplyMonochrome(_centerSubTitleStyle, null, Color.white);

            _centerLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            ApplyMonochrome(_centerLabelStyle, null, new Color(1f, 1f, 1f, 0.85f));
        }

        private static void CopyDefaultsInto(GUISkin dst)
        {
            var defaults = GUI.skin;
            dst.font = defaults.font;
            dst.box = new GUIStyle(defaults.box);
            dst.window = new GUIStyle(defaults.window);
            dst.button = new GUIStyle(defaults.button);
            dst.label = new GUIStyle(defaults.label);
            dst.textField = new GUIStyle(defaults.textField);
            dst.textArea = new GUIStyle(defaults.textArea);
            dst.toggle = new GUIStyle(defaults.toggle);
            dst.horizontalSlider = new GUIStyle(defaults.horizontalSlider);
            dst.horizontalSliderThumb = new GUIStyle(defaults.horizontalSliderThumb);
            dst.verticalScrollbar = new GUIStyle(defaults.verticalScrollbar);
            dst.verticalScrollbarThumb = new GUIStyle(defaults.verticalScrollbarThumb);
            dst.scrollView = new GUIStyle(defaults.scrollView);
        }

        private static void ClearBackgrounds(GUIStyle s)
        {
            s.normal.background = null;
            s.onNormal.background = null;
            s.active.background = null;
            s.onActive.background = null;
            s.focused.background = null;
            s.onFocused.background = null;
            s.hover.background = null;
            s.onHover.background = null;
        }

        private static void ApplyMonochrome(GUIStyle s, Texture2D bg, Color text, Texture2D hoverBg = null)
        {
            if (bg != null)
            {
                Texture2D activeBg = hoverBg != null ? hoverBg : bg;
                s.normal.background = bg;
                s.onNormal.background = bg;
                s.active.background = activeBg;
                s.onActive.background = activeBg;
                s.focused.background = bg;
                s.onFocused.background = bg;
                s.hover.background = activeBg;
                s.onHover.background = activeBg;
            }
            s.normal.textColor = text;
            s.onNormal.textColor = text;
            s.active.textColor = text;
            s.onActive.textColor = text;
            s.focused.textColor = text;
            s.onFocused.textColor = text;
            s.hover.textColor = text;
            s.onHover.textColor = text;
        }

        private static Texture2D MakeColorTex(byte r, byte g, byte b, byte a)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var c = new Color32(r, g, b, a);
            t.SetPixels32(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        private static Texture2D MakeBorderedTex(byte fr, byte fg, byte fb, byte fa,
            byte br, byte bg, byte bb, byte ba, int borderPx)
        {
            // 内部保留 4x4 纯填充中心区，避免九宫格 border=RectOffset(2,2,2,2) 拉伸时中心区宽度退化为 0
            int size = borderPx * 2 + 4;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };
            var fill = new Color32(fr, fg, fb, fa);
            var edge = new Color32(br, bg, bb, ba);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isEdge = x < borderPx || x >= size - borderPx || y < borderPx || y >= size - borderPx;
                    pixels[y * size + x] = isEdge ? edge : fill;
                }
            }
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        private static void DestroyTexture(ref Texture2D tex)
        {
            if (tex != null)
            {
                try { Object.Destroy(tex); } catch { }
                tex = null;
            }
        }

        private static void DestroyObject<T>(ref T obj) where T : Object
        {
            if (obj != null)
            {
                try { Object.Destroy(obj); } catch { }
                obj = null;
            }
        }
    }
}
