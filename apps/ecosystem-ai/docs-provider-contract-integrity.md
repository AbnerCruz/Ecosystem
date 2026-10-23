# Integridade do contrato do provider Chat Completions (P6-5)

Este incremento reforça o adapter existente de `AgentRuntime.Providers.ChatCompletions`.
Não cria outro loop, provider, permissões, ledger, schema ou infraestrutura
compartilhada. O custo remoto continua desconhecido se o POST chegou ao serviço,
mesmo quando a resposta é recusada: **não fazer retry automático**.

## Custo e tokens

- `usage.cost` numérico decimal, quando presente, representa USD e é usado
  como custo informado (com arredondamento conservador já existente).
- `usage.cost` ausente ou `null` permite estimativa **somente** quando há
  contagem completa de tokens e preços explícitos suficientes, conforme
  a política anterior. `TokensKnown` distingue tokens faltantes de zero.
- `usage.cost` explicitamente presente como string, booleano, objeto,
  array ou número irrepresentável como decimal falha com `bad-response`;
  nunca é reinterpretado silenciosamente como custo desconhecido para estimar.
  Valor negativo também falha.
- Respostas incompatíveis não inserem resposta ou cobrança fictícia no
  ledger do Runtime. A tentativa remota pode ter tido custo, cuja
  conciliação depende do provider.

## Ferramentas

- As chamadas de função recebidas devem usar **exatamente** um dos
  wire names derivados das tools anunciadas naquela requisição.
- Chamadas de tools não anunciadas, retorno do nome canônico cru,
  ou chamadas quando a capacidade Tools não foi anunciada são rejeitados
  como `bad-response`, sem executar tools ou incluir payloads no erro.
- `tool_calls` pode estar ausente ou ser `null`/array; outros tipos
  são incompatíveis. `finish_reason: "tool_calls"` requer ao menos
  uma chamada válida, para não produzir falsa conclusão.

## Evidência e limites

`ChatCompletionsProviderTests` cobre custo inválido, custo `null`
com tarifa legítima, tools desconhecidas, retorno malformado e
`finish_reason` inconsistente, junto das regressões preexistentes
de chamadas válidas. A suíte usa HTTP mockado, sem segredos comerciais,
rede real, IA de outro Product ou Android.

A verificação CI e a integração devem ser registradas no PR/handoff.
Isso não encerra a tarefa P6-5, não estabelece contrato público nem
substitui DEVICE ou validação de custo real de provedor.
