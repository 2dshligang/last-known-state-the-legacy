using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationResearchTerminal : MonoBehaviour
    {
        public static readonly StableId Target = new StableId("quest.report");
        private static readonly ReportObservation[] Observations = (ReportObservation[])Enum.GetValues(typeof(ReportObservation));
        private readonly HashSet<ReportObservation> selected = new();
        private StationFirstPerson player;
        private Func<GameplaySession> sessionSource;
        private Func<CommandResult> save;
        private GameplaySession boundSession;
        private IDisposable lease;
        private ReportInterpretation interpretation;
        private ReportAlternative alternative;
        private ReportUncertainty uncertainty;
        private ReportFollowUp followUp;
        private IReadOnlyList<ReportEvidence> evidence;
        private FictionalSample reference;
        private int focus;

        internal bool IsOpen => lease != null;
        internal string Feedback { get; private set; }
        internal bool FeedbackSucceeded { get; private set; }
        internal FictionalSample FeedbackReference { get; private set; }
        internal bool Accepted => boundSession.Research.Snapshot == ReportStatus.Accepted;
        internal ResearchDraft Draft => new ResearchDraft(Observations.Where(selected.Contains),
            interpretation, alternative, uncertainty, followUp);
        private bool CanControl => isActiveAndEnabled && IsOpen && player.HasApplicationFocus && !player.IsPaused &&
            ReferenceEquals(boundSession, sessionSource());
        private bool CanSubmit => boundSession.Quests.Snapshot[Target] == QuestStatus.Available;
        private bool CanQueue => Accepted && boundSession.Logistics.Delivery == DeliveryState.NotQueued;

        internal bool Open(StationFirstPerson input, Func<GameplaySession> source, Func<CommandResult> saveProgress)
        {
            var session = source?.Invoke();
            if (!isActiveAndEnabled || IsOpen || input == null || !input.isActiveAndEnabled || input.IsPaused ||
                input.HasReadingFocus || !input.HasApplicationFocus || session?.Research == null || saveProgress == null)
                return false;
            if (!ReferenceEquals(boundSession, session))
            {
                SetDraft(session.Research.CaptureAccepted());
                Feedback = null;
                FeedbackReference = null;
            }
            else if (session.Research.CaptureAccepted() is ResearchDraft accepted)
                SetDraft(accepted);
            player = input;
            sessionSource = source;
            save = saveProgress;
            boundSession = session;
            reference = null;
            evidence = session.Research.ReadEvidence();
            focus = 0;
            lease = input.AcquireReadingFocus(Close);
            return true;
        }

        internal void Close()
        {
            var current = lease;
            lease = null;
            player = null;
            sessionSource = null;
            save = null;
            reference = null;
            evidence = null;
            current?.Dispose();
        }

        private void ExitToStation()
        {
            var input = player;
            Close();
            if (!input.HasWorldFocus) input.ResumePointer();
        }

        private void OnDisable()
        {
            Close();
            boundSession = null;
        }

        private void Update()
        {
            if (IsOpen && !ReferenceEquals(boundSession, sessionSource()))
            {
                Close();
                boundSession = null;
            }
        }

        private void SetDraft(ResearchDraft draft)
        {
            selected.Clear();
            if (draft != null) selected.UnionWith(draft.Observations);
            interpretation = draft?.Interpretation ?? ReportInterpretation.Unspecified;
            alternative = draft?.Alternative ?? ReportAlternative.Unspecified;
            uncertainty = draft?.Uncertainty ?? ReportUncertainty.Unspecified;
            followUp = draft?.FollowUp ?? ReportFollowUp.Unspecified;
        }

        internal bool ToggleObservation(ReportObservation observation)
        {
            if (!CanControl || Accepted || !Observations.Contains(observation)) return false;
            if (!selected.Remove(observation)) selected.Add(observation);
            return true;
        }

        internal bool CycleChoice(int section, int step)
        {
            if (!CanControl || Accepted || step is not (1 or -1)) return false;
            switch (section)
            {
                case 0: interpretation = Next(interpretation, step); break;
                case 1: alternative = Next(alternative, step); break;
                case 2: uncertainty = Next(uncertainty, step); break;
                case 3: followUp = Next(followUp, step); break;
                default: return false;
            }
            return true;
        }

        private static T Next<T>(T choice, int step) where T : Enum
        {
            var options = (T[])Enum.GetValues(typeof(T));
            return options[(Array.IndexOf(options, choice) + step + options.Length) % options.Length];
        }

        internal bool SubmitReport()
        {
            if (!CanControl || !CanSubmit || reference != null) return false;
            if (!Accepted)
            {
                var result = boundSession.Research.Submit(Draft);
                FeedbackReference = StationResearch.FeedbackReference(result.MessageKey);
                if (!Show(result)) return false;
                SetDraft(boundSession.Research.CaptureAccepted());
            }
            if (!boundSession.TryGetTarget(Target, out var target)) return false;
            var state = target.Read(StationQuests.Player);
            if (!state.Available) return false;
            var confirmation = target.Request(StationQuests.Player, StationQuests.Confirm);
            if (!confirmation.Succeeded) return Show(confirmation);
            if (string.IsNullOrEmpty(Feedback)) return Show(confirmation);
            return true;
        }

        internal bool QueueReport()
        {
            if (!CanControl || reference != null) return false;
            FeedbackReference = null;
            return Show(boundSession.Ending.QueueAcceptedReport());
        }

        internal bool SaveProgress()
        {
            if (!CanControl || reference != null) return false;
            FeedbackReference = null;
            return Show(save());
        }

        internal string Transmission
        {
            get
            {
                var state = boundSession.Logistics.Capture();
                if (state.Delivery == DeliveryState.Delivered) return "Delivered in the station simulation";
                if (state.Delivery == DeliveryState.NotQueued) return "Not queued";
                bool supplied = boundSession.Power.Snapshot.Loads.Any(load => load.Id == StationPower.Transmit && load.Supplied);
                return supplied ? $"Queued  /  {state.RemainingSeconds} powered seconds left" :
                    $"Queued  /  Waiting for transmitter power ({state.RemainingSeconds} s)";
            }
        }

        internal bool OpenReference(FictionalSample sample)
        {
            if (!CanControl || sample == null) return false;
            reference = sample;
            focus = 0;
            return true;
        }

        private bool Show(CommandResult result)
        {
            Feedback = Resolve(result.MessageKey);
            FeedbackSucceeded = result.Succeeded;
            return result.Succeeded;
        }

        private string Resolve(StableId key) =>
            boundSession.Archive.Content.Resolve(key, Debug.isDebugBuild || Application.isEditor);
    }
}
