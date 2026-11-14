using System;
using System.Globalization;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationEchoReview : MonoBehaviour
    {
        private static readonly StableId TimelineSubject = new StableId("echo.asterion");
        private StationFirstPerson player;
        private GameplaySession openedSession;
        private Func<GameplaySession> currentSession;
        private StationEcho echo;
        private EnglishContentCatalog text;
        private IDisposable inputLease, subscription;
        private EchoProjection projection;
        private int keyboardFocus;

        internal EchoPlayback Playback { get; private set; }
        internal bool IsOpen => inputLease != null;
        internal bool ProjectionVisible => projection?.ShowsReconstruction == true;
        internal int ProjectedObjectCount => projection?.ObjectCount ?? 0;
        private bool CanControl => isActiveAndEnabled && IsOpen && !player.IsPaused &&
            player.HasApplicationFocus && ReferenceEquals(openedSession, currentSession());

        public bool Open(StationFirstPerson input, Func<GameplaySession> session)
        {
            var active = session?.Invoke();
            if (!isActiveAndEnabled || IsOpen || input == null || input.IsPaused || input.HasReadingFocus ||
                active?.Echo == null) return false;
            projection ??= new EchoProjection(transform);
            currentSession = session;
            openedSession = active;
            player = input;
            echo = active.Echo;
            text = active.Archive.Content;
            keyboardFocus = 0;
            inputLease = player.AcquireReadingFocus(Close);
            subscription = echo.Subscribe(change =>
            {
                if (change.Subject == TimelineSubject && IsOpen && ReferenceEquals(openedSession, currentSession()))
                    Present(change.Snapshot);
            });
            Present(echo.Snapshot);
            return true;
        }

        public void Close()
        {
            var lease = inputLease;
            inputLease = null;
            subscription?.Dispose();
            subscription = null;
            if (echo != null && ReferenceEquals(openedSession, currentSession()))
                echo.Stop();
            echo = null;
            openedSession = null;
            currentSession = null;
            player = null;
            Playback = default;
            projection?.Hide();
            lease?.Dispose();
        }

        private void Update()
        {
            if (IsOpen && !ReferenceEquals(openedSession, currentSession())) Close();
        }

        private void OnDisable() => Close();
        private void OnDestroy() => projection?.Dispose();

        internal bool Replay()
        {
            if (!CanControl) return false;
            openedSession.Tick();
            echo.Restart();
            Present(echo.Snapshot);
            return true;
        }

        internal bool Hold()
        {
            if (!CanControl) return false;
            openedSession.Tick();
            return SeekFrame(echo.Snapshot.Seconds);
        }

        internal bool Scrub(decimal seconds)
        {
            if (!CanControl) return false;
            openedSession.Tick();
            return SeekFrame(seconds);
        }

        private bool SeekFrame(decimal seconds)
        {
            if (echo.Snapshot.Playing) echo.Stop();
            echo.Seek(seconds);
            Present(echo.Snapshot);
            return true;
        }

        private void Present(EchoPlayback snapshot)
        {
            Playback = snapshot;
            projection.Present(snapshot.Frame);
        }

        internal string CaptionOf(EchoFrame frame) =>
            text.Resolve(frame.CaptionKey, Debug.isDebugBuild || Application.isEditor);

        internal static string LabelOf(EchoFrame frame) => frame.Evidence.Quality switch
        {
            EvidenceQuality.Recorded => "Recorded station log",
            EvidenceQuality.Estimated => "Simplified model estimate",
            EvidenceQuality.Missing => "Missing interval",
            _ => "Evidence quality unknown"
        };

        internal static string DateOf(EchoFrame frame) =>
            frame.Evidence.RecordedAt?.UtcDateTime.ToString("d MMM yyyy, HH:mm 'UTC (story time)'",
                CultureInfo.InvariantCulture) ?? "Original timestamp unknown";
    }
}
