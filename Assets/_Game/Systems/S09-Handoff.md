# S09 causal reconstruction

Uses S01 API 1.0 and [S08](https://github.com/2dshligang/last-known-state-the-legacy/pull/15). The three fictional clues are echo.stale-input, echo.controller-decision and echo.backup-exhausted, in that causal order. Show the backup model and missing interval as context. The historical DONKI training record remains separate.

1. Bind Current.Investigation after RestartWithPower. W11 calls Collect(id) when the player explicitly inspects a clue and SubmitSequence(ids) when the player submits an order. Read Conclusion and Capture after each action. Timeline playback does not collect clues.
2. Read the required S07 archive entry first. Use the returned MessageKey in the English catalog. Keep collected clues and let players revise the order after a rejected submission.
3. Show echo.freshness.rule for the fictional controller's one-minute engineering threshold and fifteen-second combined timestamp uncertainty. The archived reading is four minutes older than the controller decision. The rule evaluates suitability for that decision.
4. After confirmation, submit the existing quest.echo-evidence action and wait for the quest snapshot. The report prerequisite is still enforced by the existing quest graph. Read the confirmed conclusion rather than treating a tentative hypothesis as final.
5. Persist Capture with the archive and quest checkpoints. Pass investigationCheckpoint to RestartWithPower. Only a supported accepted order restores a confirmed conclusion. Completed ECHO quest progress requires its confirmed evidence. S16 owns durable storage.

Developer 1 owns the W11 UI and scene. Verify clue order feedback, incomplete submissions, repeat submissions and the transition into sampling. Developer 2 owns report integration.
