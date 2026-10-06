# Working Studio Remote Auth Smoke

Status: **PENDING — human Vercel SSO gate**. Backend/CORS preparation passed; remote authenticated frontend smoke has not started. This evidence does not declare production readiness.

Prepared on 05/10/2026 at 23:32 BRT (06/10/2026 02:32 UTC).

## Exact scope and deployed artifacts

- Backend branch/PR: `codex/imagino-ai-revival-v2`, draft PR #56.
- Tested and deployed backend SHA: `76aec87644f8a55532a0a1a36175b5792f23b565`.
- Render service: `imagino-api-ai-staging`, `srv-db1tmv17lnhs73efdjp0`, workspace `Imagino-Staging`.
- Manual deploy: `dep-db25r4rbc2fs73fbucug`, Live at `2026-10-06T02:32:27.251982Z`. Auto-deploy remains off.
- Frontend branch/PR: `feat/imagino-working-studio`, draft PR #88.
- Stable alias resolves to frontend SHA `46c64a646dd1504369ef917c8075ea80e45b432c`, deployment `dpl_H6v4cP2FZf1PZQnQ5NbQnbDFNV7e`, READY. This is the intended smoke target; browser smoke is still pending.
- New authorized origin: `https://imagino-front-git-feat-imagino-work-16a0f7-danitest45s-projects.vercel.app`.
- Preserved origin: `https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app`.

The AIStaging profile requires exactly these two origins, using an ordinal, order-independent set comparison plus a count check. Missing/duplicate/additional origins, wildcards and modified base URL are rejected. All other AIStaging checks remain intact.

`Frontend:BaseUrl` stays at the old AI Preview. Its uses are email verification/reset links and Google OAuth redirect; the requested password login/refresh/user/Generation V2 reads do not depend on it. No environment values or secrets were edited.

## Completed checks

| Check | Result |
| --- | --- |
| `dotnet build` | PASS, 0 errors; 33 existing nullable warnings |
| `dotnet test` | PASS, 243 passed, 0 failed, 0 skipped |
| `GET /health` after deploy | 200 |
| `GET /api/generation/catalog` after deploy | 200, live catalog |
| Both exact Preview origins | 14/14 OPTIONS: 204, exact `Access-Control-Allow-Origin`, `Access-Control-Allow-Credentials: true` |
| Arbitrary external origin and unrelated vercel.app origin | 14/14 OPTIONS: no `Access-Control-Allow-Origin` or credentials header |
| Paid generation configuration | Render dashboard flag observed `false`; not edited, same configuration used for manual deploy |
| Paid model availability | `flux-fast`, `flux-studio`, `gemini-edit`: `approval_required`; Veo variants: `migration_required` |
| New jobs/provider calls/cost in this pass | 0 / 0 / US$0 |

Real OPTIONS covered password login, refresh, logout, users/me, catalog, owner history and the authenticated download path of an existing staging output. OPTIONS does not itself prove successful authentication or download.

Post-deploy application log review covered the new deploy from `2026-10-06T02:31:47Z` through the postflight. The returned page had 23 entries and `hasMore=false`; no password, JWT, refresh token, complete cookie, Mongo URI, R2 credentials, BFL key or private prompt was present. Existing DataProtection persistence/encryption warnings appeared during startup; no new CORS/startup error appeared. Logs must be reviewed again after authenticated smoke.

## Human gate and remaining remote checks

The Preview remains protected by Vercel SSO. Deployment Protection, access settings and bypass links were not changed. The user must manually open/authenticate the protected Preview before continuation.

Still pending with the existing synthetic AI staging owner: password login; users/me; refresh; reload session persistence; live catalog in UI; owner Library and foreign-job exclusion; job detail; authenticated output download 200; Use as reference without generation; no quote while provider/spending is blocked; zero submissions; logout 200; post-logout refresh 401; reload stays logged out.

Theme/state smoke is also pending: default System without preference; manual Light/Dark and reload persistence; preference survives login/logout; theme changes preserve prompt/form; custom selects; readable Library/detail in both themes.

Browser network host summary is pending. Only the stable Preview, AI staging API, staging R2 and required Vercel/SSO infrastructure are permitted. No production API/bucket, Stripe or provider request is authorized.

No redesign, Stripe change, paid enablement, provider call, production change, merge or second-provider work is part of this pass. Stop after the requested smoke and its evidence update.
