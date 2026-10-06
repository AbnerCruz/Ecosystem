# Proposta de Product — autoria matemática temporal

> Natureza: proposta de constituição, não Product registrado nem implementação.
> `Math Studio` / `math-studio` são somente codinomes documentais.
> Identidade permanente: DEC-0040. Constituição: ADR-0029. Documento: ADR-0030.
> Origem: delegação do proprietário `ecosystem-math-studio-prompt-2.md`, recebida em 2026-10-06.

## Intenção já expressa pelo proprietário

Criar um Product nativo independente dentro de `AbnerCruz/Ecosystem`: uma IDE/engine
de autoria matemática temporal. Professores, estudantes, autores de cursos e
divulgadores devem combinar relações matemáticas, composição visual, cenas,
timeline e saída audiovisual. O diferencial é produzir complexidade visual alta
a partir de poucas relações matemáticas e intenções temporais.

Não é uma feature do Hub/Urbe/Lunet2D, clone de Manim ou editor genérico de vídeo.
A intenção está definida na delegação; a identidade permanente não foi definida.
A aprovação da DEC-0040 legitima a constituição proposta e o ID escolhido, sem
inventar um nome público. Um nome de exibição explicitamente provisório pode
representar o domínio até uma decisão de marca. Não usar o codinome como ID.

## Requisitos e evidências

Os IDs `MA-REQ-*` identificam requisitos desta proposta, não o futuro Product.
Após a constituição, os requisitos migram com os mesmos IDs para sua SPEC local.

| ID | Requisito | Evidência exigida |
|---|---|---|
| MA-REQ-001 | Product independente, C#, local-first, utilizável e distribuível sem Hub, conta, rede ou IA | Grafo sem referências a outros Products; teste headless; instalação e uso offline sem Hub |
| MA-REQ-002 | Um documento canônico versionado para projeto, cenas, semântica, apresentação, timeline e assets | Mesmo estado após operações visuais/textuais, save/load e undo/redo; nenhuma sincronização heurística |
| MA-REQ-003 | Expressões estruturadas, funções, variáveis e dependências reativas determinísticas | Mudança em Δx invalida B/reta/inclinação; ciclos, referências inválidas e domínio matemático produzem erros estruturados |
| MA-REQ-004 | Scene graph genérico com transformações, grupos, ordem visual, estilos e câmera | Render de projetos diferentes e hit testing; demo não contém branch por ID de objeto/cena |
| MA-REQ-005 | Estado temporal calculável em t sem frame anterior | Seek fora de ordem e export sequencial produzem o mesmo estado; interpolação/easing/keyframes testados |
| MA-REQ-006 | Undo/redo transacional desde a fundação | Criação/remoção, edição de variável, texto e timeline voltam ao estado anterior sem apagar assets referenciados |
| MA-REQ-007 | Persistência local robusta e portável | Save atômico, autosave recuperável, process death, round-trip, migração e import/export sem caminhos absolutos |
| MA-REQ-008 | Editor profissional mobile-first adaptável a desktop | Seleção por toque, handles, pan/pinch, inspector e timeline em painéis recolhíveis; DEVICE no Android |
| MA-REQ-009 | Autoria textual segura sobre o mesmo documento | Parser/formatter determinísticos, source spans, erros sem commit parcial; alterações visuais reaparecem no texto |
| MA-REQ-010 | Tipografia matemática nítida com layout estruturado e funcionamento offline | Medição de glyphs/baselines, fórmulas vetoriais/offscreen; nenhuma imagem de fórmula como autoridade |
| MA-REQ-011 | Export de imagem e MP4 H.264 real, cancelável e fora da UI thread | PNG real e MP4 com bytes, metadados, decode de frames, seek/reprodução Android e hashes do candidato |
| MA-REQ-012 | Δx → 0 produzido pelo runtime/editor genérico | Todos os passos do aceite abaixo, com projeto salvo e reaberto; nenhum vídeo pré-gerado ou renderer externo oculto |
| MA-REQ-013 | Imports são dados não confiáveis, não execução arbitrária | Limites de bytes/nós/profundidade/assets, validação de tipos/refs/valores, proteção contra path traversal/zip bombs |
| MA-REQ-014 | Preview responde em tempo real e preserva a semântica do export | Medições de frame time/alocações/avaliações na build e aparelho identificados; invalidação incremental verificável |
| MA-REQ-015 | Lifecycle, CI, versão e roadmap próprios | VERSION único, pipelines seletivos, boundaries e consistency verdes; milestones encerrados somente com evidências |

## Autoridade de estado

`ProjectDocument` é a autoridade dos dados autorais. Contém `schemaVersion`, ID,
metadata, assets, variáveis, cenas e perfis de saída. Cada cena contém scene graph,
math graph, bindings, câmera e timeline. IDs de objetos permanecem estáveis em
renomeação e round-trip. As duas visões editam por operações/transações do mesmo
documento; texto em digitação é um rascunho com revisão-base explícita, não uma
segunda cena autoritativa. Parse válido e validação completa precedem commit.

| Conceito | Autoridade | Projeções descartáveis |
|---|---|---|
| Projeto e revisão atual | ProjectDocument publicado pelo controlador de transações | UI, hierarquia, inspector, representação textual formatada |
| Matemática e dependências | Nós estruturados e referências do documento | Plano compilado, ordem topológica, valores e dirty sets |
| Animação | Tracks/keyframes/clips do documento | Estado avaliado em t, playhead e preview |
| Assets | Catálogo do documento + bytes locais identificados por ID/hash | Thumbnails, caches de decode e upload futuro |
| Arquivo salvo | Revisão validada persistida com manifesto e assets | Autosave/journal identificados como recuperação e associados à revisão |
| Vídeo/imagem | Derivados de uma revisão congelada + perfil de export | Arquivo de saída nunca substitui o projeto editável |

Separar matemática, apresentação, tempo, render, persistência e UI em módulos
locais. Engine e ferramentas são C#. Nenhuma pasta global `shared/common/core`,
nenhuma extração preventiva, nenhuma dependência Product → Product.

## Fundação semântica e visual

Primeiro subconjunto: Variable/Constant, MathExpression (AST), Function 1D,
Point, Line/Segment, CoordinatePlane/Axis, FunctionPlot, Annotation e grupos.
Scene nodes referenciam resultados matemáticos via bindings tipados; coordenadas
matemáticas não são pixels. Registrar avaliadores por tipo dentro do Product,
evitando um switch central crescente. Equation/Inequality, vetores, ângulos,
matrizes, sequências, datasets, curvas paramétricas e geometria mais rica evoluem
sem exigir que todo o catálogo seja implementado no primeiro slice.

AST e identidade de termos permitem futuras transformações fatorar/expandir,
simplificar, derivar/integrar, substituir, resolver/isolar e transformar matrizes.
Não prometer CAS completo nem equivalência geral no primeiro corte. Avaliação,
dependências, layout, render e timeline nunca dependem de IA. Extensões C#
arbitrárias, se vierem, têm fronteira de confiança própria e não são serializadas
como projeto seguro nem reescritas silenciosamente pelo editor visual.

Scene graph cresce para clipping/masks, vetores, texto/fórmulas, câmera, guias,
snap, seleção/hit testing e handles. Timeline cresce para múltiplas cenas,
transições, áudio/narração, música/SFX/legendas e correspondência de termos.
Apresentação e experiências interativas são consumidores futuros do mesmo modelo.

## Aceite obrigatório: Δx → 0

Cena autoral: `f(x)=x²`, `x₀=1`, `A=(x₀,f(x₀))`, `dx=2`,
`B=(x₀+dx,f(x₀+dx))`, `secante=line(A,B)` e
`slope=(f(x₀+dx)-f(x₀))/dx`. Animar dx de 2 até ε positivo,
com ε inicial proposto 0.01. B aproxima-se de A; Δx é marcado geometricamente;
secante/inclinação reagem; há entrada/saída de texto e animação de câmera.

Para esta função, slope = 2 + dx e a secante tem intercepto -1 - dx.
Em dx=0.01, slope=2.01; isso é aproximação, não tangente exata. O limite é 2 e
a tangente exata é `y=2x-1`. Em dx=0, a divisão original é indefinida e os pontos
coincidem: retornar diagnóstico explícito, nunca NaN desenhado ou falso resultado
2 por epsilon oculto. A tangente pode ser um objeto autoral distinto construído
da expressão `2x-1`, exibido na composição final com rótulo adequado.

O proprietário precisa conseguir no próprio aplicativo:

1. Criar/abrir projeto e montar ou carregar a cena autoral real.
2. Dar play, pausar e buscar tempos arbitrários; acompanhar B, Δx e secante.
3. Alterar Δx, duração/keyframes e propriedades e ver reação imediata.
4. Abrir a visão textual; editar e observar o mesmo documento; fazer edição
   visual e verificar o texto da mesma revisão.
5. Salvar, fechar e reabrir sem perda semântica, visual, temporal ou de assets.
6. Exportar MP4 H.264, perfil-alvo 1280×720/30 fps, e reproduzir/buscar no Android.
7. Conferir tutorial curto e arquivos/projeto/hash/build exatos da evidência.

Vídeo pronto em assets, Manim/Python como renderer, screenshots encadeadas,
demo hardcoded e botão de export fictício não satisfazem o aceite.

## UX e saídas

No celular, Stage ocupa a área principal. Hierarquia, inspector, timeline,
texto, assets e storyboard abrem como drawers/painéis. Controles têm seleção,
drag/handles, pan/pinch, snap/multi-select, sliders, inserção rápida e scrubbing;
desktop pode manter painéis simultâneos sobre as mesmas operações.

Saídas progressivas: projeto local versionado; PNG e vetor quando adequado;
MP4/H.264; Presenter com pausa/variáveis; interatividade exportável. Áudio e
edição audiovisual servem à explicação matemática. IA futura pode sugerir cenas,
storyboard, transformações, narração e exercícios por operações no mesmo modelo,
sem novo formato ou dependência direta do Ecosystem AI.

## Não escopo inicial

Backend/login/nuvem, colaboração/multiplayer, 3D, CAS completo, marketplace,
API executável de plugins, editor genérico de vídeo e extração para plataforma.
Nenhum roadmap de outro Product é alterado para viabilizar esta proposta.

## Fontes e continuidade

[Roadmap proposto](ROADMAP.md), [plano técnico e auditoria](IMPLEMENTATION.md),
[ADR-0029](../../adr/0029-product-de-autoria-matematica-temporal.md),
[ADR-0030](../../adr/0030-documento-canonico-de-autoria-matematica.md),
[decisões](../../governance/decisions.json), Issue
[#260](https://github.com/AbnerCruz/Ecosystem/issues/260).

Não há aceite funcional nesta proposta. A missão completa continua até o slice
implementado/validado ou um gate humano real; este pacote prepara o primeiro gate.
