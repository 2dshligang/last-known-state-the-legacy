import fs from 'node:fs/promises';
import path from 'node:path';
import { assert } from './lib.mjs';

// A baseline text/GUID linter, not a C# semantic analyzer or a complete release audit.
try {
    const root=path.resolve(process.argv[2]||'.');
    const assets=path.join(root,'Assets');
    const guids=new Map(); let count=0;
    await fs.access(path.join(root,'ProjectSettings/ProjectVersion.txt'));
    async function walk(dir) {
        for(const e of await fs.readdir(dir,{withFileTypes:true})){
            const f=path.join(dir,e.name);
            assert(!e.isSymbolicLink(),`Linked asset: ${f}`);
            if(e.isDirectory()){await walk(f);continue;}
            count++;
            if(e.name.endsWith('.meta')){
                const txt=await fs.readFile(f,'utf8'); const guid=/^guid: ([0-9a-f]{32})$/m.exec(txt)?.[1];
                assert(guid,`Invalid GUID: ${f}`);assert(!guids.has(guid),`Duplicate GUID: ${f}`);guids.set(guid,f);
                await fs.access(f.slice(0,-5));
            } else if(!e.name.startsWith('.')) await fs.access(f+'.meta');
            if(/\.(cs|json|unity|prefab|asset|uxml|uss|shader|txt)$/.test(f)){
                const txt=await fs.readFile(f,'utf8');
                assert(!/\p{Script=Cyrillic}/u.test(txt),`Cyrillic content: ${f}`);
                assert(!/^(?:<{7}|={7}|>{7})/m.test(txt),`Conflict marker: ${f}`);
                if(f.includes(`${path.sep}_Game${path.sep}`)) assert(!/[^\r\n][ \t]+\r?$/m.test(txt),`Trailing whitespace: ${f}`);
            }
        }
    }
    await walk(assets);
    assert(count>0,'No assets checked');console.log(`Baseline policy checked ${count} asset files.`);
}catch(e){console.error(e.message);process.exitCode=1;}
