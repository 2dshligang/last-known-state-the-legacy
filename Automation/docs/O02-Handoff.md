# O02: Candidate quarantine and approved acquisition

Base: O01 [PR #3](https://github.com/2dshligang/last-known-state-the-legacy/pull/3);
the downloader, portable CI and DONKI snapshot came from
[PR #1](https://github.com/2dshligang/last-known-state-the-legacy/pull/1).
The approved source lock remains schema 1; quarantine receipts use schema 1.
The science source identity remains `nasa-donki-sep-2017`.

1. Discover a candidate on an official provider page and save the bytes using an
   authorized download route. Do not open or execute candidate contents.
2. From Automation, run `node tools/quarantine.mjs <candidate.json> <local-file>`.
   Supply id, sourceUrl, downloadUrl, author, license, termsUrl, attribution,
   query, retrievedAtUtc, instrument, units and transformVersion. Explicitly
   record unknown or inapplicable fields; unknown rights cannot be approved.
   Keep the terms evidence separately pending review. The receipt and opaque
   payload stay under ignored work/quarantine, never under Assets or sources/raw.
3. The coordinator and the relevant asset/data owner review the provider,
   terms, rights scope, content, and hash. If approved, submit a reviewed lock
   diff with the exact size and digest plus provenance and any dataset query,
   dates, instruments, units and transform version. Set `licenseUrl` to the
   canonical HTTPS rights page. Save its URL on line one of
   `sources/terms/<id>.txt`, followed by a short excerpt that reviewers can
   compare against the provider's terms. Set `termsEvidence.path` and
   `termsEvidence.sha256` in the lock entry to pin those exact bytes.
   Only the exact previously accepted DONKI lock entry is exempt.
   Never flip a quarantine receipt into an approval.
4. Run `node tools/fetch-locked.mjs` from Automation and then
   `npm test` and `npm run verify`. The approved fetch validates the entire
   lock before any network work and retrieves independently of quarantine.
   An existing invalid copy fails; retain the previously verified snapshot.

No archive extraction or Unity import is provided. ZIP entries remain opaque;
any future extraction needs a separate reviewed implementation with bounded
expansion and entry count, safe names, links and extension policy. Keep
procedural substitutes when art ingestion is blocked. Missing scientific raw
data fails verification. No external content or challenge catalog is changed
by this handoff. Before updating the catalog, obtain the official Resources
listing and review a diff against the pinned catalog from PR #1.

Next owner: automation coordinator for O02 review; then O03 for the licensed
runner and Unity gate. Developer 1 owns any art import; Developer 2 owns any
runtime science integration.
