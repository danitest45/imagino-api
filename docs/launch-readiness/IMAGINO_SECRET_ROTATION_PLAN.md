# IMAGINO — plano de rotação de segredos

**PREPARADO, NÃO EXECUTADO. B3 BLOCKED.** O operador confirmou o ambiente legado e pediu primeiro o inventário dos oito candidatos. Nenhuma rotação, criação de replacement ou revogação está aprovada nesta etapa. Produção, live/comercial, DNS, billing e geração paga permanecem fora do escopo.

## Lista para decisão humana

| Provider/system e candidatos | Ambiente / credencial | Ainda utilizada? | Ação recomendada, ordem e impacto esperado |
|---|---|---|---|
| MongoDB — B3-039, B3-041 | Provável Atlas legado Imagino.ai / imagino-cluster; duas credenciais em URI | UNKNOWN; hosts diferentes do staging autorizado | Confirmar usuário, privilégios, consumidores e lifecycle de cada variante. Se real e ainda necessária, aprovar novo usuário no ambiente específico, instalar, testar e só depois remover o antigo. Remoção prematura pode derrubar app/worker legado |
| JWT — B3-043, B3-047 | Backend/app legado; dois signers históricos | Novo AI staging não usa nenhum dos candidatos testados; legado UNKNOWN | Verificar configuração efetiva e todos os consumidores do app legado. Se nenhum consumidor permanece, comprovar aposentadoria; se houver consumidor aprovado, planejar signer/sessões. Não trocar o signer atual do novo staging por inferência. Mudança de signer pode invalidar access tokens e exigir novo login |
| RunPod — B3-040 | Histórico do imagino-api legado; key, account/project UNKNOWN | UNKNOWN | Correlacionar conta/key ID, permissões e consumidores. Se real/necessária, replacement de menor escopo, instalar no ambiente explicitamente aprovado, metadata/read-only smoke e revogação posterior. Sem task/pod/job de teste nem mudanças de billing |
| Replicate — B3-046 | Histórico do imagino-api legado; API key, account/project UNKNOWN | UNKNOWN | Mesma ordem segura; inventariar workers/webhooks consumidores. Validar somente autenticação/metadata autorizada; zero predictions/generation. Fixtures de webhook não precisam rotação |
| Stripe — B3-044, B3-045 | App legado; API key e segredo de endpoint; account/mode UNKNOWN | UNKNOWN; nem autenticidade está confirmada | Primeiro conferir placeholder vs sandbox/live. Checklist apenas. Não usar a ação de rotação do Dashboard antes de escolher a estratégia, pois ela pode revogar a antiga. API key e webhook são decisões separadas. Nenhuma alteração live/comercial nesta etapa |

Evidências históricas, revisões e paths de cada registro constam do [inventário](IMAGINO_SECRET_HISTORY_AUDIT.md). A resposta humana necessária é metadado: associação exata ao recurso/conta, consumidores, status de revogação/remoção ou approval explícito por candidato e ambiente. Não enviar valores no chat.

## Sequência após aprovação específica

1. Registrar candidato(s), resource/account ID, ambiente, consumidores, permissões, operador, janela e critérios de interrupção. Consumidor fora do ambiente aprovado interrompe a execução.
2. Criar replacement no recurso aprovado, com menor privilégio e sem ativar cobrança/auto billing. Manter o antigo durante a transição quando a plataforma permitir.
3. Instalar por secret store/Dashboard ou cliente autenticado que leia o valor em memória/STDIN, sem argv, transcript, stdout ou resposta contendo segredos. Não usar uma ferramenta que ecoe o segredo nos argumentos/retorno. Nenhum replacement em Git.
4. Confirmar SHA/configuração e deploy compatível. No Render, mudanças de env podem disparar deploy mesmo com auto-deploy off; preservar todo o restante da configuração.
5. Validar build/test relevante, health/readiness 200, logs sanitizados e funcionalidade afetada. Manter `PaidGenerationEnabled=false`, smokes reais false e emergency stop. Não testar credenciais históricas em destinos ainda não autorizados.
6. Confirmar replacement em todos os consumidores aprovados. Só então obter/executar a revogação prevista na aprovação humana; interromper se surgir consumidor adicional.
7. Provar rejeição da antiga por método read-only seguro ou registrar comprovante de revogação/remoção do provider. Falha de rede não prova revogação. Registrar somente IDs/horários/status; repetir inventário sem valores.

Se uma credencial já foi revogada ou a conta foi removida, coletar a prova e evitar rotação redundante. Um fingerprint diferente sozinho não prova revogação. Se o único consumidor for legado fora do ambiente autorizado, preparar plano separado; não instalar seu replacement nos novos stagings.

## JWT e refresh

Após aprovação, um signer HS256 deve usar material aleatório adequado gerado no canal seguro. Identificar suporte real a múltiplas signing keys/kid; não presumir transição sem invalidar sessões. Se houver apenas uma key, planejar reautenticação. Refresh token pode ter armazenamento/lifecycle independente; a troca de JWT não demonstra invalidação de refresh tokens. Reconciliar essa política antes da mudança.

Smoke obrigatório: login legítimo → owner autenticado → refresh → owner autenticado → logout → refresh rejeitado. Verificar também anonymous/foreign e mídia privada B1. Só aposentar a antiga após comprovar o replacement; sessões existentes e consumidores compartilhados devem estar incluídos no plano. Não fabricar JWTs com candidatos históricos para testar impersonation.

## MongoDB

O usuário **staging** observado possui readWrite apenas em `imagino_staging`, limitado ao cluster staging. Esse perfil serve como referência de privilégio, sem demonstrar vínculo com credenciais legadas. Um replacement aprovado deve atender somente às operações reais do app, incluindo índices/transações necessários, no banco/cluster definido. Validar conectividade, readiness e leitura/índices autorizados; não executar export/delete/migration no legado para comprovar autenticação.

As duas variantes históricas devem ser tratadas separadamente até provar identidade e consumidores. Não aumentar allowlist de IPs, permissões ou tier para contornar um teste sem aprovação adicional.

## Providers, Stripe e Google

Providers de geração: metadata/health somente, nenhum POST de geração/prediction/task. Um 200 de health não prova que uma API key externa funciona; usar comprovante/provider metadata autorizado, com saída filtrada.

Stripe: identificar conta e sandbox/live de API key e endpoint; webhook secrets são independentes. Uma sandbox key real também precisa proteção. A ação Stripe de rotação pode revogar a key existente; selecionar replacement/paralelismo antes de executá-la. Estas propriedades estão descritas na [documentação de keys](https://docs.stripe.com/keys). Nenhum produto, preço, checkout, webhook ou billing é alterado por este plano.

Google OAuth: a auditoria encontrou placeholder, sem segredo real identificado no texto. Se uma revisão privada futura comprovar exposição, identificar client/project/ambiente, callback/consumidores e estratégia de coexistência antes de pedir aprovação. Nenhuma mudança Google foi feita.

## Rollback e encerramento

Antes da revogação, interromper implantação problemática e manter consumidores na credencial anterior apenas durante a janela aprovada. Após revogação, não restaurar um segredo exposto: corrigir ou emitir outro replacement aprovado. Preservar deploy/configuração compatíveis com mídia privada, sem reabrir r2.dev.

Encerramento por candidato: classificação final, recurso/ambiente, comprovante de revogação/remoção ou teste/placeholder, data, operador, consumidores e smoke. B3 PASS somente quando não restar P0/P1. Git history rewrite é decisão posterior e separada; esta etapa não o executa.
