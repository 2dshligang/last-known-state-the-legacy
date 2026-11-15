using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationPowerConsole
    {
        private static readonly Color Frame = StationUi.Panel;
        private static readonly Color Surface = StationUi.Surface;
        private static readonly Color Track = StationUi.Control;
        private static readonly Color Ink = StationUi.Ink;
        private static readonly Color Muted = StationUi.Muted;
        private static readonly Color Accent = StationUi.Accent;
        private static readonly Color Warning = StationUi.Amber;
        private GUIStyle headline, numeric, label, detail, button, buttonText;

        private void OnGUI()
        {
            if (!CanEdit) return;
            EnsureStyles();
            var previous = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .72f));
            const float width = 964;
            const float height = 645;
            float scale = Mathf.Min(1.55f, Screen.width / (width + 28), Screen.height / (height + 28));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
                (Screen.height - height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try { DrawContents(); }
            finally { GUI.matrix = previous; }
        }

        private void DrawContents()
        {
            StationUi.Frame(new Rect(0, 0, 964, 645), Accent);
            Write(new Rect(24, 17, 660, 42), "Station power", headline, Ink);
            Write(new Rect(25, 65, 780, 24), "Simplified game model — fictional station values", detail, Muted);

            int applyIndex = Live.Loads.Count;
            int confirmIndex = applyIndex + 1;
            int closeIndex = confirmIndex + 1;
            bool ready = TryReadConfirmation(out _, out var state) && state.Available;
            HandleKeyboard(applyIndex, confirmIndex, closeIndex, ready);
            if (!IsOpen) return;
            if (Button(new Rect(802, 29, 138, 36), "Close panel", closeIndex))
            {
                Close();
                return;
            }

            Metric(24, 228, "Available generation", Units(Live.GenerationKilowatts, "kW"));
            Metric(264, 212, "Requested now", Units(Live.DemandKilowatts, "kW"));
            Metric(488, 212, "Proposed demand", Units(Proposed.DemandKilowatts, "kW"));
            Metric(712, 228, "Battery stored / full", BatteryText(Live));
            float fraction = Live.CapacityKilowattHours > 0 ?
                Mathf.Clamp01((float)(Live.BatteryKilowattHours / Live.CapacityKilowattHours)) : 0;
            Fill(new Rect(724, 176, 204, 4), Track);
            Fill(new Rect(724, 176, 204 * fraction, 4), fraction > .1f ? Accent : Warning);
            Write(new Rect(25, 192, 914, 25),
                "kW is how fast power is made or used.   kWh is how much energy the battery stores.", detail, Muted);

            DrawLoads();
            DrawConsequences(ready, state);
            Fill(new Rect(24, 572, 916, 1), Track);
            Button(new Rect(24, 586, 208, 38), "Apply allocation", applyIndex, CanApply, true, () => Apply());
            Button(new Rect(246, 586, 244, 38), "Confirm safe life support", confirmIndex, ready, false,
                () => ConfirmLifeSupport());
            Write(new Rect(507, 585, 432, 42), Feedback ?? "Tab to move between controls. Enter to select.", detail,
                Feedback == null ? Muted : Ink);
        }

        private void DrawLoads()
        {
            Fill(new Rect(24, 230, 548, 328), Surface);
            Write(new Rect(39, 239, 220, 24), "Equipment", label, Ink);
            Write(new Rect(280, 240, 94, 24), "Live status", detail, Muted);
            Write(new Rect(397, 240, 175, 24), "Proposed status", detail, Muted);
            for (int i = 0; i < Live.Loads.Count; i++)
            {
                LoadState live = Live.Loads[i];
                LoadState proposed = Proposed.Loads[i];
                float y = 270 + i * 39;
                Fill(new Rect(39, y, 518, 1), Track);
                Write(new Rect(39, y + 1, 230, 36), LoadName(live.Id), label, Ink);
                Write(new Rect(280, y + 1, 105, 36), Status(live), detail, StatusColor(live));
                if (Button(new Rect(396, y + 4, 160, 31), Status(proposed), i, true, proposed.Requested,
                    warning: proposed.Requested && !proposed.Supplied))
                    Toggle(live.Id);
            }
        }

        private void DrawConsequences(bool ready, InteractionState state)
        {
            Fill(new Rect(586, 230, 354, 328), Surface);
            Write(new Rect(601, 239, 320, 26), "If you apply this selection", label, Ink);
            Write(new Rect(601, 274, 320, 76), Explanation, detail, PreviewResult.Succeeded ? Ink : Warning, true);
            Fill(new Rect(601, 357, 324, 1), Track);
            string faults = string.Join(", ", Live.Loads.Where(load => load.Requested && !load.Supplied)
                .Select(load => LoadName(load.Id)));
            string powerNotice = faults.Length == 0 ? "No interruptions to selected equipment." : "Power interrupted: " + faults;
            if (Live.BatteryKilowattHours == 0)
                powerNotice = faults.Length == 0 ? "Battery empty. The generator is supplying the station." :
                    powerNotice + ". The battery is empty.";
            Write(new Rect(601, 366, 324, 57), powerNotice, label, faults.Length > 0 ? Warning : Ink, true);
            Write(new Rect(601, 428, 324, 59), RecoveryHint, detail, Muted, true);
            string confirmation = ready ? "Air and water are safe. Confirm to continue." : state.PromptKey.value switch
            {
                "quest.result.completed" => "Safe life support confirmed.",
                "quest.result.resources" when interaction.HasDoorPower =>
                    "Close the panel to let the air reserve recover.",
                "quest.result.resources" => "Supply both air and water before confirming.",
                _ => "Safe life support confirmation is unavailable."
            };
            Write(new Rect(601, 498, 324, 48), confirmation, detail, ready ? Accent : Muted, true);
        }

        private void HandleKeyboard(int apply, int confirm, int close, bool ready)
        {
            var input = Event.current;
            if (input.type != EventType.KeyDown) return;
            if (input.keyCode == KeyCode.Tab)
            {
                int direction = input.shift ? -1 : 1;
                int next = keyboardFocus < 0 ? (input.shift ? close : 0) : keyboardFocus;
                if (keyboardFocus >= 0)
                {
                    do { next = (next + direction + close + 1) % (close + 1); }
                    while ((next == apply && !CanApply) || (next == confirm && !ready));
                }
                keyboardFocus = next;
                input.Use();
                return;
            }
            if (keyboardFocus < 0 || (input.keyCode != KeyCode.Return && input.keyCode != KeyCode.KeypadEnter)) return;
            input.Use();
            if (keyboardFocus < apply) Toggle(Live.Loads[keyboardFocus].Id);
            else if (keyboardFocus == apply) Apply();
            else if (keyboardFocus == confirm && ready) ConfirmLifeSupport();
            else if (keyboardFocus == close) Close();
        }

        private void Metric(float x, float width, string title, string value)
        {
            Fill(new Rect(x, 106, width, 82), Surface);
            Write(new Rect(x + 12, 113, width - 24, 22), title, detail, Muted);
            Write(new Rect(x + 12, 133, width - 24, 39), value, numeric, Ink);
        }

        private bool Button(Rect rect, string caption, int index, bool enabled = true, bool accent = false,
            Action activate = null, bool warning = false)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            var color = enabled && accent ? (warning ? Warning : Accent) : enabled ? Track : Frame;
            if (enabled && hovered) color = Color.Lerp(color, Ink, .15f);
            if (keyboardFocus == index)
                Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Warning);
            Fill(rect, color);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, button);
            GUI.enabled = wasEnabled;
            Write(rect, caption, buttonText, enabled && accent ? Frame : enabled ? Ink : Muted);
            if (clicked)
            {
                keyboardFocus = index;
                activate?.Invoke();
            }
            return clicked;
        }

        private void EnsureStyles()
        {
            if (headline != null) return;
            headline = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            numeric = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detail = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            button = new GUIStyle(GUIStyle.none);
            buttonText = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        }

        private static void Write(Rect rect, string text, GUIStyle style, Color color, bool wrap = false)
        {
            style.normal.textColor = color;
            style.wordWrap = wrap;
            GUI.Label(rect, text, style);
        }

        private static void Fill(Rect rect, Color color) => StationUi.Fill(rect, color);

        private static string Units(double value, string unit) =>
            value.ToString("0.##", CultureInfo.InvariantCulture) + " " + unit;

        private static string BatteryText(PowerSnapshot snapshot)
        {
            string stored = snapshot.BatteryKilowattHours is > 0 and < .01 ? "<0.01" :
                snapshot.BatteryKilowattHours.ToString("0.##", CultureInfo.InvariantCulture);
            return stored + " / " + Units(snapshot.CapacityKilowattHours, "kWh");
        }

        private static string Status(LoadState load) => load.Supplied ? "On" : load.Requested ? "No power" : "Off";
        private static Color StatusColor(LoadState load) => load.Supplied ? Accent : load.Requested ? Warning : Muted;
        private static string LoadName(StableId id) => id.value switch
        {
            "load.air" => "Air circulation",
            "load.water" => "Water supply",
            "load.seedling-preservation" => "Seedling preservation",
            "load.archive" => "ECHO archive",
            "load.lab" => "Laboratory",
            "load.shelter-actuator" => "Shelter door",
            "load.transmit" => "Transmitter",
            _ => id.value
        };
    }
}
