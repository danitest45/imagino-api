# Entrega e validação — Imagino AI Generation 2.0

Atualização 05/10/2026: **Generation V2 Core Staging = PASS** no serviço separado imagino-api-ai-staging. O registro abaixo preserva a entrega de 02/10; a homologação atual, saldos, deployments, jobs, journal, logs e limites estão em [GENERATION_V2_CORE_STAGING.md](GENERATION_V2_CORE_STAGING.md).

Data: 02/10/2026. Branch codex/imagino-ai-revival-v2 nos dois repositórios, criada a partir de fix/revival-operational-security. Nenhum merge. Não houve acesso ao banco/buckets de produção, alteração Stripe, mudança de planos, rotação de segredo ou chamada paga de IA.

## Código entregue

Backend: GenerationController, contratos e policy, agregado de catálogo/pricing, transações Mongo, worker com lease/deadline/backoff, binding nativo, adapters FLUX.2/Gemini Interactions/Veo, validação e storage R2, fixture PNG 1024px claramente sintético e testes. Registro em Program.cs opt-in; nenhuma dependência nova. Old controllers/resolvers/webhooks preservados.

Frontend: /create/image e /create/video, schema dinâmico, referências locais, regras combinatórias, cotação com invalidação/expiração, envio idempotente, histórico com polling sem sobreposição, reutilização, cancelamento, resultado/download e saldo atualizado. Navigation flag. Estado legado normalizado na leitura. Catálogo público com snapshot de prévia indisponível durante falha da API, sem habilitar créditos/criação. Guard do build restringe esta branch a Preview e recursos staging.

## Verificação local

- Backend: dotnet test Imagino.Api.Tests/Imagino.Api.Tests.csproj --no-restore — 201 aprovados, zero falhas/ignorados (164 anteriores + 37 novos).
- Frontend: npm test — 26 aprovados (18 anteriores + 8 novos); TypeScript sem erros.
- Build Next: passou com configuração staging; avisos existentes de img/variáveis em componentes legados. A primeira tentativa no sandbox falhou ao obter Google Fonts; repetir com rede autorizada passou.
- Novos testes cobrem capabilities, tipo inválido, resolução/duração/ref pricing, fingerprint com pricing, expiração, desabilitados, gate pago antes da reserva, saldo insuficiente, replay/conflito, timeout, POST incerto sem retry, GET com retry, storage antes de charge, binding regional/foreign IDs, BSON round-trip, isolamento staging, ownership e formatos wire dos três adapters.

Os testes de adapters usam handler HTTP em memória. Não validar isso como prova de resposta real dos providers. Os testes de repository/processor com mocks não provam transações concorrentes reais no Atlas. Duplicados de callbacks legados continuam cobertos pelos testes existentes; V2 não adicionou endpoint de callback.

## Staging e bloqueio constatado

Render imagino-api-staging foi apontado para a nova branch, autoDeploy permanece desligado. Flags Enabled/SeedStagingCatalog/StagingFixtureEnabled=true e PaidGenerationEnabled=false. Configurações foram mescladas, sem obter/substituir os demais segredos. A origem exata do novo Preview foi adicionada ao CORS.

Deploy dep-db03grad0e5s739vc0h0 compilou, mas ficou update_failed antes do startup com a mensagem preexistente: `Invalid configuration: Stripe:ApiKey must be a TEST key in staging`. A aplicação anterior foi mantida pelo Render. Nenhuma investigação/correção Stripe foi feita. Um update de CORS já em andamento gerou segunda tentativa; não insistir neste gate.

Consequência: endpoints v2 do Render ainda não estão disponíveis. A vertical slice existe em código, mas a conclusão ponta a ponta no staging não foi demonstrada. Não houve PUT R2 v2, geração real, benchmark de qualidade nem medida de latência de provider. Não afirmar staging operacional completo.

Atlas imagino_staging recebeu seis documentos generation_catalog_v2 por $setOnInsert, exportados do mesmo catálogo compilado. Repetição do seed: 6 matches, zero alterações/upserts. Migração restrita dos dois Veo para COMPATIBILITY/version .2: duas alterações na primeira execução, zero na repetição. Três contas sintéticas dedicadas foram criadas com saldos fixture 20/20/0, e senha aleatória guardada somente em private/generation-v2-credentials.json do workspace, fora dos repositórios. Não são créditos comerciais; nenhuma conta real teve saldo alterado.

Vercel Preview usa NEXT_PUBLIC_API_URL=https://imagino-api-staging.onrender.com, MEDIA_ALLOWED_HOSTS=host público R2 staging, NEXT_PUBLIC_GENERATION_V2_ENABLED=true, exclusivamente na nova branch. O primeiro build remoto foi recusado pelo guard antes das variáveis; o deploy após configuração é registrado no adendo de validação abaixo.

## Roteiro já preparado para homologar após liberar o gate

1. Redeployar o SHA aprovado no mesmo Render staging, sem alterar billing nesta tarefa. Confirmar health e catálogo live; manter PaidGenerationEnabled=false.
2. Login nas contas sintéticas. Quote nas ofertas vigentes; POST pago deve falhar antes do débito. Veo deve permanecer migration_required, e retired após a data de retirada. Pipeline Demo sucesso/falha com custo IA zero.
3. Mesma key/payload deve retornar mesmo job; mesma key/payload alterado deve gerar conflito; conta zero deve falhar sem job/provider.
4. Consultar carteira/jobs Atlas: sucesso Charged uma vez; falha/timeout/cancel Refunded uma vez. Repetir GET/liquidação; verificar concorrência, cancel/claim e reinício.
5. Confirmar PNG no R2, download do dono e 404 do usuário estrangeiro; nenhuma referência original ou URL assinada no response/log.
6. Desabilitar modelo/provider fixture de forma controlada, confirmar bloqueio e restaurar apenas flag original. Fazer prova no Preview e captura do resultado; só depois solicitar/usar orçamento de provider real.

Scripts de geração de fixture e catálogo não chamam IA. O smoke staging deve recusar ofertas fora de fixture automaticamente. Não tentar consertar configuração Stripe, usar chave live ou pular validação para alcançar a demo.

## Gates humanos e riscos remanescentes

O gate preexistente de startup precisa ser resolvido pelo responsável fora do escopo desta fase. Credenciais/acesso dos providers precisam confirmação antes de POST real. Autorizar o teste BFL descrito em AI_CREDIT_ECONOMICS.md não autoriza habilitação permanente, novo plano ou compra de saldo.

Antes de lançamento: teste Atlas de concorrência/restart, reconciliação de submissão incerta, medição de custos/qualidade, política de imagens públicas/retention, limites e fila de produção, orçamento por conta/modelo, revisão de termos. O escopo implementado é staging-only.

## Próximas cinco ações recomendadas

1. Resolver o gate operacional já conhecido na tarefa responsável e concluir homologação sintética no Render/R2.
2. Autorizar o teste limitado BFL, fornecer acesso se necessário e comparar saída/custo do pipeline real.
3. Fazer benchmark pequeno de edição/texto com Gemini, GPT Image 2.5, Ideogram 4.5 e FLUX.3 antes de fechar catálogo de lançamento.
4. Migrar vídeo para endpoint vigente: Omni 1.1 Flash ou Veo GA Cloud; comparar Ray/Kling/Seedance/Grok Lite sob orçamento próprio antes de fechar a oferta.
5. Definir preços efetivos/retention/limites a partir das medições e conduzir a fase de design de workspaces e biblioteca de assets.

## Adendo de validação

Preview de imagem: https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app/create/image . Vídeo na mesma origem em /create/video. A interface foi verificada no browser, com controles por modelo, referências locais e criação desabilitada visivelmente. Capturas no workspace documentam a interface; não representam jobs concluídos.

Smoke tools/smoke-generation-staging.cjs executado em 02/10/2026 às 23:20 UTC: /health 200, /api/generation/catalog 404, providerCalls=0, outcome=blocked_before_generation. O script parou no preflight, antes de login/reserva/job. Resultado em GENERATION_V2_STAGING_CHECKS.json no workspace. Os índices v2 estão implementados, mas sua criação no Atlas depende do worker iniciado; não foram homologados neste deploy.

Calendário Google final revelou retirada prevista dos Veo 3.1 Preview em 22/10. Código, catálogo Atlas e snapshot frontend foram corrigidos: COMPATIBILITY/migration_required; bloqueio antes de reserva/POST, inclusive para documento ACTIVE antigo; após retirada, quote recusada. Testes específicos passaram. As três ofertas de imagem usam endpoints distintos e permanecem candidatas de homologação. Vídeo não está pronto para lançamento.

O backend permanece no último deploy saudável anterior; o novo código não será redeployado nesta fase enquanto existir o gate preexistente. Nenhum gasto de provider foi executado.

Vercel final READY: dpl_7gLHTJTeML422Q2bAr3GexHcLEfu, SHA 39d32eadb1af7995677807608000f2507faf285c, target Preview (null), alias da branch acima. Também foi confirmado no navegador que 1080p/4s mostra erro de regra e 1080p/8s remove esse erro; os inputs de frames e áudio nativo aparecem. A última publicação compilou com a revisão .2 do snapshot. Capturas: GENERATION_V2_IMAGE_PREVIEW.jpg e GENERATION_V2_VIDEO_PREVIEW.jpg no workspace.

PRs em draft, base fix/revival-operational-security, anexados a esta conversa:
- [Backend #56](https://github.com/danitest45/imagino-api/pull/56): implementação ff2c14c; pesquisa/lifecycle/migração/smoke 4a99617; evidência final em commit documental seguinte.
- [Frontend #87](https://github.com/danitest45/imagino-front/pull/87): af22619, 54aeae1, 378f841, 39d32ea. Último commit corresponde ao Preview READY.

Os PRs não foram mergeados. Ambos deixam explícitas as verificações pendentes, sem declarar o pipeline staging concluído. Os sete documentos deste diretório também foram disponibilizados com os nomes pedidos na raiz do workspace para leitura/download.
