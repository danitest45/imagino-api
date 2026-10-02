# Aprendizados de produto para a próxima fase visual

Pesquisa pública em 02/10/2026; análise de interfaces documentadas e páginas oficiais, sem assinar planos ou gerar amostras pagas. Observações de produto não provam disponibilidade do endpoint específico de cada conta.

| Produto / evidência | Padrão observado | Aplicação no Imagino |
|---|---|---|
| [Krea Realtime Edit](https://www.krea.ai/blog/realtime-edit) | Direção visual por canvas e resposta contínua | Futuro editor regional com feedback; não prometer realtime no pipeline atual |
| [Krea video editor, setembro/2026](https://www.krea.ai/blog/how-to-edit-video-with-krea-agent) | Clips viram timeline, extensão e export | Pensar em projeto/asset reutilizável, além de lista de gerações |
| [Runway Gen-4.5](https://help.runwayml.com/hc/en-us/articles/46974685288467-Creating-with-Gen-4-5) | Brief, controles, iteração e uso/download | Resultado com reutilizar prompt e controles, não resultado sem continuidade |
| [Google Flow](https://blog.google/innovation-and-ai/products/google-flow-veo-ai-filmmaking-tool/) | Continuidade de cenas, ingredientes e direção de câmera | Primeiro/último frame agora; biblioteca de referências e shots depois |
| [Higgsfield](https://higgsfield.ai/ai-video) | Hub de ferramentas, motion control, frames | Entradas claras por objetivo; evitar uma parede de nomes nativos |
| [Adobe Firefly](https://www.adobe.com/learn/firefly/web/video-motion-reference) | Referência de movimento torna intenção concreta | Mostrar papel do input: produto, estilo, primeiro frame ou movimento |
| [Ideogram 4.5](https://ideogram.ai/models/4.5/) | Edição precisa e comparação do detalhe | Futuro antes/depois e preservação de área, sem chamar referências atuais de inpainting |

O site de um agregador pode continuar exibindo um modelo após descontinuação do fornecedor. Disponibilidade comercial deve vir do contrato oficial, não de um logo em landing page. Exemplo concreto: proposta exclui Sora após remoção oficial da API.

## Decisões já implementadas

Um workspace por mídia, nomes orientados à tarefa, descrição do modelo subjacente, controles provenientes do catálogo, upload somente quando suportado, custo antes de criar, estado de processamento recuperado do servidor, cancelamento somente quando possível, erro com situação dos créditos, reutilização e download. A UI acompanha o idioma inglês já usado pelo produto; esta fase não muda identidade visual global.

Durante indisponibilidade, catálogo versionado aparece como prévia com criação bloqueada. Isso permite avaliar taxonomia e controles sem fabricar resultados. Pipeline Demo declara mídia sintética; não deve aparecer no lançamento. A lógica de preço vive no backend; preço inicial do snapshot é apenas referência, não cotação válida.

## Recomendações para o redesign posterior

1. Separar Explore, Create, Refine e Motion por intenção, com recomendação padrão e comparação de custo/uso. Manter número pequeno de escolhas.
2. Criar projetos e biblioteca pessoal de assets. Permitir usar resultado como referência ou primeiro frame com consentimento claro.
3. Adicionar editor de região quando houver máscara/inpainting real. Mostrar antes/depois e custo da iteração.
4. Construir fluxo de vídeo em shots: brief, keyframes, render, seleção, timeline/export. Evitar embutir um editor completo antes de validar uso.
5. Testar mobile, teclado, leitores de tela, upload e mensagens de crédito com usuários. A aprovação técnica não substitui estudo de usabilidade.

Evitar percentuais falsos de progresso, promessas de identidade perfeita, presets de câmera que só concatenam texto sem explicar resultado, custo oculto por referência, geração novamente após erro de rede e marketing de modelos não disponíveis. Medir tempo até primeira criação útil, taxa de resultado reaproveitado, erro recuperável, falhas de upload e abandono após cotação. Não registrar prompt privado como evento analítico.
