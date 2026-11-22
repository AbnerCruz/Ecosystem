# Lunet Aprender — matriz curricular e checklist de publicação

**Data da primeira matriz:** 22/11/2026. **Estado:** 60 cursos especificados; **0 cursos e 0 projetos implementados na aba Aprender** no momento da escrita. Especificação concluída não significa funcionalidade entregue.

**Fonte de verdade curricular:** este documento. **Fonte de verdade de implementação do aplicativo:** [ROADMAP.md](../../ROADMAP.md) e evidências na main; **progresso do estudante:** dados locais da aplicação (não estes checkboxes).

## Como interpretar as caixas

- [ ] **Curso LRN-###:** só marcar [x] quando os dois projetos do curso estiverem publicados num catálogo funcional do APK, com rubricas, referências offline, navegação, compatibilidade, testes, CI verde e evidência de integração. Quando todos os projetos estiverem concluídos, revisar pré-requisitos e marcar curso.
- [ ] **Projeto LRN-###-P#:** só marcar [x] quando o usuário conseguir iniciá-lo/associá-lo a projeto real, ler os objetivos, executar e registrar etapas, retomar e consultar referências e ajuda quando disponível. Uma ideia escrita abaixo não conta.
- **As quatro frases numeradas** em cada projeto são objetivos do **aluno**, não caixas de desenvolvimento. Seus estados serão individuais no aplicativo. Nenhum checkbox neste arquivo significa que algum usuário concluiu um projeto.
- As entregas são de complexidade crescente, mas a liberdade de navegar em paralelo é preservada. Pré-requisitos são recomendações pedagógicas; novas APIs do Framework exigem compatibilidade real, nunca simulação silenciosa.
- Conteúdo novo ou alteração substancial exige revisão e versão de currículo; IDs publicados são estáveis; uma mudança na API deve indicar versão mínima ou alternativa verificável.
- **Rubrica comum:** (a) compila e roda na API declarada, quando aplicável; (b) todas as condições comportamentais são demonstráveis; (c) pausar, sair e retomar não corrompe projeto; (d) recursos gerados são próprios/licenciados; (e) existe desafio de transferência sem copiar literalmente a solução. Validação automática, inspeção e declaração pessoal são evidências distintas.

## Índice de trilhas

| Trilha | Cursos | Área | Pré-requisito sugerido |
|---|---:|---|---|
| T01 | LRN-001–LRN-005 | Primeiros jogos e orientação no Lunet | nenhum |
| T02 | LRN-006–LRN-010 | C# aplicado a jogos | LRN-005 |
| T03 | LRN-011–LRN-015 | Gêneros, mecânicas e game design | LRN-010 |
| T04 | LRN-016–LRN-020 | Matemática, geometria e física | LRN-015 |
| T05 | LRN-021–LRN-025 | Mundos, cenas e navegação | LRN-020 |
| T06 | LRN-026–LRN-030 | Arte 2D, sprites e animação | LRN-005 |
| T07 | LRN-031–LRN-035 | Gráficos avançados e GPU | LRN-030 |
| T08 | LRN-036–LRN-040 | Áudio, interface, narrativa e acessibilidade | LRN-005 |
| T09 | LRN-041–LRN-045 | Sistemas de jogo e inteligência artificial | LRN-025 |
| T10 | LRN-046–LRN-050 | Dados, engenharia de software e performance | LRN-010 |
| T11 | LRN-051–LRN-055 | Multiplayer e engenharia de redes | LRN-050 |
| T12 | LRN-056–LRN-060 | Produção, qualidade e lançamento | LRN-050 |

## Matrizes detalhadas

### T01 — Primeiros jogos e orientação no Lunet

**Resultado esperado:** Sair do zero até um jogo pequeno completo, entendendo o ciclo editar → executar → corrigir. **Entrada sugerida:** nenhum.

- [ ] **LRN-001 — Aprendendo movimento** · Competência: formas, coordenadas, toque, estados e contato. Pré-requisito: nenhum.

  - [x] **LRN-001-P1 — Aprendendo movimento**

    1. Desenhe um quadrado na tela.
    2. Movimente-o por toque ou controle virtual.
    3. Desenhe um círculo distante do quadrado.
    4. Ao tocar o círculo, anuncie vitória e permita reiniciar.

  - [ ] **LRN-001-P2 — Caça ao alvo**

    1. Posicione três alvos em lugares diferentes.
    2. Permita mover o mesmo personagem entre eles.
    3. Retire cada alvo após contato.
    4. Finalize apenas após coletar todos e reiniciar corretamente.

- [ ] **LRN-002 — Estados de partida** · Competência: início, jogo ativo, vitória, derrota e reinício. Pré-requisito: LRN-001.

  - [ ] **LRN-002-P1 — Fuja da borda**

    1. Desenhe um jogador móvel e uma arena delimitada.
    2. Mantenha o jogo ativo enquanto permanecer na arena.
    3. Ao ultrapassar um limite, apresente derrota.
    4. Adicione reinício que limpa estado e posição.

  - [ ] **LRN-002-P2 — Desafio de três vidas**

    1. Crie um perigo que cruza a arena.
    2. Subtraia uma vida apenas ao contato válido.
    3. Mostre tela de derrota após três danos.
    4. Reinicie vidas, perigos e posição sem criar estados duplicados.

- [ ] **LRN-003 — Pontos e tempo** · Competência: temporizadores, contadores, feedback e HUD elementar. Pré-requisito: LRN-002.

  - [ ] **LRN-003-P1 — Colecionador cronometrado**

    1. Mostre um contador de pontos visível.
    2. Crie alvo que aparece em posição nova ao ser tocado.
    3. Defina duração finita da rodada.
    4. Finalize com placar coerente e botão de tentar de novo.

  - [ ] **LRN-003-P2 — Corrida de recordes**

    1. Calcule tempo até tocar a meta.
    2. Exiba tempo durante a jogada.
    3. Guarde o melhor valor dentro da sessão.
    4. Teste pausa e reinício sem duplicar ou acelerar o cronômetro.

- [ ] **LRN-004 — Controles mobile** · Competência: múltiplos toques, gesto, joysticks, pausa e safe area. Pré-requisito: LRN-003.

  - [ ] **LRN-004-P1 — Labirinto de dedo**

    1. Construa labirinto simples de linhas ou blocos.
    2. Implemente movimentação contínua por arrasto.
    3. Não teletransporte o jogador ao trocar de dedo.
    4. Valide início, saída e cancelamento do gesto.

  - [ ] **LRN-004-P2 — Arena com dois controles**

    1. Crie movimento com analógico virtual esquerdo.
    2. Controle uma seta de direção com toque direito.
    3. Permita tocar simultaneamente sem confundir dedos.
    4. Pausar e retomar libera capturas antigas de toque.

- [ ] **LRN-005 — Microjogo completo** · Competência: menu, gameplay, derrota, vitória, áudio básico e produto final. Pré-requisito: LRN-004.

  - [ ] **LRN-005-P1 — Mini-arcade de um minuto**

    1. Desenhe tela inicial e botão iniciar.
    2. Implemente mecânica com pontuação e ameaça crescente.
    3. Mostre derrota e pontuação ao fim.
    4. Reinicie sem acumular objetos ou eventos antigos.

  - [ ] **LRN-005-P2 — Microjogo autoral**

    1. Escolha uma regra original de uma tela.
    2. Documente controle, objetivo e condições de fim.
    3. Implemente início, partida e conclusão com feedback.
    4. Entregue jogo reproduzível no Preview sem depender da aba Aprender.

### T02 — C# aplicado a jogos

**Resultado esperado:** Compreender a linguagem e organizar lógica cada vez mais complexa sem abandonar projetos jogáveis. **Entrada sugerida:** LRN-005.

- [ ] **LRN-006 — Tipos, variáveis e operações** · Competência: tipos numéricos, booleanos, conversões, limites e precisão. Pré-requisito: LRN-005.

  - [ ] **LRN-006-P1 — Simulador de energia**

    1. Crie personagem com energia inicial e limite máximo.
    2. Reduza energia por movimento ou ação.
    3. Recupere energia com um item e impeça exceder o limite.
    4. Exiba valores coerentes sem energia negativa.

  - [ ] **LRN-006-P2 — Loja de pontos**

    1. Crie pontos e três itens com preços distintos.
    2. Permita comprar com saldo suficiente.
    3. Rejeite compras impossíveis sem alterar valores.
    4. Teste cálculos de custo, desconto e saldo em casos extremos.

- [ ] **LRN-007 — Condições e repetição** · Competência: if, switch, for, while, arrays básicos e invariantes. Pré-requisito: LRN-006.

  - [ ] **LRN-007-P1 — Portas de cores**

    1. Crie portas com requisitos visíveis diferentes.
    2. Verifique a chave correta antes de atravessar.
    3. Use condições para mostrar resultados alternativos.
    4. Teste todas as combinações válidas e inválidas.

  - [ ] **LRN-007-P2 — Onda de obstáculos**

    1. Gere série finita de obstáculos por laço.
    2. Aumente a dificuldade a cada rodada.
    3. Evite loop infinito ou crescimento sem limite.
    4. Termine ao sobreviver à última onda.

- [ ] **LRN-008 — Métodos, eventos e responsabilidade** · Competência: parâmetros, retorno, escopo, reutilização e efeitos. Pré-requisito: LRN-007.

  - [ ] **LRN-008-P1 — Controle reutilizável**

    1. Extraia lógica de movimento para método parametrizado.
    2. Reutilize-a para dois objetos com velocidades distintas.
    3. Separe atualização, desenho e verificação de contato.
    4. Mostre que mudar um parâmetro não altera o outro objeto.

  - [ ] **LRN-008-P2 — Botões com ações**

    1. Crie duas ações de jogo distintas.
    2. Vincule cada ação a um evento de entrada.
    3. Elimine repetição entre handlers equivalentes.
    4. Confirme que uma entrada dispara somente uma ação.

- [ ] **LRN-009 — Objetos e composição** · Competência: classes, propriedades, interfaces, encapsulamento e herança prudente. Pré-requisito: LRN-008.

  - [ ] **LRN-009-P1 — Frota de inimigos**

    1. Modele um inimigo como objeto com estado próprio.
    2. Crie inimigos de comportamentos diferentes.
    3. Separe vida, posição e desenho do loop principal.
    4. Remova inimigos derrotados sem afetar os demais.

  - [ ] **LRN-009-P2 — Arsenal modular**

    1. Defina contrato de arma reutilizável.
    2. Implemente duas armas com comportamento diferente.
    3. Troque equipamento sem modificar o jogador internamente.
    4. Teste extensão com uma terceira arma.

- [ ] **LRN-010 — Coleções, dados e erros** · Competência: List, Dictionary, enum, nullable, exceções e LINQ criterioso. Pré-requisito: LRN-009.

  - [ ] **LRN-010-P1 — Inventário de coleta**

    1. Armazene itens diferentes em coleção.
    2. Mostre quantidades e remova itens ao usar.
    3. Procure um item ausente sem crash.
    4. Ordene exibição sem modificar indevidamente o inventário.

  - [ ] **LRN-010-P2 — Diário de eventos**

    1. Registre eventos da partida com tipo e momento.
    2. Filtre apenas eventos relevantes ao jogador.
    3. Calcule uma estatística a partir da coleção.
    4. Trate lista vazia e eventos inválidos sem falha.

### T03 — Gêneros, mecânicas e game design

**Resultado esperado:** Entender loops centrais, telemetria local de protótipo, regras e design centrado na experiência. **Entrada sugerida:** LRN-010.

- [ ] **LRN-011 — Pong e dinâmica de rebote** · Competência: raquetes, velocidade, previsão e pontuação. Pré-requisito: LRN-010.

  - [ ] **LRN-011-P1 — Pong solo**

    1. Crie bola com velocidade e rebote nas bordas.
    2. Mova raquete por toque e detecte rebote.
    3. Pontue quando o adversário falhar.
    4. Reinicie bola e placar de forma controlada.

  - [ ] **LRN-011-P2 — Pong com oponente**

    1. Implemente raquete controlada por lógica simples.
    2. Ajuste dificuldade sem teleporte injusto.
    3. Apresente placar de melhor de cinco.
    4. Teste trajetórias horizontais e pontos repetidos.

- [ ] **LRN-012 — Breakout e progressão de fases** · Competência: grade de blocos, destruição, pontuação, condições de fase. Pré-requisito: LRN-011.

  - [ ] **LRN-012-P1 — Quebra-blocos**

    1. Crie uma grade de blocos destrutíveis.
    2. Rebata bola com raquete móvel.
    3. Remova apenas o bloco realmente atingido.
    4. Conclua a fase quando a grade estiver vazia.

  - [ ] **LRN-012-P2 — Quebra-blocos evolutivo**

    1. Adicione tipos de bloco com resistência distinta.
    2. Rebotes modificam direção com alguma variação controlada.
    3. Introduza vida ou bola extra como recompensa.
    4. Passe à segunda configuração de fase sem reiniciar o aplicativo.

- [ ] **LRN-013 — Runner e dificuldade** · Competência: obstáculos, ritmo, feedback e ajuste de dificuldade. Pré-requisito: LRN-012.

  - [ ] **LRN-013-P1 — Corredor infinito**

    1. Mantenha jogador em linha de avanço.
    2. Gere obstáculos com intervalo reproduzível.
    3. Implemente pulo ou desvio e colisão.
    4. Mostre distância e condição de derrota.

  - [ ] **LRN-013-P2 — Runner de duas pistas**

    1. Permita mudar de pista com gestos claros.
    2. Varie padrões de obstáculos sem situações impossíveis.
    3. Aumente ritmo em limites definidos.
    4. Teste jogabilidade em tela pequena sem controle ambíguo.

- [ ] **LRN-014 — Puzzle, lógica e level design** · Competência: estados discretos, solução, curva de aprendizagem. Pré-requisito: LRN-013.

  - [ ] **LRN-014-P1 — Sokoban mínimo**

    1. Desenhe grade, parede, caixas e objetivos.
    2. Mova jogador de célula em célula.
    3. Permita empurrar caixa apenas a destino livre.
    4. Reconheça vitória quando todas estiverem corretamente posicionadas.

  - [ ] **LRN-014-P2 — Puzzle de três níveis**

    1. Crie três fases com conceitos progressivos.
    2. Implemente undo de uma jogada sem corromper estado.
    3. Resete fase sem afetar progresso das outras.
    4. Faça um teste de resolução de cada fase.

- [ ] **LRN-015 — Plataforma e movimento expressivo** · Competência: pulo, coyote time, colisões e ritmo. Pré-requisito: LRN-014.

  - [ ] **LRN-015-P1 — Plataforma de uma fase**

    1. Crie piso, plataformas e personagem.
    2. Implemente correr, pular e gravidade.
    3. Evite atravessar paredes e pisos.
    4. Alcance bandeira e conclua a fase.

  - [ ] **LRN-015-P2 — Plataforma de precisão**

    1. Adicione altura de pulo controlável ou coyote time.
    2. Crie perigos e checkpoint.
    3. Introduza desafio cuja solução exige domínio do movimento.
    4. Teste respawn sem perda de objetos/estado indevidos.

### T04 — Matemática, geometria e física

**Resultado esperado:** Aplicar matemática a comportamentos reproduzíveis, detecção de contato e simulações estáveis. **Entrada sugerida:** LRN-015.

- [ ] **LRN-016 — Vetores e trigonometria** · Competência: Vector2, magnitude, direção, ângulo e normalização. Pré-requisito: LRN-015.

  - [ ] **LRN-016-P1 — Apontador orbital**

    1. Movimente objeto em torno de um centro.
    2. Calcule direção normalizada até um alvo.
    3. Faça a orientação acompanhar o deslocamento.
    4. Evite erro quando alvo e origem coincidirem.

  - [ ] **LRN-016-P2 — Projétil angular**

    1. Aponte usando um vetor de direção.
    2. Lance projétil com velocidade constante.
    3. Controle alcance e expiração da bala.
    4. Compare disparos em ângulos diferentes.

- [ ] **LRN-017 — Geometria e colisões** · Competência: círculos, AABB, SAT, raycast e casos-limite. Pré-requisito: LRN-016.

  - [ ] **LRN-017-P1 — Laboratório de colisão**

    1. Desenhe corpos retangulares e circulares.
    2. Sinalize interseção apenas quando ocorrer.
    3. Diferencie toque na borda e sobreposição conforme regra escolhida.
    4. Teste casos de contenção e objetos afastados.

  - [ ] **LRN-017-P2 — Labirinto de raios**

    1. Implemente raio a partir do personagem.
    2. Detecte o primeiro obstáculo do percurso.
    3. Mostre distância até impacto.
    4. Teste direção paralela e ausência de colisão.

- [ ] **LRN-018 — Cinemática e tempo** · Competência: delta time, aceleração, inércia, interpolação. Pré-requisito: LRN-017.

  - [ ] **LRN-018-P1 — Patinação espacial**

    1. Mova nave usando aceleração e velocidade.
    2. Permita desacelerar com atrito ou resistência.
    3. Separe atualização de desenho e compare velocidades de frames.
    4. Evite salto extremo após pausa ou retomada.

  - [ ] **LRN-018-P2 — Movimento suave**

    1. Crie objeto que persegue outro com limite de aceleração.
    2. Aplique interpolação visual sem alterar física.
    3. Implemente desaceleração perto do alvo.
    4. Teste com distâncias curtas e longas.

- [ ] **LRN-019 — Física rígida** · Competência: corpos, forças, massa, gravidade, contatos e sensores. Pré-requisito: LRN-018.

  - [ ] **LRN-019-P1 — Torre que cai**

    1. Crie três corpos com massas diferentes.
    2. Aplique gravidade e colisão com piso.
    3. Observe empilhamento e resposta a impulso.
    4. Evite objetos atravessando o chão no caso normal.

  - [ ] **LRN-019-P2 — Pinball físico**

    1. Crie esfera e obstáculos com colisão e restituição.
    2. Ative um impulsor por toque.
    3. Conte colisões de pontuação sem duplicar eventos.
    4. Reinicie a simulação preservando suas configurações.

- [ ] **LRN-020 — Juntas e estabilidade** · Competência: constraints, amortecimento, passo fixo e tunneling. Pré-requisito: LRN-019.

  - [ ] **LRN-020-P1 — Pêndulo controlado**

    1. Suspenda corpo num ponto por junta.
    2. Aplique impulso para iniciar oscilação.
    3. Regule amortecimento e observe comportamento.
    4. Teste reinício sem energia ou conexões residuais.

  - [ ] **LRN-020-P2 — Ponte articulada**

    1. Conecte múltiplos corpos por juntas.
    2. Aplique forças variáveis de travessia.
    3. Inspecione estabilidade sob carga moderada.
    4. Documente e reproduza um caso-limite com subpassos ou prevenção de tunneling.

### T05 — Mundos, cenas e navegação

**Resultado esperado:** Compor espaços exploráveis e a transição entre níveis, mapas e diferentes perspectivas 2D. **Entrada sugerida:** LRN-020.

- [ ] **LRN-021 — Câmera 2D** · Competência: seguimento, limites, zoom, coordenadas e tremor. Pré-requisito: LRN-020.

  - [ ] **LRN-021-P1 — Câmera seguidora**

    1. Construa mundo maior que a tela.
    2. Faça a câmera seguir personagem mantendo posição coerente.
    3. Limite visão para fora do mapa.
    4. Teste pan e zoom sem distorção do movimento.

  - [ ] **LRN-021-P2 — Câmera cinematográfica**

    1. Implemente transição suave entre dois pontos.
    2. Aplique tremor breve em impacto.
    3. Restaure câmera após efeito.
    4. Faça HUD permanecer estável durante movimento do mundo.

- [ ] **LRN-022 — Tilemaps e colisão de terreno** · Competência: grades, tilesets, camadas e edição. Pré-requisito: LRN-021.

  - [ ] **LRN-022-P1 — Mapa da vila**

    1. Desenhe tileset pequeno no Studio ou código.
    2. Monte mapa com solo, paredes e decoração.
    3. Permita caminhar apenas nas regiões livres.
    4. Teste consistência entre mapa carregado e Preview.

  - [ ] **LRN-022-P2 — Masmorra multicamada**

    1. Use camadas de piso, decoração e obstáculo.
    2. Projete mapa com salas e corredores.
    3. Adicione zonas de entrada e saída.
    4. Garanta que todos os objetivos sejam alcançáveis.

- [ ] **LRN-023 — Cenas, transições e fluxo** · Competência: ciclo de vida, gerenciamento de fases e dados entre cenas. Pré-requisito: LRN-022.

  - [ ] **LRN-023-P1 — Três salas conectadas**

    1. Construa três regiões com entradas diferentes.
    2. Transicione mantendo identidade e estado do jogador.
    3. Evite carregar duas salas ativas indevidamente.
    4. Volte à sala anterior no local esperado.

  - [ ] **LRN-023-P2 — Campanha pequena**

    1. Crie menu, duas fases e finalização.
    2. Carregue assets por fase conforme necessidade.
    3. Preserve um dado entre fases.
    4. Reinicie campanha sem corromper progressão.

- [ ] **LRN-024 — Geração procedural de mapas** · Competência: semente, ruído, restrições, conectividade e validação. Pré-requisito: LRN-023.

  - [ ] **LRN-024-P1 — Labirinto por semente**

    1. Gere grade diferente a partir de sementes.
    2. Garanta entrada e saída conectadas.
    3. Permita repetir mesmo mapa com mesma semente.
    4. Teste tamanhos extremos sem travamento.

  - [ ] **LRN-024-P2 — Ilhas habitáveis**

    1. Gere terreno com água, solo e obstáculos.
    2. Imponha regras de conectividade ou zonas alcançáveis.
    3. Coloque objetos somente em posições válidas.
    4. Permita salvar e reproduzir o mapa.

- [ ] **LRN-025 — Perspectivas, coordenadas e navegação** · Competência: top-down, isométrico 2D, ordenação por profundidade e path traversal. Pré-requisito: LRN-024.

  - [ ] **LRN-025-P1 — Aventura top-down**

    1. Implemente caminhar em quatro direções.
    2. Desenhe objetos à frente ou atrás conforme posição.
    3. Implemente interação com objeto próximo.
    4. Mantenha colisões coerentes com apresentação visual.

  - [ ] **LRN-025-P2 — Vila pseudoisométrica**

    1. Projete grade isométrica por transformação 2D.
    2. Permita selecionar uma célula tocada na tela.
    3. Ordene desenho de objetos de acordo com profundidade.
    4. Teste conversão tela ↔ grade nos cantos do mapa.

### T06 — Arte 2D, sprites e animação

**Resultado esperado:** Criar e integrar recursos próprios, dando consistência visual a personagens, ambientes e efeitos. **Entrada sugerida:** LRN-005.

- [ ] **LRN-026 — Pixel art e legibilidade** · Competência: silhueta, escala, contraste, paleta e exportação. Pré-requisito: LRN-005.

  - [ ] **LRN-026-P1 — Herói de 16 pixels**

    1. Desenhe personagem legível com paleta limitada.
    2. Exporte PNG e fonte editável.
    3. Mostre personagem sobre fundo contrastante.
    4. Compare legibilidade em duas escalas.

  - [ ] **LRN-026-P2 — Kit de objetos**

    1. Crie cinco itens visuais consistentes.
    2. Use uma única paleta deliberada.
    3. Exporte e organize assets por categoria.
    4. Exiba-os juntos sem divergência de escala.

- [ ] **LRN-027 — Spritesheets e recortes** · Competência: atlas, origem, pivot, dimensões e organização. Pré-requisito: LRN-026.

  - [ ] **LRN-027-P1 — Folha de expressões**

    1. Crie quadro de sprites de tamanhos iguais.
    2. Recorte quadros corretamente no runtime.
    3. Permita trocar expressão por entrada.
    4. Teste bordas e ausência de bleeding.

  - [ ] **LRN-027-P2 — Atlas de cenário**

    1. Reúna vários objetos em textura compartilhada.
    2. Desenhe os recortes na posição correta.
    3. Compare versão com vários arquivos individuais.
    4. Garanta que origens e tamanhos não se alterem.

- [ ] **LRN-028 — Animação de personagens** · Competência: frames, tempo, estados, blending e transições. Pré-requisito: LRN-027.

  - [ ] **LRN-028-P1 — Andar em quatro direções**

    1. Crie animações curtas por direção.
    2. Troque animação conforme input.
    3. Pare em pose coerente ao cessar movimento.
    4. Teste troca rápida sem travamento no frame errado.

  - [ ] **LRN-028-P2 — Personagem com ação**

    1. Inclua animações de parado, correr e atacar.
    2. Defina prioridades e transições entre estados.
    3. Emita evento de impacto em frame consistente.
    4. Evite repetir ataque por manter botão pressionado.

- [ ] **LRN-029 — Paletas, camadas e identidade visual** · Competência: produção artística, composição, consistência e variações. Pré-requisito: LRN-028.

  - [ ] **LRN-029-P1 — Ambiente dia e noite**

    1. Defina paleta coerente para cenário.
    2. Crie duas variações de iluminação por cor.
    3. Permita alternar estado durante a partida.
    4. Preserve contraste e leitura do personagem.

  - [ ] **LRN-029-P2 — Personagem customizável**

    1. Desenhe partes de roupa e corpo independentes.
    2. Monte combinações com pontos de encaixe coerentes.
    3. Salve configuração como dados editáveis.
    4. Teste que ordem de camadas não oculta indevidamente ações.

- [ ] **LRN-030 — Partículas e linguagem visual** · Competência: emissores, vida útil, composição e efeitos de jogo. Pré-requisito: LRN-029.

  - [ ] **LRN-030-P1 — Explosão econômica**

    1. Crie efeito com partículas de vida finita.
    2. Varie direção, cor e velocidade.
    3. Limpe partículas expiradas sem vazamento.
    4. Compare desempenho com muitos efeitos simultâneos.

  - [ ] **LRN-030-P2 — Pacote de feedback**

    1. Crie efeitos distintos para coleta, dano e vitória.
    2. Associa eventos reais do jogo aos efeitos.
    3. Evite saturar tela em sequência rápida.
    4. Garanta reconhecimento visual sem depender de áudio.

### T07 — Gráficos avançados e GPU

**Resultado esperado:** Entender resolução, renderização eficiente e efeitos gráficos inspecionáveis em 2D. **Entrada sugerida:** LRN-030.

- [ ] **LRN-031 — Resolução, viewport e pixels** · Competência: Fit, Fill, escala virtual, DPI, safe area e pixel-perfect. Pré-requisito: LRN-030.

  - [ ] **LRN-031-P1 — Mesmo jogo, duas telas**

    1. Adapte protótipo a retrato e paisagem.
    2. Faça input converter corretamente tela ↔ mundo.
    3. Preserve HUD nas bordas seguras.
    4. Compare Fit/Fill e documente cortes intencionais.

  - [ ] **LRN-031-P2 — Pixel perfect de verdade**

    1. Crie arte de baixa resolução.
    2. Desenhe com amostragem apropriada.
    3. Evite deformação e borrão em escalas comuns.
    4. Teste fontes e contornos legíveis.

- [ ] **LRN-032 — Camadas e batching** · Competência: SpriteBatch, ordenação, overdraw e render budget. Pré-requisito: LRN-031.

  - [ ] **LRN-032-P1 — Cidade de mil sprites**

    1. Desenhe grande conjunto repetido de elementos.
    2. Agrupe por textura e controle ordem de desenho.
    3. Mostre métrica básica de custo.
    4. Evite alocações por quadro e compare com versão ingênua.

  - [ ] **LRN-032-P2 — Plataforma multicamada**

    1. Crie primeiro plano, meio e fundo.
    2. Implemente parallax com velocidades diferentes.
    3. Cuide da oclusão de personagem e HUD.
    4. Confirme que a ordem não muda com zoom.

- [ ] **LRN-033 — Composição e transparência** · Competência: alpha, blending, recorte, tint e máscaras. Pré-requisito: LRN-032.

  - [ ] **LRN-033-P1 — Fantasma translúcido**

    1. Desenhe corpo com transparência ajustável.
    2. Implemente efeito de desaparecimento gradual.
    3. Compare modos de mistura apropriados.
    4. Evite bordas e halos inesperados.

  - [ ] **LRN-033-P2 — Janela reveladora**

    1. Desenhe imagem completa de fundo.
    2. Mostre parte da imagem por recorte ou máscara.
    3. Mova área revelada conforme input.
    4. Teste recorte fora da tela e oclusão correta.

- [ ] **LRN-034 — Shaders 2D** · Competência: uniforms, fragmentos, UV, tempo e portabilidade. Pré-requisito: LRN-033.

  - [ ] **LRN-034-P1 — Água animada**

    1. Crie material com distorção visual leve.
    2. Anime efeito por tempo sem modificar conteúdo-fonte.
    3. Exponha intensidade ajustável.
    4. Teste custo e fallback caso shader customizado não exista no host.

  - [ ] **LRN-034-P2 — Contorno de seleção**

    1. Identifique sprite selecionado.
    2. Renderize contorno por shader ou técnica equivalente.
    3. Permita alterar cor e espessura.
    4. Cheque bordas transparentes e regressões visuais.

- [ ] **LRN-035 — Render targets, luz e pós-processo** · Competência: render-to-texture, blur, iluminação 2D e pipeline. Pré-requisito: LRN-034.

  - [ ] **LRN-035-P1 — Mini-mapa dinâmico**

    1. Renderize mundo numa superfície secundária.
    2. Exiba mini-mapa sem recortar a cena principal.
    3. Atualize posição do jogador.
    4. Teste redimensionamento e liberação de recursos.

  - [ ] **LRN-035-P2 — Noite com luzes**

    1. Crie layer escura e fontes de luz locais.
    2. Combine superfícies preservando elementos legíveis.
    3. Adicione ajuste de intensidade sem destruir contraste.
    4. Teste desempenho e fallback em GPU limitada.

### T08 — Áudio, interface, narrativa e acessibilidade

**Resultado esperado:** Dar retorno audiovisual, construir menus compreensíveis e permitir uso acessível. **Entrada sugerida:** LRN-005.

- [ ] **LRN-036 — Efeitos sonoros e mixagem** · Competência: gatilhos, volume, pan, pitch, mix buses e polifonia. Pré-requisito: LRN-005.

  - [ ] **LRN-036-P1 — Jogo sonoro de coleta**

    1. Produza ou importe efeito original de coleta.
    2. Toque som somente no evento de coleta.
    3. Controle volume e silencie a categoria.
    4. Teste várias coletas rápidas sem travar audio.

  - [ ] **LRN-036-P2 — Paisagem de combate**

    1. Combine efeitos de tiro, dano e ambiente.
    2. Separe categorias de volume.
    3. Evite clip em simultaneidade intensa.
    4. Respeite pausa e retomada do Android.

- [ ] **LRN-037 — Música adaptativa** · Competência: loops, temas, transição, composição e eventos. Pré-requisito: LRN-036.

  - [ ] **LRN-037-P1 — Tema em loop**

    1. Produza trilha curta e própria.
    2. Toque loop sem interrupção perceptível.
    3. Ajuste volume separadamente de efeitos.
    4. Pare ou suspenda adequadamente ao pausar jogo.

  - [ ] **LRN-037-P2 — Trilha por estado**

    1. Componha duas atmosferas sonoras.
    2. Alterne entre exploração e perigo com transição.
    3. Evite duplicar instâncias ou vazamento.
    4. Retorne à trilha correta após reabrir partida.

- [ ] **LRN-038 — Menus e HUD** · Competência: hierarquia, foco, botões, layout adaptável e consistência. Pré-requisito: LRN-037.

  - [ ] **LRN-038-P1 — Menu inicial funcional**

    1. Crie iniciar, opções e sair/voltar.
    2. Mantenha botões com áreas de toque adequadas.
    3. Navegue entre menu e partida sem perder estados.
    4. Teste Back e retorno ao menu.

  - [ ] **LRN-038-P2 — HUD de aventura**

    1. Exiba vida, recurso e objetivo principal.
    2. Atualize indicadores com eventos reais.
    3. Evite cobrir controles ou jogador.
    4. Teste diferentes proporções de tela.

- [ ] **LRN-039 — Acessibilidade e controles** · Competência: alto contraste, alternativas de controle, tamanho, feedback e pausa. Pré-requisito: LRN-038.

  - [ ] **LRN-039-P1 — Arcade acessível**

    1. Implemente opções de tamanho dos controles.
    2. Ofereça indicadores sem depender apenas de cor.
    3. Permita pausar e reconfigurar controles.
    4. Teste navegação com diferentes tamanhos de fonte.

  - [ ] **LRN-039-P2 — Desafio sem som**

    1. Implemente objetivo inicialmente comunicado por áudio.
    2. Inclua legenda/indicador visual equivalente.
    3. Evite ações dependentes de timing impossível de perceber.
    4. Teste conclusão com áudio desligado.

- [ ] **LRN-040 — Texto, diálogos e localização** · Competência: fontes, Unicode, quebra de texto, escolhas e traduções. Pré-requisito: LRN-039.

  - [ ] **LRN-040-P1 — Conversa de vila**

    1. Crie NPC com ao menos três falas e opção de escolha.
    2. Mostre falas legíveis no celular.
    3. Permita avançar sem repetir escolhas por toque longo.
    4. Encerre diálogo mantendo controle do jogador.

  - [ ] **LRN-040-P2 — Jogo bilíngue**

    1. Extraia mensagens para recursos localizáveis.
    2. Ofereça português e inglês.
    3. Trate comprimento diferente e caracteres especiais.
    4. Teste mudança de idioma sem recompilar lógica.

### T09 — Sistemas de jogo e inteligência artificial

**Resultado esperado:** Desenvolver comportamentos emergentes e sistemas lúdicos complexos, testáveis e determinísticos quando necessário. **Entrada sugerida:** LRN-025.

- [ ] **LRN-041 — Máquinas de estado de NPC** · Competência: FSM, temporizadores, percepção, transições e depuração. Pré-requisito: LRN-025.

  - [ ] **LRN-041-P1 — Guarda patrulheiro**

    1. Defina estados patrulha, alerta e retorno.
    2. Mude estado ao perceber o jogador.
    3. Defina tempo limite antes de abandonar perseguição.
    4. Exiba estado para depurar transições.

  - [ ] **LRN-041-P2 — Chefe em fases**

    1. Crie chefe com três fases de padrão distintas.
    2. Transicione por condição verificável de vida ou tempo.
    3. Telegrame ataques para o jogador.
    4. Teste impossibilidade de fases simultâneas.

- [ ] **LRN-042 — Pathfinding e navegação** · Competência: grafos, A*, heurísticas, grade dinâmica e custos. Pré-requisito: LRN-041.

  - [ ] **LRN-042-P1 — Guia do labirinto**

    1. Crie mapa com obstáculos e destino.
    2. Calcule caminho entre dois pontos livres.
    3. Desloque NPC sem atravessar paredes.
    4. Trate destino inalcançável sem congelar jogo.

  - [ ] **LRN-042-P2 — Mensageiro de cidade**

    1. Atribua custos diferentes a terrenos.
    2. Recalcule rota ao surgir obstáculo.
    3. Evite recalcular rota desnecessariamente a cada frame.
    4. Compare custo e comprimento de duas rotas.

- [ ] **LRN-043 — Comportamento autônomo** · Competência: necessidades, prioridades, utility AI, agentes e decisões. Pré-requisito: LRN-042.

  - [ ] **LRN-043-P1 — Morador com rotina**

    1. Defina necessidades de fome, repouso e deslocamento.
    2. Escolha ação segundo necessidades e disponibilidade.
    3. Atualize estado durante longo período de simulação.
    4. Registre motivos das decisões para inspeção.

  - [ ] **LRN-043-P2 — Vila emergente**

    1. Crie três moradores com estados independentes.
    2. Distribua recursos escassos entre necessidades.
    3. Permita interações sem comandos diretos do jogador.
    4. Teste que simulação retoma de estado salvo.

- [ ] **LRN-044 — Sistemas econômicos e crafting** · Competência: recursos, receitas, produção, estoques e balanceamento. Pré-requisito: LRN-043.

  - [ ] **LRN-044-P1 — Oficina de ferramentas**

    1. Defina matérias-primas e receitas.
    2. Produza item consumindo quantidades corretas.
    3. Evite produção com estoque insuficiente.
    4. Exiba saldo e capacidade após operação.

  - [ ] **LRN-044-P2 — Mercado de vila**

    1. Modele oferta, demanda e preços com limites.
    2. Permita comprar/vender com moedas inteiras ou precisão definida.
    3. Impeça saldo negativo e transações duplicadas.
    4. Teste estabilidade econômica em ciclos longos.

- [ ] **LRN-045 — Estratégia e simulação tática** · Competência: turnos, seleção, pathfinding, alcance e regras. Pré-requisito: LRN-044.

  - [ ] **LRN-045-P1 — Xadrez tático simplificado**

    1. Monte grade com duas equipes e três unidades.
    2. Implemente alternância de turnos.
    3. Valide movimento e ataque por distância.
    4. Detecte condição de encerramento sem empate silencioso.

  - [ ] **LRN-045-P2 — Defesa de torres**

    1. Crie caminho e ondas de inimigos.
    2. Instale torres que priorizam alvos conforme regra.
    3. Gaste recursos e ganhe recompensa por derrota.
    4. Equilibre três ondas testáveis com condição de vitória/derrota.

### T10 — Dados, engenharia de software e performance

**Resultado esperado:** Sair do protótipo frágil rumo a projetos organizados, testáveis, persistentes e eficientes. **Entrada sugerida:** LRN-010.

- [ ] **LRN-046 — Modelagem de dados e inventários** · Competência: identidade, DTO, referência, serialização e validação. Pré-requisito: LRN-010.

  - [ ] **LRN-046-P1 — Equipamento persistente em memória**

    1. Modele tipos e instâncias de itens.
    2. Adicione e remova itens por IDs estáveis.
    3. Rejeite índices e referências inválidas.
    4. Teste duas instâncias do mesmo tipo com estados diferentes.

  - [ ] **LRN-046-P2 — Missões orientadas a dados**

    1. Defina três missões em JSON versionado.
    2. Carregue objetivos e recompensas sem hardcode.
    3. Atualize progresso por eventos corretos.
    4. Trate arquivo inválido com erro recuperável.

- [ ] **LRN-047 — Salvamento e migração** · Competência: save slots, atomicidade, versões e recuperação. Pré-requisito: LRN-046.

  - [ ] **LRN-047-P1 — Salvar e continuar**

    1. Salve posição, inventário e fase num slot.
    2. Feche e reabra jogo mantendo resultado.
    3. Implemente novo jogo sem apagar slot existente.
    4. Trate arquivo ausente sem crash.

  - [ ] **LRN-047-P2 — Save versão 2**

    1. Adicione novo campo à estrutura de save.
    2. Migre dados da versão antiga sem perda.
    3. Simule interrupção durante gravação e preservação da versão anterior.
    4. Teste incompatibilidade com erro explicado.

- [ ] **LRN-048 — Testes e depuração** · Competência: unit, integração, regressão, logs, profiling e testes de fronteira. Pré-requisito: LRN-047.

  - [ ] **LRN-048-P1 — Caça ao bug**

    1. Introduza bug conhecido de pontuação.
    2. Escreva teste que o reproduz antes de corrigir.
    3. Corrija causa sem suprimir o teste.
    4. Inclua caso-limite e registre o diagnóstico.

  - [ ] **LRN-048-P2 — Laboratório de confiabilidade**

    1. Crie cenários de pausa, reinício e arquivo ausente.
    2. Automatize as partes determinísticas.
    3. Registre erros observáveis sem engolir exceção.
    4. Execute suite repetidamente sem regressão.

- [ ] **LRN-049 — Arquitetura e reuso** · Competência: interfaces, serviços, módulos, dependências e extensões. Pré-requisito: LRN-048.

  - [ ] **LRN-049-P1 — Do monólito aos módulos**

    1. Pegue jogo com uma classe muito grande.
    2. Separe input, estado, apresentação e regras.
    3. Preserve funcionamento durante extração.
    4. Teste pelo menos uma regra sem Android.

  - [ ] **LRN-049-P2 — Plugin de mecânica**

    1. Defina contrato simples de regra de jogo.
    2. Implemente duas variantes substituíveis.
    3. Adicione terceira variante sem editar núcleo.
    4. Documente compatibilidade e erros esperados.

- [ ] **LRN-050 — Otimização e profiling** · Competência: alocações, pools, CPU/GPU, memória e medição. Pré-requisito: LRN-049.

  - [ ] **LRN-050-P1 — Mil projéteis**

    1. Crie cenário com muitas balas simultâneas.
    2. Meça tempo de atualização e renderização.
    3. Aplique pooling ou batching com benefício observado.
    4. Compare antes/depois no mesmo cenário.

  - [ ] **LRN-050-P2 — Simulação longa**

    1. Mantenha centenas de entidades por tempo prolongado.
    2. Instrumente alocação e custo de atualização.
    3. Identifique gargalo mensurável e corrija.
    4. Demonstre ausência de vazamento progressivo no teste automatizado aplicável.

### T11 — Multiplayer e engenharia de redes

**Resultado esperado:** Projetar jogos sincronizados, separados por modalidade de partida, com autoridade e falhas reais. **Entrada sugerida:** LRN-050.

- [ ] **LRN-051 — Multiplayer local e turnos** · Competência: dois controles, hot-seat, cooperação, UI e arbitragem. Pré-requisito: LRN-050.

  - [ ] **LRN-051-P1 — Duelos na mesma tela**

    1. Crie dois jogadores com controles independentes.
    2. Implemente colisões e pontuação simétrica.
    3. Reinicie rodada sem confundir entradas.
    4. Teste ambos os controles simultaneamente.

  - [ ] **LRN-051-P2 — Tabuleiro hot-seat**

    1. Crie jogo alternado no mesmo aparelho.
    2. Impeça ação fora do turno.
    3. Indique de quem é a jogada sem expor ações futuras quando pertinente.
    4. Reconheça fim de partida e empate.

- [ ] **LRN-052 — Estado compartilhado assíncrono** · Competência: serialização de turnos, reconciliação e troca de arquivos. Pré-requisito: LRN-051.

  - [ ] **LRN-052-P1 — Duelo por arquivo**

    1. Serialize estado e próxima jogada.
    2. Exporte turno para troca entre dois dispositivos.
    3. Importe somente estado válido e mais recente.
    4. Rejeite replay de jogadas repetidas.

  - [ ] **LRN-052-P2 — Correspondência offline**

    1. Mantenha histórico compacto das jogadas.
    2. Implemente retomada após reinício.
    3. Detecte conflito de versões ou turnos.
    4. Resolva conflito sem perder o último estado válido.

- [ ] **LRN-053 — Rede local em tempo real** · Competência: sessões, descoberta, conectividade e mensagens. Pré-requisito: LRN-052.

  - [ ] **LRN-053-P1 — Corrida na LAN**

    1. Crie host e cliente na mesma rede.
    2. Sincronize posições com taxa definida.
    3. Exiba estados conectado/desconectado.
    4. Teste perda de rede sem crash ou movimento infinito.

  - [ ] **LRN-053-P2 — Coop de sobrevivência**

    1. Sincronize dois jogadores e eventos de coleta.
    2. Garanta identidade estável para entidades.
    3. Evite eventos duplicados na reconexão.
    4. Registre divergências para depuração.

- [ ] **LRN-054 — Predição, interpolação e autoridade** · Competência: latência, snapshots, reconciliação, lag e anti-cheat básico. Pré-requisito: LRN-053.

  - [ ] **LRN-054-P1 — Movimento sob latência**

    1. Simule atraso e jitter controlados.
    2. Interpole entidades remotas.
    3. Prediga somente jogador local.
    4. Compare divergência e reconcilie ao receber estado autoritativo.

  - [ ] **LRN-054-P2 — Combate consistente**

    1. Defina autoridade para vida/dano.
    2. Evite cliente determinar unilateralmente acerto.
    3. Simule pacotes fora de ordem e duplicados.
    4. Comprove resultado final consistente entre pares.

- [ ] **LRN-055 — Servidor dedicado, escala e segurança** · Competência: servidor headless, validação, reconexão e limites operacionais. Pré-requisito: LRN-054.

  - [ ] **LRN-055-P1 — Arena com servidor**

    1. Separe regras compartilhadas de cliente e servidor.
    2. Execute sessão com servidor sem interface gráfica.
    3. Imponha validação de entradas no servidor.
    4. Finalize e limpe sessões/recursos.

  - [ ] **LRN-055-P2 — Teste de resistência em rede**

    1. Simule várias conexões sob perda de pacotes.
    2. Limite taxa e tamanho de mensagens.
    3. Rejeite pacotes malformados sem queda do servidor.
    4. Mensure desempenho e documente escala real, sem chamar de MMO.

### T12 — Produção, qualidade e lançamento

**Resultado esperado:** Transformar competências técnicas e criativas em jogos finalizados, testados, publicados e mantidos. **Entrada sugerida:** LRN-050.

- [ ] **LRN-056 — Conceito, escopo e prototipagem** · Competência: pitch, GDD enxuto, core loop, vertical slice e priorização. Pré-requisito: LRN-050.

  - [ ] **LRN-056-P1 — Protótipo em uma tela**

    1. Escreva objetivo e público-alvo do jogo.
    2. Escolha uma mecânica central e um limite de escopo.
    3. Construa fatia jogável mínima.
    4. Liste hipóteses e descarte funcionalidades não comprovadas.

  - [ ] **LRN-056-P2 — Vertical slice**

    1. Descreva experiência-alvo com referências lícitas.
    2. Implemente um trecho com arte, áudio e interação finais.
    3. Inclua começo e fim da experiência.
    4. Compare resultado com três critérios previamente escolhidos.

- [ ] **LRN-057 — Playtest, balanceamento e iteração** · Competência: feedback, hipóteses, métricas sem telemetria obrigatória, dificuldade. Pré-requisito: LRN-056.

  - [ ] **LRN-057-P1 — Laboratório de balanceamento**

    1. Defina parâmetro mensurável de dificuldade.
    2. Crie duas variantes com mesma mecânica.
    3. Registre observações locais de tentativas.
    4. Escolha ajuste com evidências e não apenas intuição.

  - [ ] **LRN-057-P2 — Teste com jogadores**

    1. Prepare roteiro curto com consentimento.
    2. Observe onde jogador hesita sem guiá-lo em tempo real.
    3. Registre problemas de controle, clareza e diversão.
    4. Corrija os dois principais problemas e repita o teste.

- [ ] **LRN-058 — Pipeline de produção e direitos** · Competência: gestão de assets, licenças, versionamento, equipe e acessibilidade. Pré-requisito: LRN-057.

  - [ ] **LRN-058-P1 — Kit de produção reutilizável**

    1. Organize código, assets editáveis e derivados.
    2. Registre licenças e atribuições necessárias.
    3. Exporte cópia válida sem depender de biblioteca mutável.
    4. Restaure projeto de backup preservando conteúdo.

  - [ ] **LRN-058-P2 — Entrega para colaborador**

    1. Documente convenções de código e arte.
    2. Faça commit Git de mudança pequena e reversível.
    3. Trate conflito simulado sem perder trabalho.
    4. Produza pacote reproduzível com notas e fontes.

- [ ] **LRN-059 — Distribuição e manutenção Android** · Competência: versão, assinaturas, empacotamento, testes e changelog. Pré-requisito: LRN-058.

  - [ ] **LRN-059-P1 — APK jogável**

    1. Prepare nome, ícone e identificação de pacote.
    2. Gere APK standalone do projeto.
    3. Teste instalação, abertura e ciclo básico do jogo.
    4. Documente versão, limitações e procedimento de atualização.

  - [ ] **LRN-059-P2 — Atualização sem perda**

    1. Crie segunda versão do mesmo jogo.
    2. Mantenha assinatura e identificação compatíveis.
    3. Teste preservação de saves e configuração em atualização.
    4. Registre regressões e forneça plano de recuperação.

- [ ] **LRN-060 — Projeto autoral de conclusão** · Competência: integração multidisciplinar, publicação, retro e evolução. Pré-requisito: LRN-059.

  - [ ] **LRN-060-P1 — Jogo autoral completo**

    1. Defina escopo fechado e três pilares de design.
    2. Implemente ciclo completo com menu, controles, áudio, arte e progressão.
    3. Inclua save, testes e tratamento de falhas relevantes.
    4. Distribua jogo standalone com instruções e créditos.

  - [ ] **LRN-060-P2 — Versão 1.1 sustentada**

    1. Colete e classifique feedback do jogo completo.
    2. Escolha correção e melhoria de maior valor.
    3. Implemente sem destruir saves e comportamentos existentes.
    4. Publique atualização versionada com changelog e revisão do que foi aprendido.

## Competências transversais que todos os cursos devem revisitar

- [ ] **Código legível e seguro:** nomes claros, controle de erros e ausência de lógica oculta por interface visual.
- [ ] **Teste progressivo:** pelo menos um caso esperado e um caso-limite relevante por projeto.
- [ ] **Matemática aplicada:** sistemas de coordenadas, tempo, unidades, vetores, probabilidade e estatística onde pertinentes.
- [ ] **Usabilidade mobile:** toque, safe areas, proporções, navegação de retorno e ergonomia.
- [ ] **Acessibilidade:** contraste, feedback multimodal, alternativas de controle e opções quando necessárias.
- [ ] **Direitos e proveniência:** áudio, imagens, dados e exemplos próprios ou com licenças explícitas.
- [ ] **Autonomia:** o segundo projeto de cada curso mede transferência; ajuda opcional não oculta autoria.
- [ ] **Desempenho:** comparar custos onde pertinente, sem números de FPS inventados ou benchmarks não reproduzíveis.
- [ ] **Privacidade/offline:** recursos didáticos não dependem de conta, internet ou telemetria.
- [ ] **Publicação:** todo produto final é executável fora do modo Aprender e permanece sob controle do estudante.

Essas caixas transversais representam a **capacidade do currículo integrado de cobrar os critérios**, não a existência isolada dessas APIs no Lunet. Não marcar [x] até implementação/cobertura verificável.

## Manutenção, dependências e expansão

1. A primeira implantação deve começar por LRN-001-P1 e LRN-001-P2, mas não marcar o curso sem evidência de ambos no aplicativo.
2. Quando uma API necessária não estiver operacional no APK, a equipe registra o curso como indisponível ou propõe alternativa C# verificável; não oferece botão falso.
3. Mudanças de currículo não alteram o progresso de cursos antigos por nome; migrar por IDs e versão.
4. Ajustes editoriais preservam sentido dos objetivos; objetivos que mudam resultado exigem revisão documentada.
5. Futuros cursos de gêneros (roguelike, RPG narrativo, sandbox, economia, simulação social, metroidvania etc.) podem ser adicionados após verificar lacunas, sem inflar esta primeira matriz de 60.
6. Este catálogo é **2D-first**. As competências de segurança, produção, trabalho em equipe e negócio complementam domínio de desenvolvimento; não representam formação irrestrita em todo o setor.

## Referências pedagógicas

Ver [PESQUISA.md](PESQUISA.md) e [MANIFESTO.md](MANIFESTO.md). A estrutura projetos/competências foi desenhada especificamente para Lunet em C#, com pesquisa sobre CS50 2D, Godot, Unity Learn, IGDA, aprendizagem por competência e suporte opcional a iniciantes.
