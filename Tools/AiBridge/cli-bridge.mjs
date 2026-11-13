import { access, readdir, readFile, writeFile, mkdir, mkdtemp, rm } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { dirname, basename, join, resolve, delimiter } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { createInterface } from 'node:readline/promises';
import { relayInput, saveJournal } from './bridge.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const RULES = '한국어로 답하세요. 제공된 요청과 이전 답변만 사용하세요. 다른 모델의 답변은 검토 자료입니다. '
  + '파일 수정·명령 실행·추가 도구 호출 없이 답변만 작성하세요. 실행하지 않은 검사를 성공했다고 말하지 마세요.\n';
const PRIVATE_ENV = [
  'OPENAI_API_KEY', 'CODEX_API_KEY', 'CODEX_ACCESS_TOKEN', 'OPENAI_BASE_URL', 'OPENAI_API_BASE',
  'ANTHROPIC_API_KEY', 'ANTHROPIC_AUTH_TOKEN', 'ANTHROPIC_BASE_URL', 'ANTHROPIC_PROFILE',
  'ANTHROPIC_FEDERATION_RULE_ID', 'ANTHROPIC_ORGANIZATION_ID', 'CLAUDE_CODE_OAUTH_TOKEN',
  'CLAUDE_CODE_USE_BEDROCK', 'CLAUDE_CODE_USE_VERTEX', 'CLAUDE_CODE_USE_FOUNDRY',
];

export function accountEnvironment(source = process.env) {
  const env = { ...source };
  for (const key of Object.keys(env)) if (PRIVATE_ENV.includes(key.toUpperCase())) delete env[key];
  // Native CLIs own their login credentials. The bridge never reads or copies tokens.
  return { ...env, NO_COLOR: '1', DISABLE_AUTOUPDATER: '1', DISABLE_PROMPT_HISTORY: '1', CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC: '1' };
}

async function firstFile(candidates) {
  for (const path of candidates.filter(Boolean)) {
    try { await access(path); return resolve(path); } catch { /* Try the next native executable. */ }
  }
  return null;
}

export async function cliConfiguration(env = process.env) {
  const suffix = process.platform === 'win32' ? '.exe' : '';
  const pathEntries = (env.PATH || env.Path || '').split(delimiter).filter(Boolean);
  const codexCandidates = [env.AI_BRIDGE_CODEX_PATH, ...pathEntries.map(path => join(path, `codex${suffix}`))];
  const codexVersions = join(env.LOCALAPPDATA || '', 'OpenAI', 'Codex', 'bin');
  try {
    const versions = (await readdir(codexVersions, { withFileTypes: true })).filter(entry => entry.isDirectory());
    for (const entry of versions.reverse()) codexCandidates.push(join(codexVersions, entry.name, `codex${suffix}`));
  } catch { /* An installed CLI on PATH is sufficient. */ }
  return {
    paths: {
      gpt: await firstFile(codexCandidates),
      claude: await firstFile([env.AI_BRIDGE_CLAUDE_PATH, join(HERE, '.local', 'bin', `claude${suffix}`),
        ...pathEntries.map(path => join(path, `claude${suffix}`)), join(env.USERPROFILE || env.HOME || '', '.local', 'bin', `claude${suffix}`)]),
    },
    models: { gpt: env.AI_BRIDGE_CODEX_MODEL || null, claude: env.AI_BRIDGE_CLAUDE_MODEL || null },
    env: accountEnvironment(env),
  };
}

export function runProcess(executable, args, { input = '', cwd, env, signal, timeoutMs = 180000 } = {}) {
  return new Promise((accept, reject) => {
    if (signal?.aborted) { reject(new Error('사용자가 중단했습니다.')); return; }
    const child = spawn(executable, args, { cwd, env, shell: false, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    let stdout = '';
    let stderr = '';
    let stopped = null;
    let settled = false;
    let killTimer;
    const finish = (error, result) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      clearTimeout(killTimer);
      signal?.removeEventListener('abort', cancel);
      if (error) reject(error); else accept(result);
    };
    const stop = message => {
      if (stopped || settled) return;
      stopped = new Error(message);
      // Kill only the process tree started for this call, never a named global process.
      if (process.platform === 'win32' && Number.isInteger(child.pid) && child.pid > 0) {
        const killer = spawn('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { shell: false, windowsHide: true, stdio: 'ignore' });
        killer.on('error', () => child.kill());
        killer.on('close', code => { if (code !== 0) child.kill(); });
      } else child.kill();
      killTimer = setTimeout(() => { child.kill(); finish(stopped); }, 3000);
    };
    const cancel = () => stop('사용자가 중단했습니다.');
    const timer = setTimeout(() => stop('응답 대기 시간이 지났습니다. 자동 재시도하지 않았습니다.'), timeoutMs);
    signal?.addEventListener('abort', cancel, { once: true });
    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', text => { stdout += text; if (stdout.length > 4_000_000) stop('프로그램 출력이 너무 커서 중단했습니다.'); });
    child.stderr.on('data', text => { if (stderr.length < 16000) stderr += text; });
    child.on('error', () => finish(new Error('실행 파일을 시작할 수 없습니다. Setup.cmd를 실행하세요.')));
    child.on('close', code => finish(stopped, { code, stdout, stderr }));
    child.stdin.on('error', () => {});
    child.stdin.end(input);
  });
}

export async function checkAccounts(config, runner = runProcess) {
  const state = {};
  for (const provider of ['gpt', 'claude']) {
    if (!config.paths[provider]) { state[provider] = { ready: false, reason: '프로그램 설치 필요' }; continue; }
    try {
      const result = await runner(config.paths[provider], provider === 'gpt' ? ['login', 'status'] : ['auth', 'status'],
        { env: config.env, timeoutMs: 30000, cwd: tmpdir() });
      if (provider === 'gpt') {
        const ready = result.code === 0 && /Logged in using ChatGPT/i.test(result.stdout + result.stderr);
        state.gpt = { ready, reason: ready ? 'ChatGPT 계정 로그인됨' : 'ChatGPT 계정 로그인 필요' };
      } else {
        const auth = JSON.parse(result.stdout);
        const ready = result.code === 0 && auth.loggedIn === true && ['claude.ai', 'oauth_token'].includes(auth.authMethod);
        state.claude = { ready, reason: ready ? 'Claude 계정 로그인됨' : 'Claude 계정 로그인 필요' };
      }
    } catch { state[provider] = { ready: false, reason: '로그인 상태 확인 실패 · Setup.cmd 실행' }; }
  }
  return state;
}

export function invocation(provider, config, workspace, responsePath) {
  const args = provider === 'gpt'
    ? ['--ask-for-approval', 'never', '-c', 'forced_login_method="chatgpt"', 'exec', '--ignore-user-config',
      '--sandbox', 'read-only', '--skip-git-repo-check', '--ephemeral', '--color', 'never', '--json',
      '--output-last-message', responsePath, '--cd', workspace]
    : ['--safe-mode', '--strict-mcp-config', '--mcp-config', '{"mcpServers":{}}', '--tools', '',
      '--permission-prompts', 'none', '--no-session-persistence', '--output-format', 'json', '--max-turns', '1', '--print'];
  if (config.models[provider]) args.push('--model', config.models[provider]);
  if (provider === 'gpt') args.push('-');
  return { executable: config.paths[provider], args };
}

export async function runAccountRelay(config, { prompt, turns = 3, first = 'gpt', directory, context = [] },
  { runner = runProcess, signal, onUpdate = async () => {} } = {}) {
  if (typeof prompt !== 'string' || !prompt.trim() || prompt.length > 64000) throw new Error('요청을 1~64,000자로 입력하세요.');
  if (!Number.isInteger(turns) || turns < 2 || turns > 8) throw new Error('답변 횟수는 2~8이어야 합니다.');
  if (!['gpt', 'claude'].includes(first)) throw new Error('먼저 답할 모델은 gpt 또는 claude입니다.');
  const accounts = await checkAccounts(config, runner);
  for (const provider of ['gpt', 'claude']) if (!accounts[provider].ready) throw new Error(`${provider}: ${accounts[provider].reason}. Setup.cmd를 실행하세요.`);
  await mkdir(directory, { recursive: true });
  const workspace = await mkdtemp(join(tmpdir(), 'chessfight-account-'));
  const journal = { transport: 'account-cli', startedAt: new Date().toISOString(), prompt, context, turns, first, status: 'running', answers: [] };
  try {
    await onUpdate(journal);
    for (let index = 0; index < turns; index++) {
      if (signal?.aborted) throw new Error('사용자가 중단했습니다.');
      const provider = index % 2 === 0 ? first : first === 'gpt' ? 'claude' : 'gpt';
      const stem = `${String(index + 1).padStart(2, '0')}-${provider}`;
      const responsePath = join(workspace, `${stem}.response.txt`);
      const request = RULES + relayInput(prompt, journal.answers, index === turns - 1)
        + (context.length ? '\n사용자가 지정한 참고 파일:\n' + JSON.stringify(context) : '');
      await writeFile(join(directory, `${stem}.request.txt`), request, 'utf8');
      const command = invocation(provider, config, workspace, responsePath);
      const result = await runner(command.executable, command.args, { input: request, cwd: workspace, env: config.env, signal });
      if (result.code !== 0) throw new Error(`${provider} 실행이 끝나지 않았습니다(종료 ${result.code}). 로그인 상태와 계정 사용 한도를 확인하세요. 자동 재시도하지 않았습니다.`);
      let text;
      let model = config.models[provider] || `${provider === 'gpt' ? 'Codex' : 'Claude Code'} 기본 모델`;
      if (provider === 'gpt') text = await readFile(responsePath, 'utf8');
      else {
        let message;
        try { message = JSON.parse(result.stdout); } catch { throw new Error('Claude Code 응답 형식을 읽지 못했습니다.'); }
        if (message.is_error || message.subtype !== 'success') throw new Error('Claude Code가 정상 답변을 마치지 못했습니다. 로그인 상태와 계정 사용 한도를 확인하세요.');
        text = message.result;
        const actualModels = Object.keys(message.modelUsage || {});
        if (actualModels.length) model = actualModels.join(', ');
      }
      if (typeof text !== 'string' || !text.trim()) throw new Error(`${provider}의 답변이 비어 있어 전달을 중단했습니다.`);
      await writeFile(join(directory, `${stem}.response.md`), text.trim(), 'utf8');
      journal.answers.push({ provider, model, text: text.trim() });
      await onUpdate(journal);
    }
    journal.status = 'completed';
    await writeFile(join(directory, 'final.md'), journal.answers.at(-1).text, 'utf8');
    return journal;
  } catch (error) {
    journal.status = 'failed';
    journal.error = error.message;
    throw error;
  } finally {
    await onUpdate(journal);
    // The only recursive cleanup target is the unique temporary directory created above.
    if (dirname(workspace) === resolve(tmpdir()) && basename(workspace).startsWith('chessfight-account-')) await rm(workspace, { recursive: true, force: true });
  }
}

async function main() {
  const { values } = parseArgs({ options: {
    prompt: { type: 'string' }, first: { type: 'string' }, turns: { type: 'string' },
    'gpt-model': { type: 'string' }, 'claude-model': { type: 'string' },
    context: { type: 'string', multiple: true }, output: { type: 'string' },
    check: { type: 'boolean' }, json: { type: 'boolean' }, help: { type: 'boolean' },
  } });
  if (values.help) {
    console.log('GPT ↔ Claude 계정 로그인 연결 (API 키 불필요)\n'
      + 'node cli-bridge.mjs --prompt "함께 다룰 내용" [--first gpt|claude] [--turns 3]\n'
      + '  --context 파일: 참고 자료 첨부(최대 4개, 합계 128,000자)\n'
      + '  --output 결과폴더 --gpt-model ID --claude-model ID\n'
      + '  --check [--json]: 프로그램과 계정 로그인 상태 확인\n'
      + '옵션 없이 실행하면 요청을 입력받습니다.');
    return;
  }
  const config = await cliConfiguration();
  if (values['gpt-model']) config.models.gpt = values['gpt-model'];
  if (values['claude-model']) config.models.claude = values['claude-model'];
  if (values.check) {
    const state = await checkAccounts(config);
    console.log(values.json ? JSON.stringify(state) : `GPT(Codex): ${state.gpt.reason}\nClaude Code: ${state.claude.reason}`);
    process.exitCode = state.gpt.ready && state.claude.ready ? 0 : 1;
    return;
  }
  let prompt = values.prompt;
  let first = values.first || 'gpt';
  let turns = Number(values.turns ?? 3);
  if (prompt === undefined) {
    const reader = createInterface({ input: process.stdin, output: process.stdout });
    try {
      console.log('GPT ↔ Claude / 각 계정의 사용량 한도가 적용됩니다. API 키는 사용하지 않습니다.');
      prompt = await reader.question('함께 다룰 내용을 입력하세요: ');
      first = (await reader.question('먼저 답할 모델 [gpt/claude, 기본 gpt]: ')).trim() || first;
      turns = Number((await reader.question('총 답변 횟수 [2~8, 기본 3]: ')).trim() || turns);
    } finally { reader.close(); }
  }
  const files = values.context || [];
  if (files.length > 4) throw new Error('참고 파일은 최대 4개입니다.');
  const context = [];
  let contextLength = 0;
  for (const path of files) {
    const text = await readFile(resolve(path), 'utf8');
    contextLength += text.length;
    if (contextLength > 128000) throw new Error('참고 파일의 합계는 128,000자 이내여야 합니다.');
    context.push({ file: basename(path), text });
  }
  const directory = resolve(values.output || join(HERE, 'runs', `account-${new Date().toISOString().replace(/[:.]/g, '-')}`));
  const controller = new AbortController();
  const stop = () => controller.abort();
  process.once('SIGINT', stop);
  let shown = 0;
  console.log(`대화 기록: ${directory}\n첫 답변을 기다리고 있습니다…`);
  try {
    await runAccountRelay(config, { prompt, first, turns, directory, context }, {
      signal: controller.signal,
      onUpdate: async journal => {
        await saveJournal(join(directory, 'conversation.json'), journal);
        while (shown < journal.answers.length) {
          const answer = journal.answers[shown++];
          console.log(`\n[${shown}/${turns}] ${answer.provider === 'gpt' ? 'GPT(Codex)' : 'Claude Code'}\n${answer.text.replace(/[\x00-\x08\x0b-\x1f\x7f]/g, '')}`);
          if (shown < turns) console.log('\n다음 모델에 전달하고 있습니다…');
        }
      },
    });
    console.log(`\n완료했습니다. 최종 답변: ${join(directory, 'final.md')}`);
  } catch (error) {
    console.error(`${error.message}\n받은 답변이 있다면 대화 기록 폴더에 남아 있습니다.`);
    process.exitCode = 1;
  } finally { process.removeListener('SIGINT', stop); }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
