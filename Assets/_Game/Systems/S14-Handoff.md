# S14 photograph and completed game

Uses S01 API 1.0, the [accepted S06 queue](https://github.com/2dshligang/last-known-state-the-legacy/pull/12) and S13 narrative cues. W15 owns scene presentation.

1. After report acceptance, call Current.Ending.QueueAcceptedReport on the explicit transmission action. A locally queued or delivered report meets the transmission requirement. No network or wait for delivery is required.
2. Resolve the existing quest.ending target at the photograph. Use Read and Request with actor.alexei and action.confirm. Readiness checks safe life support, confirmed ECHO, reviewed samples with valid blank, accepted report, transmission, and earlier quest milestones. Resolve the ending.result key from Readiness or the target. Repeated requests return completed.
3. Subscribe to Ending.Completion for subject quest.ending and cause cause.photograph-inspected. The completed quest is committed before the event dispatches. Capture the session checkpoint before playing the shot. Read Completion.Snapshot and TryGetFinalCue on binding or Continue to recover if presentation was interrupted. The cue requests portrait.dmitry-photograph and camera.final-photograph and resolves ending.final to exactly Dad... I did it.
4. Continue and photograph replay read the existing cue and leave the completed record intact. New Game starts a new session. The existing versioned quest checkpoint is the single completion marker. Persist it alongside the domain checkpoints. A restored completed session validates evidence and transmission and does not recheck live air or emit the event again. S16 owns atomic disk writes.
5. W15 should verify each blocked reason, queue from the accepted report UI, inspect the photograph, reload after the transition, and replay the cue. Read the exact final text and wait for deliberate player input before opening credits. Use no speech.

Run LKS.Systems.Tests in Unity EditMode. Scene layout and photograph staging belong to Developer 1.
