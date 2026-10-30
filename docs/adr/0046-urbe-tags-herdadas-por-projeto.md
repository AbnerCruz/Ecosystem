# ADR-0046 — Tags herdadas por projeto no Urbe

## Status

Aceito — DEC-0058, alternativa A, escolhida pelo proprietário no portal em 30/10/2026 ([Issue #859](https://github.com/AbnerCruz/Ecosystem/issues/859), [resposta](../governance/responses/DEC-0058.md)). O número 0045 fica reservado para a proposta de documentos computacionais citada na consolidação Numa (UC-92).

## Contexto

FATO OBSERVADO: FeaturesCheck do proprietário, item "Tags herdadas por projeto", e CST-042 da consolidação Numa. O pedido diz que um projeto (uma pasta do topo, por exemplo `Droerni`) poderia definir `#worldbuilding`, herdada por tudo o que está dentro dele, para catalogar além da navegação por pastas. Também pede que se investigue se propriedades gerais podem ser herdadas da mesma forma. Critério da CST-042: "o contrato define herança, sobrescrita, remoção e movimento entre projetos; consulta e interface exibem a origem efetiva".

Hoje as tags de uma nota vêm só do texto dela (`#tag` e `tags:` no frontmatter). Projetos não têm tags. O `.urbe/area.json` guarda apenas a aparência das caixas (DEC-0053), e as notas são Markdown portátil, lido por outros editores.

Pesquisa: no Obsidian, a herança por pasta existe só em plugins, e eles **copiam** as tags da pasta para o frontmatter de cada nota ao criar ou mover. É o caso de Folder to Tag, Ishibashi Auto Tagger e Folder auto properties. O plugin Folder Notes, por sua vez, associa uma nota a cada pasta (a nota com o nome da pasta, dentro dela).

## Problema

Onde as tags de um projeto ficam e como elas chegam às notas sem duas autoridades (NN-001), sem reescrever arquivos do usuário sem necessidade e mantendo as notas legíveis fora do Urbe.

## Opções

1. **Herança calculada a partir da nota da pasta (recomendada).** As tags (e, depois, as propriedades) do frontmatter da nota que tem o nome da pasta, dentro dela (`Droerni/Droerni.md`), valem para tudo o que está na pasta e nas subpastas. O Urbe calcula a herança pela localização e não escreve nada nas outras notas. Os pontos do contrato:
   - **Subpastas:** somam as tags das próprias notas de pasta.
   - **Sobrescrita e remoção:** uma nota recusa toda a herança com `herdar: false` no frontmatter, ou só algumas tags com `sem-tags: [worldbuilding]`.
   - **Movimento:** mover uma nota muda as tags herdadas na hora, sem tocar em arquivos.
   - **Consulta e interface:** a busca por tag, os filtros da Área de trabalho e o Assistente incluem as tags herdadas; a nota mostra cada uma com a origem ("#worldbuilding · de Droerni"), separada das próprias.
   - A autoridade é uma só: a nota da pasta.
2. **Copiar as tags para cada nota, como os plugins do Obsidian.** As tags aparecem em qualquer editor, mas ao criar ou mover uma nota o Urbe reescreve o frontmatter dela. Uma mudança no projeto reescreve todas as notas dele, e é difícil separar o que veio do projeto do que a pessoa escreveu (a remoção ao sair do projeto pode apagar uma tag legítima).
3. **Tags guardadas na caixa do projeto (`.urbe/area.json`), calculadas.** Nada é reescrito, mas as tags não são portáteis (somem fora do Urbe), e o `area.json` deixa de ser só aparência.

## Decisão

Opção 1: herança calculada a partir da nota da pasta (DEC-0058, alternativa A). O contrato implementado:

- **Nota da pasta:** a nota com o nome da pasta, dentro dela (`Droerni/Droerni.md`). Só as tags do **frontmatter** dela valem para o projeto (`tags: [a, b]` ou uma por linha com `-`); um `#tag` no corpo da nota da pasta é só dela. Se duas notas têm o nome da pasta (extensões diferentes), vale a de caminho mais curto, como na resolução de links.
- **Alcance:** tudo o que está na pasta e nas subpastas. Subpastas somam as tags das próprias notas de pasta, de fora para dentro. A nota da pasta não herda de si mesma, só das pastas acima.
- **Sobrescrita e remoção:** `herdar: false` recusa toda a herança; `sem-tags: [worldbuilding]` (lista, uma por linha, ou texto) recusa algumas. Uma tag que a nota já tem é dela, não aparece como herdada.
- **Movimento:** o índice recalcula a herança pela localização a cada mudança do vault; mover, renomear ou mudar a nota da pasta não escreve em nenhuma outra nota.
- **Consulta e interface:** `find_by_tag`, `read_note` e `get_links` do Assistente, os filtros por tag da Área de trabalho e as consultas por tag das páginas usam as tags próprias e as herdadas. O formulário de propriedades mostra **herdadas**, cada uma com a origem (`#worldbuilding` de Droerni); o × dela escreve `sem-tags` na nota (reversível).
- **Autoridade:** a nota da pasta. `ProjectTags` (`Urbe.Core`) é a projeção calculada, sem cache persistido.

Propriedades herdadas (além das tags) ficam para uma segunda fatia, depois da validação das tags no aparelho.

## Consequências

Com a opção 1, a implementação fica no `Urbe.Core`: o índice de tags ganha a origem (própria ou herdada, com o caminho da nota da pasta). Consultas, filtros e o Assistente leem esse índice, e nenhum formato de arquivo muda. A nota da pasta continua sendo uma nota comum. As propriedades herdadas usam o mesmo mecanismo numa segunda fatia, depois da validação das tags.

## Alternativas rejeitadas

- **Copiar as tags para cada nota (opção 2):** reescreveria o frontmatter das notas ao criar e mover, e a remoção ao sair do projeto poderia apagar uma tag escrita pela pessoa.
- **Tags no `.urbe/area.json` (opção 3):** não são portáteis e misturariam conteúdo com a aparência das caixas (DEC-0053).

## Referências

- CST-042 e FeaturesCheck "Tags herdadas por projeto";
- NN-001 (uma autoridade só), NN-011 (modelo de dados exige decisão);
- DEC-0053 (Área de trabalho, `area.json` só com aparência);
- `apps/urbe/docs/csharp/CONSOLIDATION.md`.
