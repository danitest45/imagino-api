# IMAGINO — auditoria de segredos no histórico

B3 = **BLOCKED**. Oito candidatos `POSSIBLY_REAL` permanecem P1 preventivos até comprovar autenticidade, consumidores e revogação. Nenhum segredo foi confirmado como real e ativo no AI staging; isso não demonstra que o legado esteja seguro. Rotação/revogação: zero.

## Escopo e método

Os dois repositórios são públicos, confirmado por metadados GitHub nesta execução. A auditoria buscou branches remotas, tags e heads de PRs, incluindo histórico alcançável e refs locais. Backend: 61 refs remotas de branches, 57 heads de PRs, 1.034 blobs textuais examinados; frontend: 95 branches, 90 heads de PRs, 958 blobs textuais. Não houve reescrita nem force-push.

O analisador mantém valores e deduplicação HMAC com chave aleatória somente em memória. Não persiste valores, prefixos, sufixos, tamanho individual ou fingerprints de credenciais. A evidência contém tipos, classificações, paths, linhas, commits/blobs e status. IDs B3 identificam registros desta execução; não são fingerprints. Revisões abaixo são exemplos comprovados de presença, não uma afirmação de primeira publicação.

Resultado: 54 grupos de candidatos/literais, sendo 28 `TEST_ONLY`, 18 `PLACEHOLDER`, oito `POSSIBLY_REAL`, zero `CONFIRMED_REAL` e zero `UNKNOWN` de classificação. Há status de uso/revogação `UNKNOWN` nos candidatos P1. Prefixo de provider não prova validade; uma chave Stripe de sandbox verdadeira também seria segredo real. Fixtures incluem webhooks de teste codificados em base64; não foram atribuídos à Stripe apenas pelo prefixo compartilhado.

## Ambiente legado confirmado pelo operador

Render **My Workspace**, `tea-d2jkdnp5pdvs73fbdmi0`; serviço **imagino-api**, `srv-d2jl7uggjchc73cnqidg`; branch `master`; aplicação `imagino-api.onrender.com`. Mongo legado: Atlas **Imagino.ai**, projeto `6873143ddf968e110c0f3e92`, cluster **imagino-cluster**, `6873179d7f1bcf36a437337a`.

Esta associação foi informada pelo operador e não é prova de uso atual de cada valor. JWT pertence ao backend/app. RunPod, Replicate e Stripe estão historicamente associados ao app legado; account/project ID, modo e status específicos continuam **UNKNOWN**. Não foi presumido consumo pelos novos Atlas Imagino-Staging, Render imagino-api-staging ou imagino-api-ai-staging.

## Inventário dos oito candidatos para revisão humana

Todos os oito permanecem `POSSIBLY_REAL`, com exposição do literal no histórico público e sem comprovação de revogação. Nenhum valor está neste documento.

| ID | Sistema / tipo | Ambiente provável | Evidência histórica representativa | Status atual / proposta |
|---|---|---|---|---|
| B3-039 | MongoDB — credencial em URI, variante A | Atlas legado Imagino.ai / imagino-cluster; usuário exato não correlacionado | `appsettings.Development.json:11`, `c6e8067a780a8e4f61a6b610e952f6159cc28f8f`; 33 ocorrências de blob/path | Host difere do cluster staging autorizado. Validade antiga desconhecida. Identificar usuário e consumidores privados; comprovar exclusão/rotação ou preparar replacement no ambiente explicitamente aprovado |
| B3-040 | RunPod — API key | imagino-api legado; conta/projeto UNKNOWN | `appsettings.Development.json:10`, `c6e8067a780a8e4f61a6b610e952f6159cc28f8f`; 49 ocorrências | Uso, validade, revogação e escopo UNKNOWN. Correlacionar key ID/account e consumidores; replacement apenas se real e aprovado |
| B3-041 | MongoDB — credencial em URI, variante B | Atlas legado; vínculo exato ao cluster/usuário ainda não comprovado | `appsettings.json:12`, `c6e8067a780a8e4f61a6b610e952f6159cc28f8f`; 14 ocorrências | Host difere do cluster staging autorizado. Mesma revisão da variante A; não assumir que revogar uma resolve ambas |
| B3-043 | JWT — literal de assinatura no app | backend/app legado | `Program.cs:153`, `c502228a7b840b09864ebd6373567754a14dcc06` | Não valida o token atual do AI staging. Uso no legado e aposentadoria UNKNOWN. Correlacionar configuração/consumidores e sessões; não trocar o signer do novo staging sem evidência de necessidade |
| B3-044 | Stripe — literal no campo API key | imagino-api legado; account/mode UNKNOWN | `appsettings.Development.json:59`, `057dceed3bfce44f046e924bed9cbd5ab01fe103` | Autenticidade/formato e uso não confirmados. Conferir privadamente se é placeholder, chave de sandbox ou chave real; nenhuma chamada Stripe autenticada foi feita |
| B3-045 | Stripe — literal no campo webhook secret | imagino-api legado; endpoint/account/mode UNKNOWN | `appsettings.Development.json:60`, `057dceed3bfce44f046e924bed9cbd5ab01fe103` | Autenticidade, endpoint consumidor e revogação UNKNOWN. Correlacionar separadamente da API key; nenhuma configuração comercial/live foi acessada |
| B3-046 | Replicate — API key | imagino-api legado; account/project UNKNOWN | `appsettings.Development.json:17`, `567f054857dd7b0c57732f2a5963409e5cd41d5d`; quatro ocorrências | Uso, validade e revogação UNKNOWN. Correlacionar key ID/account e consumidores; replacement apenas se real e aprovado |
| B3-047 | JWT — literal de assinatura em configuração | backend/app legado | `appsettings.Development.json:22`, `65f54f253c189654cdce236cebd29b1a4c735288` | Não valida o token atual do AI staging. Correlacionar com variante do Program e outros consumidores; não presumir revogação global |

## Comparações que foram possíveis

Login legítimo da conta sintética no **AI staging** retornou 200, `/api/users/me` confirmou o owner esperado e os gates de geração paga/real smoke continuaram false. A assinatura do JWT emitido pelo servidor foi comparada localmente com os candidatos de signing secret do histórico; nenhum correspondeu. Nenhum JWT foi fabricado ou enviado para personificar outra conta. A sessão de auditoria foi encerrada; o runner falha se o logout não retornar 200.

Os hosts das duas URIs históricas foram comparados com metadados do cluster **Imagino-Staging** autorizado e diferem. Não houve autenticação com essas URIs em clusters desconhecidos. O projeto staging possui um usuário com `readWrite` apenas em `imagino_staging`, limitado ao cluster staging; isso não identifica o usuário legado nem prova qual password está no Render.

O conector Render disponível não oferece leitura de valores com comparação interna e retorno apenas de igualdade; não há CLI/config/API key Render local utilizável. Não foi solicitado dump de envs. A configuração versionada atual contém campos vazios/exemplos; ela não comprova os overrides de runtime. Nenhum valor de configuração de produção/legado foi lido para este relatório.

## Matriz final

| SYSTEM | CREDENTIAL TYPE | HISTORICALLY EXPOSED | CURRENT STATUS | ROTATION REQUIRED | HUMAN ACTION | VERIFIED |
|---|---|---|---|---|---|---|
| MongoDB | Duas credenciais em URI | Sim, candidatos POSSIBLY_REAL | Hosts diferentes do staging; validade antiga UNKNOWN | Revisar; se reais e não revogados, sim após aprovação específica | Confirmar usuários/consumidores no Atlas legado e destino aprovado | Presença/host: sim; validade/revogação: não |
| JWT | Dois signers históricos | Sim, candidatos POSSIBLY_REAL | Nenhum assina o token atual AI staging; legado UNKNOWN | Não substituir o signer atual apenas por este achado; revisar legado | Identificar app/instâncias/sessões e comprovar aposentadoria | Comparação no AI staging: sim; legado: não |
| RunPod | API key | Sim, candidato POSSIBLY_REAL | Conta/projeto/atividade/revogação UNKNOWN | Condicional à confirmação e aprovação | Correlacionar key ID e todos os consumidores | Presença: sim; validade: não |
| Replicate | API key; fixtures de webhook separadas | API key candidata sim; fixtures não são segredos reais | Conta/atividade/revogação UNKNOWN | Condicional à confirmação e aprovação | Correlacionar key ID; não rotacionar fixtures | Presença/classificação de fixtures: sim; key: não |
| Stripe | API key + webhook secret candidatos | Dois literais POSSIBLY_REAL | Autenticidade/account/mode/endpoint UNKNOWN | Checklist apenas; execução exige aprovação e ambiente identificado | Conferir placeholder vs sandbox/live privadamente; revisar cada endpoint | Presença: sim; autenticidade/revogação: não |
| Google OAuth | Client secret | Apenas placeholder detectado | Sem segredo real identificado na amostra textual | Não pelo achado atual | Confirmar inventário de stores antes de produção | Classificação textual: sim; inventário live: não |
| BFL / OpenAI / Runway / Google API | API keys | Apenas fixtures/valores vazios detectados | Nenhum candidato real identificado no texto auditado | Não por fixtures | Preservar stores e geração paga off | Classificação textual: sim; validade dos stores: não |
| Resend / Cloudflare-R2 | API/storage keys | Apenas campos vazios/placeholders detectados | Nenhum candidato real identificado no texto auditado | Não por placeholders | Manter inventário privado de stores | Classificação textual: sim; inventário live: não |
| Render / Vercel | Tokens de plataforma | Nenhum segredo real identificado no texto auditado | Store/config atual não comparado por valor | Não sem exposição confirmada | Confirmar inventário sem dump de valores | Texto: sim; stores: não |
| Outros | Passwords, bearer/URL credentials, private keys | Somente fixtures detectadas | Sem candidato adicional P1 identificado | Não por fixtures | Continuar scan/revisão | Dentro do escopo textual: sim |

## Limites e condição de fechamento

41 blobs binários no backend e 387 no frontend não receberam OCR. Nenhum blob excedeu o limite textual de 16 MiB. Commits inalcançáveis/deletados, forks, caches e mudanças históricas de visibilidade não foram comprovados. O scanner é heurístico: não certifica ausência de segredos nem transforma uma key com formato plausível em credencial comprovadamente real.

B3 só poderá passar após classificar/resolver os oito candidatos sem P0/P1 restante, com prova privada de placeholder/teste, remoção de conta ou revogação, e validação de qualquer rotação separadamente autorizada. Confirmação do ambiente legado, ausência do signer antigo no novo staging e remoção de valores da branch atual não substituem essa prova.

Evidência: [manifesto sanitizado](https://github.com/danitest45/imagino-api/blob/feat/imagino-launch-readiness/docs/evidence/launch-readiness/secret-history-audit-b3.json). Procedimento: [plano de rotação](IMAGINO_SECRET_ROTATION_PLAN.md). Proteção: [higiene](IMAGINO_SECRET_HYGIENE.md). A classificação de chaves de sandbox/live e de webhook separado segue a [documentação Stripe](https://docs.stripe.com/keys); nenhum acesso autenticado à Stripe foi necessário.
