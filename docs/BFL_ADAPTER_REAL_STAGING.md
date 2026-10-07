# Homologação BFL real — AI staging — 05/10/2026

**BFL adapter real = PASS.** Exatamente três POSTs pagos, três jobs Completed/Charged, custo BFL observado **US$0,089**. Teto US$0,15; saldo de orçamento não usado US$0,061. Geração paga foi desativada e seu deploy está Live. Não executar novas chamadas.

## Serviço e deploy

- Serviço: [imagino-api-ai-staging](https://dashboard.render.com/web/srv-db1tmv17lnhs73efdjp0), ID srv-db1tmv17lnhs73efdjp0.
- API: https://imagino-api-ai-staging.onrender.com; health final 200.
- Branch: codex/imagino-ai-revival-v2.
- SHA de runtime final: **96d3af59f76e525ac7731727dbc6def0b1d8d970**.
- Deploy final: **dep-db20d9ss728c73ak45c0**, Live em 05/10/2026 20:21:28 UTC, com PaidGenerationEnabled=false.
- Call 1 executou no SHA e51976b34f20356f7e9f1d81b161e5463b13a110; calls 2/3 no 96d3af5 após correção de precisão.
- [Preview Generation V2](https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app/create/image), frontend c57f923d533ce92b92525a2e9f963958dd0c8b0a; nenhum código do frontend foi alterado nesta etapa.
- PRs [API #56](https://github.com/danitest45/imagino-api/pull/56) e [frontend #87](https://github.com/danitest45/imagino-front/pull/87) permanecem draft, abertos e sem merge.

A chave foi criada/inserida manualmente pelo usuário, somente nesse serviço. Seu valor e o clipboard não foram lidos nem exibidos. O perfil AIStaging registra BFL e fixture; o gate permite apenas os dois IDs aprovados, owner sintético, briefs exatos, três chaves de idempotência e a referência controlada. Nenhum outro provider foi habilitado.

## Chamadas, saldos e custo observado

Todas: uma saída PNG de 1024×1024, 1MP, 1:1, seed 20261005, safety_tolerance=2. Pro: disable_pup=true para preservar o brief controlado. Call 3: exatamente uma referência PNG local de 11.715 bytes, 1024×1024, SHA-256 f528f7cdc43f68d75f27e3fd3cbc491a02bb296a6f73c5fe57b374834517b333. Nenhuma foto pessoal ou geração extra.

| Call | Provider/modelo e modo | Quote Imagino | Estimado USD | Observado USD | BFL créditos antes→depois | Imagino antes→depois | Resultado |
|---|---|---:|---:|---:|---|---|---|
| 1 | BFL FLUX.2 klein 4B, texto | 5 | 0,014 | 0,014 | 1.000→998,60 | 46→41 | Completed/Charged |
| 2 | BFL FLUX.2 Pro, texto | 10 | 0,030 | 0,030 | 998,60→995,60 | 41→31 | Completed/Charged |
| 3 | BFL FLUX.2 Pro, texto + 1 referência | 15 | 0,045 | 0,045 | 995,60→991,10 | 31→16 | Completed/Charged |
| Total | 3 POSTs BFL | **30** | **0,089** | **0,089** | **8,90 consumidos** | **46→16** | **3/3** |

O owner possuía 16 créditos e recebeu um único grant auditado de 30 créditos sintéticos para o teste; saldo preparado 46. Não houve compra ou cobrança de créditos Imagino. Os US$10 de saldo BFL foram adicionados previamente pelo usuário, fora desta execução. Saldo BFL final exato inferido dos 991,10 créditos: US$9,911; a UI arredonda para US$9,91. Auto top-up permaneceu Inactive.

O custo é confirmado pelos custos de aceitação e pelo delta de saldo BFL. A UI Usage confirma três requests: um klein e dois Pro. Ela exibe US$0,09 por arredondar a centavos. Infraestrutura R2/rede não tem medição faturada por job nesta etapa; o custo real de IA foi US$0,089. Não houve compra adicional de saldo, assinatura ou plano.

## Latências

Segundos. Aceitação mede o POST BFL até resposta lida. Processamento mede ProviderBound até Ready observado e inclui a cadência de polling; não é duração interna fornecida pelo BFL. Download e storage são medidos separadamente pelo worker. Total API mede submissão Imagino até Completed observado pelo cliente, incluindo polling. Total servidor mede CreatedAt→CompletedCharged do journal.

| Call | Aceitação BFL | Processamento observado | Download | R2 storage | Total servidor | Total API observado |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 0,773 | 4,459 | 1,255 | 1,269 | 8,717 | 9,354 |
| 2 | 1,325 | 4,424 | 0,447 | 1,493 | 9,002 | 9,368 |
| 3 | 0,530 | 10,199 | 0,450 | 0,383 | 12,191 | 13,682 |

## Jobs e saídas de staging

| Call | Imagino job ID | BFL job ID | Bytes PNG |
|---|---|---|---:|
| 1 | 6ac40507e10e977b2df88393 | 4246bd51-db79-4d6c-b409-c1481ef535fd | 1.291.844 |
| 2 | 6ac40643985c143f201d8ea0 | 56ebfd56-f9d7-4e53-af4a-7e523f8f2c6f | 1.689.642 |
| 3 | 6ac4068a985c143f201d8f15 | 45417d9f-12d3-4454-88ab-9236ea8edcd5 | 1.352.396 |

Bucket imagino-images-staging; prefixo generation-v2/6ac038cb05509cea703277eb/. Total 4.333.882 bytes. Todas as dimensões 1024×1024 e assinatura PNG validadas no worker e nos bytes autenticados baixados.

- [Call 1 — R2 staging](https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev/generation-v2/6ac038cb05509cea703277eb/6ac40507e10e977b2df88393.png)
- [Call 2 — R2 staging](https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev/generation-v2/6ac038cb05509cea703277eb/6ac40643985c143f201d8ea0.png)
- [Call 3 — R2 staging](https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev/generation-v2/6ac038cb05509cea703277eb/6ac4068a985c143f201d8f15.png)

## Gates e evidências

| Gate real | Resultado |
|---|---|
| Health e catálogo live | PASS; health 200, BFL ready durante teste e approval_required no final |
| Conta sintética e quote | PASS; 5/10/15, estimativas 0,014/0,030/0,045 |
| Reserva transacional e criação | PASS; saldo debita uma vez, job e slot persistidos |
| Worker claim e autorização de POST | PASS; um BflPostAttemptAuthorized por job |
| BFL POST e provider binding | PASS; três IDs BFL persistidos, três bindings em logs |
| Polling e download do output | PASS; três resultados obtidos, sem falhas de polling |
| Validação e R2 | PASS; PNG 1024×1024 armazenado antes de liquidação |
| Settlement | PASS; três Completed/Charged, nenhum refund ou erro |
| Histórico do owner | PASS; 5→6→7→8, três novos jobs presentes |
| Resultados no frontend | PASS; três cards Completed e imagens realmente carregadas a 1024×1024 |
| Download autenticado do owner | PASS via API; três HTTP 200, PNG completo, hashes gravados; Cache-Control private, no-store |
| Outro usuário | PASS; GET job e download 404 em cada call, nenhum job alheio no histórico |
| Reconciliação entre calls | PASS; ledger auditado antes de call 2 e 3 |
| Limite de gasto/contagem | PASS; três slots consumidos, nenhum reset/retry, total 0,089 |
| Logs operacionais | PASS; 32 linhas Generation, três bindings e três settlements; sem prompts completos ou segredos |
| Índices Atlas | PASS; _id_, owner_idempotency, worker_due, owner_history reconfirmados |
| Encerramento | PASS; PaidGenerationEnabled=false no deploy Live, demais providers indisponíveis |

Os três botões Download do Preview foram acionados e não mostraram erros. A captura automatizada do evento de salvamento do navegador integrado permaneceu inconclusiva, como já documentado no Core aprovado; não afirmo ter confirmado o arquivo salvo na pasta de downloads do sistema. A entrega autenticada de cada arquivo foi comprovada por HTTP 200, assinatura, dimensões, bytes e SHA-256.

Generation V2 Core Staging continua PASS conforme revisão aprovada. Não repetimos os gates sintéticos de failure/refund/zero crédito/restart nesta homologação paga. O ledger persistiu pelo redeploy entre calls 1 e 2. Não provocamos restart durante um job BFL ativo nem submissão ambígua. A proteção de replay já homologada foi mantida; não submetemos requests adicionais de geração depois da terceira chamada.

A política existente de URL pública R2 foi mantida. Os 404 comprovam ownership na API, não privacidade da URL pública R2.

## Bug corrigido

Na call 1, o BFL serializou 1,4 créditos como 1.4000000000000001. O adapter persistiu US$0,014000000000000001 e o gate corretamente interrompeu as próximas chamadas por comparação exata. Desativamos gasto, reconciliamos com o saldo 1.000→998,60 e registramos o valor bruto no ledger. Não repetimos o POST.

Correção no SHA 96d3af5: normalizar o custo convertido para 12 casas decimais, eliminando apenas ruído abaixo de US$0,000000000001. Testes cobrem os três valores com ruído e preservação de mudança real para US$0,03001. **233 testes backend passaram, zero falhas/skips.** Pricing permaneceu inalterado. Calls 2/3 reportaram os custos esperados sem interromper o gate.

## Economia e recomendação

Comparação com AI_CREDIT_ECONOMICS.md: estimado e observado coincidem, delta econômico US$0. Sob receita hipotética US$0,01/crédito, buffer 1,10 e overhead US$0,002/imagem, margens continuam 65,2%, 65,0% e 65,7% (agregado 65,37%). Overhead é provisão, não fatura medida. Não alteramos pricing permanente.

A conversão e custos estão de acordo com a [documentação oficial BFL](https://docs.bfl.ai/quick_start/pricing).

- **Manter klein como candidato Fast.** O perfume atendeu ao brief: vidro azul translúcido, pedra bege, composição simples, sombra e iluminação plausíveis. Uma amostra demonstra contrato funcional e custo baixo, não superioridade estatística de velocidade/qualidade.
- **Manter Pro como candidato Studio.** A arquitetura, materiais e reflexos ficaram convincentes; a call 2 introduziu uma inscrição indesejada apesar de “no text”. A call 3 preservou forma arredondada retangular, tampa azul, faixa dourada e três círculos em triângulo, com novo pedestal e cenário botânico.
- **Não substituir automaticamente por FLUX.3 nesta etapa.** FLUX.3 não foi testado. A [tabela oficial](https://docs.bfl.ai/quick_start/pricing) lista US$0,048 por 1K; pela fórmula atual seriam necessários aproximadamente 16 créditos para manter 65%, cálculo próprio e não aplicado. Qualidade/fidelidade e preço efetivo devem ser comparados num benchmark separado autorizado antes de decidir o catálogo. Três amostras aqui não são um benchmark estatístico nem decisão final de catálogo.

## Arquivos locais de evidência

- BFL_FINAL_SUMMARY.json: métricas/custos/IDs/configuração e resultado consolidado.
- BFL_CALL_1.json, BFL_CALL_2.json, BFL_CALL_3.json: quotes, saldos, timestamps, segurança e hashes.
- BFL_ATLAS_JOBS_FINAL.json: jobs/journal/metrics projetados sem prompts, inputs ou polling URLs.
- BFL_LEDGER_FINAL.json: slots, custos, reconciliação e auditoria.
- BFL_RENDER_LOGS.json: logs operacionais sanitizados por seleção Generation.
- BFL_FINAL_PREVIEW.png, BFL_FINAL_RENDER.png, BFL_FINAL_USAGE.png: screenshots reais.
- BFL_CALL_1.png, BFL_CALL_2.png, BFL_CALL_3.png: arquivos recebidos pelo endpoint autenticado do owner.

Stripe, serviço antigo, produção, Little Haven, Gemini, vídeo, rebranding e merges não foram tocados. Encerrado no limite de três chamadas.
