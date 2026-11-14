import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { assert, safePath, verifyBytes, sha256 } from './lib.mjs';
import { normalize } from './normalize-donki.mjs';

export async function verify(root) {
    const lock=JSON.parse(await fs.readFile(path.join(root,'sources/downloads.lock.json'),'utf8'));
    assert(lock.schemaVersion===1 && lock.entries.length>0,'Missing source lock');
    const ids=new Set();
    for (const e of lock.entries) {
        assert(!ids.has(e.id),'Duplicate source id'); ids.add(e.id);
        assert(e.approval==='approved' && e.license && e.licenseUrl && e.attribution && e.retrievedAtUtc,'Incomplete provenance');
        verifyBytes(await fs.readFile(safePath(root,e.path)),e);
    }
    const assets=JSON.parse(await fs.readFile(path.join(root,'sources/generated-assets.json'),'utf8'));
    for(const e of assets.entries) assert(sha256(await fs.readFile(safePath(root,e.path)))===e.sha256,'Generated asset changed');
    const science=JSON.parse(await fs.readFile(path.join(root,'samples/science/donki-events.json'),'utf8'));
    assert(science.sourceSha256===lock.entries.find(e=>e.id==='nasa-donki-sep-2017').sha256,'Stale science transform');
    assert(science.events.length>0 && science.events.every(e=>e.dose===null && e.doseUnit===null),'Fabricated dose');
    const raw=await fs.readFile(path.join(root,'sources/raw/donki-sep-2017.json'));
    assert(JSON.stringify(science)===JSON.stringify(normalize(JSON.parse(raw),sha256(raw))),'Derived science differs from reproducible transform');
    const files=[];
    async function walk(dir) {for(const e of await fs.readdir(dir,{withFileTypes:true})){
        if(['.git','node_modules','work','cache'].includes(e.name))continue;
        const f=path.join(dir,e.name);
        if(e.isDirectory()) await walk(f); else if(/\.(?:md|mjs|json|ps1|yml|example|obj)$/.test(f))files.push(f);
    }}
    await walk(root);
    for(const file of files) {
        const s=await fs.readFile(file,'utf8');
        assert(!/\p{Script=Cyrillic}/u.test(s),`Non-English script: ${path.relative(root,file)}`);
        assert(!/^(?:<{7}|={7}|>{7})/m.test(s),`Merge conflict: ${file}`);
        assert(!/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/.test(s),`Private key: ${file}`);
    }
    return {filesChecked:files.length,lockedDownloads:lock.entries.length,generatedAssets:assets.entries.length,scienceRecords:science.events.length};
}
if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
    try {console.log(JSON.stringify(await verify(path.resolve(process.argv[2]||'.')),null,2));}
    catch(e){console.error(e.message);process.exitCode=1;}
}
