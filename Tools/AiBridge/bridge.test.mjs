import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { configuration, runRelay, callModel, saveJournal } from './bridge.mjs';

const config = () => configuration({ OPENAI_API_KEY: 'test-openai-secret', ANTHROPIC_API_KEY: 'test-anthropic-secret' });
const reply = (provider, text, complete = true) => provider === 'gpt'
  ? { status: complete ? 'completed' : 'incomplete', output: [
    { type: 'reasoning', summary: [] },
    { type: 'message', content: [{ type: 'output_text', text }] },
  ], usage: { input_tokens: 10, output_tokens: 20 } }
  : { stop_reason: complete ? 'end_turn' : 'max_tokens', content: [
    { type: 'thinking', thinking: 'private' }, { type: 'text', text },
  ], usage: { input_tokens: 30, output_tokens: 40 } };
const ok = data => ({ ok: true, json: async () => data });

test('GPT draft, Claude review, GPT final pass the original request and each preceding answer', async () => {
  const calls = [];
  const snapshots = [];
  const result = await runRelay(config(), { prompt: '체스파이트 구간 아이디어' }, {
    fetchImpl: async (url, options) => {
      calls.push({ url, ...options, body: JSON.parse(options.body) });
      return ok(reply(calls.length === 2 ? 'claude' : 'gpt', `답변 ${calls.length}`));
    },
    onUpdate: async state => snapshots.push(structuredClone(state)),
  });
  assert.deepEqual(result.answers.map(a => a.provider), ['gpt', 'claude', 'gpt']);
  assert.equal(result.status, 'completed');
  assert.equal(calls[0].url, 'https://api.openai.com/v1/responses');
  assert.equal(calls[1].url, 'https://api.anthropic.com/v1/messages');
  assert.equal(calls[0].headers.authorization, 'Bearer test-openai-secret');
  assert.equal(calls[1].headers.authorization, 'Bearer test-anthropic-secret');
  assert.equal(calls[1].headers['anthropic-version'], '2026-10-21');
  assert.equal(calls[0].body.store, false);
  assert.equal(calls[0].redirect, 'error');
  assert.equal(calls[0].body.max_output_tokens, 4096);
  assert.equal(calls[1].body.max_tokens, 4096);
  const review = JSON.parse(calls[1].body.messages[0].content);
  assert.equal(review.user_request, '체스파이트 구간 아이디어');
  assert.deepEqual(review.previous_answers, [{ model: 'GPT', answer: '답변 1' }]);
  const final = JSON.parse(calls[2].body.input);
  assert.equal(final.previous_answers.length, 2);
  assert.match(final.task, /최종 답변/);
  assert.equal(snapshots[0].answers.length, 0);
  assert.equal(snapshots.at(-1).status, 'completed');
  assert.doesNotMatch(JSON.stringify(result), /test-openai-secret|test-anthropic-secret|private/);
});

test('Claude can start; a two-answer run ends with GPT synthesis', async () => {
  let count = 0;
  const result = await runRelay(config(), { prompt: 'test', first: 'claude', turns: 2 }, {
    fetchImpl: async () => ok(reply(count++ === 0 ? 'claude' : 'gpt', 'answer')),
  });
  assert.deepEqual(result.answers.map(a => a.provider), ['claude', 'gpt']);
});

test('both credentials are required before making the first billable call', async () => {
  let calls = 0;
  await assert.rejects(runRelay(configuration({ OPENAI_API_KEY: 'test' }), { prompt: 'test' }, {
    fetchImpl: async () => { calls++; },
  }), /ANTHROPIC_API_KEY/);
  assert.equal(calls, 0);
});

test('invalid prompts, call counts and token limits cannot make requests', async () => {
  for (const options of [
    { prompt: '' }, { prompt: 'x'.repeat(64001) }, { prompt: 'test', turns: 9 },
    { prompt: 'test', turns: 1 }, { prompt: 'test', turns: 2.5 },
    { prompt: 'test', first: 'other' }, { prompt: 'test', maxTokens: 1 },
  ]) {
    let calls = 0;
    await assert.rejects(runRelay(config(), options, { fetchImpl: async () => { calls++; } }));
    assert.equal(calls, 0);
  }
});

test('a truncated answer is saved but never passed to the other model', async () => {
  for (const first of ['gpt', 'claude']) {
    let calls = 0;
    let latest;
    await assert.rejects(runRelay(config(), { prompt: 'test', first }, {
      fetchImpl: async () => { calls++; return ok(reply(first, 'partial answer', false)); },
      onUpdate: async state => { latest = structuredClone(state); },
    }), /부분 답변/);
    assert.equal(calls, 1);
    assert.equal(latest.status, 'failed');
    assert.equal(latest.answers[0].text, 'partial answer');
  }
});

test('provider errors preserve the first answer without exposing the error body or retrying', async () => {
  let calls = 0;
  let latest;
  await assert.rejects(runRelay(config(), { prompt: 'test' }, {
    fetchImpl: async () => ++calls === 1 ? ok(reply('gpt', 'saved draft')) : {
      ok: false, status: 401,
      json: async () => { throw new Error('test-anthropic-secret must never be read'); },
    },
    onUpdate: async state => { latest = structuredClone(state); },
  }), /HTTP 401/);
  assert.equal(calls, 2);
  assert.equal(latest.answers[0].text, 'saved draft');
  assert.equal(latest.status, 'failed');
  assert.doesNotMatch(latest.error, /test-anthropic-secret/);
});

test('network errors and malformed JSON never reveal the underlying exception', async () => {
  for (const fetchImpl of [
    async () => { throw new Error('test-openai-secret'); },
    async () => ({ ok: true, json: async () => { throw new Error('test-openai-secret'); } }),
  ]) {
    await assert.rejects(callModel('gpt', 'test', config(), { fetchImpl }), error => {
      assert.doesNotMatch(error.message, /test-openai-secret/);
      return true;
    });
  }
});

test('empty text and refusal-only responses are not relayed', async () => {
  await assert.rejects(callModel('gpt', 'test', config(), {
    fetchImpl: async () => ok({ status: 'completed', output: [
      { type: 'message', content: [{ type: 'refusal', refusal: 'no' }] },
    ] }),
  }), /텍스트/);
});

test('multiple GPT text messages are collected after reasoning blocks', async () => {
  const data = reply('gpt', 'first');
  data.output.push({ type: 'message', content: [{ type: 'output_text', text: 'second' }] });
  const result = await callModel('gpt', 'test', config(), { fetchImpl: async () => ok(data) });
  assert.equal(result.text, 'first\nsecond');
});

test('cancellation between answers saves progress and makes no further call', async () => {
  const controller = new AbortController();
  let calls = 0;
  let latest;
  await assert.rejects(runRelay(config(), { prompt: 'test' }, {
    signal: controller.signal,
    fetchImpl: async () => { calls++; return ok(reply('gpt', 'saved')); },
    onUpdate: async state => {
      latest = structuredClone(state);
      if (state.answers.length) controller.abort();
    },
  }), /중단/);
  assert.equal(calls, 1);
  assert.equal(latest.status, 'failed');
});

test('API timeout aborts an in-flight request without retrying', async () => {
  // Keep the mock request alive while the unref'ed timeout signal expires.
  const hold = setTimeout(() => {}, 1000);
  try {
    await assert.rejects(callModel('gpt', 'test', config(), {
      timeoutMs: 5,
      fetchImpl: async (_url, { signal }) => new Promise((_resolve, reject) => {
        signal.addEventListener('abort', () => reject(new Error('hidden')), { once: true });
      }),
    }), /대기 시간/);
  } finally { clearTimeout(hold); }
});

test('an unwritable transcript fails before billing', async () => {
  let calls = 0;
  await assert.rejects(runRelay(config(), { prompt: 'test' }, {
    fetchImpl: async () => { calls++; },
    onUpdate: async () => { throw new Error('disk unavailable'); },
  }), /disk unavailable/);
  assert.equal(calls, 0);
});

test('transcript files can be updated and retain Korean content', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'chessfight-bridge-'));
  try {
    const path = join(directory, 'runs', 'test.json');
    await saveJournal(path, { status: 'running', prompt: '초안' });
    await saveJournal(path, { status: 'completed', prompt: '최종' });
    assert.deepEqual(JSON.parse(await readFile(path, 'utf8')), { status: 'completed', prompt: '최종' });
  } finally { await rm(directory, { recursive: true, force: true }); }
});
