# Configuração segura

Os arquivos rastreados não contêm credenciais funcionais. `appsettings.example.json`
lista os nomes; copie apenas para um arquivo local ignorado ou use User Secrets.
No Render configure variáveis individuais; `__` representa `:` do .NET.
Nunca cole valores em Git, logs, PRs ou no relatório operacional.

## Obrigatórias no startup

- `ImageGeneratorSettings__MongoConnection`, `ImageGeneratorSettings__MongoDatabase`
- `Jwt__Secret` (pelo menos 32 bytes; gere 256 bits aleatórios), `Jwt__Issuer`, `Jwt__Audience`
- `Frontend__BaseUrl` (HTTPS confiável em produção)
- `RefreshTokenCookie__Secure`, `RefreshTokenCookie__HttpOnly`,
  `RefreshTokenCookie__SameSite`, `RefreshTokenCookie__ExpiresDays`

Defaults seguros de cookie podem permanecer no JSON. Configure `Domain` somente se
necessário e permitido pelo host da API; host-only é preferível. JWT fica em memória
no frontend. Restrinja `Cors__AllowedOrigins__0`, `__1`, etc. a origens exatas;
wildcards não são aceitos com cookies. Remova entradas herdadas com curingas.

## Integrações

| Integração | Nomes |
| --- | --- |
| Google | `Google__ClientId`, `Google__ClientSecret`, `Google__RedirectUri` |
| Replicate | `ReplicateSettings__ApiKey`, `ReplicateSettings__WebhookUrl`, `Webhooks__ReplicateSigningSecret` |
| Gemini | `GeminiSettings__ApiKey` |
| Veo | `VeoSettings__ApiKey` |
| R2 | `R2Settings__AccessKeyId`, `R2Settings__SecretAccessKey`, `R2Settings__ServiceUrl`, `R2Settings__BucketName`, `R2Settings__BucketNameVideos`, `R2Settings__PublicUrl` |
| Stripe | `Stripe__ApiKey`, `Stripe__WebhookSecret`, `Stripe__PricePro`, `Stripe__PriceUltra`, `Stripe__SuccessUrl`, `Stripe__CancelUrl`, `Stripe__PortalReturnUrl` |
| Resend | `Resend__ApiKey`, `Email__Provider`, `Email__From`, `Email__FromName`, `Email__Template__Verify`, `Email__Template__Reset` |
| Admin | `Admin__UserIds__0`, `__1`, etc.; vazio nega acesso |
| RunPod legado | `ImageGeneratorSettings__RunPodApiKey`, `ImageGeneratorSettings__RunPodApiUrl`, `ImageGeneratorSettings__WebhookUrl`; `Webhooks__RunPodEnabled` desligado por padrão, `Webhooks__RunPodSigningSecret` somente para gateway confiável |

Google, Stripe e R2 parcialmente configurados são rejeitados no startup. Replicate
exige `Webhooks__ReplicateSigningSecret` no startup quando `ApiKey` ou `WebhookUrl`
estiver preenchido; deixe ambos vazios para desabilitar essa integração. Antes de
testar geração/storage/e-mail, forneça as credenciais da integração correspondente.
O callback Replicate retorna 503 sem signing secret e 401 para assinatura inválida.
O RunPod exige gateway/worker com o mesmo protocolo de assinatura; não é uma
assinatura nativa confirmada do provider. Não habilite por receber callbacks antigos.

Avatares permanecem limitados a 5 MiB. Outputs gerados usam validação própria:
20 MiB, magic bytes PNG/JPEG/WebP e extensão/MIME derivados no servidor. AVIF
não foi habilitado: não há necessidade/suporte confirmado nesta fase. O download
Replicate tem o mesmo limite real de 20 MiB. RunPod admite base64 de até 20 MiB
decodificados, com envelope JSON limitado ao tamanho base64 máximo + 64 KiB;
continua desabilitado por padrão e exige gateway/worker com assinatura validada.

O OAuth começa em `GET /api/auth/google/login`. Configure o callback da API no
cliente Google dedicado ao ambiente; o frontend não precisa de ClientId/RedirectUri.
Transações OAuth duram dez minutos, são de uso único, vinculadas ao navegador e
ficam em memória de uma instância. Reinício invalida transações pendentes. Antes
de escalar, substituir por store compartilhado com consumo atômico.

## Deploy e migrações

Consulte `SECURITY_OPERATIONAL_PLAN.md`. Nenhum construtor cria índices.
As PRs da Phase 0B.1 são propostas de código, sem merge/deploy/rotação.
`Auth__RefreshTokensValidAfter` é um corte UTC opcional para sessões na rotação
aprovada de JWT. Ausente, preserva a compatibilidade com sessões legadas.
Os nomes de env atuais foram confirmados; isso não comprova valores válidos ou
permissões. Validação de staging e plano de rollback compatível são obrigatórios.
