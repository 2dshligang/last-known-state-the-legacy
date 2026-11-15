import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const ownerIds = { world: 185981598, systems: 201782954 };
const owners = new Set(Object.values(ownerIds));
const workflowId = 418214576;
const workflowPath = '.github/workflows/lks-tooling.yml';
const checkName = 'Tooling validation only';
const actionsAppId = 15368;
const repository = '2dshligang/last-known-state-the-legacy';

const world = /^(?:Assets\/_Game\/(?:World|Art|Scenes|Prefabs|Cinematics|Editor\/World)|ArtSource\/Developer1)\//;
const systems = /^Assets\/_Game\/(?:Systems|Quests|Science|Data|Tests|Contracts)\//;
const sensitive = /^(?:\.github\/|Automation\/(?:tools|templates|sources)\/|(?:.*\/)?AGENTS\.md$)/i;
const decisionStates = new Set(['APPROVED', 'CHANGES_REQUESTED', 'DISMISSED']);
const sha = /^[a-f0-9]{40}$/;

function requireCondition(condition, message) {
    if (!condition) throw new Error(message);
}

export function verifyPolicy(snapshot, expected) {
    const { pr, currentBase, comparison, files, reviews, conversation, check, protection, permissions } = snapshot;
    requireCondition(pr.state === 'open' && !pr.draft && pr.base.ref === 'main', 'Expected an open, ready PR targeting main');
    requireCondition(sha.test(expected.head) && sha.test(expected.base) && pr.head.sha === expected.head,
        'PR head changed');
    requireCondition(pr.base.sha === expected.base && currentBase === expected.base &&
        comparison.behind_by === 0 && comparison.merge_base_commit?.sha === expected.base, 'PR base changed or head is behind main');
    requireCondition(pr.head.repo?.id === pr.base.repo?.id && pr.base.repo?.full_name === repository,
        'Fork or unrecognized repository');
    const author = pr.user.id;
    requireCondition(owners.has(author), 'Unrecognized PR author');
    requireCondition([...owners].every(id => ['admin', 'maintain', 'write'].includes(permissions[id])),
        'Registered owners must retain repository write access');

    const reviewPolicy = protection.required_pull_request_reviews;
    const requiredChecks = protection.required_status_checks;
    requireCondition(protection.enforce_admins?.enabled === true &&
        protection.required_conversation_resolution?.enabled === true &&
        reviewPolicy?.dismiss_stale_reviews === true && reviewPolicy.require_last_push_approval === true &&
        reviewPolicy.required_approving_review_count >= 1 &&
        Object.values(reviewPolicy.bypass_pull_request_allowances ?? {}).every(list => Array.isArray(list) && list.length === 0) &&
        protection.allow_force_pushes?.enabled === false && protection.allow_deletions?.enabled === false,
        'Required native branch protections are missing');
    requireCondition(requiredChecks?.strict === true && requiredChecks.checks?.length === 1 &&
        requiredChecks.checks[0].context === checkName && requiredChecks.checks[0].app_id === actionsAppId,
        'Expected strict main protection with the pinned GitHub Actions check');

    requireCondition(files.length > 0 && files.length === pr.changed_files, 'Changed-file inventory is empty or incomplete');
    const requiredOwners = new Set();
    for (const file of files) {
        const paths = [file.filename];
        if (file.status === 'renamed') {
            requireCondition(typeof file.previous_filename === 'string' && file.previous_filename.length > 0,
                'Rename missing its original path');
        }
        if (file.previous_filename) paths.push(file.previous_filename);
        for (const name of paths) {
            requireCondition(typeof name === 'string' && name.length > 0, 'Invalid changed path');
            requireCondition(!sensitive.test(name), 'Workflow or policy change requires separate manual review');
            if (world.test(name)) requiredOwners.add(ownerIds.world);
            else if (systems.test(name)) requiredOwners.add(ownerIds.systems);
            else for (const id of owners) requiredOwners.add(id);
        }
    }
    requiredOwners.delete(author);

    const decisions = new Map();
    for (const review of [...reviews].sort((a, b) => a.id - b.id)) {
        if (review.user && decisionStates.has(review.state)) decisions.set(review.user.id, review);
    }
    requireCondition(conversation.reviewDecision === 'APPROVED' &&
        !conversation.threads.some(thread => !thread.isResolved) &&
        ![...decisions.values()].some(review => review.state === 'CHANGES_REQUESTED'),
        'PR review decision or unresolved conversation blocks merge');
    const approvals = new Map([...decisions].filter(([id, review]) => id !== author && owners.has(id) &&
        review.state === 'APPROVED' && review.commit_id === expected.head));
    requireCondition(approvals.size > 0 && [...requiredOwners].every(id => approvals.has(id)),
        'Missing current-head approval from independent or affected owner');

    requireCondition(Number.isSafeInteger(check?.id) && check.id > 0 &&
        check.workflowId === workflowId && check.workflowPath === workflowPath &&
        check.appId === actionsAppId && check.name === checkName && check.prNumber === pr.number &&
        check.head === expected.head && check.base === expected.base &&
        check.runConclusion === 'success' && check.conclusion === 'success', 'Required tooling check missing, stale, or failed');
    return { pr: pr.number, head: expected.head, base: expected.base, checkRun: check.id,
        approvals: [...approvals].map(([ownerId, review]) => ({ ownerId, reviewId: review.id })), crossOwner: [...requiredOwners] };
}

export async function inspectPull(number, expected, token) {
    const root = '/repos/' + repository;
    async function api(endpoint, body) {
        const response = await fetch('https://api.github.com' + endpoint, {
            method: body ? 'POST' : 'GET', redirect: 'error', signal: AbortSignal.timeout(15000),
            headers: { Authorization: 'Bearer ' + token, Accept: 'application/vnd.github+json',
                'Content-Type': 'application/json',
                'X-GitHub-Api-Version': '2022-11-28' }, ...(body && { body: JSON.stringify(body) })
        });
        requireCondition(response.ok, 'GitHub API request failed (' + response.status + '): ' + endpoint.split('?')[0]);
        return response.json();
    }
    async function pages(endpoint, resultKey) {
        const results = [];
        for (let page = 1; page <= 31; page++) {
            const response = await api(root + endpoint + (endpoint.includes('?') ? '&' : '?') + 'per_page=100&page=' + page);
            const items = resultKey ? response[resultKey] : response;
            requireCondition(Array.isArray(items), 'Incomplete paginated API response');
            results.push(...items);
            if (items.length < 100) return results;
        }
        throw new Error('API result exceeded the supported review size');
    }
    async function conversations() {
        const query = `query($number:Int!,$cursor:String) { repository(owner:"2dshligang",name:"last-known-state-the-legacy") {
            pullRequest(number:$number) { reviewDecision reviewThreads(first:100,after:$cursor) {
                nodes { isResolved } pageInfo { hasNextPage endCursor }
            } } } }`;
        const threads = [];
        let cursor = null;
        let previous;
        for (let page = 0; page < 31; page++) {
            const result = await api('/graphql', { query, variables: { number, cursor } });
            const pull = result.data?.repository?.pullRequest;
            requireCondition(!result.errors && pull?.reviewThreads, 'PR review conversations unavailable');
            requireCondition(page === 0 || previous === pull.reviewDecision, 'Review decision changed during pagination');
            previous = pull.reviewDecision;
            const connection = pull.reviewThreads;
            threads.push(...connection.nodes);
            if (!connection.pageInfo.hasNextPage) return { reviewDecision: previous, threads };
            cursor = connection.pageInfo.endCursor;
        }
        throw new Error('Too many PR review conversations');
    }

    const prPath = root + '/pulls/' + number;
    const basePath = root + '/git/ref/heads/main';
    const pr = await api(prPath);
    requireCondition(pr.head.sha === expected.head && pr.base.sha === expected.base, 'PR head or base changed');
    const [ref, comparison, files, reviews, conversation, protection, collaborators, runs] = await Promise.all([
        api(basePath), api(root + '/compare/' + expected.base + '...' + expected.head),
        pages('/pulls/' + number + '/files'), pages('/pulls/' + number + '/reviews'), conversations(),
        api(root + '/branches/main/protection'), pages('/collaborators?affiliation=all'),
        api(root + '/actions/workflows/' + workflowId + '/runs?event=pull_request&per_page=100&head_sha=' + expected.head)
    ]);
    const run = runs.workflow_runs?.[0];
    const pull = run?.pull_requests?.find(item => item.number === number);
    requireCondition(pull && run.head_sha === expected.head && run.event === 'pull_request' &&
        run.head_repository?.id === pr.base.repo.id && run.repository?.id === pr.base.repo.id,
        'No matching tooling run for this pull request');
    const jobs = await pages('/actions/runs/' + run.id + '/attempts/' + run.run_attempt + '/jobs', 'jobs');
    const matchingJobs = jobs.filter(job => job.name === checkName);
    requireCondition(matchingJobs.length === 1, 'Required tooling job missing or ambiguous');
    const job = matchingJobs[0];
    const checkId = /\/check-runs\/(\d+)$/.exec(job.check_run_url ?? '')?.[1];
    requireCondition(checkId, 'GitHub did not provide a check run ID');
    const checkRun = await api(root + '/check-runs/' + checkId);
    requireCondition(job.head_sha === expected.head && checkRun.head_sha === expected.head && checkRun.status === 'completed' &&
        run.status === 'completed', 'Tooling job is incomplete or belongs to another commit');
    const check = { workflowId: run.workflow_id, workflowPath: run.path, name: checkRun.name,
        appId: checkRun.app?.id, id: checkRun.id, prNumber: pull.number, head: pull.head.sha, base: pull.base.sha,
        runConclusion: run.conclusion, conclusion: job.conclusion === checkRun.conclusion ? checkRun.conclusion : null };

    const [latest, latestRef, latestConversation, latestReviews, latestRun] = await Promise.all([
        api(prPath), api(basePath), conversations(), pages('/pulls/' + number + '/reviews'),
        api(root + '/actions/runs/' + run.id)
    ]);
    requireCondition(latest.head.sha === pr.head.sha && latest.base.sha === pr.base.sha &&
        latestRef.object.sha === ref.object.sha && latest.draft === pr.draft && latest.state === pr.state &&
        latestRun.run_attempt === run.run_attempt && latestRun.status === run.status && latestRun.conclusion === run.conclusion &&
        JSON.stringify(latestConversation) === JSON.stringify(conversation) &&
        JSON.stringify(latestReviews.map(({ id, state, commit_id }) => [id, state, commit_id])) ===
        JSON.stringify(reviews.map(({ id, state, commit_id }) => [id, state, commit_id])),
        'PR, base, reviews or conversations changed during evaluation');
    const permissions = Object.fromEntries(collaborators.map(user => [user.id, user.role_name]));
    return verifyPolicy({ pr, currentBase: ref.object.sha, comparison, files, reviews, conversation, check, protection, permissions }, expected);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const [, , number, head, base] = process.argv;
        requireCondition(/^\d+$/.test(number ?? '') && Number.isSafeInteger(Number(number)) &&
            sha.test(head ?? '') && sha.test(base ?? '') && process.env.GITHUB_TOKEN,
            'Usage: set GITHUB_TOKEN, then run node tools/review-main.mjs <pr> <head-sha> <base-sha>');
        console.log(JSON.stringify(await inspectPull(Number(number), { head, base }, process.env.GITHUB_TOKEN), null, 2));
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
