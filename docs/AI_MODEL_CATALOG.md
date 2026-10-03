# Catálogo curado — staging

Revisão pública 2026-10-02.2, seis entradas em generation_catalog_v2. Três candidatos de imagem, dois schemas de vídeo COMPATIBILITY bloqueados para migração, e Pipeline Demo sintético exclusivo do staging. Vídeos têm versão .2; imagens/preços mantêm .1.

| ID | Nome / tarefa | Provider / endpoint | Controles implementados | Inputs |
|---|---|---|---|---|
| flux-fast-20261002 | Fast Image / Explore | BFL / flux-2-klein-4b | 1MP ou 4MP; quadrado, horizontal, vertical | Apenas texto nesta oferta |
| flux-studio-20261002 | Studio Image / Create | BFL / flux-2-pro | Mesmos controles | Até 4 referências PNG |
| gemini-edit-20261002 | Edit & Design / Refine | Gemini / gemini-3.1-flash-image | 1K, 2K, 4K; 1:1, 16:9, 9:16 | Até 4 referências PNG |
| veo-fast-20261002 | Fast Video / Motion | Google / veo-3.1-lite-generate-preview | 4/6/8s; 720p/1080p; 16:9/9:16; áudio nativo | Primeiro e último frame |
| veo-cinema-20261002 | Cinema Video / Motion | Google / veo-3.1-generate-preview | Mesmos controles | Primeiro e último frame |
| pipeline-demo-20261002 | Pipeline Demo / Staging | fixture / synthetic-png-v1 | Sucesso ou falha; 1 crédito fixture | Nenhum |

Os limites do produto são deliberadamente menores que os limites máximos dos vendors. Referências passam por canvas no browser: PNG até 1024px e 2MiB cada. O backend rejeita URL arbitrária, formato inválido, excesso, tamanho e capability ausente. Primeiro/último frame exige 8s e último exige primeiro; 1080p exige 8s. Áudio não tem toggle, pois está incluído nessas ofertas.

Dimensões BFL: 1024×1024, 1344×768 ou 768×1344; opção 4MP duplica ambos os eixos. Os formatos horizontais/verticais são aproximações alinhadas a blocos do provider. O cálculo usa megapixels efetivos arredondados para cima, não apenas o nome da opção. Uma saída por job; quantidade em lote, seed, negative prompt, máscara, transparência, reference video e controle estruturado de câmera não são capabilities expostas.

## Estados de disponibilidade

- ACTIVE é lifecycle do candidato, não prova de homologação.
- approval_required: gasto real bloqueado globalmente.
- credentials_required: após autorização, falta configuração do provider.
- ready: acesso configurado e gate liberado.
- synthetic_demo: somente fixture habilitado em staging.
- disabled: oferta/provider desabilitado.
- deployment_pending: snapshot do frontend durante indisponibilidade do backend; todas as criações bloqueadas.
- migration_required: os Veo Preview foram bloqueados por retirada próxima; liberar gasto não habilita esses endpoints.
- retired: após 22/10/2026, os endpoints Veo Preview não aceitam nem cotação. A data é publicada no catálogo público; ofertas antigas ACTIVE também são protegidas pelo guard central.

O catálogo público contém schemas, descrição e preço inicial; não contém chaves, polling URL ou metadata interna de custo. Cotação autenticada resolve o custo para os controles atuais. A UI usa fields, inputs e rules; mudar o modelo não exige uma página por provider.

## Seed e versionamento

GenerationCatalog.Seed produz documentos versionados. MongoGenerationRepository.InitializeAsync usa $setOnInsert por ID e cria índices. A migração .1→.2 é restrita aos dois IDs Veo do seed e ao endpoint original, atualizando só versão/lifecycle/descrição; não altera pricing ou entradas administradas separadamente. Nesta execução, seed e migração foram exportados do assembly e aplicados pelo conector Atlas porque o startup do Render foi bloqueado. O seed repetido não modificou nenhuma das seis entradas; a migração repetida não modificou nenhuma das duas. Não houve migração de jobs legados nem remoção de documentos reais.

Nova revisão de preço/contrato deve criar ID/versão novos e desativar a oferta anterior; cada job contém snapshot imutável. O snapshot público do frontend foi gerado a partir deste catálogo e serve apenas como prévia indisponível. O endpoint live substitui a prévia automaticamente quando ficar disponível.

## Legado

| Integração | Classificação | Ação nesta fase |
|---|---|---|
| Adapters v2 BFL/Gemini imagem | ACTIVE, avaliação paga bloqueada | Novo fluxo |
| Adapter Veo 3.1 Gemini Preview | COMPATIBILITY | Estrutura preservada; ativação bloqueada; migrar endpoint antes do lançamento |
| Resolvers, presets e jobs de imagem/vídeo antigos | COMPATIBILITY | Preservados; novos preços não retroagem |
| Gemini antigo via generateContent e Veo antigo | COMPATIBILITY | Não usados nas ofertas v2 |
| ReplicateImageProviderClient e rotas antigas | COMPATIBILITY | Preservados por dependências e callbacks |
| RunPod/Comfy, parâmetros fixos de steps/guidance | DEPRECATED para catálogo novo | Nenhuma entrada nova; remoção futura após auditoria de uso |
| URLs de providers na navegação como identidade do produto | DEPRECATED | Novos workspaces /create/image e /create/video |
| Sora API removida | REMOVE da proposta de lançamento | Nenhum código novo |

Estados legados Created/Running/Pending são normalizados na leitura do frontend para Queued/Processing, preservando documentos e contratos antigos. As collections v2 já armazenam os seis estados canônicos. Não houve limpeza destrutiva de legado.
