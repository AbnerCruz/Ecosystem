# Urbe nativo — Experiência de usuário

> **Autoridade:** [ADD-0026](../../../../docs/governance/addenda/ADD-0026-urbe-experiencia-de-usuario-topo-de-linha.md) (DEC-0049).
> Este documento é o critério de aceite de toda tela, fluxo e PR de interface do cliente nativo.
> O ROADMAP C# diz **o que** se faz; este documento diz **como** cada coisa precisa ser sentida.

O proprietário (16/10/2026): *"o ponto central das observações: EXPERIÊNCIA DE USUÁRIO. Topo de linha, o mais moderno dos modernos, tecnologia e psicologia de ponta."*

O Urbe tem dois pilares: **desempenho** (ADD-0024) e **fluxo de produção** (observações da beta.13). Experiência de usuário é o que une os dois. Funcionalidade que existe mas não é agradável, imediata e óbvia **não está pronta**.

> **Direção adicional do proprietário, 28/10/2026:** **menos interfaces, visual minimalista, intuição e elegância**, mesmo com o crescimento das capacidades. Mais funcionalidades não implicam mais telas, estúdios, modos, barras ou painéis. Este é um **requisito de arquitetura da experiência**, não um simples tema visual. A extensão de documentos computacionais, editor de código e ferramentas interativas (UC-92) deve servir de prova; referência: [COMPUTATIONAL-DOCUMENTS-AND-TOOLS.md](COMPUTATIONAL-DOCUMENTS-AND-TOOLS.md). A antiga cidade está congelada por DEC-0051 e não é reintroduzida por trechos históricos deste documento.

## 0. Calibração (proprietário, 16/10/2026)

Não há um app de referência. A escala dada pelo proprietário foi: *"o Notion tá frio, o Obsidian tá morno"*. O Urbe precisa estar **quente**.

- **Frio (Notion):** correto e organizado, mas impessoal. Blocos e telas genéricos, sem personalidade e sem vida.
- **Morno (Obsidian):** perto do que importa (local-first, poder e controle sobre as notas), mas ainda uma ferramenta seca, que pede configuração e esforço.
- **Quente (Urbe):** o poder do Obsidian com alma.
  - As notas moram numa cidade viva, e o app reage a quem escreve com movimento, som e tato discretos.
  - Os pequenos momentos dão prazer: criar uma nota, ver a casa surgir, salvar, voltar ao trabalho.
  - Tudo responde na hora e nada é burocrático.
  - O calor vem do cuidado em cada detalhe, não de enfeite que atrapalha (P3, P11).

## 1. Princípios

Cada princípio vem com a base (tecnologia ou psicologia) e com a forma de medir.

| # | Princípio | Base | Como se mede |
|---|---|---|---|
| P1 | **Resposta imediata.** Todo toque produz reação visual em até 100 ms; nenhuma ação deixa a pessoa esperando sem retorno. | Limiar de Doherty (< 400 ms mantém a atenção); limites de 0,1 s / 1 s / 10 s de Nielsen. | Testes de desempenho headless: ms do toque ao primeiro quadro alterado; ms por tecla. |
| P2 | **Interface otimista.** O que a pessoa fez aparece na hora. Gravação, índice e cidade se atualizam por trás, sem bloquear. Falha real é avisada sem perder nada. | Percepção de velocidade; controle percebido. | Nenhum `await` de disco ou rede no caminho do toque até o quadro. |
| P3 | **Fluxo é sagrado.** Nada interrompe quem está escrevendo: sem janelas modais no meio do texto, sem saltos de rolagem, sem mudança de foco. Avisos são discretos e somem sozinhos. | Estado de fluxo (Csikszentmihalyi); custo de troca de contexto. | Auditoria de cada fluxo: zero diálogos modais não pedidos; rolagem estável (teste de posição do bloco). |
| P4 | **O polegar manda.** Ações frequentes ficam na metade de baixo da tela, ao alcance do polegar. Alvos de toque têm pelo menos 48 dp. | Lei de Fitts; zonas de alcance do polegar. | Teste de layout em 412×900: alvos ≥ 48 dp; ações primárias nos 40% de baixo. |
| P5 | **Poucas escolhas por vez.** Cada tela mostra o essencial. O resto aparece quando é pedido: menus contextuais, segurar, paleta de comandos. | Lei de Hick; carga cognitiva; revelação progressiva. | Máximo de 5 a 7 ações visíveis por barra; o resto em "Mais". |
| P6 | **Movimento com sentido.** Transições curtas (150 a 250 ms) e com curva natural mostram de onde as coisas vêm e para onde vão. Nada pisca nem salta. | Continuidade espacial; animação com física de mola. | 60 quadros por segundo nas transições; nenhuma transição acima de 300 ms. |
| P7 | **Tato.** Vibração leve (háptico) para confirmar segurar, soltar, concluir e erro, no Android. | Retorno multissensorial. | Lista de eventos com háptico, testada no aparelho. |
| P8 | **Gestos naturais.** Arrastar da borda volta. Pinçar dá zoom. Segurar abre opções. Deslizar num item da lista dá ações rápidas. Tudo tem alternativa por botão. | Lei de Jakob: as pessoas esperam o que já usam em outros apps. | Cada gesto tem teste headless e equivalente por botão. |
| P9 | **Beleza que funciona.** Um sistema visual único: tipografia, espaçamento em grade de 8, raio, cor e elevação definidos uma vez e usados em todo lugar. Nada desalinhado. | Efeito estética-usabilidade. | Tokens de design em `UrbeTheme`; teste que acusa medidas fora da grade nas telas principais. |
| P10 | **Começo e fim marcantes.** Abrir o app é instantâneo e cai onde a pessoa parou. Concluir algo tem um fechamento claro e agradável. | Regra do pico-fim; efeito Zeigarnik (retomar o que ficou aberto). | Tempo até a última nota editável (UC-59); retomada da sessão. |
| P11 | **A interface é calma.** Nenhuma superfície ou animação disputa atenção com o conteúdo; informação contextual aparece apenas quando importa. | Tecnologia calma; atenção periférica. | Tela comum sem controles computacionais extras; nenhum salto de foco/scroll ou overlay intrusivo; sem simulação/cidade (DEC-0051). |
| P12 | **Acessível para todos.** Leitor de tela, contraste AA, fonte ajustável, nada que dependa só de cor. | WCAG 2.2 AA. | Rótulos de automação em todos os controles; contraste verificado nos temas. |
| P13 | **Personalização sem custo.** Tudo que é preferência pode ser ajustado: barra do editor, atalhos, temas. O padrão já é excelente sem ajuste nenhum. | Controle percebido; padrões bons. | Preferências em `Personalização/`; o padrão passa em todos os itens acima. |

## 1.1. Minimalismo estrutural: mais capacidade, menos interface (28/10/2026)

A complexidade existe no motor do Urbe; **não deve ser transferida ao usuário**. Estudo de referência: [Nielsen Norman Group — progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/) e [complex application design](https://www.nngroup.com/articles/complex-application-design/); [Android — layouts e navegação](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-and-nav-patterns).

- **Reutilizar antes de adicionar.** Uma nova função deve entrar na superfície que já contém o objeto, sempre que possível. Exigir justificativa com alternativas e teste antes de criar aba, estúdio, modo, toolbar, menu, FAB ou painel.
- **Uma área de trabalho, uma experiência de edição.** A ferramenta contextual se ajusta ao arquivo/seleção; não criar três áreas `Documentos`, `Código`, `Laboratório`. `.cs` tem ferramentas de código no editor; `.md` continua nota; gráficos/controles aparecem dentro do próprio texto.
- **Conteúdo domina.** Na nota comum, nenhuma nova barra computacional ou widget aparece. Os controles de um bloco só surgem quando aquele bloco está em foco/interação e não cobrem permanentemente a leitura.
- **Descoberta sem barulho.** Ações frequentes visíveis e compreensíveis; avançadas são reveladas por uma entrada contextual previsível. Não esconder a única ação atrás de gesto secreto ou níveis intermináveis.
- **Uma rota por intenção.** Evitar ações duplicadas em diferentes painéis. Um resultado é editável no contexto ou possui um caminho claro até sua fonte e retorno exato.
- **Pouca decoração, hierarquia forte.** Tipografia, alinhamento, espaçamento e uso comedido de cor resolvem hierarquia. Evitar caixas aninhadas, overlays múltiplos, ícones redundantes, badges ornamentais e movimento gratuito.
- **Espaço e gestos respeitados.** Editor, formulário, gráfico e código não disputam scroll, caret, teclado nem seleção. Expandir uma ferramenta espacial é exceção explícita; ao voltar, foco e âncora são restaurados.
- **O sistema faz o trabalho difícil.** Criar ferramenta simples não exige escrever JSON/código nem escolher kernel. Autoria por IA é facultativa, com preview e aprovação, sem sequestrar a interface.

### Gate para qualquer PR de interface

Além de P1–P13, a revisão deve registrar **tarefa** e fluxo anterior/novo; **interfaces/controles adicionados versus reutilizados/removidos**; número e motivo das transições obrigatórias; evidência de descobribilidade sem tutorial; captura do mesmo conteúdo antes/depois; espaço disponível com Gboard; teste de gestos, foco e rolagem em Android. Números absolutos de desempenho/espaço dependem de baseline. **Uma funcionalidade nova que aumenta fricção visual ou de navegação sem ganho demonstrado não passa**, ainda que o backend esteja correto.

**Acordo de escopo:** UI minimalista não significa retirar recursos úteis nem esconder funções essenciais. Significa torná-los acessíveis no contexto certo, sem painéis globais para cada recurso.

## 2. Fundação técnica (antes de redesenhar telas)

1. **Tokens de design** (`UrbeTheme`): escala tipográfica, espaçamentos em múltiplos de 8, raios, elevação/sombra, cores semânticas (fundo, superfície, texto 1/2/3, acento, perigo, sucesso) e durações e curvas de animação. Toda tela passa a usar só os tokens.
2. **Componentes únicos:** botão, botão de ícone, campo, lista com deslizar, folha inferior (bottom sheet), aviso discreto (toast), menu contextual e cabeçalho. Cada um com estados (normal, pressionado, foco, desativado), háptico e rótulo de acessibilidade.
3. **Motor de transições:** entrada e saída de telas e folhas com mola; nenhuma troca seca de `IsVisible` nas telas principais.
4. **Orçamento de desempenho na suíte:** testes que falham se o toque passar de 100 ms até o quadro, a tecla passar de 16 ms ou a abertura passar do limite (UC-59/UC-60).

## 3. Ordem de redesenho (tela por tela)

Cada tela é auditada contra P1–P13, redesenhada e entregue com testes e capturas antes e depois:

1. **Editor de notas:** é onde a produção acontece.
   - escrever sem saltos e com o cursor sempre visível (UC-57);
   - barra junto ao teclado, configurável;
   - seleção nativa;
   - menu `/` com busca;
   - gravação invisível;
   - leitura e escrita com tipografia de livro.
2. **Abertura e primeira vez:** abrir direto na última nota (UC-59) e a configuração inicial guiada (UC-58).
3. **Explorer:**
   - deslizar para ações e arrastar com háptico;
   - busca instantânea;
   - criação de nota em um toque.
4. **Ferramentas incorporadas:** gráficos, cálculos, formulários e edição de código no contexto do artefato, sem novo HUD ou modo global (UC-92).
5. **Estúdio de páginas:** edição direta, ferramentas antes de modelos (UC-62).
6. **Assistente, Personalização, Tutorial, Configurações e widget.**

## 4. Como uma tela é aceita

- Passa nos princípios mensuráveis acima, com teste automatizado sempre que for possível.
- Tem capturas antes e depois anexadas ao PR.
- É validada pelo proprietário no aparelho (NN-017). Validação no aparelho é o que fecha.

## 4. Assistente contextual: execução presente, interface discreta (24/10/2026, issue #746)

A interface do Assistente tem **duas apresentações do mesmo estado**, não dois agentes: a conversa completa e o cartão contextual para `&Agente&:` na nota. O launcher permanente de IA ocupa 48 dp no canto inferior direito. A navegação principal não reserva uma aba inteira para algo que pode ser invocado localmente. A ação explícita no launcher abre a conversa completa, com modelos, custos/tokens, memória, histórico, pesquisa, fontes, resultados e permissões; nada disso é eliminado.

O pedido iniciado na nota não toma o foco nem abre a conversa quando começa ou termina. A folha contextual mostra apenas o estado atual: pensamento/etapa e progresso; pergunta com resposta; aprovação com prévia, edição e rejeição; ou resposta/erro. "Abrir conversa" é sempre uma decisão do usuário. Se o agente alterar a nota, a sugestão inline continua prioritária e o pop-up espelha a mesma aprovação, sem criar um caminho paralelo de autorização. Se faltar provedor/modelo, configuração completa é exceção justificada.

**Regressões bloqueantes**: perda de parâmetros/resultados no painel completo; aprovação automática por abrir/fechar pop-up; teclado virtual aberto espontaneamente; perda da seleção/cursor ou saltos na rolagem do editor; o cartão cobrir permanentemente o texto; botão com alvo de toque inferior a 48 dp; rodar IA em thread UI; recriar o transcript inteiro a cada token; renderizar progresso animado sob configuração de movimento reduzido. Testar 412×900 e desktop amplo; prever teclado IME e gesto de voltar. O status do pop-up deve refletir a execução real, não um indicador simulado.

**Inspiração, não cópia**: conversas modernas que mantêm conteúdo no fluxo sem dezenas de painéis; agentes/coders que deixam atividades auditáveis sob expansão; interfaces contextuais com ações pontuais e saída explícita para a visão completa. Uma estética minimalista depende de tipografia, ritmo e hierarquia, não de uma coleção de caixas translúcidas e bordas decorativas. As informações continuam descobríveis sem ritual de configuração.
