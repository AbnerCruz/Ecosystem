# ADR-0003 — Checks de consistência em C# sem dependências

## Status

Aceito — ratificado pelo proprietário em DEC-0001 ([ADD-0003](../governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md), 2026-09-30).

## Contexto

MANIFEST §45.1 e §57.12 exigem checks mínimos de consistência e uma forma explícita de validar a matriz de enforcement já no primeiro commit. MANIFEST §4.1 define C# como padrão para nova infraestrutura. MANIFEST §49 exige justificar novas dependências.

## Problema

Escolher linguagem, runtime, forma de empacotamento e dependências dos checks, e como provar que eles realmente detectam violações.

## Opções

1. C# como *file-based app* do .NET 10 (`dotnet run tests/consistency/Check.cs`), sem pacotes NuGet, com validador próprio do subconjunto de JSON Schema usado.
2. C# em projeto `.csproj` com biblioteca de JSON Schema (ex.: JsonSchema.Net).
3. Scripts em Python ou shell.

## Decisão

Opção 1.

- Um único arquivo `tests/consistency/Check.cs`; nenhuma solução/projeto vazio (MANIFEST §45, §47).
- Requer .NET SDK 10+. Sem dependências externas.
- O validador implementa apenas as keywords usadas pelos schemas e **falha** diante de qualquer keyword não suportada — um schema nunca deixa de ser aplicado em silêncio.
- Cada check tem ID estável `CHK-...`; a matriz de enforcement referencia esses IDs e `CHK-ENFORCEMENT-MATRIX` verifica que existem.
- `--self-test` copia o repositório para um diretório temporário, injeta uma violação por caso e exige que o check correspondente falhe; exige também que todo check tenha ao menos um caso. O CI executa os dois modos.

Análise de MANIFEST §49 para a dependência no .NET SDK 10: é o runtime padrão do ecossistema; sem impacto em Android, offline, tamanho de produto ou segurança dos produtos (roda apenas em desenvolvimento/CI); licença MIT; substituível.

## Consequências

- Checks rodam igual local e no CI com um comando.
- O validador de schema é código próprio a manter; se os schemas passarem a precisar de keywords avançadas, reavaliar a opção 2 por ADR.
- Quando os checks crescerem além de um arquivo legível, poderão virar projeto próprio — decisão futura, não antecipada.

## Alternativas rejeitadas

- **Projeto `.csproj` + JsonSchema.Net:** dependência externa e scaffolding extra sem necessidade atual (NN-020, §49).
- **Python/shell:** exigiria exceção à linguagem padrão sem benefício demonstrável (NN-005, §4.1).

## Referências

MANIFEST §0.2, §4.1, §45.1, §49, §57.6, §57.12; NN-005, NN-020.
