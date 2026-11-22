# LUNET-520-T01 — estudo e validação da trilha inicial

Data: 22/11/2026. Base: Ecosystem main c787b843092e161a5eb0a27bff163b8b1164ab8c. Mandato: estudar antes de desenvolver, implementar 10 missões dos cursos LRN-001 a LRN-005, sem liberar T02.

## Auditoria interna (três frentes)

**Framework:** Game.Update/Draw, SpriteBatch, InputState, VirtualStick, TouchId, RectangleF, GameTime.DeltaSeconds e recursos de texto/áudio já existem. As APIs estão documentadas em docs/guides/primeiros-passos.md, entrada.md, colisao.md e loop-e-tempo.md. LRN-004-P2 depende de VirtualStick.Update(Input, excludedTouchId) e Cancel(); ProjectStore permite orientação landscape. Não é necessário criar outra engine.

**IDE e Aprender:** LearningCatalog disponibilizava somente LRN-001-P1; MainActivity.Learn mostrava somente quatro etapas. LearningProgressStore V1 armazenava um único GameId/nome, inviabilizando dez progressos independentes. É necessário V2 com migração V1 sem perda, escrita atômica, IDs fixos e validação de dados antes de abrir. Projeto de curso deve continuar sendo pasta comum Lunet com Game.cs em branco, inspecionável e independente da aba.

**Studio:** A primeira trilha deve ser realizável sem Studio, download, IA ou rede. Assets criados em Studio são opcionais e seguem C# como fonte da verdade (ADR 0007). Não introduzir lógica de jogo oculta.

## Pesquisa externa e comunidades

- Godot: https://docs.godotengine.org/en/stable/getting_started/first_2d_game/ — sequência progressiva com loop de jogo completo; não reproduzir código ou estrutura de cenas.
- Unity Create with Code: https://learn.unity.com/course/create-with-code — protótipos, desafios e projeto próprio em C#; adaptar progressão e transferência, sem exigir mídia ou login.
- GDevelop Teacher Guide: https://wiki.gdevelop.io/gdevelop5/education/teacher-guide/ — missões práticas sucessivas e conclusão com jogo autoral; evitar receitas prontas.
- Pesquisa de scaffolding: https://journals.sagepub.com/doi/10.1177/07356331231174454 — apoio graduado; IA deve dar pistas sob pedido em vez de completar o trabalho.
- Android offline-first: https://developer.android.com/topic/architecture/data-layer/offline-first — base pedagógica e estado local sem rede.
- Comunidades: https://www.reddit.com/r/godot/comments/1qotw27/one_step_forward_from_the_tutorial_hell/ e https://www.reddit.com/r/godot/comments/1w0v9gv/what_exercises_or_projects_i_should_do_if_the/ — relatos qualitativos contra tutorial hell e a favor de pequenos projetos próprios; não representam estatística.

## Confronto crítico

**Incorporar:** dez missões independentes com enunciado, quatro etapas, critério de aceite no Preview, referências offline, desafio extra de transferência, navegação por curso e progresso por GameId.

**Adaptar:** esquema de progresso V2 migrado a partir de V1; orientação horizontal só no desafio de dois controles; ajuda por IA como pista opcional no projeto vinculado.

**Rejeitar:** dez jogos já resolvidos (contrário a "se vira e aprende"), certificados automáticos por checkbox, jogos fictícios para armazenar progresso, liberação dos cursos 006–060 antes do conteúdo real, alterações silenciosas em Game.cs.

## Gates verificáveis

Exatamente 60 cursos, 12 trilhas, 5 cursos publicados e 10 projetos T01. Cada projeto possui quatro etapas e documentação local. Persistência independente, migração V1, corrupção preservada e nenhum código do aluno alterado; testes xUnit. Compilação .NET e Android e release somente pela CI canônica. Sem evidência DEVICE nesta implementação; nunca chamar um checkbox de avaliação automática. Projeto final LRN-005-P2 exige jogo autoral jogável fora de Aprender.

## 6. Reavaliação UX: missão desaparecia ao abrir o editor

Relato do proprietário em 22/11/2026: abrir missão levava ao Game.cs e escondia os objetivos, obrigando o usuário a abandonar a produção para recuperar instruções. Auditoria do código confirmou o acoplamento: ShowLearningProject possuía checkboxes apenas na tela do launcher, enquanto ShowWorkspace oferecia Problemas/Console/Profiler, sem superfície pedagógica persistente. A solução não é duplicar páginas no editor, mas conectar o mesmo LearningProgressStore e a mesma LearningCatalog ao painel acoplável existente.

**Pesquisa e confronto:** o editor atual já dispõe de WorkspaceLayout, dock adaptativo Bottom/Right, painel redimensionável, abas salvas e Preview. As diretrizes Android de navegação e offline-first já pesquisadas sugerem manter conteúdo local e reduzir trocas de contexto; a experiência de tutoriais de Godot/Unity mantém objetivos próximos à produção, mas o Lunet não deve copiar suas telas nem substituir o código real por tutoriais com solução.

**Decisão:** botão Aprender somente quando GameId associa desafio; abrir esse painel automaticamente ao partir de um curso; registrar progresso na mesma store, mostrar referências/rubricas e permitir voltar a Console/Problemas. A seleção de missão é explícita quando GameId vincula vários desafios. LayoutStore aceita PanelTab=learn; compilação com erro abre Problemas; Preview bem-sucedido restaura Aprender após saída. A fileira de abas passa a permitir scroll horizontal. Testes de layout e resolução de GameId precederam a mudança. Validar no Android 13 sem afirmar DEVICE.
