# ADR 0012 — Expansão multiplataforma do Lunet2D (desktop, navegador, iOS)

**Data:** 2026-11-14
**Estado:** **Proposto** — depende da decisão **DEC-0062** do proprietário (NN-011). Nenhum código de produto é alterado por este ADR.
**Relacionados:** [ADR 0001](0001-fundacao-android.md), [ADR 0002](0002-modelo-de-compilacao-e-camadas.md), [ADR 0007](0007-framework-ide-studio-code-first.md), diagnóstico `docs/architecture/audits/lunet-diagnostico-geral-2026-11-14.md` (C1, C3), estudo [multiplataforma-viabilidade-2026-11-14.md](../architecture/multiplataforma-viabilidade-2026-11-14.md).

## Contexto

O SPEC §2 ainda declara "Android only". O proprietário quer avaliar levar o Lunet a desktop, navegador e iOS, preservando o Android. O estudo mostra que as camadas de jogo (Framework, Runtime, Compiler) e de dados (Core, Studio, Editor, Git, Docs, Networking, Ai) já são `net10.0` sem APIs de plataforma, com costuras `IGraphicsBackend`/`IAudioBackend`; a interface, o backend OpenGL ES, o Preview, os segredos e o empacotamento são Android. O gargalo difícil é compilar/carregar jogos no iOS e no navegador.

## Decisão proposta

1. **Android continua plataforma de referência** e recebe melhorias normalmente; nenhuma troca de UI Android sem decisão própria.
2. **Não reescrever** Framework, Compiler nem Runtime (NN-005); preservar boundaries (NN-002) e a independência em relação à plataforma de controle do Ecosystem (NN-003/NN-023).
3. **Fiscalizar a portabilidade já existente** (feito: `PlatformPortabilityTests`) e **propor contratos de plataforma** (`IInputBackend`, `IStorageBackend`, `ISecretsBackend`, `IBuildBackend`, `IClipboardBackend`, `IPlatformLifecycle`) **só quando o segundo host os exigir** (NN-022): extração por necessidade, não por antecipação.
4. **Tecnologia da casca compartilhada:** estudar **Avalonia** por spike mensurável (LUNET-482), com views nativas onde o desempenho exigir (Preview GL). Alternativas: manter UI nativa por plataforma, ou casca web. **A escolha é do proprietário (DEC-0062).**
5. **Ordem:** Desktop → Navegador → iOS. iOS não promete execução local de C# antes de o spike resolver a restrição de código dinâmico; o caminho provável é build remoto com AOT.
6. **SPEC:** §2/§3/§11 ("Android only", "landscape prioritário") passam a "Android primeiro, desktop previsto" **somente depois** da decisão, em mudança própria.

## Alternativas consideradas

- **Três destinos no projeto Android atual:** inviável; o projeto referencia `Android.*` diretamente.
- **MAUI:** o SPEC §3 já o recusa; o spike compara apenas opções coerentes com o SPEC.
- **Reescrever tudo em web:** perde Roslyn local, Preview e o investimento Android; descartada.

## Consequências

- Positivas: o investimento em Framework/Studio se multiplica sem fork; Android não regride.
- Custos: contratos de plataforma e backends novos; matriz de testes por host; DEVICE para cada plataforma (NN-017).
- Riscos: perda do Preview instantâneo no iOS; manutenção de duas UIs se a casca compartilhada não vingar.

## Critérios de aceite do spike (LUNET-482)

Partida a frio, rolagem de listas longas, latência de toque do canvas de pixel art e fluidez do Preview GL embutido, comparadas ao Android nativo atual no mesmo aparelho; relatório com números reproduzíveis e decisão registrada. Sem DEVICE e sem números, nenhuma plataforma é marcada como suportada.
