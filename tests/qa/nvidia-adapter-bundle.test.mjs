import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const project = path.join(root, 'host', 'NeoBabylon.NvidiaAdapter', 'NeoBabylon.NvidiaAdapter.csproj');
const output = await mkdtemp(path.join(os.tmpdir(), 'neobabylon-nvidia-bundle-'));
const files = [
  'NeoBabylon.NvidiaAdapter.exe',
  'NeoBabylon.NvidiaAdapter.dll',
  'NeoBabylon.NvidiaAdapter.deps.json',
  'NeoBabylon.NvidiaAdapter.runtimeconfig.json'
];
try {
  const result = spawnSync('dotnet', ['publish', project, '-c', 'Release', '-p:NvidiaAdapterTestBuild=true', '-o', output], { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr || result.stdout);
  for (const file of files) assert.ok(existsSync(path.join(output, file)), `missing published bundle file ${file}`);
  assert.equal(existsSync(path.join(output, 'NeoBabylon.Core.dll')), false, 'standalone adapter unexpectedly references Core');
  const wrapper = await readFile(path.join(root, 'host', 'NeoBabylon.Core', 'NvidiaAdapterProcess.cs'), 'utf8');
  for (const file of files) assert.ok(wrapper.includes(JSON.stringify(file)), `wrapper identity hash omits ${file}`);
  assert.equal(wrapper.includes('NeoBabylon.Core.dll'), false, 'wrapper identity hash must not require an unpublished Core DLL');
  console.log('PASS NVIDIA adapter published-bundle manifest');
} finally {
  await rm(output, { recursive: true, force: true });
}
