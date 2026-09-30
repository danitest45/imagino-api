# Imagino Revival — Phase 0A: Security Containment

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
