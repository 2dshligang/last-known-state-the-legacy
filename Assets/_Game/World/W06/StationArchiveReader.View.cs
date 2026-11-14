using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationArchiveReader
    {
        private const float PanelWidth = 966;
        private const float PanelHeight = 622;
        private const float RowHeight = 104;
        private const float ListHeight = 360;
        private const float DocumentHeight = 372;
        private static readonly Color Panel = new Color32(16, 24, 30, 255);
        private static readonly Color Surface = new Color32(26, 39, 46, 255);
        private static readonly Color Control = new Color32(47, 66, 74, 255);
        private static readonly Color Ink = new Color32(242, 247, 248, 255);
        private static readonly Color Muted = new Color32(190, 212, 220, 255);
        private static readonly Color Accent = new Color32(128, 230, 208, 255);
        private static readonly Color Focus = new Color32(255, 204, 133, 255);
        private GUIStyle title, heading, body, small, centered, hitArea;
        private float documentScrollLimit;

        private void OnGUI()
        {
            if (!CanRead) return;
            EnsureStyles();
            HandleKeyboard(Event.current);
            if (!IsOpen) return;
            var previous = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .75f));
            float scale = Mathf.Min(1.55f, Screen.width / (PanelWidth + 28), Screen.height / (PanelHeight + 28));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - PanelWidth * scale) / 2,
                (Screen.height - PanelHeight * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                Fill(new Rect(0, 0, PanelWidth, PanelHeight), Panel);
                Fill(new Rect(0, 0, PanelWidth, 3), Accent);
                if (Document == null) DrawList();
                else DrawDocument(Document);
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawList()
        {
            Write(new Rect(27, 18, 725, 53), "Station archive", title, Ink);
            if (Button(new Rect(786, 26, 151, 39), "Close archive", closeFocused))
            {
                Close();
                return;
            }
            Write(new Rect(29, 80, 882, 43), "Choose a record. Open it for a short summary or expand the supporting evidence.", body, Muted);
            var entries = EnglishContentCatalog.Archive;
            const float rowWidth = 884;
            Fill(new Rect(26, 139, 914, ListHeight), Surface);
            listPosition = GUI.BeginScrollView(new Rect(29, 139, 905, ListHeight), listPosition,
                new Rect(0, 0, rowWidth, entries.Count * RowHeight), false, false);
            int clicked = -1;
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var row = new Rect(5, i * RowHeight + 4, rowWidth - 10, RowHeight - 8);
                    bool focused = !closeFocused && i == listIndex;
                    if (focused) Fill(row, Focus);
                    Fill(new Rect(row.x + 2, row.y + 2, row.width - 4, row.height - 4), Panel);
                    if (GUI.Button(row, GUIContent.none, hitArea)) clicked = i;
                    Write(new Rect(row.x + 16, row.y + 10, 686, 34), TitleOf(entry), heading, Ink);
                    Write(new Rect(row.x + 16, row.y + 49, 686, 30),
                        CategoryOf(entry) + "    /    " + DateOf(entry), small, Muted);
                    Write(new Rect(row.x + 728, row.y + 28, 136, 40),
                        WasRead(entry) ? "Read" : "Unread", centered, WasRead(entry) ? Accent : Muted);
                }
            }
            finally { GUI.EndScrollView(); }
            if (clicked >= 0) OpenDocument(clicked, listPosition);
            Write(new Rect(29, 532, 907, 50), "Up and Down select a record. Enter opens it. Tab moves between the list and Close archive.", small, Muted);
        }

        private void DrawDocument(ArchiveEntry entry)
        {
            Write(new Rect(27, 18, 725, 53), TitleOf(entry), title, Ink);
            if (Button(new Rect(786, 26, 151, 39), "Close archive", documentFocus == 3))
            {
                Close();
                return;
            }
            Write(new Rect(29, 75, 898, 28), CategoryOf(entry), body, Accent);
            Write(new Rect(29, 105, 909, 24), DateOf(entry) + "    /    " + SourcesOf(entry), small, Muted);

            var area = new Rect(26, 146, 914, DocumentHeight);
            Fill(area, Surface);
            const float textWidth = 844;
            float contents = 20;
            string summary = SummaryOf(entry);
            float summaryHeight = TextHeight(summary, body, textWidth);
            contents += 42 + summaryHeight + 22;
            string detail = Expanded ? DetailOf(entry) : null;
            if (detail != null) contents += 55 + TextHeight(detail, body, textWidth) + 23;
            documentScrollLimit = Mathf.Max(0, contents - area.height);
            documentPosition.y = Mathf.Clamp(documentPosition.y, 0, documentScrollLimit);
            documentPosition = GUI.BeginScrollView(area, documentPosition,
                new Rect(0, 0, 890, contents), false, false);
            try
            {
                Write(new Rect(24, 19, textWidth, 31), "Summary", heading, Accent);
                Write(new Rect(24, 60, textWidth, summaryHeight), summary, body, Ink);
                if (detail != null)
                {
                    float detailStart = 81 + summaryHeight;
                    Fill(new Rect(24, detailStart, textWidth, 1), Control);
                    Write(new Rect(24, detailStart + 16, textWidth, 34), "Supporting evidence", heading, Accent);
                    Write(new Rect(24, detailStart + 56, textWidth, TextHeight(detail, body, textWidth)), detail, body, Ink);
                }
            }
            finally { GUI.EndScrollView(); }

            if (Button(new Rect(27, 536, 168, 43), "Back to list", documentFocus == 0))
            {
                ReturnToList();
                return;
            }
            if (Button(new Rect(207, 536, 218, 43), Expanded ? "Hide evidence" : "Expand evidence", documentFocus == 1))
                ToggleDetails();
            bool read = WasRead(entry);
            if (Button(new Rect(437, 536, 179, 43), read ? "Marked as read" : "Mark as read",
                documentFocus == 2, !read))
                MarkAsRead();
            Write(new Rect(635, 545, 297, 35), Feedback ?? string.Empty, small, Accent);
            Write(new Rect(29, 588, 902, 25), "Tab and Enter select actions. Use the scroll wheel, arrow keys or Page Up and Page Down to read.", small, Muted);
        }

        internal void HandleKeyboard(Event input)
        {
            if (!CanRead || input.type != EventType.KeyDown) return;
            if (input.keyCode == KeyCode.Tab)
            {
                if (Document == null) closeFocused = !closeFocused;
                else documentFocus = (documentFocus + (input.shift ? 3 : 1)) % 4;
                input.Use();
                return;
            }
            if (input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter)
            {
                input.Use();
                if (Document == null)
                {
                    if (closeFocused) Close();
                    else OpenDocument(listIndex, listPosition);
                }
                else if (documentFocus == 0) ReturnToList();
                else if (documentFocus == 1) ToggleDetails();
                else if (documentFocus == 2 && !WasRead(Document)) MarkAsRead();
                else if (documentFocus == 3) Close();
                return;
            }
            if (Document == null) MoveListFocus(input);
            else ScrollDocument(input);
        }

        private void MoveListFocus(Event input)
        {
            int last = EnglishContentCatalog.Archive.Count - 1;
            int? next = input.keyCode switch
            {
                KeyCode.UpArrow => listIndex - 1,
                KeyCode.DownArrow => listIndex + 1,
                KeyCode.PageUp => listIndex - 3,
                KeyCode.PageDown => listIndex + 3,
                KeyCode.Home => 0,
                KeyCode.End => last,
                _ => null
            };
            if (!next.HasValue) return;
            input.Use();
            listIndex = Mathf.Clamp(next.Value, 0, last);
            closeFocused = false;
            float top = listIndex * RowHeight;
            listPosition.y = Mathf.Clamp(listPosition.y, Mathf.Max(0, top + RowHeight - ListHeight), top);
        }

        private void ScrollDocument(Event input)
        {
            float next = input.keyCode switch
            {
                KeyCode.UpArrow => documentPosition.y - 28,
                KeyCode.DownArrow => documentPosition.y + 28,
                KeyCode.PageUp => documentPosition.y - DocumentHeight * .85f,
                KeyCode.PageDown => documentPosition.y + DocumentHeight * .85f,
                KeyCode.Home => 0,
                KeyCode.End => documentScrollLimit,
                _ => float.NaN
            };
            if (float.IsNaN(next)) return;
            input.Use();
            documentPosition.y = Mathf.Clamp(next, 0, documentScrollLimit);
        }

        private bool Button(Rect rect, string label, bool focused, bool enabled = true)
        {
            if (focused) Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Focus);
            Fill(rect, enabled ? Control : Panel);
            var previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, hitArea);
            GUI.enabled = previous;
            Write(rect, label, centered, enabled ? Ink : Muted);
            return clicked;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 18 };
            centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hitArea = new GUIStyle(GUIStyle.none);
        }

        private static float TextHeight(string value, GUIStyle style, float width) =>
            style.CalcHeight(new GUIContent(value), width) + 7;

        private static void Write(Rect rect, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
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
