import fs from 'node:fs/promises';
import path from 'node:path';
import { approvedUrl, assert, safePath } from './lib.mjs';

export function validateLock(lock, root) {
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
    return lock;
}

export async function readLock(root) {
    return validateLock(JSON.parse(await fs.readFile(path.join(root, 'sources/downloads.lock.json'), 'utf8')), root);
}
