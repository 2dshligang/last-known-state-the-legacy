# S12 research report

Uses S01 API 1.0, [S09 ECHO](https://github.com/2dshligang/last-known-state-the-legacy/pull/16) and [S11 analysis](https://github.com/2dshligang/last-known-state-the-legacy/pull/18). The session owns StationResearch after RestartWithPower.

1. W15 renders the five section keys under report.section. Use StationResearch.TextKey for each choice. Leave Unspecified choices empty in the UI. Pass the complete ResearchDraft to Evaluate for preview or Submit for an explicit submission. Resolve the result's MessageKey through the English catalog. Rejected drafts can be revised without clearing evidence.
2. Read ReadEvidence for available fictional station results and their category. Use ReferenceContextKey and ReferenceSource for the separate scientific reference card from the reviewed S10 catalog. FeedbackReference provides a reference for applicable rejection messages. Results from the station always carry the Fiction label.
3. After acceptance, submit the existing quest.report action. The quest target also requires earlier milestones. Acceptance completes Dmitry's research report. S14 uses the report snapshot and the single change with subject research.dmitry-report and cause cause.research-complete. Queueing the report and checking ending readiness are separate steps owned by S14. Dispose subscriptions on session replacement.
4. CaptureAccepted returns the immutable accepted report, or null while incomplete. Save it with ECHO, sample and quest state using checkpoint version 1. Restore with researchCheckpoint in RestartWithPower. Restore does not emit another completion event. S16 owns durable storage.
5. Keep S01 IResearchService for status and subscriptions. Its legacy Submit overload returns complete-sections because it cannot represent alternatives, uncertainty or follow-up. Use StationResearch.Submit(ResearchDraft) for gameplay.

Developer 1 owns W15 presentation. In the scene, submit a report claiming confirmed life, revise it using the evidence, and verify that the quest advances once. Check that the fictional results and the reference cards remain easy to distinguish. Run LKS.Systems.Tests in Unity EditMode.
