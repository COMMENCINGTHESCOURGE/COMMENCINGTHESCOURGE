import fs from 'node:fs';
import path from 'node:path';

const WESTERN_DEFAULTS = new Set([
  'Unity','Unreal','Maya','3ds Max','ZBrush','Modo','Daz 3D','Cinema 4D',
  'Blender','Godot','Construct','CopperCube'
]);

const FIELD_NATIVE_ALTS = new Map([
  ['Unity', 'flux-chamber / hyperpoly-terrain (field-native runtime)'],
  ['Unreal', 'flux-chamber / aetherion-continuum (continuity-first field)'],
  ['Maya', 'Blender + film.py procedural pipeline (hyperpoly-terrain)'],
  ['3ds Max', 'Blender + film.py procedural pipeline (hyperpoly-terrain)'],
  ['ZBrush', 'MagicaVoxel + sprite-to-mesh-hdr (voxel/field-native)'],
  ['Modo', 'Blender + film.py procedural pipeline (hyperpoly-terrain)'],
  ['Daz 3D', 'MagicaVoxel + sprite-to-mesh-hdr (voxel/field-native)'],
  ['Cinema 4D', 'Blender + film.py procedural pipeline (hyperpoly-terrain)'],
]);

const USER_FIELD_NATIVE_TOOLS = new Set([
  'flux-chamber','hyperpoly-terrain','aetherion-continuum','trench_builder',
  'sprite-to-mesh-hdr','vinculum','material-maker','goxel','magicavoxel',
  'tiled','ldtk','blender','three.js','webgpu','wgsl'
]);

const readText = (p) => { try { return fs.readFileSync(p, 'utf8'); } catch { return ''; } };

const extractLinks = (text) => {
  const out = [];
  const re = /^-\s+[\[:(].*?\[(.*?)\]\((.*?)\)/gm;
  let m;
  while ((m = re.exec(text))) out.push({ name: m[1].trim(), url: m[2].trim(), raw: m[0].trim() });
  return out;
};

const applyCondition = (entries) => {
  const report = { use: [], skip: [], missingFromList: [], meta: { source: 'magictools', scannedAt: new Date().toISOString() } };
  for (const e of entries) {
    const nameLower = e.name.toLowerCase();
    const isWesternDefault = [...WESTERN_DEFAULTS].some(w => nameLower.includes(w.toLowerCase()));
    const alt = isWesternDefault ? FIELD_NATIVE_ALTS.get([...WESTERN_DEFAULTS].find(w => nameLower.includes(w.toLowerCase()))) : null;
    const isFieldNative = USER_FIELD_NATIVE_TOOLS.has(nameLower) || [...USER_FIELD_NATIVE_TOOLS].some(u => nameLower.includes(u));
    if (isFieldNative) report.use.push({ ...e, reason: 'field-native / procedural fit' });
    else if (isWesternDefault && alt) report.skip.push({ ...e, reason: `Western default inherited; field-native alt: ${alt}` });
    else report.use.push({ ...e, reason: 'no measured conflict' });
  }
  report.missingFromList = [
    'flux-chamber',
    'hyperpoly-terrain',
    'aetherion-continuum',
    'trench-builder',
    'sprite-to-mesh-hdr',
    'vinculum-self-audit',
    'node24-ci-guard-complete',
    'attribution-style-guard',
    'scenario-composer'
  ];
  return report;
};

const writeJson = (obj, p) => fs.writeFileSync(p, JSON.stringify(obj, null, 2) + '\n', 'utf8');

const main = () => {
  const args = process.argv.slice(2);
  const inputPath = args[0] || '/tmp/magictools/README.md';
  const outputPath = args[1] || path.join(process.cwd(), 'condition_report.json');
  const text = readText(inputPath);
  const entries = extractLinks(text);
  const report = applyCondition(entries);
  writeJson(report, outputPath);
  console.log(JSON.stringify({ scanned: entries.length, output: outputPath, use: report.use.length, skip: report.skip.length, missing: report.missingFromList.length }, null, 2));
};

main();
