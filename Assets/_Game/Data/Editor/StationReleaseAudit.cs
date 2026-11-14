using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LKS
{
    public static class StationReleaseAudit
    {
        private const string CatalogPath = "Assets/_Game/Data/EnglishContentCatalog.cs";
        private const string EvidenceVariable = "LKS_RELEASE_EVIDENCE";
        private const string ReportPath = "Library/LKS-release-audit.txt";
        private static readonly string[] RequiredRuntimeSurfaces =
        {
            "menu.main", "quest.objective", "interaction.prompt", "interaction.error",
            "archive.entry", "science.label", "credits.team", "narrative.farewell", "ending.final"
        };

        private static readonly Regex Cyrillic =
            new Regex(@"[\u0400-\u052F\u1C80-\u1C8F\u2DE0-\u2DFF\uA640-\uA69F]");
        private static readonly Regex Placeholder = new Regex(
            @"\A\s*(?:new text|sample text|text mesh pro|lorem ipsum|todo|tbd|placeholder)(?:\b|\z)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SpeechIntegration = new Regex(
            @"\b(?:System\.Speech|Windows\.Media\.SpeechSynthesis|Microsoft\.CognitiveServices\.Speech|SpeechSynthesizer|TextToSpeech)\b|\b(?:texttospeech\.googleapis\.com|api\.elevenlabs\.io|text-to-speech|com\.cesarsalinas\.tts)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".m4a", ".aac", ".wma",
            ".mod", ".it", ".s3m", ".xm", ".opus", ".mpeg", ".mpg", ".mp4", ".mov", ".webm", ".avi", ".mkv", ".wmv"
        };

        [Serializable]
        public sealed class MediaReview
        {
            public string guid, sha256, purpose, source, license, reviewer;
            public bool listened, speechFree;
        }

        [Serializable]
        private sealed class Evidence
        {
            public string playerLog, textureReview, mutedPlaythrough;
            public MediaReview[] media;
        }

        [MenuItem("LKS/Validation/Audit release content")]
        public static void VerifyContent() => Run(false);

        public static void VerifyRelease() => Run(true);

        private static void Run(bool release)
        {
            var issues = new List<string>();
            var evidence = ReadEvidence(issues);
            CheckCatalog(issues);
            string[] paths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(path))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (string path in paths)
            {
                try { CheckAsset(path, evidence?.media, issues); }
                catch (Exception error) when (error is IOException || error is ArgumentException ||
                    error is InvalidOperationException || error is System.Xml.XmlException)
                {
                    Add(issues, path, "Cannot inspect asset: " + error.Message);
                }
            }
            CheckSpeech("Packages/manifest.json", issues);
            CheckSpeech("Packages/packages-lock.json", issues);
            if (release)
                CheckRuntime(evidence, issues);

            var report = new StringBuilder();
            report.AppendLine(release ? "LKS release content audit" : "LKS static content audit");
            report.AppendLine($"Assets inspected: {paths.Length}. Findings: {issues.Count}.");
            if (!release)
                report.AppendLine("Runtime text, texture review and muted playthrough require VerifyRelease.");
            foreach (string issue in issues)
                report.AppendLine(issue);
            File.WriteAllText(ReportPath, report.ToString());
            if (issues.Count != 0)
                throw new BuildFailedException($"Content audit failed. See {ReportPath}.\n" +
                    string.Join("\n", issues.Take(10)));
            Debug.Log($"LKS audit passed. {ReportPath}");
        }

        public static string TextProblem(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (Cyrillic.IsMatch(value))
                return "Cyrillic text";
            if (Placeholder.IsMatch(value) || value.StartsWith("[Missing text:", StringComparison.Ordinal))
                return "Untranslated default or missing text";
            return null;
        }

        public static string KeyProblem(EnglishContentCatalog catalog, string key)
        {
            try { return TextProblem(catalog.Resolve(new StableId(key), false)); }
            catch (Exception error) when (error is KeyNotFoundException || error is ArgumentException)
            {
                return "Missing or invalid English catalog key";
            }
        }

        public static string MediaProblem(string guid, string hash, IEnumerable<MediaReview> reviews)
        {
            var matches = reviews?.Where(review => review != null && review.guid == guid).ToArray();
            if (matches == null || matches.Length != 1)
                return "Media requires exactly one review for this asset GUID";
            var entry = matches[0];
            if (!string.Equals(entry.sha256, hash, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(entry.source) || string.IsNullOrWhiteSpace(entry.license) ||
                string.IsNullOrWhiteSpace(entry.reviewer))
                return "Media review is missing provenance or applies to different bytes";
            if (entry.purpose != "ambience" && entry.purpose != "music" &&
                entry.purpose != "environmental-effect" && entry.purpose != "silent-video")
                return "Media purpose is outside the speech-free policy";
            return entry.listened && entry.speechFree ? null : "Human speech review is required";
        }

        public static IReadOnlyList<string> RuntimeProblems(IEnumerable<string> lines, IEnumerable<string> requiredSurfaces)
        {
            var issues = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                int start = line.IndexOf(PlayerTextAudit.LogPrefix, StringComparison.Ordinal);
                if (start < 0)
                    continue;
                try
                {
                    var entry = JsonUtility.FromJson<PlayerTextAudit.Entry>(
                        line.Substring(start + PlayerTextAudit.LogPrefix.Length));
                    if (entry == null || string.IsNullOrWhiteSpace(entry.surface) || string.IsNullOrWhiteSpace(entry.text))
                    {
                        issues.Add("Malformed runtime text capture");
                        continue;
                    }
                    seen.Add(entry.surface);
                    string problem = entry.surface == "ending.final" && entry.text != "Dad... I did it."
                        ? "Ending punctuation differs from the approved text" : TextProblem(entry.text);
                    if (problem != null)
                        issues.Add($"{entry.surface}: {problem}");
                }
                catch (ArgumentException) { issues.Add("Malformed runtime text capture"); }
            }

            foreach (string surface in requiredSurfaces)
                if (!seen.Contains(surface))
                    issues.Add($"No runtime capture for {surface}");
            return issues.AsReadOnly();
        }

        private static Evidence ReadEvidence(List<string> issues)
        {
            string path = Environment.GetEnvironmentVariable(EvidenceVariable);
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                var evidence = JsonUtility.FromJson<Evidence>(File.ReadAllText(path));
                if (evidence == null)
                    Add(issues, path, "Invalid review evidence");
                return evidence;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                Add(issues, path, "Cannot read review evidence: " + error.Message);
                return null;
            }
        }

        private static void CheckRuntime(Evidence evidence, List<string> issues)
        {
            if (evidence == null || string.IsNullOrWhiteSpace(evidence.playerLog) || !File.Exists(evidence.playerLog))
                Add(issues, "Runtime player log", "Development-player capture file is required");
            else
                foreach (string problem in RuntimeProblems(File.ReadLines(evidence.playerLog), RequiredRuntimeSurfaces))
                    Add(issues, evidence.playerLog, problem);
            if (string.IsNullOrWhiteSpace(evidence?.textureReview))
                Add(issues, "Texture and screen review", "Record reviewer and evidence for rendered text and textures");
            if (string.IsNullOrWhiteSpace(evidence?.mutedPlaythrough))
                Add(issues, "Muted playthrough", "Record reviewer and evidence for a muted run");
        }

        private static void CheckCatalog(List<string> issues)
        {
            try
            {
                var catalog = EnglishContentCatalog.CreateDefault();
                foreach (string key in RequiredKeys.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                    .Concat(catalog.Keys.Select(key => key.ToString())))
                {
                    string problem = KeyProblem(catalog, key);
                    if (problem != null)
                        Add(issues, CatalogPath, key + ": " + problem);
                }
                var categories = Enum.GetValues(typeof(EvidenceKind)).Cast<EvidenceKind>()
                    .Select(kind => catalog.Resolve(new StableId(EnglishContentCatalog.CategoryKey(kind)), false));
                if (categories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                    Add(issues, CatalogPath, "Evidence category labels must be distinct");
                foreach (var sample in FictionalSample.All)
                    CheckReference(issues, sample.Reference, sample.Id.ToString(), CatalogPath);
                var science = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(DonkiCatalogPath);
                if (science == null || string.IsNullOrWhiteSpace(science.Attribution) ||
                    TextProblem(science.Attribution) != null)
                    Add(issues, DonkiCatalogPath, "A reviewed catalog with English attribution is required");
                else
                    foreach (var entry in science.Events)
                        CheckReference(issues, entry.Record.Source, entry.OriginalId, DonkiCatalogPath);
            }
            catch (Exception error) when (error is ArgumentException || error is KeyNotFoundException || error is FormatException)
            {
                Add(issues, CatalogPath, error.Message);
            }
        }

        private const string DonkiCatalogPath = "Assets/_Game/Data/StationScienceCatalog.asset";

        private static void CheckReference(List<string> issues, Uri url, string id, string path)
        {
            if (url == null || !url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(url.UserInfo))
                Add(issues, path, "Invalid HTTPS source citation for " + id);
        }

        private static void CheckAsset(string path, MediaReview[] reviews, List<string> issues)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".unity")
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        CheckHierarchy(root, path, issues);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            else if (extension == ".prefab")
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    Add(issues, path, "Prefab could not be opened");
                else
                    issues.AddRange(InspectSerializedText(root, path));
            }
            else if (extension == ".uxml")
            {
                var document = System.Xml.Linq.XDocument.Load(path);
                foreach (var attribute in document.Descendants().SelectMany(element => element.Attributes())
                    .Where(attribute => attribute.Name.LocalName == "text" || attribute.Name.LocalName == "label"))
                    CheckText(path, attribute.Name.LocalName, attribute.Value, issues);
            }

            if (MediaExtensions.Contains(extension) || AssetImporter.GetAtPath(path) is AudioImporter ||
                AssetImporter.GetAtPath(path) is VideoClipImporter)
            {
                using var stream = File.OpenRead(path);
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                string problem = MediaProblem(AssetDatabase.AssetPathToGUID(path), hash, reviews);
                if (problem != null)
                    Add(issues, path, problem);
            }

            if ((extension == ".cs" || extension == ".asmdef" || extension == ".asmref") &&
                !path.Split('/').Any(segment => segment == "Tests" || segment == "Editor"))
                CheckSpeech(path, issues);
            if (extension == ".dll" && AssetImporter.GetAtPath(path) is PluginImporter plugin &&
                plugin.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64))
                Add(issues, path, "Review the Windows runtime binary for speech integration before release");
        }

        private static void CheckSpeech(string path, List<string> issues)
        {
            if (SpeechIntegration.IsMatch(File.ReadAllText(path)))
                Add(issues, path, "Speech integration detected");
        }

        public static IReadOnlyList<string> InspectSerializedText(GameObject root, string assetPath)
        {
            var issues = new List<string>();
            CheckHierarchy(root, assetPath, issues);
            return issues.AsReadOnly();
        }

        private static void CheckHierarchy(GameObject root, string path, List<string> issues)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                for (Type type = component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.FullName != "TMPro.TMP_Text" && type.FullName != "UnityEngine.UI.Text" &&
                        type.FullName != "UnityEngine.TextMesh")
                        continue;
                    using var serialized = new SerializedObject(component);
                    var property = serialized.FindProperty("m_text") ?? serialized.FindProperty("m_Text");
                    CheckText(path, HierarchyPath(component.transform), property?.stringValue, issues);
                    break;
                }
            }
        }

        private static string HierarchyPath(Transform item) => item.parent == null
            ? item.name : HierarchyPath(item.parent) + "/" + item.name;

        private static void CheckText(string path, string location, string text, List<string> issues)
        {
            string problem = TextProblem(text);
            if (problem != null)
                Add(issues, path, $"{location}: {problem}");
        }

        private static void Add(List<string> issues, string path, string issue)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            bool world = path.StartsWith("Assets/_Game/", StringComparison.Ordinal) &&
                new[] { "/World/", "/Art/", "/Scenes/", "/Prefabs/", "/Cinematics/" }.Any(path.Contains);
            string owner = world ? "Developer 1" : path.StartsWith("Packages/", StringComparison.Ordinal)
                ? "Coordinator" : "Developer 2";
            issues.Add($"{owner} | {path} | GUID {(string.IsNullOrEmpty(guid) ? "n/a" : guid)} | {issue}");
        }

        private const string RequiredKeys = @"
menu.new-game menu.continue menu.pause menu.resume
menu.credits menu.exit credits.team credits.sources
action.confirm action.recover-power action.acknowledge prompt.interact
prompt.inspect quest.arrival quest.safe-power quest.echo-evidence
quest.sampling quest.analysis quest.report quest.ending
quest.result.applied quest.result.completed quest.result.prerequisite quest.result.resources
quest.result.evidence quest.result.invalid-action quest.result.unavailable power.result.applied
power.result.unchanged power.result.insufficient power.result.unknown-load power.result.invalid-allocation
power.result.unavailable shelter.phase.waiting shelter.phase.ready shelter.phase.moving
shelter.phase.interrupted shelter.phase.sheltered shelter.result.unavailable shelter.result.stale-reading
shelter.result.wait-for-briefing shelter.result.already-requested shelter.result.actuator-unpowered shelter.result.started
logistics.result.unavailable logistics.result.invalid-choice logistics.result.unknown-message logistics.result.already-queued
logistics.result.queued archive.result.read archive.result.already-read archive.result.unavailable
evidence.observation evidence.engineering-model evidence.fiction evidence.unknown
archive.donki-sep.summary archive.donki-sep.detail archive.power-model.summary archive.power-model.detail
archive.arrival.summary archive.arrival.detail science.organics science.food
echo.stale-input.caption echo.controller-decision.caption echo.backup-estimate.caption echo.missing-interval.caption
echo.backup-exhausted.caption echo.result.unavailable echo.result.unknown-clue echo.result.read-archive
echo.result.collected echo.result.already-collected echo.result.invalid-selection echo.result.missing-clues
echo.result.timing-before-decision echo.result.decision-before-outage echo.result.uncertain-reading echo.result.confirmed
echo.result.already-confirmed echo.freshness.rule sample.layered-mudstone.context sample.iron-rich-rock.context
sample.procedural-blank.context sample.result.context-recorded sample.result.already-recorded sample.result.record-context
sample.result.already-collected sample.result.collected sample.result.already-started sample.result.collect-first
sample.result.lab-busy sample.result.run-blank sample.result.power-lab sample.result.analysis-started
sample.result.no-active-run sample.result.cancelled sample.result.failed-blank-first sample.result.cleaned
sample.result.already-reviewed sample.result.await-evidence sample.result.reviewed sample.result.evidence-ready
sample.result.unknown-sample sample.result.unavailable sample.interpretation.pending sample.interpretation.limited
sample.layered-mudstone.observation sample.layered-mudstone.interpretation sample.iron-rich-rock.observation sample.iron-rich-rock.interpretation
sample.procedural-blank.observation sample.procedural-blank.interpretation report.result.already-accepted report.result.unavailable
report.result.complete-sections report.result.life-unsupported report.result.confirm-echo report.result.valid-blank
report.result.contradictory-blank report.result.cite-blank report.result.cite-reviewed-samples report.result.duplicate-observation
report.result.interpretation-needed report.result.alternative-needed report.result.state-uncertainty report.result.follow-up-needed
report.result.accepted report.section.observations report.section.interpretation report.section.alternatives
report.section.uncertainty report.section.follow-up report.choice.observation.layersvisible report.choice.observation.ironmineralsigns
report.choice.observation.blankpassed report.choice.observation.blankshowedbackground report.choice.interpretation.sedimentmayhavesettled report.choice.interpretation.formationremainsuncertain
report.choice.interpretation.lifeconfirmed report.choice.alternative.winddeposition report.choice.alternative.latermineralchange report.choice.alternative.noalternatives
report.choice.uncertainty.originuncertain report.choice.uncertainty.moreevidenceneededforlife report.choice.uncertainty.nouncertainty report.choice.follow-up.comparenearbylayerswithblank
report.choice.follow-up.checkmineralswithfreshblank report.choice.follow-up.none narrative.farewell narrative.arrival
narrative.research-context narrative.action.continue narrative.action.skip narrative.action.replay
archive.dmitry-notes.summary archive.dmitry-notes.detail ending.result.unavailable ending.result.completed
ending.result.restore-air ending.result.confirm-echo ending.result.review-samples ending.result.accept-report
ending.result.queue-report ending.result.finish-objectives ending.result.ready ending.action.replay
ending.final
";
    }
}
