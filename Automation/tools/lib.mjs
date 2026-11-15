import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';

export const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
export function assert(ok, message) { if (!ok) throw new Error(message); }
export function safePath(root, relative) {
    assert(typeof relative === 'string' && relative.length > 0, 'Missing relative path');
    assert(!relative.includes('\\') && !relative.includes(':') && !path.isAbsolute(relative), 'Invalid path');
    assert(relative.split('/').every(p => p && p !== '.' && p !== '..'), 'Unsafe path segment');
    assert(relative.split('/').every(p => !/[ .]$|[<>"|?*\x00-\x1f]/.test(p) &&
        !/^(?:con|prn|aux|nul|com[1-9¹²³]|lpt[1-9¹²³]|conin\$|conout\$)(?:\.|$)/i.test(p)), 'Unsafe path segment');
    const target = path.resolve(root, relative);
    assert(target.startsWith(path.resolve(root) + path.sep), 'Path escapes root');
    return target;
}
export async function noLinks(root, target) {
    const absolute = path.resolve(target);
    const anchor = path.parse(absolute).root;
    let current = anchor;
    for (const part of absolute.slice(anchor.length).split(path.sep).filter(Boolean)) {
        current = path.join(current, part);
        try { assert(!(await fs.lstat(current)).isSymbolicLink(), `Link not allowed: ${current}`); }
        catch (e) { if (e.code !== 'ENOENT') throw e; }
    }
    assert(absolute.startsWith(path.resolve(root) + path.sep), 'Path escapes root');
}
export function approvedUrl(raw, hosts) {
    const u = new URL(raw);
    assert(u.protocol === 'https:' && !u.username && !u.password && (!u.port || u.port === '443'), 'HTTPS only');
    assert(hosts.includes(u.hostname), `Unapproved host: ${u.hostname}`);
    assert(!/[?&](?:api_key|token|access_token|key)=/i.test(u.href), 'Do not persist credentials in URLs');
    return u;
}
export function verifyBytes(bytes, entry) {
    assert(bytes.length > 0 && bytes.length <= entry.maxBytes, 'Invalid payload size');
    if (entry.bytes !== undefined) assert(bytes.length === entry.bytes, 'Pinned byte count mismatch');
    if (entry.format === 'json') JSON.parse(bytes.toString('utf8'));
    else if (entry.format === 'zip') assert(bytes.subarray(0, 4).equals(Buffer.from([80,75,3,4])), 'Expected ZIP; HTML or login page rejected');
    else if (entry.format === 'obj') assert(/^v /m.test(bytes.toString('utf8')), 'Expected OBJ vertices');
    else throw new Error('Unsupported format');
    assert(/^[a-f0-9]{64}$/.test(entry.sha256), 'Missing pinned SHA-256');
    assert(sha256(bytes) === entry.sha256, 'SHA-256 mismatch; retain previous snapshot');
}
export async function writeOnce(file, bytes) {
    await fs.mkdir(path.dirname(file), { recursive: true });
    try { await fs.writeFile(file, bytes, { flag: 'wx' }); }
    catch(e) {
        if(e.code !== 'EEXIST') throw e;
        assert(sha256(await fs.readFile(file)) === sha256(bytes), `Refusing to overwrite: ${file}`);
    }
}
