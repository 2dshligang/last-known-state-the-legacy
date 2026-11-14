import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { safePath, approvedUrl, verifyBytes, sha256, writeOnce } from '../tools/lib.mjs';
import { fetchEntry } from '../tools/fetch-locked.mjs';
import { normalize } from '../tools/normalize-donki.mjs';
import { box } from '../tools/fallbacks.mjs';
import { verify } from '../tools/verify.mjs';

const bytes=Buffer.from('[{"ok":true}]');
const base={id:'fixture',approval:'approved',license:'Test fixture',licenseUrl:'https://example.com/license',attribution:'Test',url:'https://example.com/data',path:'data/test.json',format:'json',maxBytes:1000,sha256:sha256(bytes)};
async function temp(t){const base=path.resolve('work/test-temp');await fs.mkdir(base,{recursive:true});const p=await fs.mkdtemp(path.join(base,'lks-test-'));t.after(()=>{assert.ok(path.resolve(p).startsWith(base+path.sep));return fs.rm(p,{recursive:true,force:true});});return p;}
test('reject traversal, absolute paths, Windows ADS and backslash traversal',()=>{for(const s of ['../a','/a','a/../b','a\\..\\b','C:/x','a:stream','a//b']) assert.throws(()=>safePath('/root',s));});
test('reject HTTP, credentials, unapproved redirects and embedded tokens',()=>{for(const u of ['http://example.com/a','https://evil.com/a','https://a:b@example.com/a','https://example.com/a?api_key=secret']) assert.throws(()=>approvedUrl(u,['example.com']));});
test('reject HTML disguised as an archive',()=>assert.throws(()=>verifyBytes(Buffer.from('<html>login</html>'),{...base,format:'zip'}),/ZIP/));
test('reject truncated and oversized files',()=>{assert.throws(()=>verifyBytes(bytes.subarray(0,3),base)); assert.throws(()=>verifyBytes(bytes,{...base,maxBytes:1}));});
test('reject incorrect checksums',()=>assert.throws(()=>verifyBytes(bytes,{...base,sha256:'0'.repeat(64)}),/mismatch/));
test('download once and reuse verified cache without network',async t=>{const root=await temp(t);let calls=0;const mock=async()=>{calls++;return new Response(bytes);};assert.equal(await fetchEntry(base,root,['example.com'],mock),'downloaded');assert.equal(await fetchEntry(base,root,['example.com'],mock),'cached');assert.equal(calls,1);});
test('blocked redirect is never followed',async t=>{const root=await temp(t);let calls=0;await assert.rejects(fetchEntry(base,root,['example.com'],async()=>{calls++;return new Response(null,{status:302,headers:{location:'https://evil.com/private'}});}),/Unapproved/);assert.equal(calls,1);});
test('unapproved entry never reaches network',async t=>{const root=await temp(t);await assert.rejects(fetchEntry({...base,approval:'pending'},root,['example.com'],()=>{throw Error('network called');}),/not approved/);});
test('changed cached bytes fail without replacement',async t=>{const root=await temp(t);await writeOnce(path.join(root,base.path),'[]');await assert.rejects(fetchEntry(base,root,['example.com']),/mismatch/);assert.equal(await fs.readFile(path.join(root,base.path),'utf8'),'[]');});
test('write-once is repeatable but preserves other content',async t=>{const root=await temp(t), f=path.join(root,'a');await writeOnce(f,'a');await writeOnce(f,'a');await assert.rejects(writeOnce(f,'b'),/overwrite/);assert.equal(await fs.readFile(f,'utf8'),'a');});
test('stream size limit works without Content-Length',async t=>{const root=await temp(t);await assert.rejects(fetchEntry({...base,maxBytes:2},root,['example.com'],async()=>new Response(bytes)),/maximum/);await assert.rejects(fs.access(path.join(root,base.path)));});
const row={sepID:'x',eventTime:'2017-09-04T22:56Z',instruments:[{displayName:'MODEL: example'}],link:'https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/1/-1',versionId:1};
test('preserve model-vs-observation and never invent dose',()=>{const n=normalize([row,{...row,sepID:'y',instruments:[{displayName:'GOES13: SEM/EPS >10 MeV'}]}],'hash');assert.equal(n.events[0].evidenceKind,'ModelOutput');assert.equal(n.events[1].evidenceKind,'ObservedEvent');assert.ok(n.events.every(e=>e.dose===null));});
test('reject empty, duplicated, malformed or sourceless science records',()=>{for(const a of [[],[row,row],[{...row,eventTime:'yesterday'}],[{...row,instruments:[]}],[{...row,link:'https://fake.test/'}]]) assert.throws(()=>normalize(a,'hash'));});
test('normalization is deterministic across input order',()=>{const b={...row,sepID:'a',eventTime:'2017-09-01T01:00Z'};assert.deepEqual(normalize([row,b],'h'),normalize([b,row],'h'));});
test('procedural geometry has finite vertices, valid faces and stable bytes',()=>{const a=box('wall',4,3,0.2);assert.equal(a,box('wall',4,3,0.2));assert.equal(a.split('\n').filter(l=>l.startsWith('v ')).length,8);assert.equal(a.split('\n').filter(l=>l.startsWith('f ')).length,6);assert.ok(!/NaN|Infinity/.test(a));});
async function copyScienceFixture(t){const root=await temp(t);for(const dir of ['sources','samples']) await fs.cp(path.resolve(dir),path.join(root,dir),{recursive:true});return root;}
test('manifest verification accepts the real archived snapshot',async t=>{const root=await copyScienceFixture(t);const result=await verify(root);assert.equal(result.scienceRecords,9);assert.equal(result.generatedAssets,5);});
test('tampered derived science fails even when its raw hash is unchanged',async t=>{const root=await copyScienceFixture(t);const file=path.join(root,'samples/science/donki-events.json');const data=JSON.parse(await fs.readFile(file,'utf8'));data.events[0].evidenceKind='ObservedEvent';await fs.writeFile(file,JSON.stringify(data));await assert.rejects(verify(root),/reproducible transform/);});
