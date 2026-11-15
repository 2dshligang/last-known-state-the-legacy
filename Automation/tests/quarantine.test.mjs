import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { quarantine } from '../tools/quarantine.mjs';
import { validateLock } from '../tools/source-lock.mjs';
import { sha256 } from '../tools/lib.mjs';

const lock = JSON.parse(await fs.readFile(new URL('../sources/downloads.lock.json', import.meta.url)));
const candidate = {
    id: 'candidate', sourceUrl: 'https://example.com/item', downloadUrl: 'https://example.com/download',
    termsUrl: 'https://example.com/terms', author: 'Fixture', license: 'unknown', attribution: 'Fixture',
    query: 'not applicable', retrievedAtUtc: '2026-11-15T00:00:00Z',
    instrument: 'not applicable', units: 'not applicable', transformVersion: 'none'
};

test('candidate bytes remain opaque and separate from the approved lock across repeats', async t => {
    const work = path.resolve('work');
    await fs.mkdir(work, { recursive: true });
    const root = await fs.mkdtemp(path.join(work, 'quarantine-test-'));
    t.after(() => fs.rm(root, { recursive: true, force: true }));
    const payload = Buffer.from('<html>download failed</html>');
    const first = await quarantine(root, { ...candidate, approval: 'approved' }, payload);
    assert.equal(first.status, 'quarantined');
    assert.equal(first.approval, undefined);
    assert.deepEqual(await quarantine(root, candidate, payload), first);
    const bucket = path.join(root, 'work/quarantine/candidate', first.sha256);
    assert.deepEqual(await fs.readFile(path.join(bucket, 'payload.bin')), payload);
    await assert.rejects(fs.access(path.join(root, 'sources')));
    await assert.rejects(quarantine(root, { ...candidate, author: 'Changed' }, payload), /overwrite/);
    await assert.rejects(quarantine(root, { ...candidate, id: '../candidate' }, payload), /Invalid candidate id/);
    await assert.rejects(quarantine(root, { ...candidate, id: 'nul' }, payload), /Unsafe path/);
});

test('approved lock rejects duplicate identities and destinations before acquisition', async () => {
    const [entry] = lock.entries;
    const check = entries => validateLock({ ...lock, entries }, '.');
    await check([entry]);
    await assert.rejects(check([entry, entry]), /Duplicate source id/);
    await assert.rejects(check([entry, { ...entry, id: 'duplicate-path' }]), /Duplicate source path/);
    await assert.rejects(check([{ ...entry, license: 'unknown' }]), /Source rights not approved/);
    await assert.rejects(check([{ ...entry, path: 'work/quarantine/item.json' }]), /Invalid raw source path/);
    await assert.rejects(check([{ ...entry, path: 'sources/raw/nul.json' }]), /Unsafe path/);
});

test('new or modified approvals need HTTPS and matching terms evidence', async t => {
    const work = path.resolve('work');
    await fs.mkdir(work, { recursive: true });
    const root = await fs.mkdtemp(path.join(work, 'terms-test-'));
    t.after(() => fs.rm(root, { recursive: true, force: true }));
    const [legacy] = lock.entries;
    const entry = { ...legacy, id: 'new-source', licenseUrl: 'https://example.com/rights' };
    const check = candidate => validateLock({ ...lock, entries: [candidate] }, root);
    await check(legacy);
    await assert.rejects(check({ ...legacy, attribution: 'Changed' }), /Missing pinned terms evidence/);
    await assert.rejects(check({ ...entry, licenseUrl: 'invalid' }), /Invalid URL/);
    await assert.rejects(check({ ...entry, licenseUrl: 'http://example.com/rights' }), /HTTPS only/);
    await assert.rejects(check(entry), /Missing pinned terms evidence/);
    const evidencePath = 'sources/terms/new-source.txt';
    const evidenceFile = path.join(root, evidencePath);
    const evidence = entry.licenseUrl + '\nSynthetic test terms.\n';
    const approved = { ...entry, termsEvidence: { path: evidencePath, sha256: sha256(evidence) } };
    await assert.rejects(check(approved), { code: 'ENOENT' });
    await fs.mkdir(path.dirname(evidenceFile), { recursive: true });
    await fs.writeFile(evidenceFile, evidence);
    await check(approved);
    await assert.rejects(check({ ...approved, licenseUrl: 'https://example.com/other' }), /rights URL and an excerpt/);
    await fs.writeFile(evidenceFile, 'Tampered');
    await assert.rejects(check(approved), /Terms evidence SHA-256 mismatch/);
});
