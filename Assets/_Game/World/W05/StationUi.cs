using System;
using System.Collections.Generic;
using UnityEngine;

namespace LKS.World
{
    public static class StationUi
    {
        public static readonly Color Panel = new Color32(17, 23, 28, 255);
        public static readonly Color Surface = new Color32(27, 36, 43, 255);
        public static readonly Color Control = new Color32(37, 56, 65, 255);
        public static readonly Color Ink = new Color32(242, 239, 231, 255);
        public static readonly Color Muted = new Color32(185, 196, 204, 255);
        public static readonly Color Accent = new Color32(128, 216, 204, 255);
        public static readonly Color Amber = new Color32(255, 202, 120, 255);
        public static readonly Color Alert = new Color32(255, 143, 133, 255);
        public static readonly Color Edge = new Color32(57, 69, 79, 255);
        private static readonly Color Primary = new Color32(47, 83, 83, 255);

        private static GUIStyle invisibleButton;
        private static Texture2D controlTexture, hoverTexture, pressedTexture;
        private const string TextPreference = "LKS.Station.LargeText";
        private static bool? largeText;

        public static bool LargeText
        {
            get => largeText ??= PlayerPrefs.GetInt(TextPreference, 0) == 1;
            set
            {
                if (LargeText == value) return;
                largeText = value;
                PlayerPrefs.SetInt(TextPreference, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static int TextSize(int standard, int large) => LargeText ? large : standard;

        public static float HudScale(float normal, int minimumWidth, int minimumHeight)
        {
            return Mathf.Min(normal * (LargeText ? 1.26f : 1f),
                Screen.width / (float)minimumWidth, Screen.height / (float)minimumHeight);
        }

        public static void Fill(Rect area, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void Frame(Rect area, Color tone)
        {
            Fill(area, Panel);
            Fill(new Rect(area.x, area.y, area.width, 1), Edge);
            Fill(new Rect(area.x, area.y, 52, 4), tone);
            Fill(new Rect(area.x, area.yMax - 1, area.width, 1), Edge);
        }

        public static GUIStyle ButtonStyle(int size, bool wrap = false)
        {
            controlTexture ??= Pixel(Control);
            hoverTexture ??= Pixel(new Color32(50, 76, 85, 255));
            pressedTexture ??= Pixel(new Color32(61, 91, 97, 255));
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = size,
                wordWrap = wrap,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(6, 6, 4, 4)
            };
            style.normal.background = controlTexture;
            style.normal.textColor = Ink;
            style.hover.background = hoverTexture;
            style.hover.textColor = Ink;
            style.active.background = pressedTexture;
            style.active.textColor = Ink;
            style.focused.background = hoverTexture;
            style.focused.textColor = Ink;
            return style;
        }

        public static bool DrawButton(Rect area, string text, GUIStyle label, bool enabled = true,
            bool focused = false, bool primary = false)
        {
            enabled &= GUI.enabled;
            if (focused && enabled) Outline(area);
            bool hovering = enabled && area.Contains(Event.current.mousePosition);
            Fill(area, !enabled ? Panel : hovering ? Control : primary ? Primary : Surface);
            Fill(new Rect(area.x, area.y, area.width, 1), Edge);
            Fill(new Rect(area.x, area.yMax - 1, area.width, 1), Edge);
            if (primary && enabled)
                Fill(new Rect(area.x, area.y, 4, area.height), Accent);
            invisibleButton ??= new GUIStyle(GUIStyle.none);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(area, GUIContent.none, invisibleButton);
            GUI.enabled = wasEnabled;
            var old = label.normal.textColor;
            label.normal.textColor = enabled ? Ink : Muted;
            GUI.Label(area, text, label);
            label.normal.textColor = old;
            return clicked;
        }

        private static Texture2D Pixel(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        public static void Outline(Rect area)
        {
            var color = Amber;
            Fill(new Rect(area.x - 2, area.y - 2, area.width + 4, 2), color);
            Fill(new Rect(area.x - 2, area.yMax, area.width + 4, 2), color);
            Fill(new Rect(area.x - 2, area.y, 2, area.height), color);
            Fill(new Rect(area.xMax, area.y, 2, area.height), color);
        }
    }

    public sealed class StationKeyboardFocus
    {
        private readonly List<(string Id, Action Activate, Action Reveal)> controls = new();
        private string selected;

        public bool IsSelected(string id) => selected == id;

        public void Reset()
        {
            controls.Clear();
            selected = null;
        }

        public void Begin()
        {
            if (Event.current.type == EventType.Repaint) controls.Clear();
        }

        public bool Handle(Event input)
        {
            if (input.type != EventType.KeyDown || controls.Count == 0) return false;
            int index = controls.FindIndex(control => control.Id == selected);
            if (input.keyCode == KeyCode.Tab)
            {
                int next = index < 0 ? (input.shift ? controls.Count - 1 : 0) :
                    (index + (input.shift ? controls.Count - 1 : 1)) % controls.Count;
                selected = controls[next].Id;
                controls[next].Reveal?.Invoke();
            }
            else if (input.keyCode is KeyCode.Return or KeyCode.KeypadEnter && index >= 0 &&
                controls[index].Activate != null)
                controls[index].Activate();
            else return false;
            input.Use();
            return true;
        }

        public void Register(Rect area, string id, Action action, bool enabled = true, Action reveal = null)
        {
            if (Event.current.type != EventType.Repaint || !enabled) return;
            controls.Add((id, action, reveal));
            if (IsSelected(id)) StationUi.Outline(area);
        }

        public bool Button(Rect area, string label, string id, GUIStyle style, Action action, bool enabled = true)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool clicked = GUI.Button(area, label, style);
            GUI.enabled = previous;
            Register(area, id, action, previous && enabled);
            if (!clicked) return false;
            selected = id;
            action();
            return true;
        }
    }
}
