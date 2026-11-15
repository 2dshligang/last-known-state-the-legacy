using System;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationShelterSet : MonoBehaviour
    {
        public static readonly StableId ShelterTarget = new StableId("target.shelter-controls");
        public static readonly StableId SeedlingTarget = new StableId("target.seedling-plan");
        // S05 checkpoint schema v1 expresses completed actuator travel in model seconds.
        private const decimal FullTravelSeconds = 5m;

        [SerializeField] private StationInteraction interaction;
        [SerializeField] private StationFirstPerson player;
        [SerializeField] private Transform shutter;
        [SerializeField] private Transform closedStop;
        [SerializeField] private StationLabel shelterStatus;
        [SerializeField] private StationLabel seedlingStatus;

        private Vector3 openPosition;
        private EnglishContentCatalog text;
        private GameplaySession readingSession;
        private IDisposable reading;
        private StableId page;
        private string worldShelter, worldSeedlings;

        internal string Feedback { get; private set; }
        private bool feedbackSucceeded;
        internal bool IsOpen => reading != null;
        private GameplaySession Session => interaction.Session;
        private bool CanControl => isActiveAndEnabled && IsOpen && !player.IsPaused &&
            player.HasApplicationFocus && ReferenceEquals(readingSession, Session);

        private void Awake()
        {
            openPosition = shutter.position;
            text = EnglishContentCatalog.CreateDefault();
        }

        private void OnDisable() => Close();

        private void LateUpdate()
        {
            if (IsOpen && !ReferenceEquals(readingSession, Session)) Close();
            Present();
        }

        internal static bool Handles(StableId id) => id == ShelterTarget || id == SeedlingTarget;

        internal string Describe(StableId id)
        {
            string title = id == ShelterTarget ? "Shelter control" : "Seedling cabinet";
            return CanOpen(id) ? "E  Open " + title.ToLowerInvariant() + "\n" + title :
                "Open " + title.ToLowerInvariant() + " / " + title + "\nControls are unavailable.";
        }

        internal bool Open(StableId id)
        {
            if (!CanOpen(id) || player.IsPaused || player.HasReadingFocus) return false;
            readingSession = Session;
            page = id;
            reading = player.AcquireReadingFocus(Close);
            return true;
        }

        internal void Close()
        {
            var lease = reading;
            reading = null;
            readingSession = null;
            page = default;
            Feedback = null;
            lease?.Dispose();
        }

        internal bool RequestShelter()
        {
            if (!CanControl || page != ShelterTarget) return false;
            var support = readingSession.LifeSupport;
            return Report(support.RequestShelter(support.ReadShelter()));
        }

        internal bool SetActuatorPower(bool enabled)
        {
            if (!CanControl || page != ShelterTarget) return false;
            return Report(readingSession.Power.RequestLoad(StationPower.ShelterActuator, enabled));
        }

        internal bool Choose(FoodSchedule choice)
        {
            if (!CanControl || page != SeedlingTarget) return false;
            return Report(readingSession.Logistics.Choose(choice));
        }

        private bool CanOpen(StableId id) => isActiveAndEnabled && Handles(id) &&
            Session?.LifeSupport != null && Session.Logistics != null;

        private bool Report(CommandResult result)
        {
            Feedback = Resolve(result.MessageKey);
            feedbackSucceeded = result.Succeeded;
            return feedbackSucceeded;
        }

        private string Resolve(StableId key) => text.Resolve(key, Debug.isDebugBuild || Application.isEditor);

        internal string FoodStatus
        {
            get
            {
                var service = Session.Logistics;
                return service.Capture().Schedule switch
                {
                    FoodSchedule.PreserveSeedlings => service.SeedlingPlanPowered
                        ? "Seedling preservation is powered." : "Seedling preservation is scheduled. Waiting for power.",
                    FoodSchedule.RunLab => LoadSupplied(StationPower.Lab)
                        ? "The lab is powered. Seedling care is deferred." : "The lab is scheduled. Waiting for power.",
                    _ => "Choose a plan. The cabinet holds starter seedlings from this expedition."
                };
            }
        }

        private bool LoadSupplied(StableId id) => Session.Power.Snapshot.Loads.Any(load => load.Id == id && load.Supplied);

        private void Present()
        {
            var support = Session?.LifeSupport;
            var phase = support?.ReadShelter().Phase ?? ShelterPhase.Waiting;
            decimal travel = support?.Capture().ActuatorSeconds ?? 0m;
            Vector3 position = Vector3.Lerp(openPosition, closedStop.position, (float)(travel / FullTravelSeconds));
            if (shutter.position != position) shutter.position = position;
            string shelter = phase switch
            {
                ShelterPhase.Ready => "TRAINING READY",
                ShelterPhase.Moving => "SHUTTER MOVING",
                ShelterPhase.Interrupted => "ACTUATOR STOPPED",
                ShelterPhase.Sheltered => "SHIELD CLOSED",
                _ => "BRIEFING PENDING"
            };
            if (shelter != worldShelter)
            {
                shelterStatus.SetText(shelter);
                worldShelter = shelter;
            }
            string seedlings = Session?.Logistics?.Capture().Schedule switch
            {
                FoodSchedule.PreserveSeedlings => Session.Logistics.SeedlingPlanPowered ? "CARE ON" : "CARE WAITING",
                FoodSchedule.RunLab => "LAB PLAN",
                _ => "STANDBY"
            };
            if (seedlings != worldSeedlings)
            {
                seedlingStatus.SetText(seedlings);
                worldSeedlings = seedlings;
            }
        }
    }
}
