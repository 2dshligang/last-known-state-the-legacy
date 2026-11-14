import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { sha256, writeOnce } from './lib.mjs';

export function box(name,x,y,z) {
    const v=[[-x/2,0,-z/2],[x/2,0,-z/2],[x/2,y,-z/2],[-x/2,y,-z/2],[-x/2,0,z/2],[x/2,0,z/2],[x/2,y,z/2],[-x/2,y,z/2]];
    return `# Original LKS procedural placeholder. Units: meters.\no ${name}\n`+v.map(p=>'v '+p.join(' ')).join('\n')+'\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\n'+[[4,3,2,1],[2,3,7,6],[6,7,8,5],[5,8,4,1],[8,7,3,4],[1,2,6,5]].map(f=>'f '+f.map((n,i)=>`${n}/${i+1}`).join(' ')).join('\n')+'\n';
}
if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
    const root=path.resolve(process.argv[2] || '.');
    const entries=[];
    for (const [id,x,y,z] of [['wall',4,3,0.2],['floor',4,0.2,4],['equipment-rack',0.8,2,0.6],['lab-bench',2,0.9,0.8],['solar-panel',2,0.08,1]]) {
        const content=box(id,x,y,z), target=`samples/art/${id}.obj`;
        await writeOnce(path.join(root,target),content);
        entries.push({id,path:target,sha256:sha256(content),origin:'procedural',author:'LKS project',generator:'tools/fallbacks.mjs',license:'Project-owned original placeholder',dimensionsMeters:[x,y,z],approval:'approved'});
    }
    await writeOnce(path.join(root,'sources/generated-assets.json'),JSON.stringify({schemaVersion:1,entries},null,2)+'\n');
    console.log(`Generated or verified ${entries.length} original placeholders.`);
}
