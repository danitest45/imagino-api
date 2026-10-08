'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { scanText, safeFinding, git, checkIndex } = require('./secret-hygiene.cjs');
const syntheticKey = () => 'sk-proj-' + crypto.randomBytes(32).toString('hex');
const blocking = found => found.some(x => ['POSSIBLY_REAL', 'CONFIRMED_REAL', 'UNKNOWN'].includes(x.classification));

test('provider-shaped literals block even inside an isolated test', () => {
  const value = syntheticKey();
  const found = scanText(JSON.stringify({ GenerationV2: { OpenAiApiKey: value } }), 'tests/provider.test.cjs');
  assert.ok(blocking(found), 'Provider credential shape must remain a finding.');
  assert.ok(!JSON.stringify(found.map(f => safeFinding(f, 'tests/security.cs'))).includes(value), 'Safe findings must withhold values.');
});
test('runtime signing literals remain findings without a provider prefix', () => {
  const value = crypto.randomBytes(32).toString('base64');
  const found = scanText(JSON.stringify({ Jwt: { Secret: value } }), 'appsettings.json');
  assert.ok(found.some(f => f.system === 'JWT' && f.type === 'Signing secret' && f.classification === 'POSSIBLY_REAL'), 'Runtime signer detection failed.');
});
test('Mongo credentials are detected without returning their URI', () => {
  const value = 'mongodb+srv://synthetic:' + crypto.randomBytes(16).toString('hex') + '@cluster.mongodb.net/db';
  const found = scanText(JSON.stringify({ ImageGeneratorSettings: { MongoConnection: value } }), 'appsettings.json');
  assert.ok(found.some(f => f.system === 'MongoDB' && f.classification === 'POSSIBLY_REAL'), 'Mongo credential detection failed.');
  assert.ok(!JSON.stringify(found.map(f => safeFinding(f, 'appsettings.json'))).includes(value), 'Mongo URI escaped redaction.');
});
test('JSON comments do not hide a runtime credential', () => {
  const value = syntheticKey();
  const found = scanText('// settings comment\n' + JSON.stringify({ OpenAiApiKey: value }), 'appsettings.json');
  assert.ok(blocking(found), 'Commented JSON must still be scanned.');
});
test('empty examples and environment-name placeholders require no rotation', () => {
  const found = scanText(JSON.stringify({ Jwt: { Secret: '' }, Google: { ClientSecret: 'GOOGLE_CLIENT_SECRET' } }), 'appsettings.example.json');
  assert.ok(found.length === 2 && found.every(f => f.classification === 'PLACEHOLDER'), 'Example classification failed.');
});
test('synthetic account and encoded mock webhook fixtures stay usable', () => {
  const value = 'whsec_' + Buffer.from('synthetic-webhook-fixture-only').toString('base64');
  const found = scanText(JSON.stringify({ Replicate: { WebhookSigningSecret: value } }) + '\n// using Xunit', 'tests/webhook.test.cjs');
  assert.ok(found.length > 0 && found.every(f => f.classification === 'TEST_ONLY'), 'Mock webhook fixture classification failed.');
});
test('random encoded webhook material is never exempted by a test path', () => {
  const value = 'whsec_' + crypto.randomBytes(32).toString('base64');
  const found = scanText(JSON.stringify({ Replicate: { WebhookSigningSecret: value } }) + '\n// using Xunit', 'tests/webhook.test.cjs');
  assert.ok(blocking(found), 'Random webhook credential shape was exempted.');
});
test('Stripe test-mode credentials are real credential candidates, not test fixtures', () => {
  const value = 'sk_test_' + crypto.randomBytes(24).toString('hex');
  assert.ok(blocking(scanText(JSON.stringify({ Stripe: { ApiKey: value } }), 'appsettings.json')), 'Test-mode provider keys still require review.');
});
test('Google API keys, opaque bearer headers and URL passwords are detected', () => {
  const google = 'AIza' + crypto.randomBytes(24).toString('base64url').padEnd(35, 'Z').slice(0,35);
  const bearer = 'Bearer ' + crypto.randomBytes(24).toString('hex');
  const url = 'https://synthetic:' + crypto.randomBytes(16).toString('hex') + '@example.invalid/path';
  assert.ok(blocking(scanText(JSON.stringify({ GoogleApiKey: google, Authorization: bearer, endpoint: url }), 'config.json')), 'Additional credential classes were missed.');
});
test('UI field names, error codes and secret references are not credentials', () => {
  const found = scanText('const fields = { TOKEN_EXPIRED: "expired", "new-password": "current-password", SecretRef: "ReplicateSettings.ApiKey" };', 'src/AuthFields.tsx');
  assert.ok(found.length === 0, 'Non-credential source symbols were detected.');
});
test('staged scan checks the index rather than a cleaned working file; diagnostics stay redacted', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'imagino-secret-hygiene-'));
  const value = syntheticKey();
  try {
    git(['init', '--quiet'], root);
    fs.writeFileSync(path.join(root, 'config.json'), JSON.stringify({ OpenAiApiKey: value }));
    git(['add', '--', 'config.json'], root);
    fs.writeFileSync(path.join(root, 'config.json'), '{}');
    const result = checkIndex(root);
    assert.ok(result.status === 'BLOCKED', 'Staged credential must block after working-tree cleanup.');
    assert.ok(!JSON.stringify(result).includes(value), 'Index scan emitted a credential.');
    git(['add', '--', 'config.json'], root);
    assert.ok(checkIndex(root).status === 'PASS', 'Clean index must pass.');
  } finally {
    const resolved = fs.realpathSync(root);
    assert.ok(path.dirname(resolved).toLowerCase() === fs.realpathSync(os.tmpdir()).toLowerCase() && path.basename(resolved).startsWith('imagino-secret-hygiene-'), 'Temporary cleanup target must stay in the intended temp directory.');
    fs.rmSync(resolved, { recursive: true, force: true });
  }
});

test('conditional status and type labels are not secret assignments', () => {
  const statuses = ['condition ?', JSON.stringify('HOST_MATCHES_AUTHORIZED_STAGING_PASSWORD_NOT_VERIFIED'), ':', JSON.stringify('HOST_DIFFERS_FROM_AUTHORIZED_STAGING_OLD_VALIDITY_UNKNOWN')].join(' ');
  const types = ['condition ?', JSON.stringify('Token'), ':', JSON.stringify('API key')].join(' ');
  assert.ok(scanText(statuses + '\n' + types, 'tools/status.cjs').length === 0, 'Conditional labels must not become credentials.');
});
