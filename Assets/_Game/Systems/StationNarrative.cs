using System;

namespace LKS
{
    public enum OpeningProgress { Farewell, Arrival, Complete }

    public sealed class NarrativeCue
    {
        public StableId TextKey { get; }
        public StableId Portrait { get; }
        public StableId CameraCue { get; }

        internal NarrativeCue(string text, string portrait, string camera)
        {
            TextKey = new StableId(text);
            Portrait = new StableId(portrait);
            CameraCue = new StableId(camera);
        }

        public void Present(INarrativeView view, EnglishContentCatalog content)
        {
            string text = content.Resolve(TextKey, false);
            view.ShowText(text, Portrait, CameraCue);
            PlayerTextAudit.Record(TextKey.ToString(), text);
        }
    }

    public sealed class StationNarrative
    {
        public const int CheckpointVersion = 1;

        public static readonly DateTime AlexeiBirthDate = new DateTime(2050, 3, 15);
        public static readonly DateTime FarewellDate = new DateTime(2059, 1, 20);
        public static readonly DateTime ArrivalDate = new DateTime(2089, 11, 14);

        private static readonly NarrativeCue Farewell =
            new NarrativeCue("narrative.farewell", "portrait.dmitry", "camera.farewell");
        private static readonly NarrativeCue Arrival =
            new NarrativeCue("narrative.arrival", "portrait.alexei", "camera.station-arrival");
        private static readonly NarrativeCue ResearchContext =
            new NarrativeCue("narrative.research-context", "portrait.dmitry-photograph", "camera.research-desk");

        private readonly StationResearch research;
        private OpeningProgress opening;
        private OpeningProgress? replay;
        private bool stopped;

        internal StationNarrative(StationResearch research, OpeningProgress checkpoint)
        {
            if (!Enum.IsDefined(typeof(OpeningProgress), checkpoint))
                throw new ArgumentException("Invalid narrative checkpoint.", nameof(checkpoint));
            this.research = research;
            opening = checkpoint;
        }

        public OpeningProgress CaptureOpening() => opening;
        public bool IsReplaying => replay.HasValue;

        public bool TryGetOpening(out NarrativeCue cue)
        {
            cue = stopped ? null : (replay ?? opening) switch
            {
                OpeningProgress.Farewell => Farewell,
                OpeningProgress.Arrival => Arrival,
                _ => null
            };
            return cue != null;
        }

        public bool AdvanceOpening()
        {
            if (!TryGetOpening(out _))
                return false;

            var current = replay ?? opening;
            var next = current == OpeningProgress.Farewell ? OpeningProgress.Arrival : OpeningProgress.Complete;
            if (replay.HasValue)
                replay = next == OpeningProgress.Complete ? (OpeningProgress?)null : next;
            else
                opening = next;
            return true;
        }

        public bool SkipOpening()
        {
            if (!TryGetOpening(out _))
                return false;
            if (replay.HasValue)
                replay = null;
            else
                opening = OpeningProgress.Complete;
            return true;
        }

        public bool ReplayOpening()
        {
            if (stopped || replay.HasValue || opening != OpeningProgress.Complete)
                return false;
            replay = OpeningProgress.Farewell;
            return true;
        }

        public bool TryGetResearchContext(out NarrativeCue cue)
        {
            cue = !stopped && research.Snapshot == ReportStatus.Accepted ? ResearchContext : null;
            return cue != null;
        }

        internal void Stop()
        {
            stopped = true;
            replay = null;
        }
    }
}
