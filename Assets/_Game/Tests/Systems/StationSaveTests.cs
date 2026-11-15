using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationSaveTests
    {
        private static readonly StableId Slot = new StableId("campaign");
        private string folder;
        private string PathToSlot => Path.Combine(folder, "slot-campaign.lks");

        [SetUp]
        public void SetUp() => folder = Path.Combine(Path.GetTempPath(), "lks-save-test-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void InterruptedWriteAndTruncatedPrimaryPreserveTheGoodBackup()
        {
            var store = new AtomicSaveStore(folder, "content", 2);
            Assert.That(store.Write(Slot, Snapshot(1)).Succeeded, Is.True);
            Assert.That(store.Write(Slot, Snapshot(2)).Succeeded, Is.True);
            File.WriteAllBytes(PathToSlot + ".abandoned.tmp", new byte[] { 7 });
            using (var writer = new FileStream(PathToSlot + ".lock", FileMode.Open, FileAccess.Write, FileShare.None))
                Assert.That(store.Write(Slot, Snapshot(3)).Code, Is.EqualTo(ResultCode.StorageError));

            Assert.That(store.Read(Slot, out var saved).Succeeded, Is.True);
            Assert.That(saved.CopyPayload(), Is.EqualTo(new byte[] { 2 }));
            File.WriteAllBytes(PathToSlot, File.ReadAllBytes(PathToSlot).Take(8).ToArray());
            Assert.That(store.Read(Slot, out saved).MessageKey.value, Is.EqualTo("save.result.backup-loaded"));
            Assert.That(saved.CopyPayload(), Is.EqualTo(new byte[] { 1 }));
            var backup = File.ReadAllBytes(PathToSlot + ".bak");
            Assert.That(store.Write(Slot, Snapshot(4)).Succeeded, Is.True);
            Assert.That(File.ReadAllBytes(PathToSlot + ".bak"), Is.EqualTo(backup));
            Assert.That(store.Read(Slot, out saved).Succeeded, Is.True);
            Assert.That(saved.CopyPayload(), Is.EqualTo(new byte[] { 4 }));
        }

        [Test]
        public void FutureSchemaAndChangedContentLeaveBothFilesUntouched()
        {
            var future = new AtomicSaveStore(folder, "content", 3);
            Assert.That(future.Write(Slot, Snapshot(1, 3)).Succeeded, Is.True);
            Assert.That(future.Write(Slot, Snapshot(2, 3)).Succeeded, Is.True);
            var original = File.ReadAllBytes(PathToSlot);
            var backup = File.ReadAllBytes(PathToSlot + ".bak");
            foreach (var candidate in new[] { (2, "content"), (3, "changed") })
            {
                var store = new AtomicSaveStore(folder, candidate.Item2, candidate.Item2 == "changed" ? 3 : 2);
                Assert.That(store.Read(Slot, out var read).Code, Is.EqualTo(ResultCode.IncompatibleVersion));
                Assert.That(read, Is.Null);
                Assert.That(store.Write(Slot, Snapshot(3, candidate.Item1, candidate.Item2)).Code,
                    Is.EqualTo(ResultCode.IncompatibleVersion));
                Assert.That(File.ReadAllBytes(PathToSlot), Is.EqualTo(original));
                Assert.That(File.ReadAllBytes(PathToSlot + ".bak"), Is.EqualTo(backup));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void RestoresChapterWithoutReplayingTheEnding(int stage)
        {
            WithStation((saves, boot, catalog, required) =>
            {
                var stages = StationQuests.RequiredStages.Take(stage).ToArray();
                var clues = EchoInvestigation.RequiredClues;
                var evidence = new[] { new StableId("archive.donki-sep") };
                var phases = FictionalSample.All.ToDictionary(sample => sample.Id, _ => SamplePhase.Reviewed);
                var pending = FictionalSample.All[0].Id;
                if (stage == 4)
                    phases[pending] = SamplePhase.AnalysisPending;
                var report = new ResearchDraft(
                    new[] { ReportObservation.LayersVisible, ReportObservation.IronMineralSigns, ReportObservation.BlankPassed },
                    ReportInterpretation.SedimentMayHaveSettled, ReportAlternative.WindDeposition,
                    ReportUncertainty.OriginUncertain, ReportFollowUp.CompareNearbyLayersWithBlank);
                var opening = stage == 0 ? OpeningProgress.Arrival : OpeningProgress.Complete;
                var logistics = stage >= 6 ? new LogisticsCheckpoint(FoodSchedule.Unselected,
                    StationLogistics.ReportMessage, DeliveryState.Queued, 15) : null;

                Assert.That(boot.RestartWithPower(catalog, required, CreateTargets, Readiness,
                    powerCheckpoint: new PowerCheckpoint(1m, 0.25m, new[] { StationPower.Air, StationPower.Water }),
                    questCheckpoint: stages, clock: new Clock(), logisticsCheckpoint: logistics,
                    archiveCheckpoint: stage >= 3 ? evidence : null,
                    investigationCheckpoint: stage >= 3 ? new EchoInvestigationCheckpoint(clues, clues) : null,
                    sampleCheckpoint: stage >= 4 ? new SampleCheckpoint(phases, ControlStatus.Valid,
                        stage == 4 ? pending : default, stage == 4 ? 3 : 0, true) : null,
                    researchCheckpoint: stage >= 6 ? report : null, openingCheckpoint: opening), Is.Empty);
                var before = boot.Current;
                Assert.That(saves.Save(Slot, before).Succeeded, Is.True);
                Assert.That(saves.Load(Slot, boot, CreateTargets, Readiness, new Clock()).Succeeded, Is.True);
                Assert.That(boot.Current, Is.Not.SameAs(before));
                Assert.That(saves.Save(new StableId("obsolete"), before).Code, Is.EqualTo(ResultCode.InvalidRequest));
                var loaded = boot.Current;
                Assert.That(loaded.CaptureQuestCheckpoint(), Is.EqualTo(stages));
                Assert.That(loaded.Power.Capture().PendingSeconds, Is.EqualTo(0.25m));
                Assert.That(loaded.Power.Capture().BatteryKwh, Is.EqualTo(1m));
                Assert.That(loaded.Narrative.CaptureOpening(), Is.EqualTo(opening));
                Assert.That(loaded.Logistics.Capture().RemainingSeconds, Is.EqualTo(stage >= 6 ? 15 : 0));
                Assert.That(loaded.Samples.Capture().PendingId, Is.EqualTo(stage == 4 ? pending : default));
                Assert.That(loaded.Investigation.Conclusion == EchoConclusion.Confirmed, Is.EqualTo(stage >= 3));
                Assert.That(loaded.Ending.TryGetFinalCue(out var cue), Is.EqualTo(stage == 7));
                if (stage == 7)
                    Assert.That(loaded.Archive.Content.Resolve(cue.TextKey, false), Is.EqualTo("Dad... I did it."));
                int completionEvents = 0;
                using var listener = loaded.Ending.Completion.Subscribe(_ => completionEvents++);
                loaded.Dispatch();
                Assert.That(completionEvents, Is.Zero);
            });
        }

        [Test]
        public void MigratesVersionOneAndIgnoresRetiredOptionalArchiveEntry()
        {
            WithStation((saves, boot, catalog, required) =>
            {
                var stages = StationQuests.RequiredStages.Take(1).ToArray();
                Assert.That(boot.RestartWithPower(catalog, required, CreateTargets, Readiness,
                    questCheckpoint: stages, clock: new Clock()), Is.Empty);
                Assert.That(saves.Save(Slot, boot.Current).Succeeded, Is.True);
                var hash = ReadHash();
                var store = new AtomicSaveStore(folder, hash, 2);
                Assert.That(store.Read(Slot, out var current).Succeeded, Is.True);
                var json = Encoding.UTF8.GetString(current.CopyPayload())
                    .Replace("\"opening\":\"Farewell\",", "")
                    .Replace("\"archive\":[]", "\"archive\":[\"archive.retired-entry\"]");
                Assert.That(json, Does.Contain("archive.retired-entry"));
                Assert.That(json, Does.Not.Contain("\"opening\":"));
                var legacyFolder = Path.Combine(folder, "legacy");
                var legacy = new AtomicSaveStore(legacyFolder, hash, 1);
                Assert.That(legacy.Write(Slot, new SaveSnapshot(1, hash, Encoding.UTF8.GetBytes(json))).Succeeded, Is.True);
                var migrated = new StationSaves(legacyFolder, catalog, required);
                Assert.That(migrated.Load(Slot, boot, CreateTargets, Readiness, new Clock()).Succeeded, Is.True);
                Assert.That(boot.Current.Narrative.CaptureOpening(), Is.EqualTo(OpeningProgress.Complete));
                Assert.That(boot.Current.Archive.Capture(), Is.Empty);
                Assert.That(migrated.Save(Slot, boot.Current).Succeeded, Is.True);
                Assert.That(new AtomicSaveStore(legacyFolder, hash, 2).Read(Slot, out var rewritten).Succeeded, Is.True);
                Assert.That(rewritten.SchemaVersion, Is.EqualTo(2));
            });
        }

        [Test]
        public void ChangedScienceSnapshotAndInvalidPayloadCannotReplaceTheRunningSession()
        {
            WithStation((saves, boot, catalog, required) =>
            {
                Assert.That(boot.RestartWithPower(catalog, required, CreateTargets, Readiness, clock: new Clock()), Is.Empty);
                var run = boot.Current;
                Assert.That(saves.Save(Slot, run).Succeeded, Is.True);
                var modified = UnityEngine.Object.Instantiate(catalog);
                try
                {
                    var alteredHash = new string(catalog.RawSha256[0] == 'a' ? 'b' : 'a', 64);
                    modified.ReplaceSnapshot(catalog.Attribution, catalog.License, catalog.TransformVersion,
                        alteredHash, catalog.NormalizedSha256, catalog.TransformSha256, catalog.Events.ToArray());
                    var rejected = new StationSaves(folder, modified, required);
                    Assert.That(rejected.Load(Slot, boot, CreateTargets, Readiness, new Clock()).Code,
                        Is.EqualTo(ResultCode.IncompatibleVersion));
                    Assert.That(boot.Current, Is.SameAs(run));
                }
                finally { UnityEngine.Object.DestroyImmediate(modified); }

                string hash = ReadHash();
                var raw = new AtomicSaveStore(folder, hash, 2);
                Assert.That(raw.Write(Slot, new SaveSnapshot(2, hash, Encoding.UTF8.GetBytes("{}"))).Succeeded, Is.True);
                Assert.That(saves.Load(Slot, boot, CreateTargets, Readiness, new Clock()).MessageKey.value,
                    Is.EqualTo("save.result.backup-loaded"));
                var goodBackup = File.ReadAllBytes(PathToSlot + ".bak");
                Assert.That(saves.Save(Slot, boot.Current).Succeeded, Is.True);
                Assert.That(File.ReadAllBytes(PathToSlot + ".bak"), Is.EqualTo(goodBackup));
                File.Delete(PathToSlot + ".bak");
                Assert.That(raw.Write(new StableId("broken"), new SaveSnapshot(2, hash, Encoding.UTF8.GetBytes("{}")))
                    .Succeeded, Is.True);
                run = boot.Current;
                Assert.That(saves.Load(new StableId("broken"), boot, CreateTargets, Readiness, new Clock()).Code,
                    Is.EqualTo(ResultCode.StorageError));
                Assert.That(boot.Current, Is.SameAs(run));
            });
        }

        private void WithStation(Action<StationSaves, SessionBootstrap, StationScienceCatalog, StableId[]> test)
        {
            var asset = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>("Assets/_Game/Data/StationScienceCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            var required = new[] { asset.Events[0].Record.Id };
            var host = new GameObject("Save tests");
            try { test(new StationSaves(folder, asset, required), host.AddComponent<SessionBootstrap>(), asset, required); }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private string ReadHash()
        {
            using var reader = new BinaryReader(File.OpenRead(PathToSlot));
            reader.ReadInt32();
            reader.ReadInt32();
            return reader.ReadString();
        }

        private static SaveSnapshot Snapshot(byte value, int schema = 2, string hash = "content") =>
            new SaveSnapshot(schema, hash, new[] { value });

        private static IInteractable[] CreateTargets() => Array.Empty<IInteractable>();
        private static ResultCode Readiness(StableId id) => ResultCode.Applied;
        private sealed class Clock : ISessionClock { public double Seconds => 0; }
    }
}
