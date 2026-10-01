## Escopo

<!-- Tarefa (ID do ROADMAP ou Issue) e componente(s) afetado(s) conforme ecosystem.json. -->

## Invariantes consideradas

<!-- NN-XXX relevantes e como foram respeitadas. -->

## Decisões

<!-- Esta mudança altera boundary, protocolo, ownership, modelo de dados, compatibilidade pública,
     responsabilidade de componente, lifecycle ou decisão consolidada? Se sim: ADR e/ou DEC-XXXX (NN-011). -->

## Documentos normativos consultados

<!-- NN-010: MANIFEST.md, ADRs, SPECs, contracts lidos diretamente. -->

## Base e integração (ADR-0014, ADR-0015)

- Base (`base_commit` do handoff):
- [ ] Handoff em `review` (ou `done`, se só faltar a integração) e PR fora de rascunho: o integrador automático testa o estado combinado com a `main` atual e integra conforme o `mergePolicy` (status `ecosystem/integration`)
- [ ] Conflitos/sobreposição: nenhum / descritos no handoff (causa, resolução, evidência); se o integrador devolver (`precisa-reconciliar`), merge da `main` na branch, reconciliação semântica, push
- [ ] Regras de merge do Product respeitadas: quem autoriza é o proprietário (label `integrar`); nenhum agente adiciona essa label

## Verificação

- [ ] `dotnet run tests/consistency/Check.cs`
- [ ] `dotnet run tests/consistency/Check.cs -- --self-test`
- [ ] Testes do componente
- [ ] Validação humana necessária? Qual? (NN-017)

## Handoff

<!-- Caminho do handoff em docs/governance/handoffs/. -->
