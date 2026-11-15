using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationArchiveReader : MonoBehaviour
    {
        public static readonly StableId Target = new StableId("quest.echo-evidence");

        private StationFirstPerson player;
        private Func<GameplaySession> currentSession;
        private GameplaySession openedSession;
        private StationArchive archive;
        private IDisposable inputLease, archiveLease;
        private Vector2 listPosition, documentPosition;
        private int listIndex, documentFocus;
        private bool closeFocused;
        private StableId[] acknowledged = Array.Empty<StableId>();

        internal ArchiveEntry Document { get; private set; }
        internal string Feedback { get; private set; }
        internal bool Expanded { get; private set; }
        internal bool IsOpen => inputLease != null;
        internal Vector2 ListPosition => listPosition;
        internal int SelectedIndex => listIndex;
        private bool CanRead => isActiveAndEnabled && IsOpen && player.HasApplicationFocus &&
            !player.IsPaused && ReferenceEquals(openedSession, currentSession());

        internal bool HasArchive(GameplaySession session) => isActiveAndEnabled && session?.Archive != null;

        public bool Open(StationFirstPerson input, Func<GameplaySession> session)
        {
            var active = session?.Invoke();
            if (IsOpen || input == null || !input.isActiveAndEnabled || input.IsPaused || input.HasReadingFocus ||
                !HasArchive(active)) return false;
            player = input;
            currentSession = session;
            openedSession = active;
            archive = active.Archive;
            acknowledged = archive.Capture().ToArray();
            listIndex = 0;
            listPosition = Vector2.zero;
            closeFocused = false;
            inputLease = player.AcquireReadingFocus(Close);
            return true;
        }

        public void Close()
        {
            CloseDocument();
            var lease = inputLease;
            inputLease = null;
            openedSession = null;
            currentSession = null;
            player = null;
            archive = null;
            lease?.Dispose();
        }

        private void OnDisable() => Close();

        private void Update()
        {
            if (IsOpen && !ReferenceEquals(openedSession, currentSession())) Close();
        }

        internal bool OpenDocument(int index, Vector2 currentPosition)
        {
            if (!CanRead || Document != null || index < 0 || index >= EnglishContentCatalog.Archive.Count)
                return false;
            var next = EnglishContentCatalog.Archive[index];
            archiveLease = archive.Open(next.Id);
            listIndex = index;
            listPosition = currentPosition;
            documentFocus = 0;
            Document = next;
            return true;
        }

        internal bool ReturnToList()
        {
            if (!CanRead || Document == null) return false;
            CloseDocument();
            closeFocused = false;
            return true;
        }

        private void CloseDocument()
        {
            archiveLease?.Dispose();
            archiveLease = null;
            Expanded = false;
            Document = null;
            Feedback = null;
            documentPosition = Vector2.zero;
        }

        internal bool ToggleDetails()
        {
            if (!CanRead || Document == null) return false;
            Expanded = !Expanded;
            return true;
        }

        internal bool MarkAsRead()
        {
            if (!CanRead || Document == null) return false;
            var result = archive.Acknowledge(Document.Id);
            Feedback = Resolve(result.MessageKey);
            acknowledged = archive.Capture().ToArray();
            return result.Succeeded;
        }

        internal bool WasRead(ArchiveEntry entry) => Array.IndexOf(acknowledged, entry.Id) >= 0;
        internal string SummaryOf(ArchiveEntry entry) => Resolve(entry.SummaryKey);
        internal string DetailOf(ArchiveEntry entry) => Resolve(entry.DetailKey);
        private string Resolve(StableId key) => archive.Content.Resolve(key, Debug.isDebugBuild || Application.isEditor);

        internal static string TitleOf(ArchiveEntry entry) => entry.Id.value switch
        {
            "archive.donki-sep" => "Solar particle observation",
            "archive.power-model" => "How station power works",
            "archive.dmitry-notes" => "Dmitry's research notes",
            "archive.arrival" => "Alexei's arrival",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(entry.Id.value
                .Replace("archive.", "").Replace('-', ' ').Replace('.', ' '))
        };

        internal static string CategoryOf(ArchiveEntry entry)
        {
            string kind = entry.Kind switch
            {
                EvidenceKind.Observation => entry.Sources.Any(source => source.value == "nasa-donki-sep-2017")
                    ? "Archived NASA data" : "Recorded observation",
                EvidenceKind.EngineeringModel => "Simplified model",
                EvidenceKind.Fiction => "Fictional station record",
                _ => "Evidence type unknown"
            };
            return entry.IsFictional && entry.Kind != EvidenceKind.Fiction
                ? kind + "  /  Fictional station values" : kind;
        }

        internal static string DateOf(ArchiveEntry entry)
        {
            if (entry.IsFictional)
                return "Station date (fiction): " + entry.Date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            string label = entry.Kind == EvidenceKind.Observation ? "Observation date: " : "Record date: ";
            return label + entry.Date.UtcDateTime.ToString("d MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }

        internal static string SourcesOf(ArchiveEntry entry) => entry.Sources.Count == 0
            ? "Source: ASTERION station story archive"
            : "Source: " + string.Join(", ", entry.Sources.Select(source => source.value switch
            {
                "nasa-donki-sep-2017" => "NASA DONKI archive, record 13000",
                "asterion-prototype-v1" => "ASTERION engineering model v1",
                _ => source.value
            }));
    }
}
