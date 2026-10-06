# Working Studio Remote Auth Smoke

**Working Studio Remote Auth Smoke = PASS.**

Authenticated remote smoke completed on 06 October 2026, 02:17–02:25 BRT (05:17–05:25 UTC). This is a staging smoke for the approved Working Studio Preview; it does not declare production readiness.

## Exact artifacts and minimal change

- Backend tested/runtime SHA: `76aec87644f8a55532a0a1a36175b5792f23b565`, branch `codex/imagino-ai-revival-v2`, [draft PR #56](https://github.com/danitest45/imagino-api/pull/56).
- Render: `imagino-api-ai-staging`, service `srv-db1tmv17lnhs73efdjp0`, workspace `Imagino-Staging`. Manual deploy `dep-db25r4rbc2fs73fbucug`, Live since `2026-10-06T02:32:27.251982Z`; rechecked Live after smoke. Auto-deploy remains off.
- Frontend tested SHA: `46c64a646dd1504369ef917c8075ea80e45b432c`, branch `feat/imagino-working-studio`, [draft PR #88](https://github.com/danitest45/imagino-front/pull/88).
- Stable alias rechecked against READY Vercel deployment `dpl_H6v4cP2FZf1PZQnQ5NbQnbDFNV7e`, URL `imagino-front-9lzm1gki5-danitest45s-projects.vercel.app`.
- New exact authorized origin: https://imagino-front-git-feat-imagino-work-16a0f7-danitest45s-projects.vercel.app
- Preserved exact origin: https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app
- API: https://imagino-api-ai-staging.onrender.com

Only the AIStaging CORS configuration, its startup validation and corresponding tests changed in application source. Validation requires exactly the two origins above, using an ordinal, order-independent set comparison and a count check. Missing/duplicate/additional origins, wildcards, trailing slashes and a modified base URL are rejected. Other AIStaging checks remain intact.

`Frontend:BaseUrl` stays at the old AI Preview. Its email verification/reset and Google OAuth redirect uses do not prevent these password-authentication/read flows. No secrets or environment values were edited. Later commits only record documentation/evidence and were not redeployed.

## Backend and real CORS verification

| Check | Result |
| --- | --- |
| `dotnet build` | PASS: 0 errors; 33 existing nullable warnings |
| `dotnet test` | PASS: 243 passed, 0 failed, 0 skipped |
| `GET /health` | 200 after deploy and again after browser smoke |
| Direct staging catalog | 200 |
| Both exact Preview origins | 14/14 OPTIONS: 204, exact ACAO and credentials=true |
| Random external and unrelated vercel.app origins | 14/14 OPTIONS: 204 without ACAO or credentials permission |
| PaidGenerationEnabled | false before, during and after smoke; Render dashboard observed, not edited |

Real OPTIONS covered login, refresh, logout, users/me, catalog, owner history and the existing output's authenticated download. Negative origins were `https://external-random-remote-smoke.example` and `https://attacker.vercel.app`. No arbitrary origin reflection, wildcard, broad regex or bypass was introduced. [28 CORS results](evidence/working-studio-remote-auth/cors-postdeploy.json).

## Authenticated remote checks A–O

The user manually authorized Vercel SSO in a temporary installed Chrome 154.0.8037.98 session, then explicitly authorized logout/relogin and local network observation. Deployment Protection and access settings remained intact; no bypass link or cookie copying was used. Only two existing synthetic AI staging accounts were exercised; no account was created.

| Gate | Result |
| --- | --- |
| A. Email/password login | UI owner and foreign login: 200 |
| B. users/me | Authenticated browser GET: 200; expected synthetic identity confirmed |
| C. Refresh | 200 while authenticated |
| D. Reload session | Remained authenticated; owner balance 16 credits |
| E. Live catalog | UI route 200, source=live, revision 2026-10-02.2; 6 models |
| F. Owner Library | 200; same 8 existing jobs, 7 Completed and 1 Failed |
| G. Ownership | Foreign Library empty; owner's detail and download each 404 for foreign account |
| H. Job detail | Owner GET 200, correct existing ID; UI detail opened in Light/Dark |
| I. Existing output download | Browser download event and authenticated HTTP 200; valid PNG below |
| J. Use as reference | Download 200; one existing asset prepared with Studio Image; no generation |
| K. Blocked quote | Paid model unavailable, Create disabled; 0 quote POSTs |
| L. No submission | 0 Generation V2 POSTs; unchanged 8 owner job IDs and 16-credit balance |
| M. Logout | Owner, foreign and final owner logout: 200 |
| N. Refresh after logout | 401 after each logout |
| O. Reload after logout | Refresh 401; remained signed out; private assets cleared |

The existing frontend catalog route fetched the live AI staging catalog; this pass did not create a proxy. users/me and direct job-detail ownership checks supplemented UI flows using fetch inside the authorized Preview browser. Authentication tokens were used transiently inside browser memory for these requests; no tokens, credentials, cookies, headers or authentication payloads were exported or persisted in evidence.

Downloaded existing job `6ac4068a985c143f201d8f15`: `imagino-6ac4068a985c143f201d8f15.png`, 1,352,396 bytes, 1024×1024. SHA-256 `1180fb5b6efd81464a799a2e1fc687ab6a044b21cf0b50ea5d11f041bd36195d` matches the previously generated staging output. This incurred no new provider call or charge.

[Structured browser results](evidence/working-studio-remote-auth/browser-results.json).

## Theme and state smoke

System was the default with no stored preference. Manual Light/Dark worked; reload and owner/foreign login/logout retained Dark. Theme changes preserved the synthetic prompt, selected 16:9 form value and subsequently the prepared reference asset. Model/aspect-ratio selects and Library model/status filters opened and worked. Library and detail remained readable in both themes. No redesign or full visual-suite rerun occurred.

| Evidence | Light | Dark |
| --- | --- | --- |
| Prompt/form and blocked generation | [Create](evidence/working-studio-remote-auth/create-light.png) | [Create](evidence/working-studio-remote-auth/create-dark.png) |
| Existing owner jobs | [Library](evidence/working-studio-remote-auth/library-light.png) | [Library](evidence/working-studio-remote-auth/library-dark.png) |
| Existing job detail | [Detail](evidence/working-studio-remote-auth/detail-light.png) | [Detail](evidence/working-studio-remote-auth/detail-dark.png) |
| Prepared reference, Create disabled | [Reference](evidence/working-studio-remote-auth/reference-prepared-light.png) | [Reference](evidence/working-studio-remote-auth/reference-prepared-dark.png) |

[System default](evidence/working-studio-remote-auth/system-default.png) · [Foreign empty Library](evidence/working-studio-remote-auth/foreign-library-empty-dark.png) · [Final signed-out session](evidence/working-studio-remote-auth/final-signed-out-dark.png) · [Paid flag false](evidence/working-studio-remote-auth/paid-flag-false-final.png).

## Browser network and application logs

During the Imagino application smoke, every observed HTTP request used one of:

- `imagino-front-git-feat-imagino-work-16a0f7-danitest45s-projects.vercel.app`
- `imagino-api-ai-staging.onrender.com`
- `vercel.live` (existing Preview infrastructure)

No R2 browser request was needed; existing images used the application's approved delivery paths. No production API/bucket, Stripe, BFL, Gemini, OpenAI or other generation-provider host appeared. All API POSTs were authentication endpoints. No page error was observed. WebSocket observation during reference/logout/relogin recorded none. The initial human SSO phase was separated from app smoke and involved Vercel, Google/GitHub authentication and their assets/telemetry; it was not a generation-provider flow.

[Network summary](evidence/working-studio-remote-auth/browser-network-summary.json) aggregates host/method/path/status and counts. Local observation retained metadata only, excluding query strings, headers, credentials, cookies, request/response bodies, HAR and traces.

The observer retained 33 `net::ERR_ABORTED` events during navigation/reload, including requests whose response headers had already arrived. Required positive endpoint statuses and UI state assertions passed; these cancellation events remain visible in the summary rather than being discarded.

AI staging application logs were manually reviewed from `2026-10-06T02:31:47Z` through `2026-10-06T05:30:00Z`: 53 entries, hasMore=false. They contained deployment/lifecycle messages and existing DataProtection persistence/encryption startup warnings. No password, JWT, refresh token, complete cookie, Mongo URI, R2 credentials, BFL key or integral private prompt appeared. The authenticated smoke window itself emitted 0 application entries, hasMore=false. No provider execution was present. [Full runtime review](evidence/working-studio-remote-auth/logs-full-runtime-review.json) · [Smoke-window review](evidence/working-studio-remote-auth/logs-authenticated-smoke-review.json).

## Final state and boundary

**Cost for this pass: US$0. PaidGenerationEnabled=false.** No quote, job, credit reservation, provider call or new generation occurred. The owner retained 8 jobs and 16 credits. The temporary test session was logged out and Chrome closed after evidence collection; the user's original browser session was not logged out.

Both PRs remain draft and unmerged. No production, Stripe, secrets, provider configuration, public bucket policy or second-provider work was changed. Verification is limited to this live staging Preview and installed Chrome smoke; existing startup warnings remain outside this pass. Stop after recording this result.
