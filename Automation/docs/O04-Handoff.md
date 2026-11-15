# O04 trusted merge preflight

The local preflight reads GitHub data and prints decision evidence. Run it
from an accepted, clean checkout of main using Node 22 or later. It imports
no PR code, checks out no PR files, and makes no merge or settings changes.
The GitHub token must be able to read contents, pull requests, checks,
Actions, collaborators, and main branch protection for this repository.
Provide it in the process environment using the approved credential store.

```powershell
node Automation/tools/review-main.mjs <pr-number> <reviewed-head-sha> <reviewed-main-sha>
```

The owner mapping pins GitHub user IDs from accepted work:

- Developer 1: `chitkid`, ID `185981598`, [W18](https://github.com/2dshligang/last-known-state-the-legacy/commit/48bf3f5).
- Developer 2: `2dshligang`, ID `201782954`, [S17](https://github.com/2dshligang/last-known-state-the-legacy/commit/2537196).

An affected owner other than the author must have a current GitHub approval.
Every PR needs an independent approval, and shared paths require the other
owner. Renames are evaluated at both paths. The output records both commit
SHAs and the GitHub review and check IDs. Changes to workflows and merge
policy go through separate manual review. An unattended allowlist is outside
the current scope.

## Activate server settings

The repository administrator owns activation. Use GitHub branch protection
for main with the following settings:

1. Require a pull request and one approval. Dismiss stale approvals,
   require approval of the latest reviewable push, and require conversation
   resolution. Apply the rules to administrators and keep bypass lists empty.
   Block force pushes and branch deletion.
2. Require **Tooling validation only** from GitHub Actions and require
   branches to be current with main. Workflow ID is `418214576` and its
   path is `.github/workflows/lks-tooling.yml`. This check covers O03
   portable tooling. Unity validation for gameplay changes runs locally
   under the accepted O03 process.
3. Verify the live protections through the API. Run the preflight against
   a current same-repository test PR with a distinct owner approval.
   From the GitHub merge box, use native auto-merge only after a passing
   preflight and independent review. Recheck if the base or head moves.
   Confirm through a deliberate failed check and a new push after approval
   that GitHub blocks the merge.

The initial integration supports strict current-base branch protection.
A merge queue needs separate validation against merge-group evidence and
must be treated as a separate policy mode before activation.

The branch protection API returned HTTP 403 and requires GitHub Pro or
public visibility for this repository. The repository remains private.
The preflight fails closed if native settings cannot be read. Keep native
auto-merge disabled until protection is available, verified, and exercised.
The coordinator owns the trusted script and tests. The administrator owns
GitHub settings.
