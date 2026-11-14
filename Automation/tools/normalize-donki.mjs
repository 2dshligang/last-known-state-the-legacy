import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { assert, sha256, writeOnce } from './lib.mjs';

export function normalize(records, rawHash) {
    assert(Array.isArray(records) && records.length > 0, 'Empty or invalid DONKI dataset');
    const ids = new Set();
    const events=records.map(row => {
        assert(typeof row.sepID === 'string' && !ids.has(row.sepID), 'Missing or duplicate SEP id');
        ids.add(row.sepID);
        assert(typeof row.eventTime === 'string' && /Z$/.test(row.eventTime) && Number.isFinite(Date.parse(row.eventTime)), 'Invalid UTC timestamp');
        assert(Array.isArray(row.instruments) && row.instruments.length > 0 && row.instruments.every(i=>typeof i.displayName==='string' && i.displayName.length>0), 'Missing instruments');
        assert(typeof row.link === 'string' && new URL(row.link).hostname === 'ccmc.gsfc.nasa.gov', 'Unexpected citation');
        return {
            id:row.sepID, observedAtUtc:row.eventTime, sourceId:'nasa-donki-sep-2017',
            sourceRecordUrl:row.link, sourceVersion:row.versionId ?? null,
            instruments:row.instruments.map(i=>i.displayName),
            evidenceKind:row.instruments.some(i=>i.displayName.startsWith('MODEL:')) ? 'ModelOutput' : 'ObservedEvent',
            linkedEventIds:(row.linkedEvents ?? []).map(e=>e.activityID),
            location:'Instrument-specific; not an ASTERION surface measurement',
            dose:null, doseUnit:null
        };
    }).sort((a,b)=>a.observedAtUtc.localeCompare(b.observedAtUtc)||a.id.localeCompare(b.id));
    return {
        schemaVersion:1, transformVersion:'donki-sep-v1', sourceSha256:rawHash,
        displayLabel:'Archived NASA event records (2017)',
        limitations:'These event records do not supply Mars surface dose or predict conditions in 2059 or 2089.',
        events
    };
}
if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
    try {
        const root=path.resolve(process.argv[2] || '.');
        const raw=await fs.readFile(path.join(root,'sources/raw/donki-sep-2017.json'));
        const output=JSON.stringify(normalize(JSON.parse(raw),sha256(raw)),null,2)+'\n';
        await writeOnce(path.join(root,'samples/science/donki-events.json'),output);
        console.log(`Normalized ${JSON.parse(output).events.length} records; no dose invented.`);
    }catch(e){console.error(e.message);process.exitCode=1;}
}
