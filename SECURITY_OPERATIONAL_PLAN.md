# Imagino Revival — Phase 0B.1

Data da verificação: 2026-09-30. Escopo: preparação de código e inventário somente leitura.
Nenhuma credencial rotacionada/revogada; nenhum merge, deploy de produção, mudança
de DNS, cobrança, escrita em dados de usuários ou criação de índice executados.

## 1. Evidências e limites

- Render: serviço `imagino-api`, Docker, Free, uma instância, branch `master`,
  auto deploy habilitado. A branch desta fase não é a branch de produção.
- Vercel: projeto `imagino-front`, workspace Hobby. As três variáveis de projeto
  visíveis estão em **All Environments**. Uma preview automática deste projeto
  ainda pode apontar à API de produção; não usá-la como staging até separar envs.
- Os painéis foram autenticados pelo usuário e consultados sem revelar/copiar
  valores. A presença do **nome** de uma variável não prova valor não vazio,
  validade da credencial, ambiente de billing ou configuração correta.
- Atlas: projeto `Imagino.ai`, banco usado pelo código `imagino`. Um único usuário
  de banco foi listado, com `atlasAdmin` em `admin`, sem restrição de escopo.
  Sua identidade deve ser tratada como “principal legado”; não é publicada aqui.
  A URI rastreada indica a credencial legada. Como o valor da URI do Render não
  foi revelado, o vínculo entre o principal e a conexão efetiva deve ser confirmado
  pelo operador, com comparação local privada, antes de qualquer revogação.
- Consumidor confirmado no código: API .NET. O frontend só possui configurações
  públicas e usa a API. Não foi possível provar que não existem consumidores
  externos, scripts locais ou ferramentas usando a mesma credencial Atlas.
- Stripe/R2/Google/Resend e contas de IA não foram exercitados com credenciais
  reais. Não inferir uso atual ou saúde a partir de envs/catálogo.

## 2. Inventário e matriz de rotação

“Fonte” descreve a situação antes desta PR; credenciais reais foram removidas.
“Env” significa nome observado no Render, salvo indicação Vercel. “Histórico”
significa exposição identificada na auditoria anterior; tratar como comprometido
até comprovar revogação fora desta fase. Valores não constam deste documento.

| Configuração / nome .NET env | Fonte | Env | Uso/dependência de produção | Secreto? | Rotação | Ordem segura |
| --- | --- | --- | --- | --- | --- | --- |
| `ImageGeneratorSettings__MongoConnection` | Real, agora vazio | Sim | Toda API persistente; consumidor efetivo confirmar | Sim | Obrigatória | Novo principal restrito → configurar → validar → revogar legado |
| `Jwt__Secret` | Vazio atual; histórico exposto | Sim | Emissão/validação de access tokens | Sim | Obrigatória | Staging → código/cutoff compatível → chave nova + cutoff → reautenticação |
| `Jwt__Issuer` | Development presente | Sim | Auth | Não | Revisar coerência; não é secret | Junto à configuração auth; não mudar sem necessidade |
| `Jwt__Audience` | Development presente | Sim | Auth | Não | Revisar coerência | Junto ao issuer/chave |
| `ImageGeneratorSettings__RunPodApiKey` | Real, agora vazio | Sim | Nenhum client RunPod no catálogo ativo; callbacks legados possíveis | Sim | Obrigatória se ativa; revogação após confirmar consumidores/callbacks | Inventário de jobs/endpoint → nova chave se necessário → validar → revogar |
| `ReplicateSettings__ApiKey` | Vazio atual; histórico exposto | Sim | Client de imagem e possível provider legado do catálogo | Sim | Obrigatória se ainda válida | Chave nova + signing secret da mesma conta → validar predições/callbacks → revogar |
| `GeminiSettings__ApiKey` | Vazio | Sim | Client e provider presentes; chamadas reais não verificadas | Sim | Revisão de escopo/uso; exposição não comprovada | Chave restrita independente → staging → substituir → revogar se necessário |
| `VeoSettings__ApiKey` | Vazio | Não observado | Client/provider presentes e jobs históricos; pode estar sem configuração | Sim | Criar credencial isolada de staging; não presumir que Gemini é fallback | Confirmar conta/permissão/quotas antes de habilitar vídeo |
| `R2Settings__AccessKeyId` | Vazio | Sim | Upload/avatar/output | Credencial identificadora | Rotação do par por higiene; exposição atual não comprovada | Novo par restrito ao bucket → upload/leitura → troca → revogar antigo |
| `R2Settings__SecretAccessKey` | Vazio | Sim | Upload/avatar/output | Sim | Como acima | Trocar junto ao AccessKeyId |
| `Stripe__ApiKey` | Vazio atual; histórico exposto | Sim | Checkout/portal/consulta de subscription | Sim | Obrigatória se válida | Sandbox/test primeiro → nova chave restrita → validar sem cobrança → revogar |
| `Stripe__WebhookSecret` | Vazio atual; histórico exposto | Sim | Verificação Stripe preservada | Sim | Obrigatória se válida | Rotação coordenada com endpoint/entregas; confirmar janela de sobreposição no painel |
| `Stripe__PricePro`, `Stripe__PriceUltra` | Vazios | Sim | Seleção de preço e créditos | Não | Não são secrets; separar test/live | Criar/copiar preços apenas em sandbox; validar mapeamento antes do checkout |
| `Resend__ApiKey` | Vazio | Sim; também alias `RESEND__API_KEY` | Verificação/reset por e-mail | Sim | Revisar/rotacionar conforme exposição e escopo | Chave dedicada → test mailbox/domínio → troca canônica → remover alias depois |
| `Google__ClientId` | Vazio atual; histórico | Sim; ClientId público também na Vercel | OAuth | Não | Revisar; não exige troca por ser identificador | Cliente staging separado → callback testado |
| `Google__ClientSecret` | Vazio atual; histórico exposto | Sim | Troca do authorization code | Sim | Obrigatória se válida | OAuth novo testado → coordenar segredo no console/API → validar → retirar antigo |
| `Google__RedirectUri` | Vazio | Sim; URI pública também na Vercel | Callback OAuth | Não | Revisar registro exato | Staging em cliente separado; produção só com autorização |
| `Frontend__BaseUrl` | Presente | Sim | Redirect/links e-mail/CORS fallback | Não | Separar ambientes | URL estável de staging antes de testar OAuth/e-mail |
| `Cors__AllowedOrigins__0..4` | Presente | Sim, cinco nomes | Browser com cookies | Não | Restringir a origens exatas | Revisar lista antes do rollout; nenhuma permissão global `*.vercel.app` |
| `RefreshTokenCookie__Secure/HttpOnly/SameSite/Domain/ExpiresDays` | Presente | Não observado | Sessões; defaults do JSON | Não | Revisar host/domain/SameSite e expiração | HTTPS e domínio compatível → browser staging → manter no rollout |
| `Admin__UserIds__0..N` | Ausente; PR adiciona lista vazia | Não observado | Rotas admin negadas por padrão | Não; identificadores internos | Definir só IDs autorizados, sem conceder a todos | Confirmar operador/admin em staging e depois produção autorizada |
| `Webhooks__ReplicateSigningSecret` | Novo vazio | Não observado | Startup rejeita Replicate com API key/webhook configurado sem ele; callback falha fechado | Sim | Obter da conta correta, sem publicar | Antes de ativar o novo receiver |
| `Webhooks__RunPodEnabled/RunPodSigningSecret` | Novo desligado/vazio | Não observado | Gateway customizado; não é assinatura nativa confirmada | Flag não; chave sim | Não habilitar automaticamente | Confirmar necessidade → gateway confiável → teste de assinatura/job |
| `Auth__RefreshTokensValidAfter` | Novo opcional, ausente | Não observado | Corte global de sessões na rotação | Não | Não ativado nesta fase | Ativar com a nova chave JWT apenas na rotação aprovada |
| `NEXT_PUBLIC_API_URL` (Vercel) | Nome público/exemplo | Sim, All Environments | Destino da API do frontend | Não | Separar Preview/Production | Preview → API staging; nunca credencial aqui |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID/REDIRECT_URI` (Vercel) | Uso removido no código | Sim, All Environments | Legado, substituído por início OAuth na API | Não | Remoção futura controlada | Depois de validar novo fluxo e rollback |
| `MEDIA_ALLOWED_HOSTS` (Vercel server-only) | Novo exemplo vazio | Não observado | Proxy de imagens, hosts exatos R2 | Não | Configurar por ambiente | Antes de validar imagens da preview |

Outros nomes observados no Render: `Email__Provider/From/FromName/Template__Reset/Template__Verify`,
`ImageGeneratorSettings__ImageCost/JobsCollection/MongoDatabase/RunPodApiUrl/WebhookUrl`,
`Jwt__ExpiresMinutes`, `R2Settings__BucketName/PublicUrl/ServiceUrl`,
`ReplicateSettings__ModelUrl/WebhookUrl`, `Stripe__CancelUrl/PortalReturnUrl/SuccessUrl`,
`Logging__LogLevel__Default/Microsoft.AspNetCore`. `R2Settings__BucketNameVideos` não
foi observado: verificar o fallback rastreado e criar bucket isolado em staging.

Nenhum valor de env foi removido. O código agora prefere a chave Resend canônica;
há dois nomes no Render que podem divergir. Conferir em privado e retirar o alias
somente depois de provar o envio com a configuração canônica.

## 3. Dependências

```text
Vercel Preview (API_URL e MEDIA_ALLOWED_HOSTS de staging)
  → Render API staging (uma instância, HTTPS, CORS exato)
     → Atlas staging: usuários, sessões, catálogo, jobs, eventos
     → R2 staging: imagens, vídeos e avatares sintéticos
     → Google cliente OAuth staging → cookie refresh → JWT em memória
     → Resend staging → caixas controladas
     → Stripe sandbox/test → callback assinado → crédito atômico
     → Replicate/Gemini/Veo isolados → output validado → R2

Produção: Phase 0A #54/#85 → revisão Phase 0B.1 → staging validado
  → autorização explícita de rollout/rotação → Phase 0B.2
```

## 4. Código implementado

- Secrets Mongo/RunPod removidos dos dois appsettings; exemplo sem credenciais,
  documentação de envs e validação de startup com mensagens só de nomes.
- OAuth começa na API: state/nonce/verifier aleatórios de 256 bits, cookie
  HttpOnly/Secure/Lax, expiração dez minutos, consumo único vinculado ao navegador.
  PKCE S256 enviado e verifier na troca do code. Audience/assinatura/issuer/expiry
  validados pela biblioteca Google; nonce conferido depois. Redirect sem JWT,
  sessão via cookie refresh. E-mail já existente requer fluxo futuro de linking.
  Nenhuma alteração em Google Console. State store em memória exige uma instância;
  restart falha fechado. Testar aceitação de PKCE no cliente real de staging.
- Replicate: HMAC SHA-256 nos bytes originais, headers oficiais, comparação constante,
  janela de ±5 minutos e limites de body. String/array de output suportados.
- Callbacks: lookup **só** por provider job id, provider interno e dono existente,
  jobs pré-cobrados, transição ativa → completed, lease Mongo atômico, conclusão
  parcial por lease, replay concluído sem novo upload/debito. Jobs sem vínculo
  novo são recusados; não fazer backfill inferido a partir do payload.
- RunPod preservado, desligado por padrão. Infraestrutura de assinatura customizada
  existe, mas exige gateway/worker confiável. Nenhuma afirmação de suporte nativo.
- SSRF: API valida HTTPS/443, host do provider/R2, sem userinfo/IP literal;
  socket usa DNS público verificado e endereço fixado, sem proxy/auto redirect.
  Download autenticado da imagem e outputs de callbacks até 20 MiB; Veo até 100 MiB
  e deadline de dois minutos. Chave Google só no host Google, nunca em storage.
  Endpoint Replicate limitado à API oficial; chave Gemini saiu da query string.
- Frontend: proxy arbitrário corrigido, allowlist por ambiente, DNS público fixado,
  nenhum redirect, MIME de imagem, 20 MiB, deadline 30 s e `nosniff`.
- Avatar até 5 MiB: limite HTTP/multipart e real stream, assinaturas PNG/JPEG/WebP,
  MIME/extensão derivados e chave UUID. SVG/HTML/executável recusados. Isso valida
  magic bytes, não é um decoder completo ou antivírus; re-encode futuro pode ampliar.
- Refresh: novos tokens aleatórios e somente SHA-256 no banco, consumo atômico,
  compatibilidade de leitura com plaintext legado sem migração de dados nesta fase.
  Timestamp de criação para corte operacional opcional, inativo por padrão. TTL
  alinhado à cookie config, HttpOnly forçado, logout com Path/Domain corretos.
  Frontend compartilha refresh concorrente e bloqueia resposta tardia após logout.
- Todos os `UserRepository.UpdateAsync` removidos: email/password/OAuth/customer/
  billing usam `$set` parcial; crédito concorrente não é substituído. Stripe mantém
  verificação original e créditos de checkout/invoice usam `$inc` + ledger no mesmo
  documento, evitando duplicação por retry ou eventos equivalentes de invoice.
- Removida criação automática de índice no construtor de email tokens.
- Removida apenas a referência ASP.NET 2.3.0; target permanece `net8.0`.
- Erro interno genérico não retorna mensagem de exceção com possíveis detalhes sensíveis.

A autenticação Replicate segue o contrato publicado em
[Verify webhooks](https://replicate.com/docs/topics/webhooks/verify-webhook).
O segredo de assinatura deve ser obtido pela API oficial da conta e armazenado no
ambiente, nunca no PR. Não foi consultado nesta fase. A documentação RunPod
[Send API requests](https://docs.runpod.io/serverless/endpoints/send-requests)
descreve URL de callback e retries, mas não estabelece assinatura nativa nessa
página: a opção customizada precisa ser validada separadamente. Google recomenda
proteções de state/nonce no fluxo [OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect).

## 5. Mongo: credencial e escopo

Não criar/modificar usuário de banco agora. Primeiro confirmar em privado qual
principal a URI de runtime usa e mapear logs/consumidores sem registrar senha/URI.
O único principal listado tem privilégio administrativo amplo, superior ao necessário.

Collections requeridas pelo código, no banco de aplicação:
`Users`, `RefreshTokens`, `email_tokens`, `stripe_events`, `image_jobs`, `video_jobs`,
`image_models`, `image_model_versions`, `image_model_providers`, `image_model_presets`,
`video_models`, `video_model_versions`, `video_model_providers`, `video_model_presets`.
As primeiras treze existem no snapshot; `video_model_presets` não foi listado.
`JobsCollection/VideoJobsCollection` configuráveis devem ser conferidos no runtime.

Preferir role Atlas customizada com `find/insert/update/remove` nas collections
necessárias, restrita ao cluster de aplicação; catálogo necessita escrita somente
por rotas AdminOnly, mas a credencial da API executa essas operações. Sem `atlasAdmin`,
`dbAdmin`, administração de usuários, dropDatabase, dropCollection ou createIndex.
Provisionamento de collections/índices fica em credencial de migração separada,
temporária e aprovada. Alternativa inicial `readWrite` no único banco é mais ampla
e exige aprovação da diferença de escopo. Atlas documenta roles e restrições em
[Configure Database Users](https://www.mongodb.com/docs/atlas/security-add-mongodb-users/).

Sequência: criar novo principal com segredo forte → conceder escopo mínimo →
testar em ambiente isolado → substituir somente a URI no Render autorizado →
reiniciar com release aprovado → provar leitura e escrita sintética controlada,
auth/jobs/callbacks, sem mutações destrutivas → observar erros de auth e conexões
→ confirmar todos os consumidores migrados → revogar legado explicitamente.
Em falha antes da revogação, restaurar URI anterior apenas como exceção temporária
aprovada; manter novo usuário e investigar. Depois da revogação, emitir outra
credencial restrita válida; não reativar automaticamente o segredo comprometido.

## 6. JWT e sessões

Gerar nova chave de pelo menos 256 bits em ferramenta segura; não imprimir.
Issuer/Audience devem permanecer coerentes com a aplicação. A troca invalida
access JWTs antigos, mas **não revoga refresh tokens por si só**.

Na operação aprovada: instalar código compatível → preparar nova chave e timestamp
UTC de corte → aplicar `Jwt__Secret` e `Auth__RefreshTokensValidAfter` juntos →
reiniciar → comprovar rejeição de access token antigo e refresh anterior ao corte
→ login novo/Google + refresh/logout funcionam. Legacy sem CreatedAt falha no
corte; sessões criadas depois sobrevivem. Usuários autenticam novamente. Não
usar só mudança de JWT para concluir que todas as sessões foram revogadas.
O cutoff não foi definido, e nenhuma sessão real foi destruída nesta fase.

Hash migration: na transição autorizada, tokens antigos são aceitos e consumidos,
substituídos por hashes novos. Não há bulk rewrite/TTL executados. Depois da janela
de validade aprovada, retirar a compatibilidade de plaintext e remover resíduos
apenas por migração aprovada. Family/reuse detection ainda não implementada:
consumo único impede duas rotações válidas, porém não diferencia roubo de uma
corrida de clientes. Planejar family id e tombstone de hash usado, expiração,
revogação da família, e auditar falsos positivos antes de ativar.

## 7. Callbacks, jobs legados e limites restantes

Snapshot Atlas (contagens, sem prompts/PII/URLs): imagens têm 11 `Completed`,
19 `completed`, 235 `COMPLETED`, 34 `in_queue` e 39 `starting`; vídeos têm 2
`Failed`, 1 `Completed` e 12 `Created`. Estados heterogêneos não provam jobs ainda
ativos no provider. Nenhum documento foi alterado. O catálogo tem Gemini e Veo,
e um provider de imagem sem providerType explícito (o código usa default Replicate).
Isso não prova ausência de callbacks RunPod ou saúde dos providers.

Antes de rollout: consultar estado nos providers sem publicar IDs/outputs,
classificar jobs por vínculo interno confirmado, conciliar créditos previamente
debitados, planejar migração de status/provider/owner sob autorização específica.
Drain de callbacks antigos ou rota de compatibilidade autenticada precisa existir
antes de desligar o receiver legado em produção. Não colocar `callbackProvider`
nos jobs a partir de dados não autenticados. Unknown/failed/unpaid jobs falham
fechado no novo código. Callbacks recebidos antes de persistir providerId precisam
retry; garantir política de retry/reconciliação em staging. Lease dura três minutos;
crash pode gerar reupload da mesma chave R2 depois do lease, sem novo débito.

Stripe: assinatura mantida. Ledger de crédito protege concorrência por checkout
e invoice; índice único do event journal ainda é planejado. Eventos de subscription
fora de ordem podem atualizar Plan/Status antigos; projetar reconciliação com a
subscription atual e ordenação por evento antes de generalizar billing. Ledger
no usuário precisa de retenção/arquivamento para não crescer indefinidamente.
Não assumir exatamente-once global ou transação multicollection.

Vídeo ainda usa `Task.Run` em memória e não sobrevive a restart com retomada
durável. Mover polling/reconciliação para worker é etapa futura; fazer drain antes
de restart/rotação. Não executar geração paga para provar saúde sem orçamento
autorizado; testar transporte/assinaturas com fixtures até essa autorização.

Redirects de mídia falham fechado. Se provider legítimo redirecionar, capturar
somente hosts/status em staging, aprovar allowlist explícita e revalidar cada hop
sem credenciais entre hosts. Não afrouxar DNS ou aceitar qualquer HTTPS. Novos
modelos/hosts/endpoints também precisam gate de staging.

## 8. Índices — plano, não migração executada

Consultas de duplicidade retornaram somente totais, não dados pessoais:
Email case-insensitive: **um grupo duplicado, um documento excedente**.
Username case-insensitive, GoogleId não vazio, EventId Stripe, slugs de imagem/vídeo,
pares modelId/versionTag e providerJobId não vazio de imagem/vídeo: zero grupos
duplicados encontrados no snapshot. Reexecutar imediatamente antes da migração;
dados ausentes/null e tipos inválidos precisam preflight próprio.

Todos os índices inspecionados são `_id_`, exceto `email_tokens`, que também tem
`UserId/Purpose/ExpiresAt` composto, sem TTL. Nenhum índice novo criado.

| Collection | Índice proposto | Pré-condição / rollback |
| --- | --- | --- |
| Users | Email e Username únicos separados, collation case-insensitive coerente com normalização | Reconciliar duplicidade e campos ausentes antes; não apagar/mesclar usuário automaticamente; remover índice pelo nome não desfaz normalização |
| Users | GoogleId único parcial para strings não vazias | Preferir partial a sparse simples: null explícito pode colidir; verificar duplicados e tipo |
| stripe_events | EventId único | Preflight e tratamento de duplicate key; ledger já impede duplicação de crédito; drop por nome só em rollback aprovado |
| image_jobs / video_jobs | providerJobId parcial único; jobId lookup; userId + createdAt desc; imagem isPublic + createdAt desc | Confirmar escopo de ID por provider, tipos e legados; se ID só único por provider usar par; não impor índice único global sem confirmação |
| RefreshTokens | TokenHash único parcial; Token legado parcial temporário; UserId | Recontar hash/raw sem retornar valores; comparar formato e campos ausentes; drop não reverte migração hash |
| RefreshTokens / email_tokens | ExpiresAt TTL, expireAfterSeconds 0 | Confirmar datas UTC e política de retenção; TTL apaga expirados e essa remoção não é reversível por drop; backup/restore e aprovação específicos |
| image_models / video_models | slug único | Verificar strings/collation e referências antes |
| image_model_versions / video_model_versions | modelId + versionTag único | Conferir referências e normalização da tag; drop pelo nome |
| presets | modelId/versionId conforme consultas e nome/slug se introduzido | video_model_presets ausente; provisionar só em migração aprovada |

Procedimento futuro: snapshot/backup autorizado → read-only preflight → reconciliação
aprovada de duplicados → migration manifest com nomes/keys/options e down script
→ aplicar primeiro staging → explain/latência e testes de conflito → janela de
produção autorizada. Planejar impacto de build de índice em CPU/disco Free.
Rollback remove apenas os índices recém-criados; não restaurar permissões amplas,
tokens apagados ou usuários alterados sem plano próprio. TTL fica em passo separado.

## 9. Staging com custo mínimo e isolamento real

**Não criado nesta fase.** Caminho recomendado: novo projeto Atlas com Free M0,
novo serviço Render e preview Vercel com branch-scoped envs; somente dados sintéticos.
Atlas permite um Free cluster por projeto, portanto separar projeto evita dividir
o cluster de produção. [Atlas Free deployment](https://www.mongodb.com/docs/atlas/tutorial/deploy-free-tier-cluster/).

1. Criar projeto Atlas `Imagino-Staging`, cluster Free M0 em região compatível.
   Banco staging, credencial distinta restrita, IP access list só egress autorizado
   do Render. Não abrir `0.0.0.0/0` por conveniência. Criar collections necessárias
   com ferramenta de migração aprovada. Seed de catálogo mínimo sem tokens, dados
   pessoais, URLs privadas ou histórico de produção.
2. Criar Web Service Render **separado**, nome `imagino-api-staging`, Dockerfile da
   API, branch `fix/revival-operational-security`, auto deploy desligado, uma
   instância, health `/health`. Iniciar Free só para testes curtos se houver quota;
   para validar callbacks/OAuth sem suspensão usar o menor plano sempre ativo.
   Referência atual é compute 512 MiB por US$7/mês; confirmar seletor/preço antes
   de contratar. [Render pricing](https://render.com/pricing).
3. Não consumir inadvertidamente quota Free de produção: as 750 horas são
   compartilhadas pelo workspace; Free dorme após quinze minutos e callbacks
   podem perder a janela. Preferir staging pago mínimo com teto aprovado ou
   workspace separado de testes, conforme acesso disponível. Nenhum gasto foi
   contratado. [Render Free limitations](https://render.com/docs/free).
4. Preencher somente secrets de staging: Mongo, JWT independente, R2 bucket/token
   separado (inclusive vídeos), Google OAuth client de testes separado, Resend
   chave/caixas de teste, Stripe sandbox/test keys + webhook test + preços test.
   Não copiar valores secretos de produção para Preview. Não fazer cobrança live.
5. Definir URL estável da preview de branch na Vercel. Em Settings → Environment
   Variables, escopar `NEXT_PUBLIC_API_URL` para Preview + esta branch, apontando à
   API staging; `MEDIA_ALLOWED_HOSTS` para o host R2 staging. Preservar Production.
   Conferir variáveis compartilhadas/herdadas. A mudança exige rebuild da preview.
   [Vercel environment scoping](https://vercel.com/docs/environment-variables).
6. API staging: Frontend BaseUrl e CORS **exatos** para essa preview, HTTPS;
   refresh cookie host-only, Secure/HttpOnly, SameSite None quando cross-site,
   ExpiresDays coerente. Google RedirectUri é o callback da API staging, registrado
   somente no novo client staging. Não mexer no cliente Google de produção.
7. Se preview protection bloquear navegação, autorizar apenas testers pelo recurso
   suportado; não desproteger globalmente o projeto. OAuth volta à preview estável.
   Safari/Chrome que bloqueiam cookies de terceiros podem impedir refresh entre
   `.vercel.app` e `.onrender.com`; testar ambos. Sem mudar DNS nesta fase, usar
   ambiente same-site já disponível ou planejar BFF autorizado em etapa futura.
8. Providers IA: começar mocks/fixtures. Replicate/Gemini/Veo não devem ser tratados
   como possuidores de sandbox grátis. Criar credenciais restritas/separadas,
   quotas e limite de gastos; geração real só depois de orçamento explícito.
   RunPod não criar endpoint GPU sem confirmar necessidade/custo. Webhook signing
   secret deve corresponder à conta da chave do ambiente.
9. Rodar cadastro/e-mail/login/reset, OAuth state errado/replay/nonce, refresh e
   logout, profile/avatar, ownership/admin, SSRF, callbacks concorrentes, jobs e
   billing com eventos Stripe test. Mockar falhas e reinício/polling. Confirmar que
   nenhum host/credential/database de produção foi usado. Limitar mídia/testers
   e observabilidade para cabeçalhos/token/body não irem a logs.
10. Exercitar rotação e rollback em staging, inclusive plaintext legado sintético,
    hash/cutoff, mudança da URI, signing secrets e jobs pendentes. Registrar só
    status, IDs de release e métricas, sem secrets/PII. Guardar evidência funcional
    antes de qualquer autorização de produção.

Custo base possível de testes curtos: Vercel Hobby + Atlas M0 + Render Free,
respeitando elegibilidade/quota. Validação contínua recomendada adiciona compute
Render mínimo (~US$7/mês de referência), mais storage/tráfego/IA/e-mail conforme uso.
Não prometer custo zero de IA, Stripe live ou storage. Budget e aprovação precedem
criação de recurso cobrado. Os planos comerciais/quotas devem ser reconfirmados
na criação, pois podem mudar.

## 10. Ordem exata para Phase 0B.2

1. Revisar PRs 0A e 0B.1 e obter autorização específica para staging, gastos, rollout,
   mudanças de env/secrets, sessões, migrations e revogações. PR aberta não autoriza
   merge/deploy. Confirmar dono do incidente e janela de manutenção.
2. Exportar backup/snapshot seguro e inventário privado de configurações válidas.
   Identificar consumidores da credencial Atlas e chaves históricas. Conferir
   backups disponíveis no plano Free; não presumir backup automático.
3. Construir staging isolado pelos passos acima; gerar secrets próprios, validar
   código, integrações e rollback. Corrigir blockers de cookies/PKCE/hosts/status.
4. Classificar/drain jobs legados e vídeo; preparar migração autenticada de vínculos
   somente quando necessária e aprovada. Não deployar receiver que rejeita jobs
   ativos sem alternativa. Revisar aliases Resend, bucket de vídeos, admin IDs.
5. Preparar release e artefato de rollback **compatível com o schema novo**, com
   tolerância a campos extra, leitura hash/cutoff e sem restauração de secrets.
   Parar auto deploy de master por operação aprovada antes de qualquer merge.
6. Criar principal Mongo restrito, testar conexão/permissões privadas, aplicar URI
   nova na operação autorizada. Validar release/callbacks sem revogar antigo ainda.
7. Provisionar signing secret Replicate e validar conta/key correspondentes; migrar
   as chaves comprometidas Replicate e Google conforme janelas oficiais. RunPod:
   se necessário migrar chave/gateway; se inativo comprovado, revogação/remoção
   controlada separada. Não inferir inatividade de arquivos antigos.
8. Rollout API 0A+0B.1 aprovado com CORS/cookies/outputs/config corretos e frontend
   compatível. Validar saúde e fluxos sintéticos autorizados; deploys coordenados
   evitando frontend novo com API antiga. Nenhuma DNS necessária para esse passo
   se browsers suportarem cookies; se não, resolver arquitetura antes.
9. Rotacionar JWT com cutoff de refresh UTC numa mudança coordenada; comunicar
   reautenticação por canal autorizado. Validar tokens antigos negados e sessões
   novas funcionando. Nunca reativar chave JWT comprometida no rollback.
10. Migrar R2/Resend/Stripe e demais provider keys separadamente, uma integração por
    vez: credencial nova → configuração → teste autorizado → observação → revogação
    anterior. Stripe API key e webhook secret são operações distintas coordenadas.
11. Aplicar índices aprovados depois de preflight atualizado e reconciliação de
    e-mails; TTL em passo separado com retenção/backups. Não usar essa etapa para
    apagar dados sem autorização.
12. Só após confirmação de consumidores, callbacks e janela estável, revogar cada
    credencial legada remanescente. Reescrita Git/remoção de históricos, forks,
    caches e artefatos é operação futura específica; retirar do HEAD não desfaz
    comprometimento histórico. Registrar evidências sem valores e reabilitar auto
    deploy somente após decisão operacional aprovada.

## 11. Rollback

- Primeiro parar promoções e novas gerações/cobranças por controle autorizado;
  preservar jobs e logs sanitizados. Identificar componente que falhou.
- Antes de revogar Mongo/provider anterior, sobreposição permite retorno temporário
  da configuração aprovada. Se a antiga é comprometida, retorno é exceção de
  incidente limitada; preferir nova credencial válida com release compatível.
- Não executar rollback cego ao `master` antigo depois de gravar `TokenHash`,
  `CreatedAt`, `BillingCreditEvents` e campos de callback. Modelos antigos podem
  rejeitar campos desconhecidos, e refresh antigo não lê hashes. Preparar e testar
  artefato compatível antes do rollout; rollback de código não deve apagar hashes
  ou reconverter tokens em plaintext.
- Manter validação de assinatura, ownership, CORS exato e hash/cutoff no rollback.
  Nunca voltar a receiver anônimo que aceita callbacks arbitrários.
- Chave JWT/cutoff já ativados permanecem; se a chave nova falhou, usar outra chave
  forte válida e reautenticação. Não restaurar chave comprometida ou sessões cortadas.
- Frontend pode voltar à build compatível com `/me` e OAuth sem JWT em URL;
  restabelecer envs por ambiente, nunca usar produção como “fallback” de staging.
- Índices: down script nomeado só remove novos índices. TTL/documentos apagados
  e reconciliação de usuários exigem restore separado; não são rollback automático.
- Após revogação de credencial, ela não é opção de rollback. Emitir outra credencial
  restrita e repetir validação. Critério de continuidade: auth/refresh/logout,
  webhook/idempotência, Mongo, storage e billing testados sem aumento de erros.

## 12. Verificação local e ações pendentes

`dotnet restore`, `dotnet build` e `dotnet test` executados; target continua net8.0,
grafo sem Microsoft.AspNetCore 2.3.0. Testes incluem pipeline HTTP com dependências
mockadas, assinaturas, ownership/replay, nonce/PKCE, avatar, URL/IP/DNS policy,
download/redirect, updates Mongo renderizados, plaintext/hash/cutoff e concorrência
do frontend. Não comprovam TLS/DNS real, Mongo concorrente real, console Google,
provider, storage ou billing externos. Esses gates ficam em staging.

Resultado após ajustes finais: **82/82 testes API**, zero falhas/ignorados; build com zero erros
e 30 warnings de nulabilidade no código legado/testes. Restore concluído com
acesso ao cache/configuração NuGet local. A referência removida não aparece no
grafo restaurado.

Frontend: `npm test` (**7/7**) e `npx tsc --noEmit` (zero erros). O teste aritmético antigo continua;
novos testes exercitam refresh compartilhado/logout e SSRF do proxy.

Pendentes operacionais: criação de staging, valores/validade/permissões de env,
matriz de consumidores, aprovação de gastos e operações, reconciliação de e-mail/
jobs legados, teste browser/provider/Google/R2/Resend/Stripe, índices/TTL,
rotacionar/revogar secrets e promover releases. Não confundir preparação concluída
com contenção operacional efetiva em produção.
