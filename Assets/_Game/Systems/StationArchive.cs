using System;
using System.Collections.Generic;
using System.Linq;

namespace LKS
{
    public sealed class StationArchive
    {
        private readonly GameplaySession session;
        private readonly HashSet<StableId> acknowledged;
        private readonly Dictionary<StableId, int> open = new Dictionary<StableId, int>();
        private bool stopped;

        internal StationArchive(GameplaySession session, IEnumerable<StableId> checkpoint)
        {
            this.session = session;
            var restored = checkpoint?.ToArray() ?? Array.Empty<StableId>();
            StableId.ValidateUnique(restored);
            if (restored.Any(id => !EnglishContentCatalog.Archive.Any(entry => entry.Id == id)))
                throw new ArgumentException("Archive checkpoint contains an unknown entry.", nameof(checkpoint));
            acknowledged = new HashSet<StableId>(restored);
            Content = EnglishContentCatalog.CreateDefault();
        }

        public EnglishContentCatalog Content { get; }
        public bool RequiredEntryAcknowledged => acknowledged.Contains(new StableId("archive.donki-sep"));

        public IReadOnlyList<StableId> Capture() => Array.AsReadOnly(
            EnglishContentCatalog.Archive.Select(entry => entry.Id).Where(acknowledged.Contains).ToArray());

        public IDisposable Open(StableId id)
        {
            if (stopped)
                throw new ObjectDisposedException(nameof(StationArchive));
            if (!EnglishContentCatalog.Archive.Any(entry => entry.Id == id))
                throw new ArgumentException("Unknown archive entry.", nameof(id));

            if (open.Count == 0)
                session.SetArchiveReadingPaused(true);
            open.TryGetValue(id, out int count);
            open[id] = count + 1;
            return new Reading(this, id);
        }

        public CommandResult Acknowledge(StableId id)
        {
            if (stopped || !open.ContainsKey(id))
                return new CommandResult(ResultCode.Unavailable, new StableId("archive.result.unavailable"));
            return acknowledged.Add(id)
                ? new CommandResult(ResultCode.Applied, new StableId("archive.result.read"))
                : new CommandResult(ResultCode.AlreadyApplied, new StableId("archive.result.already-read"));
        }

        private void Close(StableId id)
        {
            if (stopped)
                return;
            if (open[id] == 1)
                open.Remove(id);
            else
                open[id]--;
            if (open.Count == 0)
                session.SetArchiveReadingPaused(false);
        }

        internal void Stop()
        {
            stopped = true;
            open.Clear();
        }

        private sealed class Reading : IDisposable
        {
            private StationArchive archive;
            private readonly StableId id;

            public Reading(StationArchive archive, StableId id)
            {
                this.archive = archive;
                this.id = id;
            }

            public void Dispose()
            {
                archive?.Close(id);
                archive = null;
            }
        }
    }
}
