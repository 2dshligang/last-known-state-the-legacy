// No dependencies. Run: node ArtSource/Developer1/W01/verify_assets.mjs
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const source = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(source, '../../..');
const inventory = JSON.parse(fs.readFileSync(path.join(source, 'inventory.json'), 'utf8'));
const hash = p => createHash('sha256').update(fs.readFileSync(p)).digest('hex');
assert.equal(hash(path.join(source, 'build_assets.py')), inventory.generatorSha256);
const guids = new Set();
for (const file of inventory.files) {
  const absolute = path.resolve(root, file.path);
  assert.ok(absolute.startsWith(root + path.sep));
  assert.equal(hash(absolute), file.sha256, file.path);
  if (file.path.endsWith('.meta')) {
    const guid = fs.readFileSync(absolute, 'utf8').match(/^guid: ([a-f0-9]{32})$/m)?.[1];
    assert.ok(guid, file.path);
    assert.ok(!guids.has(guid), `Duplicate asset GUID: ${file.path}`);
    guids.add(guid);
  }
}

const subtract = (a,b) => a.map((x,i) => x-b[i]);
const cross = (a,b) => [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
const dot = (a,b) => a.reduce((s,v,i) => s+v*b[i], 0);
let meshes = 0;
for (const entry of inventory.entries.filter(e => e.path.endsWith('.obj'))) {
  const text = fs.readFileSync(path.join(root, entry.path), 'utf8');
  assert.ok(!/[^\x00-\x7f]/.test(text), 'Non-ASCII content');
  const vertices = [], uvs = [], faces = [], edges = new Map();
  for (const line of text.split('\n')) {
    const [kind,...parts] = line.trim().split(/\s+/);
    if (kind === 'v') vertices.push(parts.map(Number));
    if (kind === 'vt') uvs.push(parts.map(Number));
    if (kind === 'f') faces.push(parts.map(p => p.split('/').map(Number)));
  }
  assert.ok(vertices.length && uvs.length && faces.length);
  assert.ok(vertices.every(v => v.length === 3 && v.every(Number.isFinite)));
  assert.ok(uvs.every(v => v.length === 2 && v.every(x => Number.isFinite(x) && x >= 0 && x <= 1)));
  let volume = 0;
  for (const face of faces) {
    assert.ok(face.length >= 3);
    for (const [v,uv] of face) {
      assert.ok(Number.isInteger(v) && v > 0 && v <= vertices.length);
      assert.ok(Number.isInteger(uv) && uv > 0 && uv <= uvs.length);
    }
    for (let i=0; i<face.length; i++) {
      const a=face[i][0], b=face[(i+1)%face.length][0];
      const key=[a,b].sort((a,b)=>a-b).join('/');
      const edge=edges.get(key) ?? {count:0,balance:0};
      edge.count++; edge.balance += a < b ? 1 : -1; edges.set(key,edge);
    }
    for (let i=1; i<face.length-1; i++) {
      const [a,b,c] = [face[0],face[i],face[i+1]].map(f => vertices[f[0]-1]);
      const normal=cross(subtract(b,a), subtract(c,a));
      assert.ok(dot(normal,normal) > 1e-12, `Zero-area triangle: ${entry.id}`);
      volume += dot(a,cross(b,c))/6;
    }
  }
  assert.ok([...edges.values()].every(e => e.count===2 && e.balance===0), `Non-manifold/inconsistent winding: ${entry.id}`);
  assert.ok(volume > 0, `Inverted or empty mesh: ${entry.id}`);
  const dimensions=[0,1,2].map(i => Math.max(...vertices.map(v=>v[i]))-Math.min(...vertices.map(v=>v[i])));
  dimensions.forEach((d,i) => assert.ok(Math.abs(d-entry.dimensionsMetersUnityXYZ[i]) < 1e-5, `${entry.id} bounds`));
  meshes++;
}
const fbx = fs.readFileSync(path.join(root, inventory.entries[0].path));
const header = Buffer.from('Kaydara FBX Binary  \0\x1a\0', 'ascii');
assert.deepEqual(fbx.subarray(0,header.length), header);
console.log(`PASS: ${inventory.files.length} file hashes; ${guids.size} unique GUIDs; ${meshes} OBJ meshes with closed oriented edges, positive volume, UVs and meter bounds; binary FBX header.`);
