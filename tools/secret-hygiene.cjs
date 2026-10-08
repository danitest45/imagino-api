'use strict';
// Values stay in process memory. Neither diagnostics nor errors contain matches.
const fs = require('node:fs');
const cp = require('node:child_process');
const path = require('node:path');

function git(args, cwd, input) {
  try { return cp.execFileSync('git', args, { cwd, input, stdio: ['pipe', 'pipe', 'pipe'], maxBuffer: 96 * 1024 * 1024 }); }
  catch { throw new Error('Git operation failed; captured output withheld.'); }
}
function systemFor(key, context) {
  const patterns = [[/jwt|symmetricsecurity/, 'JWT'], [/mongo/, 'MongoDB'], [/stripe/, 'Stripe'], [/google|gocspx/, 'Google OAuth'], [/replicate/, 'Replicate'], [/runpod|run.pod/, 'RunPod'], [/bfl|blackforest/, 'BFL'], [/openai/, 'OpenAI'], [/runway/, 'Runway'], [/resend/, 'Resend'], [/\br2\b|r2settings|cloudflare/, 'Cloudflare/R2'], [/render/, 'Render'], [/vercel/, 'Vercel'], [/gemini/, 'Google Gemini']];
  for (const s of [key.toLowerCase(), context.toLowerCase()]) for (const [rx, system] of patterns) if (rx.test(s)) return system;
  if (/signing/i.test(key)) return 'JWT';
  return 'Other';
}
function placeholder(value) {
  return !value || /^\s*(?:<[^>]+>|\$\{[^}]+\}|\{\{[^}]+\}\}|\*+|REDACTED|REPLACE(?:_|-)|CHANGE(?:_|-)?ME|YOUR(?:_|-)|INSERT(?:_|-)|TODO\b|example\b|placeholder\b|dummy\b|fake\b|test(?:_|-)?(?:key|secret|token|password)?$|not-a-real\b|secret$|password$)/i.test(value) || /^(?:x{8,}|0{16,})$/i.test(value) || (/^[A-Z][A-Z0-9_]+$/.test(value) && /(?:CLIENT_SECRET|API_KEY|SECRET_KEY|WEBHOOK_SECRET)/.test(value)) || /^(?:Stripe|Google|OpenAi|Runway|Bfl|Replicate|RunPod)(?:ApiKey|SecretKey|ClientSecret)$/.test(value) || /^(?:sk_(?:test|live)_|whsec_)(?:x+|0+|YOUR[_A-Z]*|your[_a-z]*)$/.test(value);
}
function isFixture(file) { return /(?:^|\/)(?:tests?|__tests__|e2e|fixtures?)(?:\/|\.)|Tests\.cs$/i.test(file); }
function machineCredential(value) {
  const suffix = value.replace(/^(?:sk[_-](?:proj-|live_|test_)?|rk_|whsec_|r8_|GOCSPX-|rnd_|rp[as]_|bfl_|re_)/, '');
  const frequencies = new Map(); for (const ch of suffix) frequencies.set(ch, (frequencies.get(ch) || 0) + 1);
  let entropy = 0; for (const count of frequencies.values()) { const p = count / suffix.length; entropy -= p * Math.log2(p); }
  return /^[a-f0-9]{32,}$/i.test(suffix) || /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(suffix) || (suffix.length >= 24 && entropy >= 3.5 && /[A-Za-z0-9]{20}/.test(suffix)) || (entropy >= 4 && /^[A-Za-z0-9+/_=-]{32,}$/.test(suffix));
}
function classify(value, type, file, context = '') {
  if (placeholder(value)) return { classification: 'PLACEHOLDER', reason: 'EXPLICIT_PLACEHOLDER' };
  if (type === 'MongoDB credential' && /(?:localhost|127\.0\.0\.1|\.invalid|\.example)(?::|\/|\?|$)/i.test(value)) return { classification: 'TEST_ONLY', reason: 'LOCAL_OR_RESERVED_HOST' };
  if (type === 'URL credential' && /^https?:\/\/[^@]+@(?:localhost|127\.0\.0\.1|[^/]*\.invalid|example\.(?:com|org|net))(?::|\/|\?|$)/i.test(value)) return { classification: 'TEST_ONLY', reason: 'LOCAL_OR_RESERVED_HOST' };
  const synthetic = /test|fake|dummy|mock|synthetic|example|placeholder|development|local|fixture|not-real/i.test(value);
  const machinePrefix = /^(?:sk[_-]|rk_|whsec_|r8_|GOCSPX-|rnd_|rp[as]_|bfl_|re_)/.test(value);
  const fakeContext = /@example\.(?:com|invalid|test)|@test\.com|FakeUser|InMemory|class Fake|class InMemory|mock\.method|route\.fulfill|page\.route|HttpMessageHandler|node:test|node:assert|using Xunit|\[Fact\]|\[Theory\]/.test(context);
  if (isFixture(file) && fakeContext && /^whsec_[A-Za-z0-9+/=_-]+$/.test(value)) {
    const decoded = Buffer.from(value.slice(6), 'base64').toString('utf8');
    if (/^[\x20-\x7e]+$/.test(decoded) && (/test|fake|dummy|fixture|synthetic|example|0123456789|1234567890|abcdefghijklmnopqrstuvwxyz/i.test(decoded) || placeholder(decoded))) return { classification: 'TEST_ONLY', reason: 'BASE64_ENCODED_SYNTHETIC_WEBHOOK_FIXTURE' };
  }
  const fixedSequence = /abcdefghijklmnopqrstuvwxyz|0123456789|1234567890|abcdef0123456789|0123456789abcdef/i.test(value);
  if (isFixture(file) && fakeContext && fixedSequence) return { classification: 'TEST_ONLY', reason: 'FIXED_ALPHABET_OR_NUMERIC_SEQUENCE_IN_ISOLATED_TEST' };
  const sentinel = /must[-_ ]?(?:not|never)|should[-_ ]?(?:not|never)|(?:never|not)[-_ ]?(?:leak|expose|log|return)|(?:server|provider|upstream)[-_ ]?(?:only|secret|private)|(?:configured|metadata|preflight)[-_ ]?(?:only|key|secret)/i.test(value);
  if (isFixture(file) && fakeContext && sentinel) return { classification: 'TEST_ONLY', reason: 'EXPLICIT_REDACTION_OR_PROVIDER_MOCK_SENTINEL' };
  if (isFixture(file) && fakeContext && !machineCredential(value)) return { classification: 'TEST_ONLY', reason: 'MOCK_TRANSPORT_OR_UNIT_ACCOUNT_NON_RANDOM_SENTINEL' };
  if (isFixture(file) && synthetic && !machinePrefix) return { classification: 'TEST_ONLY', reason: 'EXPLICIT_SYNTHETIC_FIXTURE' };
  // Existing mock fixtures use human-readable sentinels; random provider-shaped
  // strings remain findings even in test files. Paths alone are not exceptions.
  if (isFixture(file) && fakeContext && ['Password', 'Token'].includes(type) && !machinePrefix && !/^eyJ/.test(value)) return { classification: 'TEST_ONLY', reason: 'ISOLATED_SYNTHETIC_ACCOUNT_OR_MOCK_TOKEN' };
  if (value.length < 8) return { classification: 'UNKNOWN', reason: 'SHORT_LITERAL_REQUIRES_REVIEW' };
  return { classification: 'POSSIBLY_REAL', reason: 'CREDENTIAL_LITERAL_WITHOUT_REVOCATION_PROOF' };
}
function scanText(text, file) {
  const findings = [];
  const seen = new Set();
  const add = (system, type, value, offset, key = '') => {
    if (value === undefined || value === null) return;
    value = String(value).trim();
    const identity = system + '\0' + type + '\0' + value;
    if (seen.has(identity)) return;
    seen.add(identity);
    findings.push({ system, type, value, key, line: text.slice(0, Math.max(0, offset)).split('\n').length, ...classify(value, type, file, text) });
  };
  const patterns = [
    ['MongoDB', 'MongoDB credential', /mongodb(?:\+srv)?:\/\/[^\s"'<>`]+/g, v => /:\/\/[^/@:]+:[^/@]+@/.test(v)],
    ['Other', 'URL credential', /https?:\/\/[^\s/@:"'<>`]+:[^\s/@"'<>`]+@[^\s"'<>`]+/g],
    ['Google API', 'API key', /\bAIza[A-Za-z0-9_-]{35}\b/g],
    ['Stripe', 'API key', /\b(?:sk|rk)_(?:live|test)_[A-Za-z0-9]{16,}\b/g],
    ['Stripe', 'Webhook signing secret', /\bwhsec_[A-Za-z0-9]{16,}\b/g],
    ['Google OAuth', 'OAuth client secret', /\bGOCSPX-[A-Za-z0-9_-]{20,}\b/g],
    ['Replicate', 'API key', /\br8_[A-Za-z0-9]{20,}\b/g],
    ['RunPod', 'API key', /\brp[as]_[A-Za-z0-9_-]{20,}\b/g],
    ['OpenAI', 'API key', /\bsk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{24,}\b/g],
    ['Resend', 'API key', /\bre_[A-Za-z0-9_]{20,}\b/g],
    ['Render', 'API key', /\brnd_[A-Za-z0-9]{20,}\b/g],
    ['BFL', 'API key', /\bbfl_[A-Za-z0-9_-]{20,}\b/g],
    ['JWT', 'Bearer token', /\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]{16,}\b/g],
    ['Other', 'Private key', /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----[\s\S]+?-----END (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/g],
  ];
  for (const [system, type, rx, predicate] of patterns) for (const match of text.matchAll(rx)) if (!predicate || predicate(match[0])) {
    let identified = system;
    if (type === 'Webhook signing secret') {
      const nearby = text.slice(Math.max(0, match.index - 500), match.index).toLowerCase();
      identified = nearby.lastIndexOf('replicate') > nearby.lastIndexOf('stripe') ? 'Replicate' : nearby.includes('stripe') ? 'Stripe' : 'Webhook provider unknown';
    }
    add(identified, type, match[0], match.index);
  }
  const secretKey = /secret|password|passwd|pwd|api[-_]?key|access[-_]?key|signing[-_]?key|private[-_]?key|token$|(?:^|[_-])token(?:$|[_-])|connectionstring|mongoconnection/i;
  const typeFor = (system, key) => /mongo/i.test(key) || system === 'MongoDB' ? 'MongoDB credential' : /password|passwd|pwd/i.test(key) ? 'Password' : system === 'JWT' && /secret|key/i.test(key) ? 'Signing secret' : /clientsecret/i.test(key) ? 'OAuth client secret' : /webhook/i.test(key) ? 'Webhook signing secret' : /secretaccesskey/i.test(key) ? 'Storage secret key' : /accesskeyid/i.test(key) ? 'Storage access key ID' : /token/i.test(key) ? 'Token' : 'API key';
  const named = (key, value, offset, context) => {
    if ((!secretKey.test(key) && !/^authorization$/i.test(key)) || /passwordhash|sha256|publickey|quoteid|idempotency|tokenurl|tokenendpoint|tokenlifetimes|tokenexpiry|tokenminutes|tokenexpires|tokencookie|tokenpath|tokenname|tokenversion/i.test(key)) return;
    if (/clientid|secretref|secretname|secretpath|secrettype|tokenquery|secretvalues|valuewithheld|credentialtype/i.test(key)) return;
    if (/^(?:TOKEN_(?:INVALID|EXPIRED|CONSUMED)|WEAK_PASSWORD|new-password|current-password)$/.test(key)) return;
    if (findings.some(f => f.value === value && !f.key)) return;
    if (/^(?:https?:\/\/|\/api\/|Bearer\s*['"+]?\s*$|process\.env\.|Environment\.|\$env:)/.test(value)) return;
    const system = systemFor(key, context);
    add(system, /^authorization$/i.test(key) ? 'Bearer token' : /^whsec_/.test(value) ? 'Webhook signing secret' : typeFor(system, key), value, offset, key);
  };
  let parsedJson = false;
  try {
    const jsonc = text.replace(/("(?:\\.|[^"\\])*")|\/\/[^\r\n]*|\/\*[\s\S]*?\*\//g, (m, quoted) => quoted || m.replace(/[^\r\n]/g, ' '));
    const json = JSON.parse(jsonc);
    parsedJson = true;
    const walk = (node, parents = []) => {
      if (Array.isArray(node)) { node.forEach(x => walk(x, parents)); return; }
      if (!node || typeof node !== 'object') return;
      for (const [key, value] of Object.entries(node)) {
        if (typeof value === 'string') named(parents.concat(key).join('.'), value, text.indexOf(JSON.stringify(value)), parents.join('.'));
        else walk(value, parents.concat(key));
      }
    };
    walk(json);
  } catch { /* Non-JSON source uses the bounded assignment matcher below. */ }
  for (const m of text.matchAll(/(?:["']([A-Za-z][A-Za-z0-9_.:-]{0,100})["']|\b([A-Za-z][A-Za-z0-9_.:-]{0,100}))\s*[:=]\s*(["'])([^\r\n"']{0,600})\3/g)) {
    // JSON already provides parent context; do not assign a second, ambiguous class.
    if (parsedJson) continue;
    // A quoted ternary result followed by ':' is a label, not an object key.
    if (m[1] && text.slice(0, m.index).trimEnd().endsWith('?')) continue;
    named(m[1] || m[2], m[4], m.index, text.slice(Math.max(0, m.index - 240), m.index));
  }
  return findings;
}
function safeFinding(f, file) {
  return { system: f.system, credentialType: f.type, path: safePath(file), line: f.line, classification: f.classification, reason: f.reason };
}
function safePath(file) {
  if (/mongodb(?:\+srv)?:\/\/|eyJ[A-Za-z0-9_-]+\.|(?:sk[_-]|rnd_|r8_|GOCSPX-|re_)[A-Za-z0-9_-]{16,}/.test(file)) return '[PATH_WITHHELD]';
  return file;
}
function readBlobs(ids, cwd, consume) {
  for (let start = 0; start < ids.length; start += 32) {
    const data = git(['cat-file', '--batch'], cwd, ids.slice(start, start + 32).join('\n') + '\n');
    let at = 0;
    while (at < data.length) {
      const newline = data.indexOf(10, at);
      if (newline < 0) throw new Error('Invalid Git batch response.');
      const [id, type, sizeText] = data.subarray(at, newline).toString('ascii').split(' ');
      const size = Number(sizeText);
      if (type !== 'blob' || !Number.isSafeInteger(size)) throw new Error('Invalid Git blob metadata.');
      const bytes = data.subarray(newline + 1, newline + 1 + size);
      consume(id, bytes);
      at = newline + 1 + size + 1;
    }
  }
}
function checkIndex(cwd) {
  const entries = git(['ls-files', '--stage', '-z'], cwd).toString('utf8').split('\0').filter(Boolean).map(row => {
    const m = /^(\d+) ([a-f0-9]{40}) (\d)\t([\s\S]+)$/.exec(row);
    if (!m || m[3] !== '0') throw new Error('Unmerged Git index; secret check stopped.');
    return { id: m[2], file: m[4] };
  });
  const byId = new Map();
  for (const row of entries) { if (!byId.has(row.id)) byId.set(row.id, []); byId.get(row.id).push(row.file); }
  const blocked = []; let textBlobs = 0, binaryBlobs = 0;
  readBlobs([...byId.keys()], cwd, (id, bytes) => {
    if (bytes.includes(0)) { binaryBlobs++; return; }
    textBlobs++;
    for (const file of byId.get(id)) for (const f of scanText(bytes.toString('utf8'), file)) if (!['TEST_ONLY', 'PLACEHOLDER'].includes(f.classification)) blocked.push(safeFinding(f, file));
  });
  return { status: blocked.length ? 'BLOCKED' : 'PASS', scope: 'Entire staged index; textual blobs, no binary OCR', textBlobs, binaryBlobs, findings: blocked };
}
module.exports = { scanText, safeFinding, safePath, git, readBlobs, checkIndex };
if (require.main === module) {
  try { const result = checkIndex(path.resolve(process.argv[2] || '.')); console.log(JSON.stringify(result, null, 2)); process.exitCode = result.status === 'PASS' ? 0 : 1; }
  catch { console.error('Secret hygiene check failed; content and exception details withheld.'); process.exitCode = 2; }
}
