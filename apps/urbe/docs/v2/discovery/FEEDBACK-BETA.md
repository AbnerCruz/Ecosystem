# Feedback de uso da beta — 2026-09-30

> Fonte: notas do proprietário ao usar o app (Android) + 2 capturas do Assistente criando o plugin de cronômetro (`Personalização/plugins/cronometro.js`, política "Pede aprovação", com "Desfazer"). Cada observação vira REQ (REQ-089..109), cláusula da SPEC (§15) e item(ns) do ROADMAP (fase F7, gate G7). **[F]** verificado no código, **[I]** inferência, **[P]** proposta.
> Regra (REQ-011/012): nada disto entra sem REQ; nada fica só neste documento.

| OBS | Observação (resumo) | Tipo | Verificação | REQ | Itens |
|---|---|---|---|---|---|
| OBS-01 | Fazer comentários em notas (hoje só dá para citar); nas páginas HTML os comentários também devem ser tratados | feature | — | REQ-089 | RM-F7-12, RM-F7-13 |
| OBS-02 | O atalho de código já insere a palavra "código" e não permite código pequeno no meio do texto (ocupa a linha) | bug/UX | **[F]** `app.js:3965` insere o texto de reserva `'código'` no modo Visual | REQ-090 | RM-F7-02 |
| OBS-03 | No celular, produzir consultando outra nota é penoso (English Translation): "Fixar em painel" com o trecho selecionado no topo enquanto o editor continua | feature | — | REQ-091 | RM-F7-14 |
| OBS-04 | Tags com estilização customizável (`#Todo:` vira bloco/citação/estrutura configurável) | feature | — | REQ-092 | RM-F7-15 |
| OBS-05 | Templates de nota reutilizáveis (ex.: ficha de personagem) | feature | — | REQ-093 | RM-F7-16 |
| OBS-06 | Relógio/cronômetro/timer nativo no editor | feature | **[F]** as capturas mostram o Assistente gerando o plugin `cronometro.js` (o mecanismo de plugins já permite; questão: nativo × plugin) | REQ-094 | RM-F7-17 |
| OBS-07 | Ferramentas do editor "bugadas": clicar ativa, às vezes não desativa; a visualização desconecta do estado real | bug | [I] estado dos botões não deriva da formatação sob o cursor; a reproduzir em E2E | REQ-095 | RM-F7-03 |
| OBS-08 | Aba de notas deve se chamar Explorer, com todos os tipos de arquivo (extensão/ícone); selecionar pasta não mostra opções; segurar e arrastar não faz nada — "disfuncional" | bug + feature | [I] a reproduzir em E2E móvel (toque longo/arrastar) e desktop | REQ-096 | RM-F7-04, RM-F7-05, RM-F7-06 |
| OBS-09 | NPCs somem de repente; deveriam viver (buscar água, cortar madeira, plantar…) | bug + feature | [I] a reproduzir com simulação longa determinística | REQ-097, REQ-098 | RM-F7-07, RM-F7-18 |
| OBS-10 | Eventos frequentes demais; o arco-íris desagrada | tuning | — | REQ-099 | RM-F7-08 |
| OBS-11 | No zoom máximo, mostrar o nome da pasta-mãe (raiz) para orientação | feature | — | REQ-100 | RM-F7-19 |
| OBS-12 | Visual da cidade em zoom (contorno atrapalha → muralha/baixa opacidade; estruturas feias, falta decoração); menu "+" com opções duplicadas → posicionar construção / desenhar região / importar / decoração, tipo opcional; contorno configurável; construções por extensão; editar o visual da construção | design + feature | **[F]** o "+" tem hoje duas rotas para a mesma ação (nova nota/construir e pasta/desenhar) — a confirmar no E2E | REQ-101, REQ-102, REQ-103, REQ-104 | RM-F7-20…RM-F7-23 |
| OBS-13 | Composições são páginas HTML no fundo: remover e delegar ao editor de páginas | mudança de escopo | **[F]** `.urbe/compositions.json` (`composition/*`) tem dados de usuário (DATA-CATALOG) | REQ-105 | RM-F7-24, RM-F7-25 |
| OBS-14 | "Voltar" no editor leva ao mundo, não ao explorador | bug | **[F]** `closeFullEditor` (`app.js:1396`) só fecha o editor e limpa `currentFile`; não restaura a origem | REQ-106 | RM-F7-09 |
| OBS-15 | Barra de ferramentas mais próxima do teclado no mobile | UX | — | REQ-107 | RM-F7-10 |
| OBS-16 | Tutorial não deve "spawnar" no mundo: painel dedicado (texto/imagens, navegação, busca) aberto em Configurações → Tutorial | design + feature | **[F]** hoje o Tutorial cria 46–48 notas/casas no vault (`tutorial.js`) | REQ-108 | RM-F7-26, RM-F7-27 |
| OBS-17 | Criar nota digitando `[[nova nota]]` no editor não funciona | bug/feature | **[F]** clicar em link para nota inexistente só mostra o aviso "A nota vinculada não existe." (`app.js:966`); não oferece criar | REQ-109 | RM-F7-11 |

## Decisões que dependem do proprietário
Ver `OPEN-DECISIONS.md` (OD-12..OD-15): formato de armazenamento dos comentários (OBS-01); cronômetro nativo × plugin (OBS-06); remoção das Composições e migração dos dados (OBS-13, destrutivo); ordem/escopo dos itens de design da cidade (OBS-12).

## Ordem de execução [P]
1. **Bugs primeiro** (OBS-02, 07, 08, 09-sumiço, 10, 14, 15, 17): reproduzir em E2E → corrigir → teste de regressão (RM-F7-01…11). Não dependem de decisão.
2. **Features de editor** (OBS-01, 03, 04, 05, 06).
3. **Cidade** (OBS-09-atividades, 11, 12).
4. **Escopo** (OBS-13 Composições, OBS-16 Tutorial), com ADR e migração de dados.
