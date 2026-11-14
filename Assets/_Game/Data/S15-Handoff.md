# S15 release content checks

S15 uses the accepted English catalog and S14 ending. Developer 2 owns the catalog and audit code. Developer 1 owns scene, prefab, texture and UI remediation. The coordinator owns package and CI changes.

Use **LKS > Validation > Audit release content** for a static preflight. The batch entry point is `LKS.StationReleaseAudit.VerifyContent`. Inspect `Library/LKS-release-audit.txt` for the asset path, GUID, object hierarchy and owner. The gate checks catalog keys, source URL format and attribution, distinct science labels, serialized TMP/uGUI/TextMesh text including inactive scene objects and prefabs, UXML defaults, media reviews and known speech integrations.

For release acceptance call `LKS.StationReleaseAudit.VerifyRelease` via Unity batch mode. Set `LKS_RELEASE_EVIDENCE` to the absolute path of a JSON evidence file outside Assets. Example structure:

```json
{
  "playerLog": "C:/review/player.log",
  "textureReview": "Reviewer, build revision and evidence location",
  "mutedPlaythrough": "Reviewer, build revision and evidence location",
  "media": []
}
```

For each audio or video asset add a record with guid, sha256 of the imported source bytes, purpose, source, license, reviewer, listened and speechFree. Accepted purposes are ambience, music, environmental-effect and silent-video. Record original production or the source URL and license evidence. Listen to the complete asset before marking it speech-free. Review newly added media and changed bytes again. A filename supplies no listening evidence. Review runtime binaries and packages for automated speech or external media calls.

W17 binds `PlayerTextAudit.Record(surface, renderedText)` at the final text assignment or display callback in a development build. It logs rendered snapshots with prefix LKS_PLAYER_TEXT. Record changes once. NarrativeCue already records rendered captions. Instrument UI presenters and capture at least menu.main, quest.objective, interaction.prompt, interaction.error, archive.entry, science.label, credits.team, narrative.farewell and ending.final. Include error, pause, continue and optional-detail branches. Text drawn by an uninstrumented component requires manual review.

Capture the final line exactly as Dad... I did it. Review all on-screen paths, font coverage, clipping, baked texture text and images by inspection or OCR followed by a human check. Check archive observation, model and fiction badges in the player. Complete a muted run with someone other than the author when available. Keep source research files and verification evidence outside runtime assets.

The static gate checks common placeholder strings and Cyrillic. Human review is necessary for other languages and rendered content. Preview builds can run independently. The release workflow must call VerifyRelease once its evidence is ready.
