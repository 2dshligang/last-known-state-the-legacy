# S10 science catalog

Uses S01 API 1.0 and the accepted O02 provenance foundation. The editor importer pins the raw archive, normalizer and normalized snapshot. Changes require review and updated pins. Run LKS > Science > Import approved DONKI snapshot. Reimports retain the asset GUID. The player reads the serialized catalog offline.

Pass StationScienceCatalog.asset as IScienceCatalog to the session bootstrap. Display the record's category and original timestamp and keep null dose missing. The historical source is nasa-donki-sep-2017. EngineeringModel labels the instrument's ModelOutput entry. All records refer to the historical archive.

W12 binds sample.layered-mudstone to the layered outcrop and sample.iron-rich-rock to the comparison site. W13 binds sample.procedural-blank to the empty lab control. Use FictionalSample.All for the stable IDs, context keys and reference links. All three are fictional ASTERION definitions. No specimen measurements are supplied. The references provide scientific context for lakebed mudstone, mineral comparison and blank controls.

Developer 1 owns scene binding in W12 and W13. Developer 2 owns collection and analysis in S11. Run DonkiImporterTests in Unity EditMode and check that reimport leaves the catalog unchanged.
