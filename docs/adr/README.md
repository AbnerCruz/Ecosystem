# Architecture Decision Records

Decisões estruturais do Ecosystem (MANIFEST §29, NN-011). Processo definido em [ADR-0001](0001-registro-de-decisoes-arquiteturais.md).

## Quando um ADR é obrigatório

Protocolo IPC, formato de manifest, modelo de capability, linguagem/runtime excepcional, boundaries, packaging, transporte, modelo de plugins, versionamento, compatibilidade, migração de dados, mudança de arquitetura do Urbe ou do Lunet, escolha estrutural do Hub, reescrita estrutural (NN-005), extração de componente compartilhado relevante (NN-022) e qualquer exceção às invariantes que o manifesto permita via ADR (ex.: NN-015).

## Como criar

1. Copie [`TEMPLATE.md`](TEMPLATE.md) para `NNNN-titulo-em-kebab.md` com o próximo número livre (números nunca são reutilizados).
2. Preencha todas as seções obrigatórias: Status, Contexto, Problema, Opções, Decisão, Consequências, Alternativas rejeitadas.
3. Status inicial: `Proposto`. Se a decisão cabe ao proprietário (NN-011), registre também uma entrada `pending` em [`docs/governance/decisions.json`](../governance/decisions.json).
4. Adicione o ADR ao índice abaixo.
5. `dotnet run tests/consistency/Check.cs` (`CHK-ADR`) deve passar.

Um ADR `Aceito` só muda por outro ADR que o substitua (`Substituído por ADR-XXXX`). Agentes nunca marcam `Aceito` uma decisão que exige o proprietário sem registro da decisão dele (MANIFEST §23.2).

**ADRs de fundação (0001–0004):** criados pelo agente fundador sob MANIFEST §57, com status `Proposto`. Vigoram provisoriamente como a fundação da Fase 0 até que o proprietário os ratifique ou rejeite (DEC-0001).

## Índice

| ADR | Título | Status |
|-----|--------|--------|
| [0001](0001-registro-de-decisoes-arquiteturais.md) | Registro de decisões arquiteturais | Proposto |
| [0002](0002-formato-dos-registros-canonicos.md) | Formato de `ecosystem.json` e dos registros de governança | Proposto |
| [0003](0003-checks-de-consistencia-em-csharp.md) | Checks de consistência em C# sem dependências | Proposto |
| [0004](0004-estrutura-inicial-do-repositorio.md) | Estrutura inicial do repositório | Proposto |
