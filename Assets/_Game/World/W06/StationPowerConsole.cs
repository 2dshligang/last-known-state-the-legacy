using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationPowerConsole : MonoBehaviour
    {
        public static readonly StableId Target = new StableId("quest.safe-power");
        private static readonly StableId Arrival = new StableId("quest.arrival");
        [SerializeField] private StationInteraction interaction;
        [SerializeField] private StationFirstPerson player;

        private readonly HashSet<StableId> draft = new HashSet<StableId>();
        private EnglishContentCatalog text;
        private StationPower power;
        private IDisposable reading, subscription;
        private bool edited;
        private int keyboardFocus = -1;

        internal PowerSnapshot Live { get; private set; }
        internal PowerSnapshot Proposed { get; private set; }
        internal CommandResult PreviewResult { get; private set; }
        internal string Feedback { get; private set; }
        internal bool IsOpen => reading != null;
        internal string OpenBlockReason => !isActiveAndEnabled || interaction.Session?.Power == null
            ? "Power controls are unavailable."
            : interaction.Session.Quests?.Snapshot[Arrival] != QuestStatus.Completed ? "Confirm arrival first." : null;
        private bool CanEdit => isActiveAndEnabled && IsOpen && !player.IsPaused && player.HasApplicationFocus &&
            ReferenceEquals(power, interaction.Session?.Power);

        private void Awake() => text = EnglishContentCatalog.CreateDefault();
        private void OnDisable() => Close();

        private void Update()
        {
            if (IsOpen && !ReferenceEquals(power, interaction.Session?.Power)) Close();
        }

        internal bool Open()
        {
            if (OpenBlockReason != null || player.IsPaused || player.HasReadingFocus) return false;
            power = interaction.Session.Power;
            IPowerService readings = power;
            reading = player.AcquireReadingFocus(Close);
            subscription = readings.Subscribe(update => Refresh(update.Snapshot));
            Refresh(readings.Snapshot);
            return true;
        }

        internal void Close()
        {
            var lease = reading;
            reading = null;
            subscription?.Dispose();
            subscription = null;
            power = null;
            Live = null;
            Proposed = null;
            edited = false;
            draft.Clear();
            Feedback = null;
            keyboardFocus = -1;
            lease?.Dispose();
        }

        internal bool Toggle(StableId id)
        {
            if (!CanEdit || !Live.Loads.Any(load => load.Id == id)) return false;
            if (!draft.Add(id)) draft.Remove(id);
            edited = !MatchesLiveRequest();
            Feedback = null;
            Preview();
            return true;
        }

        internal bool Apply()
        {
            if (!CanEdit || !CanApply) return false;
            var result = power.Commit(draft);
            Feedback = Resolve(result.MessageKey);
            if (result.Succeeded) edited = false;
            Refresh(power.Snapshot);
            return result.Succeeded;
        }

        internal bool ConfirmLifeSupport()
        {
            if (!CanEdit || !TryReadConfirmation(out var target, out var state) || !state.Available) return false;
            var result = target.Request(StationQuests.Player, state.PromptKey);
            Feedback = Resolve(result.MessageKey);
            return result.Succeeded;
        }

        internal string Explanation
        {
            get
            {
                if (!PreviewResult.Succeeded)
                    return Resolve(PreviewResult.MessageKey) + " Reduce the selection and try again.";
                if (Proposed.DemandKilowatts > Proposed.GenerationKilowatts)
                    return "The battery covers the extra demand. If it runs out, lower-priority equipment loses power. Try using the lab and archive one at a time.";
                return "The generator can meet this selection. To change it, select a button in the Proposed status column.";
            }
        }

        internal string RecoveryHint =>
            "Keep Air circulation and Water supply on. If power is lost, use Restore Safe Power across the corridor.";

        private bool CanApply => edited || (Live.Loads.Any(load => load.Requested && !load.Supplied) && PreviewResult.Succeeded);

        private void Refresh(PowerSnapshot snapshot)
        {
            Live = snapshot;
            if (!edited)
            {
                draft.Clear();
                draft.UnionWith(Live.Loads.Where(load => load.Requested).Select(load => load.Id));
            }
            Preview();
        }

        private bool MatchesLiveRequest() =>
            Live.Loads.All(load => load.Requested == draft.Contains(load.Id));

        private void Preview()
        {
            PreviewResult = power.Preview(draft, out var preview);
            Proposed = preview;
        }

        private bool TryReadConfirmation(out IInteractable target, out InteractionState state)
        {
            state = default;
            target = null;
            if (interaction.Session?.TryGetTarget(Target, out target) != true) return false;
            state = target.Read(StationQuests.Player);
            return true;
        }

        private string Resolve(StableId key) => text.Resolve(key, Debug.isDebugBuild || Application.isEditor);
    }
}
