# W13 laboratory handoff

Developer 1 owns the terminal and its focus handling. Developer 2 owns the running instrument, power, quest gates and save state.

- [W12 PR #33](https://github.com/2dshligang/last-known-state-the-legacy/pull/33) provides the Station anchors and collection flow.
- [S11 PR #18](https://github.com/2dshligang/last-known-state-the-legacy/pull/18) provides StationSamples and SampleCheckpoint schema 1. The S01 API remains 1.0. IResearchService is the report interface. Instrument commands and readings come from StationSamples, and supply comes from StationPower at load.lab.
- [S16 PR #29](https://github.com/2dshligang/last-known-state-the-legacy/pull/29) persists the pending run and specimen phases; the terminal rebinds to the current session after a load.

Open Station.unity. Complete W12 including the empty control at the lab fixture and the collection confirmation at C. Aim at SAMPLE ANALYSIS and press E. The existing quest.analysis anchor is bound at runtime by StationInteraction. No scene migration is required.

Supply load.lab at the power bay. Run sample.procedural-blank before sample.layered-mudstone or sample.iron-rich-rock. While a run is active, switch specimens to compare context notes and the blank status, or return to the station and come back. A paused game, loss of focus or absent lab supply stops the displayed activity. The words "Analyzing" and the moving dots indicate activity, not a measured spectrum or percentage.

The first blank reports fictional background. Clean the chamber, repeat the blank, run both rocks and choose the interpretation supported by the control on each specimen. Cleaning clears old results and preserves collection. Confirm sample analysis after all three reviews with a passed blank. Return to the terminal to revisit the results.

The NASA context references are supplied by S10 for [layered mudstone](https://ntrs.nasa.gov/citations/20210013268), [iron mineral comparison](https://ntrs.nasa.gov/citations/20205001823) and [empty control context](https://ntrs.nasa.gov/citations/20130011130). The [verified challenge Resources](../../../Automation/docs/SCIENCE.md) inform the plain-language power trade-off. The NASA references are context for the activity. All instrument responses are fictional.

Run LaboratoryUsesLivePowerAndRestoresAnInterruptedRun in the station interaction Play Mode suite. For visual verification, open the terminal at 800x600 in a Windows build of Station. Check a run in progress, an outage, the failed and passed blank displays, and the reviewed evidence after reopening.

Next owner: Developer 1 can connect the report station to accepted S12 report commands. The laboratory advances quest.analysis through the existing quest target.
