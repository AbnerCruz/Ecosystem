# O que é o Urbe — direção do cliente C#

> **Autoridade:** DEC-0043 / [ADD-0020](../../../../docs/governance/addenda/ADD-0020-urbe-e-a-1-8-4-nativo-sem-web.md) (ordem do proprietário, 2026-10-10) e [ADR-0032](../../../../docs/adr/0032-urbe-ui-nativa-sem-webview.md). Escopo e estado dos itens: [`ROADMAP.md`](ROADMAP.md). Este documento descreve o produto; não registra andamento.

## 1. O Urbe é a 1.8.4-beta

O Urbe é o aplicativo publicado como **1.8.4-beta** (`apps/urbe/`, JavaScript): **as suas notas em Markdown desenhadas como uma cidade**. Cada nota é uma casa, cada pasta é um bairro e cada `[[link]]` é uma rua. O mundo é um continente gerado proceduralmente (placas, serras, clima, rios), em pixel-art, com dia e noite, moradores e animais. Tudo continua sendo arquivo `.md` comum numa pasta real.

**A cidade é o aplicativo.** Ela ocupa a tela inteira; tudo o mais aparece por cima dela:

| Elemento da 1.8.4 | Onde (`apps/urbe/index.html`) | O que faz |
|---|---|---|
| Mundo | `#game` (canvas) | Cidade em tela cheia; arrastar move, pinça/roda dá zoom, toque seleciona |
| Barra de ferramentas | `#tools` | Selecionar, Região, Construir, Explorador, Cidades |
| Busca | `#busca` | Buscar nota e ir até a casa |
| Minimapa e mapa | `#mini`, `#mapao` | Visão geral; tocar no mapa leva até o lugar |
| HUD | `#hud` | Dica e contadores (notas, arquivos, regiões, conexões, zoom) |
| Painel da casa | `#housePanel` | Casa selecionada: prévia, metadados, abrir nota, mover, copiar, excluir |
| Explorador lateral | `#fileSidebar`, `#filePreview` | Árvore de arquivos, seleção múltipla, mover/agrupar/excluir, prévia |
| Editor sobreposto | `#editorFull` | Visual e Fonte, barra de Markdown, propriedades, estatísticas, sugestão de `[[link]]` |
| Assistente | `#aiPanel` | Agentes de IA sobre o vault |
| Cidades (vaults) | `#menuCidades` | Trocar de cidade/pasta |

**Regra para o cliente C#:** cada uma dessas peças é reproduzida com a mesma aparência e o mesmo comportamento, comparada lado a lado com a 1.8.4. Uma tela que não existe na 1.8.4 (por exemplo, um painel de "estado da migração" ou abas Início/Explorer/Editor/Cidade/Mais) não faz parte do Urbe até ser aprovada como melhoria.

## 2. Tecnologia

C# nativo em **Android e Windows**, sem WebView, HTML ou CSS como interface (ADR-0032). Android vem primeiro. Não há versão Web/PWA do cliente C#.

O domínio já portado em `Urbe.Core` é aproveitado: vault, Markdown, editor visual, páginas e livros, matemática e o motor de mundo da 1.8.4 (terreno, rios, vegetação, texturas, sprites, prédios). A casca Blazor (`Urbe.UI`, `Urbe.App`, `Urbe.Web`) está congelada e sai quando o cliente nativo a substituir.

## 3. Para onde o Urbe vai depois (direção do proprietário)

Nas palavras do proprietário, o Urbe não deve ser só "um editor de texto como o antigo", mas **"um criador e produtor de artefatos que funciona em cima de todo o sistema de edição de texto"**. O **editor Visual** é a peça central dessa produção.

Na prática, os artefatos são o que o `Urbe.Core` já sabe produzir a partir das notas: **páginas, sites e livros** (blocos, layout livre, modelos, temas), **composições** prontas para imprimir e **documentos com matemática**. A ordem é:

1. Primeiro, o Urbe que o proprietário usa hoje (a 1.8.4) funcionando em C# nativo.
2. Depois, a produção de artefatos como evolução, sobre o editor Visual e a cidade, cada melhoria como item próprio do roadmap.

A direção nova não troca a experiência da cidade por uma interface de "workspace". Ela acrescenta capacidade de produção a ela.
