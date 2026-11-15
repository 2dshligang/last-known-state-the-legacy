using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationShelterSet
    {
        private static readonly Color Ink = new Color32(236, 242, 243, 255);
        private static readonly Color Muted = new Color32(183, 205, 212, 255);
        private static readonly Color Accent = new Color32(119, 230, 209, 255);
        private static readonly Color Notice = new Color32(255, 199, 129, 255);
        private GUIStyle title, body, heading, button;

        private void OnGUI()
        {
            if (!CanControl) return;
            EnsureStyles();
            var previous = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .7f));
            const float width = 884;
            const float height = 541;
            float scale = Mathf.Min(1.5f, Screen.width / (width + 28), Screen.height / (height + 28));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
                (Screen.height - height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                Fill(new Rect(0, 0, width, height), new Color32(16, 24, 30, 255));
                Fill(new Rect(0, 0, width, 3), Accent);
                bool shelter = page == ShelterTarget;
                Write(new Rect(24, 17, 630, 42), shelter ? "Shelter control" : "Seedling cabinet", title, Ink);
                if (GUI.Button(new Rect(716, 23, 144, 38), "Close panel", button))
                {
                    Close();
                    return;
                }
                if (shelter) DrawShelter();
                else DrawSeedlings();
                if (!string.IsNullOrEmpty(Feedback))
                    Write(new Rect(25, 487, 834, 33), Feedback, heading, feedbackSucceeded ? Accent : Notice);
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawShelter()
        {
            var support = readingSession.LifeSupport;
            var reading = support.ReadShelter();
            var air = support.Snapshot;
            string reserve = air.ReserveFraction?.ToString("P0", CultureInfo.InvariantCulture) ?? "Unavailable";
            Write(new Rect(25, 69, 834, 28), $"Air reserve {reserve}   /   {(air.Air == AirStatus.Safe ? "Safe" : "Needs support")}",
                heading, air.Air == AirStatus.Safe ? Accent : Notice);
            Card(new Rect(24, 108, 836, 155), "Training warning  /  Fiction",
                "A practice alert asks you to prepare the shelter. Thick walls are passive shielding. The motor needs power only to move the shutter. The marked side passage stays open.");
            var observation = StationLifeSupport.ArchiveEvent;
            string date = observation.RecordedAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                ?? "Time unavailable";
            Write(new Rect(39, 204, 803, 54),
                "Archived observation  /  NASA DONKI record 13000  /  " + date +
                "\nA historical solar particle event inspired the drill. The archive gives no Mars surface dose.", body, Muted);
            Write(new Rect(25, 272, 834, 29), Resolve(reading.MessageKey), heading,
                reading.Phase == ShelterPhase.Interrupted ? Notice : Ink);
            string guidance = reading.Phase switch
            {
                ShelterPhase.Waiting => "Close the panel briefly to let the training briefing arrive.",
                ShelterPhase.Ready when !reading.ActuatorPowered =>
                    "Power the actuator. If power is limited, turn off an optional load in the power bay.",
                ShelterPhase.Ready => "Request the shutter, then close the panel to let it move.",
                ShelterPhase.Moving => "Close the panel to let the shutter move. Use the marked side passage.",
                ShelterPhase.Interrupted => "Restore actuator power here. After it is supplied, close the panel to resume.",
                _ => "The shutter stays closed without power. Release the actuator load and use the marked side passage."
            };
            string actuator = reading.ActuatorPowered ? "Actuator supplied" : "Actuator has no power";
            Write(new Rect(25, 307, 824, 49), actuator + ". " + guidance, body, Muted);
            bool latched = reading.Phase == ShelterPhase.Sheltered;
            bool actuatorRequested = readingSession.Power.Snapshot.Loads.Any(load =>
                load.Id == StationPower.ShelterActuator && load.Requested);
            if (Control(new Rect(24, 377, 253, 42), latched ? "Release actuator power" : "Power shelter actuator",
                latched ? actuatorRequested : !reading.ActuatorPowered))
                SetActuatorPower(!latched);
            if (Control(new Rect(292, 377, 246, 42), "Request shutter closure",
                reading.Phase == ShelterPhase.Ready && reading.ActuatorPowered))
                RequestShelter();
            if (GUI.Button(new Rect(554, 377, 306, 42), "Open NASA DONKI catalog", button))
                Application.OpenURL("https://ccmc.gsfc.nasa.gov/DONKI/events/");
            Write(new Rect(25, 432, 834, 31), "The drill and its timing are a simplified station model.", body, Muted);
        }

        private void DrawSeedlings()
        {
            Write(new Rect(25, 69, 834, 45), "The starter seedlings came with Alexei's expedition. The cabinet preserves them for a future growing plan.",
                body, Muted);
            Card(new Rect(24, 126, 408, 177), "Preserve seedlings",
                "Keep the starter seedlings ready for future growing. The lab pauses while this plan is selected.");
            Card(new Rect(452, 126, 408, 177), "Run the laboratory",
                "Use power to analyze samples now. Seedling care is deferred until you switch back.");
            if (GUI.Button(new Rect(41, 242, 374, 42), "Preserve seedlings", button))
                Choose(FoodSchedule.PreserveSeedlings);
            if (GUI.Button(new Rect(469, 242, 374, 42), "Run the laboratory", button))
                Choose(FoodSchedule.RunLab);
            Write(new Rect(25, 322, 834, 45), FoodStatus, heading, Accent);
            Write(new Rect(25, 374, 834, 45), StationLogistics.FoodExplanation, body, Muted);
            Write(new Rect(25, 428, 834, 46),
                "If a choice is rejected, restore safe power or turn off an optional load in the power bay, then try again.",
                body, Muted);
        }

        private bool Control(Rect rect, string caption, bool enabled)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool selected = GUI.Button(rect, caption, button);
            GUI.enabled = previous;
            return selected;
        }

        private void Card(Rect rect, string caption, string message)
        {
            Fill(rect, new Color32(26, 39, 46, 255));
            Write(new Rect(rect.x + 15, rect.y + 9, rect.width - 30, 32), caption, heading, Ink);
            Write(new Rect(rect.x + 15, rect.y + 44, rect.width - 30, 65), message, body, Ink);
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 35, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.UpperLeft };
            heading = new GUIStyle(body) { fontSize = 21, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
        }

        private static void Write(Rect rect, string message, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, message, style);
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
