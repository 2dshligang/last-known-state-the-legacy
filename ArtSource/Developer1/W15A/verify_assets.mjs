import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const manifest = JSON.parse(readFileSync(new URL('./sources.json', import.meta.url), 'utf8'));
const root = new URL('../../../', import.meta.url);
const imports = [...manifest.external, ...manifest.original.assets];
const guids = new Set();

for (const asset of imports) {
  const url = new URL(asset.unityPath, root);
  const filename = fileURLToPath(url);
  const digest = createHash('sha256').update(readFileSync(filename)).digest('hex');
  const golden = asset.sha256 ?? asset.originalSha256;
  if (digest !== golden || (asset.importedSourceSha256 && digest !== asset.importedSourceSha256))
    throw new Error(`Source hash differs or LFS content is missing: ${asset.unityPath}`);

  const meta = readFileSync(filename + '.meta', 'utf8');
  const guid = /^guid: ([a-f0-9]{32})\r?$/m.exec(meta)?.[1];
  if (guid !== asset.unityGuid || guids.has(guid))
    throw new Error(`Missing, changed or duplicate GUID: ${asset.unityPath}`);
  guids.add(guid);

  if (asset.status === 'approved-imported' &&
      (!/^  textureType: 1\r?$/m.test(meta) || asset.license !== 'CC0-1.0'))
    throw new Error(`Incorrect format or license: ${asset.unityPath}`);
}

console.log(`Verified ${manifest.external.length} external textures and ${manifest.original.assets.length} original meshes.`);
