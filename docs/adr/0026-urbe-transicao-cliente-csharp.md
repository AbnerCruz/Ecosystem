# ADR-0026 — Transição do Urbe JavaScript para o cliente C#

## Status

Aceito — **DEC-0036-C** escolhida pelo proprietário em 2026-10-04. O corte continua proibido até UC-31/G-C5.

## Contexto

DEC-0024-B manda construir o novo cliente C# em paralelo e só trocar o produto distribuído após paridade total. Ao mesmo tempo, o Urbe JavaScript já possui usuários/instalações possíveis em três superfícies e uma transição de distribuição em andamento:

- Android: pacote `app.urbe`; atualização exige a mesma chave privada e `versionCode` monotônico.
- Windows: Electron/NSIS e updater atual; o produto lê/escreve o mesmo vault de arquivos.
- Web/PWA: URL/origem e escopo do service worker determinam acesso ao IndexedDB/PWA instalado. Mudar origem é, na prática, também uma migração de dados.
- DEC-0031/ADR-0019 movem builds para o Ecosystem; P4-9 usa uma versão-ponte do **cliente JS** para trocar o feed atual sem quebrar instalações.
- DEC-0021-C mantém como direção futura uma plataforma first-party progressiva, mas ela não existe e não deve ser acoplada à migração de linguagem.

Misturar três mudanças no mesmo corte — **tecnologia do cliente**, **identidade/canal de atualização** e **origem Web** — multiplica o risco sem benefício. A transição para C# precisa preservar o produto e deixar a mudança futura de distribuição first-party como passo separado.

## Problema

Como levar instalações existentes do Urbe JavaScript ao cliente C# preservando dados, identidade, capacidade de atualização e rollback, sem obrigar reinstalação ou mover a origem Web no mesmo evento?

## Opções

| Opção | Estratégia | Consequências |
|---|---|---|
| **A — Ponte preservando identidade/origem, rollout por superfície após G-C5** **(recomendada)** | Android mantém `app.urbe`, mesma chave e versão crescente; Windows recebe uma última versão JS/bridge capaz de transferir o canal para o instalador C# e preservar o vault; Web troca a implementação mantendo primeiro a mesma origem/escopo e acesso ao IndexedDB. Canais antigos ficam como recuperação. A plataforma first-party/URL nova é migração posterior | Melhor continuidade e rollback; exige ensaio real de upgrade em UC-26. Cada superfície pode ser promovida separadamente **somente depois** de paridade total e autorização G-C5 |
| **B — Cliente C# paralelo com identidade/canal próprios** | Instala o C# ao lado do JS; usuário escolhe/importa vault; Web usa rota/origem nova | Rollback simples e nenhum updater cruzando tecnologias, mas duplica apps, configurações e dados; PWA/IndexedDB não migram entre origens automaticamente; fragmenta a identidade do Product |
| **C — Reinstalação deliberada com export/import obrigatório** | No corte, usuário exporta backup, remove o cliente antigo, instala o novo e importa | Implementação mais simples; péssima continuidade, exige ação manual e aumenta risco de perda/confusão; inadequada para atualização normal |
| **D — Big-bang simultâneo nas três superfícies** | Mesmo princípio de identidade preservada, mas Android/Windows/Web mudam no mesmo release | Uma data de corte simples, porém blast radius máximo e rollback coordenado mais difícil; uma falha de uma superfície segura todas as outras |

## Decisão

**DEC-0036-C aceita:** reinstalação deliberada com backup/export/import obrigatório.

O novo cliente C# não tentará substituir silenciosamente a instalação JavaScript existente. A transição aprovada é explícita e orientada a dados: antes da troca, o usuário gera um backup/export verificável; instala o novo cliente; importa o backup; valida o vault; e só então remove o cliente anterior se desejar. O produto antigo continua disponível como recuperação durante a janela definida por UC-26/UC-31.

### Android

- não depender de upgrade in-place do APK JavaScript para o APK C#;
- exigir export/backup antes da troca;
- instalar o novo APK pelo canal aprovado quando G-C5 autorizar;
- importar e validar o vault antes de considerar a migração concluída.

### Windows

- não depender de substituição automática Electron → .NET;
- exportar/backup, instalar o cliente C# como instalação nova e importar;
- manter o instalador anterior disponível para recuperação durante a janela definida.

### Web/PWA

- a transição pode envolver uma nova implementação/origem, desde que o fluxo de export/import preserve integralmente os dados;
- IndexedDB/origem antiga não é assumido como canal de migração;
- a instalação PWA antiga só é removida depois de backup/export e validação no cliente novo.

## Consequências

- P4-9 pode continuar movendo o **feed do JS** para releases diretas; isso não é o corte C#.
- UC-26 implementa e ensaia o fluxo export → instalação nova → import → validação; UC-31 continua sendo quem autoriza o corte.
- O cliente JS e seus canais permanecem disponíveis como recuperação durante a janela definida pelo plano de corte.
- Todo ensaio usa backup restaurável e `surface-protocol.json`; Android/Windows exigem aparelho/instalação real.
- Nenhum backend, conta, Hub ou plataforma first-party é pré-requisito da migração (NN-003/NN-023).
- A compatibilidade do ZIP/manifesto passa a ser requisito central da transição entre clientes.

## Alternativas rejeitadas

Rejeitadas por DEC-0036: A (ponte preservando identidade/origem), B (cliente paralelo permanente) e D (big-bang simultâneo). Permanecem documentadas acima como histórico.

## Referências

- DEC-0024-B; DEC-0021-C; DEC-0031; [ADR-0016](0016-migracao-do-urbe-para-csharp.md); [ADR-0019](0019-releases-diretas-dos-products.md).
- `docs/distribution/direct-releases.md`; `docs/architecture/distribution.md`.
- `apps/urbe/docs/csharp/ROADMAP.md`, `acceptance/surface-protocol.json`.
- Urbe ADR-0004 (dados/forward/backup) e ADR-0010 (migração C#).
- MANIFEST NN-001, NN-003, NN-011, NN-014, NN-017, NN-019, NN-023.
