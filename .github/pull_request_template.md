## Escopo

<!-- Tarefa (ID do ROADMAP ou Issue) e componente(s) afetado(s) conforme ecosystem.json. -->

## Invariantes consideradas

<!-- NN-XXX relevantes e como foram respeitadas. -->

## Decisões

<!-- Esta mudança altera boundary, protocolo, ownership, modelo de dados, compatibilidade pública,
     responsabilidade de componente, lifecycle ou decisão consolidada? Se sim: ADR e/ou DEC-XXXX (NN-011). -->

## Documentos normativos consultados

<!-- NN-010: MANIFEST.md, ADRs, SPECs, contracts lidos diretamente. -->

## Base e integração (ADR-0014)

- Base (`base_commit` do handoff):
- [ ] `dotnet run tests/consistency/Check.cs -- --integration <esta branch>` = FRESH (se STALE: merge da `main`, reconciliação semântica, checks de novo)
- [ ] Conflitos/sobreposição: nenhum / descritos no handoff (causa, resolução, evidência)
- [ ] Regras de merge do Product respeitadas (ex.: Urbe só com pedido explícito do proprietário)

## Verificação

- [ ] `dotnet run tests/consistency/Check.cs`
- [ ] `dotnet run tests/consistency/Check.cs -- --self-test`
- [ ] Testes do componente
- [ ] Validação humana necessária? Qual? (NN-017)

## Handoff

<!-- Caminho do handoff em docs/governance/handoffs/. -->
