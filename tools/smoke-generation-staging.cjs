// Only synthetic jobs. Requires credentials JSON outside the repository.
// Usage: node tools/smoke-generation-staging.cjs <absolute-credentials-json> <absolute-report-json>
const fs = require('node:fs');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const base = 'https://imagino-api-ai-staging.onrender.com';
const report = { startedAt: new Date().toISOString(), api: base, providerCalls: 0, checks: [] };
const credentialPath = process.argv[2], reportPath = process.argv[3];
if (!credentialPath || !reportPath) throw new Error('Credentials and report paths required.');
const check = (name, detail) => { report.checks.push({ name, detail }); console.log(name); };
async function request(path, token, body, key) {
  const response = await fetch(base + path, { method: body ? 'POST' : 'GET', signal: AbortSignal.timeout(60000),
    headers: { ...(token ? { Authorization: 'Bearer ' + token } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}), ...(key ? { 'Idempotency-Key': key } : {}) },
    body: body ? JSON.stringify(body) : undefined });
  const data = await response.json().catch(() => null);
  return { status: response.status, data };
}
async function main() {
  const health = await request('/health'); check('health', health.status); assert.equal(health.status, 200);
  const catalog = await request('/api/generation/catalog'); check('catalog', catalog.status);
  if (catalog.status !== 200 || !catalog.data?.models) {
    report.outcome = 'blocked_before_generation'; report.reason = 'Generation V2 has not started on staging.';
    return;
  }
  const fixture = catalog.data.models.find(m => m.id === 'pipeline-demo-20261002');
  assert.equal(fixture?.availability, 'synthetic_demo', 'Only the explicitly synthetic fixture may run.');
  const credentials = JSON.parse(fs.readFileSync(credentialPath, 'utf8'));
  const tokens = [];
  for (const account of credentials.accounts) {
    const login = await request('/api/auth/login', null, { email: account.Email, password: credentials.password });
    assert.equal(login.status, 200); assert.equal(typeof login.data.token, 'string'); tokens.push(login.data.token);
  }
  const credits = async token => { const r = await request('/api/users/credits', token); assert.equal(r.status, 200); return r.data.credits; };
  check('synthetic_login', credentials.accounts.map(a => a.Email));
  const before = await credits(tokens[0]); assert.equal(await credits(tokens[2]), 0);
  report.balances = { ownerBefore: before, foreignBefore: await credits(tokens[1]), emptyBefore: 0 };
  const payload = outcome => ({ modelId: fixture.id, prompt: 'Synthetic staging pipeline validation', settings: { resolution: '1MP', outcome }, inputs: [] });
  const quoted = async (body, token = tokens[0]) => { const q = await request('/api/generation/quote', token, body); assert.equal(q.status, 200); assert.equal(q.data.credits, 1); check('fixture_quote', q.data); return { ...body, quoteId: q.data.quoteId }; };
  const paidModel = catalog.data.models.find(m => m.id === 'flux-fast-20261002');
  assert.equal(paidModel.availability, 'approval_required', 'Refuse to run if paid generation is enabled.');
  const paid = await request('/api/generation/jobs', tokens[0], { modelId: paidModel.id, prompt: 'Gate check, no provider dispatch', settings: {}, inputs: [] }, crypto.randomUUID());
  assert.equal(paid.status, 403); assert.equal(await credits(tokens[0]), before); check('paid_gate_before_debit', 403);
  const empty = await request('/api/generation/jobs', tokens[2], await quoted(payload('success'), tokens[2]), crypto.randomUUID());
  assert.equal(empty.status, 402); check('insufficient_credits', empty.status);
  const key = crypto.randomUUID(), body = await quoted(payload('success'));
  const creates = await Promise.all(Array.from({ length: 3 }, () => request('/api/generation/jobs', tokens[0], body, key)));
  creates.forEach(r => assert.equal(r.status, 202));
  assert.equal(new Set(creates.map(r => r.data.id)).size, 1); const id = creates[0].data.id;
  assert.equal(creates[0].data.creditState, 'Reserved');
  check('transactional_reservation', { job: creates[0].data, balance: await credits(tokens[0]) });
  check('concurrent_idempotency', id);
  const collision = await request('/api/generation/jobs', tokens[0], { ...body, prompt: 'Changed payload' }, key);
  assert.equal(collision.status, 409); check('key_collision', 409);
  const failed = await request('/api/generation/jobs', tokens[0], await quoted(payload('failure')), crypto.randomUUID()); assert.equal(failed.status, 202);
  assert.equal(failed.data.creditState, 'Reserved');
  report.jobs = { success: id, failed: failed.data.id };
  check('failure_reservation', { job: failed.data, balance: await credits(tokens[0]) });
  async function terminal(jobId) {
    for (let i = 0; i < 30; i++) {
      const result = await request('/api/generation/jobs/' + jobId, tokens[0]); assert.equal(result.status, 200);
      if (['Completed', 'Failed', 'Cancelled'].includes(result.data.status)) return result.data;
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
    throw new Error('Synthetic job did not complete within 60 seconds.');
  }
  const successJob = await terminal(id), failedJob = await terminal(failed.data.id);
  assert.equal(successJob.status, 'Completed'); assert.equal(successJob.creditState, 'Charged');
  assert.equal(failedJob.status, 'Failed'); assert.equal(failedJob.creditState, 'Refunded');
  assert.equal(await credits(tokens[0]), before - 1); check('charge_and_single_refund', { success: id, failed: failed.data.id, balance: before - 1 });
  check('terminal_jobs', { success: successJob, failure: failedJob });
  const history = await request('/api/generation/jobs', tokens[0]); assert.equal(history.status, 200);
  assert(history.data.some(j => j.id === id && j.status === 'Completed'));
  assert(history.data.some(j => j.id === failed.data.id && j.creditState === 'Refunded'));
  check('owner_history', history.data);
  for (const path of ['/api/generation/jobs/' + id, '/api/generation/jobs/' + id + '/download']) {
    const response = await fetch(base + path, { headers: { Authorization: 'Bearer ' + tokens[1] }, signal: AbortSignal.timeout(30000) });
    assert.equal(response.status, 404);
  }
  check('foreign_owner_hidden', 404);
  const anonymous = await fetch(base + '/api/generation/jobs/' + id + '/download', {signal: AbortSignal.timeout(30000)});
  assert.equal(anonymous.status, 401); check('download_requires_auth', 401);
  const download = await fetch(base + '/api/generation/jobs/' + id + '/download', { headers: { Authorization: 'Bearer ' + tokens[0] }, signal: AbortSignal.timeout(30000) });
  assert.equal(download.status, 200);
  const bytes = Buffer.from(await download.arrayBuffer());
  assert.deepEqual(bytes.subarray(0, 8), Buffer.from([137,80,78,71,13,10,26,10]));
  check('stored_png_download', { bytes: bytes.length, sha256: crypto.createHash('sha256').update(bytes).digest('hex') });
  fs.writeFileSync(require('node:path').join(require('node:path').dirname(reportPath), 'GENERATION_V2_SYNTHETIC_API.png'), bytes);
  const repeat = await request('/api/generation/jobs', tokens[0], body, key); assert.equal(repeat.data.id, id);
  assert.equal(await credits(tokens[0]), before - 1); check('terminal_replay_no_second_charge', id);
  report.balances.ownerAfter = await credits(tokens[0]); report.balances.foreignAfter = await credits(tokens[1]); report.balances.emptyAfter = await credits(tokens[2]);
  report.outcome = 'passed_synthetic_only';
}
main().catch(error => { report.outcome = 'failed'; report.reason = error.message; process.exitCode = 1; })
  .finally(() => { report.finishedAt = new Date().toISOString(); fs.writeFileSync(reportPath, JSON.stringify(report, null, 2)); console.log('Outcome: ' + report.outcome); });
