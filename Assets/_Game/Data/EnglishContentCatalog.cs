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
            new ArchiveEntry("archive.dmitry-notes", 2059, 8, 18, 0, 0,
                EvidenceKind.Fiction, true),
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
sample.layered-mudstone.context|These fine layers are a clue to how this fictional rock formed. Curiosity studied layered mudstone from an ancient Martian lake.
sample.iron-rich-rock.context|Compare this fictional rock with the layered sample. Curiosity found iron minerals in rocks at Vera Rubin Ridge.
sample.procedural-blank.context|Run the empty control to see what the instrument detects without a rock. Curiosity ran blank cups to check its instrument background.
sample.result.context-recorded|Sample context recorded.
sample.result.already-recorded|Sample context is already recorded.
sample.result.record-context|Record where the sample came from first.
sample.result.already-collected|Sample already collected.
sample.result.collected|Sample collected.
sample.result.already-started|This sample already has a run or a result.
sample.result.collect-first|Collect the sample first.
sample.result.lab-busy|The lab is busy.
sample.result.run-blank|Run the blank control first.
sample.result.power-lab|Supply power to the lab.
sample.result.analysis-started|Analysis started.
sample.result.no-active-run|There is no active run.
sample.result.cancelled|Analysis cancelled.
sample.result.failed-blank-first|Cleaning for a retry is available after a failed blank.
sample.result.cleaned|Chamber cleaned. Run the blank again.
sample.result.already-reviewed|This result is already reviewed.
sample.result.await-evidence|Wait for the analysis result.
sample.result.reviewed|Interpretation reviewed.
sample.result.evidence-ready|Analysis result ready.
sample.result.unknown-sample|Unknown sample.
sample.result.unavailable|Sample controls are unavailable.
sample.interpretation.pending|Run the analysis to see an interpretation.
sample.interpretation.limited|The blank found instrument background. Clean the chamber and repeat the checks before drawing a conclusion.
sample.layered-mudstone.observation|Fictional result. Fine layers are visible in the sample.
sample.layered-mudstone.interpretation|The layers could have built up as sediment settled. More clues are needed to tell where it formed.
sample.iron-rich-rock.observation|Fictional result. The comparison rock shows signs of iron minerals.
sample.iron-rich-rock.interpretation|Iron minerals can help compare rocks. This finding alone cannot show that life was present.
sample.procedural-blank.observation|Fictional result. Check the blank status for instrument background.
sample.procedural-blank.interpretation|This blank passed the station check. Use it as the comparison for the rock runs.
report.result.already-accepted|This report has already been accepted.
report.result.unavailable|Research controls are unavailable.
report.result.complete-sections|Complete all five report sections.
report.result.life-unsupported|Organics can form without life. The evidence cannot confirm life.
report.result.confirm-echo|Confirm the station incident with ECHO first.
report.result.valid-blank|Check the instrument with a valid blank before drawing conclusions.
report.result.contradictory-blank|The current blank passed. Earlier instrument background cannot describe this run.
report.result.cite-blank|Include the valid blank in your observations.
report.result.cite-reviewed-samples|Review both rock results and include them in your observations.
report.result.duplicate-observation|Include each observation once.
report.result.interpretation-needed|Choose a cautious interpretation that fits the evidence.
report.result.alternative-needed|Include another way the rocks could have changed.
report.result.state-uncertainty|Say what the current evidence leaves open.
report.result.follow-up-needed|Choose a comparison and a fresh blank for the next check.
report.result.accepted|Dmitry's report is complete. The evidence supports a cautious explanation and a next step.
report.section.observations|Observations
report.section.interpretation|What the evidence could mean
report.section.alternatives|Other explanations
report.section.uncertainty|What is still uncertain
report.section.follow-up|What to check next
report.choice.observation.layersvisible|The rock sample shows fine layers.
report.choice.observation.ironmineralsigns|The comparison rock shows signs of iron minerals.
report.choice.observation.blankpassed|The current blank passed.
report.choice.observation.blankshowedbackground|The current blank found instrument background.
report.choice.interpretation.sedimentmayhavesettled|The fine layers could have formed as sediment settled.
report.choice.interpretation.formationremainsuncertain|Several environments could explain these rocks.
report.choice.interpretation.lifeconfirmed|The samples prove that life was here.
report.choice.alternative.winddeposition|Wind could also have carried sediment here.
report.choice.alternative.latermineralchange|The minerals could have changed after the rock formed.
report.choice.alternative.noalternatives|There is no other explanation.
report.choice.uncertainty.originuncertain|The rock's exact origin is still uncertain.
report.choice.uncertainty.moreevidenceneededforlife|We would need more evidence to test for life.
report.choice.uncertainty.nouncertainty|The evidence answers every question.
report.choice.follow-up.comparenearbylayerswithblank|Compare nearby layers and use a fresh blank.
report.choice.follow-up.checkmineralswithfreshblank|Check the minerals in a second sample and use a fresh blank.
report.choice.follow-up.none|No more checks are needed.
narrative.farewell|January 20, 2059. Alexei is eight. His father Dmitry is leaving for Mars to study ancient rocks. Dmitry gives Alexei a photograph. ""Write down what you see,"" he says. ""Then ask what it could mean.""
narrative.arrival|November 14, 2089. Alexei is 39. He carries the photograph into the silent ASTERION station. His father's work is still here. First, he needs to restore air and power.
narrative.research-context|Dmitry wanted to know what this place was like long ago. Alexei checks the rock results against the blank, keeps the uncertainties in his report, and leaves a clear next step for the next scientist.
narrative.action.continue|Continue
narrative.action.skip|Skip opening
narrative.action.replay|Replay opening
archive.dmitry-notes.summary|Dmitry kept his rock notes separate from his ideas about how the rocks formed.
archive.dmitry-notes.detail|The station's fictional research notes ask whether fine layers could have formed as sediment settled. Dmitry planned to compare nearby rocks and check for material left behind by the instrument. The expedition ended before he could finish.
ending.final|Dad... I did it.
";
    }
}
