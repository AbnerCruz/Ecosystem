#!/usr/bin/env bash
# Veredito do workflow decision (ADR-0007/0008): "verde" só quando o RESULTADO desejado existe no repositório.
#
# Entradas (variáveis de ambiente, vindas dos 'outputs' dos passos anteriores):
#   APPLY_CODE   código de saída do aplicador (0 registrou ou já estava registrado; 3 recusou; 2 erro; vazio = não rodou)
#   OUTCOME      outcome do aplicador: registered | already-registered | rejected | error | (vazio)
#   VERIFY_CODE  código dos checks de consistência rodados antes de gravar (vazio quando não houve o que gravar)
#   PUSHED       true quando o commit foi enviado à branch padrão (vazio quando não houve o que gravar)
# Saída: código 0 = sucesso (registrada agora ou idempotentemente já registrada); código 1 = falha (decisão NÃO registrada).
# Escreve em stdout a linha "reason=completed|not planned" (para encerrar a Issue) e "verdict=success|failure".
# Regra única: a falha é o padrão; só combinações explicitamente corretas são sucesso.
set -u
apply="${APPLY_CODE:-}"; outcome="${OUTCOME:-}"; verify="${VERIFY_CODE:-}"; pushed="${PUSHED:-}"
ok=false
if [ "$apply" = "0" ]; then
  case "$outcome" in
    already-registered) ok=true ;;
    registered) if [ "$verify" = "0" ] && [ "$pushed" = "true" ]; then ok=true; fi ;;
  esac
fi
if [ "$ok" = "true" ]; then
  echo "verdict=success"; echo "reason=completed"; exit 0
fi
echo "verdict=failure"; echo "reason=not planned"; exit 1
