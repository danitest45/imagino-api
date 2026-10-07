# Creative Hub Remote Smoke = PASS

Completed remotely on 2026-10-07. Scope: frontend PR #89 stable branch Preview against **imagino-api-ai-staging** only. **Cost: US$0.** No generation, paid smoke, purchase, provider preflight or video submission was executed.

## Reviewed deployment

- Frontend PR #89: feat/imagino-creative-hub-core, commit deadd29c775564f0c78e99fcf36c6209dcf038c0.
- Stable Preview: https://imagino-front-git-feat-imagino-crea-9cdb36-danitest45s-projects.vercel.app
- READY Vercel deployment: dpl_dVb4GPKx7Zd756yqrzsGfnSZ4uYH.
- User-confirmed Render workspace: **Imagino-Staging**. Only service imagino-api-ai-staging (srv-db1tmv17lnhs73efdjp0) was changed/deployed. Free plan; auto-deploy off.
- Backend runtime: commit 4f634ce755b303b2d363ea460f3073eda7b2670b, deployment dep-db2ujlqjnfac7383nar0, Live at 2026-10-07T06:43:20.814477Z. Final health: **200**.

No frontend or production deployment occurred. The merge-only flag update caused a deploy of the prior backend docs HEAD, followed by the initial and final CORS fixes; all three deployments were on this same staging service. This evidence-only commit does not require another deployment.

## Exact CORS and closed spending gates

The new literal origin was appended to the two existing authorized origins, preserving both and the existing frontend base URL. AIStaging startup validation requires these three distinct strings. AIStaging uses ordinal literal matching; other profiles retain their previous matcher. No wildcard, broad regex or protection bypass was introduced.

Remote **12/12 CORS probes passed**: login, refresh, logout, users/me, catalog and jobs accept the exact new alias; both prior aliases remain allowed. External, unrelated Vercel, immutable deployment and trailing-slash Origins receive no CORS authorization or credential permission. Public catalog: **200**, exact CORS, no paid model ready.

Authenticated runtime proof returned **PaidGenerationEnabled=false**, **OpenAiSingleSmokeEnabled=false**, OpenAI ledger halted=true and slot Completed. The BFL paid authorization expired at 2026-10-07T00:00:00Z and remains closed together with the global paid-generation flag. There is no separate BFL paid-smoke boolean in GenerationSettings. Existing provider registration/homologation flags and masked API keys were preserved; they are not represented here as observed false values. All BFL/OpenAI/Gemini image offers are approval_required; video offers are migration_required.

## Remote verification

| Check | Evidence |
| --- | --- |
| Login, users/me, refresh | 200; refresh rotates the Secure/HttpOnly/SameSite=None host-only cookie and retains the owner |
| Reload | Owner session retained in the human-authenticated Chrome |
| Image / Model Picker | Controls render; paid offers unavailable; Generate image disabled |
| Video | Model update required; Generate video disabled; no video work initiated |
| Assets / detail | 9 existing jobs; drawer opens; authenticated detail 200 |
| Owner download | 200; valid 1,426,902-byte PNG; private/no-store; native Chrome download event confirmed |
| Foreign access | Owner detail, download and operator proof 404; foreign history empty |
| Use as reference | Existing asset prepared as 1/4; Generate image remains disabled; no quote or submission |
| Appearance | Light, Dark and System selected and resolved correctly |
| Logout | 200; anonymous UI; refresh in the same Chrome session returns 401 |
| Balance/history | Owner credits 10→10; existing jobs 9→9 |

**20/20 API checks and 13/13 Chrome checks passed.** The API checks used independent ephemeral sessions for two existing synthetic accounts; credentials remained in memory. Chrome used a temporary context authenticated manually by the human. No browser cookies, storage state, credentials, OTPs or bypass tokens were copied or entered by the agent. Human gates were respected for Vercel SSO and Imagino login.

## Network and logs

After manual authentication, the Chrome audit allowed only the exact Preview and AIStaging hosts. It recorded **369 HTTP responses**, one native download event and zero page errors. All permitted responses belong to these two hosts. Three attempts to load Vercel's feedback script from vercel.live were aborted before transmission. No production, Stripe, BFL, OpenAI, Gemini or other generation-provider request occurred in the armed app audit; quote requests and job submissions were **zero**. SSO authentication traffic was separate from the armed app audit.

Render app-log review after deploy: 83 entries, no further pages, zero detected sensitive indicators, provider-submission messages or errors. Authenticated continuation from 2026-10-07T07:03:00Z through the final snapshot: zero entries, hasMore=false. Only counts were retained. Saved evidence excludes raw logs, headers, query strings, auth bodies, secrets, JWTs, cookies, private prompts, HAR, traces and signed media URLs.

## Code validation

- Build passed with zero errors and existing nullable warnings.
- Focused CORS/security suite: **79/79 passed**.
- Full suite: **328 passed, 4 failed**, zero skipped. An unmodified baseline rerun produced **308 passed and the same 4 failures**. Three historical BFL cost-validation cases and one mocked submission case depend on the expired finite authorization. No expiry or paid guard was relaxed. Remote smoke PASS does not imply a fully passing unit-test suite.

## Sanitized evidence

- [CORS, health and catalog](evidence/creative-hub-remote-smoke/cors-postdeploy.json)
- [API authentication and owner/foreign authorization](evidence/creative-hub-remote-smoke/auth-api.json)
- [Chrome UI checks](evidence/creative-hub-remote-smoke/chrome-ui-checks.json)
- [Aggregated browser network audit](evidence/creative-hub-remote-smoke/browser-network-summary.json)
- [Post-deploy log counts](evidence/creative-hub-remote-smoke/logs-summary.json)
- [Final authenticated log counts](evidence/creative-hub-remote-smoke/logs-final-summary.json)

The smoke is complete. Update PR #56/#89 with this evidence and stop; do not begin video work.
