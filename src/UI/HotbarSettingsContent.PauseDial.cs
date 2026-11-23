using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    internal sealed partial class HotbarSettingsContent
    {
        private static readonly string[] PauseDialSliderLabelKeys =
        {
            "lbl.pause_dial_angle",
            "lbl.pause_dial_radius",
            "lbl.pause_dial_scale",
        };

        private void DrawPauseDialSection(float sliderLabelW)
        {
            DrawSectionHeader(HotbarI18n.T("sec.pause_dial"));
            GUILayout.BeginVertical(BlackWhiteSkin.CardStyle);

            DrawSwitch(HotbarI18n.T("sw.pause_dial_auto_avoid"), _cfg.PauseDialAutoAvoid);
            GUILayout.Space(RowGap);

            DrawDialFloatSlider(
                HotbarI18n.T("lbl.pause_dial_angle"),
                _cfg.PauseDialAngleDeg,
                -180f,
                180f,
                _cfg.PauseDialAngleDeg.Value.ToString("0.#", CultureInfo.InvariantCulture) + "°",
                sliderLabelW);
            GUILayout.Space(RowGap);

            DrawDialFloatSlider(
                HotbarI18n.T("lbl.pause_dial_radius"),
                _cfg.PauseDialRadiusOffset,
                PauseDialLayoutSolver.MinRadiusOffset,
                PauseDialLayoutSolver.MaxRadiusOffset,
                _cfg.PauseDialRadiusOffset.Value.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture) + " px",
                sliderLabelW);
            GUILayout.Space(RowGap);

            DrawDialIntSlider(
                HotbarI18n.T("lbl.pause_dial_scale"),
                _cfg.PauseDialScalePercent,
                60,
                160,
                sliderLabelW);
            GUILayout.Space(RowGap);

            GUILayout.BeginHorizontal();
            string resetText = HotbarI18n.T("btn.pause_dial_reset");
            float resetW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ActionBtnStyle, resetText, 220f, 16f);
            if (GUILayout.Button(resetText, BlackWhiteSkin.ActionBtnStyle,
                GUILayout.Width(resetW), GUILayout.Height(ActionBtnH)))
            {
                BlackWhiteSkin.ClearTextFocus();
                _cfg.ResetPauseDialLayout();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label(
                HotbarI18n.T("hint.pause_dial_drag"),
                BlackWhiteSkin.HintLabelStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(36f));

            GUILayout.EndVertical();
        }

        private static void DrawDialFloatSlider(
            string label,
            ConfigEntry<float> entry,
            float min,
            float max,
            string valueText,
            float labelWidth)
        {
            float boxW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ValueBoxStyle, valueText, 92f, 8f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.SingleLineLabelStyle,
                GUILayout.Width(labelWidth), GUILayout.Height(RowH));
            GUILayout.Space(10f);
            // 禁止给 HorizontalSlider 传 GUILayout.Height，否则会覆盖 fixedHeight=14f 拉胖轨道
            float raw = GUILayout.HorizontalSlider(entry.Value, min, max, GUILayout.ExpandWidth(true));
            float snapped = Mathf.Clamp(Mathf.Round(raw), min, max);
            if (Math.Abs(raw - entry.Value) > 0.0001f && Math.Abs(snapped - entry.Value) > 0.0001f)
            {
                BlackWhiteSkin.ClearTextFocus();
                entry.Value = snapped;
            }
            GUILayout.Space(10f);
            Rect valSlot = ReserveRightSlot(boxW);
            GUI.Label(valSlot, valueText, BlackWhiteSkin.ValueBoxStyle);
            GUILayout.EndHorizontal();
        }

        private static void DrawDialIntSlider(
            string label,
            ConfigEntry<int> entry,
            int min,
            int max,
            float labelWidth)
        {
            string valueText = entry.Value.ToString(CultureInfo.InvariantCulture) + "%";
            float boxW = BlackWhiteSkin.CalcWidth(BlackWhiteSkin.ValueBoxStyle, valueText, 92f, 8f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, BlackWhiteSkin.SingleLineLabelStyle,
                GUILayout.Width(labelWidth), GUILayout.Height(RowH));
            GUILayout.Space(10f);
            float raw = GUILayout.HorizontalSlider(entry.Value, min, max, GUILayout.ExpandWidth(true));
            int rounded = Mathf.Clamp(Mathf.RoundToInt(raw), min, max);
            if (rounded != entry.Value)
            {
                BlackWhiteSkin.ClearTextFocus();
                entry.Value = rounded;
            }
            GUILayout.Space(10f);
            Rect valSlot = ReserveRightSlot(boxW);
            GUI.Label(valSlot, valueText, BlackWhiteSkin.ValueBoxStyle);
            GUILayout.EndHorizontal();
        }
    }
}
