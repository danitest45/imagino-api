# Mercado de geração de mídia — outubro de 2026

Consulta: 02/10/2026, páginas oficiais de API, modelos, preços e produtos. Valores públicos em USD, sem impostos, descontos negociados ou conversão cambial. Nenhuma geração paga foi realizada. A disponibilidade descrita é documental, não acesso confirmado na conta do Imagino. Exemplos e rankings do vendor não são benchmark independente.

## Mudanças que invalidam uma seleção baseada em 2025

OpenAI documenta GPT Image 2.5 Sunburst/Flare e registra a remoção de Sora 2/Videos API em 24/09/2026. BFL já publica FLUX.3 Image/Video além de FLUX.2. Google possui Gemini 3.1 Flash Image, alternativa Flash Lite Image e Omni 1.1 Flash para vídeo/edição. Ideogram apresenta 4.5; Luma Ray 3.2/Uni-1 Max; MiniMax H3; Wan 3 e Seedance 2.5 também devem entrar na avaliação. Não tratar Veo 2, SDXL ou Hailuo 2.3 como estado atual da arte.

Fontes: [OpenAI imagem](https://developers.openai.com/api/docs/guides/image-generation), [remoções](https://developers.openai.com/api/docs/deprecations), [BFL modelos](https://docs.bfl.ai/llms.txt), [Google preços](https://ai.google.dev/gemini-api/docs/pricing), [Ideogram 4.5](https://ideogram.ai/models/4.5/), [Luma modelos](https://docs.agents.lumalabs.ai/guides/models), [MiniMax](https://platform.minimax.io/subscribe/token-plan?tab=api-enterprise).

A revisão da página Google de deprecações, atualizada em 01/10, altera a seleção de vídeo: Gemini API programa a retirada dos três Veo 3.1 Preview para 22/10/2026 e recomenda Omni 1.1 Flash. Imagen 4 nessa API foi retirado em agosto; Veo 2/3 em junho. Cloud/Agent Platform possui catálogo e ciclo próprios: não inferir que seus endpoints GA também foram retirados. Os adapters Veo implementados nesta fase passam a COMPATIBILITY, sem ativação paga; a escolha para lançamento exige uma versão nova do endpoint vigente. [Calendário oficial](https://ai.google.dev/gemini-api/docs/deprecations?hl=en).

## Matriz de imagem

Legenda: S = capacidade documentada; P = aproximação por prompt/referência, sem controle dedicado ou garantia; ND = não confirmado para esse endpoint nesta pesquisa; ferramenta = operação separada. Edição generativa não implica máscara/inpainting, alpha, identidade perfeita ou manutenção de todos os pixels.

| Provider / modelo | T2I | I2I / edição | Inpainting | Referências | Personagem / estilo | Transparência | Resolução / proporções | Texto / qualidade percebida |
|---|---|---|---|---|---|---|---|---|
| OpenAI GPT Image 2.5 Sunburst/Flare | S | S | Máscara na API de edit | S | P/P | PNG/WebP alpha | Dimensões customizadas, múltiplos 16, até 4K; limites do endpoint | Candidato design; não medido |
| Google Gemini 3.1 Flash Image | S | S | Edição por prompt; máscara dedicada ND | S | P/P | Alpha ND | 0.5K–4K, ratios por API | Candidato edição/layout; não medido |
| Google Flash Lite Image | S | S, confirmar oferta | ND | ND | P/P | ND | 1K na tarifa consultada | Alternativa econômica; não medido |
| Google Cloud Imagen 4 Fast/Standard/Ultra | S | Ferramentas distintas; não inferir edit de Imagen 3 | Por operação/SKU | ND | ND | ND | Limites por endpoint Cloud | Cloud ainda lista preços; acesso não homologado |
| xAI Grok Imagine Image 2.0 | S | Edição e composição | Máscara dedicada ND | Multi-image | P/P | ND | 1K/1.5K/2K, ratios por API | Challenger sem benchmark |
| BFL FLUX.2 klein 4B/9B | S | S | ND | Até 4 | P/P | ND | Até 4MP, dimensões controladas | Exploração rápida; não medido |
| BFL FLUX.2 pro/max/flex | S | S | ND | Pro até 8 na API | P/P | ND | Até 4MP | Produto/fidelidade a avaliar |
| BFL FLUX.3 Image | S | S | ND | Referências/controles espaciais | P/P | ND | Faixas 768px, 1K, 2K, 4K | Novo challenger; não medido |
| Ideogram 4.5 | S | Generate/edit e precise-edit | Máscara opcional em precise-edit | Até 4 nesse fluxo | P/P | ND | 2K / edição de crop documentada | Precisão alegada pelo vendor; não medida |
| Ideogram P-Image / 4.0 | S | Ver SKU/operação | Ver SKU | Ver SKU | P/P | ND | P-Image 1K/2K | Texto/design a comparar com 4.5 |
| Stability Core/Ultra/SD3.5 | S | Ferramentas separadas | S, ferramenta | Controle/style separado | P/S por ferramenta | Remoção de fundo separada | Limites específicos, tipicamente 1–1.5MP nas rotas consultadas | Bom candidato editor regional; não medido |
| Luma Uni-1/Uni-1 Max | S | S | ND | Até 9 no Max | P/P | ND | Max 2K | Referências multimodais; não medido |
| Recraft API | S raster/vetor | S | S | S | P/S | Ferramenta de fundo | Raster e SVG; verificar SKU | Diferencial vetor, gate contratual |
| Adobe Firefly API | S | Fill/expand/composite | S, fill | S por operação | ND/S por operação | Depende da operação | ND por SKU | Workflow de design; não medido |
| fal / Replicate | Por modelo | Por modelo | Por modelo | Por modelo | Por modelo | Por modelo | Por endpoint/version | Distribuição não é qualidade de modelo |

Fontes de capabilities: [Google imagem/Interactions](https://ai.google.dev/gemini-api/docs/image-generation), [FLUX.2](https://docs.bfl.ai/flux_2/flux2_overview), [edição BFL](https://docs.bfl.ai/flux_2/flux2_image_editing), [Ideogram API v2](https://developer.ideogram.ai/ideogram-api/api-overview), [Stability referência](https://platform.stability.ai/docs/api-reference), [Luma](https://docs.agents.lumalabs.ai/guides/models), [Recraft](https://www.recraft.ai/api), [Firefly async](https://developer.adobe.com/firefly-services/docs/firefly-api/guides/how-tos/using-async-apis).

## Imagem: preço e contrato

| Provider/modelo | Preço observado / unidade | Execução / maturidade documental | Limitação / decisão |
|---|---|---|---|
| GPT Image 2.5 | Imagem input $8/M, cache $2/M, output $30/M tokens; texto input $5/M | Images/Responses, sync/stream | Custo depende de tokens, qualidade e dimensão; não inventar preço fixo 1K |
| Gemini 3.1 Flash Image | Output 0.5K $0,045; 1K $0,067; 2K $0,101; 4K $0,151; input adicional | Interactions, contrato steps atual | Conta paga; edição com referências tem custo de input |
| Flash Lite Image | 1K $0,0336 de output | Oferta documentada | Challenger antes de substituir Fast |
| Cloud Imagen 4 | Fast $0,02; Standard $0,04; Ultra $0,06/imagem | API Cloud separada | Não reativar o endpoint removido da Gemini API |
| Grok Imagine Image 2.0 | Input $0,01/imagem; output 1K low $0,04, 2K low $0,06, 1K medium $0,06, 2K medium $0,08 | REST imagem síncrona | Preços regionais/acesso a confirmar |
| FLUX.2 klein 4B | Primeiro MP $0,014, extra $0,001, referência $0,001/MP | Async, polling regional retornado | Inference subsegundo é alegação, não latency total |
| FLUX.2 klein 9B | Primeiro MP $0,015, extra $0,002 | Mesmo padrão | Comparar qualidade/custo com 4B |
| FLUX.2 pro | Primeiro MP $0,03, extra/ref $0,015/MP | Endpoint fixo; polling | URL de saída assinada expira; armazenar imediatamente |
| FLUX.2 max / flex | Max $0,07 primeiro MP + $0,03 extras/ref; flex $0,05/MP | Polling | Premium sem demanda provada |
| FLUX.3 Image | $0,041 em 768; $0,048 em 1K; $0,100 em 2K; $0,607 em 4K | Novo contrato, validar SKU | Não aplicar fórmula de MP de FLUX.2 |
| Ideogram 4.5 | ND: página atual exige carregamento dinâmico para tabela | API v2 generate/precise-edit | Não usar taxa de 4.0 para 4.5 |
| Ideogram 4.0 / P-Image | 4.0 $0,03/$0,06/$0,10; P-Image 1K $0,003–$0,033 por qualidade | API disponível | Tabela de SKU anterior; revalidar conta/endpoint |
| Stability | Core $0,03; Ultra $0,08; SD3.5 large $0,065, turbo $0,04, medium $0,035, flash $0,025 | REST por operação, saída bytes/base64 | Pricing de ferramentas não é preço de Core |
| Uni-1 Max | 2K $0,100 texto; $0,103 edição/1 ref; +$0,003/ref | Submit/poll/download | PAYG compartilhado sem SLA; throughput contratado desnecessário agora |
| Recraft / Adobe | Não fixado nesta seleção | Recraft API; Adobe OAuth/IMS e jobs async | Escopo/licença ou contrato enterprise a esclarecer |

Preços: [OpenAI guia e calculadora](https://developers.openai.com/api/docs/guides/image-generation), [Google](https://ai.google.dev/gemini-api/docs/pricing), [FLUX.2](https://bfl.ai/pricing?category=flux.2), [FLUX.3](https://docs.bfl.ai/quick_start/pricing), [Ideogram tabela de API consultada](https://ideogram.ai/plans?plan=pro&pricing_tab=api), [página atual](https://ideogram.ai/pricing/?pricing_tab=api), [Stability](https://platform.stability.ai/docs/api-reference), [Luma](https://docs.agents.lumalabs.ai/guides/pricing).

Complementos: [Google Cloud pricing](https://cloud.google.com/gemini-enterprise-agent-platform/generative-ai/pricing), [xAI preços](https://docs.x.ai/developers/pricing), [xAI capacidades](https://x.ai/api/imagine).

## Matriz de vídeo

S = documentado; ND = endpoint/combinação ainda não verificado. Os recursos máximos do vendor não são automaticamente capabilities oferecidas pelo Imagino. Duração, resolução, inputs e áudio frequentemente alteram preço ou restringem combinações.

| Modelo / distribuição | T2V / I2V | Primeiro / último frame | Ref imagem/vídeo / edição | Duração e saída | Áudio / câmera | Preço público e observação |
|---|---|---|---|---|---|---|
| Veo 3.1 Lite / Google | S/S | S/S | Frame; refs especiais não no Lite | 4/6/8s, 720/1080 | Nativo / prompt | $0,05/s 720; $0,08/s 1080 |
| Veo 3.1 Fast / Google | S/S | S/S | Referências por modo | 4/6/8s, até 4K | Nativo / prompt | $0,10/s 720; $0,12 1080; $0,30 4K |
| Veo 3.1 / Google | S/S | S/S | Referências por modo | 4/6/8s, até 4K | Nativo / prompt | $0,40/s 720/1080; $0,60 4K |
| Gemini Omni 1.1 Flash / Google | Geração e edição documentadas | ND por modo | Multimodal, edição | Conferir limites do SKU | Multimodal; ND câmera dedicada | Output tokenizado, ordem $0,10/s 720, input extra |
| Sora 2 / OpenAI | API removida | Não aplicável | Não aplicável | Remoção 24/09/2026 | Não aplicável | Não oferecer |
| Kling 3 Pro / fal | S/S | S/S por endpoint | Elements, motion control | 3–15s segundo guia; SKU a confirmar | Áudio opcional / motion | $0,112/s sem áudio; $0,168 áudio; $0,196 voice; elementos podem duplicar |
| Runway Gen-4.5 | S/S | Primeiro S, último ND | Edição via Aleph 2 separado | 2–10s, 720p, seis ratios no playground | Confirmar áudio por modelo / direção por prompt | $0,12/s; turbo $0,05/s; Aleph2 $0,28/s |
| Luma Ray 3.2 | S/S | Interpolação/keyframes documentados | Edit, extend, reframe | 5/10s; draft–1080; HDR por modo | Áudio ND / controles por prompt | 5s 720 $0,30, 1080 $1,20; 10s $0,90/$3,60; draft 5s $0,06 |
| MiniMax H3 | S/S por guia | Confirmar último no SKU | Referências e vídeo de input | 768P/2K, duração por endpoint ND | Confirmar áudio/câmera | $0,08/s 768P; $0,13/s 2K; input vídeo também cobrado |
| Seedance 2.5 / fal | S/S | Frames e multimodal por endpoint | Imagem/vídeo/áudio | Até 30s; resolução depende do endpoint/região | Áudio; direção multimodal | Tokenizado; 480/720 $0,0214/1k tokens, US mais caro |
| Seedance 2.5 / Runway | S/S por SKU | ND | Ref vídeo com sobretaxa | 480/720 no pricing consultado | Ver contrato do agregador | $0,20/$0,30 por segundo, input ref +$0,10/$0,15 |
| Wan 3 / Alibaba | S/S | S/S | Vídeo/áudio/refs, edição | 2–30s, 480/720/1080 | Nativo / multimodal | Internacional $0,05/$0,10/$0,20 por segundo; desconto expirou 24/09 |
| FLUX.3 Video / BFL | S/S | ND para contrato preciso | V2V documentado | HD/FHD/QHD/UHD/draft; duração ND | Áudio/câmera confirmar no SKU | T2V/I2V $0,17/$0,29/$0,40/$0,80 por segundo; draft $0,06 |
| Grok Imagine Video 1.5 / xAI | S/S | Primeiro por imagem; último ND | Referências por endpoint; edição em SKU distinto | Até 15s conforme família; 480/720/1080 por SKU | Áudio; câmera ND | $0,08/$0,14/$0,25 por segundo; Lite $0,02/$0,03/$0,14; input adicional |
| Replicate/fal outros | Dependente do modelo | Dependente | Dependente | Versionar endpoint | Dependente | Comparar SKU com taxa direta, não média do agregador |

Fontes: [Veo API](https://ai.google.dev/gemini-api/docs/veo?hl=en), [Google preços](https://ai.google.dev/gemini-api/docs/pricing), [Google Cloud Veo](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/models/veo/3-1-generate), [Kling](https://fal.ai/learn/tools/how-to-use-kling-3-0-pro), [Runway preços](https://docs.dev.runwayml.com/guides/pricing/), [Runway playground](https://dev.runwayml.com/playground), [Luma API](https://docs.agents.lumalabs.ai/), [MiniMax](https://platform.minimax.io/subscribe/token-plan?tab=api-enterprise), [Seedance fal](https://fal.ai/learn/tools/how-to-access-seedance-2-5-on-fal), [BytePlus](https://docs.byteplus.com/en/docs/ModelArk/2191775), [Wan](https://docs.modelstudio.console.alibabacloud.com/en/model-studio/wan3-0-video), [FLUX.3 preços](https://docs.bfl.ai/quick_start/pricing).

Não comparar tokens de Seedance diretamente com segundos: fórmula envolve width × height × duração × fps / 1024; referências de vídeo podem adicionar tokens. Packs BytePlus com desconto, expiração e compra mínima não são PAYG equivalente; nenhum pack foi comprado. Tabelas de app Luma/Ideogram com créditos de assinatura também não são taxa API USD.

As três linhas Veo acima descrevem contratos/preços pesquisados, mas os endpoints Gemini Preview ficam bloqueados na proposta por retirada próxima. Para xAI, vídeo usa submit/poll; não misturar preço de Video 1.5 com o modelo anterior nem prometer último frame. [Preço por modelo](https://docs.x.ai/developers/pricing), [API](https://x.ai/api/imagine).

## Latência, maturidade, callbacks e disponibilidade

Nenhum tempo médio medido nesta execução. Benchmarks promocionais representam configuração do vendor e podem excluir fila/download. Planejar medidas separadas: queue, provider, storage, end-to-end, P50/P95, falhas e moderação. Não colocar segundos exatos no produto antes da homologação.

| Família | Contrato observado | Maturidade / risco de integração |
|---|---|---|
| BFL | POST + polling_url regional, resultado temporário | Usar polling_url retornado, validar ID e baixar antes da expiração |
| Gemini imagem | Interactions armazenado; GET por ID; steps model_output | Contrato mudou em maio/2026; evitar parser antigo outputs |
| Veo | predictLongRunning, operation name, GET operação e download | Preview, combinações de resolução/duração, disponibilidade/região |
| Runway | Tasks async e polling | API de 4.5 publicada; saídas profissionais/HDR têm sobretaxa |
| Luma Agents | Submit/poll/download unificado | PAYG sem SLA; não confundir com API Dream Machine antiga |
| Adobe | Jobs async, poll/cancel, OAuth IMS | Onboarding enterprise e quotas precisam validação |
| fal | Fila, status, resultado e webhook | Assinatura ED25519 com JWKS, timestamp e raw body |
| Replicate | Predictions, polling e webhooks | Dados/output da API removidos após 1h por padrão; persistir no R2 |
| Kling/Seedance/H3/Wan | Contrato depende da distribuição | Versão e região são parte do catálogo; não misturar preço de agregadores |

Fontes: [BFL integração](https://docs.bfl.ai/api_integration/integration_guidelines), [Gemini alteração de contrato](https://ai.google.dev/gemini-api/docs/interactions-breaking-changes-may-2026), [Runway changelog](https://docs.dev.runwayml.com/api-details/api_changelog/), [fal webhooks](https://fal.ai/docs/documentation/model-apis/inference/webhooks), [Replicate retenção](https://replicate.com/docs/topics/predictions/data-retention).

## Termos e limitações relevantes

Google diferencia dados de tiers pagos/gratuitos na página de preços; o pipeline proposto requer acesso pago autorizado, com retenção de interação explicitada. Recraft contém restrição a uso em serviços concorrentes: esclarecer elegibilidade, sem emitir parecer jurídico. Ideogram separa API hospedada de licenciamento self-host e uso customer-facing. Luma throughput reservado exige compromisso significativo e não foi recomendado. Open weights não equivalem a autorização irrestrita para hospedar um produto comercial. Região, segurança de pessoas/rostos, direitos de input e políticas de cada modelo precisam revisão antes do lançamento, sem afirmar identidade perfeita ou garantia de propriedade sobre qualquer saída.

Fontes: [Recraft developer terms](https://www.recraft.ai/legal/developer-terms), [Ideogram licenciamento](https://ideogram.ai/licensing/), [MiniMax acordo pago](https://platform.minimax.io/protocol/paid-agreement), [Luma preços](https://docs.agents.lumalabs.ai/guides/pricing).

## Conclusão operacional

Selecionar três ofertas de imagem para homologação inicial, com dois fornecedores diretos e preço versionado. Manter dois schemas/adapters de vídeo como compatibilidade bloqueada, migrando antes do lançamento para Omni ou Veo GA em Cloud, ou um concorrente que vença benchmark limitado. Não declarar vencedor de qualidade sem gerações autorizadas. Transparência, máscara e vídeo de referência continuam fora do contrato implementado. Ray 3.2 draft e Grok Video 1.5 Lite são challengers econômicos; Kling/Seedance são challengers multimodais; FLUX.3 e Ideogram 4.5 merecem comparação de imagem.
