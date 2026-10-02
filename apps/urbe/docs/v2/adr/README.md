# Architecture Decision Records — Urbe 2.0

ADRs registram decisões arquiteturais significativas e o contexto que levou a elas.

## Quando criar

Use ADR quando a decisão:

- define ou altera boundary;
- escolhe tecnologia estrutural;
- muda persistência/schema;
- muda modelo de confiança/segurança;
- muda estratégia de release/plataforma;
- cria, substitui ou remove uma autoridade;
- impõe custo relevante para decisões futuras.

## Estados

- Proposed
- Accepted
- Superseded
- Rejected
- Deprecated

## Numeração

Arquivos:

```
0001-titulo-curto.md
0002-titulo-curto.md
```

Números não são reutilizados.

## Regra

ADR explica uma decisão. Não substitui requisito, SPEC, roadmap ou documentação operacional.

Use [0000-template.md](0000-template.md) como base.

## Índice

| ADR | Título | Status |
|---|---|---|
| [0001](0001-runtime-sem-build-e-registro-de-modulos.md) | Runtime sem build com registro explícito de módulos | Accepted |
| [0002](0002-confianca-de-plugins-full-trust.md) | Confiança de plugins: full-trust aprovado com UX honesta | Accepted |
| [0003](0003-licenca-fonte-disponivel.md) | Licença: todos os direitos reservados | Accepted |
| [0004](0004-compatibilidade-1x-e-protecao-forward.md) | Compatibilidade 1.x → 2.x e proteção forward | Accepted |
| [0005](0005-release-e-versao-unica.md) | Release por tag e fonte única de versão | Accepted |
| [0006](0006-identidade-documental-e-modelo-de-artefatos.md) | Identidade em sidecar e modelo de artefatos | Accepted |
| [0007](0007-credenciais-de-ia-e-csp.md) | Credenciais de IA e CSP | Accepted |
| [0008](0008-comentarios-em-sidecar.md) | Comentários em sidecar fora do arquivo | Accepted |
| [0009](0009-composicoes-para-paginas.md) | Composições migram para páginas | Accepted |
| [0010](0010-migracao-para-csharp.md) | Migração do Urbe para C# (reescrita com paridade; Ecosystem ADR-0016, DEC-0024-B) | Accepted |
