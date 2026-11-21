using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>设置面板半透明黑白线条风格 GUISkin 与基础几何绘制辅助。</summary>
    internal static class BlackWhiteSkin
    {
        private static readonly Stack<GUISkin> SkinStack = new Stack<GUISkin>(4);
        private static readonly GUIContent MeasureContent = new GUIContent();

        private static GUISkin _skin;
        private static Texture2D _bgPanel;
        private static Texture2D _bgCard;
        private static Texture2D _bgHintCard;
        private static Texture2D _bgInput;
        private static Texture2D _bgInputFocus;
        private static Texture2D _bgButton;
        private static Texture2D _bgButtonHover;
        private static Texture2D _bgButtonActive;
        private static Texture2D _bgControlBtn;
        private static Texture2D _bgControlBtnHover;
        private static Texture2D _bgControlBtnPress;
        private static Texture2D _bgInverted2;
        private static Texture2D _bgInverted2Hover;
        private static Texture2D _bgInverted2Active;
        private static Texture2D _bgInverted1;
        private static Texture2D _bgInverted1Hover;
        private static Texture2D _bgInverted1Active;
        private static Texture2D _bgValueBox;
        private static Texture2D _bgSliderTrack;
        private static Texture2D _thumbActive;
        private static Texture2D _line;

        private static GUIStyle _hStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _hButtonStyle;
        private static GUIStyle _overlayBtnStyle;
        private static GUIStyle _cardStyle;
        private static GUIStyle _hintCardStyle;
        private static GUIStyle _tabStyle;
        private static GUIStyle _tabActiveStyle;
        private static GUIStyle _actionBtnStyle;
        private static GUIStyle _actionBtnActiveStyle;
        private static GUIStyle _controlBtnStyle;
        private static GUIStyle _controlBtnActiveStyle;
        private static GUIStyle _scaleBtnStyle;
        private static GUIStyle _scaleBtnActiveStyle;
        private static GUIStyle _valueBoxStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _iconBtnStyle;
        private static GUIStyle _singleLineLabelStyle;
        private static GUIStyle _rowLabelStyle;
        private static GUIStyle _hintLabelStyle;
        private static GUIStyle _mutedLabelStyle;
        private static GUIStyle _centerTitleStyle;
        private static GUIStyle _centerSubTitleStyle;
        private static GUIStyle _centerLabelStyle;

        internal static GUIStyle HeaderStyle { get { EnsureBuilt(); return _hStyle; } }
        internal static GUIStyle SectionHeaderStyle { get { EnsureBuilt(); return _sectionHeaderStyle; } }
        internal static GUIStyle HeaderButtonStyle { get { EnsureBuilt(); return _hButtonStyle; } }
        internal static GUIStyle OverlayBtnStyle { get { EnsureBuilt(); return _overlayBtnStyle; } }
        internal static GUIStyle CardStyle { get { EnsureBuilt(); return _cardStyle; } }
        internal static GUIStyle HintCardStyle { get { EnsureBuilt(); return _hintCardStyle; } }
        internal static GUIStyle TabStyle { get { EnsureBuilt(); return _tabStyle; } }
        internal static GUIStyle TabActiveStyle { get { EnsureBuilt(); return _tabActiveStyle; } }
        internal static GUIStyle ActionBtnStyle { get { EnsureBuilt(); return _actionBtnStyle; } }
        internal static GUIStyle ActionBtnActiveStyle { get { EnsureBuilt(); return _actionBtnActiveStyle; } }
        internal static GUIStyle ControlBtnStyle { get { EnsureBuilt(); return _controlBtnStyle; } }
        internal static GUIStyle ControlBtnActiveStyle { get { EnsureBuilt(); return _controlBtnActiveStyle; } }
        internal static GUIStyle ScaleBtnStyle { get { EnsureBuilt(); return _scaleBtnStyle; } }
        internal static GUIStyle ScaleBtnActiveStyle { get { EnsureBuilt(); return _scaleBtnActiveStyle; } }
        internal static GUIStyle ValueBoxStyle { get { EnsureBuilt(); return _valueBoxStyle; } }
        internal static GUIStyle BadgeStyle { get { EnsureBuilt(); return _badgeStyle; } }
        internal static GUIStyle IconBtnStyle { get { EnsureBuilt(); return _iconBtnStyle; } }
        internal static GUIStyle SingleLineLabelStyle { get { EnsureBuilt(); return _singleLineLabelStyle; } }
        internal static GUIStyle RowLabelStyle { get { EnsureBuilt(); return _rowLabelStyle; } }
        internal static GUIStyle HintLabelStyle { get { EnsureBuilt(); return _hintLabelStyle; } }
        internal static GUIStyle MutedLabelStyle { get { EnsureBuilt(); return _mutedLabelStyle; } }
        internal static GUIStyle CenterTitleStyle { get { EnsureBuilt(); return _centerTitleStyle; } }
        internal static GUIStyle CenterSubTitleStyle { get { EnsureBuilt(); return _centerSubTitleStyle; } }
        internal static GUIStyle CenterLabelStyle { get { EnsureBuilt(); return _centerLabelStyle; } }
        internal static Texture2D LineTex { get { EnsureBuilt(); return _line; } }

        internal static void EnsureStyles() => EnsureBuilt();

        internal static void Push()
        {
            EnsureBuilt();
            SkinStack.Push(GUI.skin);
            GUI.skin = _skin;
        }

        internal static void Pop()
        {
            if (SkinStack.Count > 0)
            {
                GUI.skin = SkinStack.Pop();
            }
        }

        // 点击按钮后立即释放残留 keyboardControl，防止输入焦点锁死全局热键
        internal static void ClearTextFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
        }

        // GUILayout.MinWidth 在空间吃紧时仍会被压回下限，必须按 CalcSize 预留实测宽度传给 GUILayout.Width
        internal static float CalcWidth(GUIStyle style, string text, float minWidth, float extraPad = 8f)
        {
            if (style == null) return minWidth;
            MeasureContent.text = text ?? string.Empty;
            float measured = Mathf.Ceil(style.CalcSize(MeasureContent).x) + extraPad;
            return Mathf.Max(minWidth, measured);
        }

        internal static void Dispose()
        {
            SkinStack.Clear();
            DestroyObject(ref _skin);
            DestroyTexture(ref _bgPanel);
            DestroyTexture(ref _bgCard);
            DestroyTexture(ref _bgHintCard);
            DestroyTexture(ref _bgInput);
            DestroyTexture(ref _bgInputFocus);
            DestroyTexture(ref _bgButton);
            DestroyTexture(ref _bgButtonHover);
            DestroyTexture(ref _bgButtonActive);
            DestroyTexture(ref _bgControlBtn);
            DestroyTexture(ref _bgControlBtnHover);
            DestroyTexture(ref _bgControlBtnPress);
            DestroyTexture(ref _bgInverted2);
            DestroyTexture(ref _bgInverted2Hover);
            DestroyTexture(ref _bgInverted2Active);
            DestroyTexture(ref _bgInverted1);
            DestroyTexture(ref _bgInverted1Hover);
            DestroyTexture(ref _bgInverted1Active);
            DestroyTexture(ref _bgValueBox);
            DestroyTexture(ref _bgSliderTrack);
            DestroyTexture(ref _thumbActive);
            DestroyTexture(ref _line);

            _hStyle = null;
            _sectionHeaderStyle = null;
            _hButtonStyle = null;
            _overlayBtnStyle = null;
            _cardStyle = null;
            _hintCardStyle = null;
            _tabStyle = null;
            _tabActiveStyle = null;
            _actionBtnStyle = null;
            _actionBtnActiveStyle = null;
            _controlBtnStyle = null;
            _controlBtnActiveStyle = null;
            _scaleBtnStyle = null;
            _scaleBtnActiveStyle = null;
            _valueBoxStyle = null;
            _badgeStyle = null;
            _iconBtnStyle = null;
            _singleLineLabelStyle = null;
            _rowLabelStyle = null;
            _hintLabelStyle = null;
            _mutedLabelStyle = null;
            _centerTitleStyle = null;
            _centerSubTitleStyle = null;
            _centerLabelStyle = null;
        }

        internal static void DrawHLine(Rect rect)
        {
            EnsureBuilt();
            GUI.DrawTexture(rect, _line, ScaleMode.StretchToFill);
        }

        internal static void DrawSoftVLine(Rect rect)
        {
            EnsureBuilt();
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(rect, _line, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        // 半透明面板透出高亮游戏画面时，1px 黑色偏移底影可保证白字标题边缘锐利可读
        internal static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
        {
            EnsureBuilt();
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
                GUI.color = prev;
            }
            GUI.Label(rect, text, style);
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

        internal static void DrawCloseX(Rect rect, float thickness = 5f)
        {
            EnsureBuilt();
            DrawDiagonalLine(rect.x, rect.y, rect.xMax, rect.yMax, thickness);
            DrawDiagonalLine(rect.x, rect.yMax, rect.xMax, rect.y, thickness);
        }

        private static void DrawDiagonalLine(float x1, float y1, float x2, float y2, float thickness)
        {
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

            _bgPanel = MakeColorTex(0, 0, 0, 125);
            _bgCard = MakeBorderedTex(15, 15, 15, 105, 255, 255, 255, 100, 1);
            _bgHintCard = MakeBorderedTex(15, 15, 15, 105, 255, 255, 255, 105, 1);
            _bgInput = MakeBorderedTex(0, 0, 0, 150, 255, 255, 255, 130, 1);
            _bgInputFocus = MakeBorderedTex(0, 0, 0, 150, 255, 255, 255, 255, 2);

            _bgButton = MakeBorderedTex(0, 0, 0, 150, 255, 255, 255, 220, 2);
            _bgButtonHover = MakeBorderedTex(60, 60, 60, 190, 255, 255, 255, 255, 2);
            _bgButtonActive = MakeBorderedTex(90, 90, 90, 210, 255, 255, 255, 255, 2);

            _bgControlBtn = MakeBorderedTex(0, 0, 0, 130, 255, 255, 255, 160, 1);
            _bgControlBtnHover = MakeBorderedTex(60, 60, 60, 190, 255, 255, 255, 240, 1);
            _bgControlBtnPress = MakeBorderedTex(90, 90, 90, 210, 255, 255, 255, 255, 1);

            _bgInverted2 = MakeBorderedTex(255, 255, 255, 235, 255, 255, 255, 255, 2);
            _bgInverted2Hover = MakeBorderedTex(255, 255, 255, 250, 255, 255, 255, 255, 2);
            _bgInverted2Active = MakeBorderedTex(220, 220, 220, 245, 255, 255, 255, 255, 2);

            _bgInverted1 = MakeBorderedTex(255, 255, 255, 235, 255, 255, 255, 255, 1);
            _bgInverted1Hover = MakeBorderedTex(255, 255, 255, 250, 255, 255, 255, 255, 1);
            _bgInverted1Active = MakeBorderedTex(220, 220, 220, 245, 255, 255, 255, 255, 1);

            _bgValueBox = MakeBorderedTex(0, 0, 0, 150, 255, 255, 255, 160, 1);
            _bgSliderTrack = MakeBorderedTex(0, 0, 0, 150, 255, 255, 255, 130, 1);
            _thumbActive = MakeColorTex(220, 220, 220, 255);
            _line = MakeColorTex(255, 255, 255, 255);

            var border1 = new RectOffset(1, 1, 1, 1);
            var border2 = new RectOffset(2, 2, 2, 2);
            var zeroOffset = new RectOffset(0, 0, 0, 0);
            var hintTextColor = new Color(1f, 1f, 1f, 0.88f);
            var mutedTextColor = new Color(1f, 1f, 1f, 0.80f);

            _skin = ScriptableObject.CreateInstance<GUISkin>();
            _skin.hideFlags = HideFlags.HideAndDontSave;
            CopyDefaultsInto(_skin);

            // 嵌入 CuSaveManager 时 CopyDefaultsInto 可能取到宿主皮肤，所有基础样式字段必须显式写死
            ApplyStateful(_skin.box, _bgPanel, Color.white);
            ApplyStateful(_skin.window, _bgPanel, Color.white);
            ApplyStateful(_skin.button, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);
            ClearBackgrounds(_skin.label);
            ApplyTextColor(_skin.label, Color.white);
            ApplyInput(_skin.textField, _bgInput, _bgInputFocus, Color.white, border2);
            ApplyInput(_skin.textArea, _bgInput, _bgInputFocus, Color.white, border2);
            ClearBackgrounds(_skin.toggle);
            ApplyTextColor(_skin.toggle, Color.white);
            ApplyStateful(_skin.horizontalSlider, _bgSliderTrack, Color.white);
            ApplyStateful(_skin.horizontalSliderThumb, _line, Color.black, _line, _thumbActive);
            ApplyStateful(_skin.verticalScrollbar, _bgSliderTrack, Color.white);
            ApplyStateful(_skin.verticalScrollbarThumb, _line, Color.black, _line, _thumbActive);
            ClearBackgrounds(_skin.scrollView);

            _skin.scrollView.border = zeroOffset;
            _skin.scrollView.margin = zeroOffset;
            _skin.scrollView.padding = zeroOffset;

            // 14px 轨道配上下 -7 padding 与 16 margin，使 28px 纯白滑块头在 46px 表单行内垂直居中跨骑导轨
            _skin.horizontalSlider.fixedHeight = 14f;
            _skin.horizontalSlider.border = border1;
            _skin.horizontalSlider.margin = new RectOffset(4, 4, 16, 16);
            _skin.horizontalSlider.padding = new RectOffset(0, 0, -7, -7);
            _skin.horizontalSlider.overflow = zeroOffset;
            _skin.horizontalSliderThumb.fixedWidth = 16f;
            _skin.horizontalSliderThumb.fixedHeight = 28f;
            _skin.horizontalSliderThumb.border = zeroOffset;
            _skin.horizontalSliderThumb.margin = zeroOffset;
            _skin.horizontalSliderThumb.padding = zeroOffset;
            _skin.horizontalSliderThumb.overflow = zeroOffset;

            _skin.verticalScrollbar.fixedWidth = 14f;
            _skin.verticalScrollbar.border = border1;
            _skin.verticalScrollbar.margin = zeroOffset;
            _skin.verticalScrollbar.padding = zeroOffset;
            _skin.verticalScrollbarThumb.fixedWidth = 14f;
            _skin.verticalScrollbarThumb.border = zeroOffset;
            _skin.verticalScrollbarThumb.margin = zeroOffset;
            _skin.verticalScrollbarThumb.padding = zeroOffset;

            _skin.button.alignment = TextAnchor.MiddleCenter;
            _skin.button.fontSize = 20;
            _skin.button.fontStyle = FontStyle.Bold;
            _skin.button.padding = new RectOffset(16, 16, 5, 5);
            _skin.button.margin = zeroOffset;
            _skin.button.border = border2;
            _skin.button.wordWrap = false;
            _skin.button.clipping = TextClipping.Clip;

            _skin.label.alignment = TextAnchor.MiddleLeft;
            _skin.label.fontSize = 20;
            _skin.label.fontStyle = FontStyle.Bold;
            _skin.label.padding = new RectOffset(2, 2, 2, 2);
            _skin.label.margin = zeroOffset;
            _skin.label.wordWrap = false;
            _skin.label.clipping = TextClipping.Clip;

            _skin.textField.alignment = TextAnchor.MiddleLeft;
            _skin.textField.fontSize = 20;
            _skin.textField.fontStyle = FontStyle.Bold;
            _skin.textField.padding = new RectOffset(10, 10, 5, 5);
            _skin.textField.margin = zeroOffset;
            _skin.textField.wordWrap = false;
            _skin.textField.clipping = TextClipping.Clip;

            _skin.textArea.alignment = TextAnchor.UpperLeft;
            _skin.textArea.fontSize = 18;
            _skin.textArea.fontStyle = FontStyle.Bold;
            _skin.textArea.padding = new RectOffset(10, 10, 6, 6);
            _skin.textArea.margin = zeroOffset;
            _skin.textArea.wordWrap = true;
            _skin.textArea.clipping = TextClipping.Clip;

            _skin.toggle.fontSize = 20;
            _skin.toggle.fontStyle = FontStyle.Bold;
            _skin.toggle.wordWrap = false;
            _skin.box.alignment = TextAnchor.MiddleCenter;
            _skin.box.fontSize = 18;
            _skin.box.fontStyle = FontStyle.Bold;
            _skin.box.padding = new RectOffset(8, 8, 6, 6);
            _skin.box.margin = zeroOffset;
            _skin.box.border = zeroOffset;
            _skin.box.wordWrap = false;

            _hStyle = new GUIStyle(_skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                margin = zeroOffset,
                padding = new RectOffset(0, 0, 2, 2),
            };
            ApplyTextColor(_hStyle, Color.white);

            _sectionHeaderStyle = new GUIStyle(_skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                margin = zeroOffset,
                padding = new RectOffset(2, 2, 2, 2),
            };
            ApplyTextColor(_sectionHeaderStyle, Color.white);

            _hButtonStyle = new GUIStyle(_skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                border = border2,
                padding = new RectOffset(18, 18, 10, 10),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_hButtonStyle, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);

            _overlayBtnStyle = new GUIStyle(_skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border2,
                padding = new RectOffset(16, 16, 8, 8),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_overlayBtnStyle, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);

            _cardStyle = new GUIStyle(_skin.box)
            {
                border = border1,
                padding = new RectOffset(18, 18, 14, 14),
                margin = new RectOffset(0, 6, 2, 2),
            };
            ApplyStateful(_cardStyle, _bgCard, Color.white);

            _hintCardStyle = new GUIStyle(_skin.box)
            {
                border = border1,
                padding = new RectOffset(14, 14, 10, 10),
                margin = new RectOffset(0, 6, 2, 2),
            };
            ApplyStateful(_hintCardStyle, _bgHintCard, Color.white);

            _tabStyle = new GUIStyle(_skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border2,
                padding = new RectOffset(18, 18, 6, 6),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_tabStyle, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);

            _tabActiveStyle = new GUIStyle(_tabStyle);
            ApplyStateful(_tabActiveStyle, _bgInverted2, Color.black, _bgInverted2Hover, _bgInverted2Active);

            _actionBtnStyle = new GUIStyle(_skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border2,
                padding = new RectOffset(16, 16, 5, 5),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_actionBtnStyle, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);

            _actionBtnActiveStyle = new GUIStyle(_actionBtnStyle);
            ApplyStateful(_actionBtnActiveStyle, _bgInverted2, Color.black, _bgInverted2Hover, _bgInverted2Active);

            // 水平 margin 归零，使行内多按钮组仅由显式间距控制，避免多按钮累加错位
            _controlBtnStyle = new GUIStyle(_skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border1,
                padding = new RectOffset(12, 12, 4, 4),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_controlBtnStyle, _bgControlBtn, Color.white, _bgControlBtnHover, _bgControlBtnPress);

            _controlBtnActiveStyle = new GUIStyle(_controlBtnStyle);
            ApplyStateful(_controlBtnActiveStyle, _bgInverted1, Color.black, _bgInverted1Hover, _bgInverted1Active);

            _scaleBtnStyle = new GUIStyle(_skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border2,
                padding = new RectOffset(10, 10, 4, 4),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_scaleBtnStyle, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);

            _scaleBtnActiveStyle = new GUIStyle(_scaleBtnStyle);
            ApplyStateful(_scaleBtnActiveStyle, _bgInverted2, Color.black, _bgInverted2Hover, _bgInverted2Active);

            _valueBoxStyle = new GUIStyle(_skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border1,
                padding = new RectOffset(8, 8, 4, 4),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_valueBoxStyle, _bgValueBox, Color.white);

            _badgeStyle = new GUIStyle(_skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = border1,
                padding = new RectOffset(10, 10, 3, 3),
                margin = zeroOffset,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            ApplyStateful(_badgeStyle, _bgValueBox, Color.white);

            _iconBtnStyle = new GUIStyle(_skin.button)
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = zeroOffset,
                border = border2,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            ApplyStateful(_iconBtnStyle, _bgButton, Color.white, _bgButtonHover, _bgButtonActive);

            _singleLineLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
                margin = zeroOffset,
                padding = new RectOffset(0, 8, 2, 2),
            };
            ApplyTextColor(_singleLineLabelStyle, Color.white);

            _rowLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleLeft,
                margin = zeroOffset,
                padding = new RectOffset(0, 8, 5, 5),
            };
            ApplyTextColor(_rowLabelStyle, Color.white);

            _hintLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleLeft,
                margin = zeroOffset,
                padding = new RectOffset(2, 2, 4, 4),
            };
            ApplyTextColor(_hintLabelStyle, hintTextColor);

            _mutedLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleRight,
                margin = zeroOffset,
                padding = new RectOffset(4, 2, 3, 3),
            };
            ApplyTextColor(_mutedLabelStyle, mutedTextColor);

            _centerTitleStyle = new GUIStyle(_skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                margin = zeroOffset,
                padding = new RectOffset(4, 4, 2, 2),
            };
            ApplyTextColor(_centerTitleStyle, Color.white);

            _centerSubTitleStyle = new GUIStyle(_skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                margin = zeroOffset,
                padding = new RectOffset(4, 4, 2, 2),
            };
            ApplyTextColor(_centerSubTitleStyle, Color.white);

            _centerLabelStyle = new GUIStyle(_skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                margin = zeroOffset,
                padding = new RectOffset(4, 4, 4, 4),
            };
            ApplyTextColor(_centerLabelStyle, hintTextColor);
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

        private static void ApplyStateful(GUIStyle s, Texture2D bg, Color text,
            Texture2D hoverBg = null, Texture2D activeBg = null)
        {
            s.normal.background = bg;
            s.onNormal.background = bg;
            s.hover.background = hoverBg ?? bg;
            s.onHover.background = hoverBg ?? bg;
            s.active.background = activeBg ?? hoverBg ?? bg;
            s.onActive.background = activeBg ?? hoverBg ?? bg;
            s.focused.background = bg;
            s.onFocused.background = bg;
            ApplyTextColor(s, text);
        }

        private static void ApplyInput(GUIStyle s, Texture2D normalBg, Texture2D focusBg, Color text, RectOffset border)
        {
            s.border = border;
            s.normal.background = normalBg;
            s.onNormal.background = normalBg;
            s.hover.background = normalBg;
            s.onHover.background = normalBg;
            s.active.background = focusBg;
            s.onActive.background = focusBg;
            s.focused.background = focusBg;
            s.onFocused.background = focusBg;
            ApplyTextColor(s, text);
        }

        private static void ApplyTextColor(GUIStyle s, Color text)
        {
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
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var c = new Color32(r, g, b, a);
            t.SetPixels32(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        private static Texture2D MakeBorderedTex(byte fr, byte fg, byte fb, byte fa,
            byte br, byte bg, byte bb, byte ba, int borderPx)
        {
            // 内部保留 4x4 纯填充中心区，避免九宫格拉伸时中心区宽度退化为 0
            int size = borderPx * 2 + 4;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
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
