using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LKS
{
    public sealed class ArchiveEntry
    {
        public StableId Id { get; }
        public DateTimeOffset Date { get; }
        public EvidenceKind Kind { get; }
        public bool IsFictional { get; }
        public IReadOnlyList<StableId> Sources { get; }
        public StableId SummaryKey => new StableId(Id + ".summary");
        public StableId DetailKey => new StableId(Id + ".detail");

        internal ArchiveEntry(string id, int year, int month, int day, int hour, int minute,
            EvidenceKind kind, bool fictional, params string[] sources)
        {
            Id = new StableId(id);
            Date = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
            Kind = kind;
            IsFictional = fictional;
            Sources = Array.AsReadOnly(Array.ConvertAll(sources, source => new StableId(source)));
        }
    }

    public sealed class EnglishContentCatalog
    {
        public static readonly StableId EndingKey = new StableId("ending.final");
        private const string Ending = "Dad... I did it.";
        private static readonly Regex Cyrillic = new Regex(@"[\u0400-\u052F\u1C80-\u1C8F\u2DE0-\u2DFF\uA640-\uA69F]");
        private readonly Dictionary<StableId, string> text;

        public EnglishContentCatalog(IEnumerable<KeyValuePair<StableId, string>> entries,
            IEnumerable<StableId> requiredKeys)
        {
            text = new Dictionary<StableId, string>();
            foreach (var entry in entries)
            {
                if (!entry.Key.IsValid || string.IsNullOrWhiteSpace(entry.Value) || Cyrillic.IsMatch(entry.Value))
                    throw new ArgumentException($"Invalid English content for '{entry.Key}'.", nameof(entries));
                if (!text.TryAdd(entry.Key, entry.Value))
                    throw new ArgumentException($"Duplicate content key '{entry.Key}'.", nameof(entries));
            }

            foreach (var key in requiredKeys)
                Resolve(key, false);
            if (Resolve(EndingKey, false) != Ending)
                throw new ArgumentException("The final ending text has changed.", nameof(entries));
        }

        public string Resolve(StableId key, bool development)
        {
            if (text.TryGetValue(key, out var value))
                return value;
            if (development)
                return $"[Missing text: {key}]";
            throw new KeyNotFoundException($"Missing English text for '{key}'.");
        }

        public static readonly IReadOnlyList<ArchiveEntry> Archive = Array.AsReadOnly(new[]
        {
            new ArchiveEntry("archive.donki-sep", 2017, 9, 4, 23, 52,
                EvidenceKind.Observation, false, "nasa-donki-sep-2017"),
            new ArchiveEntry("archive.power-model", 2089, 11, 14, 0, 0,
                EvidenceKind.EngineeringModel, true, "asterion-prototype-v1"),
            new ArchiveEntry("archive.arrival", 2089, 11, 14, 0, 0,
                EvidenceKind.Fiction, true)
        });

        public static string CategoryKey(EvidenceKind kind)
        {
            switch (kind)
            {
                case EvidenceKind.Observation: return "evidence.observation";
                case EvidenceKind.EngineeringModel: return "evidence.engineering-model";
                case EvidenceKind.Fiction: return "evidence.fiction";
                default: return "evidence.unknown";
            }
        }

        public static EnglishContentCatalog CreateDefault()
        {
            var entries = new List<KeyValuePair<StableId, string>>();
            foreach (var line in DefaultText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int boundary = line.IndexOf('|');
                entries.Add(new KeyValuePair<StableId, string>(new StableId(line.Substring(0, boundary)),
                    line.Substring(boundary + 1)));
            }

            var required = new List<StableId>();
            var ids = new List<StableId>();
            foreach (var entry in Archive)
            {
                ids.Add(entry.Id);
                required.Add(entry.SummaryKey);
                required.Add(entry.DetailKey);
                required.Add(new StableId(CategoryKey(entry.Kind)));
            }
            StableId.ValidateUnique(ids);
            return new EnglishContentCatalog(entries, required);
        }

        private const string DefaultText = @"
menu.new-game|New game
menu.continue|Continue
menu.pause|Pause
menu.resume|Resume
menu.credits|Credits
menu.exit|Exit
credits.team|A game by the LAST KNOWN STATE team.
credits.sources|Science references: NASA's DONKI archive and the Canadian Space Agency.
action.confirm|Confirm
action.recover-power|Restore a safe power allocation
action.acknowledge|Mark as read
prompt.interact|Interact
prompt.inspect|Inspect
quest.arrival|Enter the station.
quest.safe-power|Restore power to air and water.
quest.echo-evidence|Gather clues in the ECHO archive.
quest.sampling|Collect the marked rock samples.
quest.analysis|Compare the samples with the control.
quest.report|Prepare a report supported by the evidence.
quest.ending|Look at Dad's photograph.
quest.result.applied|Step complete.
quest.result.completed|This step is already complete.
quest.result.prerequisite|Finish the previous step first.
quest.result.resources|Restore resources and try again.
quest.result.evidence|More evidence is needed.
quest.result.invalid-action|This action is unavailable here.
quest.result.unavailable|This control is unavailable.
power.result.applied|Allocation applied.
power.result.unchanged|Already selected.
power.result.insufficient|There is not enough power for these loads.
power.result.unknown-load|Unknown load.
power.result.invalid-allocation|Invalid load selection.
power.result.unavailable|Power controls are unavailable.
shelter.phase.waiting|Wait for the training briefing.
shelter.phase.ready|Prepare the shelter.
shelter.phase.moving|The shelter is closing.
shelter.phase.interrupted|Restore power to continue closing the shelter.
shelter.phase.sheltered|The shelter is closed.
shelter.result.unavailable|Shelter controls are unavailable.
shelter.result.stale-reading|Read the shelter status again.
shelter.result.wait-for-briefing|Wait for the training briefing.
shelter.result.already-requested|The shelter is already requested.
shelter.result.actuator-unpowered|Power the shelter actuator first.
shelter.result.started|Shelter requested.
logistics.result.unavailable|This control is unavailable.
logistics.result.invalid-choice|Choose seedling care or lab power.
logistics.result.unknown-message|Unknown message.
logistics.result.already-queued|The report is already queued or delivered.
logistics.result.queued|The report is queued.
archive.result.read|Marked as read.
archive.result.already-read|Already marked as read.
archive.result.unavailable|Open the archive entry before marking it as read.
evidence.observation|Recorded observation
evidence.engineering-model|Engineering model
evidence.fiction|Fictional story
evidence.unknown|Evidence type unknown
archive.donki-sep.summary|An instrument on SOHO recorded an event involving energetic particles.
archive.donki-sep.detail|NASA's archive lists the event on September 4, 2017 at 23:52 UTC. The record identifies the observing instrument. It gives no radiation dose for the surface of Mars.
archive.power-model.summary|Air, water and the lab share the station's power.
archive.power-model.detail|In this simplified game model, a battery stores extra energy. If the station asks for more power than it generates, the battery drains. When it runs low, the system gives air and water higher priority.
archive.arrival.summary|Alexei arrives at the station where his father worked.
archive.arrival.detail|In 2059, Alexei said goodbye to his father Dmitry. Thirty years later, he returns to Mars to find out what happened and finish his father's research.
science.organics|Organic molecules can form without life. Finding them gives scientists a clue to study.
science.food|Growing food takes time and suitable light, water, nutrients and temperature.
echo.stale-input.caption|The controller received an old power reading.
echo.controller-decision.caption|The controller treated the old reading as current.
echo.backup-estimate.caption|The model suggests backup power took over.
echo.missing-interval.caption|Part of the station log is missing here.
echo.backup-exhausted.caption|The final station log shows backup power ran out.
echo.result.unavailable|The ECHO controls are unavailable.
echo.result.unknown-clue|Choose a clue from the station logs.
echo.result.read-archive|Read and mark the required archive entry first.
echo.result.collected|Clue collected.
echo.result.already-collected|Clue already collected.
echo.result.invalid-selection|Choose each of the three clues once.
echo.result.missing-clues|Collect all three clues to confirm your explanation.
echo.result.timing-before-decision|Put the old reading before the decision that used it.
echo.result.decision-before-outage|Put the controller decision before the backup power ran out.
echo.result.uncertain-reading|The time records are too uncertain to confirm this explanation.
echo.result.confirmed|The old reading guided the controller decision. Backup power later ran out.
echo.result.already-confirmed|This explanation is already confirmed.
echo.freshness.rule|In this game model, the controller needs a reading no more than one minute old. The time records may be off by fifteen seconds. The old reading may still describe an earlier time.
ending.final|Dad... I did it.
";
    }
}
