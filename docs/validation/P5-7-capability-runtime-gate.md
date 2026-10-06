# P5-7 — Gate DEVICE da Capability Runtime

> **Candidato fixo:** Capability Runtime integrado em `0a5a49a802d881fc192b6e9426d4f65b5e7344a5` (P5-6 / PR #236); correção exclusivamente de distribuição do Hub integrada em `d725c2beda0ed4344dd41c68754f0969977b9dd4` (PR #240).
>
> **Hub:** `hub-v0.0.1-dev.16` — `ecosystem-hub-0.0.1-dev.16.apk`
>
> https://github.com/AbnerCruz/Ecosystem/releases/tag/hub-v0.0.1-dev.16
>
> **Lunet:** `lunet2d-v0.0.1-dev.1000116` — `Lunet-0.0.1-dev.1000116-arm64.apk`
>
> https://github.com/AbnerCruz/Ecosystem/releases/tag/lunet2d-v0.0.1-dev.1000116
>
> Os dois workflows de release concluíram com sucesso em 2026-10-05. Não substitua o candidato durante o teste sem registrar a nova versão.

## Objetivo

Provar em Android real o gate da Fase 5:

```text
mesma Tool
   |
   +-- Hub Host (standalone)
   |
   +-- Lunet Host (standalone, sem Hub)

mesma semântica + Context/grants explícitos + lifecycle/UX real
```

P5-7 não introduz feature nova. Se alguma etapa falhar, o gate falha e a correção volta ao item responsável; não se "ajusta" a expectativa para caber no resultado.

## Pré-condições

1. Instale/atualize **Hub dev.16** e **Lunet dev.1000116** pelos links acima. O Hub dev.16 substitui o dev.15, que foi revogado após falha DEVICE causada por `versionCode` regressivo.
2. No Lunet, abra um projeto existente ou crie um projeto simples.
3. Para o texto de referência, use exatamente:

```text
Olá mundo
🙂
```

Resultado canônico de `text.inspect@1.0.0`: **11 caracteres, 3 palavras, 2 linhas**.

## A — Hub standalone

1. Feche/force a parada do Lunet.
2. Abra somente o Hub.
3. Toque em **Connections**.
4. Confirme:
   - `text.inspect@1.0.0` aparece;
   - status `available`;
   - Context mostra `ecosystem:ecosystem → product:hub`;
   - grants aparecem explicitamente como `nenhum`;
   - a tela informa que a fonte é o Registry do Host.
5. Feche Connections e abra **Analisar texto**.
6. Cole o texto de referência e execute.
7. Esperado: **11 / 3 / 2**.
8. Feche o diálogo, reabra e repita. Não pode aparecer resposta stale, crash ou sessão antiga.

**A passa** somente se o Hub executar a Tool sozinho e a UX continuar responsiva.

## B — Lunet standalone sem Hub

1. Force a parada do Hub.
2. Desative Wi-Fi/dados móveis para eliminar dependência externa acidental.
3. Abra o Lunet e um projeto.
4. Crie/abra um arquivo simples com o texto de referência. O conteúdo não precisa compilar; o inspector recebe o texto atual em memória.
5. Abra **Ferramentas → Connections**.
6. Confirme:
   - `text.inspect@1.0.0` aparece;
   - status `available`;
   - Context inclui `ecosystem:ecosystem → product:lunet2d → project:<GameId>`;
   - grants aparecem explicitamente como `nenhum`;
   - a fonte declarada é o Registry do Host.
7. Feche Connections e use **Ferramentas → Inspecionar texto (Host local)**.
8. Esperado no documento inteiro: **11 / 3 / 2**.
9. Selecione apenas parte do texto e inspecione novamente; o diálogo deve indicar **seleção atual** e contar somente a seleção.
10. O diálogo deve afirmar que nenhum provider externo é necessário.

**B passa** somente se tudo funcionar com o Hub parado e a rede desligada.

## C — Mesma Tool, mesma semântica

Compare Hub e Lunet usando o mesmo input:

| Caso | Esperado |
|---|---|
| texto de referência | 11 caracteres, 3 palavras, 2 linhas |
| vazio | 0 / 0 / 0 |
| Unicode/emoji | não corromper nem travar |
| múltiplas linhas | mesma contagem nos dois Hosts |

Qualquer divergência entre Hosts reprova o gate, mesmo que cada aplicativo "pareça funcionar".

## D — Lifecycle, toque e layout

Nos dois aplicativos:

1. abra/feche Connections repetidamente;
2. abra/feche a Tool repetidamente;
3. envie o app ao background e retorne;
4. gire o aparelho **se a Activity permitir rotação**;
5. teste com teclado aberto/fechado;
6. confirme que botões e conteúdo permanecem acessíveis em tela pequena;
7. confirme que fechar uma sessão/operação impede resposta tardia de reaparecer;
8. no Lunet, troque de projeto e confirme que Connections passa a mostrar o novo `GameId`.

Falha visual ou de toque que impeça a operação reprova D.

## E — Segurança e independência

A suíte automatizada já prova: deny-by-default, Context mismatch, revoke, cancel, close, erros e conformance da Host API v1. O DEVICE precisa provar a consequência observável:

- Hub executa localmente sem Lunet;
- Lunet executa localmente sem Hub;
- Lunet continua funcionando com Hub forçado a parar e rede desligada;
- Connections nunca oferece "conectar" criando grant novo: ela apenas explica o estado derivado do Host;
- o pareamento IPC permanece separado em **Conexões IPC** e não é requisito da Tool local.

P5-4 continua sendo a evidência canônica do transporte Binder autenticado. P5-7 não reabre DEC-0037 nem exige uma sessão IPC ativa para a Tool local.

## Resultado

**PASSED / aprovado.**

- O candidato final foi Hub `hub-v0.0.1-dev.16` + Lunet `lunet2d-v0.0.1-dev.1000116`.
- O gate DEVICE A–E completo foi aprovado pelo proprietário pelo portal na Issue #246; registro canônico `docs/governance/responses/VAL-HO-20261005-p5-7-dev16-candidate-c2ff0d8de451.md`.
- A correção de `versionCode` do Hub foi validada separadamente na Issue #242, provando atualização in-place do dev.16 sobre a instalação anterior.
- A matriz de enforcement da Fase 5 foi reconciliada no PR #245: NN-007/TEST e NN-016/RUNTIME passaram de `planned` para `implemented`, refletindo mecanismos já existentes e testados.

Com implementação, integração, enforcement, CI e DEVICE registrados, P5-7 e o gate da Fase 5 estão concluídos.
