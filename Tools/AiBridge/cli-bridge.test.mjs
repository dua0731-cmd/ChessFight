import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, rm, readdir } from 'node:fs/promises';
import { join, dirname, resolve, basename } from 'node:path';
import { tmpdir } from 'node:os';
import { accountEnvironment, checkAccounts, invocation, runAccountRelay, runProcess } from './cli-bridge.mjs';

const config = () => ({ paths: { gpt: 'codex', claude: 'claude' }, models: {}, env: accountEnvironment({ PATH: process.env.PATH }) });
const status = (executable, args) => args.includes('status')
  ? executable === 'codex'
    ? { code: 0, stdout: '', stderr: 'Logged in using ChatGPT\n' }
    : { code: 0, stdout: JSON.stringify({ loggedIn: true, authMethod: 'claude.ai', email: 'never-record@example.invalid' }), stderr: '' }
  : null;
async function temporary(action) {
  const root = await mkdtemp(join(tmpdir(), 'chessfight-cli-test-'));
  try { return await action(root); }
  finally {
    if (dirname(root) !== resolve(tmpdir()) || !basename(root).startsWith('chessfight-cli-test-')) throw new Error('Unexpected cleanup path');
    await rm(root, { recursive: true, force: true });
  }
}

test('account mode strips API keys and alternate provider routes without changing the caller', () => {
  const source = { PATH: 'path', CODEX_HOME: 'profile', OPENAI_API_KEY: 'hidden', openai_api_key: 'hidden', ANTHROPIC_API_KEY: 'hidden',
    ANTHROPIC_AUTH_TOKEN: 'hidden', CODEX_ACCESS_TOKEN: 'hidden', CLAUDE_CODE_OAUTH_TOKEN: 'hidden',
    CLAUDE_CODE_USE_BEDROCK: '1', ANTHROPIC_BASE_URL: 'other', ANTHROPIC_PROFILE: 'api-profile' };
  const env = accountEnvironment(source);
  for (const name of Object.keys(source)) if (!['PATH', 'CODEX_HOME'].includes(name)) assert.equal(env[name], undefined);
  assert.equal(env.CODEX_HOME, 'profile');
  assert.equal(source.OPENAI_API_KEY, 'hidden');
});

test('native login checks accept subscription logins and exclude profile details', async () => {
  const state = await checkAccounts(config(), async (exe, args) => status(exe, args));
  assert.equal(state.gpt.ready, true);
  assert.equal(state.claude.ready, true);
  assert.doesNotMatch(JSON.stringify(state), /example.invalid|email/);
});

test('API-key login is rejected instead of being used as a fallback', async () => {
  const state = await checkAccounts(config(), async exe => exe === 'codex'
    ? { code: 0, stdout: 'Logged in using an API key', stderr: '' }
    : { code: 0, stdout: '{"loggedIn":true,"authMethod":"api_key"}', stderr: '' });
  assert.equal(state.gpt.ready, false);
  assert.equal(state.claude.ready, false);
});

test('missing native programs are reported without attempting to start them', async () => {
  const state = await checkAccounts({ ...config(), paths: {} }, async () => { throw new Error('must not run'); });
  assert.equal(state.gpt.ready, false);
  assert.equal(state.claude.ready, false);
});

test('native invocations keep permissions and expose no file editing tools', () => {
  const gpt = invocation('gpt', config(), 'exchange', 'answer.txt');
  assert.ok(gpt.args.indexOf('--ignore-user-config') > gpt.args.indexOf('exec'));
  assert.equal(gpt.args[gpt.args.indexOf('--sandbox') + 1], 'read-only');
  assert.equal(gpt.args[gpt.args.indexOf('--output-last-message') + 1], 'answer.txt');
  assert.ok(gpt.args.includes('forced_login_method="chatgpt"'));
  const claude = invocation('claude', config(), 'exchange', 'answer.txt');
  assert.equal(claude.args[claude.args.indexOf('--tools') + 1], '');
  assert.ok(claude.args.includes('--safe-mode'));
  assert.ok(claude.args.includes('--no-session-persistence'));
  assert.doesNotMatch([...gpt.args, ...claude.args].join(' '), /dangerously|bypassPermissions/);
});

test('GPT to Claude to GPT exchange preserves each answer in files and sends no API credentials', async () => temporary(async root => {
  const calls = [];
  const snapshots = [];
  const journal = await runAccountRelay(config(), { prompt: '구간 아이디어를 검토해줘', directory: root }, {
    runner: async (exe, args, options) => {
      const auth = status(exe, args);
      if (auth) return auth;
      calls.push({ exe, args, ...options });
      const text = `답변 ${calls.length}`;
      if (exe === 'codex') await writeFile(args[args.indexOf('--output-last-message') + 1], text, 'utf8');
      return { code: 0, stdout: exe === 'claude' ? JSON.stringify({ subtype: 'success', is_error: false, result: text, modelUsage: { 'claude-test': {} } }) : '', stderr: '' };
    },
    onUpdate: async state => snapshots.push(structuredClone(state)),
  });
  assert.deepEqual(calls.map(call => call.exe), ['codex', 'claude', 'codex']);
  assert.match(calls[1].input, /답변 1/);
  assert.match(calls[2].input, /답변 2/);
  assert.equal(journal.status, 'completed');
  assert.equal(journal.transport, 'account-cli');
  assert.equal(await readFile(join(root, 'final.md'), 'utf8'), '답변 3');
  assert.match(await readFile(join(root, '02-claude.request.txt'), 'utf8'), /답변 1/);
  assert.equal(await readFile(join(root, '02-claude.response.md'), 'utf8'), '답변 2');
  assert.equal(snapshots.at(-1).status, 'completed');
  assert.doesNotMatch(JSON.stringify(journal), /example.invalid|authMethod/);
  await assert.rejects(readdir(calls[0].cwd), { code: 'ENOENT' });
}));

test('Claude can start and explicit reference files appear in the exchanged request', async () => temporary(async root => {
  const providers = [];
  const journal = await runAccountRelay(config(), { prompt: 'test', first: 'claude', turns: 2, directory: root, context: [{ file: 'brief.md', text: '참고 문서' }] }, {
    runner: async (exe, args, options) => {
      const auth = status(exe, args);
      if (auth) return auth;
      providers.push(exe);
      assert.match(options.input, /참고 문서/);
      if (exe === 'codex') await writeFile(args[args.indexOf('--output-last-message') + 1], 'final');
      return { code: 0, stdout: '{"subtype":"success","result":"first"}', stderr: '' };
    },
  });
  assert.deepEqual(providers, ['claude', 'codex']);
  assert.equal(journal.answers.at(-1).text, 'final');
}));

test('an unavailable second account prevents the first model call', async () => temporary(async root => {
  let calls = 0;
  await assert.rejects(runAccountRelay(config(), { prompt: 'test', directory: root }, {
    runner: async (exe, args) => {
      if (!args.includes('status')) calls++;
      return exe === 'claude' ? { code: 1, stdout: '{"loggedIn":false,"authMethod":"none"}', stderr: '' } : status(exe, args);
    },
  }), /claude/);
  assert.equal(calls, 0);
}));

test('CLI error results with exit code zero are saved as failure and not forwarded', async () => temporary(async root => {
  let calls = 0;
  let last;
  await assert.rejects(runAccountRelay(config(), { prompt: 'test', first: 'claude', directory: root }, {
    runner: async (exe, args) => {
      const auth = status(exe, args);
      if (auth) return auth;
      calls++;
      return { code: 0, stdout: '{"subtype":"error_max_turns","is_error":true,"result":"private failure"}', stderr: '' };
    },
    onUpdate: async state => { last = structuredClone(state); },
  }), /正常|정상/);
  assert.equal(calls, 1);
  assert.equal(last.status, 'failed');
  assert.doesNotMatch(JSON.stringify(last), /private failure/);
}));

test('invalid requests never start a CLI', async () => temporary(async root => {
  for (const options of [{ prompt: '' }, { prompt: 'x', turns: 9 }, { prompt: 'x', first: 'unknown' }]) {
    await assert.rejects(runAccountRelay(config(), { ...options, directory: root }, { runner: async () => { throw new Error('must not run'); } }));
  }
}));

test('process runner transmits Korean input without shell interpolation', async () => {
  const input = '한글 "quotes" $() & ; `literal`';
  const result = await runProcess(process.execPath, ['-e', 'process.stdin.setEncoding("utf8");process.stdin.on("data",s=>process.stdout.write(s))'], { input });
  assert.equal(result.code, 0);
  assert.equal(result.stdout, input);
});

test('process runner timeout stops its own process', async () => {
  await assert.rejects(runProcess(process.execPath, ['-e', 'setInterval(()=>{},1000)'], { timeoutMs: 100 }), /대기 시간/);
});

test('process runner cancellation stops its own process', async () => {
  const controller = new AbortController();
  const promise = runProcess(process.execPath, ['-e', 'setInterval(()=>{},1000)'], { signal: controller.signal });
  setTimeout(() => controller.abort(), 100);
  await assert.rejects(promise, /중단/);
});
