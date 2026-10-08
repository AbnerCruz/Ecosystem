# Autoria matemática temporal

Product independente do Ecosystem para criar explicações matemáticas visuais, temporais e exportáveis a partir de um documento semântico único.

- **ID técnico:** `math-authoring`
- **Nome público:** em aberto
- **Nome de exibição provisório:** Autoria matemática
- **Linguagem:** C# / .NET 10
- **Princípios:** local-first, mobile-first, determinístico, standalone sem outro Product e sem IA
- **Fonte canônica do projeto:** `ProjectDocument` versionado; visual e textual editam o mesmo modelo

Fundação ratificada pela DEC-0040 e ADRs 0029/0030. O primeiro alvo é o vertical slice Δx → 0 pela engine genérica.

## Documento JSON v1 (MA-002)

O JSON editável é projeção textual do mesmo `ProjectDocument`; não há estado
autoral separado para a UI. [document.v1.schema.json](document.v1.schema.json)
descreve os campos públicos; `ProjectJson.Parse` impõe também unicidade das
chaves JSON, limites de bytes/AST e referências de variáveis. IDs e ordem das
listas semânticas são preservados, campos desconhecidos são recusados em vez
de descartados, e números seguem cultura invariável.

`ProjectSession` guarda somente snapshots canônicos validados. Comandos visuais
usam `Apply(baseRevision, command)`, e a edição textual usa
`ApplyText(baseRevision, json)`. Ambos compartilham publicação atômica em memória,
undo/redo e revisão monotônica. Texto inválido e revisão obsoleta jamais
substituem o documento. `Source` exporta a representação textual da revisão
publicada; `Snapshot` fornece cópia destacada para leitura.

**Limites atuais:** JSON em até 1 MiB, 512 variáveis, 128 cenas, 32 perfis,
4096 IDs por cena, AST com no máximo 10 mil nós e profundidade 32.
`ProjectMigrator.Open` aceita *apenas* steps C# explicitamente registrados no Product,
de versão N para N+1; nenhum migrador legado vem ativo por padrão. A origem
permanece intacta e o destino passa novamente pelo parser/validador v1;
versões futuras são recusadas, nunca rebaixadas. A primeira migração real só
será registrada quando existir formato legado concreto. Importar arquivo,
autosave, flush/replace atômico, backup e recuperação são parte de MA-003, não
deste controlador em memória. O catálogo e a validação de objetos do scene
graph entram em MA-005; não são inferidos a partir de IDs isolados.
