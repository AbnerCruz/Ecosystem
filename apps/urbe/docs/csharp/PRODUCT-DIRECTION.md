# O que é o Urbe — direção do cliente C#

> **Autoridade:** DEC-0051 / [ADD-0028](../../../../docs/governance/addenda/ADD-0028-urbe-sem-simulacao-visual.md) e [ADR-0034](../../../../docs/adr/0034-urbe-sem-simulacao-visual.md) (ordem do proprietário, 2026-10-22), sobre DEC-0043 / [ADD-0020](../../../../docs/governance/addenda/ADD-0020-urbe-e-a-1-8-4-nativo-sem-web.md) e [ADR-0032](../../../../docs/adr/0032-urbe-ui-nativa-sem-webview.md). Escopo e estado dos itens: [`ROADMAP.md`](ROADMAP.md). Este documento descreve o produto; não registra andamento.

## 1. O Urbe são as suas notas

O Urbe guarda **as suas notas em Markdown numa pasta real** do aparelho. Cada nota é um arquivo `.md` comum, que qualquer outro programa abre.

**A tela do Urbe é o Explorer.** Ele mostra as pastas, as notas e os outros arquivos do vault. O resto abre por cima dele:

| Peça | O que faz |
|---|---|
| Explorer | Árvore de pastas e arquivos; busca; seleção múltipla; mover, agrupar, renomear, duplicar e mandar para a lixeira |
| Editor | Visual e Fonte, barra de Markdown, propriedades, estatísticas, sugestão de `[[link]]`, abas, versões anteriores |
| Busca e paleta | Abrir nota pelo nome ou texto; todos os comandos do Urbe |
| Assistente | Agentes de IA que pesquisam, escrevem e organizam o vault |
| Páginas | Sites, páginas e livros feitos a partir das notas |
| Configurações | Personalização, plugins, backups, importação, exportação, tutorial |

Na abertura, o app volta direto para a última nota que estava aberta.

**A simulação visual saiu do produto** (DEC-0051). O mundo procedural, a cidade (notas como casas, pastas como bairros, links como ruas) e a vida (moradores, fauna, clima) eram a interface da 1.8.4-beta e da beta 2.0 até a beta.14. Na prática, ficaram pesados e limitados por espaço. O estado com a simulação está congelado no ramo `urbe-simulacao-congelada`, até o proprietário decidir como substituí-la. Nenhuma parte dela volta sem nova ordem dele.

**A 1.8.4 continua referência fora da simulação.** Isso vale para as regras do editor, do Explorer, da lixeira, do histórico, da importação, das páginas e do assistente. Mas ela é referência de essência, não de defeitos (DEC-0045, [ADD-0022](../../../../docs/governance/addenda/ADD-0022-urbe-1-8-4-como-essencia-e-observacoes-de-uso.md)). O que o proprietário apontou como defeito ou aprovou como melhoria nas observações de uso é corrigido, não reproduzido.

**Dados.** Id, datas e pastas das notas ficam em `.urbe/notas.json`. Os arquivos da cidade num vault antigo (`.urbe/mapa.json`, `.urbe/mundo.json`) são lidos uma vez e nunca apagados nem reescritos.

## 2. Tecnologia

C# nativo em **Android e Windows**, sem WebView, HTML ou CSS como interface (ADR-0032). Android vem primeiro. Não há versão Web/PWA do cliente C#.

O domínio fica em `Urbe.Core`: vault, notas, Markdown, editor visual, páginas e livros, matemática, assistente. A interface fica em `Urbe.Client` (Avalonia), com os heads `Urbe.Android` e `Urbe.Desktop`. A casca Blazor antiga foi retirada (UC-34).

A experiência de usuário segue [`UX.md`](UX.md) (ADD-0026) e o desempenho vem primeiro (ADD-0024).

## 3. Para onde o Urbe vai (direção do proprietário)

Nas palavras do proprietário, o Urbe não deve ser só "um editor de texto como o antigo", mas **"um criador e produtor de artefatos que funciona em cima de todo o sistema de edição de texto"**. O **editor Visual** é a peça central dessa produção.

Na prática, os artefatos são o que o `Urbe.Core` já sabe produzir a partir das notas: **páginas, sites e livros** (blocos, layout livre, modelos, temas), **composições** prontas para imprimir e **documentos com matemática**. Cada melhoria entra como item próprio do roadmap.

Decisões de 13/10/2026 (DEC-0045):

- recursos novos de escrita (comentários, tags estilizadas, blocos aninhados) se expressam como **sintaxe Markdown** no próprio arquivo;
- **composições passam a ser páginas**, sob o editor de páginas;
- modelos de nota e componentes reutilizáveis são **um conceito só**.

A ideia de "Urbe como sistema operacional" (cada pasta como um ambiente de trabalho) é **exploração**, a ser provada pelo uso.
