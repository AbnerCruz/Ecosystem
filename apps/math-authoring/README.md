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
será registrada quando existir formato legado concreto. O formato físico, os checkpoints e a recuperação são realizados por MA-003 em
`MathAuthoring.Persistence`, sem alterar o controlador autoral em memória. O catálogo e a validação de objetos do scene
graph entram em MA-005; não são inferidos a partir de IDs isolados.

## Pacote portátil e recuperação (MA-003)

`MathAuthoring.Persistence` implementa um pacote ZIP versionado de dados:
`manifest.json` (bundleVersion, revisão, SHA-256 do documento, catálogo de assets),
`project.json` (formato canônico MA-002) e `assets/<sha256>.bin`.
Nomes de entradas são derivados exclusivamente de hashes, nunca de caminhos
fornecidos pelo projeto. Dois IDs de asset podem apontar para o mesmo conteúdo
armazenado uma só vez. O import valida versões, campos/entradas permitidos,
integridade SHA-256, referências do documento, MIME allowlisted, limites de
expansão e UTF-8 válido, sem executar conteúdo importado.

A API `ProjectPackage.Write(Stream,...)` / `Read(Stream,...)` serve para
import/export, inclusive streams provenientes de SAF Android. Limites iniciais:
ZIP até 72 MiB, conteúdo expandido até 64 MiB, no máximo 128 assets, 8 MiB
por asset e 1 MiB para cada JSON. Nenhum arquivo ZIP é extraído no filesystem.

`ProjectStorage` é **somente um adapter para filesystem local**: salva um
checkpoint temporário no mesmo diretório, dá flush, relê e valida os bytes e
publica com `File.Replace`/backup quando havia destino válido. Falhas antes da
publicação não alteram o último principal; após publicação, principal e backup
podem ser avaliados. Não sobrescreve primário corrompido, não aceita rollback de
revisão nem mistura IDs de projetos diferentes. `Autosave` grava um checkpoint
separado; `Recover` inspeciona principal, autosave e backups e **retorna**
a maior revisão íntegra sem modificar arquivos nem ocultar a origem recuperada.
A interface deve exibir a proveniência e pedir decisão antes de substituir um
arquivo avariado.

**Limites conhecidos:** o rename/replace atômico só vale no filesystem local
que oferece essa primitiva; SAF/document providers Android têm semântica
diferente e exigem adapter transacional próprio. Não foi realizado DEVICE.
Gravações concorrentes para **o mesmo arquivo, dentro do mesmo processo**, são serializadas por caminho, impedindo substituições concorrentes de checkpoint e rollback de revisão; isso **não** implementa lock entre processos, sincronização multiplayer nem atomicidade SAF. Não há crash-fsync de diretório garantido em todos os sistemas operacionais.
A autenticação de autor/assinatura criptográfica não faz parte do pacote: SHA-256
detecta corrupção, mas **não** certifica procedência contra adulteração intencional.
Assets do Stage continuam sem nós de cena até MA-005; a lista de media types
deverá evoluir com decisão de compatibilidade quando houver segundo uso real.
