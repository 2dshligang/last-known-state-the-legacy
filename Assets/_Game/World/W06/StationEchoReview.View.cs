using System.Globalization;
using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationEchoReview
    {
        private const float PanelWidth = 1112;
        private const float PanelHeight = 676;
        private static readonly Color Panel = new Color32(14, 23, 29, 255);
        private static readonly Color Surface = new Color32(24, 38, 48, 255);
        private static readonly Color Control = new Color32(48, 67, 77, 255);
        private static readonly Color Ink = new Color32(240, 248, 249, 255);
        private static readonly Color Muted = new Color32(187, 207, 217, 255);
        private static readonly Color Accent = new Color32(111, 227, 219, 255);
        private static readonly Color Estimate = new Color32(255, 202, 122, 255);
        private static readonly Color Gap = new Color32(205, 197, 235, 255);
        private GUIStyle title, heading, label, centered, body, hitArea;

        private void OnGUI()
        {
            if (!CanControl) return;
            EnsureStyles();
            HandleKeyboard(Event.current);
            if (!IsOpen) return;
            float scale = Mathf.Min(1.5f, Screen.width / (PanelWidth + 28), Screen.height / (PanelHeight + 28));
            var old = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .76f));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - PanelWidth * scale) / 2,
                (Screen.height - PanelHeight * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try { Draw(); }
            finally { GUI.matrix = old; }
        }

        private void Draw()
        {
            int replayIndex = StationEcho.Timeline.Count;
            int holdIndex = replayIndex + 1;
            int exitIndex = holdIndex + 1;
            int investigationIndex = exitIndex + 1;
            Fill(new Rect(0, 0, PanelWidth, PanelHeight), Panel);
            Fill(new Rect(0, 0, PanelWidth, 3), Accent);
            Write(new Rect(24, 15, 784, 49), "ECHO reconstruction", title, Ink);
            string date = StationEcho.Timeline[0].Evidence.RecordedAt?.UtcDateTime.ToString("d MMMM yyyy",
                CultureInfo.InvariantCulture);
            Write(new Rect(26, 66, 806, 27), "Fictional ASTERION incident" +
                (date == null ? "" : "  /  " + date), body, Muted);
            if (Button(new Rect(927, 27, 160, 42), "Exit replay", exitIndex))
            {
                Close();
                return;
            }
            if (Button(new Rect(688, 27, 222, 42), "Investigate incident", investigationIndex, CanInvestigate)
                && OpenInvestigation()) return;
            if (Playback.Frame == null) return;
            DrawProjection(Playback.Frame);
            DrawRecords();
            DrawControls(replayIndex, holdIndex);
        }

        private void DrawProjection(EchoFrame frame)
        {
            var preview = new Rect(24, 104, 673, 349);
            Fill(preview, Surface);
            if (ProjectionVisible && projection.Texture != null)
                GUI.DrawTexture(preview, projection.Texture, ScaleMode.ScaleToFit, false);
            else if (ProjectionVisible)
                Write(new Rect(120, 240, 480, 50), "Projection unavailable", centered, Muted);
            else
            {
                Fill(new Rect(75, 276, 135, 2), Gap);
                Fill(new Rect(512, 276, 135, 2), Gap);
                Write(new Rect(218, 247, 288, 59), "Missing interval", centered, Gap);
                Write(new Rect(121, 307, 506, 29), "No reconstruction is available for this part of the record.", body, Muted);
            }
            var tone = Tone(frame);
            Fill(new Rect(24, 465, 673, 90), Surface);
            Fill(new Rect(24, 465, 3, 90), tone);
            Write(new Rect(39, 476, 642, 29), LabelOf(frame) + "  /  " + DateOf(frame), label, tone);
            Write(new Rect(39, 513, 642, 35), CaptionOf(frame), heading, Ink);
        }

        private void DrawRecords()
        {
            Fill(new Rect(711, 104, 377, 451), Surface);
            Write(new Rect(725, 116, 345, 32), "Timeline records", heading, Ink);
            for (int i = 0; i < StationEcho.Timeline.Count; i++)
            {
                EchoFrame record = StationEcho.Timeline[i];
                var tone = Tone(record);
                var rect = new Rect(725, 158 + i * 77, 348, 70);
                if (i == keyboardFocus) Fill(rect, Estimate);
                Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4),
                    Playback.Frame.Sequence == record.Sequence ? Control : Panel);
                if (GUI.Button(rect, GUIContent.none, hitArea))
                {
                    keyboardFocus = i;
                    Scrub(record.ReplaySecond);
                }
                Write(new Rect(rect.x + 12, rect.y + 8, 323, 25),
                    Elapsed(record.ReplaySecond) + "  /  " + LabelOf(record), label, tone);
                Write(new Rect(rect.x + 12, rect.y + 30, 321, 40), CaptionOf(record), label, Ink);
            }
        }

        private void DrawControls(int replay, int hold)
        {
            Write(new Rect(24, 567, 168, 28),
                Elapsed(Playback.Seconds) + " / " + Elapsed(StationEcho.DurationSeconds), label, Ink);
            float position = (float)Playback.Seconds;
            float changed = GUI.HorizontalSlider(new Rect(173, 577, 690, 18), position, 0, StationEcho.DurationSeconds);
            if (Mathf.Abs(changed - position) > .001f) Scrub((decimal)changed);
            var records = StationEcho.Timeline;
            for (int i = 0; i < records.Count; i++)
            {
                int finish = i + 1 < records.Count ? records[i + 1].ReplaySecond : StationEcho.DurationSeconds;
                int duration = finish - records[i].ReplaySecond;
                if (duration == 0) continue;
                const float units = 690f / StationEcho.DurationSeconds;
                Fill(new Rect(173 + records[i].ReplaySecond * units, 599, duration * units - 2, 4),
                    Tone(records[i]));
            }
            Write(new Rect(890, 568, 193, 27), Playback.Playing ? "Playing" : "Frame held", label, Muted);
            if (Button(new Rect(24, 617, 204, 39), "Replay from start", replay)) Replay();
            if (Button(new Rect(241, 617, 156, 39), "Hold frame", hold, Playback.Playing)) Hold();
            Write(new Rect(419, 621, 666, 42),
                "Left and Right scrub the replay. Tab and Enter select controls. Escape pauses.", label, Muted);
        }

        internal void HandleKeyboard(Event input)
        {
            if (!CanControl || input.type != EventType.KeyDown) return;
            int replay = StationEcho.Timeline.Count;
            int hold = replay + 1;
            int exit = hold + 1;
            int investigate = exit + 1;
            switch (input.keyCode)
            {
                case KeyCode.LeftArrow: Scrub(Playback.Seconds - 1); break;
                case KeyCode.RightArrow: Scrub(Playback.Seconds + 1); break;
                case KeyCode.Home: Scrub(0); break;
                case KeyCode.End: Scrub(StationEcho.DurationSeconds); break;
                case KeyCode.Tab:
                    int direction = input.shift ? -1 : 1;
                    int count = investigate + 1;
                    do { keyboardFocus = (keyboardFocus + direction + count) % count; }
                    while ((keyboardFocus == hold && !Playback.Playing) ||
                           (keyboardFocus == investigate && !CanInvestigate));
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (keyboardFocus < replay) Scrub(StationEcho.Timeline[keyboardFocus].ReplaySecond);
                    else if (keyboardFocus == replay) Replay();
                    else if (keyboardFocus == hold && Playback.Playing) Hold();
                    else if (keyboardFocus == exit) Close();
                    else if (keyboardFocus == investigate) OpenInvestigation();
                    break;
                default: return;
            }
            input.Use();
        }

        private bool Button(Rect rect, string text, int index, bool enabled = true)
        {
            if (index == keyboardFocus)
                Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Estimate);
            Fill(rect, enabled ? Control : Surface);
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, hitArea);
            GUI.enabled = old;
            Write(rect, text, centered, enabled ? Ink : Muted);
            if (clicked) keyboardFocus = index;
            return clicked;
        }

        private static Color Tone(EchoFrame frame) => frame.Evidence.Quality switch
        {
            EvidenceQuality.Recorded => Accent,
            EvidenceQuality.Estimated => Estimate,
            _ => Gap
        };

        private static string Elapsed(decimal time) => time.ToString("00.0", CultureInfo.InvariantCulture) + " s";

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            label = new GUIStyle(body) { fontSize = 16 };
            centered = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hitArea = new GUIStyle(GUIStyle.none);
        }

        private static void Write(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
