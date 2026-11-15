import test from 'node:test';
import assert from 'node:assert/strict';
import { ownerIds, verifyPolicy } from '../tools/review-main.mjs';

const expected = { head: 'a'.repeat(40), base: 'b'.repeat(40) };
const repository = { id: 42, full_name: '2dshligang/last-known-state-the-legacy' };

function fixture() {
    return {
        pr: { number: 4, state: 'open', draft: false, changed_files: 1, user: { id: ownerIds.systems },
            head: { sha: expected.head, repo: repository }, base: { ref: 'main', sha: expected.base, repo: repository } },
        currentBase: expected.base,
        comparison: { behind_by: 0, merge_base_commit: { sha: expected.base } },
        files: [{ filename: 'Assets/_Game/Systems/Example.cs', status: 'modified' }],
        reviews: [{ id: 10, user: { id: ownerIds.world }, state: 'APPROVED', commit_id: expected.head }],
        conversation: { reviewDecision: 'APPROVED', threads: [{ isResolved: true }] },
        check: { id: 31, prNumber: 4, workflowId: 418214576, workflowPath: '.github/workflows/lks-tooling.yml',
            name: 'Tooling validation only', appId: 15368, runConclusion: 'success', conclusion: 'success',
            head: expected.head, base: expected.base },
        protection: {
            enforce_admins: { enabled: true }, required_conversation_resolution: { enabled: true },
            allow_force_pushes: { enabled: false }, allow_deletions: { enabled: false },
            required_pull_request_reviews: { dismiss_stale_reviews: true, require_last_push_approval: true,
                required_approving_review_count: 1 },
            required_status_checks: { strict: true, checks: [{ context: 'Tooling validation only', app_id: 15368 }] }
        },
        permissions: { [ownerIds.world]: 'write', [ownerIds.systems]: 'admin' }
    };
}

test('records a current independent review and both paths of a cross-owner rename', () => {
    const snapshot = fixture();
    snapshot.files[0] = { status: 'renamed', previous_filename: 'Assets/_Game/World/Example.cs',
        filename: 'Assets/_Game/Systems/Example.cs' };
    const result = verifyPolicy(snapshot, expected);
    assert.deepEqual(result.crossOwner, [ownerIds.world]);
    assert.deepEqual(result.approvals, [{ ownerId: ownerIds.world, reviewId: 10 }]);
});

for (const [name, mutate, error] of [
    ['stale head', f => { f.pr.head.sha = 'c'.repeat(40); }, /head changed/],
    ['changed base', f => { f.currentBase = 'c'.repeat(40); }, /base changed/],
    ['new push after approval', f => { f.reviews[0].commit_id = 'c'.repeat(40); }, /current-head approval/],
    ['fork', f => { f.pr.head.repo = { id: 99 }; }, /Fork/],
    ['unknown actor', f => { f.pr.user.id = 99; }, /Unrecognized PR author/],
    ['rename without source', f => { f.files[0].status = 'renamed'; }, /Rename missing/],
    ['rename out of workflows', f => Object.assign(f.files[0], { status: 'renamed',
        previous_filename: '.github/workflows/LKS.yml' }), /manual review/],
    ['workflow modification', f => { f.files[0].filename = '.github/workflows/lks-tooling.yml'; }, /manual review/],
    ['missing check', f => { f.check.id = null; f.check.name = null; }, /Required tooling check/],
    ['skipped check', f => { f.check.conclusion = 'skipped'; }, /Required tooling check/],
    ['wrong check issuer', f => { f.check.appId = 99; }, /Required tooling check/],
    ['check ran on a previous base', f => { f.check.base = 'c'.repeat(40); }, /Required tooling check/],
    ['empty required list', f => { f.protection.required_status_checks.checks = []; }, /strict main protection/],
    ['bypass user', f => { f.protection.required_pull_request_reviews.bypass_pull_request_allowances = { users: [42] }; },
        /protections are missing/],
    ['unresolved thread', f => { f.conversation.threads[0].isResolved = false; }, /unresolved conversation/],
    ['revoked review', f => { f.reviews.push({ ...f.reviews[0], id: 11, state: 'DISMISSED' }); }, /current-head approval/],
    ['owner lost write access', f => { f.permissions[ownerIds.world] = 'read'; }, /write access/],
    ['shared root change with author-only review', f => {
        f.files[0].filename = 'ProjectSettings/ProjectVersion.txt'; f.reviews[0].user.id = ownerIds.systems;
    }, /independent or affected owner/]
]) {
    test('blocks ' + name, () => {
        const snapshot = fixture();
        mutate(snapshot);
        assert.throws(() => verifyPolicy(snapshot, expected), error);
    });
}
