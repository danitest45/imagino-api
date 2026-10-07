# Imagino Generation 2.0 — decisão de providers

Referência: 02/10/2026. Decisão de engenharia para avaliação; qualidade e latência reais ainda não homologadas.

## Decisão

Começar com integração direta BFL e Google, mantendo uma arquitetura que admite agregadores. A estratégia de evolução é híbrida: fal para um futuro segundo fornecedor de vídeo se superar Veo no benchmark; Replicate como alternativa de distribuição, sem fallback automático pago.

O catálogo tem três ferramentas de imagem para homologação e dois schemas de vídeo em compatibilidade. Fast Image explora ideias a baixo custo; Studio Image trabalha com referências; Edit & Design prioriza edição por linguagem natural. Fast Video e Cinema Video representam dois níveis de custo com áudio nativo, mas seus endpoints Preview NÃO devem ser ativados para lançamento. O modelo aparece na descrição; a navegação é por tarefa.

Decisão final após verificar o calendário publicado pelo Google: Veo 3.1 Preview foi reclassificado como COMPATIBILITY e bloqueado por migration_required, mesmo com credenciais/gate pago liberados. A retirada prevista é 22/10/2026. O código também protege documentos ACTIVE já seedados. Após a data, disponibilidade retired e cotação recusada. Recomendo Omni 1.1 Flash como primeiro candidato de migração, ou Veo GA em Cloud se seus controles forem necessários; ambos exigem contrato/acesso/benchmark próprios. Os dois adapters existentes preservam trabalho de schema/poll/storage, sem fingir que o endpoint mudou. [Calendário](https://ai.google.dev/gemini-api/docs/deprecations?hl=en).

FLUX.2 klein 4B e pro foram escolhidos como baseline de custo e contrato conhecido, não como declaração de liderança em outubro. FLUX.3 já existe e merece comparação antes do lançamento. O pro usa o endpoint fixo, enquanto versões Google preview exigem revalidação de disponibilidade. Gemini 3.1 Flash Image usa o contrato Interactions atual, com interação armazenada para recuperação após reinício.

## Alternativas

| Caminho | Vantagem | Custo operacional / decisão |
|---|---|---|
| Todos diretos | Preço do fornecedor, menos intermediários | Muitos contratos, contas e esquemas. Limitado a duas famílias nesta fase. |
| Tudo em fal | Fila e catálogo amplo | Preços e capabilities variam por SKU; mais um operador de dados. Reservado para expansão. |
| Tudo em Replicate | Versionamento, predictions, webhooks | Retenção temporária e diferença de preço por deployment. Não substituir automaticamente os diretos. |
| GPU própria / RunPod | Controle de workflow e pesos | Operação, warm-up, GPU ociosa, licença e moderação. Sem volume que justifique. |
| Híbrido | Liberdade de escolher por tarefa | Cada oferta guarda provider e versão; mudança de fornecedor cria versão nova. |

As recomendações são inferências de preço, documentação e escopo, não resultados de um teste comparativo pago. Não há SLA medido. Rate limits devem ser obtidos da conta efetivamente autorizada; não adotar limite de marketing como limite do produto.

## Adiamentos concretos

- OpenAI GPT Image 2.5: candidato importante para texto/design e transparência; cobrança por tokens pede calibração antes de preço fixo ao usuário.
- Ideogram 4.5: candidato a edição precisa; preço atual precisa confirmação no painel de API. A tabela anterior de 4.0 não precifica 4.5.
- Stability: bom candidato para máscara/inpainting e ferramentas separadas; adicionar somente quando existir fluxo de edição regional.
- FLUX.3, Gemini Omni, Wan 3, Kling 3, Seedance 2.5, Runway 4.5, Luma Ray 3.2 e MiniMax H3: ampliar benchmark, não ampliar o menu agora.
- xAI Grok Imagine Image 2.0/Video 1.5 Lite: alternativa direta econômica pesquisada; acesso, qualidade e controles precisam medição antes de mais um adapter.
- Imagen 4: excluído na Gemini API por retirada; Cloud é distribuição separada, sem acesso confirmado nesta fase.
- Recraft: vetor é interessante, mas a restrição contratual a serviços concorrentes exige esclarecimento comercial antes da seleção.
- Sora: excluído porque a documentação oficial registra remoção da API em 24/09/2026.
- Comfy/RunPod e Replicate legado: manter compatibilidade; não reutilizar parâmetros e preços antigos como decisão nova.

## Critérios para trocar a escolha

Comparar os mesmos briefs, com orçamento aprovado: aderência ao prompt, texto em português, fidelidade ao produto, identidade entre variações, artefatos, edição fora da região desejada, áudio e continuidade temporal. Medir sucesso, moderação, custo faturado, tempo de fila, geração, download e armazenamento, P50/P95. Uma troca precisa melhorar qualidade útil ou custo total sem degradar confiabilidade. Não fazer benchmark de centenas de chamadas nesta fase.

## Implementação e gates

Há adapters HTTP diretos BFL, Gemini Image e Veo; o fixture sintético exercita o pipeline sem IA. O flag global pago fica false, e nenhuma chave foi adicionada. O gate bloqueia antes da reserva e novamente antes do POST do worker. A autorização humana futura deve delimitar modelos, quantidade e teto; não significa habilitar o catálogo inteiro permanentemente.

Fontes e preços comparados: [pesquisa](AI_MARKET_RESEARCH_2026.md), [catálogo](AI_MODEL_CATALOG.md), [economia](AI_CREDIT_ECONOMICS.md). Contratos: [BFL](https://docs.bfl.ai/api_integration/integration_guidelines), [Google Interactions](https://ai.google.dev/gemini-api/docs/image-generation), [fal webhooks](https://fal.ai/docs/documentation/model-apis/inference/webhooks), [Replicate retenção](https://replicate.com/docs/topics/predictions/data-retention).
