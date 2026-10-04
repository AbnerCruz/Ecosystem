# ADR-0026 — Transição do Urbe JavaScript para o cliente C#

## Status

Proposto — decisão pendente **DEC-0036**. Nenhuma opção autoriza o corte; UC-31/G-C5 continuam sendo a decisão crítica final.

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

Pendente de DEC-0036.

A recomendação técnica é **A**. O Urbe continua sendo o mesmo Product; trocar a implementação não é motivo para criar um segundo produto ou abandonar a origem que contém os dados do navegador. O rollout pode ser sequencial depois do gate de paridade total, mas nenhuma superfície entra em produção antes de G-C5.

A estratégia A fixa apenas invariantes. O mecanismo exato é provado em UC-26:

### Android

- manter package id `app.urbe`;
- assinar o APK C# com a mesma chave privada do canal existente;
- `versionCode` sempre maior que qualquer build JS distribuída;
- validar upgrade por cima da instalação real sem desinstalar e com vault/estado sentinela;
- se assinatura ou identidade divergirem, **não publicar** um “substituto” incompatível.

### Windows

- preservar identidade do produto e localização/descoberta do vault;
- uma versão JS de ponte deve conseguir direcionar o usuário/updater ao instalador C#;
- o ensaio deve provar instalação/atualização/rollback e não presumir que um instalador .NET pode simplesmente substituir arquivos do Electron;
- se upgrade in-place seguro não puder ser provado, o fallback é paralelo **temporário e explícito**, com backup e migração, nunca destruição do cliente antigo.

### Web/PWA

- o primeiro corte tecnológico mantém a origem e o escopo do PWA existente;
- o cliente C# deve ler/migrar o IndexedDB da instalação JS antes de gravar qualquer formato novo;
- service worker/caches antigos precisam de estratégia de ativação e rollback;
- mover de `/Urbe/` para rota do Ecosystem ou domínio first-party é outro item de distribuição, com migração própria. Não esconder essa mudança dentro de UC-26.

## Consequências

- P4-9 pode continuar movendo o **feed do JS** para releases diretas; isso não é o corte C#.
- UC-26 implementa e ensaia a ponte, mas UC-31 continua sendo quem autoriza trocar o produto distribuído.
- Os repositórios/canais antigos não são apagados; continuam recuperação e histórico, coerente com DEC-0021-C.
- Todo ensaio usa backup restaurável e `surface-protocol.json`; Android/Windows exigem aparelho/instalação real.
- Nenhum backend, conta, Hub ou plataforma first-party é pré-requisito da atualização (NN-003/NN-023).
- A mesma origem Web é uma restrição de migração de dados, não uma decisão de distribuição eterna.

## Alternativas rejeitadas

Nenhuma até a decisão do proprietário. Depois da DEC-0036, esta seção será atualizada preservando o histórico.

## Referências

- DEC-0024-B; DEC-0021-C; DEC-0031; [ADR-0016](0016-migracao-do-urbe-para-csharp.md); [ADR-0019](0019-releases-diretas-dos-products.md).
- `docs/distribution/direct-releases.md`; `docs/architecture/distribution.md`.
- `apps/urbe/docs/csharp/ROADMAP.md`, `acceptance/surface-protocol.json`.
- Urbe ADR-0004 (dados/forward/backup) e ADR-0010 (migração C#).
- MANIFEST NN-001, NN-003, NN-011, NN-014, NN-017, NN-019, NN-023.
