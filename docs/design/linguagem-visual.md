# Linguagem de design do Ecosystem (v2.0)

**Escopo normativo:** todos os Products, ferramentas, portal, Hub e produtos futuros. Revisão por ordem explícita do proprietário de 28/11/2026; corrige a generalização indevida da primeira aplicação no Lunet (DEC-0069, ADD-0037). [Estudo de revisão](estudo-global-2026-11-28.md). [Tokens](tokens.json). [Vitrine interativa](https://abnercruz.github.io/Ecosystem/design/).

## 0. Objetivo e precedência

O sistema define uma gramática de interação, composição e acessibilidade, não uma skin obrigatória. A preferência do proprietário por preto, azul, branco e roxo é inspiração, não fronteira cromática. Não existe app-modelo universal. Um Product pode ter identidade, fontes, densidade, navegação e composição próprias. O conteúdo e a tarefa determinam o desenho.

Três níveis:
- **Obrigatório:** significado consistente dos papéis, acessibilidade, estados e feedback, previsibilidade, segurança e recuperação de dados, adaptação à janela e entrada, fonte declarada de tokens e evidência.
- **Padrão recomendado:** escalas compartilhadas, componentes convencionais, tema claro/escuro de referência, hierarquia por região, tipografia legível e movimento econômico. Ajustar com justificativa de uso.
- **Livre por domínio:** paleta, composição, arte, fontes de leitura/código, densidade, formato de painéis e navegação, visualizações, componentes especializados e temas adicionais. Não exigir ADR para cada composição local; registrar mudanças de contrato global com versão e decisão.

## 1. Arquitetura dos tokens

Separar **primitivos** (valores), **semânticos** (funções independentes de matiz) e, quando necessário, **componentes** (aliases específicos). Não criar um token de componente por capricho. Ex.: action → accent.9; textPrimary → neutral.12; danger → status.danger.11. O mesmo componente usa papéis, não hexadecimais de uma marca.

`tokens.json` é a única fonte dos valores compartilhados. O namespace `global` v2 define temas, papéis, acentos e densidades de referência; os campos da raiz v1 preservam o snapshot dos consumidores antigos, não constituem regras globais atuais. Escalas de espaço, raio, tipografia e movimento na raiz são referências reutilizáveis. O Product declara versão, seleciona um perfil e registra overrides locais e sua autoridade. Nenhuma dependência direta entre Products (NN-002); nenhuma extração de pacote sem NN-022. A vitrine projeta os tokens, não é fonte normativa.

## 2. Cor, contraste e identidade

- A cor comunica função antes de ornamentar. Marca, ação, seleção, links, informação, sucesso, aviso, erro e séries de dados são conceitos separados; podem coincidir cromaticamente quando não houver ambiguidade.
- Claro e escuro são perfis de referência, não inversões mecânicas. Preferir a escolha do usuário/sistema quando ambos existirem; outros modos, como alto contraste, OLED, sepia ou temáticos, são permitidos. Não impor dark mode nem proibir preto ou branco puros.
- Azul, violeta, verde-petróleo, âmbar e rosa são opções demonstrativas de acento; nenhum é imposto a um Product. A paleta de status continua semanticamente separada. Revalidar contrastes quando alterar matiz, fundo, opacidade ou estado.
- Distribuição cromática e 60/30/10 são heurísticas, não percentuais normativos. Múltiplos acentos são legítimos em dados, categorização, mapas, arte e estados, com uma hierarquia clara. Não usar apenas cor como significado.
- Gradiente, textura, sombra, borda, transparência e brilho são ferramentas. Usá-los conforme função, contraste, desempenho e identidade; não impor matiz nem proibir categorias inteiras.
- Gráficos e mapas: distinguir paletas categóricas, sequenciais e divergentes; evitar confundir dados com status; usar legendas, rótulos, padrões e formas quando necessário. Conteúdo artístico não precisa seguir a paleta da interface.

## 3. Tipografia e ritmo

Inter e fontes nativas são boas bases de UI; JetBrains Mono e monospace nativa são referências para código; texto longo pode usar serifas, outras famílias e escala editorial. Hierarquia por tamanho, peso, posição e espaço, sem teto arbitrário de tamanhos. Usar roles (caption, label, body, title, headline, display, code), adaptar ao conteúdo e às preferências de escala. Usar números tabulares onde comparados; largura de leitura em torno de 60–75 caracteres como ponto de partida. Não cortar texto crítico, respeitar idiomas e ampliação. Sentence case em UI, verbos claros, unidades explícitas.

## 4. Layout e densidade

Grade de 4 unidades e escalas de espaço/raio são referências de ritmo, não restrições de canvas, tabelas ou controles especializados. Espaço disponível, modalidade de entrada, orientação, teclado, fonte ampliada e tarefa determinam layout. Não equiparar celular a retrato ou desktop a alta densidade. Oferecer variantes confortável e compacta quando úteis; 48 dp de alvo tátil recomendado no Android, mesmo se o desenho for menor; controles de mouse e teclado podem ser compactos. Adotar listas, grade, navegação lateral, inferior, abas, multipainel, editor em tela cheia e canvas segundo a tarefa. Ações frequentes ao alcance, não necessariamente no rodapé. Ações principais por **contexto de decisão**, não por tela inteira; evitar competição visual no mesmo grupo.

## 5. Superfícies e componentes

A superfície precisa ser identificável por espaço, tom, borda ou sombra apropriados. Elevação não exige ficar mais clara em todo tema. Raios coerentes e agrupamento visual com propósito; cartões aninhados, tabelas com grade e campos contornados são válidos quando expressam estrutura. Evitar ornamentação sem função e controles indistinguíveis.

| Família | Contrato de interação | Variantes permitidas |
|---|---|---|
| Botão | nome acessível, estados, resultado previsível, sem deslocamento ao carregar | sólido, tonal, contornado, texto, ícone, destrutivo |
| Campo | rótulo identificável, valor, foco, erro e ajuda associados; teclado adequado | contornado, preenchido, sublinhado; rótulo fixo ou flutuante persistente |
| Busca | rótulo/nome acessível, limpar, vazio, sem resultados | barra, campo compacto, comando global |
| Seleção | estado visual e programático, sem depender de cor | checkbox, rádio, switch, segmentado, chip, lista |
| Listas/cartões/tabelas | hierarquia, seleção, estados vazios e ações contextuais | linhas, cards, grade, agrupamentos, virtualização, ordenação e filtros |
| Navegação | localização atual, voltar previsível, preservação do contexto | abas, barra, trilho, sidebar, breadcrumbs, pilha |
| Painéis e overlays | foco, retorno de foco, fechamento, cancelamento, não ocultar conteúdo essencial | dock, sheet, diálogo, popover, menu |
| Feedback | progresso real quando disponível, erro acionável, confirmação proporcional ao risco | inline, snackbar, toast não crítico, banner, diálogo |
| Conteúdo especializado | sem perda de capacidade; teclado, toque e acessibilidade equivalentes quando viáveis | editor, tabela, canvas, gráfico, chat, timeline, mídia |

Controles nativos são permitidos quando coerentes e acessíveis. Não obrigar a reimplementar comportamento nativo por estética. Escolher tamanho, espaçamento e raio das variantes de forma explícita. Placeholder não substitui rótulo. Estados normal, hover (quando aplicável), foco, pressionado, selecionado, desativado, carregando, vazio, erro e sucesso conforme aplicabilidade; desativado precisa explicar bloqueios quando necessário. Nunca mostrar sucesso antes da operação real. Ações irreversíveis precisam de confirmação ou recuperação adequada; não simular persistência.

## 6. Ícones, movimento, arte e desempenho

Usar família coerente por contexto; ícones lineares e preenchidos podem representar estados diferentes; 16/20/24 são referências. Nome acessível para ícone sem texto e tooltip quando útil; não depender do tooltip para informação essencial. Animação comunica relação, feedback e continuidade. 90–300 ms são boas referências, não limite universal de timeline/mídia; não bloquear interação, respeitar redução de movimento, evitar flashes e preservar foco. Preferir transformação/opacidade quando reduz custo; validar qualquer animação de layout ou blur no hardware alvo. Arte, sombras, gradientes, textura e identidade do domínio são permitidos quando legíveis e medidos. Não obrigar efeitos caros.

## 7. Acessibilidade verificável

WCAG 2.2 AA como baseline para web e referência adaptada para nativo: 4,5:1 para texto comum, 3:1 para texto grande (18 pt normal ou 14 pt negrito, equivalentes), 3:1 para informação não textual necessária de controles e estados. Medir pares reais incluindo composição alfa, estados e fundos. Texto informativo, placeholder e seleção não recebem dispensa por serem secundários. Estados inativos e outros casos seguem as exceções do critério; não usar exceção para ocultar instrução importante. APCA é métrica complementar, não certificação WCAG nem limiar universal isolado. Foco visível e não obscurecido, navegação teclado, leitor de tela, nomes, ordem, mensagens, escala de texto, alvos adequados, contraste forçado quando disponível e redução de movimento. Testar com entradas e tamanhos representativos; não alegar conformidade integral apenas por verificar paletas.

## 8. Exemplos de aplicação, não prescrições

- Ferramenta técnica: editor, múltiplas áreas de trabalho e comandos; interface compacta quando há mouse/teclado; conteúdo e preview independentes do cromo.
- Workspace documental: leitura confortável, hierarquia editorial, exploração e criação; escolha de tema conforme leitura.
- Gestão e dados: tabela densa com cabeçalhos/grade, seleção e ações em lote; cores de séries distintas da semântica de erro.
- App de uso cotidiano: fluxo simples, alvos amplos, navegação reduzida; identidade mais expressiva quando adequada.
- Canvas e multimídia: arte ou dados ocupam a área principal, controles adaptáveis; paleta de conteúdo não restrita.

Nenhum exemplo determina o visual de Lunet, Urbe, Hub ou futuros Products.

## 9. Revisão de tela (checklist)

- [ ] Tarefa principal e hierarquia por região claras; padrões escolhidos pelo contexto, não por outro Product.
- [ ] Versão e perfil de tokens declarados; papéis semânticos, overrides e paleta de dados documentados.
- [ ] Tipografia, forma, densidade, espaçamento e superfícies coerentes; conteúdo real, longo e localizado.
- [ ] Estados, teclado, foco, leitores de tela, contraste real e redução de movimento verificados.
- [ ] Adaptação em janela compacta, larga e quando teclado/zoom/orientação alteram o espaço.
- [ ] Feedback verdadeiro, erros recuperáveis, navegação sem perda de estado; desempenho mensurado quando relevante.
- [ ] Capturas e testes anexados ao PR, distinguindo estático, browser, headless, emulador e aparelho; sem alegação falsa de DEVICE (ADD-0035).

## 10. Adoção e compatibilidade

Lunet e outros consumidores v1 mantêm suas implementações até migração explícita, sem regressão por troca de tokens. Novas fundações e revisões visuais devem usar o contrato v2; não forçar redesign nem release de app nesta mudança documental. Adoção por Product registra perfil e resultados. O snapshot v1 só é retirado depois que nenhum consumidor depender dele. Mudanças de contrato global requerem decisão registrada e versão; alterações locais dentro do contrato não exigem decisão global. O estudo de 26/11 é histórico da primeira aplicação, não regra atual.
