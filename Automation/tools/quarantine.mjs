import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { approvedUrl, assert, noLinks, safePath, sha256, writeOnce } from './lib.mjs';

const maxBytes = 32 * 1024 * 1024;

export async function quarantine(root, metadata, payload) {
    assert(typeof metadata?.id === 'string' && /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/.test(metadata.id), 'Invalid candidate id');
    const evidence = {};
    for (const key of ['sourceUrl', 'downloadUrl', 'author', 'license', 'termsUrl', 'attribution',
        'query', 'retrievedAtUtc', 'instrument', 'units', 'transformVersion']) {
        const value = metadata[key];
        assert(typeof value === 'string' && value.trim(), 'Missing candidate field: ' + key);
        if (key.endsWith('Url')) approvedUrl(value, [new URL(value).hostname]);
        evidence[key] = value;
    }
    assert(Buffer.isBuffer(payload) && payload.length > 0 && payload.length <= maxBytes, 'Invalid candidate size');
    const hash = sha256(payload);
    const record = { schemaVersion: 1, id: metadata.id, status: 'quarantined', ...evidence, bytes: payload.length, sha256: hash };
    const relative = 'work/quarantine/' + metadata.id + '/' + hash;
    const content = safePath(root, relative + '/payload.bin');
    const receipt = safePath(root, relative + '/candidate.json');
    await noLinks(root, content);
    await noLinks(root, receipt);
    await writeOnce(content, payload);
    await writeOnce(receipt, JSON.stringify(record, null, 2) + '\n');
    return record;
}

async function readCandidate(file) {
    const input = await fs.open(file, 'r');
    try {
        const info = await input.stat();
        assert(info.isFile() && info.size > 0 && info.size <= maxBytes, 'Invalid candidate size');
        const buffer = Buffer.alloc(info.size + 1);
        let offset = 0;
        while (offset < buffer.length) {
            const { bytesRead } = await input.read(buffer, offset, buffer.length - offset, offset);
            if (!bytesRead) break;
            offset += bytesRead;
        }
        assert(offset === info.size, 'Candidate size changed while reading');
        return buffer.subarray(0, offset);
    } finally { await input.close(); }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const [metadata, file] = process.argv.slice(2);
        assert(metadata && file, 'Usage: node tools/quarantine.mjs <candidate.json> <local-file>');
        const descriptor = JSON.parse(await fs.readFile(metadata, 'utf8'));
        console.log(JSON.stringify(await quarantine(process.cwd(), descriptor, await readCandidate(file)), null, 2));
    } catch (error) { console.error(error.message); process.exitCode = 1; }
}
