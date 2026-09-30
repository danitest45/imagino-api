# Imagino Revival — Phase 0A: Security Containment

## Staging de mídia — blocker pré-produção (30/09/2026)

- r2.dev público está autorizado somente para objetos sintéticos de `imagino-images-staging`. Não é solução de mídia privada para produção.
- **Blocker de promoção:** decidir e implementar signed URLs/presigned R2 com expiração curta e autorização antes da emissão, ou entrega autenticada. Manter objetos privados sem URL pública permanente, para que acesso direto ao objeto não contorne ownership da API. Rever também cache, revogação, URLs de vídeo e proxy público antes de promoção.
- Fixtures Replicate podem usar apenas o host r2.dev exato configurado em staging mediante `Webhooks:StagingFixturesEnabled=true`. Configuração exige banco `imagino_staging`, os dois buckets staging, PublicUrl correspondente e nenhuma API key Replicate. Desativada por padrão; configuração fora desse isolamento falha no startup. Download continua com HTTPS, DNS público fixado, sem redirects e limite de 20 MiB; GeneratedImageValidator continua PNG/JPEG/WebP com magic bytes.
- Nenhuma prediction real, segredo de produção, bucket de produção ou recurso Little Haven faz parte desse teste.

Data: 29 de setembro de 2026. Repositório: `danitest45/imagino-api`. Branch: `fix/revival-security-containment`. Base da PR: `master`.

## Escopo e decisões

- Cadastro público aceita somente email, senha, username e telefone. Campos desconhecidos, inclusive econômicos, recebem HTTP 400. `UserService.CreateAsync` inicializa todo usuário novo com `Subscription=Free`, `Credits=0`, `Plan=null`, `SubscriptionStatus=null` e IDs Stripe nulos. A política explícita de créditos iniciais nesta fase é **zero**. O cadastro Google já usava Free/0 e não foi alterado.
- A autorização administrativa usa uma policy `AdminOnly` baseada no `sub` de um JWT validado pela API e numa lista de IDs fornecida apenas pela configuração do servidor (`Admin:UserIds`). Sem lista configurada, ninguém é admin. Não há atribuição automática de privilégio. Provisionamento posterior: após verificar a identidade e autorização operacional, configurar no ambiente da API `Admin__UserIds__0=<ID do usuário>` (e índices adicionais para outros IDs) e reiniciar o serviço em mudança aprovada. Nenhum usuário do Atlas foi modificado nesta fase.
- `GET/PUT /api/users/me` derivam o ID do JWT. Rotas antigas por ID permanecem para compatibilidade com o frontend, mas só aceitam o próprio usuário; acesso a outro ID retorna 404. A mesma verificação foi aplicada a exclusão e upload de foto. O DTO de perfil não contém campos econômicos. `POST /api/users` e `POST /api/users/{id}/credits` exigem `AdminOnly`; incremento de crédito requer valor positivo.
- Consultas por ID a image jobs e video jobs exigem titularidade; download de imagem exige autenticação e titularidade. A API retorna 404 para job inexistente ou de outro usuário.
- A rota pública `/api/image/jobs/latest` entrega apenas jobs marcados `IsPublic=true`. O novo campo é falso por padrão; documentos antigos do Mongo sem o campo continuam privados. Nenhuma rota pública de publicação foi adicionada. Isso elimina a exposição de prompts e URLs privados, mas a galeria pública pode ficar vazia até a definição de um fluxo explícito de publicação.
- Os três testes antigos de `ImageJobCreationService` tinham snapshot prematuro do fixture. O fixture foi corrigido sem mudar o serviço de produção.

## Hardening final antes do merge

- `UserProfileUpdateDto` agora contém **somente** `Username` e `PhoneNumber`. `PUT /api/users/me` e a rota legada por ID ignoram email, senha, avatar e campos econômicos enviados no JSON. Troca de email com reverificação e troca de senha autenticada ficam para fluxos próprios futuros.
- A edição de perfil e o upload de avatar gravam apenas seus campos por operações parciais no Mongo. Isso evita que essas rotas substituam o documento completo e revertam email, senha, avatar ou créditos alterados por outros fluxos.
- `POST /api/users/me/profile-image` recebe multipart com JWT e deriva o ID do token. A rota antiga por ID continua exigindo titularidade. O frontend envia o arquivo por `FormData` usando `fetchWithAuth`; não transforma mais a imagem em Data URL nem envia `profileImageUrl` no PUT.
- Navbar e perfil do frontend usam `GET /api/users/me`. O perfil usa `PUT /api/users/me` com payload explícito `{ username, phoneNumber }`; o email aparece somente para leitura.
- `GET /api/video/providers` agora usa um DTO público sem `Config`. As ações administrativas POST/PUT continuam protegidas por `AdminOnly` e mantêm o contrato de configuração.

## Inventário das mutações administrativas

| Área | Rotas de mutação | Proteção |
| --- | --- | --- |
| Catálogo de imagens | POST/PUT/DELETE de providers, models, versions e presets; POST de versão padrão | `AdminOnly` no controller inteiro |
| Catálogo de vídeo | POST/PUT/DELETE de providers, models, versions e presets; POST de versão padrão | `AdminOnly` em cada ação mutável |
| Usuários | `POST /api/users` | `AdminOnly`; criação ainda força Free/0 |
| Créditos manuais | `POST /api/users/{id}/credits` | `AdminOnly`; valor positivo |
| Perfil e exclusão | PUT/DELETE de usuário; upload de foto | Somente o próprio `sub` |
| Assinatura/plano | Nenhuma rota de edição direta para usuário comum | DTO público sem esses campos; billing/Stripe continuam em fluxos internos |

As rotas de leitura do catálogo continuam públicas conforme o comportamento anterior. O checkout autenticado recebe um nome de plano, mas não altera diretamente o estado econômico do usuário; o serviço de billing e o webhook Stripe assinado continuam responsáveis pela atualização.

## Testes e verificação

`dotnet build Imagino.Api.sln --no-restore --verbosity quiet -clp:ErrorsOnly`: **sucesso, 0 erros**.

`dotnet test Imagino.Api.sln --verbosity quiet -clp:ErrorsOnly` (restore padrão): **27 aprovados, 0 falhas, 0 ignorados**.

Frontend: `npm test` **1 aprovado, 0 falhas**; `npx tsc --noEmit` **sem erros**.

Cobertura dos cenários pedidos:

| Caso | Evidência |
| --- | --- |
| A–B: Credits/Subscription no registro | `Register_RejectsClientControlledEconomicFields`, dois casos HTTP 400; criação Free/0 também testada em `UserServiceTests` |
| C: usuário comum em rota admin | `NormalUser_CannotAccessImageAdminRouteOrMutateVideoCatalog`, HTTP 403; auditoria por reflexão em `EveryCatalogMutationRequiresAdminPolicy` |
| D: crédito direto | `NormalUser_CannotIncrementCreditsOrCreateUser`, HTTP 403 e nenhuma chamada ao serviço |
| E: usuário A edita B | `UserCannotReadOrUpdateOtherUser`, GET/PUT/DELETE 404 e nenhuma chamada mutável |
| F–G: job privado de B | `UserCannotReadOtherUsersImageOrVideoJob`, inclusive details/download; `PublicGalleryDoesNotExposePrivateJobs` |
| H: proprietário consulta job | `OwnerCanReadOwnImageAndVideoJobsAndProfile`, HTTP 200, inclusive detalhes e `/users/me` |
| I: criação legítima de job | Três testes de `ImageJobCreationService` reparados e aprovados |
| J: crédito por camada confiável | `UserServiceTests.IncrementCreditsAsync_CallsRepository` e `ConfiguredAdminCanIncrementCredits`; o webhook Stripe segue usando a camada interna sem alteração |
| Hardening de perfil | DTO limitado a duas propriedades; PUT com campos econômicos, email, senha e avatar extras; teste do serviço preserva esses campos e verifica gravação parcial |
| Avatar | HTTP 401 anônimo, 404 para outro usuário e 200 em `/me/profile-image`; teste do serviço verifica gravação parcial |
| Provider de vídeo | GET público e autenticado sem `Config`, testado com valor marcador que não aparece na resposta |

Os testes HTTP usam `WebApplicationFactory`, JWTs de teste e repositórios simulados; não acessam Atlas nem provedores. Login, Google OAuth, refresh, checkout e webhook Stripe não foram exercitados ponta a ponta. Seus caminhos não foram alterados diretamente, mas ainda precisam de regressão funcional em ambiente de teste antes de qualquer deploy.

## Compatibilidade e limites

- O frontend agora usa `GET/PUT /api/users/me`; as rotas legadas por ID permanecem temporariamente para compatibilidade, sempre com verificação de titularidade. Campos extras no PUT são descartados pelo DTO restrito.
- A galeria pública preserva o contrato de resposta, mas pode não mostrar imagens enquanto não houver jobs publicados explicitamente.
- O download autenticado exige JWT. O helper `src/lib/download.ts` do frontend foi ajustado em branch/PR separada para usar `fetchWithAuth` e Blob; `npm test` e `npx tsc --noEmit` passaram. A implantação futura precisa coordenar as duas PRs e validar o download no navegador. O `package-lock.json` local da auditoria ficou fora do commit.
- `UserRepository.UpdateAsync` ainda substitui o documento completo em outros fluxos, como billing/OAuth. Perfil e avatar agora usam atualizações parciais; os demais fluxos exigem revisão posterior de concorrência.
- `WebhookController` de RunPod/Replicate continua sem autenticação de origem, conforme a auditoria. Autenticar callbacks, controlar URLs de saída e idempotência permanece urgente.
- Google OAuth ainda não tem proteção `state`/PKCE e expõe o JWT no redirect em query string. Não foi modificado nesta fase.
- A rota pública de galeria não possui fluxo de opt-in nesta fase. Também não há signed URLs; objetos já publicados diretamente no storage podem continuar acessíveis fora da API.

## Segredos e Phase 0B

Nova inspeção apenas dos nomes, sem copiar valores: `appsettings.json` e `appsettings.Development.json` rastreados contêm valores não vazios em `ImageGeneratorSettings.RunPodApiKey` e `ImageGeneratorSettings.MongoConnection`. A auditoria anterior encontrou ainda segredos históricos de JWT, Google, Replicate e Stripe em revisões desses arquivos. Nenhum valor foi inserido neste relatório, alterado, removido ou rotacionado; nenhum histórico Git foi reescrito. Como esses arquivos ainda são usados pela aplicação, substituí-los por placeholders agora poderia quebrar a configuração existente sem a migração operacional combinada. A Phase 0B deve preparar configuração segura, migrar valores para o gerenciador de segredos, revogar/rotacionar credenciais expostas e tratar o histórico conforme um plano aprovado.

Também para a Phase 0B: testar produção equivalente em staging, revisar índices e idempotência Stripe, autenticar webhooks de providers, revisar concorrência nas demais atualizações de usuário, criar fluxos separados de troca de email e senha, definir publicação explícita da galeria e revisar o fluxo OAuth. Nenhuma configuração de Atlas, Render, Vercel, Stripe ou Google foi modificada nesta entrega. Não houve deploy ou merge.

## Arquivos alterados

- Segurança/infra: `Security/AdminAuthorization.cs`, `Program.cs`, `Imagino.Api.csproj`.
- Cadastro/usuários: `Controllers/AuthController.cs`, `Controllers/UsersController.cs`, `DTOs/CreateUserDto.cs`, remoção de `DTOs/UpdateUserDto.cs`, novo `DTOs/UserProfileUpdateDto.cs`, `Services/IUserService.cs`, `Services/UserService.cs`, `Repository/IUserRepository.cs`, `Repository/UserRepository.cs`.
- Jobs e galeria: `Controllers/Image/ImageJobsController.cs`, `Controllers/VideoController.cs`, `Models/ImageJob.cs`, `Repository/ImageJobRepository.cs`.
- Catálogo admin: os quatro controllers em `Controllers/Admin/Image/` e `Controllers/VideoModelProvidersController.cs`, `Controllers/VideoModelsController.cs`, `Controllers/VideoModelVersionsController.cs`, `Controllers/VideoModelPresetsController.cs`.
- Testes: `Imagino.Api.Tests/Imagino.Api.Tests.csproj`, `Imagino.Api.Tests/ImageJobCreationServiceTests.cs`, `Imagino.Api.Tests/SecurityContainmentTests.cs`, `Imagino.Api.Tests/UserServiceTests.cs`.
- Este relatório: `SECURITY_CONTAINMENT_REPORT.md`.
- Frontend, em PR separada: `imagino-front/src/lib/download.ts`, `src/lib/api.ts`, `src/types/user.ts`, `src/components/Navbar.tsx`, `src/components/profile/UserInfo.tsx`.
## Continuação Phase 0B.1 — 2026-09-30

A preparação operacional está em `SECURITY_OPERATIONAL_PLAN.md`, em uma nova
branch dependente desta Phase 0A. Acrescenta OAuth state/nonce/PKCE sem JWT na URL,
assinatura/vínculo/idempotência de callbacks, SSRF também no proxy frontend,
avatar com limites/magic bytes, hash/rotação atômica de refresh, updates parciais
de usuário e ledger de crédito Stripe, configuração sanitizada e remoção da
referência ASP.NET 2.3.0. Nenhum merge/deploy/rotação ocorreu.

Gates operacionais incluem credencial Atlas administrativa, um grupo de e-mails
duplicados, jobs legados, signing secret Replicate ausente e isolamento dos envs
Preview. Tests desta fase: API 65/65; frontend 7/7; build/typecheck aprovados.

## Ajustes finais antes da Phase 0B.2 — 2026-09-30

- `GeneratedImageValidator` separado do validator de avatar: limite de 20 MiB,
  PNG/JPEG/WebP por magic bytes. O avatar permanece em 5 MiB. AVIF não foi
  habilitado por ausência de necessidade/suporte confirmado.
- `VerifiedWebhookImageService` usa o validator de geração para Replicate e
  RunPod. Replicate limita o download a 20 MiB; RunPod limita base64, bytes
  decodificados e envelope JSON assinado, sem habilitar o provider por padrão.
- Startup rejeita Replicate com API key ou webhook configurado e signing secret
  ausente. A mensagem identifica apenas o nome da configuração.
- Os redirects após login normal e OAuth no frontend agora levam a `/images`,
  cujo catálogo existente resolve o modelo disponível. As duas referências
  restantes a `/images/replicate` são links de galeria: `src/app/page.tsx`
  (Explore gallery) e `src/app/_components/ClientGallery.tsx` (Browse entire
  library). Permanecem para revisão posterior, conforme o escopo solicitado.
- Verificações locais: `dotnet build` sem erros (30 warnings de nulabilidade
  existentes); `dotnet test` 82/82; `npm test` 7/7; `npx tsc --noEmit` sem erros.
  Testes incluem PNG completo acima de 5 MiB, rejeição acima de 20 MiB/formato
  inválido, ambos os callbacks, envelope RunPod assinado e config Replicate.

PRs atuais #55/#86 atualizados por commits adicionais. Nenhum merge/deploy ou
alteração de configuração em produção; staging ainda não criado nesta etapa.
