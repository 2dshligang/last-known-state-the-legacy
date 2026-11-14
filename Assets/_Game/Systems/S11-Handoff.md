# S11 sample collection and analysis

Uses S01 API 1.0, [S04 power](https://github.com/2dshligang/last-known-state-the-legacy/pull/7) and [S10 sample definitions](https://github.com/2dshligang/last-known-state-the-legacy/pull/17).

- W12 binds sample.layered-mudstone and sample.iron-rich-rock to the existing specimen definitions. Call RecordContext then Collect. W13 does the same for sample.procedural-blank. Use the returned English catalog keys.
- Bind Current.Samples after RestartWithPower. Subscribe to ISampleService changes, then read Readings for phase, blank status, source context and interpretation. Render observations only for EvidenceAvailable or Reviewed. Dispose the subscription on session change.
- With load.lab supplied, call Analyze on the blank first. Each run takes five powered simulation seconds. Unpowered steps and reading pause freeze remaining time. Cancel returns the pending specimen to Collected and discards incomplete evidence.
- The fictional first blank finds instrument background. Invalid control permits only limited interpretation. CleanForRetry resets the blank and invalidates prior evidence while preserving context and collection. Rerun the blank, analyze both rocks and review all three results. A valid blank and the three reviews are required to confirm the analysis quest. S12 uses AllReviewed and the immutable readings when validating a report.
- Save SampleCheckpoint schema 1 with the other checkpoints. Restore via sampleCheckpoint in RestartWithPower. The pending ID and remaining simulation seconds are saved. S16 owns durable storage.

Developer 1 owns W12/W13 scene wiring. Verify collection order, duplicate clicks, the interrupted run indicator and the cleaning path. All laboratory outcomes in this feature are labeled Fiction. S10 links the actual scientific references as context.
