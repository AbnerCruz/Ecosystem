# Páginas mais robustas — plano (UC-62)

> **Estado:** aprovado pelo proprietário em 23/10/2026 (DEC-0055, alternativa A: o plano inteiro, nas quatro fatias).
> **Autoridade:** o escopo e o andamento ficam no [ROADMAP C#](ROADMAP.md) (UC-62); este documento só descreve o plano.

## O pedido (observação de 16/10/2026)

O estúdio de páginas deve ser uma extensão do editor de notas e do Explorer. As ferramentas devem ser flexíveis e completas, e os modelos devem derivar delas, não o contrário. O estúdio deve ser inteligente com o vault e as notas.

## Como está hoje (fatos observados no código)

- **Motor:** o motor de páginas é o da 1.8.4, portado com oráculos (`Urbe.Core/Pages`, `engine.js`, `free.js`).
- **Seções:**
  - Há seções prontas (capa, citação, cartões…).
  - Há seções **livres**, montadas com nós: caixa, título, texto, imagem, botão, lista, citação, ícone, divisória, espaço, vídeo, tabela, código, fórmula, selo, incorporação, quebra de página, HTML.
  - O nó **nota** mostra uma nota inteira do vault.
- **Modelos** (`PageTemplates`): especificações fixas da 1.8.4, aplicadas por substituição, por acréscimo ou só como tema.
- **Ligação com o vault:**
  - A seção **Notas** da 1.8.4 já lista notas por pasta (com as subpastas), tag, escolha ou mais recentes, em cartões, lista ou grade, com resumo, calculada a cada render. (Correção de 23/10/2026: a primeira versão deste plano dizia que não havia bloco assim.)
  - Faltam: listar pelas ligações (quem liga a uma nota, a quem ela liga), por busca de texto, e uma pasta sem as subpastas.
- **Edição pela prévia:** o texto de títulos e textos já se edita tocando na prévia (`PreviewMessage` "text"). Os outros campos ficam no painel **Editar**.
- **Abrir:** abre-se uma página pela lista de Páginas. Uma nota ou uma pasta do Explorer não tem "Criar página daqui".

## O plano, em quatro fatias

Cada fatia tem teste e entra sozinha. Os oráculos da 1.8.4 ficam intactos: as novidades ficam atrás de opções do motor, como `UrbeMarks` hoje, e uma página da 1.8.4 abre e exporta igual.

1. **Blocos ligados ao vault (feito em 23/10/2026).**
   - Em vez de um nó novo, a seção **Notas** que já existe ganhou as consultas que faltavam:
     - **Ligam a uma nota**: as notas que têm `[[ela]]`.
     - **Ligadas por uma nota**: os `[[links]]` dela.
     - **Busca**: título ou texto, sem diferenciar maiúsculas.
     - **Incluir subpastas** na origem Pasta.
   - Ordem, limite, aparência, resumo e "texto completo abaixo" são os da 1.8.4.
   - A consulta fica na página (`.page.json`) e o resultado é calculado a cada render, então a página acompanha as notas (NN-001).
   - Só com as marcas do Urbe ligadas (`UrbeMarks`): os oráculos da 1.8.4 não mudam, e uma origem nova vista sem elas volta ao padrão da 1.8.4 (Pasta).
2. **Abrir a partir do Explorer (feito em 23/10/2026).** No ⋯ de uma pasta, no cartão de uma nota e no Mais ações do editor.
   - Segurar uma nota ou pasta → **Criar página daqui**.
   - De uma nota: uma página com a nota como conteúdo (nó **nota**).
   - De uma pasta: uma página com a capa e uma **Lista de notas** daquela pasta.
   - A página é gravada pela gravação verificada, em `Páginas/`.
3. **Modelos como combinações salvas (feito em 23/10/2026).** Já existia da 1.8.4: **Salvar como modelo** grava `Páginas/Modelos/<nome>.template.json`, e os modelos da pessoa aparecem ao lado dos prontos. Esse lugar fica, para a 1.8.4 continuar lendo os modelos. Entrou a escolha **Com os textos** ou **Só a estrutura**, que volta os textos ao padrão e mantém seções, aparência, ícones e o tamanho das listas (`PageStudio.StructureOnly`).
   - Os blocos (seções e nós) passam a ser as ferramentas. Um modelo vira uma combinação salva deles.
   - **Salvar como modelo** grava a página atual (sem o conteúdo de texto, ou com ele, à escolha) em `Personalização/modelos-de-pagina/`, no vault, como os temas.
   - A lista de modelos mostra os da 1.8.4 e os da pessoa lado a lado.
   - Os modelos da 1.8.4 continuam iguais (oráculo).
4. **Editar tudo pela prévia (feito em 23/10/2026, menos a imagem).** Os textos de uma linha sem Markdown de todas as seções (títulos, rótulos, itens) se escrevem no lugar na prévia (`PageEditMarks`, só com `UrbeMarks`). Textos com Markdown ou comentários continuam no painel Editar. Falta: a imagem tocada abrir a escolha de imagem; hoje o toque abre o painel Editar da seção.
   - Todo texto da página se edita tocando na prévia, inclusive itens de lista, legendas, botões e citações, não só títulos e parágrafos.
   - Uma imagem tocada abre a escolha de imagem. O painel **Editar** continua para o resto.

## Fora do plano

- Um segundo motor de páginas.
- Mudar o formato `.page.json` de forma incompatível com a 1.8.4.
- Qualquer coisa da simulação (ADD-0028).
- Páginas que se atualizam sozinhas em segundo plano. O cálculo é sempre no abrir, na prévia ou na exportação.

## Riscos e custo

- **Desempenho:** a consulta da **Lista de notas** num vault grande usa o índice que já existe (`KnowledgeIndex`/`WorkspaceIndex`) e um limite de itens.
- **Compatibilidade:** uma página com **Lista de notas** aberta na 1.8.4 mostra o nó como desconhecido. Isso fica documentado no tutorial.
- **Tamanho:** quatro fatias de tamanho parecido com o do UC-35; a 1 e a 2 primeiro.
