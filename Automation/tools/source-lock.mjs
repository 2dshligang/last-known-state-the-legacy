import fs from 'node:fs/promises';
import path from 'node:path';
import { approvedUrl, assert, noLinks, safePath, sha256 } from './lib.mjs';

// Preserve the exact DONKI record accepted before terms evidence was required.
const acceptedDonkiRecord = '768eddcfbdb3531b7864239f5926b108461faa88261bcaba277fad084ad41cba';

async function verifyTerms(root, entry) {
    const url = new URL(entry.licenseUrl);
    approvedUrl(url.href, [url.hostname]);
    const fields = Object.keys(entry).sort().map(key => [key, entry[key]]);
    if (entry.id === 'nasa-donki-sep-2017' && sha256(JSON.stringify(fields)) === acceptedDonkiRecord) return;

    const evidence = entry.termsEvidence;
    assert(evidence?.path === 'sources/terms/' + entry.id + '.txt' &&
        /^[a-f0-9]{64}$/.test(evidence?.sha256 ?? ''), 'Missing pinned terms evidence: ' + entry.id);
    const target = safePath(root, evidence.path);
    await noLinks(root, target);
    const info = await fs.stat(target);
    assert(info.isFile() && info.size > 0 && info.size <= 256 * 1024, 'Invalid terms evidence file: ' + entry.id);
    const bytes = await fs.readFile(target);
    assert(sha256(bytes) === evidence.sha256, 'Terms evidence SHA-256 mismatch: ' + entry.id);
    const [reference, ...excerpt] = bytes.toString('utf8').split(/\r?\n/);
    assert(reference === url.href && excerpt.join('\n').trim(), 'Terms evidence must contain the rights URL and an excerpt: ' + entry.id);
}

export async function validateLock(lock, root) {
    assert(lock?.schemaVersion === 1 && Array.isArray(lock.allowedHosts) &&
        Array.isArray(lock.entries) && lock.entries.length > 0, 'Missing source lock');
    const ids = new Set(), paths = new Set();
    for (const entry of lock.entries) {
        assert(typeof entry.id === 'string' && /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/.test(entry.id), 'Invalid source id');
        assert(!ids.has(entry.id), 'Duplicate source id');
        ids.add(entry.id);
        const target = safePath(root, entry.path).toLowerCase();
        assert(/^sources\/raw\/(?:[a-z0-9][a-z0-9_-]*\/)*[a-z0-9][a-z0-9_-]*\.(json|zip|obj)$/.test(entry.path) &&
            entry.path.endsWith('.' + entry.format), 'Invalid raw source path');
        assert(!paths.has(target), 'Duplicate source path');
        paths.add(target);
        for (const key of ['license', 'licenseUrl', 'attribution', 'retrievedAtUtc', 'approvalScope']) {
            assert(typeof entry[key] === 'string' && entry[key].trim(), 'Incomplete provenance: ' + key);
        }
        assert(entry.approval === 'approved' && !/^(unknown|pending|unverified)$/i.test(entry.license.trim()), 'Source rights not approved');
        approvedUrl(entry.url, lock.allowedHosts);
        assert(/^[a-f0-9]{64}$/.test(entry.sha256) && Number.isSafeInteger(entry.maxBytes) &&
            entry.maxBytes > 0 && Number.isSafeInteger(entry.bytes) && entry.bytes > 0 &&
            entry.bytes <= entry.maxBytes, 'Invalid source size or checksum pin');
    }
    for (const entry of lock.entries) await verifyTerms(root, entry);
    return lock;
}

export async function readLock(root) {
    return validateLock(JSON.parse(await fs.readFile(path.join(root, 'sources/downloads.lock.json'), 'utf8')), root);
}
