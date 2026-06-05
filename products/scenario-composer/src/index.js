import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';

const DEFAULT_TARGETS = new Set(['node24-ci-guard-complete','asset-pipeline-ci']);

const showHelp = () => {
  console.log(`
scenario-composer
  --scenario <name|path>   Run one scenario file (.scenario.json, .scenario.md, or JSON)
  --target <product>       Product to make active before composing (repeatable)
  --all-targets            Compose all known targets in dependency order
  --dry-run                Print execution plan without invoking scripts
  --help
`);
};

const parseArgs = (argv) => {
  const out = { scenario: null, targets: [], dryRun: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--scenario' && argv[i+1]) { out.scenario = argv[++i]; continue; }
    if (a === '--target' && argv[i+1]) { out.targets.push(argv[++i]); continue; }
    if (a === '--all-targets') { out.targets = []; break; }
    if (a === '--dry-run') { out.dryRun = true; }
    if (a === '--help' || a === '-h') { showHelp(); process.exit(0); }
  }
  return out;
};

const resolveScenario = (input) => {
  if (!input) throw new Error('Missing --scenario');
  if (fs.existsSync(input)) return path.resolve(input);
  const cwd = process.cwd();
  const candidates = [
    path.join(cwd, 'scenarios', `${input}.scenario.json`),
    path.join(cwd, 'scenarios', `${input}.scenario.md`),
    path.join(cwd, `${input}.scenario.json`),
    path.join(cwd, `${input}.scenario.md`),
    path.join(process.env.HOME || '', 'Projects', 'COMMENCINGTHESCOURGE', 'scenarios', `${input}.scenario.json`),
    path.join(process.env.HOME || '', 'Projects', 'COMMENCINGTHESCOURGE', 'scenarios', `${input}.scenario.md`),
  ].filter(fs.existsSync);
  if (!candidates.length) throw new Error(`Scenario not found: ${input}`);
  return path.resolve(candidates[0]);
};

const loadScenario = (file) => {
  const ext = path.extname(file).toLowerCase();
  const raw = fs.readFileSync(file, 'utf8');
  if (ext === '.json') return { file, raw: JSON.parse(raw), fmt: 'json' };
  const metaMatch = raw.match(/```json([\s\S]*?)```/);
  const body = metaMatch ? metaMatch[1] : raw;
  const json = JSON.parse(body);
  return { file, raw, fmt: 'md+json', json };
};

const activateTarget = (product, options) => new Promise((resolve, reject) => {
  const spawnOptions = { cwd: path.join(process.env.HOME || '', 'Projects', 'COMMENCINGTHESCOURGE', 'products', product), stdio: 'inherit' };
  console.log(`[scenario-composer] activate target=${product}`);
  const child = spawn('npm', ['run', 'compose', '--prefix', '..', '..', 'product', product], spawnOptions);
  child.on('exit', (code) => (code === 0 ? resolve(product) : reject(new Error(`target ${product} exited ${code}`))));
});

const executeScenario = (scenario) => new Promise((resolve, reject) => {
  console.log(`[scenario-composer] execute scenario=${scenario.file}`);
  resolve(scenario);
});

const main = () => {
  const argv = process.argv.slice(2);
  if (!argv.length) { showHelp(); process.exit(0); }
  const opts = parseArgs(argv);
  const scenarioPath = resolveScenario(opts.scenario);
  const scenario = loadScenario(scenarioPath);
  if (opts.dryRun) {
    console.log(JSON.stringify({ scenario: scenarioPath, targets: opts.targets.length ? opts.targets : [...DEFAULT_TARGETS] }, null, 2));
    process.exit(0);
  }
  const targets = opts.targets.length ? opts.targets : [...DEFAULT_TARGETS];
  (async () => {
    try {
      for (const target of targets) await activateTarget(target, opts);
      await executeScenario(scenario);
      console.log('[scenario-composer] done');
    } catch (e) {
      console.error(`[scenario-composer] ${e && e.message ? e.message : e}`);
      process.exit(2);
    }
  })();
};

main();
