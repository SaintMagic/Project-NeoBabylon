import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { once } from 'node:events';
import { createServer } from 'node:http';
import { existsSync } from 'node:fs';
import { mkdtemp, mkdir, rm, writeFile, readFile } from 'node:fs/promises';
import { createHash, randomUUID } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const project = path.join(root, 'host', 'NeoBabylon.NvidiaAdapter', 'NeoBabylon.NvidiaAdapter.csproj');
const runRoot = await mkdtemp(path.join(os.tmpdir(), 'neobabylon-nvidia-qa-'));
const wrapperSource = await readFile(path.join(root, 'host', 'NeoBabylon.Core', 'NvidiaAdapterProcess.cs'), 'utf8');
assert.match(wrapperSource, /\[JsonIgnore\]\s+public string SessionToken/);
assert.ok(wrapperSource.indexOf('destination.Clear();') < wrapperSource.indexOf('foreach (var name in new[] { "SystemRoot"'), 'adapter child environment must be cleared before copying the runtime allowlist');
for (const diagnostic of ['supportedScope', 'maximumRequestBodyBytes', 'encryptedReasoningUnavailable', 'reasoningReplay']) assert.ok(wrapperSource.includes('[' + JSON.stringify(diagnostic) + ']'), `missing adapter diagnostic ${diagnostic}`);
const managedBundleFiles = ['NeoBabylon.NvidiaAdapter.exe', 'NeoBabylon.NvidiaAdapter.dll', 'NeoBabylon.NvidiaAdapter.deps.json', 'NeoBabylon.NvidiaAdapter.runtimeconfig.json'];
for (const bundleFile of managedBundleFiles) assert.ok(wrapperSource.includes(JSON.stringify(bundleFile)), `wrapper bundle hash omits published file ${bundleFile}`);
assert.equal(wrapperSource.includes('NeoBabylon.Core.dll'), false, 'standalone adapter bundle must not require an unrelated Core assembly');
const names = ['deepseek-ai/deepseek-v4.1-flash', 'z-ai/glm-5.3', 'moonshotai/kimi-k3'];
const envBase = {};
for (const key of ['SystemRoot', 'WINDIR', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'PATH', 'TEMP', 'TMP', 'USERPROFILE', 'APPDATA', 'LOCALAPPDATA']) {
  if (process.env[key]) envBase[key] = process.env[key];
}
let upstream;
const seen = [];
const appServerBodies = [];
let mode = 'success';
let appServerCalls = 0;

try {
  const build = spawnSync('dotnet', ['publish', project, '-c', 'Release', '-p:NvidiaAdapterTestBuild=true', '-p:UseAppHost=true', '-o', runRoot], { cwd: root, encoding: 'utf8' });
  assert.equal(build.status, 0, 'test-only adapter build: ' + (build.stderr || build.stdout));
  const exe = path.join(runRoot, 'NeoBabylon.NvidiaAdapter.exe');
  for (const bundleFile of managedBundleFiles) assert.ok(existsSync(path.join(runRoot, bundleFile)), `publish did not produce ${bundleFile}`);
  assert.equal(existsSync(path.join(runRoot, 'NeoBabylon.Core.dll')), false, 'standalone adapter publish unexpectedly includes Core');
  upstream = createServer(async (req, res) => {
    const chunks = [];
    for await (const chunk of req) chunks.push(chunk);
    const request = { path: req.url, authorization: req.headers.authorization, body: JSON.parse(Buffer.concat(chunks).toString('utf8')) };
    seen.push(request);
    if (mode === 'appserver') appServerBodies.push(request.body);
    if (mode === 'error') {
      res.writeHead(429, { 'content-type': 'application/json' });
      res.end(JSON.stringify({ error: { code: 'rate_limit_exceeded', message: 'synthetic upstream failure' } }));
    } else if (mode === 'credential-error') {
      res.writeHead(403, { 'content-type': 'application/json' });
      res.end(JSON.stringify({ error: { code: 'SYNTHETIC-NVIDIA-KEY', message: 'synthetic failure' } }));
    } else if (mode === 'appserver') {
      appServerCalls++;
      const stream = chunks => {
        res.writeHead(200, { 'content-type': 'text/event-stream' });
        for (const chunk of chunks) res.write('data: ' + JSON.stringify(chunk) + '\n\n');
        res.end('data: [DONE]\n\n');
      };
      if (appServerCalls === 1) {
        const tool = request.body.tools?.find(item => item.function?.parameters?.properties?.cmd);
        if (!tool) {
          res.writeHead(422, { 'content-type': 'application/json' });
          res.end(JSON.stringify({ error: { code: 'mock_exec_command_not_advertised', message: 'fixture only' } }));
          return;
        }
        stream([
          { id: 'chatcmpl-appserver-1', model: request.body.model, choices: [{ index: 0, delta: { reasoning_content: 'opaque pinned-model reasoning', tool_calls: [{ index: 0, id: 'call_nb_mock_tool', type: 'function', function: { name: tool.function.name, arguments: JSON.stringify({ cmd: 'cmd.exe /d /c echo NB_NVIDIA_MOCK_TOOL_OK', workdir: root }) } }] }, finish_reason: null }] },
          { id: 'chatcmpl-appserver-1', model: request.body.model, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] }
        ]);
      } else {
        const output = JSON.stringify(request.body.messages);
        if (!output.includes('NB_NVIDIA_MOCK_TOOL_OK')) {
          res.writeHead(422, { 'content-type': 'application/json' });
          res.end(JSON.stringify({ error: { code: 'mock_tool_result_missing', message: 'fixture only' } }));
          return;
        }
        stream([
          { id: 'chatcmpl-appserver-2', model: request.body.model, choices: [{ index: 0, delta: { reasoning_content: 'opaque final reasoning', content: 'NB_NVIDIA_MOCK_TOOL_OK' }, finish_reason: null }] },
          { id: 'chatcmpl-appserver-2', model: request.body.model, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }
        ]);
      }
    } else if (mode === 'stream-length') {
      res.writeHead(200, { 'content-type': 'text/event-stream' });
      res.end('data: {"choices":[{"index":0,"delta":{"content":"partial","reasoning_content":"opaque"},"finish_reason":"length"}]}\n\ndata: [DONE]\n\n');
    } else {
      const message = mode === 'tool'
        ? { role: 'assistant', content: null, reasoning_content: 'opaque Kimi reasoning', tool_calls: [{ id: 'call_synth', type: 'function', function: { name: request.body.tools[0].function.name, arguments: '{"word":"ok"}' } }] }
        : { role: 'assistant', content: 'ok', reasoning_content: 'synthetic reasoning', tool_calls: [] };
      res.writeHead(200, { 'content-type': 'application/json' });
      res.end(JSON.stringify({ id: 'chatcmpl-test', model: request.body.model, choices: [{ index: 0, message, finish_reason: mode === 'tool' ? 'tool_calls' : 'stop' }], usage: { prompt_tokens: 4, completion_tokens: 1, total_tokens: 5 } }));
    }
  });
  upstream.listen(0, '127.0.0.1');
  await once(upstream, 'listening');
  const upstreamUrl = 'http://127.0.0.1:' + upstream.address().port + '/v1/chat/completions';

  async function start(model, efforts) {
    const token = 'synthetic-session-' + Math.random().toString(16).slice(2);
    const child = spawn(exe, [], { cwd: runRoot, env: { ...envBase, NEOBABYLON_NVIDIA_TEST_UPSTREAM: upstreamUrl }, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
    let stderr = '';
    child.stderr.setEncoding('utf8').on('data', chunk => { stderr += chunk; });
    const lines = readLines(child.stdout);
    child.stdin.end(JSON.stringify({ apiKey: 'SYNTHETIC-NVIDIA-KEY', modelIdentifier: model, sessionToken: token, supportedReasoningEfforts: efforts }) + '\n');
    const ready = JSON.parse(await lines.next());
    assert.equal(ready.provider, 'nvidia');
    assert.equal(ready.model, model);
    assert.match(ready.endpoint, /^http:\/\/127\.0\.0\.1:/);
    return { child, ready, token, stderr: () => stderr };
  }
  const send = (server, payload, token = server.token, route = '/responses', method = 'POST') => fetch(server.ready.endpoint + route, { method, headers: { authorization: 'Bearer ' + token, 'content-type': 'application/json' }, body: method === 'POST' ? JSON.stringify(payload) : undefined });
  const stop = async server => { if (server.child.exitCode === null) { server.child.kill(); await Promise.race([once(server.child, 'exit'), new Promise(resolve => setTimeout(resolve, 1500))]); } };

  const kimi = await start(names[2], ['low', 'high', 'max']);
  try {
    const ok = await send(kimi, {
      model: names[2], instructions: 'system rule', input: [{ type: 'message', role: 'user', content: [{ type: 'input_text', text: 'hello' }] }],
      tools: [{ type: 'function', name: 'exec_command', namespace: 'codex', description: 'run', parameters: { type: 'object', properties: {} } }],
      tool_choice: 'auto', parallel_tool_calls: true, reasoning: { effort: 'high' }, store: false, stream: false,
      max_output_tokens: 41, include: [], prompt_cache_key: 'synthetic-cache', client_metadata: { thread_id: 'synthetic-thread' }
    });
    assert.equal(ok.status, 200);
    const request = seen.at(-1);
    assert.equal(request.path, '/v1/chat/completions');
    assert.equal(request.authorization, 'Bearer SYNTHETIC-NVIDIA-KEY');
    assert.deepEqual(request.body.messages, [{ role: 'system', content: 'system rule' }, { role: 'user', content: 'hello' }]);
    assert.equal(request.body.max_tokens, 41);
    assert.equal(request.body.reasoning_effort, 'high');
    assert.equal(request.body.tools[0].function.name.startsWith('nbx_'), true);
    const output = await ok.json();
    assert.equal(output.status, 'completed');
    assert.equal(output.output.find(x => x.type === 'message').content[0].text, 'ok');
    assert.equal(output.usage.input_tokens, 4);
    assert.equal(output.usage.output_tokens, 1);
    assert.equal(output.model, names[2]);

    const toolName = 'cmd_' + 'x'.repeat(70) + '雪';
    const functionTools = [{ type: 'namespace', name: 'codex_tool_space', description: 'synthetic namespace', tools: [
      { type: 'function', name: toolName, description: 'local command', parameters: { type: 'object', properties: { word: { type: 'string' } } }, strict: false }
    ] }];
    mode = 'tool';
    const callResponse = await send(kimi, {
      model: names[2], instructions: '', input: [{ type: 'message', role: 'user', content: [{ type: 'input_text', text: 'run' }] }],
      tools: functionTools, tool_choice: 'auto', parallel_tool_calls: true, reasoning: { effort: null, summary: 'none' },
      store: false, stream: false, max_output_tokens: 41, include: ['reasoning.encrypted_content'],
      client_metadata: { thread_id: 'thread-one' }
    });
    assert.equal(callResponse.status, 200);
    assert.match(callResponse.headers.get('x-neobabylon-adapter-warnings'), /encrypted_content is unavailable/);
    const callResult = await callResponse.json();
    const reasoningItem = callResult.output.find(x => x.type === 'reasoning');
    const functionItem = callResult.output.find(x => x.type === 'function_call');
    assert.equal(functionItem.name, toolName);
    assert.equal(functionItem.namespace, 'codex_tool_space');
    assert.equal(functionItem.arguments, '{"word":"ok"}');
    assert.equal(seen.at(-1).body.tools[0].function.name.length <= 64, true);
    assert.match(seen.at(-1).body.tools[0].function.name, /^nbx_[0-9a-f]{24}$/);

    const continuation = {
      model: names[2], instructions: '', input: [
        { type: 'message', role: 'user', content: [{ type: 'input_text', text: 'run' }] },
        { type: 'reasoning', id: reasoningItem.id, summary: [], content: reasoningItem.content, encrypted_content: null },
        functionItem,
        { type: 'function_call_output', call_id: functionItem.call_id, output: 'command result' }
      ],
      tools: functionTools, tool_choice: 'auto', parallel_tool_calls: true, reasoning: {}, store: false,
      stream: false, max_output_tokens: 41, include: [], client_metadata: { thread_id: 'thread-one' }
    };
    mode = 'success';
    const continued = await send(kimi, continuation);
    assert.equal(continued.status, 200, await continued.clone().text());
    const replayed = seen.at(-1).body.messages;
    assert.equal(replayed[0].role, 'user');
    assert.equal(replayed[1].role, 'assistant');
    assert.equal(replayed[1].reasoning_content, 'opaque Kimi reasoning');
    assert.equal(replayed[1].tool_calls[0].id, 'call_synth');
    assert.equal(replayed[1].tool_calls[0].function.name, seen[seen.length - 2].body.tools[0].function.name);
    assert.deepEqual(replayed[2], { role: 'tool', tool_call_id: 'call_synth', content: 'command result' });
    const mixedThread = await send(kimi, { ...continuation, client_metadata: { thread_id: 'thread-two' } });
    assert.equal(mixedThread.status, 400, 'cached assistant messages must be thread identity bound');

    mode = 'stream-length';
    const streamed = await send(kimi, {
      model: names[2], input: [{ type: 'message', role: 'user', content: [{ type: 'input_text', text: 'stream' }] }],
      tools: [], tool_choice: 'auto', parallel_tool_calls: true, reasoning: {}, store: false, stream: true,
      max_output_tokens: 3, include: [], client_metadata: { thread_id: 'thread-one' }
    });
    assert.equal(streamed.status, 200);
    const sse = await streamed.text();
    assert.match(sse, /response\.output_text\.delta/);
    assert.match(sse, /"delta":"partial"/);
    assert.match(sse, /response\.incomplete/);
    assert.doesNotMatch(sse, /response\.completed/);
    mode = 'success';

    for (const [label, value] of [
      ['model mismatch', { model: names[0], input: [], stream: false }],
      ['stateful previous response', { model: names[2], input: [], previous_response_id: 'old', stream: false }],
      ['image input', { model: names[2], input: [{ type: 'message', role: 'user', content: [{ type: 'input_image', image_url: 'https://invalid' }] }], stream: false }],
      ['unadvertised effort', { model: names[2], input: [], reasoning: { effort: 'medium' }, stream: false }],
      ['unsupported include', { model: names[2], input: [], include: ['reasoning.unknown'], stream: false }],
      ['unknown semantic flag', { model: names[2], input: [], temperature: 0.2, stream: false }],
      ['malformed function arguments', { model: names[2], input: [{ type: 'function_call', name: 'f', arguments: '{bad', call_id: 'call_bad' }], stream: false }]
    ]) {
      const response = await send(kimi, value);
      assert.equal(response.status, 400, label);
      assert.equal((await response.json()).error.type, 'invalid_request_error', label);
    }
    assert.equal((await send(kimi, { model: names[2], input: [], stream: false }, 'wrong')).status, 401);
    assert.equal((await send(kimi, {}, kimi.token, '/health')).status, 404);
    assert.equal((await send(kimi, {}, kimi.token, '/responses', 'GET')).status, 404);
    mode = 'error';
    const failed = await send(kimi, { model: names[2], input: [], stream: false, max_output_tokens: 20, store: false, include: [], parallel_tool_calls: true, tool_choice: 'auto', client_metadata: { thread_id: 'synthetic-thread' } });
    assert.equal(failed.status, 429);
    const failedBody = await failed.json();
    assert.equal(failedBody.error.type, 'upstream_error');
    assert.equal(failedBody.error.code, 'rate_limit_exceeded');
    mode = 'credential-error';
    const planted = await send(kimi, { model: names[2], input: [], stream: false, max_output_tokens: 20, store: false, include: [], parallel_tool_calls: true, tool_choice: 'auto', client_metadata: { thread_id: 'synthetic-thread' } });
    assert.equal(planted.status, 403);
    assert.equal((await planted.text()).includes('SYNTHETIC-NVIDIA-KEY'), false);
    mode = 'success';
  } finally {
    await stop(kimi);
    assert.doesNotMatch(kimi.stderr(), /SYNTHETIC|synthetic reasoning|synthetic-cache/);
  }
  const ds = await start(names[0], []);
  try {
    const dsCommon = { model: names[0], input: [], stream: false, store: false, include: [], parallel_tool_calls: true, tool_choice: 'auto', max_output_tokens: 12 };
    assert.equal((await send(ds, { ...dsCommon, reasoning: { effort: 'low' } })).status, 400, 'do not map string effort to numeric NVIDIA effort');
    assert.equal((await send(ds, dsCommon)).status, 200);
    const reportRoot = path.join(root, '.local', 'Lab', 'Runs', 'Nvidia-20261004', 'adapter');
    await mkdir(reportRoot, { recursive: true });
    const lock = JSON.parse(await readFile(path.join(root, 'runtime', 'runtime-lock.json'), 'utf8'));
    const appServerExe = path.resolve(root, lock.runtime.appServerBinaryRelativePath);
    const binaryHash = createHash('sha256').update(await readFile(appServerExe)).digest('hex');
    assert.equal(binaryHash, lock.runtime.sha256, 'locked App Server binary hash');
    const kimiIntegration = await start(names[2], ['low', 'high', 'max']);
    try {
      mode = 'appserver';
      appServerCalls = 0;
      appServerBodies.length = 0;
      await appServerRoundTrip({ reportRoot, appServerExe, server: kimiIntegration, model: names[2] });
    } finally { await stop(kimiIntegration); }
    mode = 'success';
  } finally { await stop(ds); }
  console.log('PASS focused NVIDIA adapter mock-upstream checks');
} finally {
  if (upstream) upstream.close();
  await rm(runRoot, { recursive: true, force: true });
}

async function appServerRoundTrip({ reportRoot, appServerExe, server, model }) {
  const home = path.join(reportRoot, 'locked-appserver-codex-home-' + randomUUID());
  await mkdir(home, { recursive: false });
  const catalogPath = path.join(home, 'model-catalog.json');
  await writeFile(catalogPath, JSON.stringify({ models: [{
    slug: model, display_name: model, description: 'Isolated NVIDIA adapter qualification fixture',
    default_reasoning_level: 'high', supported_reasoning_levels: ['low', 'high', 'max'].map(effort => ({ effort, description: effort })), shell_type: 'unified_exec', visibility: 'none',
    supported_in_api: true, priority: 99, additional_speed_tiers: [], service_tiers: [], default_service_tier: null,
    availability_nux: null, upgrade: null, model_messages: null, base_instructions: 'Use the presented function tool when needed.',
    include_skills_usage_instructions: false, include_plugin_usage_instructions: false, include_apps_usage_instructions: false,
    supports_reasoning_summary_parameter: false, default_reasoning_summary: 'none', support_verbosity: false, default_verbosity: null,
    apply_patch_tool_type: 'function', web_search_tool_type: 'text', truncation_policy: { mode: 'bytes', limit: 10000 },
    supports_image_detail_original: false, context_window: 1048576, max_context_window: 1048576, auto_compact_token_limit: null,
    comp_hash: null, effective_context_window_percent: 95, experimental_supported_tools: [], input_modalities: ['text'],
    supports_search_tool: false, supports_experimental_context: false, use_responses_lite: false, node_repl_auto_review_required: false,
    node_repl_disabled: true, auto_review_model_override: null, model_specialty: null, tool_mode: null,
    multi_agent_version: null, multi_agent_reasoning_effort: null
  }] }), { flag: 'wx' });
  const config = `model = ${JSON.stringify(model)}\nmodel_provider = "nvidia"\nmodel_catalog_json = ${JSON.stringify(catalogPath.replaceAll('\\', '/'))}\nmodel_context_window = 1048576\napproval_policy = "never"\ndefault_permissions = ":danger-full-access"\nsandbox_mode = "danger-full-access"\nallow_login_shell = false\nmodel_reasoning_effort = "high"\nweb_search = "disabled"\n\n[model_providers.nvidia]\nname = "NVIDIA adapter qualification"\nbase_url = ${JSON.stringify(server.ready.endpoint)}\nwire_api = "responses"\nenv_key = "NEOBABYLON_PROVIDER_SESSION_TOKEN"\nrequires_openai_auth = false\nrequest_max_retries = 0\nstream_max_retries = 0\nsupports_websockets = false\nsupports_standalone_web_search = false\n\n[shell_environment_policy]\nexclude = ["NEOBABYLON_PROVIDER_SESSION_TOKEN"]\nexperimental_use_profile = false\n`;
  await writeFile(path.join(home, 'config.toml'), config, { flag: 'wx' });
  const env = {};
  for (const key of ['SystemRoot', 'WINDIR', 'COMSPEC', 'PATHEXT', 'PATH', 'TEMP', 'TMP', 'USERPROFILE']) if (process.env[key]) env[key] = process.env[key];
  Object.assign(env, { CODEX_HOME: home, NEOBABYLON_PROVIDER_SESSION_TOKEN: server.token });
  const child = spawn(appServerExe, [], { cwd: root, env, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
  const lines = readJsonLines(child.stdout);
  let stderrBytes = 0;
  child.stderr.on('data', chunk => { stderrBytes += chunk.length; });
  let nextId = 0;
  const sendRpc = (method, params) => new Promise((resolve, reject) => {
    const id = ++nextId;
    const timer = setTimeout(() => reject(new Error(`App Server RPC timeout: ${method}`)), 15000);
    const onMessage = message => {
      if (message.id === id) { clearTimeout(timer); resolve(message); return true; }
      return false;
    };
    lines.waiters.push(onMessage);
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, ...(params === undefined ? {} : { params }) }) + '\n');
  });
  const events = [];
  lines.onMessage = message => events.push(message);
  try {
    const init = await sendRpc('initialize', { clientInfo: { name: 'nvidia-adapter-qa', version: '1' }, capabilities: { experimentalApi: false } });
    assert.ok(init.result, JSON.stringify(init.error));
    child.stdin.write('{"jsonrpc":"2.0","method":"initialized"}\n');
    const started = await sendRpc('thread/start', { cwd: root, model, modelProvider: 'nvidia', approvalPolicy: 'never', sandbox: 'danger-full-access', allowProviderModelFallback: false });
    assert.ok(started.result?.thread?.id, JSON.stringify(started.error));
    const threadId = started.result.thread.id;
    const turn = await sendRpc('turn/start', { threadId, input: [{ type: 'text', text: 'Use the available command tool to run exactly `cmd.exe /d /c echo NB_NVIDIA_MOCK_TOOL_OK`, then report its output.' }], sandboxPolicy: { type: 'dangerFullAccess' }, approvalPolicy: 'never', maxOutputTokens: 256 });
    assert.ok(turn.result?.turn?.id, JSON.stringify(turn.error));
    const turnId = turn.result.turn.id;
    const deadline = Date.now() + 30000;
    let completed;
    while (Date.now() < deadline) {
      const event = await lines.nextMessage(10000);
      if (event?.method === 'turn/completed' || event?.method === 'turn/failed') {
        if (event.params?.turn?.id === turnId) { completed = event; break; }
      }
    }
    assert.equal(completed?.method, 'turn/completed', `turn did not complete; events=${JSON.stringify(events.map(event => ({ method: event.method, item: event.params?.item ? { type: event.params.item.type, status: event.params.item.status, command: event.params.item.command, output: event.params.item.aggregatedOutput, exitCode: event.params.item.exitCode } : undefined, turn: event.params?.turn ? { id: event.params.turn.id, status: event.params.turn.status, error: event.params.turn.error } : undefined })))}; upstreamCalls=${appServerCalls}; turn=${JSON.stringify(completed?.params?.turn)}`);
    assert.ok(appServerCalls >= 2, `expected adapter requests; saw ${appServerCalls}; methods=${JSON.stringify(events.map(event => event.method).filter(Boolean))}; turn=${JSON.stringify(completed?.params?.turn)}`);
    assert.ok(seen.filter(req => req.body.model === model).length >= 2, 'expected tool call and post-tool continuation requests');
    assert.ok(appServerBodies.some(body => JSON.stringify(body.messages).includes('NB_NVIDIA_MOCK_TOOL_OK')), 'tool result did not return to the adapter: ' + JSON.stringify(appServerBodies.map(body => body.messages)));
    assert.ok(JSON.stringify(completed.params.turn).includes('NB_NVIDIA_MOCK_TOOL_OK'), 'assistant final did not contain marker');
    await writeFile(path.join(reportRoot, 'locked-appserver-roundtrip.json'), JSON.stringify({
      runtimeVersion: '0.155.1', runtimeSha256: JSON.parse(await readFile(path.join(root, 'runtime', 'runtime-lock.json'), 'utf8')).runtime.sha256,
      model, adapterEndpoint: server.ready.endpoint, appServerCalls,
      toolResultObserved: true, assistantFinalObserved: true, stderrBytes
    }, null, 2), { flag: 'w' });
  } finally {
    child.stdin.end();
    if (child.exitCode === null) {
      const exited = once(child, 'exit').catch(() => {});
      await Promise.race([exited, new Promise(resolve => setTimeout(resolve, 2000))]);
      if (child.exitCode === null) child.kill();
    }
  }
}

function readLines(stream) {
  let buffer = '';
  const ready = [];
  const waiters = [];
  stream.setEncoding('utf8');
  stream.on('data', chunk => {
    buffer += chunk;
    while (buffer.includes('\n')) {
      const i = buffer.indexOf('\n');
      const line = buffer.slice(0, i);
      buffer = buffer.slice(i + 1);
      if (waiters.length) waiters.shift()(line); else ready.push(line);
    }
  });
  stream.on('error', error => { while (waiters.length) waiters.shift()(Promise.reject(error)); });
  return { next: () => ready.length ? Promise.resolve(ready.shift()) : new Promise(resolve => waiters.push(resolve)) };
}

function readJsonLines(stream) {
  let buffer = '';
  const waiters = [];
  const backlog = [];
  let onMessage = null;
  stream.setEncoding('utf8');
  stream.on('data', chunk => {
    buffer += chunk;
    while (buffer.includes('\n')) {
      const index = buffer.indexOf('\n');
      const line = buffer.slice(0, index); buffer = buffer.slice(index + 1);
      let message; try { message = JSON.parse(line); } catch { continue; }
      onMessage?.(message);
      let claimed = false;
      for (let i = 0; i < waiters.length; i++) if (waiters[i](message)) { waiters.splice(i, 1); i--; claimed = true; }
      if (!claimed) backlog.push(message);
    }
  });
  stream.on('error', error => { for (const waiter of waiters.splice(0)) waiter({ error: error.message }); });
  return {
    waiters,
    set onMessage(handler) { onMessage = handler; },
    nextMessage: timeout => new Promise(resolve => {
      if (backlog.length) { resolve(backlog.shift()); return; }
      let timer;
      const waiter = message => { clearTimeout(timer); resolve(message); return true; };
      waiters.push(waiter);
      timer = setTimeout(() => { const i = waiters.indexOf(waiter); if (i >= 0) waiters.splice(i, 1); resolve(null); }, timeout);
    })
  };
}
