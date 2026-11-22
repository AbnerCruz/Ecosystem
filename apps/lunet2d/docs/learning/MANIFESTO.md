# Manifesto do Lunet Aprender

**Data:** 22/11/2026  
**Estado:** especificação de produto e direção pedagógica proposta pelo proprietário; **não implementado**.  
**Produto:** Lunet2D, código canônico somente em [AbnerCruz/Ecosystem/apps/lunet2d](../../).  
**Autoridade:** subordinada a [MANIFEST.md](../../../../MANIFEST.md), [SPEC.md](../SPEC.md), [ROADMAP.md](../../ROADMAP.md), ADR 0007 e ADD-0035. Este documento governa a filosofia da função Aprender, sem alterar limites do Framework, IDE, Studio, IA ou governança.  
**Acompanhamento:** [MATRIZ-CURRICULAR.md](MATRIZ-CURRICULAR.md) contém o programa de cursos; [IMPLEMENTACAO.md](IMPLEMENTACAO.md) contém os incrementos técnicos; [PESQUISA.md](PESQUISA.md) registra fontes e decisões.

## 1. Tese

**Aprender a desenvolver jogos desenvolvendo jogos.** A aba Aprender combate a paralisia da página em branco com objetivos inequívocos, projetos executáveis, resultados observáveis e progressão crescente. Ela não é uma biblioteca de vídeos, um quiz, uma fábrica de jogos prontos pela IA nem uma segunda IDE.

O usuário recebe a missão e as condições de sucesso. Deve conceber a solução, escrever C#, investigar a documentação do Framework, experimentar no Preview, depurar e concluir um projeto verdadeiro. A assistência é solicitada, nunca imposta. Cada projeto é um jogo ou sistema reaproveitável, independente da existência da aba.

A formação deve ir do primeiro quadrado em movimento à engenharia, arte, design, otimização e publicação de jogos 2D completos. Abrangência não significa fingir que o Lunet é uma engine 3D, nem prometer uma API inexistente.

## 2. Dez princípios

1. **Projeto primeiro:** nenhuma unidade obrigatória é apenas aula, questionário ou exercício descontextualizado. A menor entrega é um protótipo funcional e significativo.
2. **Mão no código:** lógica de jogo em C# inspecionável. Studios podem produzir arte, áudio e dados, mas nunca se tornam um atalho obrigatório que oculta lógica.
3. **Dizer o que, não como:** enunciados mostram objetivos, restrições e evidências de aceite; soluções, código e técnicas aparecem sob pedido de ajuda, não por padrão.
4. **Escopo controlado:** cada projeto tem início, fim, critérios de pronto e complexidade proporcional ao domínio anterior. Acréscimos criativos são opcionais e não bloqueiam a conclusão.
5. **Progresso honesto:** um checkbox não prova conhecimento. Distinguir projeto executado, verificação automática, revisão e domínio autônomo.
6. **Ajudar sem sequestrar:** IA deve atuar como mentor contextual. Explicação não altera arquivos; edição exige consentimento explícito e preserva undo/diff.
7. **Liberdade com orientação:** trilhas sugeridas e pré-requisitos, mas o usuário pode explorar, revisar, repetir e saltar desafios; nenhum bloqueio artificial ou punição.
8. **Local primeiro:** cursos-base, matrizes, documentação e histórico de progresso disponíveis offline, sem conta, telemetria ou chave de IA.
9. **Projetos normais:** obras pertencem ao usuário, guardadas em pastas Lunet comuns; curso não cria dependência obrigatória nem arquivos ocultos de runtime no jogo.
10. **Integração verificável:** conteúdo só é publicado como disponível quando sua experiência no app, contratos, testes e referências pertinentes estão integrados e documentados.

## 3. Hierarquia e terminologia

- **Trilha (T01…T12):** conjunto temático de competências, sem obrigar linearidade total.
- **Curso (LRN-001…LRN-060):** objetivo de aprendizagem verificável e conjunto de projetos, com pré-requisitos e dificuldade.
- **Projeto (LRN-###-P1/P2):** jogo ou sistema real, com entregável, etapas e critérios de aceitação. A matriz inicial usa dois projetos por curso; expansão é versionada e não muda IDs antigos.
- **Etapa (S1…S4):** requisito observável do projeto; não uma linha de código prescrita.
- **Competência:** habilidade demonstrada por evidência de execução e, para domínio autônomo, reprodução ou extensão independente.
- **Referência:** documentação, exemplo, ferramenta ou material adicional consultável, sem copiá-lo obrigatoriamente.

Uma *etapa* pode exigir múltiplas atividades de engenharia; a ordem deve funcionar como uma progressão, mas não oferecer a implementação mastigada. Cada curso deve incluir ao menos um projeto de transferência, que exige aplicar conceitos sem reproduzir a solução do projeto anterior.

## 4. Regras da matriz de checkbox

Há **duas fontes de estado que não podem se confundir**:

1. **GitHub — disponibilidade do produto e conteúdo:** [ROADMAP.md](../../ROADMAP.md) é autoridade de implementação da aba; [MATRIZ-CURRICULAR.md](MATRIZ-CURRICULAR.md) é autoridade da cobertura pedagógica. A caixa de curso só vira [x] depois de ambos os projetos serem efetivamente utilizáveis na versão integrada, com conteúdos, referências compatíveis, critérios, testes e evidências no PR/CI. A caixa individual de projeto só vira [x] quando o projeto foi incorporado ao catálogo versionado e passou seus respectivos critérios. Texto escrito neste PR **não conta como curso implementado**.
2. **Dispositivo — progresso do estudante:** é persistido em armazenamento próprio do Lunet, por usuário local/instalação e por IDs estáveis (curso, projeto, etapa), não em Markdown versionado. Acompanhamento do estudante não altera a matriz do GitHub.

Estados do estudante: **não iniciado / em andamento / concluído por declaração / verificado automaticamente / revisado**. Evidência de **domínio independente** é separada de execução assistida, sem expor histórico privado de prompts. Percentual de progresso deve indicar a dimensão contada (etapas, projetos ou cursos); não confundir percentual de catálogo implementado com percentual aprendido.

O padrão [x] de integração segue ADD-0035: integração na main, automação/CI verificável e documentação; não exigir aprovação humana nem alegar teste físico inexistente. Se o requisito não é verificável automaticamente, indicar o limite e usar estado honesto. Nunca substituir evidência por marcação arbitrária.

## 5. Método de aprendizagem

**Receber → investigar → construir → testar → refletir → estender.**

- **Receber:** objetivo em linguagem clara, comportamento esperado, restrições, critérios de pronto, conhecimentos de referência e opções de acessibilidade.
- **Investigar:** consultar documentação offline, código próprio, exemplos externos opcionais e ferramentas do Lunet; nenhuma etapa depende da rede.
- **Construir:** escrever C# e produzir assets conforme o projeto exige; a IDE continua sendo a superfície de produção.
- **Testar:** Preview e testes apropriados; resultados observáveis na tela, logs e eventos. Compilar não comprova design nem jogabilidade.
- **Refletir:** registrar brevemente o que funcionou, o que falhou e por quê; pode ser nota opcional, não prova discursiva obrigatória.
- **Estender:** segundo projeto ou desafio bônus exige reutilização crítica sem copiar uma solução pronta.

A dificuldade sobe em eixos independentes: quantidade de conceitos novos, interações entre subsistemas, autonomia exigida, robustez, escala, requisitos de qualidade e produção. Não aumentar complexidade apenas tornando o enunciado vago.

## 6. IA em modo mentor

O botão **Pedir ajuda** acompanha o desafio do Aprender e pode aparecer como painel compacto acima do editor, com retorno imediato à etapa. A IA recebe apenas o contexto autorizado: objetivo e rubrica, etapa, API documentada da versão instalada, arquivo/seleção, diagnósticos e, quando necessário, mapa do projeto.

Escada de apoio, sempre sob demanda:

1. Pergunta orientadora ou pista conceitual.
2. Conceito, algoritmo ou referência da API relevante, sem solução específica.
3. Esqueleto pequeno e explicado de uma parte do problema.
4. Revisão do código que o estudante escreveu e dos erros encontrados.
5. Demonstração completa ou **Fazer por mim**, somente sob pedido inequívoco, com diferença registrada entre obra concluída e domínio independente.

Em modo Aprender, **Ensinar** é comportamento padrão. **Auto** não deduz autorização para escrever arquivos só porque o aluno perguntou “como faço?”. **Fazer** demanda escolha explícita; usar segurança de edições, snapshots e rollback existentes, nunca sobrescrever código silenciosamente. Não premiar nem punir por quantidade de chamadas à IA. Nenhum curso exige conta, API paga, acesso web ou modelo específico.

## 7. Navegação e UX

A navegação principal compacta proposta é **Início · Projetos · Studios · Explorar · Aprender**. Há cinco destinos de alto nível; Biblioteca, Documentação e Configurações mantêm seus pontos de entrada existentes. Em tela grande, adaptar barra para rail. Não inserir uma sexta aba sem redesenho.

**Aprender — início:** “Continuar”, progresso por trilha, curso atual, próxima entrega, catálogo filtrável por área/dificuldade/competência, projetos concluídos e opções de retomar. Em vez de dezenas de cards desenhados de uma vez, renderização incremental e busca local.

**Trilha:** mapa de competências com dependências reais e opção de visualização em lista acessível. Não impor desbloqueios artificiais. **Curso:** resumo do que será aprendido, dois projetos, pré-requisitos, entregáveis, estado do catálogo e progresso da pessoa. **Projeto:** missão, etapas claras, critérios de sucesso, documentação, botão “Criar/associar projeto” e “Continuar no editor”.

**Durante o trabalho:** projeto aberto na IDE normal, etapa visível em cartão compacto invocável ou pop-up recolhível; ao recolher, código/Preview ocupam a área útil. Botão de ajuda não deve fechar o editor ou perder foco/teclado. Voltar não apaga editor, progresso, projeto nem execução em andamento.

**Visual:** barra de progresso real, não apenas checkboxes; tipografia e contraste acessíveis; controles principais com área de toque aproximada de 48 dp; compatibilidade com TalkBack e fontes ampliadas; sem exigir gestos exclusivos. Respeitar keyboard IME, safe areas, orientação e telas estreitas como moto g32. Não criar mais uma dashboard poluída.

## 8. Contratos e ownership previstos

- Catálogo pedagógico **somente leitura**, distribuído localmente e versionado junto ao Lunet, com IDs estáveis, esquema migrável, pré-requisitos acíclicos, referências a APIs testadas e compatibilidade declarada por versão.
- Estado de aprendizagem **mutável e local**, armazenado em diretório privado separado de Projects, com persistência atômica, backup/exportação opcional, migração, integridade e recuperação segura. Não guardar dados do aluno na documentação versionada.
- Associação entre desafio e projeto por ID estável de projeto ou mecanismo não destrutivo; projeto criado continua normal ao desvincular ou concluir o desafio. Não duplicar o código do jogo numa “cópia didática” opaca.
- Cursos podem referenciar assets de exemplos próprios com licença apropriada, mas projetos iniciantes não precisam baixá-los. Não incluir jogos, arte, música ou marcas de terceiros sem direitos.
- Avaliadores são componentes opcionais e testáveis; verificação estática, testes de runtime e evidência declarada devem permanecer distintos. Não prometer interpretação universal de qualquer jogo C# sem falsos positivos.
- Integração com IA e Studio deve reutilizar contratos existentes, respeitando a separação Framework/IDE/Studio e source-of-truth de ADR 0007. A aba é cliente do editor/projeto, nunca runtime obrigatório.
- Conteúdo e progresso do estudante são dois domínios distintos. A troca da versão do curso não pode desmarcar uma conquista do aluno sem migração explícita.

## 9. Primeiro projeto de referência — Aprendendo movimento

**ID:** LRN-001-P1. **Entrega:** jogo single-screen de uma fase, controlável por toque, com condição de vitória.

1. Desenhar um quadrado visível em posição inicial fixa.
2. Permitir que a pessoa mova o quadrado com controle utilizável no dispositivo.
3. Desenhar um círculo-alvo separado do quadrado.
4. Detectar o contato entre as formas e encerrar a partida exibindo condição inequívoca de vitória.

**Aceite:** as duas formas aparecem; o controle reage; no início não existe vitória; o toque ativa o estado de vitória uma única vez; ao reiniciar, estado e posições voltam ao início; não há crash, e o projeto continua abrindo fora de Aprender. O enunciado não determina classe, desenho ou algoritmo; qualquer implementação correta via C# e APIs da versão serve. Um verificador automatizado de estados só pode ser habilitado se for confiável; caso contrário registrar teste orientado e declaração do estudante.

**Sem exigir:** download de sprite, IA, multiplayer, conta, física rígida, tutorial em vídeo ou lógica escondida.

## 10. Fronteiras e riscos assumidos

- “Absolutamente tudo” é uma meta de amplitude formativa, não afirmação impossível de completude definitiva. A matriz inicial cobre 2D e áreas conexas (design, arte, áudio, programação, matemática, redes, testes, produção), expansível com mudanças versionadas.
- Conteúdo de multiplayer, shaders, physics/Studio ou plataformas só pode ser anunciado como exercitável se a API correspondente estiver disponível e integrada; uma capacidade planejada não é pré-requisito disfarçado.
- IA não certifica diversão, qualidade artística, acessibilidade ou domínio intelectual automaticamente.
- Séries intermináveis de exercícios, streaks, monetização de dicas, vidas e gamificação punitiva não pertencem ao núcleo.
- O catálogo não modifica nem migra projetos pessoais sem consentimento, e o progresso não depende de serviço externo.

## 11. Prioridades de implantação

- **Fatia 0:** modelo de conteúdo + progresso local + roteamento de Aprender, um primeiro projeto real (LRN-001-P1), os outros 119 em breve, ajuda/retorno ao editor, offline e testados.
- **Fatia 1:** T01 completo, documentação contextual, retomada de sessão e validação gradativa.
- **Fatia 2:** T02–T04, rastreamento de competências, projetos independentes e evidências.
- **Fatia 3:** T05–T08, integração real de Studios, áudio, gráfica/UI.
- **Fatia 4:** T09–T12, engenharia, IA, multiplayer, produção, revisão do catálogo.
- **Evolução:** novos cursos e percursos por gênero com IDs/compatibilidade estáveis, sem alterar conquistas preexistentes.

Cada fatia deve ter teste de persistência, catálogo íntegro, navegação Android, desempenho aceitável e projeto executável; detalhes e caixas técnicas em [IMPLEMENTACAO.md](IMPLEMENTACAO.md). Este manifesto não autoriza introduzir recursos novos no Framework sem itens próprios no ROADMAP.

## 12. Referências e comparação crítica

Pesquisa documentada em [PESQUISA.md](PESQUISA.md). Referências primárias: [Harvard CS50 2D](https://cs50.harvard.edu/games/), [Unity Learn Pathways](https://learn.unity.com/pathways), [Godot: primeiro jogo 2D](https://docs.godotengine.org/en/stable/getting_started/first_2d_game/), [IGDA Education SIG](https://www.edusig.org/projects.html), [UNESCO-UNEVOC: competency-based education](https://connect.unevoc.unesco.org/home/TVETipedia%2BGlossary/lang%3De/show%3Dterm/lang%3Den/term%3DCompetency-based%2Beducation), [ICER 2022: scaffold opcional](https://doi.org/10.1145/3501385.3543977), [Android: navegação](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-and-nav-patterns) e [Android: offline-first](https://developer.android.com/topic/architecture/data-layer/offline-first).

Não copiar currículo, projetos protegidos, marcas, imagens ou software de terceiros. Adaptar princípios, construir material autoral e rastrear licenças.

## 13. Ajuste da primeira liberação (22/11/2026)

Por decisão do proprietário, o catálogo exibe desde a primeira release todos os 60 cursos, mas **somente LRN-001-P1** fica disponível. Os demais 119 projetos ficam sinalizados como **Disponível em breve**, incluindo LRN-001-P2. O primeiro curso está apenas parcialmente aberto e não pode ter sua caixa marcada como concluída. A área de trabalho ganha chat geral e configurações de IA; o chat de cada jogo mantém contexto e histórico próprios e apresenta somente o seletor de modelo, sem tela de configurações de provedor/chave/permissões. O chat geral não tem ferramentas de leitura/escrita de projeto. Estas decisões integram LUNET-520 e LUNET-521.
