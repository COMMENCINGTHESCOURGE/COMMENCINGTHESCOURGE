import fs from 'node:fs';
import path from 'node:path';

const DEFAULT_ROOTS = ['.'];
const DEFAULT_GLOBS = [
  '**/*.md',
  '**/*.yml',
  '**/*.yaml',
  '**/*.json',
  '**/*.ts',
  '**/*.tsx',
  '**/*.js',
  '**/*.jsx',
];

const PATTERNS = [
  { id: 'discovered_by', message: 'Avoid credit-bypassing assertions ("discovered by ...").', regex: /\b(discovered|discovery)\s+by\b/i },
  { id: 'first_european', message: 'Avoid framing Europe as the origin of record. Attribute prior civilizations/indigenous communities explicitly.', regex: /\bfirst\s+european\b/i },
  { id: 'western_invented', message: 'Avoid exclusive "western invented" framing. Cite global prior art when available.', regex: /\bwestern\s+(invented|originated|created|discovered)\b/i },
  { id: 'eurocentric_credit', message: 'Avoid Eurocentric credit language. Name multiple contributors across cultures.', regex: /\bgave\s+(credit|recognition|acknowledgement)\s+to\s+(europe|western|europ(ean|ocentric))\b/i },
  { id: 'columbus_discovery', message: 'Avoid "Columbus discovered ...". Reframe as documented contact or record prior inhabitants.', regex: /\bcolumbus\s+(discovered|discovery)\b/i },
  { id: 'livingstone_discovered', message: 'Avoid "Livingstone discovered ..." without local names.', regex: /\blivingstone\s+discovered\b/i },
  { id: 'everest_named_only', message: 'Avoid erasing native place names. Include both names from the start.', regex: /\bmount\s+everest\b/i },
  { id: 'gutenberg_invented', message: 'Avoid single-inventor myth; note Bi Sheng and earlier movable type.', regex: /\bgutenberg\s+invent/i },
  { id: 'western_adopted', message: 'Avoid "the West discovered/adopted X" framing.', regex: /\b(western\s+)?(adopted|discovered|exported)\s+from\b/i },
  { id: 'baghdad_battery_myth', message: 'Avoid un-caveated Baghdad Battery electricity claim.', regex: /\bbaghdad\s+battery\b/i },
  { id: 'native_american_erasure', message: 'Inclusive framing: avoid implying terrains were "unoccupied" or "unclaimed" before European contact.', regex: /\b(unoccupied|unclaimed|wilderness|virgin\s+land)\b/i },
];

const shouldIgnore = (relPath) => {
  if (relPath.includes('/node_modules/')) return true;
  if (relPath.includes('/.git/')) return true;
  if (relPath.includes('/dist/')) return true;
  if (relPath.includes('/coverage/')) return true;
  return false;
};

const matchesGlobs = (relPath, globs, excludes) => {
  const p = relPath.split(path.sep).join('/');
  if (excludes.some((ex) => {
    const re = new RegExp('^' + ex.replace(/\./g,'\.').replace(/\*\*/g,'(.*)').replace(/\*/g,'[^/]*') + '$', 'i');
    return re.test(p);
  })) return false;
  return globs.some((g) => {
    const regexStr = '^' + g.replace(/\./g,'\.').replace(/\*\*/g,'(.*)').replace(/\*/g,'[^/]*') + '$';
    try { return new RegExp(regexStr).test(p); } catch { return false; }
  });
};

const linesOf = (abs) => { try { return fs.readFileSync(abs, 'utf8').split(/\r?\n/); } catch { return []; } };

const walk = (root, globs, excludes) => {
  let out = [];
  if (!fs.existsSync(root)) return out;
  const stat = fs.statSync(root);
  if (stat.isFile()) {
    const rel = path.relative(process.cwd(), root);
    if (matchesGlobs(rel, globs, excludes)) out.push({ rel, abs: root });
    return out;
  }
  for (const e of fs.readdirSync(root, { withFileTypes: true })) {
    if (['.git','node_modules','dist'].includes(e.name)) continue;
    const abs = path.join(root, e.name);
    const rel = path.relative(process.cwd(), abs);
    if (shouldIgnore(rel)) continue;
    if (e.isDirectory()) out.push(...walk(abs, globs, excludes));
    else if (e.isFile() && matchesGlobs(rel, globs, excludes)) out.push({ rel, abs });
  }
  return out;
};

const failExit = (code) => process.exit(code || 2);
const required = /^(1|true|yes|on)$/i;
const FEATURE_FLAG = process.env.ATTRIBUTION_GUARD ?? 'on';
if (!required.test(FEATURE_FLAG)) { console.log('[attribution-guard] disabled via ATTRIBUTION_GUARD=' + FEATURE_FLAG); process.exit(0); }

const args = process.argv.slice(2);
const roots = [];
let globs = [...DEFAULT_GLOBS];
const excludes = ['**/node_modules/**', '**/dist/**', '**/coverage/**', '**/.git/**'];
let fail = false;

for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (a === '--roots' && typeof args[i+1] !== 'undefined') { i++; roots.push(args[++i]); continue; }
  if (a === '--glob' && typeof args[i+1] !== 'undefined') { i++; globs.push(args[++i]); continue; }
  if (a === '--exclude' && typeof args[i+1] !== 'undefined') { i++; excludes.push(args[++i]); continue; }
  if (a === '--fail-on-find') fail = true;
  else if (!a.startsWith('-')) roots.push(a);
}

const scanRoots = roots.length ? roots : DEFAULT_ROOTS;
const findings = [];
let files = [];
for (const root of scanRoots) files.push(...walk(root, globs, excludes));
files = files.filter((f, idx, arr) => arr.findIndex((x) => x.abs === f.abs) === idx);

for (const { rel, abs } of files) {
  const lines = linesOf(abs);
  for (let i = 0; i < lines.length; i++) {
    for (const p of PATTERNS) if (p.regex.test(lines[i])) findings.push({ rel, lineno: i + 1, line: lines[i], pattern: p });
  }
}

if (!findings.length) { console.log(`[attribution-guard] clean — checked ${files.length} files.`); process.exit(0); }

console.warn(`[attribution-guard] flagged ${findings.length} finding(s) in ${new Set(findings.map(f=>f.rel)).size} file(s):`);
for (const f of findings) {
  console.warn(`  ${f.rel}:${f.lineno}`);
  console.warn(`  pattern: ${f.pattern.id}`);
  console.warn(`  ${f.pattern.message}`);
  console.warn(`  line: ${f.line.trim()}`);
  console.warn('---');
}
if (fail) failExit(1);
console.warn('[attribution-guard] re-run with --fail-on-find to fail builds.');
process.exit(0);
