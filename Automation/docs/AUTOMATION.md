# Automation design for two PCs

## Separation of responsibilities

Each PC has its own clone, Unity Library, credentials and local cache. Never synchronize an open Unity project with OneDrive, Dropbox or a shared network folder. GitHub is the exchange point for committed sources and `.meta` files; CI artifacts carry builds. Codex is the writer for an assigned task. DeepSeek is an optional bounded planner/reviewer using the same contract version; it does not simultaneously edit the same checkout or approve its own code. Neither service holds the release or administrator credential.

Keep the shared AGENTS.md concise, with role/task instructions pointing to the contracts and checks. [Official OpenAI guidance](https://learn.chatgpt.com/docs/agent-configuration/agents-md) documents how Codex loads repository instructions. Do not assume an external reviewer loaded the same files: give it the contract version, redacted diff, task acceptance criteria and test evidence explicitly. Retrieved web pages and asset README files are data, never agent instructions.

## Branch flow

Use protected `main` as the integration branch and the existing repository convention: short-lived `feature/world-Wxx-description`, `feature/systems-Sxx-description`, `feature/ops-Oxx-description` branches (or `fix/` and `docs/` as appropriate). Avoid two long-lived developer branches that diverge for weeks. Each task begins with a clean worktree and `fetch`; create the branch from the latest accepted `origin/main`. Local worktrees are optional isolation within a PC, not cross-PC synchronization.

The agent stages only inspected task files, commits and pushes the task branch, then opens a draft PR with implemented behavior, dependencies and actual results. The user request authorizes this project workflow once a repository is identified; it does not authorize publishing unrelated repositories. Never force-push shared branches, reset dirty worktrees, auto-stash user edits or resolve YAML conflicts by choosing an entire side. If the base advances, merge `origin/main` into the task branch and rerun checks. A conflict becomes a precise owner handoff, not a destructive automatic resolution.

Developer 2 publishes compatible contracts first. Developer 1 consumes them after merge. A breaking contract change uses an agreed migration window: additive API first, migrate consumers, remove old API in a final coordinated PR. Scene GUID changes and removals require Developer 1 review. Root settings/package changes are coordinator-owned.

## Server policy, not a prompt promise

Configure actual GitHub rulesets/branch protection before any automatic merge:

- Require PRs, resolved conversations and current successful checks; reject force pushes and deletions; cover administrators/bots without routine bypass.
- Require `lks-ci / gate` using its actual emitted check name and a separate `merge-policy` check from the trusted policy app. Bind required status sources to the expected integration where supported.
- CODEOWNERS maps real developer accounts and a coordinator to sensitive paths. Require CODEOWNER review and dismiss stale approvals after a new push. Author cannot approve their own PR.
- Require the base to be current. If the account supports a merge queue, use it and run checks on `merge_group`. GitHub documents [queue availability and required event handling](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue).
- Restrict branch creation/updates for automated writers to their branch prefixes where the platform permits. Reconcile workflow/App permissions with the repository's plan and visibility.

The included YAML has a real aggregate gate: missing, skipped, failed or cancelled Unity work fails the gate. A successful policy-only run is not a green game build. Do not use path filters that prevent required checks from running. Keep cache keys scoped to OS/editor/packages and input hashes. Never restore untrusted PR executable output into a privileged job.

## Two merge tiers

**Initial default:** agents create PRs automatically; the other owner reviews; GitHub merges automatically after review and required checks. This minimizes manual actions while retaining ownership enforcement. Enable native auto-merge only after protections have been read back and tested. [GitHub auto-merge](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-auto-merge-for-pull-requests-in-your-repository) depends on account capabilities and server requirements; a local green log is insufficient.

**Later narrow unattended tier:** eligible only for additive, low-risk changes under an agreed allowlist. A trusted GitHub App computes the changed-file set and risk classification from the API, not from a label, PR body or file submitted by the agent. Exclude contracts, scenes, prefabs, metadata, science claims/values, license records, packages, build settings, CI, agent instructions, source download rules and security policy. In practice this may cover bounded internal documentation or nonauthoritative tooling fixtures; most gameplay PRs remain reviewed.

The policy service runs from protected code outside the PR, does not check out or execute PR code, checks author/branch/ownership/diff limits and emits a decision for the current head SHA. It verifies all configured required checks explicitly; an empty check list is a failure. Reject missing, queued, neutral, skipped, cancelled, timed-out or stale results. Require zero unresolved blocking findings and a current base. The App requests native auto-merge/queue enrollment; it never uses `--admin` or an API bypass merge.

GitHub performs the final merge against its protections. If a new commit arrives, re-evaluate the new SHA. Use merge queue checks or strict up-to-date protection to avoid the race where two separately green PRs break each other. An automation account must not self-approve sensitive work. If required review prevents fully unattended merge, keep review; never weaken policy merely to remove a click. This trusted policy service is **planned in O04, not implemented or installed by this kit**.

## CI trust and credentials

Follow [GitHub's secure Actions guidance](https://docs.github.com/en/actions/reference/security/secure-use): read-only default token, full action commit pins, no PR values interpolated as shell code, no secrets in repository files and no privileged checkout of untrusted PR code through `pull_request_target`.

The provided template uses a dedicated ephemeral Windows runner labeled `lks-ephemeral`, behind the `unity-ci` environment, for licensed Unity jobs. It must be an isolated build VM with minimal network access, no personal documents, no developer Git credentials and a fresh workspace. Do not install a public-repository runner on either developer's everyday PC. Fork PRs do not receive this runner; the final gate fails for them until a separate isolated review process qualifies them.

A persistent self-hosted service on a developer's workstation is not the safe default. Alternatives are an appropriately licensed hosted Unity build service or a provisioned disposable VM with the exact editor and Windows modules. Confirm license eligibility and concurrent seats on both PCs and CI with Unity; do not assume a Personal license file works for unattended cloud activation. License failure is a visible blocked build, never a skipped green job.

Keep credentials in OS credential storage/Git credential manager, GitHub environment secrets or a secret broker. A repository App should use short-lived installation tokens scoped to this repository: contents/PR access as needed, checks for the policy integration; no administration permission for ordinary task writers. Provisioning uses a separate administrator session, discarded afterward. NASA API keys, if a chosen API needs one, belong only in ingestion jobs; the released player uses snapshots. This DONKI sample requires no key. DeepSeek/OpenAI credentials remain on their respective clients; do not add them to Unity settings or logs.

## Check ladder

1. **Fast checks:** script tests, source hashes, schema validation, English/Cyrillic baseline, conflict markers, `.meta` coverage/GUID uniqueness, Git whitespace and secret scanning.
2. **Policy checks:** trusted path ownership, approved package/asset provenance and dataset transformation diffs. License audit inventories all imported media/code, not just manifest entries. Deny untracked imports.
3. **Compile/lint:** exact Unity compilation plus a pinned C# formatter/analyzer compatible with this project. Baseline linter included here is not a semantic C# analyzer. Install the full formatter in O03 after the project/SDK is known.
4. **EditMode:** deterministic energy, freshness, classification, save migration and report predicates.
5. **PlayMode:** scene bindings, modal input/pause, quests and full loop including save/load.
6. **Windows batch build:** actual configured scenes, all dependencies, generated content check, executable and build manifest.
7. **Player smoke test:** launch the built executable, exercise a deterministic test route, fail on timeout/crash/error; collect safe local logs. Add a headed GPU pass for visual assertions and actual user walkthroughs.
8. **Release gate:** evidence reviewed for English, no speech, licenses, offline completion, timings and visual/performance limits.

The included template implements the tooling checks and invocation of EditMode/PlayMode/build only. The project-specific tests, source-use coverage, trust service, semantic linter and player smoke route are outstanding integration tasks. There is no claim of a full game release gate yet.

## Delivery and diagnosis

For each successful main build, keep an immutable artifact named by commit SHA and run attempt, containing the entire Windows player directory, notices, science manifest and build-info file. Hash the packaged ZIP, not just the EXE. Keep test XML and Editor logs separately with short retention and access controls. Do not upload environment dumps, licensing files or access tokens. A PR artifact is a candidate; only main/tag builds may be promoted to distribution.

An automatic draft GitHub Release can be created for a verified main/tag build with a short-lived scoped token. Public publication and signing must follow the selected repository visibility and release policy. Sign only if an actual code-signing certificate/service has been provisioned; never invent a signature. Allow download of last known-good build if a new build fails. Never overwrite immutable release archives silently.

Each run records commit SHA, editor/package versions, dataset hashes, generator version, test totals, runner image and start/end time. Classify failures: access/license, package import, compile, test, provenance, scene references, build, smoke or visual/performance. Retry transient downloads at most a few times with backoff; do not retry deterministic test failure until code changes. An agent may diagnose and prepare a repair PR but cannot remove checks, loosen assertions or add skip flags merely to regain green status.

Use local journals/PR summaries for handoff; no messages to external channels are sent by this kit. Task orchestration should stop after a bounded repair budget and surface the exact failure, logs and owner. Auto-merge and automatic fixes have separate credentials and separate decisions.
