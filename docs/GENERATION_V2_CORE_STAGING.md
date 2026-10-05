# Generation V2 Core Staging = PASS

Homologação sintética concluída em 05/10/2026. Pipeline Demo executado na API e na Preview reais; nenhum provider pago foi chamado. Esta entrega encerra o primeiro gate.

## Serviço e deployments

| Item | Evidência |
|---|---|
| Serviço novo | [imagino-api-ai-staging](https://dashboard.render.com/web/srv-db1tmv17lnhs73efdjp0) |
| ID | srv-db1tmv17lnhs73efdjp0 |
| API | https://imagino-api-ai-staging.onrender.com |
| Plano / região | Render Free, Virginia, 1 instância; auto-deploy Off |
| Branch | codex/imagino-ai-revival-v2 |
| Deploy final Live | dep-db1tuk6k1f9s73d4ipmg |
| SHA backend implantado | 789fb3f00fefbe5e9de9970f6d440ad9c7c3ab1b |
| Preview | https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app/create/image |
| Deploy frontend READY | dpl_DjL5dP24pco5VNgxCfomFYCMjYY7 |
| SHA frontend implantado | c57f923d533ce92b92525a2e9f963958dd0c8b0a |
| PRs mantidos, sem merge | API #56; frontend #87 |

O serviço anterior imagino-api-staging não foi atualizado: updatedAt permanece 2026-10-02T23:03:00.361809Z. Produção e Little Haven não foram alterados.

## Configuração conferida no Render

ASPNETCORE_ENVIRONMENT=AIStaging. Enabled=true, SeedStagingCatalog=true, StagingFixtureEnabled=true, PaidGenerationEnabled=false. StagingFixtureDelaySeconds=0 no estado final.

A allowlist tem somente 20 variáveis: perfil, Mongo staging, JWT staging, R2 staging, origem/base da Preview AI e flags Generation V2. Não há variável Stripe, BFL, Gemini, Google, Replicate, Veo ou Resend; nenhum secret file/environment group foi copiado.

O perfil descarta as fontes de configuração da aplicação principal e carrega appsettings.AIStaging.json. Validação rejeita configurações de integrações externas, chaves BFL/Gemini, geração paga, banco/buckets/origem incorretos. Somente o provider fixture é registrado. Controllers disponíveis: health, login/refresh/logout, leitura do usuário/créditos e Generation V2. Não há registro de Billing nem exposição de webhooks/geradores legados.

## Gates no ambiente real

| # | Gate | Resultado e prova |
|---|---|---|
| 1 | Health | PASS — GET /health 200, inclusive após restart e deploy final |
| 2 | Catálogo live | PASS — /api/generation/catalog 200; seis modelos; Pipeline Demo synthetic_demo; imagem paga approval_required |
| 3 | Login sintético | PASS — três contas via API; conta owner também na Preview; login final leva diretamente ao Image studio |
| 4 | Quote Pipeline Demo | PASS — 1 crédito; custo provider e Imagino US$0 |
| 5 | Reserva transacional | PASS — resposta Queued/Reserved; saldo 20→19; concorrência retorna o mesmo job |
| 6 | generation_jobs_v2 | PASS — cinco documentos sintéticos no Atlas, nenhum para contas foreign/empty |
| 7 | Worker claim | PASS — journal WorkerClaim e logs por job/instância |
| 8 | PNG sintético | PASS — PNG 1024×1024, 40.046 bytes, identificado como STAGING DEMO / SYNTHETIC IMAGE / NO AI COST |
| 9 | R2 staging | PASS — quatro outputs no bucket imagino-images-staging; download da API e objeto persistido têm SHA-256 igual |
| 10 | Settlement Charged | PASS — quatro jobs Completed/Charged; um evento CompletedCharged por job |
| 11 | Histórico | PASS — owner vê cinco jobs, incluindo Charged e Refunded; foreign vê lista vazia |
| 12 | Resultado frontend | PASS — criação pelo formulário, quote 1, saldo atualizado e quatro PNGs carregados com tamanho natural 1024×1024 |
| 13 | Download autenticado | PASS — owner recebe 200/PNG; request sem token recebe 401 |
| 14 | Outro usuário | PASS nos endpoints da API — GET job e download retornam 404 para foreign; histórico não contém jobs do owner |
| 15 | Replay mesma key | PASS — três criações concorrentes retornam o mesmo ID; replay terminal e após restart não debitam novamente |
| 16 | Key/payload diferente | PASS — 409 Conflict |
| 17 | Fixture failure | PASS — Queued/Reserved → Failed/Refunded; devolução exata de 1 crédito |
| 18 | Zero crédito | PASS — 402 antes de persistir job; conta permanece 0; Atlas confirma zero jobs |
| 19 | Restart/reclaim | PASS — nova instância retomou Processing com mesmo ProviderJobId, sem novo ProviderBound/debito; um Charged |
| 20 | Índices Atlas | PASS — _id_, owner_idempotency, worker_due, owner_history confirmados |

CORS adicional: OPTIONS da Preview responde 204 com a origem exata; origem externa não recebe Access-Control-Allow-Origin.

O gate 14 verifica autorização da API. O bucket de imagens staging mantém a política pública já existente: uma URL R2 conhecida pode ser aberta sem autenticação. Esta homologação não certifica privacidade dos objetos R2 nem prontidão de produção.

O botão Download do frontend foi acionado sem erro na interface. A captura do evento de download no navegador integrado expirou; a prova dos bytes salvos foi obtida pelo endpoint autenticado da API. Não se apresenta essa captura do navegador como teste aprovado.

## Saldos e jobs

| Conta sintética | Antes | Depois |
|---|---:|---:|
| gen-v2-owner-20261002@example.invalid | 20 | 16 |
| gen-v2-foreign-20261002@example.invalid | 20 | 20 |
| gen-v2-empty-20261002@example.invalid | 0 | 0 |

| Job | Origem | Estado final | Débito líquido |
|---|---|---|---:|
| 6ac3dbf5e3290e588a5a27e2 | Smoke API, concorrência/replay | Completed / Charged | 1 |
| 6ac3dbf7e3290e588a5a27e7 | Falha sintética | Failed / Refunded | 0 |
| 6ac3dd2ce3290e588a5a2828 | Criação pela Preview | Completed / Charged | 1 |
| 6ac3dd40e3290e588a5a2839 | Primeira preparação de restart | Completed / Charged | 1 |
| 6ac3de88b1595fac4888bd09 | Restart/reclaim efetivo | Completed / Charged | 1 |

A primeira preparação terminou antes de poder segurar o job; não houve alteração desse documento. Foi então adicionada latência opcional exclusivamente à fixture, permitindo o restart efetivo. A latência voltou a zero. Os quatro créditos consumidos são fixtures sintéticas; não representam cobrança comercial.

No smoke inicial: owner 20→19 com reserva do sucesso; 19→18 com reserva da falha; 18→19 após refund. UI, preparação e restart efetivo consumiram mais três créditos únicos: saldo final 16. Replay e conflito não mudaram o saldo.

## Restart e journal

Job 6ac3de88b1595fac4888bd09:

- 17:29:44.153Z — QueuedReserved.
- 17:29:45.277Z — WorkerClaim; 17:29:45.298Z — ProviderBound.
- 17:29:49.367Z — WorkerClaim Processing na instância qbn72, com fixture em espera.
- 17:30:14.753Z — nova instância 7qqd7 iniciada; 17:30:17.506Z — instância anterior encerrando.
- Expiração de lease antecipada com $currentDate somente no _id desse job, owner/fixture/status/binding também filtrados; matched=1, modified=1. Crédito, status e binding não foram alterados.
- 17:31:10.965Z — WorkerClaim na nova instância.
- 17:32:12.548Z — OutputStored e CompletedCharged.

ProviderJobId permaneceu fixture-6ac3de88b1595fac4888bd09. Journal contém um ProviderBound e um CompletedCharged. Saldo 17→16, replay mantém 16. A espera natural de cinco minutos da lease foi antecipada; o teste prova reclaim de lease expirada após restart.

Uma tentativa de preparação com filtro mais amplo foi rejeitada automaticamente e não executada. A alteração efetiva foi limitada pelo _id ao único job sintético. Nenhum bloqueio ficou pendente.

## R2 e custos

Bucket: imagino-images-staging. Prefixo: generation-v2/6ac038cb05509cea703277eb/. Quatro PNGs de 40.046 bytes: 160.184 bytes persistidos. Bucket de vídeos não recebeu output.

SHA-256 dos PNGs: e8e38b87eb75c0c0db67283e8a3de5ca70ec9b2a9cd8e15662420c0527d0ec28.

Custo de IA total nesta execução: **US$0**. Chamadas BFL/Gemini/Veo: **0**. Custos fixos incrementais contratados: **US$0**; Render Free, sem upgrade. R2 existente recebeu os objetos acima; eventual custo variável/fatura global não foi auditado. Não houve contratação paga, operação Stripe ou merge.

## Correções e validação

Foi criado o perfil de startup AIStaging independente, eliminando a dependência do startup principal para esse serviço. A base dos PRs foi preservada no caminho normal da aplicação. Foram adicionados journal transacional e logs de transição sem prompts/secrets.

Bug corrigido: login da Preview Generation V2 redirecionava para /images, que depende de controllers legados indisponíveis nesse perfil. Com a flag V2, passa a /create/image. O fluxo final foi repetido no navegador e confirmado.

A fixture ganhou latência opcional de 0–120 segundos e teste de cancelamento por shutdown; default/final 0. Backend: 212 testes aprovados, zero falhas/ignorados. Frontend: 26 testes aprovados; TypeScript sem erros; Preview READY.

## Evidências locais

No workspace, os arquivos estão na raiz. No repositório API, as cópias estão em docs/evidence/generation-v2. O commit final do frontend atualiza apenas a documentação; o código homologado é o mesmo do commit 0a68674.

- GENERATION_V2_AI_STAGING_CHECKS.json — smoke API inicial completo.
- GENERATION_V2_RECLAIM_PREPARATION.json — primeira preparação encerrada antes do restart.
- GENERATION_V2_RECLAIM_CHECKS.json — restart, settlement, saldo, replay e download.
- GENERATION_V2_FINAL_RUNTIME_CHECKS.json — estado final, CORS, ownership, replay e R2.
- GENERATION_V2_ATLAS_EVIDENCE.json — índices, jobs/journal, carteiras e zero jobs da conta empty.
- GENERATION_V2_RENDER_LOGS.json — logs correlacionados, inclusive troca de instância.
- GENERATION_V2_SERVICE_CONFIG_EVIDENCE.json — nomes das variáveis e flags públicas finais, sem secrets.
- GENERATION_V2_DEPLOYMENT_EVIDENCE.json — serviço/deploy/SHA e estado do serviço antigo.
- GENERATION_V2_CORE_STAGING_RENDER.png — captura do serviço separado Free/Live e SHA.
- GENERATION_V2_CORE_STAGING_FRONTEND.png — captura do resultado e histórico.
- GENERATION_V2_CORE_STAGING_FRONTEND_TOP.png — captura do formulário e saldo.
- GENERATION_V2_SYNTHETIC_API.png — PNG obtido com autenticação.

**Generation V2 Core Staging = PASS. Trabalho encerrado neste gate. Chaves BFL/Gemini continuam ausentes; geração paga continua desabilitada.**
