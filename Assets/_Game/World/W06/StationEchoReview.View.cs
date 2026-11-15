using System.Globalization;
using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationEchoReview
    {
        private const float PanelWidth = 940;
        private const float PanelHeight = 662;
        private static readonly Color Panel = StationUi.Panel;
        private static readonly Color Surface = StationUi.Surface;
        private static readonly Color Control = StationUi.Control;
        private static readonly Color Ink = StationUi.Ink;
        private static readonly Color Muted = StationUi.Muted;
        private static readonly Color Accent = StationUi.Accent;
        private static readonly Color Estimate = StationUi.Amber;
        private static readonly Color Gap = new Color32(205, 197, 235, 255);
        private GUIStyle title, heading, label, centered, body, hitArea;
        private EchoFrame inspectedRecord;

        private void OnGUI()
        {
            if (!CanControl) return;
            EnsureStyles();
            body.fontSize = StationUi.TextSize(19, 22);
            label.fontSize = StationUi.TextSize(16, 18);
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
            StationUi.Frame(new Rect(0, 0, PanelWidth, PanelHeight), Accent);
            Write(new Rect(24, 14, 650, 28), "ECHO ARCHIVE  /  PLAYBACK", label, Accent);
            Write(new Rect(22, 45, 650, 52), "Review the incident", title, Ink);
            if (Button(new Rect(742, 37, 173, 47), "Exit replay", exitIndex))
            {
                Close();
                return;
            }
            if (Playback.Frame == null) return;
            DrawProjection(inspectedRecord ?? Playback.Frame);
            DrawRecords();
            DrawControls(replayIndex, holdIndex, investigationIndex);
        }

        private void DrawProjection(EchoFrame frame)
        {
            var preview = new Rect(24, 108, 526, 295);
            Fill(preview, Surface);
            if (frame.Sequence != Playback.Frame.Sequence)
                Write(preview, "Source note  /  Shares this timestamp with another record", centered, Muted);
            else if (ProjectionVisible && projection.Texture != null)
                GUI.DrawTexture(preview, projection.Texture, ScaleMode.ScaleToFit, false);
            else if (ProjectionVisible)
                Write(preview, "Projection unavailable", centered, Muted);
            else
            {
                Write(new Rect(44, 189, 486, 45), "NO DATA  /  Missing interval", centered, Gap);
                Write(new Rect(76, 248, 432, 62),
                    "There is no station record for this part. It cannot tell us what happened.", body, Ink);
            }

            var tone = Tone(frame);
            Fill(new Rect(24, 413, 526, 112), Surface);
            Fill(new Rect(24, 413, 4, 112), tone);
            Write(new Rect(38, 419, 490, 25), MarkOf(frame) + "  " + LabelOf(frame), label, tone);
            Write(new Rect(38, 445, 490, 25), DateOf(frame), label, Muted);
            Write(new Rect(38, 471, 490, 51), CaptionOf(frame), body, Ink);
            StationTextCapture.Record("echo.playback", LabelOf(frame) + " / " + DateOf(frame) + " / " + CaptionOf(frame));
        }

        private void DrawRecords()
        {
            Fill(new Rect(566, 108, 349, 417), Surface);
            Write(new Rect(579, 116, 317, 30), "Jump to a record", heading, Ink);
            for (int i = 0; i < StationEcho.Timeline.Count; i++)
            {
                EchoFrame record = StationEcho.Timeline[i];
                var tone = Tone(record);
                var rect = new Rect(580, 155 + i * 71, 320, 65);
                bool selected = (inspectedRecord ?? Playback.Frame).Sequence == record.Sequence;
                Fill(rect, selected ? Control : Panel);
                if (i == keyboardFocus) StationUi.Outline(rect);
                if (selected) Fill(new Rect(rect.x, rect.y, 4, rect.height), tone);
                if (GUI.Button(rect, GUIContent.none, hitArea))
                {
                    keyboardFocus = i;
                    InspectRecord(record);
                }
                Write(new Rect(rect.x + 11, rect.y + 4, 302, 23),
                    MarkOf(record) + "  " + Elapsed(record.ReplaySecond) + (selected ? "  /  Selected" : ""), label, tone);
                Write(new Rect(rect.x + 11, rect.y + 30, 300, 30), LabelOf(record), label, Ink);
            }
        }

        private void DrawControls(int replay, int hold, int investigate)
        {
            string clock = Elapsed(Playback.Seconds) + " / " + Elapsed(StationEcho.DurationSeconds);
            Write(new Rect(24, 532, 195, 26), clock, label, Ink);
            Write(new Rect(743, 532, 173, 26), Playback.Playing ? "Playing" : "Paused", label, Muted);
            float position = (float)Playback.Seconds;
            float changed = GUI.HorizontalSlider(new Rect(224, 539, 510, 18), position, 0, StationEcho.DurationSeconds);
            if (Mathf.Abs(changed - position) > .001f) Scrub((decimal)changed);
            if (Button(new Rect(24, 573, 193, 43), "Replay from start", replay)) Replay();
            if (Button(new Rect(225, 573, 134, 43), "Pause", hold, Playback.Playing)) Hold();
            if (Button(new Rect(667, 573, 248, 43), "Investigate incident", investigate, CanInvestigate))
                OpenInvestigation();
            Write(new Rect(24, 624, 895, 30), "Left / Right: seek    |    Tab: focus    |    Enter: select    |    Esc: pause game", label, Muted);
        }

        private void InspectRecord(EchoFrame record)
        {
            if (Scrub(record.ReplaySecond))
                inspectedRecord = record.Sequence == Playback.Frame.Sequence ? null : record;
        }

        private static string MarkOf(EchoFrame frame) => frame.Evidence.Quality switch
        {
            EvidenceQuality.Recorded => "[LOG]",
            EvidenceQuality.Estimated => "[MODEL]",
            _ => "[GAP]"
        };

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
                    if (keyboardFocus < replay) InspectRecord(StationEcho.Timeline[keyboardFocus]);
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
            bool clicked = StationUi.DrawButton(rect, text, centered, enabled, index == keyboardFocus,
                index == StationEcho.Timeline.Count + 3);
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

        private static void Fill(Rect rect, Color color) => StationUi.Fill(rect, color);
    }
}
