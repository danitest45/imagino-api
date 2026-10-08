'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { scanText, safePath, git, readBlobs } = require('./secret-hygiene.cjs');

const workspace = path.resolve(process.argv[2] || '.');
const liveAuth = process.argv.includes('--live-auth');
const hostAt = process.argv.indexOf('--staging-mongo-host');
const stagingMongoHost = hostAt >= 0 ? process.argv[hostAt + 1]?.toLowerCase() : undefined;
if (stagingMongoHost && !/^[a-z0-9.-]+\.mongodb\.net$/.test(stagingMongoHost)) throw new Error('Invalid staging host metadata.');
const runKey = crypto.randomBytes(32); // Never persisted or emitted; prevents reusable fingerprints.
const legacy = { source: 'Operator confirmation in B3 conversation; metadata only, not a credential-validity proof', renderWorkspaceId: 'tea-d2jkdnp5pdvs73fbdmi0', renderServiceId: 'srv-d2jl7uggjchc73cnqidg', renderServiceName: 'imagino-api', branch: 'master', atlasProjectId: '6873143ddf968e110c0f3e92', atlasProjectName: 'Imagino.ai', atlasClusterId: '6873179d7f1bcf36a437337a', atlasClusterName: 'imagino-cluster', providerAccountProjectStatus: 'UNKNOWN for RunPod, Replicate and Stripe', newStagingConsumption: 'NOT_ASSUMED' };
const identity = (f) => {
  let value = f.value;
  if (f.type === 'MongoDB credential') { const m = /^mongodb(?:\+srv)?:\/\/([^/@:]+):([^/@]+)@([^/?]+)/i.exec(value); if (m) value = m[1] + ':' + m[2] + '@' + m[3].toLowerCase(); }
  return crypto.createHmac('sha256', runKey).update(f.system + '\0' + f.type + '\0' + value).digest('hex');
};
function audit(cwd, repo) {
  const raw = git(['-c', 'core.quotepath=false', 'log', '--all', '-m', '--raw', '--no-abbrev', '--no-renames', '--format=@%H'], cwd).toString('utf8');
  const origins = new Map(); let commit;
  for (const row of raw.split('\n')) {
    if (/^@[a-f0-9]{40}$/.test(row)) { commit = row.slice(1); continue; }
    const m = /^:\d+ \d+ [a-f0-9]{40} ([a-f0-9]{40}) [A-Z]\d*\t(.+)$/.exec(row);
    if (!m || /^0+$/.test(m[1])) continue;
    if (!origins.has(m[1])) origins.set(m[1], new Map());
    origins.get(m[1]).set(m[2], { path: safePath(m[2]), commit });
  }
  const objects = git(['rev-list', '--objects', '--all'], cwd).toString('utf8').split('\n').filter(Boolean);
  const objectIds = objects.map(x => x.split(' ')[0]);
  const sizes = git(['cat-file', '--batch-check=%(objectname) %(objecttype) %(objectsize)'], cwd, objectIds.join('\n') + '\n').toString('ascii').trim().split('\n');
  const ids = []; let oversized = 0;
  for (const row of sizes) { const [id, type, size] = row.split(' '); if (type === 'blob') { if (Number(size) > 16 * 1024 * 1024) oversized++; else ids.push(id); } }
  const groups = new Map(); let textBlobs = 0, binaryBlobs = 0;
  readBlobs(ids, cwd, (id, bytes) => {
    if (bytes.includes(0)) { binaryBlobs++; return; }
    textBlobs++;
    const places = [...(origins.get(id)?.values() || [])];
    if (!places.length) return;
    for (const place of places) for (const f of scanText(bytes.toString('utf8'), place.path)) {
      const key = identity(f);
      if (!groups.has(key)) groups.set(key, { ...f, occurrences: [], currentStatus: 'UNKNOWN_NOT_TESTED', verified: false });
      const group = groups.get(key);
      if (f.classification === 'POSSIBLY_REAL') { group.classification = f.classification; group.reason = f.reason; }
      const occurrence = { path: place.path, line: f.line, commit: place.commit, blob: id };
      if (!group.occurrences.some(x => x.blob === id && x.path === occurrence.path && x.line === occurrence.line)) group.occurrences.push(occurrence);
    }
  });
  const refs = git(['for-each-ref', '--format=%(refname) %(objectname)', 'refs/heads', 'refs/remotes', 'refs/tags'], cwd).toString('utf8').trim().split('\n').map(x => { const [ref, revision] = x.split(' '); return { ref: safePath(ref), revision }; });
  // Current checked-in settings are not proof of Render runtime overrides.
  for (const file of ['appsettings.json', 'appsettings.Development.json', 'appsettings.AIStaging.json', 'appsettings.example.json', '.env.example']) {
    const target = path.join(cwd, file); if (!fs.existsSync(target)) continue;
    for (const f of scanText(fs.readFileSync(target, 'utf8'), file)) {
      const group = groups.get(identity(f));
      if (group) { group.currentLocalPaths ??= []; group.currentLocalPaths.push(file); }
    }
  }
  if (stagingMongoHost) for (const group of groups.values()) if (group.type === 'MongoDB credential' && !['TEST_ONLY', 'PLACEHOLDER'].includes(group.classification)) {
    const oldHost = /^mongodb(?:\+srv)?:\/\/[^/@]+@([^/?]+)/i.exec(group.value)?.[1]?.toLowerCase();
    group.currentStatus = oldHost === stagingMongoHost ? 'HOST_MATCHES_AUTHORIZED_STAGING_PASSWORD_NOT_VERIFIED' : 'HOST_DIFFERS_FROM_AUTHORIZED_STAGING_OLD_VALIDITY_UNKNOWN';
  }
  return { cwd, repo, head: git(['rev-parse', 'HEAD'], cwd).toString('ascii').trim(), refs, groups: [...groups.values()], coverage: { reachableObjects: objects.length, scannedTextBlobs: textBlobs, binaryBlobsExcluded: binaryBlobs, oversizedBlobsExcluded: oversized } };
}
async function apiCall(route, method = 'GET', body, token, cookie) {
  const headers = { Origin: 'https://imagino-front-git-feat-imagino-laun-348f6b-danitest45s-projects.vercel.app' };
  if (body) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = 'Bearer ' + token;
  if (cookie) headers.Cookie = cookie;
  const response = await fetch('https://imagino-api-ai-staging.onrender.com' + route, { method, headers, body: body ? JSON.stringify(body) : undefined, redirect: 'error', signal: AbortSignal.timeout(45000) });
  let data = null;
  if (response.headers.get('content-type')?.includes('application/json')) data = await response.json();
  return { status: response.status, data, cookies: response.headers.getSetCookie().map(x => x.split(';')[0]).join('; ') };
}
async function verifySigningSecrets(groups) {
  const credentials = JSON.parse(fs.readFileSync(path.join(workspace, 'private/generation-v2-credentials.json'), 'utf8'));
  const owner = credentials.accounts.find(x => (x.Email || x.email) === 'gen-v2-owner-20261002@example.invalid');
  if (!owner) throw new Error('Synthetic owner fixture unavailable.');
  let session;
  try {
    session = await apiCall('/api/auth/login', 'POST', { email: owner.Email || owner.email, password: credentials.password });
    if (session.status !== 200 || !session.data?.token) throw new Error('Staging login failed.');
    const me = await apiCall('/api/users/me', 'GET', undefined, session.data.token);
    if (me.status !== 200 || me.data?.id !== '6ac038cb05509cea703277eb') throw new Error('Synthetic owner binding failed.');
    const proof = await apiCall('/api/generation/runway/e2e-video/proof', 'GET', undefined, session.data.token);
    if (proof.status !== 200 || proof.data?.paidGenerationEnabled !== false || proof.data?.runwayRealSmokeEnabled !== false) throw new Error('Disabled paid flags not proven.');
    const parts = session.data.token.split('.');
    if (parts.length !== 3) throw new Error('Invalid issued token shape.');
    const header = JSON.parse(Buffer.from(parts[0], 'base64url'));
    const algorithms = { HS256: 'sha256', HS384: 'sha384', HS512: 'sha512' };
    if (!algorithms[header.alg]) return { login: 200, owner: 200, signatureComparison: 'UNSUPPORTED_ALGORITHM', paidGenerationEnabled: false, providerPosts: 0 };
    const signature = Buffer.from(parts[2], 'base64url'); let matched = 0;
    for (const group of groups.filter(x => x.system === 'JWT' && x.type === 'Signing secret')) {
      const computed = crypto.createHmac(algorithms[header.alg], Buffer.from(group.value, 'utf8')).update(parts[0] + '.' + parts[1]).digest();
      const same = signature.length === computed.length && crypto.timingSafeEqual(signature, computed);
      group.currentStatus = same ? 'LIVE_STAGING_SIGNER_MATCH' : 'NOT_CURRENT_STAGING_SIGNER_REVOCATION_ELSEWHERE_UNKNOWN';
      group.verified = true;
      if (same) { group.classification = 'CONFIRMED_REAL'; group.reason = 'VALIDATES_SERVER_ISSUED_STAGING_TOKEN'; matched++; }
    }
    return { atUtc: new Date().toISOString(), serviceId: 'srv-db1tmv17lnhs73efdjp0', login: 200, owner: 200, signatureComparison: 'SERVER_ISSUED_TOKEN_ONLY_NO_FORGING', matchedHistoricalSigningCredentials: matched, paidGenerationEnabled: false, runwayRealSmokeEnabled: false, providerPosts: 0, jobCreates: 0, emailsSent: 0, configurationWrites: 0 };
  } finally { if (session?.cookies) { const logout = await apiCall('/api/auth/logout', 'POST', undefined, undefined, session.cookies); if (logout.status !== 200) throw new Error('Audit session cleanup failed.'); } }
}
async function main() {
  const repos = [audit(path.join(workspace, 'work/imagino-api-launch-readiness'), 'danitest45/imagino-api'), audit(path.join(workspace, 'work/imagino-front-launch-readiness'), 'danitest45/imagino-front')];
  const live = liveAuth ? await verifySigningSecrets(repos.flatMap(x => x.groups)) : { signatureComparison: 'NOT_REQUESTED' };
  let nextId = 0;
  const results = repos.map(repo => ({ repository: repo.repo, visibility: 'public (verified through GitHub metadata)', auditedHead: repo.head, refs: repo.refs, coverage: repo.coverage, candidates: repo.groups.map(group => {
    const exempt = ['TEST_ONLY', 'PLACEHOLDER'].includes(group.classification);
    const active = group.currentStatus === 'LIVE_STAGING_SIGNER_MATCH';
    const environment = active ? 'AI staging (verified); other environments unknown' : exempt ? 'test/example source; no real secret established' : group.system === 'MongoDB' ? 'Probable legacy Atlas Imagino.ai / imagino-cluster (operator association); exact credential consumer unknown' : group.system === 'JWT' ? 'Legacy backend/app; actual current legacy signer unknown; AI staging signer differs' : 'Historically associated with legacy imagino-api; provider account/project/status unknown';
    return { id: 'B3-' + String(++nextId).padStart(3, '0'), system: group.system, credentialType: group.type, classification: group.classification, classificationReason: group.reason, literalPresentInHistory: true, historicallyExposed: !exempt, environment, currentStatus: exempt ? 'NON_REAL_FIXTURE_OR_PLACEHOLDER' : group.currentStatus, currentLocalPaths: group.currentLocalPaths || [], rotationRequired: exempt ? 'NO' : active ? 'YES_APPROVAL_REQUIRED' : 'REVIEW_AND_ROTATE_IF_REAL', priority: exempt ? 'NOT_REQUIRED' : active ? 'P0' : 'P1', verified: group.verified || exempt, humanAction: exempt ? 'None; preserve synthetic fixture' : active ? 'Approve staging replacement, login/refresh/logout smoke and old signer retirement; confirm every consumer first' : 'Privately verify legacy provider/account/environment, consumers and revocation; await separate explicit approval; do not infer new staging use', occurrences: group.occurrences };
  }) }));
  const evidence = { atUtc: new Date().toISOString(), gate: 'B3', status: results.some(x => x.candidates.some(c => c.priority === 'P0' || c.priority === 'P1')) ? 'BLOCKED' : 'PASS', repositories: results, operatorConfirmedLegacy: legacy, liveStagingAuth: live, fingerprints: 'Ephemeral keyed HMAC in memory only; no digest or credential persisted', rotationsPerformed: 0, revocationsPerformed: 0, historyRewrites: 0, limitations: ['Only fetched reachable branches/tags/PR heads/local refs; deleted unreachable commits, external forks and historical visibility changes are not proved.', 'Binary assets receive no OCR; text blobs larger than 16 MiB are excluded and counted.', 'Current Render secret values cannot be retrieved through a value-free connector; production configuration is outside scope.', 'A signer mismatch proves only non-use for this staging token; it does not prove revocation elsewhere.', 'Operator legacy-environment association does not prove any particular historical credential is genuine, active, revoked or used by a new staging service.'] };
  // Serialize only the explicit metadata schema above. No value/fingerprint field exists.
  const serialized = JSON.stringify(evidence, null, 2) + '\n';
  for (const repo of repos) for (const group of repo.groups) if (group.value.length >= 12 && serialized.includes(group.value)) throw new Error('Output redaction guard failed.');
  fs.writeFileSync(path.join(repos[0].cwd, 'docs/evidence/launch-readiness/secret-history-audit-b3.json'), serialized);
  fs.writeFileSync(path.join(repos[1].cwd, 'docs/evidence/launch-readiness/secret-history-audit-b3.json'), serialized);
  console.log(JSON.stringify({ gate: 'B3', status: evidence.status, coverage: results.map(x => ({ repository: x.repository, ...x.coverage, candidates: x.candidates.length })), classifications: results.flatMap(x => x.candidates).reduce((out, c) => { out[c.classification] = (out[c.classification] || 0) + 1; return out; }, {}), liveStagingAuth: live, rotationsPerformed: 0, revocationsPerformed: 0 }, null, 2));
}
main().catch(() => { console.error('B3 history audit failed; credentials, token, response bodies and exception details withheld.'); process.exitCode = 2; });
