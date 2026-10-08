# IMAGINO — higiene de segredos

As proteções abaixo previnem novos commits; não revogam o que já apareceu no histórico e não fecham B3 sozinhas.

## Configuração e stores

Manter exemplos com campos vazios ou placeholders explícitos. Nunca usar NEXT_PUBLIC_* para segredos: esses valores são destinados ao browser. Staging e produção devem ter credenciais distintas e escopos próprios; não copiar configuração legada para os novos stagings por suposição.

No Render, usar secret store/envs e secret files, com nomes aprovados, preservando variáveis existentes. `sync: false` em Blueprint não é um mecanismo de rotação de recursos já criados. Alteração de env pode disparar deploy. Consulte [Environment Variables and Secrets](https://render.com/docs/configure-environment-variables).

Na Vercel, usar variáveis secretas server-side, com ambiente/branch apropriados; configuração pública e credenciais devem ser separadas. Evitar pulls/dumps que exponham valores. Consulte [Config and Secret variables](https://vercel.com/docs/environment-variables/sensitive-environment-variables).

`.gitignore` protege `.env` e variantes locais, diretórios private/.secrets, keys/certificados privados e appsettings locais específicos. `appsettings.json` e exemplos continuam versionados apenas com configuração segura; ignore não remove o que já está tracked. A `.env.example` do frontend permanece sem segredos, e os exemplos backend existentes possuem campos sensíveis vazios.

## Detector e CI

Ambos os repositórios recebem `tools/secret-hygiene.cjs`, testes e workflow `Secret hygiene`. Usa apenas Node/Git, sem SDK externo ou rede. O detector examina o índice inteiro, não só a working tree: limpar o arquivo sem atualizar o staged content não contorna a verificação.

Classes: Mongo credential URI, passwords/client secrets/signing secrets/API keys nomeados, padrões de providers, OAuth/Google API, bearer/URL credentials e private key material. Diagnóstico contém somente sistema/tipo/path/linha/classificação; exceções e saída de Git são capturadas e omitidas. Nenhum valor, preview ou fingerprint de credencial é impresso.

Fixtures não são ignoradas só pelo path. Literais sintéticos em testes isolados/mocks, exemplos vazios e webhooks codificados com sentinels de teste são classificados separadamente. Chaves com aparência real, inclusive Stripe sandbox keys e material de webhook aleatório dentro de tests, continuam bloqueando. Não há allowlist global de `tests/**`, nem baseline que libere credenciais antigas. Alterações das regras exigem revisão dos testes negativos e das classificações.

Workflow: `pull_request`/`push`, `contents: read`, checkout fixado em SHA, credenciais Git não persistidas, timeout e nenhuma referência a secrets do repositório. Executa regressões do scanner e scan do índice. Não instala hooks globais nem modifica branch protection. O workflow está preparado na branch de revisão; comprovação da execução remota é registrada separadamente.

```sh
node --test tools/secret-hygiene.test.cjs
node tools/secret-hygiene.cjs .
```

O hook `.githooks/pre-commit` foi fornecido, com opt-in por commit:

```sh
git -c core.hooksPath=.githooks commit
```

Requer Node e Git disponíveis. Não foi alterado `core.hooksPath` compartilhado entre worktrees. O operador pode tornar o check obrigatório na proteção da branch e conferir os recursos nativos de [secret scanning GitHub](https://docs.github.com/en/code-security/concepts/secret-security/secret-scanning). O status desses recursos nativos não foi presumido nem alterado.

## Auditoria e comparação privada

`tools/secret-history-audit.cjs` no backend lê os dois históricos em memória, correlaciona ocorrências/revisões e emite somente o schema de metadados da evidência. O modo `--live-auth` usa a conta sintética privada, recebe JWT legítimo do AI staging, compara assinatura localmente e faz logout. Não fabrica tokens, não usa candidatos para acesso a outros ambientes e não chama geração. Host metadata opcional permite comparar URIs com o cluster staging autorizado sem testar os passwords antigos.

Para correlacionar valores com um store atual, o operador precisa de uma sessão autenticada/canal que faça a comparação local sem dump/argv/transcript. Usar HMAC com chave aleatória por execução somente em memória; reportar igualdade/status, nunca valor nem digest público. Não ler envs de produção ou configurar consumidores sem autorização específica. Preservar evidências de revogação do provider em canal privado e registrar somente metadados no relatório.

## Validação e limites

Validação local B3: 12 testes do detector passaram no backend; a suíte frontend completa passou 53 testes (41 existentes e os mesmos 12 de higiene). O scan de todo o índice preparado passou nos dois repositórios, sem findings. Os testes comprovaram também que limpar a working tree sem restage não libera um segredo staged. Não houve mudança de código da aplicação nem necessidade de novo build/browser smoke nesta etapa documental e de tooling.

Regressões cobrem redaction, índice staged, runtime signer, Mongo URI, JSON com comentários, exemplos, fixtures codificadas, provider keys reais aparentes em tests, sandbox Stripe e símbolos de UI. Nenhuma rotação ocorreu, portanto não há alegação de smoke pós-rotação. Builds/testes da aplicação precisam ser executados conforme o impacto de uma futura rotação aprovada.

O detector é heurístico e não substitui revisão humana nem scanners nativos. Binários não recebem OCR; falha de leitura/Git causa erro fechado, sem dados. A auditoria histórica limita blobs a 16 MiB e reporta exclusões. Não comprova ausência de secrets em forks, caches, blobs inalcançáveis ou arquivos fora do escopo. A revisão B3 permanece bloqueada pelos oito candidatos legados descritos no [inventário](IMAGINO_SECRET_HISTORY_AUDIT.md).
