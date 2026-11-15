import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { quarantine } from '../tools/quarantine.mjs';
import { validateLock } from '../tools/source-lock.mjs';

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

test('approved lock rejects duplicate identities and destinations before acquisition', () => {
    const [entry] = lock.entries;
    const check = entries => validateLock({ ...lock, entries }, '.');
    assert.doesNotThrow(() => check([entry]));
    assert.throws(() => check([entry, entry]), /Duplicate source id/);
    assert.throws(() => check([entry, { ...entry, id: 'duplicate-path' }]), /Duplicate source path/);
    assert.throws(() => check([{ ...entry, license: 'unknown' }]), /Source rights not approved/);
    assert.throws(() => check([{ ...entry, path: 'work/quarantine/item.json' }]), /Invalid raw source path/);
    assert.throws(() => check([{ ...entry, path: 'sources/raw/nul.json' }]), /Unsafe path/);
});
