// Read-only history scan. Emit names/counts only; never emit a matched value.
const { execFileSync } = require('node:child_process');
const git = (args, input) => execFileSync('git', args, { encoding: 'utf8', input, maxBuffer: 64 * 1024 * 1024 });
const patterns = {
  providerKey: /\b(?:sk_live_|rk_live_|sk-proj-|sk-ant-)[A-Za-z0-9_-]{16,}/,
  jwt: /\beyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}/,
  mongoCredentials: /mongodb(?:\+srv)?:\/\/[^\s/:"<>]+:[^\s@"<>]{8,}@/,
  privateKey: /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/,
  literalSecret: /"(?:Secret|ApiKey|SecretAccessKey|AccessKeyId|ClientSecret|Password)"\s*:\s*"(?!(?:test|fixture|dummy|YOUR_|placeholder|\$|<|never-copy|development|x{16}|\s*"))[^"\r\n]{12,}"/i,
};
const rows = git(['rev-list', '--objects', '--all']).split('\n');
const counts = {}; const examples = []; let scanned = 0; const seen = new Set(); const candidates = [];
for (const row of rows) {
  const split = row.indexOf(' '); if (split < 0) continue;
  const sha = row.slice(0, split), path = row.slice(split + 1);
  if (!/\.(json|cs|ts|tsx|js|cjs|mjs|ps1|yml|yaml|env|config|md|txt)$/i.test(path) || seen.has(sha)) continue;
  seen.add(sha);
  candidates.push({ sha, path });
}
const info = git(['cat-file', '--batch-check'], candidates.map(c => c.sha).join('\n') + '\n').trim().split('\n');
const selected = candidates.filter((c, i) => { const fields = info[i].split(' '); return fields[1] === 'blob' && Number(fields[2]) <= 1024 * 1024; });
for (let offset = 0; offset < selected.length; offset += 50) {
  const chunk = selected.slice(offset, offset + 50);
  const output = execFileSync('git', ['cat-file', '--batch'], { input: chunk.map(c => c.sha).join('\n') + '\n', maxBuffer: 64 * 1024 * 1024 });
  let cursor = 0;
  for (const { sha, path } of chunk) {
  const newline = output.indexOf(10, cursor); const size = Number(output.subarray(cursor, newline).toString().split(' ')[2]);
  const body = output.subarray(newline + 1, newline + 1 + size).toString('utf8'); cursor = newline + 2 + size; scanned++;
  for (const [rule, pattern] of Object.entries(patterns)) if (pattern.test(body)) {
    counts[rule] = (counts[rule] || 0) + 1;
    if (examples.length < 30) examples.push({ path, blob: sha, rule, reviewRequired: true });
  }
  }
}
const trackedPrivate = git(['ls-files']).split('\n').filter(p => /(^|\/)(private|\.env)(\/|$)/.test(p));
console.log(JSON.stringify({ scope: 'reachable Git history, heuristic scan; values withheld', scannedBlobs: scanned, candidateCounts: counts, examples, trackedPrivatePaths: trackedPrivate, rotationsPerformed: 0 }, null, 2));
