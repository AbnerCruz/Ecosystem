## Escopo

<!-- Tarefa (ID do ROADMAP ou Issue) e componente(s) afetado(s) conforme ecosystem.json. -->

## Invariantes consideradas

<!-- NN-XXX relevantes e como foram respeitadas. -->

## Decisões

<!-- Esta mudança altera boundary, protocolo, ownership, modelo de dados, compatibilidade pública,
     responsabilidade de componente, lifecycle ou decisão consolidada? Se sim: ADR e/ou DEC-XXXX (NN-011). -->

## Documentos normativos consultados

<!-- NN-010: MANIFEST.md, ADRs, SPECs, contracts lidos diretamente. -->

## Base e integração (ADR-0014, ADR-0015, ADD-0012)

- Base (`base_commit` do handoff):
- [ ] Handoff em `review` (ou `done`, se só faltar a integração) e PR fora de rascunho: o integrador testa o estado combinado com a `main` atual e classifica com a política da `main` — rotina entra sozinha; crítico espera o proprietário
- [ ] Mudança crítica fora das zonas da política (ex.: formato de dados num arquivo comum)? Escalada no handoff (`criticality`)
- [ ] Exige escolher uma direção nova? Decisão aberta no portal antes (decisão crítica)
- [ ] Conflitos/sobreposição: nenhum / descritos no handoff (causa, resolução, evidência); se o integrador devolver (`precisa-reconciliar`), merge da `main` na branch, reconciliação semântica, push
- [ ] Nenhum agente põe a label `integrar` nem simula eventos do proprietário

## Verificação

- [ ] `dotnet run tests/consistency/Check.cs`
- [ ] `dotnet run tests/consistency/Check.cs -- --self-test`
- [ ] Testes do componente
- [ ] Validação humana necessária? Qual? (NN-017)

## Handoff

<!-- Caminho do handoff em docs/governance/handoffs/. -->
