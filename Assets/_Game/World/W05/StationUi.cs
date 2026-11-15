using System;
using System.Collections.Generic;
using UnityEngine;

namespace LKS.World
{
    public static class StationUi
    {
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

        public static void Outline(Rect area)
        {
            var color = new Color32(255, 216, 148, 255);
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
