using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationEchoInvestigation : MonoBehaviour
    {
        private static readonly StableId EchoStage = new StableId("quest.echo-evidence");
        private static readonly StableId BackupEstimate = new StableId("echo.backup-estimate");
        private static readonly StableId MissingInterval = new StableId("echo.missing-interval");
        private static readonly StableId FreshnessRule = new StableId("echo.freshness.rule");

        // The evidence list is alphabetical. S09 evaluates the arrangement submitted by the player.
        private static readonly IReadOnlyList<StableId> Clues = Array.AsReadOnly(new[]
        {
            EchoInvestigation.Consequence, EchoInvestigation.Decision, EchoInvestigation.Timing
        });
        private readonly List<StableId> draft = new List<StableId>();
        private StationFirstPerson player;
        private GameplaySession openedSession;
        private Func<GameplaySession> currentSession;
        private IDisposable inputLease;
        private int keyboardFocus;

        internal bool IsOpen => inputLease != null;
        internal StableId Source { get; private set; }
        internal string Feedback { get; private set; }
        internal bool FeedbackSucceeded { get; private set; }
        internal IReadOnlyList<StableId> Draft => draft;
        private EchoInvestigation Investigation => openedSession.Investigation;
        internal EchoInvestigationCheckpoint Checkpoint { get; private set; }
        private bool QuestCompleted => openedSession.Quests?.Snapshot.TryGetValue(EchoStage, out var state) == true &&
            state == QuestStatus.Completed;
        private bool CanControl => isActiveAndEnabled && IsOpen && !player.IsPaused &&
            player.HasApplicationFocus && ReferenceEquals(openedSession, currentSession());

        internal bool HasInvestigation(GameplaySession session) => isActiveAndEnabled && !IsOpen &&
            session?.Investigation != null && session.Archive != null;

        public bool Open(StationFirstPerson input, Func<GameplaySession> session)
        {
            var active = session?.Invoke();
            if (input == null || !input.isActiveAndEnabled || input.IsPaused || input.HasReadingFocus ||
                !HasInvestigation(active)) return false;
            openedSession = active;
            currentSession = session;
            player = input;
            keyboardFocus = 0;
            inputLease = player.AcquireReadingFocus(Close);
            Checkpoint = Investigation.Capture();
            draft.AddRange(Checkpoint.AcceptedOrder);
            return true;
        }

        public void Close()
        {
            var lease = inputLease;
            inputLease = null;
            openedSession = null;
            currentSession = null;
            player = null;
            Source = default;
            Feedback = null;
            Checkpoint = null;
            draft.Clear();
            lease?.Dispose();
        }

        private void ExitToStation()
        {
            var input = player;
            Close();
            if (!input.HasWorldFocus) input.ResumePointer();
        }

        private void OnDisable() => Close();

        private void Update()
        {
            if (IsOpen && !ReferenceEquals(openedSession, currentSession())) Close();
        }

        internal bool Inspect(StableId clue)
        {
            if (!CanControl || Source.IsValid || !Clues.Contains(clue)) return false;
            var result = Investigation.Collect(clue);
            Show(result);
            Checkpoint = Investigation.Capture();
            Source = clue;
            exitFromSource = false;
            return true;
        }

        internal bool InspectContext()
        {
            if (!CanControl || Source.IsValid) return false;
            Source = BackupEstimate;
            exitFromSource = false;
            return true;
        }

        internal bool ReturnToComparison()
        {
            if (!CanControl || !Source.IsValid) return false;
            Source = default;
            return true;
        }

        internal bool ToggleOrder(StableId clue)
        {
            if (!CanControl || Source.IsValid || Investigation.Conclusion == EchoConclusion.Confirmed ||
                !Checkpoint.Collected.Contains(clue)) return false;
            if (!draft.Remove(clue)) draft.Add(clue);
            Feedback = null;
            return true;
        }

        internal bool SubmitOrder()
        {
            if (!CanControl || Source.IsValid) return false;
            var result = Investigation.SubmitSequence(draft);
            Show(result);
            Checkpoint = Investigation.Capture();
            return result.Succeeded;
        }

        internal bool ConfirmQuest()
        {
            if (!CanControl || Source.IsValid) return false;
            if (!openedSession.TryGetTarget(EchoStage, out var target))
            {
                Feedback = Resolve(new StableId("quest.result.unavailable"));
                FeedbackSucceeded = false;
                return false;
            }
            var state = target.Read(StationQuests.Player);
            if (!state.Available)
            {
                Feedback = Resolve(state.PromptKey);
                FeedbackSucceeded = false;
                return false;
            }
            var result = target.Request(StationQuests.Player, state.PromptKey);
            Show(result);
            return result.Succeeded;
        }

        private void Show(CommandResult result)
        {
            Feedback = Resolve(result.MessageKey);
            FeedbackSucceeded = result.Succeeded;
        }

        private string Resolve(StableId key) =>
            openedSession.Archive.Content.Resolve(key, Debug.isDebugBuild || Application.isEditor);

        internal static EchoFrame RecordOf(StableId id) => StationEcho.Timeline.Single(frame => frame.Evidence.Id == id);

        internal static string TitleOf(StableId id) => id.value switch
        {
            "echo.stale-input" => "Sensor reading",
            "echo.controller-decision" => "Controller action",
            "echo.backup-exhausted" => "Backup outage",
            "echo.backup-estimate" => "Backup power estimate",
            _ => "ECHO record"
        };

        internal static string TimeOf(EchoFrame record) =>
            record.Evidence.RecordedAt?.UtcDateTime.ToString("HH:mm 'UTC'", CultureInfo.InvariantCulture)
                ?? "Time unknown";

        internal static string SourceType(EchoFrame record) => record.Evidence.Quality switch
        {
            EvidenceQuality.Recorded => "Recorded fictional station log",
            EvidenceQuality.Estimated => "Model estimate",
            EvidenceQuality.Missing => "Missing interval",
            _ => "Evidence type unknown"
        };
    }
}
