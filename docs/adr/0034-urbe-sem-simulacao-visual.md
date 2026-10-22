# ADR-0034 — Urbe nativo sem a simulação visual: o Explorer como base e o registro das notas

## Status

Aceito — **DEC-0051** (ordem do proprietário em 2026-10-22, [ADD-0028](../governance/addenda/ADD-0028-urbe-sem-simulacao-visual.md)). **Substitui** a parte do [ADR-0032](0032-urbe-ui-nativa-sem-webview.md) que faz da cidade em tela cheia a interface do Urbe; o restante do ADR-0032 (Avalonia, heads Android e Desktop, sem WebView) continua valendo.

## Contexto

FATO: o proprietário usou a beta 2.0.0-beta.14 e concluiu que "absolutamente qualquer coisa referente a simulação simplesmente não funciona como produto" (peso, espaço). Ele mandou retirar a simulação visual inteira e todas as suas funcionalidades, limpar o código e seguir o desenvolvimento sem ela, mantendo o mesmo produto (ADD-0028).

FATO: no cliente nativo, a cidade era mais que desenho. O mapa da 1.8.4 (`.urbe/mapa.json`) guardava, junto com casas, lotes e ruas, o **id, as datas e as pastas vazias** de cada nota. Ele era gravado a cada mudança (`MapSaveScheduler` → `IUrbeVaultStorage.SaveMapAsync`). Renomear, mover, excluir e criar nota passavam pela cidade (`LegacyCity`).

FATO: `Urbe.UI`, `Urbe.App` e `Urbe.Web` (casca Blazor) estavam congelados desde o ADR-0032 e só saíam no UC-34, quando o cliente nativo cobrisse o que faziam.

## Problema

Retirar a simulação sem perder dados do vault e sem quebrar a beta instalada. Ao mesmo tempo, dar às notas uma fonte própria para o que só o mapa guardava.

## Opções

| Opção | Desenho | Riscos |
|---|---|---|
| **A — Registro próprio das notas em `.urbe/notas.json`; mapa só lido uma vez** **(escolhida)** | `NoteLibrary` (Core) guarda id, criado, modificado e pastas. Num vault sem registro, lê esses dados do `mapa.json` da 1.8.4 uma vez, sem regravar | Um arquivo novo no vault |
| B — Continuar gravando o `mapa.json` sem desenhar a cidade | Mantém o formato | Mantém o código da cidade vivo (contra "limpa o código") e reescreve um arquivo que o ramo congelado precisa intacto |
| C — Sem registro: ids novos a cada abertura | Menos código | Perde ids e datas, quebra histórico, lixeira e identidade das notas |

## Decisão

**Opção A.**

- **O Explorer é a base do app.** Ele ocupa a tela; o editor e os painéis abrem por cima. Na abertura, o app volta direto para a última nota (UC-59). Os destinos são **Notas** e **Assistente**.
- **`NoteLibrary` (Urbe.Core)** é a autoridade do id, das datas e das pastas das notas no cliente nativo (NN-001). Ela é gravada em **`.urbe/notas.json`**:

  ```json
  {"versao":1,"notas":{"<caminho>":{"id":"…","criado":"AAAA-MM-DD","modificado":"AAAA-MM-DD"}},"pastas":["…"]}
  ```

  - A gravação é verificada como as outras do vault: bytes exatos, recusa se o vault mudou por fora, recusa vault de formato futuro.
  - Um registro de versão mais nova nunca é reescrito (`NotesSaveScheduler`, 120 + 900 ms, como antes).
  - As tags vêm do texto da nota.
  - O `.urbe/identity.json` continua sendo gravado e reconciliado para notas renomeadas fora do app.
- **Vault vindo da cidade.** Sem `notas.json`, os ids, datas e pastas vêm do `.urbe/mapa.json` da 1.8.4, que é **só lido**.
- **Arquivos da cidade preservados.** `.urbe/mapa.json` e `.urbe/mundo.json` nunca são apagados nem reescritos. O `VaultWriter` não planeja mais o mapa. O Importar reconhece como vault uma pasta com `notas.json`.
- **Saem do produto:**
  - o motor do mundo e da cidade em `Urbe.Core`;
  - `Urbe.Client/World`, o minimapa, a vida, as decorações, o visual das construções e o foco de ligações;
  - os textos e comandos da cidade (busca, paleta, tutorial, assistente);
  - os oráculos e fixtures que só provavam a cidade.
- **UC-34 executado.** `Urbe.UI`, `Urbe.App` e `Urbe.Web` saem do repositório. O job `csharp-native` do CI foi retirado antes, em PR próprio (#640), porque o integrador testa com os workflows da `main`.
- **Congelado, não perdido.** O ramo `urbe-simulacao-congelada` (commit `9c13dc2e`) guarda a beta.14 com a simulação, e a 1.8.4 em `apps/urbe/` não é tocada.
- **Mesma beta.** O mesmo pacote `app.urbe.csharp` recebe a versão nova, por cima da instalada.

## Consequências

- Um teste de arquitetura (`TheSimulationIsNotInTheProduct`) impede que tipos da simulação voltem a `Urbe.Core`, `Urbe.Client`, `Urbe.Android` ou `Urbe.Desktop`.
- Os itens do ROADMAP C# que só existem pela cidade ficam **congelados** (seção ADD-0028 do ROADMAP). A paridade com a 1.8.4 continua valendo fora da simulação: editor, Explorer, lixeira, histórico, importação, páginas e assistente.
- O ramo congelado lê o mesmo vault, porque o mapa continua no disco. As notas criadas depois na versão nova não estarão nele. O ramo, ao abrir, as encaixa como notas novas, como faz com qualquer nota fora do mapa.
- A mudança é crítica: PR crítico, autorização do proprietário para integrar e validação no aparelho (NN-017) antes de fechar.

## Alternativas rejeitadas

- **B**: deixa vivo o código que o proprietário mandou tirar e regrava o arquivo do ramo congelado.
- **C**: perde dados (ids e datas) de quem já usa a beta.

## Referências

MANIFEST §0.1 (NN-001, NN-005, NN-009, NN-011, NN-013, NN-017, NN-018); DEC-0043, DEC-0045, DEC-0051; ADD-0020, ADD-0022, ADD-0026, ADD-0028; ADR-0032; PR #640.
