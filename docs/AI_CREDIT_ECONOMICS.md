# Economia de créditos — proposta implementada

Valores em USD, referência 02/10/2026. Nenhum plano/preço Stripe alterado. Os saldos históricos 100/300 são fixtures; não determinam viabilidade comercial.

## Unidade e fórmula

Hipótese de planejamento: receita efetiva de $0,01 por crédito consumido. Isso não é preço publicado nem valor garantido pelo billing atual. Pricing contém unidade, taxas por resolução, custo por referência, custo por MP adicional, buffer, overhead, margem e revisão.

```
C_provider = taxa(resolução) × duração, quando unidade = segundo
           ou primeiro MP + (ceil(MP_saida) − 1) × taxa_MP_extra
           ou taxa_por_imagem
           + número_referências × taxa_referência
C_imagino = C_provider × 1,10 + overhead
créditos = ceil(C_imagino / ((1 − 0,65) × 0,01))
margem_planejada = 1 − C_imagino / (créditos × 0,01)
```

Overhead não mede a conta real: reserva $0,002 para imagem BFL, $0,007 Gemini e $0,01 vídeo. Inclui uma provisão simplificada para armazenamento, rede e processamento. O buffer não garante absorção de moderação, retries operacionais ou chargeback. Refinar com uso faturado antes do lançamento.

| Oferta e configuração | Provider estimado | Imagino estimado | Créditos | Receita hipotética | Margem planejada |
|---|---:|---:|---:|---:|---:|
| Fast Image 1MP | 0,014 | 0,0174 | 5 | 0,05 | 65,2% |
| Fast Image 4MP | 0,017 | 0,0207 | 6 | 0,06 | 65,5% |
| Studio Image 1MP | 0,030 | 0,0350 | 10 | 0,10 | 65,0% |
| Studio Image 4MP | 0,075 | 0,0845 | 25 | 0,25 | 66,2% |
| Studio 1MP + 1 referência | 0,045 | 0,0515 | 15 | 0,15 | 65,7% |
| Studio 1MP + 4 referências | 0,090 | 0,1010 | 29 | 0,29 | 65,2% |
| Edit & Design 1K | 0,067 | 0,0807 | 24 | 0,24 | 66,4% |
| Edit & Design 2K | 0,101 | 0,1181 | 34 | 0,34 | 65,3% |
| Edit & Design 4K | 0,151 | 0,1731 | 50 | 0,50 | 65,4% |
| Edit 1K + 4 referências | 0,075 | 0,0895 | 26 | 0,26 | 65,6% |
| Fast Video 720p 4s | 0,200 | 0,2300 | 66 | 0,66 | 65,2% |
| Fast Video 720p 8s | 0,400 | 0,4500 | 129 | 1,29 | 65,1% |
| Fast Video 1080p 8s | 0,640 | 0,7140 | 204 | 2,04 | 65,0% |
| Cinema Video 720p 4s | 1,600 | 1,7700 | 506 | 5,06 | 65,0% |
| Cinema Video 1080p 8s | 3,200 | 3,5300 | 1009 | 10,09 | 65,0% |
| Pipeline Demo | 0 | 0 | 1 fixture | Não há venda | Não aplicável |

BFL reporta cost em créditos próprios de $0,01, guardado separadamente em ProviderReportedCostUsd. Gemini acima usa preço de output mais provisão de $0,002 por referência; input e thinking reais podem variar. Não representar essa provisão como tarifa oficial ou teto de gasto. Não estimar GPT Image a partir de um número de tokens de exemplo como se fosse preço universal por imagem.

As linhas Veo são comparação econômica do contrato implementado, não ofertas liberáveis: os endpoints Preview foram reclassificados como COMPATIBILITY por retirada prevista em 22/10. Sua substituição exige nova versão/pricing; não cobrar a tabela antiga para um endpoint diferente.

Fontes: [BFL pricing](https://bfl.ai/pricing?category=flux.2), [Google pricing](https://ai.google.dev/gemini-api/docs/pricing). O relatório de mercado apresenta as taxas base; esta tabela é cálculo próprio do Imagino.

## Crédito reservado e liquidação

Cotação expira em 10 minutos e liga prompt, inputs, controles, modelo, versão e pricing via hash. O POST recalcula no servidor; nunca aceita preço do browser. Transação Mongo debita carteira e insere job. Completed mantém o débito somente depois de armazenar a saída; Failed/Cancelled estorna uma única vez em transação. Replay com mesma chave e payload retorna job existente sem novo débito. Mesma chave com outro payload retorna conflito.

Timeout e submissão incerta estornam ao usuário. O fornecedor pode ainda cobrar uma submissão incerta ou um vídeo concluído após timeout: isso é perda operacional a reconciliar, não geração gratuita garantida. Não repetir POST sem idempotência/reconciliação nativa. Não há ajuste pós-fato da carteira pelo custo faturado; a cotação aceita fica congelada.

Se receita efetiva cair de $0,01 para $0,005 por crédito, as mesmas ofertas deixam de atingir 65% (Fast 1MP cai para 30,4%). Taxas de pagamento, impostos, câmbio, créditos promocionais, descontos e uso não consumido precisam entrar no P&L futuro. Margem planejada não é lucro líquido.

## Homologação paga proposta, não executada

Primeira etapa: 3 chamadas BFL no máximo — klein 1MP texto ($0,014), pro 1MP texto ($0,030), pro 1MP com uma referência até 1MP ($0,045). Estimativa total $0,089; solicitar teto de $0,15, sem repetição automática e sem compra de saldo/plano. Verifica contrato real, binding, download, R2 e comparação inicial de saídas; três amostras não constituem benchmark estatístico.

Etapa separada futura: Gemini 1K, até 2 chamadas, teto $0,25 incluindo input/thinking. Vídeo deve primeiro migrar para um endpoint vigente; apresentar então nova estimativa/modelo/quantidade/teto. As referências antigas de Veo Lite $0,20/4s e Cinema $1,60/4s não autorizam nem recomendam executar os Preview. Nenhuma chamada foi feita. Chaves/acesso são gate distinto do orçamento.
