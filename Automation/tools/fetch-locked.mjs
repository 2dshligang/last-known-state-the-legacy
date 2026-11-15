import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { approvedUrl, assert, safePath, noLinks, verifyBytes, writeOnce } from './lib.mjs';

import { readLock } from './source-lock.mjs';

// Downloads only approved, checksum-pinned files. Never extracts archives or executes content.
export async function fetchEntry(entry, root, hosts, fetcher = fetch) {
    assert(entry.approval === 'approved', 'Entry is not approved');
    assert(entry.license && entry.licenseUrl && entry.attribution, 'Missing rights provenance');
    const target = safePath(root, entry.path);
    await noLinks(root, target);
    try { const b = await fs.readFile(target); verifyBytes(b, entry); return 'cached'; }
    catch (e) { if(e.code !== 'ENOENT') throw e; }
    let url = approvedUrl(entry.url, hosts);
    const signal = AbortSignal.timeout(30000);
    let response;
    for (let hop = 0; hop < 6; hop++) {
        response = await fetcher(url, {redirect:'manual', signal, headers:{'User-Agent':'LKS-AssetSciencePipeline/0.1'}});
        if ([301,302,303,307,308].includes(response.status)) {
            const next = response.headers.get('location');
            await response.body?.cancel();
            assert(next, 'Redirect without location');
            url = approvedUrl(new URL(next, url).href, hosts);
            response = null;
        } else break;
    }
    assert(response?.ok, 'Download failed or too many redirects');
    assert(Number(response.headers.get('content-length') || 0) <= entry.maxBytes, 'Payload too large');
    const chunks=[]; let size=0;
    for await (const chunk of response.body) {
        size += chunk.length;
        assert(size <= entry.maxBytes, 'Payload exceeds maximum');
        chunks.push(chunk);
    }
    const bytes=Buffer.concat(chunks);
    verifyBytes(bytes, entry);
    await writeOnce(target, bytes);
    return 'downloaded';
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const root=path.resolve(process.argv[2] || '.');
        const lock=await readLock(root);
        for(const entry of lock.entries) console.log(`${entry.id}: ${await fetchEntry(entry,root,lock.allowedHosts)}`);
    } catch(e) { console.error(e.message); process.exitCode=1; }
}
