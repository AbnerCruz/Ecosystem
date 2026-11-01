# Numa — Plano definitivo de consolidação do produto

Versão documental: **2.5 — 01/11/2026**. Bases preservadas: 2.0 (25/10), 2.1 (Publicações), 2.2 (reconciliação integral do FeaturesCheck), 2.3 (investigação de IA computacional) e 2.4 (reconciliação da IA com o produto). A revisão 2.5 aprova **progresso nativo nas notas e acompanhamento automático de checklists**, com duas entregas novas e verificáveis (CST-227–228), sem alterar as decisões das revisões anteriores. **Revisão exclusivamente documental:** não houve alteração de código, PR, compilação, APK ou aceite físico. O Editor de Páginas legado permanece substituído por Publicações; o histórico FeaturesCheck é preservado integralmente.

## 1. Direção e alcance

Urbe é um ambiente de produção sobre arquivos locais: editor confiável, ferramentas integradas e agentes capazes de trabalhar com contexto, clareza e controle. Android é prioritário, com validação também no Windows. O moto g32 / Android 13 é o aparelho de referência de uso; não é o único ambiente suportado.

Este documento reúne o FeaturesCheck e o checklist de consolidação anexados, incorpora a comparação feita nesta conversa, a auditoria estática do código C# do Urbe e a revisão de sistemas externos de publicação, incluindo Claude Motion, e corrige as omissões identificadas. É a versão completa de planejamento desta revisão. “Definitivo” refere-se à consolidação documental, não a uma promessa de que requisitos nunca mudarão ou de que o produto já foi validado.

Os estados iniciais são documentais: a revisão de Publicações incluiu inspeção estática de arquivos da `main` do Ecosystem, mas não compilação, APK novo ou teste no aparelho nesta revisão. Uma implementação registrada pode ter defeitos; um relato não confirma causa técnica. Os testes anteriores aprovados pelo usuário continuam válidos para seus cenários e não anulam relatos posteriores mais específicos.

### 1.1. Direção aprovada em 31/10/2026 — Publicações (substitui o Editor de Páginas)

**Decisão do proprietário:** retirar do produto a interface, o fluxo e o conceito do **Editor de Páginas** legado. Como o Urbe está em beta privado, não há obrigação de manter o editor antigo por compatibilidade de interface. Isso **não autoriza apagar notas, arquivos ou mídia do vault** nem eliminar recursos compartilhados necessários a outros módulos. Fazer inventário do código, remover o que for exclusivo do editor e reaproveitar somente o que servir à nova visão, sem carregar as suas limitações para o novo sistema.

**Visão:** o Urbe reúne e organiza conhecimento, e **Publicações** transforma esse conhecimento em produtos destinados ao público: páginas e sites, livros impressos/digitais, documentos e PDFs, apresentações, imagens, infográficos, vídeos e animações, cursos e experiências interativas; outros formatos entram por adaptadores comprovados. **Não é um simples conversor de arquivos nem um conjunto de exportadores pendurados no editor.** É um sistema de autoria, composição, revisão, renderização e entrega, com IA profundamente integrada e uso manual completo.

**Regra inegociável — notas são a API e a fonte de verdade do produto.** Uma Publicação **referencia diretamente os arquivos reais do vault** (notas, trechos, blocos, tabelas, imagens, áudios, mapas, grafos e outros ativos), por identidade estável, localização e versão. **Não se criam cópias sincronizadas do conteúdo.** Editar conteúdo de uma nota a partir do estúdio é editar a própria nota, usando o mesmo serviço de escrita, histórico, autoria, conflitos e recuperação do editor comum; editar a nota fora do estúdio atualiza a publicação e vice-versa. Referências são vivas; caches de renderização e arquivos exportados são derivados, nunca fontes concorrentes.

**Distinção necessária para que essa sincronização não destrua o conhecimento:** texto e dados com vínculo de origem são editados na nota de origem; configuração visual, ordem de apresentação, cortes de duração, estilos, transições e enquadramentos pertencem à Publicação, pois não são conteúdo da nota. Quando um slide ou roteiro exigir **uma redação diferente** da fonte, o Urbe deve criar ou vincular uma **nota editorial real no vault**, com ligação/proveniência explícita para a fonte original, e editar essa nota — nunca esconder um texto independente dentro de um JSON da publicação. Mostrar sempre o destino da edição e permitir abrir a origem. O produto pode apresentar uma transformação sem sobrescrever a pesquisa original por engano. Não há "sincronizar cópias": há referências para uma ou mais notas reais.

**O HTML é o principal substrato de apresentação**, sobretudo para prévia, sites, livros web, slides interativos e experiências; CSS e SVG complementam. **HTML não é intermediário obrigatório universal**: o núcleo de publicação precisa manter representação semântica e contratos de composição suficientemente ricos para exportar corretamente PPTX editável, EPUB, PDF paginado, documentos, vídeos com linha temporal e cursos com avaliação. Não converter toda a informação prematuramente em pixels ou HTML final e depois tentar recuperá-la por engenharia reversa.

**IA é coautora operacional, não decoração:** agentes podem pesquisar fontes autorizadas, selecionar notas, propor público/objetivo/narrativa, criar estrutura, roteiros, storyboards, diagramar, programar animações, renderizar, assistir/verificar prévias, corrigir problemas e entregar artefatos. Devem operar **as mesmas ferramentas e contratos do usuário**; respeitar permissões, aprovação, autoria, privacidade e rastreabilidade. Skills dão instruções especializadas; ferramentas produzem resultados reais; plugins oferecem extensões e integrações. O fluxo também deve funcionar sem IA.

**Referência audiovisual principal:** o Claude Motion oficial da Anthropic (beta, outubro de 2026) anima texto, formas, gráficos e imagens **como código editável**, admite revisões por linguagem natural e exporta MP4; não é um modelo de filmagem fotorrealista. O Urbe deve estudar **o princípio e o fluxo**, sem copiar tecnologia proprietária, com cenas/timeline reproduzíveis, preview, revisão quadro a quadro e renderização real. Vídeos generativos externos são opção complementar e precisam ser identificados como tal.

**Princípios de UX:** criar publicação a partir de nota, trecho, seleção ou pasta; prévia em destaque; edição direta e contextual; ferramentas expandidas apenas quando necessárias; um único fluxo de criação/revisão/exportação; liberdade de composição, sem limitar o usuário a templates; Android touch-first, com a mesma visão no Windows. A camada editorial não substitui o editor de notas.

A especificação normativa, as pesquisas e os contratos de implementação constam nas **seções 9 e 10**; o checklist novo está em **4.12** e as lacunas reconciliadas em **4.13**. **Esta consolidação passa a ser a fonte única para os requisitos:** a seção 6.2 associa cada uma das 131 entradas do FeaturesCheck a tarefas CST, e a seção 13 conserva sua transcrição integral, imutável e histórica, para que nenhuma observação se perca. O anexo histórico não restaura o antigo Editor de Páginas nem sobrescreve decisões posteriores. A situação [x] de FeaturesCheck significa implementação relatada, nunca aceite neste checklist. Os itens legados CST-059/060/061/062/063/064/135 foram **redirecionados** (não apagados ou renumerados); suas intenções válidas permanecem. O estado de cada item continua **[ ] até validado**.


### 1.2. Direção aprovada para investigação — IA como inteligência nativa transversal do produto (31/10/2026)

**Correção de estratégia:** a IA **não é um produto paralelo nem o Urbe se transforma numa IDE**. A pergunta normativa é: como tornar editor, vault, propriedades, grafos, formulários, capturas, plugins e Publicações **profundamente operáveis por inteligência**, com menos passos para o usuário? A ambição dos agentes continua elevada: investigar, programar, calcular, executar, observar, testar, corrigir e entregar com confiabilidade comparável aos sistemas de ponta (Claude Code, Codex, OpenCode, Qwen Code, Goose, OpenHands). O código é uma **capacidade geral** do produto, não o centro obrigatório da experiência. Agentes, usuário e plugins utilizam operações nativas da mesma fonte de verdade. **Ausências observadas por leitura estática:** não foi encontrado executor geral de código seguro integrado ao catálogo do Urbe, ambiente geral de build/teste nem produção de MP4 integrada à IA. Não confundir ausência no catálogo examinado com inexistência absoluta de qualquer rotina auxiliar.

**Conhecimento → ação → produto, sem cópias concorrentes:** cada investigação devolve fontes, trechos e identidade estáveis; alterações de nota/bloco/propriedade/grafo usam a operação nativa correspondente; resultados editoriais são vinculados às notas reais; a geração de formatos utiliza contratos de Publicações. **Programação e matemática como capacidades transversais:** ferramentas especializadas continuam preferíveis quando mais seguras ou previsíveis, e podem ser complementadas por soluções computacionais reproduzíveis (cálculo, algoritmos, SVG, HTML, dados, simulação, cena, timeline e automação). Não confundir o poder de escrever código com autorização irrestrita para **executar** código recebido de IA ou internet. Execução exige isolamento tecnicamente demonstrado, permissões e escopo de filesystem/rede, limites reais e proteção dos arquivos canônicos. C# e integração Android/Windows são prioridades; soluções auxiliares para formatos web ou codecs são adaptadores de saída, não migração da UI nativa para JavaScript nem duplicação do Lunet.

**Produtos completos e experiência invisível da complexidade:** imagens, vídeo por código e vídeo generativo opcional, áudio, livros, sites, cursos, aplicações, simulações, gráficos, planilhas, ferramentas interativas e códigos testáveis, **sem exigir terminal, programação ou chat dedicado** para cada operação. Cada resultado utiliza a mesma origem, permissões, prévias, recuperação e provas, acionável onde a pessoa já trabalha. A produção deve usar os mesmos contratos para usuário, agente e plugin; declarar sucesso apenas após validação pertinente. **Autoridade de escopo:** 4.8–4.10 (IA/Conhecimento/estabilidade), 4.12 (Publicações), 4.14 (capacidades computacionais), 4.15 (novas lacunas), 9 (contratos editoriais), 14 (auditoria aplicada). A seção 15 dá a rastreabilidade desta revisão. Não converter o Urbe em terminal, replicar engines do Lunet ou introduzir UI WebView por conveniência.

### 1.3. Decisão aprovada em 01/11/2026 — progresso como componente nativo das notas

**Decisão do proprietário:** o checkbox informa **o que está pendente**, mas não **quanto falta**. O Urbe deve oferecer **barras de progresso nativas, funcionais e persistentes** diretamente nas notas; isso não deve depender de plugin, arquivo HTML, WebView ou código criado pelo usuário. O primeiro resultado é um indicador de progresso com valores e meta editáveis; o segundo, mais importante para checklists, calcula automaticamente a conclusão de tarefas por um escopo declarado. Ambos reutilizam os mesmos serviços de edição, salvamento e referência de dados do vault.

**Comportamento esperado:** ao criar uma barra vinculada a uma lista ou seção, o Urbe mostra tarefas concluídas, total e restantes, por exemplo **3/8 — 37,5% concluído; faltam 5**. Marcar ou desmarcar um checkbox, inserir/remover uma tarefa ou alterar o escopo recalcula o indicador sem reconfiguração manual. Também é possível acompanhar uma meta numérica, como **54/224 páginas — 24,1%**, editando diretamente o valor atual e a meta. Uma fonte vazia não pode virar artificialmente 100%: mostrar "sem tarefas" ou equivalente até haver itens.

**Integração de produto:** criação contextual pelo editor, exibição nativa Avalonia/C#, uso sem IA, leitura/edição acessíveis por toque e teclado, suporte a templates e vínculos de notas; agentes e plugins que modificam essas informações devem obedecer ao contrato canônico de edição, versão, permissão e undo/redo. O indicador é uma visão derivada dos dados reais, nunca um banco paralelo. O Markdown permanece legível fora do Urbe, com representação textual/fallback documentada. Publicações usam os mesmos valores e referências, com representação estática ou interativa adequada ao formato. **Escopo e critérios normativos: CST-227 e CST-228 (seção 4.16).**

### 1.4. Decisão de execução — abas explícitas (01/11/2026)

**Ordem do proprietário:** navegação comum não deve acumular abas. Abrir outra nota substitui o conteúdo da aba ativa, com salvamento/recuperação no caminho canônico. Abas extras exigem **+ → escolher nota** no topo; cancelar não cria aba, selecionar nota já aberta foca essa aba. Voltar/Avançar preservam navegação sem poluir a barra. Sessões antigas de histórico automático passam a uma nota ativa; sessões novas conservam as abas deliberadamente escolhidas. **Requisito verificável: CST-229.** A publicação da próxima beta foi solicitada expressamente pelo proprietário nesta sessão; isso autoriza o corte pelo workflow existente, sem afirmar validação física ou conclusão integral do plano.

## 2. Regras de execução e conclusão

- Cada ID CST é estável. CST-001 a CST-110 correspondem aos 110 itens anteriores; CST-111 em diante cobrem complementos. A ordem de apresentação pode mudar sem renumerar IDs.
- Todas as caixas ficam abertas até a entrega descrita ser verificada. Isso não significa que a funcionalidade inexiste. No FeaturesCheck, [x] continua significando implementação do escopo descrito; nesta consolidação significa conclusão verificada da tarefa.
- Não chamar relato de bug confirmado antes de reproduzir ou obter evidência suficiente. Se não reproduzir, registrar ambiente, tentativas e pendência; não marcar corrigido por ausência de reprodução.
- Não recriar o que já existe por reflexo. Conferir a implementação atual e reutilizar o que for válido; **a remoção do Editor de Páginas legado é uma decisão expressa e substitui a obrigação de manter aquele produto/fluxo**, mas arquivos reais do vault, integrações comuns e dados de usuário continuam protegidos. Sua retirada deve ser testada.
- Antes de alterar código, conferir main, trabalho em andamento e instruções aplicáveis do Ecosystem/Urbe, incluindo decisões e roadmap. Este plano não substitui contratos de desenvolvimento nem autoriza mudanças normativas silenciosas.
- Mudança persistida deve ter contrato, recuperação e validação proporcionais ao risco. Proteger o vault atual, sem inventar migrações históricas não solicitadas.
- Para fechar um item, registrar versão/commit, comportamento observado, testes pertinentes e limitações. Não substituir aceite de toque, IME, desempenho ou ciclo de vida Android por teste headless.
- Erros devem poder ser diagnosticados sem registrar conteúdo privado por padrão. Não adicionar limites artificiais de passos, perguntas, recuperação ou uso para encobrir defeitos. Respeitar limites técnicos reais e explicar incompatibilidades.
- IDs em “Dependências” indicam capacidades que precisam estar disponíveis para validar a entrega; se já funcionam, verificar e aproveitar. Não obrigam reimplementar nem congelam trabalhos independentes. **Separar dependência real de simples referência cruzada:** produtores e renderizadores não podem depender circularmente do aceite final que os consome.
- **IA transversal, não ferramenta paralela:** toda ação de IA sobre notas, grafos, propriedades, publicações, plugins ou dados deve reutilizar o contrato canônico da ação manual, inclusive autorização, versão, conflito, undo/redo e observação. Uma nova ferramenta não cria segunda fonte de verdade nem segundo editor.
- **Estado observado ≠ status fechado:** a presença de método, tool ou testes na main atualiza a descrição inicial, mas [x] exige prova de comportamento, regressão e plataforma pertinentes. Hipóteses originadas de issues/Reddit são casos a testar, nunca prevalência ou defeito atual comprovado no Urbe.
- **Experiência nativa:** a pessoa deve solicitar o resultado no contexto de seu trabalho e acompanhar escopo, progresso, fontes, prévia, decisões e falhas sem aprender comandos. Código e infraestrutura ficam atrás de interfaces progressivas; tudo continua utilizável sem agente.
- O responsável pela execução registra diagnóstico e evidência no próprio item ou em referência rastreável. Não inventar autoria, validação humana, duração de raciocínio, métricas ou disponibilidade de marca/modelo.
- **Bloqueio de prontidão definido pelo usuário:** o editor é crítico; enquanto não estiver plenamente confiável, sem falhas que interrompam a escrita, causem perda de texto ou prejudiquem a leitura/edição, **não há produto pronto**. Os relatos de matemática sobreposta/cortada, menu radial cobrindo o controle leitura/edição e interrupções com perda de texto são bloqueadores de lançamento, não melhorias cosméticas. Não declarar o produto pronto enquanto estes itens e a confiabilidade geral do editor permanecerem sem resolução e validação. {ia:"gpt-6-luna-pro"}

## 3. Sequência de execução — dependências de produto, sem bloquear frentes independentes

1. **Gate inegociável: editor, dados e ANR.** Resolver perdas de texto, falhas de toque/IME, equações sobrepostas, menu radial cobrindo botões, interrupções, crashes e avisos Android; medir baseline real (CST-003–017, CST-111–114, CST-129, CST-142–144, CST-148). Novas features não justificam deixar o editor não confiável.
2. **Conhecimento fundamentado e edição semântica.** Referência única, indexação/recuperação híbrida apenas se provar qualidade, trechos citáveis, permissões, operações estruturadas em notas, propriedades, formulários e grafos; indicadores de progresso nativos e derivados de checklists (CST-227–228) utilizam esses mesmos dados, sem armazenamento paralelo; base para a IA compreender o vault sem inventar relações (CST-068, CST-075, CST-083–089, CST-120, CST-134, CST-223–224).
3. **Capabilities e continuidade.** Unificar contratos consumidos pela UI, plugins e agentes, preservar contextos e permitir retomar tarefas reais com checkpoints, transações e provas (CST-072–079, CST-090–093, CST-098, CST-146, CST-199–200, CST-205, CST-216, CST-219, CST-225).
4. **Publicações como prova do produto integrado.** Substituir Páginas; fonte viva, nota editorial real, composição, render, prévia e exportação por formatos nativos, sempre com operação manual e coautoria por IA (CST-149–192, CST-209, CST-226).
5. **Computação geral como serviço do produto.** Matemática, código, dados, áudio, vídeo, análise de arquivos, automação e aplicações com execuções isoladas por plataforma, sem apresentar terminal como requisito (CST-141, CST-201–204, CST-206–212, CST-221).
6. **Especialização e qualidade demonstrada.** Skills compatíveis, MCP opcional, coordenação proporcional de especialistas, verificadores reais e matriz de benchmark end-to-end com mesmo modelo e hardware (CST-177–180, CST-185, CST-187–188, CST-192, CST-198, CST-213–215, CST-217–218, CST-222).
7. **Fechamento de cada incremento.** Testes antes da implementação quando apropriado, evidência por requisito, validação no aparelho, documentação e release rastreável. Não esperar todo o sistema ficar completo para corrigir um defeito ou validar um recurso independente.

**A ordem não é uma cascata artificial:** pesquisas, protótipos isolados e trabalhos que não disputam o mesmo código podem ocorrer em paralelo. Gates são o risco de perda de dados, a segurança de execução, a estabilidade e a inexistência de um resultado realmente verificável.

## 4. Checklist executável

### 4.1. Base, dados e medição inicial

- [ ] **CST-001 — Consolidar os requisitos em uma referência única, separando funcionalidade existente, implementação parcial, defeito confirmado e relato ainda não reproduzido.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Cada requisito dos anexos tem destino explícito, sem omissão de decisões, relatos ou recursos existentes.

- [ ] **CST-002 — Dividir requisitos que misturam partes prontas e ausentes. Cada checkbox deve representar um resultado verificável.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Cada item descreve escopo verificável; partes prontas e pendentes não compartilham um checkbox ambíguo.

- [ ] **CST-131 — Separar a semântica dos dois checklists e corrigir checkboxes ambíguos.**

  **Estado inicial:** Inconsistência documental identificada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-001, CST-002  
  **Critério de conclusão:** Esta consolidação usa [x] apenas para entrega verificada; no histórico preservado do FeaturesCheck, [x] significa escopo relatado como implementado. Distinguir `ask_user` existente de indicador pendente, registrar planejamento, teste físico e validação comportamental; nenhuma marca histórica migra automaticamente para um checkbox CST.

- [ ] **CST-003 — Registrar uma reprodução mínima para cada bug: versão, ação, resultado esperado e resultado observado.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Relatos têm passos, versão, aparelho, esperado/observado e evidência; hipóteses não são apresentadas como causas.

- [ ] **CST-097 — Preservar privacidade: evitar conteúdo de notas e prompts nos registros por padrão, definir retenção e controle do usuário.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-001  
  **Critério de conclusão:** Coleta minimizada tem consentimento, desativação, retenção e exclusão; conteúdo privado, chaves, trechos de notas, prompts e caminhos sensíveis não entram nos relatórios por padrão. Conferir especificamente o registro da beta.37 de retenção de apenas 30 dias, sem presumir que seja uma política de todos os dados do produto.

- [ ] **CST-114 — Medir a latência básica antes de modificar os fluxos.**

  **Estado inicial:** Lentidão generalizada relatada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003, CST-097  
  **Critério de conclusão:** Medir abrir, salvar, renderizar, navegar, enviar formulário e expandir ferramentas, incluindo início frio/quente e vault de teste identificado; registrar baseline no moto g32. Comparar correções com o mesmo cenário, sem inventar metas absolutas. Separar latência de renderização, busca, indexação, retorno de ferramenta e atualização da UI; verificar atividade em background sem bloquear dispatcher ou IME.

- [ ] **CST-137 — Definir critérios de desempenho a partir da medição e do uso.**

  **Estado inicial:** Critérios quantitativos ainda não fixados.  
  **Frente:** Estabilização.  
  **Dependências:** CST-114  
  **Critério de conclusão:** Baseline e feedback do aparelho orientam metas por operação; documentar amostra e percentis. Não aprovar apenas pelo teste headless nem reduzir qualidade para maquiar latência.

- [ ] **CST-004 — Conferir os caminhos de criar, salvar, renomear, mover, excluir, restaurar e reabrir arquivos, incluindo interrupções e erros do armazenamento.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Operações persistem os bytes esperados e reabrem corretamente; falha intermediária não produz falso sucesso nem perda silenciosa.

- [ ] **CST-005 — Verificar conflitos com alterações externas: o Urbe não pode sobrescrever silenciosamente um arquivo modificado por outro aplicativo.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Edição externa durante uma sessão gera comparação/conflito recuperável e preserva as duas versões pertinentes.

- [ ] **CST-006 — Conferir recuperação por lixeira, histórico e backup, inclusive após fechamento inesperado.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Nota excluída ou alterada pode ser recuperada e reaberta; backup é restaurável, inclusive após interrupção.

- [ ] **CST-007 — Garantir que falhas sejam apresentadas com uma ação útil de recuperação, sem anunciar sucesso antes da gravação confirmada.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Mensagem de erro identifica a operação e oferece tentar novamente ou recuperar, sem ocultar o estado real.

### 4.2. Estabilidade e continuidade

- [ ] **CST-111 — Investigar o crash no fluxo de pesquisa e geração de carrossel de notícias.**

  **Estado inicial:** Relato a investigar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003, CST-114  
  **Critério de conclusão:** Reproduzir pesquisa/verificação, capa e até nove slides separados, correções por nova geração e resposta final só com imagens; correlacionar logs, chamadas, tamanhos e memória. Identificar falha do app, ferramenta ou provedor com evidência; resultado parcial permanece recuperável.

- [ ] **CST-112 — Investigar travamento ao expandir muitos passos e ferramentas do chat.**

  **Estado inicial:** Relato a investigar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003, CST-114  
  **Critério de conclusão:** Expandir/recolher planos, argumentos e resultados extensos repetidamente não congela a UI; medir montagem, memória e resposta ao toque antes/depois. Paginação/virtualização preserva acesso à informação.

- [ ] **CST-136 — Conferir apresentação de argumentos, planos e resultados malformados/extensos.**

  **Estado inicial:** Correção registrada; regressão necessária.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003, CST-112  
  **Critério de conclusão:** Entrada malformada não derruba a UI; erro identificável e paginação preservam informação. Esse teste não substitui o caso do carrossel.

- [ ] **CST-148 — Investigar avisos repetidos “Urbe não está respondendo” durante uso da IA e do vault.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Relato do usuário na beta.38. Enquanto usa a IA e trabalha no vault, o Android exibe repetidamente “Urbe não está respondendo”; ao tocar em “Aguardar”, o usuário consegue continuar normalmente, mas o aviso volta até fechar o app. O usuário suspeita de relação com a IA, hipótese ainda não confirmada; não relata que o app esteja de fato travado. {ia:"gpt-6-luna-pro"}
  **Frente:** Estabilização. {ia:"gpt-6-luna-pro"}
  **Dependências:** CST-003, CST-011, CST-114 {ia:"gpt-6-luna-pro"}
  **Critério de conclusão:** Reproduzir ou documentar tentativas na beta.38, registrando aparelho, duração e passos disponíveis; correlacionar o aviso com atividade da IA e operações no vault e examinar diagnósticos para distinguir bloqueio real da thread principal, lentidão momentânea e aviso falso positivo do sistema. Verificar o comportamento também sem IA. Não atribuir causa nem marcar corrigido sem evidência suficiente. {ia:"gpt-6-luna-pro"}

- [ ] **CST-008 — Reproduzir e corrigir a conversa de IA que processa uma ação, mas deixa a tela desatualizada.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003, CST-114  
  **Critério de conclusão:** Mensagem, resposta, formulário e aprovação atualizam o chat visível sem precisar fechar e reabrir a aba.

- [ ] **CST-009 — Verificar segundo envio, fila de mensagens e preservação de anexos durante execuções, erros e cancelamentos.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Envios consecutivos mantêm ordem, conteúdo e anexos; erro/cancelamento deixam retomada explícita sem duplicar escrita.

- [ ] **CST-010 — Corrigir o botão Voltar do Android: fechar primeiro a interação atual, percorrer a navegação quando aplicável e sair somente no contexto adequado.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Voltar respeita sobreposições, editor, links e histórico de abas; comportamento sem histórico é previsível.

- [ ] **CST-011 — Investigar travamentos ao abrir o aplicativo, alternar superfícies, editar notas e acompanhar conversas longas.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Cada travamento encontrado tem reprodução e correção verificada ou bloqueio documentado, incluindo os casos CST-111 e CST-112.

- [ ] **CST-012 — Conferir cancelamento real: interromper uma execução deve atualizar a interface e encerrar o trabalho correspondente.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Cancelar encerra a execução correspondente e limpa esperas; operações já persistidas são informadas sem prometer desfazê-las automaticamente.

- [ ] **CST-013 — Verificar continuidade ao minimizar e retornar ao aplicativo, distinguindo execução em segundo plano de recuperação após morte do processo.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Minimizar/retornar preserva estado; morte do processo oferece estado recuperável sem repetir automaticamente efeitos já aplicados.

- [ ] **CST-014 — Conferir isolamento entre chats: mensagens, estados, formulários, aprovações e resultados devem aparecer na conversa correta.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-009  
  **Critério de conclusão:** Duas conversas simultâneas não trocam mensagens, anexos, formulários, aprovações, cancelamentos ou resultados.

### 4.3. Editor e escrita móvel

- [ ] **CST-015 — Corrigir posicionamento do cursor, toque duplo, seleção de palavras e alças para ampliar a seleção.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Toque posiciona o cursor, toque duplo seleciona a palavra e alças ampliam a seleção no Android físico.

- [ ] **CST-016 — Garantir abertura confiável do teclado e preservar o ponto de edição quando ele aparece ou desaparece.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada. O usuário relata que às vezes, sem padrão identificado, toca numa linha para continuar digitando e o teclado não abre; isso ocorre frequentemente após usar o botão do teclado para inserir a próxima linha. Relata também que o botão no lugar habitual do Enter insere a quebra de linha esperada, mas encerra a digitação e fecha o teclado. O comportamento do botão também foi relatado na entrada da IA; a abertura intermitente após tocar numa linha foi descrita no contexto de edição de linhas. Não há causa técnica confirmada. {ia:"gpt-6-luna-pro"}  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Teclado abre ao tocar para editar, inclusive após inserir uma nova linha, e preserva o ponto de edição sem roubar foco ao acompanhar respostas ou fechar painéis. No editor e na entrada da IA, a ação habitual de Enter insere nova linha e mantém a digitação quando esse é o comportamento coerente; envio só ocorre quando apropriado ao campo e à interação. Reproduzir e validar os casos no aparelho, sem fechar o item apenas por não reproduzi-los. {ia:"gpt-6-luna-pro"}

- [ ] **CST-017 — Eliminar saltos de rolagem ao entrar e sair de blocos, selecionar texto e aplicar formatação.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Entrada/saída de edição preserva o bloco visível; rolagem distingue gesto, seleção e toque sem saltos indevidos.

- [ ] **CST-018 — Conferir a barra de formatação: estado correto dos botões, ativação/desativação e comportamento consistente no Visual e na Fonte.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Cada botão representa o Markdown sob o cursor e liga/desliga corretamente nos modos Visual e Fonte.

- [ ] **CST-019 — Consolidar a barra móvel junto ao teclado, com comandos acessíveis, áreas de toque confortáveis e personalização.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Barra permanece acessível com teclado aberto, personalização persiste e comandos não deslocam a seleção indevidamente.

- [ ] **CST-020 — Conferir código inline e blocos de código: inserir, editar, remover formatação e preservar o texto.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Código inline/bloco vazio podem ser criados e alternados sem texto artificial, quebra de linha indevida ou perda de seleção.

- [ ] **CST-021 — Corrigir a criação de nota iniciada pelo editor e conferir criação por link inexistente.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Nota criada pelo editor ou link inexistente é salva, aberta e reencontrada após reinício, com cancelamento sem arquivo residual.

- [ ] **CST-022 — Consolidar comentários em palavras e trechos, incluindo consulta, acréscimo e preservação após outras edições.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Comentário é associado ao trecho correto, consultável e editável pelo fluxo previsto sem corromper texto após outras edições.

- [ ] **CST-023 — Conferir o painel fixado: seleção correta, rolagem independente, troca de nota e uso com teclado aberto.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Estabilização.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Trecho ou nota fixada permanece legível ao navegar, rola independentemente e pode ser desafixado no celular.

- [ ] **CST-024 — Consolidar tags estilizadas e definir quais aspectos podem ser personalizados além da cor.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Configuração de tags persiste e afeta apenas o conteúdo correspondente; suporte registrado a ícones além da cor deve reaparecer corretamente, inclusive em temas diferentes; ampliação de estilo tem contrato documentado.

- [ ] **CST-025 — Mostrar em cada bloco o horário da última edição e definir as regras de persistência, divisão, junção, colagem e desfazer.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Expansão.  
  **Dependências:** CST-004, CST-015  
  **Critério de conclusão:** Cada bloco mostra a última edição; fusão/divisão, colagem, IA e desfazer têm regras temporais e persistência definidas.

- [ ] **CST-026 — Consolidar contêineres aninháveis, com edição previsível e preservação do conteúdo nos formatos de saída.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-018  
  **Critério de conclusão:** Contêineres aninhados preservam conteúdo no Visual/Fonte e na exportação; comentários seguem suas próprias regras de omissão.

- [ ] **CST-128 — Investigar negrito involuntário e legibilidade do texto.**

  **Estado inicial:** Relato a investigar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-018, CST-114  
  **Critério de conclusão:** Distinguir estilo visual de Markdown realmente alterado; conferir fonte, peso, tamanho e entrelinha em ambos os modos, mantendo o conteúdo original.

- [ ] **CST-129 — Investigar demora para salvar e sair do editor.**

  **Estado inicial:** Relato a investigar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-004, CST-007, CST-114  
  **Critério de conclusão:** Saída preserva rascunho/gravação confirmada e permanece responsiva; falha informa recuperação. Medir tempo de UI separado da persistência, sem ocultar perda de dados.

- [ ] **CST-142 — Corrigir fórmulas matemáticas sobrepostas ou cortadas no editor.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Relato frequente do usuário; reprodução e causa técnica ainda não registradas. Equações podem se sobrepor ao texto ou aparecer cortadas. {ia:"gpt-6-luna-pro"}  
  **Frente:** Estabilização — bloqueador de prontidão do editor. {ia:"gpt-6-luna-pro"}  
  **Dependências:** CST-015, CST-016, CST-017, CST-114 {ia:"gpt-6-luna-pro"}  
  **Critério de conclusão:** Fórmulas e equações aparecem completas, legíveis e sem sobrepor texto em leitura e edição, em diferentes tamanhos de tela e com teclado aberto/fechado; casos reproduzíveis têm regressão e validação no aparelho pertinente. {ia:"gpt-6-luna-pro"}

- [ ] **CST-143 — Eliminar a sobreposição do menu radial sobre o botão de leitura/edição.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Relato do usuário; geometria e condições exatas ainda precisam ser reproduzidas. {ia:"gpt-6-luna-pro"}  
  **Frente:** Estabilização — bloqueador de prontidão do editor. {ia:"gpt-6-luna-pro"}  
  **Dependências:** CST-043, CST-105 {ia:"gpt-6-luna-pro"}  
  **Critério de conclusão:** Menu radial e controle que alterna leitura/edição não se cobrem; ambos permanecem visíveis, alcançáveis e operáveis por toque nas telas e estados pertinentes, incluindo layouts estreitos. {ia:"gpt-6-luna-pro"}

- [ ] **CST-144 — Impedir interrupções da escrita e perda de texto no editor, com recuperação verificável de rascunhos.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Relato do usuário de ocorrências incontáveis em que uma tentativa de clicar foi seguida de interrupção e perda do texto, exigindo reescrita. Clique falso, atualização do editor ou outra causa são hipóteses relatadas, não diagnóstico confirmado. {ia:"gpt-6-luna-pro"}  
  **Frente:** Estabilização — bloqueador crítico de prontidão do produto. {ia:"gpt-6-luna-pro"}  
  **Dependências:** CST-004, CST-005, CST-006, CST-007, CST-015, CST-016, CST-017, CST-129 {ia:"gpt-6-luna-pro"}  
  **Critério de conclusão:** Reproduzir e investigar interrupções por toque e atualização; rascunho digitado permanece preservado ou pode ser recuperado após troca de modo, foco, atualização, navegação e fechamento inesperado. Nenhuma interação do editor descarta silenciosamente texto não salvo; validar no aparelho e adicionar regressões. Este item, junto com CST-142, CST-143 e a confiabilidade geral do editor, é bloqueador de prontidão: conforme critério explícito do usuário, enquanto o editor não estiver plenamente confiável, não há produto pronto. {ia:"gpt-6-luna-pro"}

- [ ] **CST-147 — Mostrar numeração de linhas na lateral do editor para facilitar sua identificação e referência.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Proposta do usuário; suporte e impacto visual ainda não avaliados. {ia:"gpt-6-luna-pro"}  
  **Frente:** Expansão. {ia:"gpt-6-luna-pro"}  
  **Dependências:** CST-015, CST-016, CST-017 {ia:"gpt-6-luna-pro"}  
  **Critério de conclusão:** Linhas visíveis no editor exibem números laterais que permitem localizar e referenciar uma linha específica; a numeração acompanha corretamente inserções, remoções, quebras e junções de linhas, rolagem e mudanças entre os modos de edição relevantes, sem sobrepor nem ocultar o texto e sem interferir na seleção, cursor ou uso móvel. Verificar comportamento em notas longas e com linhas visualmente quebradas por largura da tela. {ia:"gpt-6-luna-pro"}

### 4.4. Autoria da IA e aprovações

- [ ] **CST-027 — Definir uma regra única para distinguir texto criado pela IA, texto do usuário e transformações de conteúdo existente.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Contrato documenta geração, regeneração e transformação, respeitando a autoria e a propriedade intelectual do usuário.

- [ ] **CST-028 — Aplicar autoria em cada bloco criado ou efetivamente regenerado, preservando a autoria dos demais.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-027  
  **Critério de conclusão:** Geração com vários blocos marca cada bloco pertinente; blocos humanos não alterados mantêm sua autoria.

- [ ] **CST-029 — Tratar correção ortográfica e outras alterações sem geração de conteúdo conforme essa regra, sem atribuir automaticamente o texto inteiro à IA.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-027  
  **Critério de conclusão:** Correção ortográfica e transformação sem criação de conteúdo não transferem autoria humana à IA.

- [ ] **CST-030 — Impedir que ferramentas dos agentes removam ou adulterem os selos de autoria.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-027, CST-028  
  **Critério de conclusão:** Tentativas de remover/alterar selo por cada ferramenta de escrita são neutralizadas sem loop nem alteração indevida do texto.

- [ ] **CST-031 — Definir e implementar a proteção dos selos nos modos Visual e Fonte.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-030  
  **Critério de conclusão:** Fonte e Visual preservam a integridade do selo conforme a regra aprovada; texto vizinho continua editável.

- [ ] **CST-032 — Substituir a autoria anterior quando um bloco for regenerado, sem acumular marcas.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-028  
  **Critério de conclusão:** Regeneração substitui o selo do bloco correspondente pelo modelo atual, sem duplicação nem selo antigo incorreto.

- [ ] **CST-033 — Informar explicitamente aos agentes que o aplicativo administra os selos e que eles não devem tentar “limpá-los”.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-027  
  **Critério de conclusão:** Instrução e contrato das ferramentas explicam gestão pelo aplicativo; tentativa de limpeza não gera ciclo repetitivo. Instruções gerais, do agente e do pedido não autorizam adulterar selos de autoria.

- [ ] **CST-034 — Conferir a edição humana da proposta antes da aprovação, incluindo rascunho, recusa, cancelamento e mudanças simultâneas na nota.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-005, CST-027  
  **Critério de conclusão:** Aprovar grava a versão editada pela pessoa; recusar/cancelar não grava; conflito externo exige resolução explícita. O cartão de aprovação mostra TODAS as linhas alteradas, sem truncar o diff ou esconder exclusões; comparar bytes e versões antes de aplicar, inclusive em mudanças de várias notas e Publicações.

- [ ] **CST-035 — Garantir apresentação discreta e coerente da autoria no editor, nas Publicações e nos produtos exportados.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-028, CST-032  
  **Critério de conclusão:** Editor, prévias de Publicação e saídas HTML, PDF, slides, vídeo, imagem e curso aplicáveis mostram autoria/proveniência de forma correta e discreta, respeitando o contrato de cada formato e sem adulterar selos das notas.

### 4.5. Vault, propriedades e navegação

- [ ] **CST-036 — Consolidar o Explorador com todos os tipos de arquivo, extensão, ícone e ações coerentes.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Todos os tipos pertinentes aparecem com extensão/ícone e recebem ações compatíveis com o arquivo.

- [ ] **CST-037 — Conferir seleção, multisseleção, menus de pasta e arrastar e soltar no computador e no celular.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Seleção, multisseleção, menus e arraste funcionam no mouse e toque sem abrir o item acidentalmente.

- [ ] **CST-038 — Permitir movimentar notas, anexos e pastas pelos caminhos apropriados, mantendo referências e metadados associados.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-005  
  **Critério de conclusão:** Movimentar e reabrir preserva arquivos, identidade e metadados; referências são atualizadas ou a pendência é informada.

- [ ] **CST-039 — Acrescentar pesquisa no seletor de destino e impedir destinos inválidos.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-038  
  **Critério de conclusão:** Destino pode ser filtrado por busca; pasta atual, descendente inválido e colisão não provocam perda de arquivos.

- [ ] **CST-040 — Fazer os seletores de `[[...]]` e `![[...]]` localizarem os tipos de arquivo pertinentes.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-036  
  **Critério de conclusão:** Autocomplete encontra notas e outros arquivos do vault, incluindo caminhos e nomes ambíguos.

- [ ] **CST-041 — Distinguir claramente “posso criar um link” de “o aplicativo consegue incorporar esse conteúdo”.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-040  
  **Critério de conclusão:** Link abre o arquivo correto; incorporação incompatível exibe indicação útil, sem fingir prévia suportada.

- [ ] **CST-042 — Definir tags e propriedades herdadas por projeto, incluindo precedência, exceções e comportamento ao mover arquivos.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Expansão.  
  **Dependências:** CST-119, CST-120  
  **Critério de conclusão:** Contrato define herança, sobrescrita, remoção e movimento entre projetos; consulta e interface exibem a origem efetiva.

- [ ] **CST-043 — Substituir as abas móveis por menu radial no canto inferior direito, validando antes o protótipo e preservando acesso às funções.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Expansão.  
  **Dependências:** CST-010, CST-105  
  **Critério de conclusão:** Menu radial substitui as abas no celular, no canto inferior direito, com acesso completo, navegação de volta e acessibilidade validados.

- [ ] **CST-119 — Conferir propriedades das notas e alternância Visual/Fonte.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-018  
  **Critério de conclusão:** Formulário de propriedades preserva valores/tipos desconhecidos; alternar modo não altera o documento sem edição nem perde rascunho.

- [ ] **CST-120 — Conferir aliases, links e backlinks.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-038, CST-040, CST-119  
  **Critério de conclusão:** Links e aliases preservam identidade e relações ao renomear/mover/restaurar; consultas sem suporte mostram problema recuperável. Conferir também links Markdown convencionais `[texto](Nota.md)` na tarefa dedicada CST-196. A indexação para IA respeita IDs persistentes, fragmentos, versões e origem de relações explícitas; vínculo de grafo autoral não vira automaticamente afirmação verdadeira (CST-223, CST-226).

### 4.6. Templates, formulários e captura

- [ ] **CST-044 — Definir como identificar templates explicitamente, incluindo a extensão própria desejada e o tratamento dos modelos Markdown existentes.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Identificação explícita/extensão de template tem contrato; modelos Markdown existentes seguem utilizáveis sem conversão destrutiva.

- [ ] **CST-045 — Unificar os campos dos templates com o sistema de formulários, evitando mecanismos concorrentes.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-044  
  **Critério de conclusão:** Criar por template usa o mesmo contrato de campos/formulários, com preenchimento e gravação verificados.

- [ ] **CST-046 — Diferenciar campos reconhecidos de texto comum entre colchetes e explicar isso na criação do modelo.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-045  
  **Critério de conclusão:** Campos reconhecidos são explicados; colchetes literais permanecem texto e definição inválida gera erro útil.

- [ ] **CST-047 — Corrigir validação numérica, seleção múltipla e demais tipos de campo com casos reais de preenchimento.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Valor inválido não é aceito no envio; seleção múltipla preserva vários itens, inclusive após reabrir o rascunho.

- [ ] **CST-048 — Conferir condições de visibilidade, cálculos, grupos repetíveis e mensagens de validação.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-047  
  **Critério de conclusão:** Cada tipo de campo, condição, cálculo e grupo repetível funciona com valores iniciais, vazio, erro e correção.

- [ ] **CST-049 — Preservar rascunhos e detectar mudanças no template ou na nota enquanto o formulário está aberto.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-005, CST-045  
  **Critério de conclusão:** Rascunho sobrevive à retomada; mudança no modelo/nota é detectada antes de aplicar respostas antigas.

- [ ] **CST-050 — Permitir fechar o formulário e continuar a ficha no editor, com comportamento definido para respostas ainda não aplicadas.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-045, CST-049  
  **Critério de conclusão:** Fechar permite continuar no editor sem perder respostas; reabrir respeita texto atual e não duplica preenchimento.

- [ ] **CST-051 — Garantir que responder um formulário não conceda automaticamente autorização para escrever arquivos ou guardar memórias.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072  
  **Critério de conclusão:** Respostas são tratadas como dados; escrita e memória continuam sujeitas às respectivas permissões e aprovações.

- [ ] **CST-052 — Consolidar relógio, cronômetro e timer, garantindo acesso previsível dentro do editor.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** Sem dependência adicional; respeitar as regras gerais de dados e validação.  
  **Critério de conclusão:** Ativar o plugin torna relógio/cronômetro/timer acessíveis no editor; iniciar, pausar, zerar e aviso final funcionam.

- [ ] **CST-053 — Tornar obrigatório o checkbox das notas rápidas em todos os caminhos de criação, incluindo usuário, widget e ferramentas da IA.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Toda entrada rápida criada pelos caminhos suportados tem checkbox, dia e horário, sem checkboxes duplicados.

- [ ] **CST-054 — Conferir a organização mensal com dias e horários, preservando os arquivos antigos.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-053  
  **Critério de conclusão:** Novas entradas vão ao mês e seção corretos; notas diárias antigas permanecem intactas.

- [ ] **CST-055 — Permitir capturas consecutivas sem esperar a gravação anterior nem abrir o aplicativo principal.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-057  
  **Critério de conclusão:** Nova captura inicia com a anterior em salvamento; fila preserva ordem e não exige abrir o app principal no Android.

- [ ] **CST-056 — Rever duração, silêncio automático e feedback da gravação para não apressar o usuário nem perder ideias.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Parada manual, silêncio e duração são previsíveis e não cortam a fala sem aviso; limites técnicos reais são comunicados.

- [ ] **CST-057 — Conferir preservação do áudio e recuperação após falha, tela bloqueada e interrupção do processo.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-006  
  **Critério de conclusão:** Áudio pendente permanece recuperável após falha/reinício, sem garantir gravação enquanto o aparelho está desligado.

- [ ] **CST-058 — Implementar o fluxo de áudio longo: importar ou gravar, transcrever, organizar e revisar antes de concluir a nota.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-057  
  **Critério de conclusão:** Áudio longo é importado/gravado, transcrito e organizado sem perda do original antes da conclusão; texto final é revisável.

- [ ] **CST-126 — Definir e validar acesso à captura pela tela de bloqueio Android.**

  **Estado inicial:** Pendente; suporte depende do sistema.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-055, CST-057  
  **Critério de conclusão:** Documentar suporte da versão do Android e requisitos de permissão/desbloqueio; onde suportado, iniciar/parar e recuperar captura sem navegar pelo app. Restrição do sistema é mostrada, sem promessa falsa.

- [ ] **CST-127 — Validar captura rápida própria do Windows.**

  **Estado inicial:** Atalho/bandeja e ditado registrados.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-053, CST-054  
  **Critério de conclusão:** Ctrl+Alt+N, conflito de atalho, bandeja, caixa de nota e Win+H são conferidos; salvamento usa regras de mês/checkbox. Não apresentar esse fluxo como gravação Android em segundo plano.

### 4.7. Publicações, incorporações, grafos e matemática (substitui Páginas)

- [ ] **CST-059 — Resolver ativos vivos de Publicações, inclusive imagens, caminhos relativos e exportações portáveis.**

  **Estado inicial:** Problema anterior de imagens ausentes no editor de páginas; reavaliar no novo fluxo, sem transplantar solução inadequada.  
  **Frente:** Publicações — fundação.  
  **Dependências:** CST-004, CST-038, CST-151, CST-153.  
  **Critério de conclusão:** Imagens, fontes, vídeo, áudio e demais ativos do vault são resolvidos por referências estáveis; mover/renomear/reabrir preserva vínculos; a prévia e cada exportação incluem os recursos necessários, com política definida de embutir/empacotar, direitos, tamanho e aviso recuperável quando faltar algo. Sem depender de um endereço local do dispositivo no arquivo distribuído.

- [ ] **CST-060 — Permitir edição contextual da nota real e comentários de revisão no estúdio de Publicações.**

  **Estado inicial:** A edição contextual e a exclusão de comentários da saída eram ideias válidas do editor anterior; reprojetar sobre o vínculo direto.  
  **Frente:** Publicações — UX e autoria.  
  **Dependências:** CST-005, CST-022, CST-152, CST-154.  
  **Critério de conclusão:** Tocar em texto vinculado permite editar **a nota de origem** com identificação do arquivo/bloco, histórico e undo/redo; mudanças aparecem tanto no editor de notas como na Publicação. Comentários permanecem em revisão e só entram no produto final quando o formato e uma escolha explícita justificarem. Edição do produto não apaga marcações ou escrita alheia.

- [ ] **CST-061 — Aposentar o Editor de Páginas e entregar Publicações como fluxo unificado de autoria, composição e distribuição.**

  **Estado inicial:** Mudança de direção **aprovada pelo proprietário em 31/10/2026**; o velho editor `.page.json` não corresponde à visão e deve sair do app. Aproveitar capacidades identificadas na auditoria, não a arquitetura e a interface como obrigatórias.  
  **Frente:** Publicações — substituição estrutural.  
  **Dependências:** CST-004, CST-149, CST-150, CST-151, CST-156, CST-158.  
  **Critério de conclusão:** UI, comandos, menus, tooltips, guias e entradas exclusivas do antigo Editor de Páginas são retirados ou substituídos. A pessoa consegue, a partir de notas reais, planejar, compor, revisar, pré-visualizar e exportar uma Publicação por um fluxo único, usando ou não IA. Não existe obrigação de compatibilizar a antiga UX; nenhum arquivo do vault é apagado pela remoção. Demonstrar que o código obsoleto saiu e que não restaram acessos quebrados.

- [ ] **CST-062 — Encerrar composições e formatos exclusivos do Editor de Páginas sem perder conteúdos preexistentes.**

  **Estado inicial:** Conversor legado documentado; a nova direção **não** exige reproduzir a experiência `.page.json` ou seus templates.  
  **Frente:** Publicações — descontinuação controlada.  
  **Dependências:** CST-004, CST-006, CST-038, CST-149, CST-157.  
  **Critério de conclusão:** Inventariar `.page.json`, `.template.json`, `.block.json`, composições antigas, links, prévias e documentos de identidade visual antes de remover leitores/menus específicos; manter os arquivos do vault intactos e acessíveis como dados; se houver conversão útil para Publicações, oferecê-la de forma **explícita, verificável e não destrutiva**, sem importação automática nem duplicação silenciosa. Preservar especialmente referências do estudo de identidade Numa. Testar instalações com e sem legados.

- [ ] **CST-063 — Validar todo o ciclo de Publicações no celular, do vínculo direto ao produto final.**

  **Estado inicial:** A versão antiga possuía prévia/editor e exportação HTML via visualizador; há limitações conhecidas no caminho Android WebView → impressão.  
  **Frente:** Publicações — aceite físico.  
  **Dependências:** CST-061, CST-152, CST-164, CST-185, CST-186.  
  **Critério de conclusão:** Criar a partir do vault, modificar nota dentro e fora do estúdio, resolver conflito, pré-visualizar e exportar de verdade no Android; conferir toque, teclado/IME, acessibilidade, suspensão/retomada, offline, arquivos grandes, consumo de RAM e reabertura. Exportar PDF não pode ser considerado concluído apenas por abrir o diálogo de impressão.

- [ ] **CST-064 — Consolidar incorporações e referências vivas em notas e Publicações.**

  **Estado inicial:** Capacidades anteriores de incorporar notas, imagens, HTML, PDF, áudio, vídeo, equações, grafos e código; mantê-las e expandir seus contratos, não o editor antigo.  
  **Frente:** Conclusão e integração / Publicações.  
  **Dependências:** CST-040, CST-041, CST-151, CST-153.  
  **Critério de conclusão:** Nota inteira, bloco, título, intervalo e arquivo incorporado resolvem para a origem correta; conteúdo editável identificado remete ao mesmo serviço de gravação, não a cópia. Incorporadores sem suporte mostram fallback ou erro útil. Exportação estática oferece representação segura de conteúdo interativo, com limites informados.

- [ ] **CST-141 — Habilitar desenvolvimento de código no Urbe, com prévia HTML atualizada ao vivo enquanto o código é editado.**

  **Estado inicial:** Expansão solicitada; escopo técnico e linguagens suportadas a definir.  
  **Frente:** Expansão.  
  **Dependências:** CST-004, CST-036, CST-064, CST-114.  
  **Critério de conclusão:** Criar/abrir e editar arquivos ou projetos de código no Urbe; ao editar HTML, a prévia integrada acompanha as mudanças do código sem atualização manual, no estilo Live Server. Documentar linguagens e recursos suportados, relação entre prévia e arquivos locais e comportamento de salvamento/recuperação. Isolar a execução do conteúdo ativo; validar o fluxo no celular e confirmar que erros no código não comprometem o editor nem os arquivos. {ia:"gpt-6-luna-pro"} A prévia ao vivo deve ser acionável pelo editor normal e por IA via as mesmas capacidades, sem obrigar terminal/IDE; diffs, múltiplos arquivos e projeto externo são escopo distinto (CST-203–204), e HTML ativo executa isolado.

- [ ] **CST-065 — Consolidar grafos livres: criar, salvar, reabrir, editar nós e conexões, agrupar, desenhar e desfazer/refazer.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Grafo reabre idêntico, preserva referências e permite editar e desfazer/refazer sem perder dados.

- [ ] **CST-066 — Conferir referências dos grafos a arquivos e pastas quando esses elementos forem renomeados, movidos ou excluídos.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-038, CST-065  
  **Critério de conclusão:** Mover/renomear/excluir referências não corrompe grafo; alvo ausente é apresentado e pode ser reparado.

- [ ] **CST-067 — Validar a incorporação navegável de grafos nas notas.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-064, CST-065  
  **Critério de conclusão:** Grafo incorporado é navegável e sua interação não bloqueia a edição/rolagem da nota.

- [ ] **CST-068 — Definir e implementar criação e edição de grafos pelos agentes, com prévia e aprovação adequadas.**

  **Estado inicial:** Implementação localizada na `main` (ferramenta `edit_graph` em `AssistantGraphTools.cs` com edição em lote); validar registro efetivo, execução no host, gravação/preview, permissões, regressões e UX física. Não declarar inexistente, tampouco marcar [x] somente por presença de código.
  **Frente:** Expansão.  
  **Dependências:** CST-065, CST-066, CST-072  
  **Critério de conclusão:** IA cria/edita grafo por contrato validado, respeita permissão e aprovação e preserva elementos não alterados. A ferramenta `AssistantGraphTools.Edit` (`edit_graph`) foi localizada em `main` com operações `add_text`, `add_note`, `connect`, `rename`, `remove_node`, `remove_edge` e `group`; por isso implementar do zero não é critério. Verificar seu registro no catálogo, host de gravação, preview, política de aprovação, persistência, referência à nota real, undo/redo e conflitos em Android/Windows; diferenciar grafo autoral de fato comprovado. Parte existente, aceite ainda aberto.

- [ ] **CST-069 — Expandir matemática e visualizações por equações em uma etapa própria, começando por um fluxo completo de inserir, configurar, visualizar e salvar.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Expansão.  
  **Dependências:** CST-026, CST-114  
  **Critério de conclusão:** Equação configurável produz gráfico cartesiano ou visualização física pertinente, com prévia em tempo real e reabertura reproduzível.

- [ ] **CST-135 — Criar Publicações contextualmente a partir de notas, trechos, pasta, busca ou seleção.**

  **Estado inicial:** A ideia antiga de criar uma página a partir de nota/pasta é mantida, mas o destino passa a ser um **projeto de Publicação** com vínculos reais.  
  **Frente:** Publicações — UX.  
  **Dependências:** CST-038, CST-061, CST-151, CST-160.  
  **Critério de conclusão:** Criar Publicação por ação contextual a partir de um ou vários itens cria **referências**, não cópias do conteúdo. Pastas podem oferecer consulta dinâmica por tag/filtro/ordem; alteração de nota refletida no produto; edição do texto no produto escreve diretamente na nota escolhida; referências quebradas têm recuperação.

### 4.8. Agentes, pesquisa, imagens e IA local

- [ ] **CST-070 — Unificar criação e configuração de agentes: instruções por blocos devem estar disponíveis desde a criação.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-071  
  **Critério de conclusão:** Novo agente oferece blocos com título opcional e conteúdo; salvar/reabrir preserva os mesmos blocos da configuração posterior.

- [ ] **CST-071 — Conferir nomes, presets opcionais, modelos e configurações individuais sem duplicações confusas.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004  
  **Critério de conclusão:** Nome, presets e modelos persistem sem criar agentes indesejados; nomes inline identificam o agente correto.

- [ ] **CST-072 — Consolidar permissões: separar acesso às ferramentas, alcance das operações e necessidade de aprovação.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Matriz de acesso/autonomia define ferramentas efetivas; proibição não é contornável por outra ferramenta.

- [ ] **CST-073 — Verificar que mudanças de permissão sejam aplicadas e que a interface explique quando passam a valer.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072  
  **Critério de conclusão:** Configuração salva reaparece e afeta as execuções conforme regra explícita, inclusive consultas iniciadas depois da mudança. Validar o registro da beta.37: permissão mais restritiva vale imediatamente, inclusive em delegação, e uma aprovação mais restrita do solicitante não é contornada pelo especialista.

- [ ] **CST-074 — Conferir início explícito de nova conversa, retomada da conversa existente e exclusão confirmada com remoção do histórico correspondente.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-006, CST-014  
  **Critério de conclusão:** A ação explícita de iniciar conversa cria uma nova; reabrir ou voltar ao agente não deve, por si só, substituir a conversa ainda em uso por uma conversa vazia (ver CST-145). Retomada de conversas anteriores é previsível; exclusão exige confirmação e remove o histórico sem apagar o vault. {ia:"gpt-6-luna-pro"}

- [ ] **CST-145 — Distinguir a conversa ainda em uso de uma situação em que iniciar um chat novo é mais prático.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Relato do usuário: criar um chat novo toda vez que o agente é aberto não está funcionando bem; critérios de decisão ainda precisam ser definidos e validados. {ia:"gpt-6-luna-pro"}  
  **Frente:** Conclusão e integração. {ia:"gpt-6-luna-pro"}  
  **Dependências:** CST-014, CST-074, CST-122 {ia:"gpt-6-luna-pro"}  
  **Critério de conclusão:** Reabrir o agente ou voltar a ele não inicia automaticamente um chat vazio se houver uma conversa em uso; essa conversa permanece aberta/retomável, com seu contexto e rascunho preservados. Quando a conversa não estiver em uso, o app deve distinguir esse estado e só iniciar uma nova quando isso for mais prático e compatível com a intenção do usuário; a ação explícita de iniciar chat novo continua disponível. Definir critérios observáveis para essa distinção, sem assumir que cada abertura significa uma nova conversa, e verificar os fluxos de saída e retorno sem perder histórico ou rascunhos. {ia:"gpt-6-luna-pro"}

- [ ] **CST-146 — Integrar plugins aos agentes configurados para experiências inteligentes, com contexto delimitado, resultados utilizáveis e permissões preservadas.** {ia:"gpt-6-luna-pro"}

  **Estado inicial:** Proposta do usuário; capacidade e arquitetura ainda não implementadas nem avaliadas tecnicamente. Plugins existentes não devem ser presumidos capazes de convocar agentes. {ia:"gpt-6-luna-pro"}
  **Frente:** Expansão. {ia:"gpt-6-luna-pro"}
  **Dependências:** CST-072, CST-073, CST-078, CST-090, CST-092, CST-113; verificar o contrato e as permissões dos plugins antes de definir a integração. {ia:"gpt-6-luna-pro"}
  **Critério de conclusão:** Um plugin autorizado consegue iniciar uma tarefa com agente previamente escolhido ou escolhido pelo usuário, recebendo somente o contexto explicitamente delimitado (por exemplo, projeto, pasta, nota ou conjunto selecionado), sem que a convocação amplie o acesso do agente ao vault. O agente investiga fontes pertinentes e devolve uma resposta estruturada que o plugin pode apresentar e usar em seu painel ou ações. Para recomendações de trabalho, o resultado distingue a ação sugerida, a justificativa, o escopo/passos, o critério de conclusão e as notas/fontes que a sustentam; incertezas e pendências não são apresentadas como fatos confirmados. Recomendação e execução são modos distintos: qualquer escrita ou outra ação efetiva obedece às permissões, aprovações e confirmação aplicáveis e nunca é autorizada apenas pelo pedido do plugin. A pessoa consegue acompanhar o estado/progresso, interromper a execução e consultar o resultado, falhas e pendências. Demonstrar com um plugin **Próxima tarefa**: a partir de um projeto real, reconhecer trabalho concluído, pendências e dependências e recomendar uma tarefa concreta com justificativa, escopo e critério de conclusão; verificar a recomendação contra as notas/fontes citadas. O contrato deve servir a plugins diferentes e deixar coordenação de múltiplos agentes como possibilidade futura, sem presumir execução automática ou acesso compartilhado irrestrito. Registro relacionado: [[FeaturesCheck]]. {ia:"gpt-6-luna-pro"} Testar também plugin de dicionário e plugin Próxima tarefa: a UI do plugin delimita o projeto/nota, dispara solicitação ao agente, recebe estrutura tipada (recomendação, evidência, estado, pendências) e pode propor próximo passo sem automaticamente obter escrita; verificar que o contrato está ausente no `IPluginApi` examinado antes de integrar, e que a conexão não cria API paralela.

- [ ] **CST-075 — Manter uma referência atualizada das ferramentas e funcionalidades do Urbe consultável pelos agentes, incluindo editor e Publicações.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-001  
  **Critério de conclusão:** Referência versionada cobre ferramentas, comandos, editor e fluxos; atualização do app atualiza o conhecimento consultável. O guia de capacidades deve ser gerado/atualizado por versão efetivamente registrada na aplicação e ser recuperável por contexto (editor, vault, grafos, plugins, Publicações), com exemplos e limites reais; testar perguntas de uso em que o agente escolhe a operação nativa correta sem recorrer a conhecimento genérico de outros editores.

- [ ] **CST-076 — Definir a relação entre instruções gerais, `AGENTS.md`, blocos do agente e pedido atual, evitando regras contraditórias.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072  
  **Critério de conclusão:** Documentar e verificar a ordem registrada na beta.37 para configuração contextual do agente: pedido atual, instruções específicas do agente, `AGENTS.md` e instruções gerais do Urbe; preservar separadamente as regras de segurança, permissões e limites de autoridade, que não podem ser rebaixados por texto de menor confiança. Alteração no próprio `AGENTS.md` pelo agente exige aprovação. Testar conflitos em todas as direções.

- [ ] **CST-077 — Adotar o idioma do sistema como padrão, permitir configuração e respeitar pedidos explícitos de outro idioma.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-071  
  **Critério de conclusão:** Idioma inicial segue o sistema; escolha persiste e pedido explícito de outro idioma é respeitado.

- [ ] **CST-078 — Consolidar planejamento proporcional à tarefa, investigação do vault e perguntas que realmente alterem a execução.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-075  
  **Critério de conclusão:** Agente investiga o necessário, pergunta uma questão relevante por vez, reaproveita respostas e mantém plano proporcional sem cotas artificiais. Plano, pergunta, aprovação e execução são estados diferentes. Contexto inicial deve incluir seleção real do editor, projeto/nota/pasta, objetivo e entregável esperado, mas não todo o vault; retomar sem repetir perguntas respondidas ou divulgar memórias privadas.

- [ ] **CST-079 — Separar revisão interna e resposta final: a entrega deve explicar o trabalho realizado, seus resultados e pendências.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-033, CST-078  
  **Critério de conclusão:** Rascunho da revisão não vaza nem substitui entrega; resposta final diz o que mudou e quais arquivos/resultados foram produzidos. Revisão textual do próprio modelo não equivale a verificação independente. Para nota, código, documento, imagem e vídeo, registrar evidência concreta do artefato final ou indicar explicitamente o que não pôde ser verificado (CST-213, CST-218).

- [ ] **CST-080 — Conferir o miniassistente e os comandos inline em estados de trabalho, pergunta, aprovação, erro e conclusão.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-010, CST-034  
  **Critério de conclusão:** Botão flutuante e comando inline permitem acompanhar, responder, aprovar/editar/recusar e ver resultado sem perder a nota. No contexto do editor, Explorer, painel do grafo, formulário ou Publicações, mostrar somente a ação/proposta/progresso útil da tarefa atual, incluindo destino de escrita; sem obrigar mudar para uma conversa nem poluir a UI com logs por padrão (CST-225).

- [ ] **CST-081 — Consolidar anexos e análise de imagens com indicação clara de compatibilidade do modelo.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-014  
  **Critério de conclusão:** Texto, nota, imagem e PDF seguem o fluxo previsto; incompatibilidade do modelo e limites reais são apresentados antes de falso sucesso. Conferir especificamente os relatos de até 20 anexos por mensagem e 30 MB agregados na beta.37, tratamento de fotos grandes e orientação EXIF, compressão com qualidade verificável, retomada/cancelamento, preservação de arquivos, e falhas úteis; a validação física fica também em CST-194.

- [ ] **CST-082 — Consolidar geração de imagens: escolha de modelo, falhas, custos, prévia, exportação e uso no vault e nas Publicações.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-132  
  **Critério de conclusão:** Geração real retorna arquivos acessíveis, prévia/zoom/exportação e custo disponível; falha e nova tentativa não duplicam resultados silenciosamente. A mídia gerada é um ativo real do vault, referenciável por uma ou mais Publicações, com proveniência/modelo e licença/uso explícitos quando pertinentes.

- [ ] **CST-083 — Conferir pesquisa web gratuita por padrão, leitura das fontes, citações verificáveis e comunicação de falhas ou leitura parcial.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-084  
  **Critério de conclusão:** Pesquisa padrão dispensa chave; fontes lidas têm URL verificável e afirmações distinguem confirmação, snippet e inferência. Citação de fato derivado do vault deve apontar nota/identidade/trecho e revisão, não apenas URL externa; explicar a diferença entre pesquisa web, evidência de arquivo e dedução do agente (CST-223).

- [ ] **CST-084 — Verificar que conteúdo externo não consiga alterar instruções, permissões ou provocar envio indevido de dados do vault.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072  
  **Critério de conclusão:** Conteúdo de páginas, anexos e memórias é dado não confiável; não altera permissões nem autoriza exfiltração. Testar prompt injection em nota, PDF, imagem OCR, saída de skill, retorno de MCP e página externa. Nunca permitir que texto obtido se promova a instrução/autorização, nem que um executor use dados privados como argumentos de rede sem consentimento.

- [ ] **CST-113 — Conferir a seleção de ferramentas do agente.**

  **Estado inicial:** Relato a investigar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072, CST-073  
  **Critério de conclusão:** Seleção na tela, configuração persistida e catálogo enviado ao modelo coincidem após salvar, trocar agente e reiniciar; ferramenta proibida é recusada na execução.

- [ ] **CST-115 — Completar a caixa adaptativa com indicador de dúvidas resolvidas e compreensão declarada pelo agente.**

  **Estado inicial:** Interação existente; indicador pendente.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-078, CST-047  
  **Critério de conclusão:** Uma resposta pode resolver várias dúvidas; novas dúvidas relevantes podem alterar o indicador. Mostrar o que foi esclarecido e o que falta; indicador é estimativa declarada, nunca medição objetiva de compreensão.

- [ ] **CST-116 — Validar download e armazenamento do modelo local.**

  **Estado inicial:** IA local registrada; validação específica necessária.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-007  
  **Critério de conclusão:** Download só sob solicitação, informa tamanho/espaço, pausa/retoma, verifica integridade e trata falta de espaço/rede; modelo fica fora do vault e arquivo incompleto não é carregado.

- [ ] **CST-117 — Validar IA local offline no Android e Windows.**

  **Estado inicial:** IA local registrada; suporte real por plataforma a validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-116, CST-072, CST-114  
  **Critério de conclusão:** Modelo carregado responde sem rede, ferramentas suportadas respeitam permissões, cancelamento libera recursos e falha de memória não derruba o app; medir carregamento/primeira resposta no moto g32. Incompatibilidade real é informada.

- [ ] **CST-118 — Definir evolução da IA local sem prometer capacidades ausentes.**

  **Estado inicial:** Imagens offline não concluídas; demais evoluções a avaliar.  
  **Frente:** Expansão.  
  **Dependências:** CST-117, CST-082  
  **Critério de conclusão:** Reserva offline só troca de modelo mediante preferência explícita e informa a troca. Cache em disco e imagens offline têm viabilidade, recursos e suporte por plataforma documentados; ausência não é marcada como pronta.

- [ ] **CST-121 — Conferir seleção e cópia de texto e código no chat.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-015, CST-016  
  **Critério de conclusão:** Selecionar, ampliar e copiar em Android/Windows preserva conteúdo e blocos de código sem disparar ações indevidas.

- [ ] **CST-122 — Conferir rascunhos de chat e anexos ao navegar.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-009, CST-014  
  **Critério de conclusão:** Rascunhos e anexos não migram para outro agente/chat, nem desaparecem ao abrir configurações ou retornar do segundo plano. Conferir restauração real da beta.37 após o Android encerrar o processo, sem multiplicar anexos, reenvios ou estados intermediários.

- [ ] **CST-123 — Conferir apresentação de tokens, preços e custos.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-082, CST-094  
  **Critério de conclusão:** Distinguir uso medido, estimativa e valor indisponível; mostrar unidade/fonte dos preços e custo por conversa sem interpretar desconhecido como gratuito. Acúmulo não duplica ao reabrir.

- [ ] **CST-124 — Conferir painel de fontes e ligação com as afirmações do chat.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-083, CST-084  
  **Critério de conclusão:** Fonte consultável corresponde ao resultado real; link abre corretamente e distingue snippet, página lida e leitura parcial.

- [ ] **CST-132 — Conferir cadastro e reutilização dos provedores.**

  **Estado inicial:** Implementação registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-071, CST-072  
  **Critério de conclusão:** Cadastrar provedor/chave uma vez, selecionar modelos por agente e imagem, editar/remover sem perder configuração alheia; credenciais não aparecem no vault, exportação ou logs.

- [ ] **CST-138 — Preservar qualidade de geração de imagens em modelos de chat mais simples.**

  **Estado inicial:** Melhoria solicitada; qualidade a verificar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-075, CST-082  
  **Critério de conclusão:** Modelos compatíveis usam o guia e a ferramenta visual correta sem trocar o chat; comparar tarefas reais, falhas e retrabalho. Melhorar instruções/ferramentas onde houver defeito demonstrado.

### 4.9. Memória e colaboração

- [ ] **CST-085 — Tornar visível quais memórias são privadas do agente e quais são compartilhadas.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072  
  **Critério de conclusão:** Gerência identifica agente e compartilhamento efetivos; memória privada não aparece ao agente errado.

- [ ] **CST-086 — Permitir inspecionar, corrigir, substituir e excluir memórias a partir do contexto adequado.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-085  
  **Critério de conclusão:** Pessoa consegue ler, corrigir, substituir e excluir registros, com efeito persistido na próxima recuperação.

- [ ] **CST-087 — Investigar falhas de recuperação com exemplos concretos de acontecimentos recentes.**

  **Estado inicial:** Relato ou lacuna a investigar; causa não confirmada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-003, CST-085  
  **Critério de conclusão:** Casos recentes são recuperados quando pertinentes; ausências têm diagnóstico de escopo, persistência ou busca, sem inventar lembrança. Teste de recuperação deve incluir questão indireta, sinônimo, relação de dois ou mais documentos, informação contraditória, nota renomeada, versão alterada e caso sem resposta; medir recuperação/precisão e justificar quando a informação não existe (CST-223).

- [ ] **CST-088 — Distinguir episódio registrado, resultado declarado pelo agente e resultado efetivamente verificado.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-085  
  **Critério de conclusão:** Registro preserva origem, confiança e estado; resultado declarado não é automaticamente tratado como validado.

- [ ] **CST-089 — Implementar consulta a resumos de conversas recentes e acesso detalhado ao histórico autorizado, preservando separação entre agentes.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-085, CST-086, CST-074  
  **Critério de conclusão:** Consulta a resumo e histórico detalhado respeita autoria/agente e permite recuperar a fonte, sem acesso global implícito. O contexto recuperado de conversas deve manter atribuição de agente, papel, fonte, consentimento e status de evidência; compactação não converte resumo do agente em verdade do vault. Recuperar decisão real depois de chat longo sem misturar transcrições entre agentes.

- [ ] **CST-090 — Conferir consultas entre agentes com modelos, permissões, memórias e históricos próprios.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-014, CST-072  
  **Critério de conclusão:** Especialista usa suas configurações e permissões, mantendo vínculo verificável com a conversa de origem. A delegação deve carregar escopo autorizado, referências de fontes, pedido e contrato de resultado, sem acesso ao histórico/memória privada do especialista ou do solicitante por atalho. A resposta deve retornar à conversa solicitante com evidências.

- [ ] **CST-091 — Encaminhar perguntas, formulários e aprovações do especialista para a conversa solicitante, permitindo continuar sem troca manual de chat.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-047, CST-090  
  **Critério de conclusão:** Perguntas/formulários/aprovações aparecem e são respondidos no chat de origem, chegam ao especialista correto e a consulta continua. Validar encaminhamento real de perguntas, formulários e aprovações sem trocar de chat. Apresentar claramente qual agente propõe cada ação e quem tem autorização; uma resposta ao formulário não significa aprovação de escrita.

- [ ] **CST-092 — Conferir retorno de textos, imagens, arquivos, erros e cancelamentos ao agente solicitante.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-090, CST-091  
  **Critério de conclusão:** Resultado, arquivo, imagem, erro e cancelamento chegam ao solicitante sem confundir conclusão com espera por intervenção. Contrato de resposta inclui artefato real, identidade/revisão, fontes, estado verificado/parcial, custos quando disponíveis e erro recuperável; não converter mensagens genéricas de subagente em comprovante de execução.

- [ ] **CST-093 — Impedir que a delegação contorne permissões ou produza consultas recursivas sem controle.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-072, CST-090  
  **Critério de conclusão:** Delegação não amplia acesso; recursão é detectada e tratada sem esconder ao usuário o bloqueio ou estado. Testar especialista mais permissivo, plugins que chamam agentes, cadeia de delegação, ferramenta indireta e revogação durante a execução; não conceder autoridade pela relação pai-filho entre chats.

- [ ] **CST-133 — Conferir proveniência, conflitos e substituições da memória.**

  **Estado inicial:** Memória tipada registrada.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-085, CST-086, CST-088  
  **Critério de conclusão:** Tipos, origem, confiança, datas, conflitos e registros substituídos são inspecionáveis; exclusão/substituição altera recuperação sem reativar lembrança obsoleta.

- [ ] **CST-134 — Conferir relações entre memória e arquivos/grafos.**

  **Estado inicial:** Relações registradas; alcance a verificar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-066, CST-085, CST-133  
  **Critério de conclusão:** Relação aponta à fonte real e reflete ausência/renomeação; grafo de memória é inspecionável no escopo do agente, sem confundir canvas livre com memória ou alegar embeddings existentes.

### 4.10. Eficiência, desempenho e regressões

- [ ] **CST-094 — Instrumentar tempo total, primeiro resultado útil, chamadas, falhas, tentativas, releituras, alterações e verificações quando esses dados puderem ser medidos.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-097, CST-114  
  **Critério de conclusão:** Instrumentação mede os eventos observáveis por tarefa e registra dados ausentes como indisponíveis. Incluir identificação de classe de tarefa e artefato entregue para calcular resultado verificável, sem armazenar conteúdo privado nos logs.

- [ ] **CST-095 — Separar tempos de rede/provedor e ferramentas quando houver instrumentação; não inventar duração de raciocínio.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-094  
  **Critério de conclusão:** Tempos medidos separam componentes quando possível; nenhuma duração interna não observada é estimada como fato.

- [ ] **CST-096 — Combinar latência com conclusão, retrabalho e qualidade verificada, em vez de premiar apenas respostas rápidas ou poucas chamadas.**

  **Estado inicial:** Implementação parcial registrada.  
  **Frente:** Estabilização.  
  **Dependências:** CST-094  
  **Critério de conclusão:** Relatórios por classe/complexidade apresentam mediana, percentis, conclusão e retrabalho com amostra identificada. Comparar por objetivo concluído e qualidade mantendo modelo, contexto, ferramentas e hardware identificados; obter custo por produto validado e por edição correta, não apenas por mensagem ou token.

- [ ] **CST-098 — Consolidar reutilização de leituras, prevenção de gravações idênticas, contexto por versão e detecção de ciclos sem progresso.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-005, CST-094  
  **Critério de conclusão:** Leitura reutilizada depende da versão correta; no-op não grava/aprova/revisa inutilmente; ciclo sem progresso fica observável. Comparar com compactação de sessão OpenCode/Codex: preservar âncora do pedido, fontes e decisões identificadas, estado da tarefa fora da janela do modelo, e métricas de perda de contexto. Cache de leitura usa revisão exata; não suprimir chamadas úteis sob pretexto de eficiência.

- [ ] **CST-099 — Medir abertura, edição, busca, importação, movimentação e exclusão em vaults de diferentes tamanhos.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-114  
  **Critério de conclusão:** Medições repetíveis com tamanhos de vault identificados permitem comparar antes/depois por operação.

- [ ] **CST-100 — Testar conversas longas e múltiplos agentes simultâneos no moto g32.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-014, CST-114  
  **Critério de conclusão:** Moto g32 executa o cenário registrado de conversa extensa/multichat sem troca de estados ou congelamento, com medições anexadas. Acrescentar tarefas reais de 100+ notas, duas Publicações usando uma fonte e agentes com longos retornos; medir UI, consumo e estado, não apenas completar turnos de chat.

- [ ] **CST-101 — Procurar bugs combinando recursos: formulário com nota alterada, consulta com aprovação pendente, arquivo movido enquanto está aberto, retorno do segundo plano e falha de rede durante pesquisa.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003, CST-004, CST-014  
  **Critério de conclusão:** Cenários cruzados preservam consistência e mostram retomada/erro úteis nas interrupções. Adicionar em matriz cruzada: nota alterada enquanto publicação renderiza, skill acionando plugin, consulta entre agentes com aprovação, nova versão de arquivo após embedding, cancelamento de geração externa e retomada de render parcial. Cada cenário tem esperado/observado.

- [ ] **CST-102 — Acrescentar testes de regressão para os defeitos relevantes encontrados, sem confundir testes automatizados com validação no aparelho.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Estabilização.  
  **Dependências:** CST-003  
  **Critério de conclusão:** Correções relevantes têm regressão automatizada quando cabível e validação física quando dependem de Android/IME/gesto.

### 4.11. Experiência, identidade, documentação e releases

- [ ] **CST-103 — Atualizar o tutorial para refletir os fluxos consolidados, incluindo Publicações, limitações reais e recuperação de erros.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-075  
  **Critério de conclusão:** Tutorial somente leitura inclui índice, pesquisa, imagens pertinentes, fluxos atuais e recuperação; não cria notas sem pedido.

- [ ] **CST-104 — Conferir configuração inicial, retomada da última nota e descoberta das ferramentas sem depender do tutorial inteiro.**

  **Estado inicial:** Implementação registrada; consolidar e validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-114  
  **Critério de conclusão:** Primeira abertura pode ser adiada/refeita; retorno à última nota não espera tarefas não essenciais e preserva escolha de vault.

- [ ] **CST-105 — Revisar acessibilidade, contraste, foco, áreas de toque e consistência entre editor, formulários, Publicações, grafos e IA.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-015, CST-016  
  **Critério de conclusão:** Controles são operáveis com toque, foco e recursos assistivos; tamanho de fonte/contraste não escondem ações. Conferir especificamente os registros da beta.37 de áreas de toque de pelo menos 40 px nas superfícies principais/Personalização e contraste WCAG AA nos temas; testar com teclado, zoom de fonte e recursos assistivos no Android.

- [ ] **CST-106 — Consolidar organização visual, reduzindo caixas redundantes e mantendo estados e informações fáceis de acompanhar.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-008, CST-112  
  **Critério de conclusão:** Hierarquia visual mostra estados e informações sem caixas redundantes; navegação e ações preservam contexto.

- [ ] **CST-107 — Implementar a proposta de Numa com referência ao estudo aprovado e verificar disponibilidade jurídica, comercial, em domínios e lojas.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-001  
  **Critério de conclusão:** Estudo aprovado é conferido e verificações jurídica/comercial/domínios/lojas têm resultado documentado, sem presumir disponibilidade.

- [ ] **CST-108 — Aplicar nome e identidade nos pontos definidos, preservando a aparência já aprovada.**

  **Estado inicial:** Entrega pendente nos documentos.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-107, CST-130  
  **Critério de conclusão:** Nome Numa e símbolo aprovado são aplicados consistentemente; cores e aparência preservam o que já foi aprovado.

- [ ] **CST-109 — Reconciliar o FeaturesCheck com as entregas verificadas, mantendo bugs e melhorias separados da existência das funcionalidades.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-001, CST-131  
  **Critério de conclusão:** A seção 6.2 desta consolidação mapeia 100% das 131 entradas de FeaturesCheck, preservando seu estado registrado na seção 13; a checklist CST separa implementação relatada, defeito, expansão e aceite verificado. O FeaturesCheck independente torna-se um retrato histórico, não uma segunda fonte normativa. Cada mudança posterior aponta prova/versão e não apaga relato anterior.

- [ ] **CST-110 — Gerar e validar a release correspondente ao estado consolidado, com lista clara do que foi entregue e do que permanece pendente.**

  **Estado inicial:** Verificação ou organização necessária; estado técnico a apurar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-006, CST-102, CST-125  
  **Critério de conclusão:** Build entregue é rastreável ao commit/versão, instala/atualiza preservando dados e tem validação registrada e pendências explícitas.

- [ ] **CST-125 — Conferir versão instalada e atualização por plataforma.**

  **Estado inicial:** Verificação manual registrada; fluxo completo a validar.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-004, CST-006  
  **Critério de conclusão:** App identifica versão/commit/canal, encontra pacote compatível do próprio produto e informa falhas; atualização Android/Windows preserva vault e configurações e não usa release global errada.

- [ ] **CST-130 — Preservar direção e referência exatas da identidade Numa.**

  **Estado inicial:** Proposta aprovada; aplicação pendente.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-107  
  **Critério de conclusão:** Consultar o arquivo preservado `Páginas/Identidade Numa — estudo de logo.page.json` no vault (legado somente como referência documental). Símbolo abstrato orgânico de curvas suaves ao redor de espaço central; preservar cores e visual aprovados e a intenção de lugar de criação. Se o estudo não estiver disponível, registrar o bloqueio em vez de reinventá-lo.

- [ ] **CST-139 — Definir releases por entregas verificadas, sem esperar todas as expansões.**

  **Estado inicial:** Organização proposta nesta consolidação.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-109, CST-110  
  **Critério de conclusão:** Cada incremento tem escopo rastreável, regressões pertinentes, pacote e pendências; expansões não bloqueiam correção urgente já validada.

- [ ] **CST-140 — Organizar validação do fluxo de trabalho e atalhos.**

  **Estado inicial:** Prioridade geral; resultados a conferir.  
  **Frente:** Conclusão e integração.  
  **Dependências:** CST-080, CST-104, CST-105  
  **Critério de conclusão:** Fluxos completos de escrever, consultar, preencher e produzir Publicação/artefato preservam contexto, origem das notas e rascunhos; atalhos configuráveis documentados e operáveis sem conflitos ocultos.

### 4.12. Publicações — substituição, contratos, IA, audiovisual e distribuição

**Autoridade desta nova frente:** seção 1.1 e especificação da seção 9; as referências da seção 10 orientam investigação e testes, **não** impõem adoção de bibliotecas ou licença. Todas as tarefas abaixo são **novas, não implementadas nem verificadas nesta revisão**. A numeração inicia em CST-149, depois do último ID existente (CST-148). Priorizar segurança dos dados e núcleo da ligação direta antes de expansões de formato, sem impedir trabalhos independentes nem sacrificar qualidade.

#### 4.12.1. Descontinuação e base semântica

- [ ] **CST-149 — Auditar e remover a superfície do Editor de Páginas legado.**  
  **Estado inicial:** Remoção decidida; código existente em `PageEngine`, `PageStudioView`, `MainView.Pages/Studio`, renderizadores, templates, modelos e testes ainda precisa de mapa de dependências.  
  **Dependências:** CST-004, CST-006.  
  **Critério de conclusão:** Matriz arquivo/símbolo → remover/reaproveitar/migrar; UI e entrypoints antigos removidos sem apagar arquivos do vault; código C# compartilhado válido reaproveitado conscientemente; build/tests e rotas do app íntegros; não há dois estúdios concorrentes. Auditar explicitamente `AssistantPanel.Tools`, `AssistantPageTools` (`page_schema`/`write_page`), guia e cache `AssistantExecutionEfficiency`: remover ou redirecionar essas tools quando Publicações tiver contrato equivalente; nunca deixar agentes criarem `.page.json` como produto novo por caminho oculto.

- [ ] **CST-150 — Definir o contrato de Publicação separado do arquivo de conteúdo.**  
  **Estado inicial:** Não há contrato unificado confirmado.  
  **Dependências:** CST-001, CST-004, CST-151.  
  **Critério de conclusão:** Schema versionado especifica `publicationId`, título, objetivo, público, fontes, peças editoriais, composições, tema/design tokens, formatos, perfis e exportações, com evolução de versão, armazenamento local e round-trip sem perda. Proíbe espelhar texto das notas nos projetos de publicação.

- [ ] **CST-151 — Expor notas/ativos como API interna de fontes vivas e tipadas.**  
  **Estado inicial:** Vault, IDs, links, backlinks, transclusões e leitura existem parcialmente; contrato de publicação pendente.  
  **Dependências:** CST-004, CST-005, CST-006, CST-038, CST-040, CST-120.  
  **Critério de conclusão:** Resolver por identidade persistente, bloco, heading, range, ativo e versão; acompanhar mover/renomear/deletar/restaurar; notificar consumidores; respeitar escopo/ACL. Sem servidor HTTP obrigatório nem dados duplicados. Os arquivos do vault são as fontes reais. Além da referência viva para Publicações, expor lookup de nota/bloco/trecho/tabela/propriedade/grafo com identificadores e versões, acesso restrito ao escopo da solicitação; consultas devem suportar proveniência e operações estruturadas sem duplicar conteúdos (CST-223–224).

- [ ] **CST-152 — Realizar edição direta e bidirecional entre estúdio e notas.**  
  **Estado inicial:** Requisito expresso do proprietário; não confundir com sincronização de cópias.  
  **Dependências:** CST-004, CST-005, CST-006, CST-027–CST-034, CST-151.  
  **Critério de conclusão:** Seleção e edição de texto, tabelas e dados vinculados no estúdio alteram os bytes da nota real pelo serviço de escrita; editor e outras Publicações veem o resultado; editar diretamente a nota atualiza todas as composições dependentes. Mostrar origem/destino, preservar histórico/autoria/undo e não sobrescrever nota externa sem resolver conflito. Editar layout não altera texto-fonte. Mesma operação é acionável por toque/teclado, plugin e agente; não escrever através de um JSON de composição nem deixar uma resposta de chat substituir o editor nativo.

- [ ] **CST-153 — Implementar dependências, eventos, atualização incremental e recuperação de vínculos.**  
  **Estado inicial:** A definir.  
  **Dependências:** CST-005, CST-038, CST-151, CST-152, CST-114.  
  **Critério de conclusão:** Alteração salva em qualquer fonte invalida somente projeções afetadas, re-renderiza conforme custo sem travar digitação, indica versão/estado e informa arquivo ausente; concorrência, renomeio e importação recuperam vínculos de modo determinístico. Nenhum polling pesado indiscriminado. Diferenciar alteração de texto, mudança semântica, referência removida e mudança somente visual; produzir conjunto de produtos afetados com versão/hash e invalidar somente derivados pertinentes. Estender rastreabilidade a consultas dependentes por CST-226.

- [ ] **CST-154 — Garantir escrita transacional, undo/redo, conflitos e autoria para as edições editoriais e de agentes.**  
  **Estado inicial:** Serviços de notas e aprovações são base; edição de múltiplas fontes no estúdio ainda não contratada.  
  **Dependências:** CST-005, CST-006, CST-007, CST-034, CST-151, CST-152, CST-072.  
  **Critério de conclusão:** Proposta/diff mostra cada nota e trecho de destino; falha em operação composta permite recuperar/continuar sem perder conteúdo; histórico identifica origem e agente; desfazer não reverte edição externa alheia; conflitos são resolvidos, nunca sobrescritos silenciosamente.

- [ ] **CST-155 — Distinguir conteúdo da fonte e adaptações editoriais sem romper o vínculo direto.**  
  **Estado inicial:** Decisão de produto expressa.  
  **Dependências:** CST-150, CST-151, CST-152.  
  **Critério de conclusão:** Edição de citação/trecho fiel escreve na nota original; versão resumida, roteiro, fala, tradução ou texto adaptado existe como **nota editorial real** vinculada à origem e referenciada pelo produto; estilo, ordem e animação ficam somente na composição. Não duplicar texto invisível no `.publication.json`; mostrar procedência e permitir atualizar a adaptação com comparação explícita.

- [ ] **CST-156 — Definir árvore semântica e composições independentes da renderização final.**  
  **Estado inicial:** HTML é substrato preferencial, mas não modelo canônico universal.  
  **Dependências:** CST-150, CST-151, CST-155.  
  **Critério de conclusão:** Modelo suporta parágrafos, capítulos, imagens, referências, tabelas, fórmulas, gráficos, cenas, slides, acessibilidade, interações, eventos e temporalidade sem reduzir tudo a HTML/pixels; prever perfis por destino e fallback. Testar round-trip e reordenação de fonte sem perder identidade. Incluir referências tipadas a trechos e componentes editáveis no modelo canônico, com operações semânticas e mapeamento de alterações de volta à nota real; separar cálculos/visualizações derivados dos dados originais.

- [ ] **CST-157 — Preservar dados legados do vault e definir destino explícito do antigo `.page.json`.**  
  **Estado inicial:** Arquivos legados podem conter conteúdos e elementos de identidade aprovados.  
  **Dependências:** CST-004, CST-006, CST-149.  
  **Critério de conclusão:** Arquivos existentes permanecem intocados ao atualizar; podem ser abertos como arquivos textuais/JSON e eventualmente importados sob comando, com relatório de partes convertíveis/não convertíveis, original preservado, sem criar cópia silenciosa de notas.

#### 4.12.2. Estúdio único e qualidade visual

- [ ] **CST-158 — Projetar UX nativa, minimalista e contextual do Estúdio de Publicações.**  
  **Estado inicial:** Produto novo; não utilizar o velho Page Studio como UI definitiva.  
  **Dependências:** CST-015, CST-016, CST-017, CST-105, CST-150, CST-151.  
  **Critério de conclusão:** Criar do Explorer/editor/chat, escolher público/produto, compor na prévia, editar origem ao tocar, abrir ferramentas só quando necessário, inspecionar fontes, revisão e exportação em um fluxo único; suporte manual completo, acessível e touch-first; testes reais com mais de um formato, sem painéis redundantes. Agente pode começar pelo contexto do toque/seleção sem forçar uma tela de chat. Em cada interação oferecer próximo passo, prévia, histórico e destino editado de modo discreto; código e timelines complexas só são expostos quando usuário desejar.

- [ ] **CST-159 — Recuperar as qualidades úteis das Páginas antigas como recursos, não como editor.**  
  **Estado inicial:** Auditoria identificou seções livres, blocos reutilizáveis, temas, capas, capítulos, sumários, notas por pasta/tag, elementos incorporáveis e edição contextual.  
  **Dependências:** CST-149, CST-156, CST-158.  
  **Critério de conclusão:** Catalogar caso a caso o que pode virar componente de composição (título, texto, grade, imagem, botão, vídeo, tabela, código, fórmula, mapa, citação, TOC, capa, template, tema) sem obrigar cópia do schema ou da interface antigos; provas de uso em múltiplos produtos.

- [ ] **CST-160 — Integrar seleção semântica de fontes, pastas e consultas dinâmicas.**  
  **Estado inicial:** Recursos de notas por pasta/tag e seleção já existem parcialmente.  
  **Dependências:** CST-038, CST-040, CST-042, CST-120, CST-151.  
  **Critério de conclusão:** Adicionar fontes por busca, seleção, vínculo, tag, pasta e transclusão; ordenar, filtrar e reagrupar; mudanças de fontes se refletem em publicação sem copiar texto. Consultas dinâmicas ficam versionadas e indicam mudanças para revisão editorial. Fontes selecionadas devem suportar busca híbrida validada, agrupamento por relações e trechos, filtros/propagação de revisões; cada resultado da IA indica por que aquela fonte foi incluída e qual parte foi usada (CST-223).

- [ ] **CST-161 — Criar composição visual livre, componentes reutilizáveis e sistema de identidade.**  
  **Estado inicial:** Temas, blocos e templates antigos são insumos de pesquisa.  
  **Dependências:** CST-156, CST-158, CST-159.  
  **Critério de conclusão:** Design tokens (tipografia, hierarquia, espaçamento, cores, grids, responsividade, acessibilidade), arranjo livre, estilos por destino, modelos e blocos reutilizáveis; não reduzir capacidade a galerias fechadas. Reedição preserva vínculo com a nota e identidade Numa.

- [ ] **CST-162 — Produzir HTML/CSS/SVG portável como primeira superfície de saída e prévia.**  
  **Estado inicial:** HTML existe em `PageEngine`/`PageRenderer` e `LiveHtmlPane`; devem ser avaliados como componentes reutilizáveis.  
  **Dependências:** CST-059, CST-156, CST-161, CST-182.  
  **Critério de conclusão:** HTML sem dependências privadas não autorizadas, assets resolvidos, CSS responsivo/print separado, recursos offline/embutidos por perfil, JavaScript somente quando necessário e isolado; prévia corresponde ao exportável; HTML nunca é necessário para gerar texto em formatos não-web.

- [ ] **CST-163 — Definir empacotamento de mídia e produtos distribuíveis.**  
  **Estado inicial:** Ausente como sistema universal.  
  **Dependências:** CST-059, CST-151, CST-162.  
  **Critério de conclusão:** Exportação como arquivo único ou pacote com manifest, mime correto, checksums e mídia licenciada; links relativos íntegros, tamanho/custo/progresso/armazenamento estimados, arquivos entregues onde o usuário escolher; nenhum sucesso falso por mostrar apenas código na tela.

#### 4.12.3. Publicação multiformato

- [ ] **CST-164 — Implementar PDF paginado editorial com motor apropriado.**  
  **Estado inicial:** O PDF legado utiliza Android WebView e sistema de impressão; beta.38 corrigiu ciclo de vida/papel, não compôs uma solução editorial profissional.  
  **Dependências:** CST-059, CST-156, CST-161, CST-162, CST-185.  
  **Critério de conclusão:** PDF real em arquivo, com páginas no tamanho correto, margens, numeração, capítulos, TOC, rodapés, imagens, fontes, links, quebras, órfãs/viúvas e orientação adequadas ao projeto; medir qualidade Android/Windows. Comparar abordagens Paged.js/Vivliostyle/QuestPDF/Chromium e escolher com evidência/licença. Não considerar abrir diálogo de impressão como exportação concluída.

- [ ] **CST-165 — Produzir livros digitais EPUB e livros web acessíveis.**  
  **Estado inicial:** Expansão nova.  
  **Dependências:** CST-156, CST-161, CST-162, CST-163.  
  **Critério de conclusão:** EPUB3 com navegação, capítulos, metadados, imagens, fontes adequadas, fallback de elementos interativos e validação em leitores/EPUBCheck; livros HTML com TOC, busca e estrutura responsiva; conteúdo permanece vinculado às notas para reedição.

- [ ] **CST-166 — Adicionar documentos de escritório DOCX/ODT por exportação semântica.**  
  **Estado inicial:** Novo adaptador; não presumir fidelidade 1:1 com HTML.  
  **Dependências:** CST-156, CST-161, CST-185.  
  **Critério de conclusão:** Documento com estilos, títulos, listas, tabelas, imagens, notas e referências nativos/editáveis onde suportado, validação de abertura/edição externa e relatório de limitações; não achatar sem informar.

- [ ] **CST-167 — Produzir apresentações HTML interativas e PDF de slides.**  
  **Estado inicial:** Adaptador novo; estudar reveal.js/Marp/Quarto.  
  **Dependências:** CST-156, CST-161, CST-162.  
  **Critério de conclusão:** Slides com temas, layouts, notas de apresentador, transições, mídia, fórmulas/gráficos, navegação e exportação HTML/PDF; visualização respeita proporção e detecta overflow. Texto vinculado edita a nota-fonte; organização de slides pertence ao projeto.

- [ ] **CST-168 — Gerar PPTX com contrato explícito de editabilidade versus fidelidade.**  
  **Estado inicial:** Exportação HTML→slides como imagens não satisfaz edição nativa; referência Marp documenta esse compromisso.  
  **Dependências:** CST-156, CST-161, CST-167, CST-185.  
  **Critério de conclusão:** Oferecer PPTX editável (formas, textos, imagens e tabelas nativas quando possível) e, se houver modo fidelidade, identificá-lo separadamente como achatado; teste real em PowerPoint/compatível, sem prometer edição de conteúdo raster. Investigar Open XML SDK e código aberto de referência.

- [ ] **CST-169 — Exportar imagens, diagramas, infográficos e vetores.**  
  **Estado inicial:** Geração de imagens por provedores já existe, mas a composição editorial não é a mesma coisa.  
  **Dependências:** CST-082, CST-156, CST-161, CST-163.  
  **Critério de conclusão:** SVG vetorial limpo, PNG/JPEG/WebP em dimensões e DPI/resolução declarados, fundos/transparência quando suportados, gráficos com dados corretos, prévia e nitidez; rasterizar somente quando o destino pedir; imagens geradas por IA viram ativos do vault com proveniência.

- [ ] **CST-170 — Definir documento audiovisual editável: cenas, timeline, trilhas e keyframes.**  
  **Estado inicial:** Frente nova guiada pelo princípio do Claude Motion, Motion Canvas e Remotion; sem dependência do produto proprietário Claude.  
  **Dependências:** CST-151, CST-155, CST-156, CST-161.  
  **Critério de conclusão:** Projeto persistente de vídeo usa fontes do vault, objetos/cenas, tempo, animações por função/keyframes, câmera 2D/3D quando suportada, transições, legendas, voz/música/efeitos, duração, fps e proporção; edição da fala/roteiro vinculado altera sua nota, e edição de tempo não. Reabrir preserva sequência e identidade dos elementos. **Fronteira com CST-209:** esta tarefa define documento/cena/keyframes/UX editável e referências às notas reais; CST-209 fornece o renderizador/encoder MP4 por plataforma. Não duplicar implementação de codec no schema editorial.

- [ ] **CST-171 — Entregar motion graphics gerados por código e arquivo MP4 real.**  
  **Estado inicial:** Novo motor a pesquisar; não confundir IA textual com modelo de geração de vídeo.  
  **Dependências:** CST-162, CST-170, CST-181, CST-185, CST-190, CST-209.
  **Critério de conclusão:** Agente ou pessoa cria explainer de texto/gráficos/formas/imagens, vê prévia da timeline, altera cena/palavra/tempo isoladamente, faz render de quadros reproduzíveis, codifica vídeo via tecnologia avaliada (por exemplo FFmpeg) e exporta MP4 verificável com duração, fps, codec, áudio e sincronia corretos. Re-renderizar apenas dependências afetadas quando viável, sem enganar com vídeo de exemplo ou GIF. **Fronteira com CST-209:** esta tarefa integra a geração de cenas com autoria por IA, preview, refinamento manual e exportação via renderer já verificado em CST-209; não desenvolver encoder concorrente. Verificar que uma cena editada altera apenas frames dependentes e preserva os textos nas notas editoriais.

- [ ] **CST-172 — Integrar áudio, locução, sincronização e acessibilidade em vídeo.**  
  **Estado inicial:** Áudio/transcrição no Urbe é base parcial, produção audiovisual é nova.  
  **Dependências:** CST-058, CST-170, CST-210.
  **Critério de conclusão:** Narração humana/gerada opcional, áudio do vault, waveform, trilhas, fades, mixagem, legendas SRT/VTT embutidas ou separadas, descrição textual e volumes audíveis; áudio e cenas sincronizados, direitos/provedores/custos explícitos, sem geração presumida quando indisponível. Narração e legendas vinculadas a notas editoriais e timeline; usar o motor de áudio CST-210 sem gerar uma segunda fonte de roteiro em metadados ocultos.

- [ ] **CST-173 — Conectar provedores de imagens e vídeo generativo como ferramentas opcionais.**  
  **Estado inicial:** Geração de imagem tem caminho próprio; vídeo generativo externo não confirmado no Urbe.  
  **Dependências:** CST-072, CST-082, CST-132, CST-170, CST-182.  
  **Critério de conclusão:** Contratos para entrada/saída, modelo/custo/limites, consentimento de envio, formatos, polling/cancelamento e salvamento de mídia; distinguir animação programática de vídeo gerado por modelo, informar incertezas/direitos, tratar falha e indisponibilidade sem quebrar produção local. Não impor Runway como fornecedor exclusivo. Seleção por modalidade e custo conforme CST-220, com consentimento, fallback real e política de direitos. Vídeo programático offline/local e vídeo generativo remoto são pipelines diferentes; uma solicitação não envia nota privada por padrão.

- [ ] **CST-174 — Criar cursos e experiências educacionais como produtos, não simples concatenação de slides.**  
  **Estado inicial:** Novo formato guiado por eXeLearning e H5P.  
  **Dependências:** CST-150, CST-156, CST-161, CST-162, CST-163.  
  **Critério de conclusão:** Estrutura curso→módulo→aula→atividade, objetivos, avaliação, feedback, progresso e acessibilidade; exportação HTML interativo, com adaptadores SCORM/IMS/xAPI apenas após testes de conformidade/compatibilidade com LMS; fontes continuam notas reais e dados de progresso são separados de conteúdo autoral.

- [ ] **CST-175 — Oferecer distribuição/publicação de produto e seus pacotes de saída.**  
  **Estado inicial:** Fluxo unificado de entrega ausente.  
  **Dependências:** CST-162, CST-163; cada saída adicional depende de seu adaptador e validação.  
  **Critério de conclusão:** Escolher formato/canal, perfil, público, resolução, metadados e destino; validar pacote, nome, integridade e privacidade; exportar ao armazenamento local ou, por integração autorizada, publicação externa. Nada de hospedar/publicar sem consentimento ou criar dependência de servidor.

#### 4.12.4. Agentes, skills e revisão editorial

- [ ] **CST-176 — Criar ferramentas de Publicação acessíveis aos agentes e ao usuário, com os mesmos contratos.**  
  **Estado inicial:** Hoje agentes contam com `page_schema`/`write_page` ligados ao modelo legado; contrato novo inexistente.  
  **Dependências:** CST-072, CST-075, CST-150–CST-154, CST-158.  
  **Critério de conclusão:** APIs tipadas para localizar fontes, criar/abrir projeto, vincular material, montar composição, propor edição com diff, pedir render/preview, inspecionar e exportar; ferramentas antigas retiradas/renomeadas e tutoriais atualizados. Agente não tem rota de escrita que bypassa permissões e revisões. Operações de Publicações expostas a agentes reutilizam exatamente o serviço acionado pela UI, não `write_page`; incluem referência de fonte e revisão, proposta/preview, validação pós-efeito, erro recuperável e formato de resultado tipado.

- [ ] **CST-177 — Implementar skills de produção compatíveis com Agent Skills e carregamento sob demanda.**  
  **Estado inicial:** Recursos de plugins e instruções existentes, skill loader/repositório não comprovado.  
  **Dependências:** CST-072, CST-075, CST-076, CST-176.  
  **Critério de conclusão:** Suportar `SKILL.md` e metadados/recursos declarados com validação, catálogo, origem/licença, versão e instruções progressivas; skills para redação, livro, slide, motion, curso, imagem, QA e publicação; skills não concedem novas permissões nem executam scripts externos automaticamente; compatibilidade externa testada caso a caso. **Fronteira com CST-214:** este item implementa formato `SKILL.md`, descoberta e ativação progressiva para fluxos de Publicações; CST-214 governa catálogo geral, segurança, casos de teste, qualidade e regressão das skills em todo o Urbe. Sem scripts automáticos instalados junto com a skill.

- [ ] **CST-178 — Estabelecer fluxo criativo multiagente orientado ao público e à finalidade.**  
  **Estado inicial:** Multiagente existe parcialmente; aplicação a produção editorial nova.  
  **Dependências:** CST-078, CST-079, CST-090–CST-093, CST-151, CST-176, CST-177.  
  **Critério de conclusão:** Agente planeja briefing, público, objetivo, referências, narrativa, storyboard/outline, composição, revisão de fatos/design/acessibilidade e entrega; tarefas podem consultar especialistas autorizados, sem misturar memórias ou inventar fontes. Sem IA, usuário opera todo o fluxo. Decisões importantes continuam controláveis. **Fronteira com CST-217:** este item define somente o método editorial e handoffs do fluxo criativo; a coordenação durável genérica de especialistas e seus contratos é de CST-217. Não exigir múltiplos agentes para trabalhos que um agente faz melhor.

- [ ] **CST-179 — Inspeção visual e revisões automáticas após renderizar, não somente validação sintática.**  
  **Estado inicial:** Não há pipeline unificado comprovado de inspeção de artefatos.  
  **Dependências:** CST-164, CST-167, CST-169, CST-171, CST-176, CST-185, CST-213.
  **Critério de conclusão:** Verificar páginas, slides, quadros representativos e áudio reais: cortes, overflows, contraste, fontes, sumário, imagens, duração, frames negros, sincronia e legibilidade; corrigir em elementos de origem/composição mantendo vínculos, rerenderizar partes afetadas e registrar o que foi e não foi inspecionado. Sem declarar "ficou bom" sem evidência. **Fronteira com CST-213:** aqui ficam critérios visuais/editoriais por formato e a correção da composição; CST-213 define infraestrutura compartilhada de verificadores, recibos e estados de evidência. Ver imagem/render real e não somente markup.

- [ ] **CST-180 — Produzir relatórios de qualidade, erros e recuperação por produto.**  
  **Estado inicial:** Novo contrato de diagnóstico.  
  **Dependências:** CST-007, CST-154, CST-163, CST-179.  
  **Critério de conclusão:** Estado explícito rascunho/renderizando/validado/erro/parcial, logs sem material privado, reexecutar/retomar/cancelar e recuperar exportação anterior; confirmar arquivo real por leitura pós-escrita/assinatura do formato, não apenas mensagem ou diálogo. O estado de uma Publicação e do arquivo exportado deve ser derivado de recibos do verificador e da gravação física, independentemente de um modelo ter produzido uma resposta convincente.

- [ ] **CST-181 — Garantir cache derivado incremental, performance e limite de memória no Android.**  
  **Estado inicial:** Criação de vídeo e paginação exigem maior carga; medição pendente.  
  **Dependências:** CST-098, CST-114, CST-137, CST-153.  
  **Critério de conclusão:** Cache por hash de fonte/estilo/versão, invalidação seletiva, renderização em lote/streaming, prioridades de UI, cancelamento, retomada, indicador de espaço/tempo e execução offline onde suportado. Medir RAM/latência/tempo de exportação no moto g32 sem empobrecer qualidade silenciosamente. Medir atualização seletiva quando uma fonte usada em duas Publicações muda; evitar reindexar todo o vault e renderizar tudo por padrão; caching nunca transforma um snapshot derivado em conteúdo canônico.

- [ ] **CST-182 — Segurança de HTML/JS, skills, mídia externa e renderizadores.**  
  **Estado inicial:** Há viewer HTML isolado e políticas de ferramenta; novos executores ampliam superfície de risco.  
  **Dependências:** CST-072, CST-084, CST-151, CST-176, CST-177.  
  **Critério de conclusão:** Conteúdo do vault e da web tratado como dado não confiável; render HTML/CSS/JS isolado e sem ponte não autorizada ao vault; scripts e ferramentas externas com permissões explícitas, saneamento, limites técnicos reais, prevenção de SSRF/exfiltração, pacote de fontes confiável e sem executar assets hostis pelo simples ato de pré-visualizar.

- [ ] **CST-183 — Acessibilidade, localização e representação alternativa em cada formato.**  
  **Estado inicial:** Atributos existentes do editor são referência; formatos novos requerem auditoria própria.  
  **Dependências:** CST-105, CST-156, CST-161, CST-164–CST-174.  
  **Critério de conclusão:** Estrutura de leitura, headings, texto alternativo, contraste, legendas, idioma, ordem de foco, teclado/toque e fallback estático; WCAG e validações pertinentes aos formatos, com aviso onde a conversão não preservar interatividade ou semântica.

- [ ] **CST-184 — Proveniência, direitos, atribuição e versionamento de fontes/publicações.**  
  **Estado inicial:** Selos da IA e informações de fontes existem parcialmente; esquema editorial novo.  
  **Dependências:** CST-027–CST-035, CST-083, CST-151, CST-155.  
  **Critério de conclusão:** Distinguir fonte humana, texto transformado/gerado, imagens/voz e ativos externos; referências, licenças, modelos e atribuição; snapshot/manifest de build vinculado a hashes das notas e versão da publicação; marca discreta e preservação dos selos originais. Publicar é snapshot explícito, não uma cópia viva que edita remotamente o vault. A IA deve citar trecho/revisão de origem nos relatórios de pesquisa e no manifesto do produto; citações fabricadas ou antigas invalidam a verificação. Ativo gerado por modelo registra licença/provedor conforme disponível.

- [ ] **CST-185 — Definir qualidade/fidelidade/editalidade por formato e provas de exportação.**  
  **Estado inicial:** Nenhum formato entrega automaticamente todas as propriedades de HTML.  
  **Dependências:** CST-156, CST-159, CST-163.  
  **Critério de conclusão:** Matriz por formato (visual, editável, pesquisável, acessível, interativo, offline, tamanho e requisitos), perfil explícito, testes de abertura em aplicativos reais, comparações de render e indicação de limitações; evitar marcar PPTX rasterizado como editável ou PDF renderizado como livro completo sem índices. Critérios de editabilidade devem ser binários quando aplicáveis: elemento é manipulável no aplicativo de destino ou é imagem estática, sem rótulo enganoso. QA inclui abertura externa e vínculo da fonte original preservado no Urbe.

- [ ] **CST-186 — Implementar experiência móvel e offline proporcional aos recursos do aparelho.**  
  **Estado inicial:** Android prioritário; smartphone de referência possui recursos limitados para vídeo longo/3D.  
  **Dependências:** CST-061, CST-158, CST-181.  
  **Critério de conclusão:** Compor e editar notas offline, criar/inspecionar artefatos, escolher formatos suportados localmente, reportar necessidade de render externo/mais recursos antes de executar, evitar ANR, perda de rascunho e consumo excessivo de RAM. Não anunciar renderização local se a plataforma não a suporta. Na plataforma móvel, tarefas pesadas podem operar em executor adaptado ou host autorizado quando realmente necessário; UI não bloqueia, os estados permanecem recuperáveis e o app não instala uma IDE/engine de jogos por obrigação.

- [ ] **CST-187 — Executar pilotos completos de Publicação com fontes vinculadas.**  
  **Estado inicial:** Nenhum piloto novo foi executado nesta revisão.  
  **Dependências:** CST-151–CST-154, CST-158, CST-162, CST-164, CST-167, CST-170–CST-172, CST-176, CST-179, CST-185.  
  **Critério de conclusão:** Mesmo projeto real gera (a) publicação HTML, (b) PDF paginado, (c) slides e (d) motion graphics MP4, com notas atualizadas dentro e fora do estúdio, arquivo reaberto offline, duas Publicações usando a mesma nota, conflito recuperável e pacote final aberto/inspecionado. Os formatos adicionais têm seus próprios pilotos e testes; não bloquear toda a fundação por depender de todos os formatos. Usar também casos de investigação do vault e criação sem IA: os mesmos dados alimentam artigo, PDF, slides e MP4, com alteração de fonte entre produções e recuperação pós-interrupção comprovada.

- [ ] **CST-188 — Manter pesquisa técnica e feedbacks de comunidades como insumos auditáveis.**  
  **Estado inicial:** Primeira curadoria incluída nas seções 9/10; pesquisa contínua pendente.  
  **Dependências:** CST-001, CST-149, CST-185.  
  **Critério de conclusão:** Para cada motor/skill/adaptador, consultar documentação, código aberto, testes de referência, issues e discussões comunitárias; registrar hipótese, alternativa, suporte, licença, custo, maturidade, feedback favorável/desfavorável, decisão e prova em código do Urbe. Não tratar opinião individual como consenso nem reproduzir arquitetura de concorrente sem avaliar adequação. Registrar link de commit/arquivo específico quando examinado, data, licença, discussão (favorável e desfavorável), aplicabilidade ao Urbe, risco de pacote Android e tarefa CST de destino; citar contrapontos da comunidade sem inventar consenso.

- [ ] **CST-189 — Garantir ciclo de vida e versão estável da Publicação editável e das exportações imutáveis.**  
  **Estado inicial:** Contrato novo.  
  **Dependências:** CST-004–CST-006, CST-150–CST-155, CST-184.  
  **Critério de conclusão:** Publicação editável guarda vínculos e opções; exportação fica registrada como snapshot identificado com manifest de fontes/versões; reconciliação ao reabrir explica mudanças posteriores; não editar PDFs/MP4 distribuídos como se fossem notas vivas nem alegar sincronização com arquivos de terceiros.

- [ ] **CST-190 — Fazer avaliação de licenças, tamanho do pacote e runtime antes de incorporar motores.**  
  **Estado inicial:** Quarto/Pandoc, Paged.js, Vivliostyle, Motion Canvas, Remotion, FFmpeg e SDKs possuem requisitos distintos.  
  **Dependências:** CST-149, CST-188.  
  **Critério de conclusão:** Matriz de licença/distribuição comercial, disponibilidade Android/Windows, suporte .NET/C#, execução offline, memória, tamanho APK, manutenção, exportação de assets e dependências; preferência por bibliotecas compatíveis e adaptadores desacoplados. Referência estudada não é dependência aprovada.

- [ ] **CST-191 — Implementar adaptadores isolados e preservação de semântica nas saídas.**  
  **Estado inicial:** Motor único de exportação universal não comprovado; algumas rotas antigas estão vinculadas ao estúdio de páginas.  
  **Dependências:** CST-156, CST-163, CST-165–CST-175, CST-185.  
  **Critério de conclusão:** Cada destino informa capacidades, exige entradas tipadas, compõe sem perder referência, gera arquivos rastreáveis, oferece fallback e valida/abre resultado; uma nova saída não exige mudanças no editor de notas nem duplicar modelos de conteúdo.

- [ ] **CST-192 — Testar matrizes de fidelidade, segurança e regressão para produção multiformato.**  
  **Estado inicial:** Testes antigos de páginas/print são evidência para riscos, não aprovação automática do novo produto.  
  **Dependências:** CST-151–CST-156, CST-164–CST-186.  
  **Critério de conclusão:** TDD com fixtures (acentos/Unicode, fórmulas, tabelas, capítulos, assets ausentes, grandes volumes, mudança externa, fonte compartilhada, IA malcomportada, vídeos e legendas), golden files e snapshots visuais onde apropriado, testes em Android/Windows e entregas/release rastreáveis.


### 4.13. Lacunas reconciliadas com o FeaturesCheck (31/10/2026)

**Autoridade:** a comparação integral com o FeaturesCheck original identificou obrigações específicas que estavam ausentes, incompletas ou rastreadas pelo CST errado. Nenhum estado [x] histórico é confirmação de teste em dispositivo real; cada item a seguir permanece aberto até sua evidência.

- [ ] **CST-193 — Garantir confiabilidade de revisões e edições longas executadas por agentes.**

  **Estado inicial:** Relato detalhado no FeaturesCheck sobre revisão de templates de dicionário: falhas de correspondência exata, releituras e reaplicações redundantes, substituição integral com versão desatualizada, inconsistências criadas na tentativa de correção e contagens finais incorretas anunciadas como concluídas.
  **Frente:** Estabilização e integridade de dados.
  **Dependências:** CST-004, CST-005, CST-006, CST-007, CST-034, CST-079, CST-094, CST-098.
  **Critério de conclusão:** Em testes reproduzíveis com arquivos médios e grandes e atualizações concorrentes, toda leitura/escrita do agente verifica versão/base e resultado efetivo; falha em patch exige novo estado e novo diff, sem reusar texto obsoleto. Não permitir reescrita de arquivo inteiro sem objetivo e base verificáveis; validar contagens e propriedades pós-operação antes de declarar conclusão; registrar operações sem progresso e preservar recuperação. Incluir casos de perda parcial e cancelamento; comparar efetivamente o conteúdo final com os requisitos e não apenas número de chamadas. Adicionar teste de texto longo com 100+ notas, mudanças externas durante a leitura parcial, revisão de um trecho em várias notas e agente que retorna resumo superficial; a verificação deve confrontar bytes, trechos, objetivos e fontes.

- [ ] **CST-194 — Validar limites declarados, orientação, preservação e UX dos anexos multimodais.**

  **Estado inicial:** FeaturesCheck registra na beta.37 até 20 anexos por mensagem dentro de 30 MB e tratamento de fotos grandes ou horizontais. É relato de implementação pendente de teste físico, não promessa de limites invariáveis nem aceite automático.
  **Frente:** Estabilização e integração.
  **Dependências:** CST-007, CST-009, CST-014, CST-081, CST-122.
  **Critério de conclusão:** Verificar limites efetivos por modelo e plataforma, contagem e volume combinado, EXIF/rotação, redimensionamento sem imagem invertida/cortada, formato, anexos grandes, cancelamento, erro, retomada, preview/zoom, e preservação de origem e privacidade. A UI informa limites reais antes do envio e não apresenta sucesso quando mídia não foi entregue.

- [ ] **CST-195 — Executar regressão das 13 entregas adicionais registradas na beta.37.**

  **Estado inicial:** Todas constam [x] no FeaturesCheck, com observação explícita `Falta: conferir no aparelho`; não há confirmação automática desta revisão.
  **Frente:** Estabilização e validação de release.
  **Dependências:** CST-003, CST-036, CST-081, CST-122, CST-019, CST-024, CST-072, CST-073, CST-076, CST-084, CST-097, CST-105, CST-194, CST-196, CST-197.
  **Critério de conclusão:** No aparelho, verificar separadamente: (1) compartilhar nota/arquivo, (2) anexos 20/30 MB e fotos, (3) rascunhos/anexos após morte do processo, (4) personalização por toque da barra, (5) ícone de tag, (6) link Markdown após mover/renomear, (7) permissões/delegação/`AGENTS.md`, (8) precedência das instruções, (9) agente de modelo com ferramentas preservadas/exclusão de memórias privadas, (10) busca sem exfiltração e URL válida de nota, (11) logs privados com retenção de 30 dias, (12) diff completo, filtragem de todas as tags e Abrir ao lado, (13) alvos de toque >= 40 px e contraste WCAG AA. Cada caso tem evidência e resultado próprio, sem um único [x] apagar lacunas parciais.

- [ ] **CST-196 — Preservar links Markdown comuns após mover e renomear arquivos.**

  **Estado inicial:** Implementação relatada na beta.37 sob o rótulo CST-101, que originalmente cobre testes combinados, sem um critério dedicado para essa regra.
  **Frente:** Conclusão e integração.
  **Dependências:** CST-004, CST-005, CST-038, CST-040, CST-101, CST-120.
  **Critério de conclusão:** Links `[texto](Nota.md)` e destinos com espaços, acentos, fragmentos, caminhos relativos e nomes ambíguos continuam resolvendo corretamente após renomear/mover, com atualização/reparação segura e sem sobrescrever mudanças externas. Testar inclusive múltiplas notas citantes, backups e links inválidos, distinguindo link comum de transclusão.

- [ ] **CST-197 — Validar filtros completos de tags e abertura lateral contextual sem limitar resultados.**

  **Estado inicial:** FeaturesCheck relata beta.37 com `qualquer tag filtra a Área de trabalho` e `Abrir ao lado alcança qualquer nota`, mas a rastreabilidade anterior associava a primeira parte a itens genéricos de Explorador sem critério específico.
  **Frente:** Conclusão e integração.
  **Dependências:** CST-036, CST-037, CST-042, CST-066, CST-099, CST-101, CST-105.
  **Critério de conclusão:** Todas as tags disponíveis, inclusive além da seleção visível inicialmente e com herança por projeto, podem ser escolhidas e filtram resultados corretos em vault grande; Abrir ao lado encontra e abre qualquer nota permitida, preservando origem, foco, histórico e suporte a tela móvel. Não truncar listas silenciosamente por limites artificiais nem confundir tag de busca com cor/estilo.

### 4.14. IA nativa de ponta — programação, matemática, execução e multimodalidade (revisão 31/10/2026)

**Natureza:** novos resultados verificáveis, abertos até evidência de execução. Não substituem nem duplicam os CST-149–CST-192 (Publicações): adicionam a **infraestrutura computacional transversal** que permite aos agentes produzir soluções que uma ferramenta fixa não antecipou. O estudo 2.3 inclui referências específicas e decisões na seção 14. Em todos os itens, C# e Android/Windows são a referência de produto, com adaptação por host e sem executar código hostil dentro da UI.

- [ ] **CST-198 — Estabelecer matriz de paridade competitiva da IA do Urbe.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Investigação e arquitetura.  
  **Dependências:** CST-001, CST-075, CST-114.  
  **Critério de conclusão:** Auditar o código dos candidatos Claude Code (somente interface/documentação pública), Codex, OpenCode, Qwen Code, Cline, Goose, OpenHands, Aider e Gemini CLI; confrontar uso de código, execução, shell, edição, testes, arquivos, multimodalidade, memória, tools, skills, contexto, MCP, segurança, custos e UX com a main do Urbe e Ecosystem AI. Para cada capacidade registrar implementação comprovada, ausência, risco, código-fonte específico, licença e benchmark reproduzível; não confundir estrelas com qualidade. A matriz competitiva tem como eixo os *fluxos reais do Urbe* (investigar vault, editar, capturar, formulário, plugin, agentes, Publicações e produção), e não uma lista de comandos de terminal. Separar capacidades de produto concorrente das acessíveis via API/modelo. Documentar o que cada código estudado resolve, não copiar UX por semelhança.

- [ ] **CST-199 — Definir convergência do runtime de agentes do Urbe com Ecosystem AI.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Arquitetura central.  
  **Dependências:** CST-072, CST-098, CST-193, CST-198.
  **Critério de conclusão:** Comparar AssistantAgent, AssistantTools, AssistantStore e AgentRuntime.Core; produzir matriz de equivalência, fronteiras de extração NN-022, adaptadores e testes. Reutilizar log de eventos/retomada, ledger, scopes e verificação quando houver prova de compatibilidade; proibir terceiro runtime e migração big bang ou dependência cruzada entre Products. Resolver explicitamente a tensão com seção 7: comparar funcionalidades e testes, fixar contrato de porta/adapter e ordem de extração por NN-022 antes de migrar; preservar ferramentas especializadas do Urbe e garantias de ledger/replay do Ecosystem AI, sem dois sistemas concorrentes de tarefa. A extração posterior não exige que o Urbe recrie runtime já validado.

- [ ] **CST-200 — Criar registry tipado e compartilhável de capacidades computacionais.**  

  **Estado inicial:** Contrato compartilhável proposto; confirmar e reutilizar primitives existentes (`AiTool`, `IPluginApi`, `AgentRuntime.Tools`) antes de construir. Não foi demonstrada paridade dessas APIs no Urbe.
  **Frente:** Arquitetura central.  
  **Dependências:** CST-072, CST-151, CST-199.
  **Critério de conclusão:** Todo recurso para usuário, plugin, publicação ou agente tem ID/versão, input/output schema, descoberta/descrição, perfis de risco, permissões, escopos, prévia, efeitos, orçamento, cancelamento, recibos, telemetria e testes de contrato. Ferramentas novas não ignoram autoria nem proteção de vault; descoberta não garante autorização. O registry é **mecanismo** de autorização/descoberta; CST-224 define as operações semânticas do editor que nele serão registradas. UI, agente e plugin usam o mesmo handler/serviço e recebem mesmo diff/receipt, inclusive transações, revisão do arquivo e revogação. Skills não são novas autoridades de execução.

- [ ] **CST-201 — Oferecer execução de código isolada e segura por plataforma.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Capacidade obrigatória com gate de segurança.  
  **Dependências:** CST-004, CST-005, CST-072, CST-084, CST-114, CST-200.  
  **Critério de conclusão:** Implementar protótipos Android/Windows de worker/interpretador/host isolado para executar código proposto; permitir somente filesystem de trabalho e capabilities concedidas, rede negada por padrão, quotas e interrupção, sem ponte para configurações/credenciais/vault canônico. Verificar ciclo de vida, crash do executor, tempo, memória, permissões e logs. Nunca rodar scripts não confiáveis dentro da UI/mesmo processo por conveniência; não alegar suporte a compilação/JIT/NativeAOT sem testes reais por arquitetura. Executor deve ser uma capacidade opcional de tarefas avançadas, não um requisito do usuário nem da UX principal; calculadora, editor, PDF e grafos operam sem scripting arbitrário. Provar que isolamento Android não dá acesso direto a vault/chaves/WebView privilegiado.

- [ ] **CST-202 — Implementar Code Mode para composição de ferramentas.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Capacidade computacional.  
  **Dependências:** CST-078, CST-098, CST-200, CST-201.  
  **Critério de conclusão:** O agente pode produzir uma rotina auditável que combina cálculos e ferramentas permitidas com resultados tipados, manipula conjuntos e retorna somente evidência relevante; aplicar a mesma política para cada chamada interna, com orçamento e cancelamento; proteger contra loops, execução de APIs não registradas e exfiltração. Confrontar Qwen Code code-mode e OpenCode; comparar custo/precisão com chamadas individuais. Usar código como *composição autorizada de ferramentas* quando a API fixa não cobre a tarefa, nunca acesso irrestrito ao host; avaliar contexto e latência com e sem Code Mode em tarefas do vault, preservando identidade, autorização e proveniência.

- [ ] **CST-203 — Transformar edição de código em trabalho sobre projetos reais.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Programação e produto.  
  **Dependências:** CST-005, CST-141, CST-193, CST-200, CST-201.  
  **Critério de conclusão:** Agentes podem ler estrutura, localizar símbolos, pesquisar arquivos, editar projetos multiarquivo e gerar código/recursos, respeitando projetos C# genéricos além de especialização Lunet; integrar linguagem, análise semântica onde disponível e diffs com rollback. Não exigir que usuário conheça CLI/IDE; nunca alterar código fonte sem escopo de projeto e autorização aplicável. Trabalhar no projeto que o usuário escolheu, inclusive C# genérico sem Lunet, com estado editável preservado no vault/workspace e abertura contextual. Linguagem/IDE não se tornam pré-requisito para solicitar uma funcionalidade pronta.

- [ ] **CST-204 — Adicionar build, testes, execução e depuração verificáveis.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Programação e produto.  
  **Dependências:** CST-003, CST-114, CST-201, CST-203.  
  **Critério de conclusão:** Em ambiente compatível, agente executa formatador/linter, build, testes, análise de erro, smoke e investigação de logs. Para Android, estudar toolchains reais, armazenamento, permissões e impossibilidade de presumir build local equivalente ao desktop; disponibilizar host remoto opcional apenas autorizado. Não declarar programa funcional só porque o código foi escrito. Critério de aceite por resultado concreto (executou o teste? gerou binário? artefato abriu?), com logs do compilador, versão de ferramentas e limitação Android; build remoto apenas autorizado, não silencioso.

- [ ] **CST-205 — Implementar edição transacional multiarquivo e checkpoints.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Confiabilidade.  
  **Dependências:** CST-004, CST-005, CST-006, CST-034, CST-193, CST-199, CST-203.  
  **Critério de conclusão:** Produção em workspace isolado, diff contextual, verificação do estado base por hash/revisão, aplicação atômica ou compensação rastreável, snapshot, rollback e tratamento de conflito; separar origem canônica, mídia e outputs derivados. Retomar trabalho sem duplicar escrita e sem sobrescrever nota modificada por usuário/terceiro. Distinguir **proposta isolada/overlay** (antes da autorização) de **edição confirmada da fonte real** (depois da revisão); aplicações efetivas usam o mesmo gravador canônico de notas/projetos da UI. Não criar cópias "sincronizadas" no vault sob o pretexto de checkpoint.

- [ ] **CST-206 — Consolidar motor matemático simbólico, numérico e científico.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Computação fundamental.  
  **Dependências:** CST-026, CST-069, CST-150, CST-200.
  **Critério de conclusão:** Disponibilizar expressões, equações, álgebra simbólica viável, funções, unidades, matrizes, estatística, integração/derivação quando suportadas, simuladores numéricos e gráficos interativos; comparar AngouriMath, Math.NET Numerics, CSharpMath e bibliotecas existentes. Precisão, domínio, unidade e limitação verificáveis; cálculo seguro e simples não depende de script arbitrário. Definir API numérica determinística disponível sem sandbox de código geral para cálculos típicos; integrar fórmula do editor, dados do vault, grafos/visualizações e uso em Publicações sem duplicar valores. Comparar precisão, unidades e problemas-limite com biblioteca de referência.

- [ ] **CST-207 — Criar pipeline programável de dados, arquivos e planilhas.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Computação fundamental.  
  **Dependências:** CST-064, CST-150, CST-200.
  **Critério de conclusão:** Importar, pesquisar, transformar e exportar dados tipados (CSV, JSON, tabelas e formatos suportados), com transformações reproduzíveis, validação de esquema, fórmulas, proveniência, tratamento de arquivos grandes em streaming e resultados editáveis quando o formato oferecer; comparação documentada com habilidades de análise de dados de agentes de ponta. Pipeline recebe notas/tabelas reais, mantém referência da coluna/linha/fonte, usa edições tipadas para persistir resultados aprovados e cria artefatos derivados com proveniência; a skill XLSX é evidência das falhas comuns (fórmulas, formatos, validação), não obrigação de usar Python.

- [ ] **CST-208 — Adicionar produção visual programática para SVG, Canvas e gráficos.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Mídia e computação.  
  **Dependências:** CST-082, CST-156, CST-169, CST-200, CST-206.
  **Critério de conclusão:** Permitir ao agente criar gráficos, infográficos, diagramas, imagens vetoriais e animações 2D por código/descrição estruturada, editáveis e determinísticas; prévia fiel, verificação de clipping, tipografia, escala, contraste, sem forçar modelo externo de imagem. Render host isolado e sem ligação irrestrita ao vault. Tornar gráfico/diagrama editável no formato canônico e proveniente de dados ou notas, com inspeção de preview; vetorização, plotagem e design usam o renderer da Publicação onde cabível, não outro editor visual paralelo.

- [ ] **CST-209 — Viabilizar renderizador de vídeo por código e MP4 real com cancelamento.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Mídia e computação.  
  **Dependências:** CST-170, CST-181, CST-190, CST-200, CST-208.
  **Critério de conclusão:** Estudar Motion Canvas, Manim, Remotion e codecs; entregar execução de timeline, cálculo de quadros, caching seletivo, áudio/legenda/sincronia, render de quadros e encoder MP4 confiável por plataforma. No Android avaliar MediaCodec/MediaMuxer e desempenho físico; em Windows adaptador apropriado. Um pedido de vídeo curto deve gerar arquivo reproduzível e verificável, não apenas HTML, storyboard ou prompt. **Fronteira com CST-171:** este item entrega engine de render temporal, encoder, cancelamento, progress/retomada e provas de arquivo por host; CST-171 consome isso na UX e no fluxo de Publicações. Vídeo remoto opcional pertence a CST-173/220. Não exigir runtime JS de Motion Canvas/Remotion no APK sem avaliação comprovada.

- [ ] **CST-210 — Transformar arquivos de áudio em recursos de produção e análise.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Mídia e computação.  
  **Dependências:** CST-058, CST-170, CST-200.
  **Critério de conclusão:** Ingestão, transcrição com timestamps, limpeza, trilhas, mixagem, edição, TTS/locução opcional, onda/legenda sincronizadas e exportação; política de modelos externos, licença de vozes e privacidade; sem alegar áudio gerado quando houve apenas descrição. Motor de áudio é capability reutilizável por gravações, notas e Publicações, não um sistema separado de roteiros; manter arquivo-fonte, licença de voz e edição textual via nota editorial.

- [ ] **CST-211 — Adicionar automação e testes de experiências web isoladas.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Ferramentas generalistas.  
  **Dependências:** CST-141, CST-182, CST-200, CST-201.  
  **Critério de conclusão:** Criar/abrir artefatos HTML, executar prévia limitada, inspecionar DOM quando disponível, capturar screenshots, validar interações e erros, sem usar WebView como UI geral do Urbe e sem conceder JavaScript acesso privilegiado ao vault; não pressupor Playwright/Chromium inteiro embarcado no Android. A tool de automação serve para verificar o artefato HTML/experiência criada em Publicações; adaptar execução por host e não instalar Chromium/Playwright no aparelho sem evidência de custo/viabilidade.

- [ ] **CST-212 — Consolidar ingestão multimodal e extração confiável.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Conhecimento e ferramentas.  
  **Dependências:** CST-064, CST-081, CST-123, CST-200.  
  **Critério de conclusão:** Entender PDF, imagens, áudio, vídeo, gráficos, anexos e texto em modos compatíveis, com trechos exatos, tabelas, timestamps, referências e indicação de leitura parcial; modelos que não recebem mídia dependem de extratores reais, não de contexto fictício. Extração inclui orientação EXIF, OCR/documento, vídeo com timestamps e tabela preservada, sempre com origem + offset + estado parcial; usar segmentos autorizados em recuperação semântica e não promover texto extraído a instrução.

- [ ] **CST-213 — Introduzir inspeção visual, testes de formato e QA independente.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Qualidade real.  
  **Dependências:** CST-079, CST-185, CST-200, CST-208.
  **Critério de conclusão:** Executar verificadores específicos e visualizar arquivos realmente renderizados; detectar texto cortado, overflow, fontes ausentes, pixels vazios, invalidade OOXML/PDF/MP4, áudio fora de sincronia, citações erradas e falhas de links; reportar estados verificado/parcial/não verificado com evidências. Revisão do próprio LLM não substitui QA. **Fronteira com CST-179:** infraestrutura de verificação por tipo/host, recibo, evidência e política de conclusão é compartilhável por todas as áreas (inclusive código, grafo e tabela); CST-179 contém a inspeção e revisão editorial específica de Publicações.

- [ ] **CST-214 — Tornar skills um sistema compatível, descobrível e mensurável.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Especialização.  
  **Dependências:** CST-075, CST-177, CST-200.
  **Critério de conclusão:** Importar/validar SKILL.md, carregar sob demanda por assunto, tipo de tarefa, arquivo e capacidade; conferir licença, versão, dependências e compatibilidade; exemplos pptx/pdf/webapp-testing/frontend-design/skill-creator devem revelar ferramentas faltantes e testes. Executar scripts somente no executor isolado e com consentimentos exigidos; medir impacto real por A/B. **Fronteira com CST-177:** mede seleção de skill por intenção/arquivos/capabilities (Qwen Code), qualidade com e sem skill (Skill Creator), erros de parse, licenças e recursos necessários. Uma skill com scripts não dispensa executor nem ganha permissão; se é requisito recorrente e básico, promover o recurso a ferramenta nativa.

- [ ] **CST-215 — Avaliar e implementar cliente MCP via contratos C#.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Interoperabilidade.  
  **Dependências:** CST-072, CST-084, CST-154, CST-200, CST-214.  
  **Critério de conclusão:** Estudar SDK oficial ModelContextProtocol/csharp-sdk, ferramentas, recursos e prompts com descoberta sob demanda; grants por ferramenta/servidor, limites, cancelamento, validação de schemas, revogação e origem não confiável; não expor arquivos, rede ou credenciais pela mera instalação de servidor. MCP integra capacidades externas opcionais; não pode virar substituto compulsório de ferramentas locais nem segundo catálogo de permissões. Validar interoperabilidade com SDK C# real e desconexão/timeout/revogação sem afetar a escrita do vault.

- [ ] **CST-216 — Persistir estado operacional e retomar execuções com idempotência.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Confiabilidade fundamental.  
  **Dependências:** CST-006, CST-012, CST-013, CST-199, CST-205.  
  **Critério de conclusão:** Persistir task ID, passos, versão de entradas, tool call IDs, resultados, decisões, tentativas e recibos; reinício do Android não duplica gastos, imagens, escritas nem geração de vídeo, e solicita intervenção se estado externo não puder ser verificado. Diferenciar conversa retomada de trabalho retomado. Persistir objetivo, escopo, fontes/revisões, resultado esperado, estado do run, aprovações e efeitos realizados em log reconstruível. Recuperar execução após morte do processo é diferente de reabrir chat e diferente de continuar execução em segundo plano (ver CST-013/225).

- [ ] **CST-217 — Implementar orquestração de subagentes com artefatos e escopos.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Agentes e autonomia.  
  **Dependências:** CST-072, CST-091, CST-092, CST-093, CST-146, CST-199, CST-200, CST-216.  
  **Critério de conclusão:** Coordenar investigador, programador, produtor, revisor e especialistas com mensagens estruturadas, limites de contexto, autonomia proporcional e publicação de evidências no workspace. Revisão independente deve inspecionar artefatos reais; evitar conversas paralelas sem resultado ou acesso indevido às memórias privadas. **Fronteira com CST-178:** infraestrutura genérica de coordenação/handoff, evidência e escopo; não criar múltiplos agentes por tarefa simples. Garantir que um especialista possa retornar artefato e justificativa verificáveis sem acesso ao contexto privado do outro.

- [ ] **CST-218 — Construir avaliação contínua competitiva e científica da IA.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Qualidade de produto.  
  **Dependências:** CST-003, CST-095, CST-096, CST-114, CST-137, CST-198, CST-213.  
  **Critério de conclusão:** Comparar o Urbe e candidatos com conjunto de tarefas representativas: escrita complexa, código multiarquivo, build, dados, matemática, recuperação semântica, HTML, imagem, vídeo, PDF, slides, habilidades de agente e recuperação de falha. Controlar modelo, prompt, hardware, custo, latência, tokens, qualidade humana, segurança, falhas e sucesso testado; não usar estrelas do GitHub como benchmark. Três provas transversais mínimas obrigatórias: (A) investigar 100 notas, detectar contradições e suportar edição concorrente; (B) mesma fonte gerar HTML/PDF/PPTX/MP4, editar nota no estúdio e revalidar dependências; (C) plugin Próxima tarefa chama agente, app reinicia e execução retoma sem efeitos duplicados. Comparar mesma configuração de modelo e hardware quando medindo ganho de sistema. Documentar resultados negativos e limites reais.

- [ ] **CST-219 — Corrigir e reforçar permissões, aprovações e cancelamento end-to-end.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Segurança e confiabilidade.  
  **Dependências:** CST-072, CST-073, CST-082, CST-084, CST-113, CST-199, CST-200.  
  **Critério de conclusão:** Validar que hosts sem aprovador falhem fechados quando aprovação é necessária, investigar `AiWebConfig.Allowed(agentId) => Enabled` versus políticas por agente, propagar CancellationToken para imagens e efeitos longa duração, revisar permissões durante a execução, proteger segredos do modelo e scripts, e medir efeitos parciais/custos em cancelamentos. Não tratar suspeita como exploração provada. Inclua validação do fallback atual `AiAgent` que aprova quando não há callback, uso de `Allowed(agentId) => Enabled` em web, cancelamento `DrawAsync(..., CancellationToken.None)` e permissões reais em consultas de especialistas; tratar como sinais de inspeção estática a reproduzir, não como exploração já provada.

- [ ] **CST-220 — Adicionar seleção inteligente de modelos por modalidade e capability.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Multimodalidade e custo.  
  **Dependências:** CST-071, CST-082, CST-116, CST-117, CST-132, CST-200.  
  **Critério de conclusão:** Catálogo tipado por modelo/provedor para texto, ferramentas, visão, OCR, imagem, vídeo, voz, contexto, preço, cache, lote e streaming; selecionar conforme tarefa e consentimento, identificar fallback, modalidade ausente e gasto real/estimado; manter IA local para operações compatíveis sem inventar capacidade multimodal do modelo escolhido. Escolha de modelo deve ser invisível quando possível mas transparente em custo/privacidade: informar se vídeo usa renderer programático ou modelo generativo externo, e se o aparelho precisa de host externo antes do gasto.

- [ ] **CST-221 — Aplicar programação à criação de experiências interativas e aplicativos.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Produção generalista.  
  **Dependências:** CST-141, CST-148, CST-150, CST-154, CST-200, CST-203, CST-206, CST-211.  
  **Critério de conclusão:** Capacitar agentes a criar calculadoras, simuladores, dashboards, ferramentas, microsites e apps usando linguagens e APIs disponíveis, com controle de estado, testes, dependências e exportação; preservar fonte de verdade no vault, sem impor HTML a alvos nativos ou exigir editor Lunet. Artefatos de código e aplicações devem ser produzidos no contexto do trabalho, associados ao projeto real e verificáveis, sem transformar o Urbe em ferramenta exclusiva do Lunet ou em IDE obrigatória.

- [ ] **CST-222 — Validar a oficina computacional completa em Android e Windows.**  

  **Estado inicial:** Nova capacidade ou lacuna técnica identificada em auditoria estática/comparação externa; funcionalidade final não validada.  
  **Frente:** Aceite competitivo.  
  **Dependências:** CST-114, CST-137, CST-181, CST-186, CST-201, CST-204, CST-209, CST-213, CST-218.  
  **Critério de conclusão:** No moto g32 e Windows real, comprovar criar código, executar tarefa isolada, transformar tabela, simular fórmula, gerar SVG, produzir MP4 com áudio, pré-visualizar, cancelar, reiniciar, corrigir visual e exportar artefatos reproduzíveis. Registrar suporte real, consumo, memória, armazenamento, temperatura, queda por codec e limitações; não marcar pronto apenas por teste headless. Acrescentar fluxo de usuário não programador no editor: solicitação contextual -> operação do agente -> prévia/edição direta -> verificação -> produto; testes de aceitação devem comparar simplicidade, não apenas sucesso técnico.

### 4.15. Lacunas de IA de produto identificadas na auditoria integrada 2.4 — sem sobrepor 4.8, 4.12 ou 4.14

**Critério de inclusão:** as quatro entregas abaixo ainda não têm um checkbox com seu resultado observável completo. São contratos nativos que **ligam** as tarefas existentes, não outro estúdio, chat, motor gráfico ou runtime. Todas permanecem [ ] até implementadas e validadas. A matriz de rastreabilidade da seção 15 define a relação com os requisitos anteriores.

- [ ] **CST-223 — Recuperar e justificar conhecimento do vault com evidência de trecho, relações e revisão.**  
  **Estado inicial:** `KnowledgeIndex.Search` usa títulos/caminhos e índice de tokens; `AssistantMemoryEngine.Search` usa termos, aliases e metadados. Não foi encontrado índice híbrido vetorial/semântico geral integrado ao vault nem contratos de citação de trecho e revisão. Isso não prova que todo problema de busca exija embeddings.  
  **Frente:** Inteligência nativa sobre conhecimento.  
  **Dependências:** CST-004, CST-005, CST-083, CST-087, CST-120, CST-151.  
  **Critério de conclusão:** Criar consulta tipada de notas/trechos/propriedades/grafos/ativos por identidade e revisão, combinando recuperação lexical, títulos, vínculos e metadados; avaliar embeddings locais/serviço híbrido **somente se melhorarem o benchmark**. Citações apontam trecho e versão reais; distinguir relação explícita, inferência e informação contraditória/ausente; revalidar após renomeação/edição externa. Benchmark reproduzível de cem notas com sinônimos, questões multietapa, contradição, abstenção, privacidade e atualização do índice; métricas precision/recall e groundedness por consulta. Recuperar não dá permissão de modificar a fonte. Extende CST-075, CST-087, CST-089, CST-134, CST-160 e CST-212 sem substituí-los.

- [ ] **CST-224 — Oferecer operações semânticas de edição ao usuário, agentes e plugins pelo mesmo serviço do editor.**  
  **Estado inicial:** IA edita notas sobretudo por substituir trechos textuais (`edit_note` com `old_text/new_text`) ou reescrever o arquivo (`write_note`); há operações específicas como `edit_graph`, mas não foi demonstrado contrato transversal de edição de blocos/propriedades/tabelas/formulários/editor.  
  **Frente:** Editabilidade nativa e autoridade dos dados.  
  **Dependências:** CST-004, CST-005, CST-015, CST-018, CST-034, CST-072, CST-119, CST-151, CST-200.  
  **Critério de conclusão:** Operações tipadas por `sourceId`, `componentId`, revisão-base, `op`, argumentos e escopo: inserir/alterar/mover bloco, preencher campo/ficha, modificar propriedade/tabela, editar grafo e apontar origem de conteúdo. O mesmo serviço atende toque no editor, agente e plugin, gera diff/prévia, confirma versão, protege selo de IA, undo/redo, conflito e recuperação. Operações sem suporte retornam erro explícito, sem sobrescrever a nota inteira. Testar alteração concorrente, divisão/fusão de blocos, 100+ notas, PDF/Publicação que usa o mesmo trecho e Android com teclado aberto. Não inventar IDs permanentes para formatos que não os suportam sem migração/teste.

- [ ] **CST-225 — Dar às tarefas de IA uma experiência contextual única e progressiva no produto.**  
  **Estado inicial:** `AssistantPanel`, miniassistente, chat, planos e aprovações já existem; não foi comprovada uma única experiência de trabalho durável que acompanha a mesma operação no editor, Explorer, grafo, plugin e Publicações.  
  **Frente:** UX e autonomia orientadas a resultados.  
  **Dependências:** CST-008, CST-012, CST-013, CST-014, CST-080, CST-105, CST-146, CST-158, CST-200, CST-216.  
  **Critério de conclusão:** A pessoa inicia pelo contexto selecionado sem aprender CLI ou abrir chat obrigatório, vê objetivo/escopo, fontes, sugestões, estado atual, aprovação crítica, evidência e resultado; pode editar manualmente a proposta, pausar/cancelar, navegar e voltar. Diferenciar conversa, plano, run e artefato; execução é retomável segundo CST-216, sem duplicar perguntas, mudanças ou custos. Fluxo de uma nota e plugin Próxima tarefa funciona no Android touch-first, sem painéis redundantes, perdas de foco/IME ou inundação de logs; acesso à história detalhada é progressivo. Não criar novo assistente concorrente.

- [ ] **CST-226 — Rastrear impacto de mudanças entre conhecimento, plugins, agentes e Publicações sem cópias canônicas.**  
  **Estado inicial:** `KnowledgeIndex` e grafos referenciam notas; CST-153 prevê invalidação incremental de Publicações, mas ainda não há prova de um contrato transversal de impacto que alcance consultas, evidências, tarefas e produtos dependentes da mesma fonte.  
  **Frente:** Continuidade de conhecimento e qualidade.  
  **Dependências:** CST-005, CST-038, CST-120, CST-151, CST-153, CST-184, CST-223.  
  **Critério de conclusão:** Resolver dependências tipadas entre nota/trecho, consulta, conclusão de agente, grafo, formulário, plugin e artefato/publicação; alterações e renomeações atualizam identidade e marcam somente resultados afetados como desatualizados, exigindo conferência antes de reutilizar evidência. Não alterar automaticamente as conclusões humanas nem renderizar tudo de novo; derivados são caches/snapshots, jamais fontes paralelas. Mostrar impacto compreensível ao usuário, permitir atualizar seletivamente e comparar versões. Validar com uma nota compartilhada por duas Publicações e um plugin, editada durante uma investigação, inclusive rollback e offline.

### 4.16. Progresso nativo nas notas — decisão do proprietário de 01/11/2026

**Escopo aprovado:** complementar checkboxes com medição visível do avanço, não substituí-los. São duas entregas distintas para facilitar implementação e aceite. Usar UI nativa C#/Avalonia; **não** exigir plugin, HTML, JavaScript, WebView, publicação ou IA. A aparência é secundária à exatidão da contagem, à persistência e à qualidade do editor. Os dois itens permanecem **[ ]** até validação física e funcional.

- [ ] **CST-227 — Criar barras de progresso nativas, editáveis e persistentes nas notas.**  
  **Estado inicial:** Nova necessidade explícita do proprietário; há checkboxes, formulários e prévias HTML, mas não foi comprovado componente nativo geral de progresso em notas na inspeção estática anterior. Não afirmar ausência absoluta sem conferir a versão/branch vigente antes de implementar.  
  **Frente:** Editor de notas — componente nativo de dados e visualização.  
  **Dependências:** CST-004, CST-005, CST-015, CST-018, CST-119; integração com CST-064 e CST-224, sem introduzir fluxo de edição paralelo.  
  **Critério de conclusão:** Inserir, configurar, editar, mover, copiar, remover, desfazer e refazer **uma barra de progresso dentro de qualquer nota** nos modos pertinentes. A barra aceita **valor atual e total/meta**, mostra valor absoluto, percentual e restante (ex.: 54/224 páginas, 24,1%, faltam 170), com opção de alterar os números por controles nativos e vínculo a propriedades numéricas existentes. Validar valores vazios, não numéricos, negativos, total zero e excedente: apresentar estado explícito ou erro útil; não exibir falsa conclusão nem gravar dados incoerentes. O estado sobrevive a salvar, fechar, reabrir, reiniciar, mudanças externas e recuperação do vault, com controle de versão/conflitos e sem duplicar fontes de verdade. A representação Markdown é documentada, legível em texto puro e não destrutiva em outros editores; renderizar nativamente no Visual e preservar o conteúdo no Fonte. Integrar com templates/formulários e o mesmo serviço de dados consumível por usuário, agente e plugin, respeitando permissões e autoria; em Publicações, fornecer leitura fiel e fallback acessível para formatos estáticos. UI minimalista, teclado/toque, leitores de tela, rótulos numéricos além de cor, temas e responsividade em Android e Windows. Validar desempenho em nota longa no moto g32, com testes automáticos e aceite físico.

- [ ] **CST-228 — Vincular barras de progresso nativas a checklists e calcular automaticamente quanto falta.**  
  **Estado inicial:** Necessidade explícita: os checkboxes atuais identificam tarefas concluídas/pendentes, mas não oferecem um resumo nativo, persistente e atualizado de conclusão; exigência nova, não bug automaticamente confirmado.  
  **Frente:** Editor e conhecimento — indicadores derivados do conteúdo canônico.  
  **Dependências:** CST-227, CST-004, CST-005, CST-018, CST-038, CST-064, CST-119; reutilizar alterações semânticas CST-224 e referências/invalidação CST-226 quando disponíveis, sem exigir implementação duplicada.  
  **Critério de conclusão:** Vincular o componente a um **escopo explícito**, começando por checklist, seção ou nota; permitir referência declarada a outra nota/lista, preservando a identidade de origem e informando vínculos quebrados. Contar **itens concluídos / itens de checklist pertinentes**, exibir percentual e **quantidade restante** (ex.: 3/8, 37,5%, faltam 5), sem tratar texto comum, checkboxes literais em código ou listas fora do escopo como tarefas. Definir a regra para subtarefas e evitar dupla contagem; se o total for zero, exibir "sem tarefas", nunca 100% fictícios. Marcar/desmarcar, adicionar/remover, dividir/mover itens, renomear notas, editar externamente, desfazer/refazer e reabrir recalcula o indicador sem exigir atualização manual; mudanças da barra **não marcam/desmarcam tarefas por conta própria**. O total exibido é derivado dos checkboxes reais e não uma segunda cópia editável do resultado. Tocar no resumo permite localizar as tarefas pendentes sem perder o contexto de edição. Agentes, plugins e Publicações leem o mesmo resultado e atualizam a fonte por operações autorizadas; prevenir loops, referências circulares e recomputação desnecessária. Demonstrar 0/0, 0/8, 3/8, 8/8, lista aninhada, itens excluídos do escopo, nota vinculada renomeada e duas visões do mesmo conjunto; testar persistência, acessibilidade e resposta ao toque em Android real e Windows, inclusive listas longas.

### 4.17. Navegação com abas deliberadas

- [ ] **CST-229 — Reutilizar uma aba por padrão e oferecer abertura explícita de abas adicionais.**

  **Estado observado:** implementação em verificação para beta.39; não encerrado por existência de código.
  **Frente:** editor e UX.
  **Dependências:** salvamento e recuperação do editor (CST-004–007/144); sessão local e seleção de notas existentes.
  **Critério de conclusão:** Explorer, busca e links reutilizam a aba ativa; **+** acessível no topo escolhe uma nota e abre outra aba sem duplicar a já aberta. Cancelar não adiciona aba, inclusive reabrindo o seletor rapidamente. Troca/fechamento não descartam rascunhos; histórico Voltar/Avançar não vira abas. Fechar abas é persistido; sessões antigas sem marcador não restauram a fila automática e sessões novas restauram as abas explícitas. Verificar 320/412 dp e desktop, teclado, salvamento falho, renomeio e ciclo Android; arquivos de notas intactos.

## 5. Evidência de conclusão e validação de release

Para cada tarefa concluída, registrar:

- ID e escopo efetivamente entregue.
- Versão/commit e plataforma/aparelho usados.
- Reprodução ou cenário de aceitação; esperado e observado.
- Testes executados e resultados, com logs/capturas somente quando pertinentes e sem segredos.
- Medições antes/depois quando o item tratar de desempenho.
- Limitações e pendências remanescentes. Se parte ficou pendente, separar o escopo sem esconder trabalho ausente.

Uma release deve conferir os fluxos afetados e uma amostra essencial de criar/editar/salvar/reabrir, recuperação, navegação e IA conforme o incremento. Mudanças em Android exigem as validações físicas pertinentes de toque/IME/ciclo de vida. A profundidade dos testes acompanha o risco; não multiplicar checagens repetidas sem motivo.

## 6. Rastreabilidade com o FeaturesCheck (incorporado nesta consolidação)

### 6.1. Índice temático de requisitos e implementações anteriores

O índice temático abaixo foi preservado e corrigido para consulta rápida. Ele não substitui a matriz integral 6.2 nem o detalhamento completo da seção 13.

Esta relação indica onde cada entrada do arquivo de origem é tratada. Não atesta execução nem altera seus checkboxes. Entradas repetidas na origem continuam listadas para preservar o vínculo. A plataforma compartilhada tem direção própria na seção 7.

- **Desempenho e fluidez:** CST-114, CST-137, CST-099.

- **Métrica de execução útil do Assistente:** CST-094, CST-095, CST-096, CST-097.

- **Fluxo de trabalho:** CST-140.

- **Experiência móvel:** CST-015, CST-016, CST-017, CST-105.

- **Navegação radial no celular:** CST-043.

- **Progresso visual nativo e acompanhamento automático de checklists (nova decisão de 01/11):** CST-227 (componente editável), CST-228 (cálculo derivado de tarefas). Esta necessidade é posterior ao FeaturesCheck histórico; não altera a matriz 6.2 nem o anexo 13.

- **Confiabilidade do editor:** CST-015, CST-016, CST-017, CST-128, CST-129, CST-142, CST-143, CST-144, CST-147. {ia:"gpt-6-luna-pro"}

- **Numeração de linhas no editor:** CST-147. {ia:"gpt-6-luna-pro"}

- **Renderização matemática no editor:** CST-142. {ia:"gpt-6-luna-pro"}

- **Menu radial sobre o controle de leitura/edição:** CST-143. {ia:"gpt-6-luna-pro"}

- **Interrupções e perda de texto durante a edição — bloqueador crítico:** CST-144. {ia:"gpt-6-luna-pro"}

- **Estado da barra de formatação:** CST-018.

- **Código em linha e blocos:** CST-020.

- **Comentários em notas:** CST-022.

- **Comentários na produção editorial e na nota-fonte:** CST-060, CST-152, CST-184.

- **Revisão de alterações da IA:** CST-034.

- **Marca de autoria da IA:** CST-027, CST-028, CST-029, CST-030, CST-031, CST-032, CST-033, CST-035.

- **Painel de referência fixado:** CST-023.

- **Tags estilizadas:** CST-024.

- **Modelos reutilizáveis de notas:** CST-044, CST-045, CST-046, CST-049, CST-050.

- **Blocos com horário:** CST-025.

- **Relógio, cronômetro e timer:** CST-052.

- **Notas com blocos aninháveis:** CST-026.

- **Criar nota pelo editor:** CST-021.

- **Barra de ferramentas móvel:** CST-019.

- **Ferramentas de matemática e visualização:** CST-069.

- **Instruções por blocos na criação de agentes:** CST-070.

- **Planejamento dos agentes:** CST-078.

- **Caixa de perguntas adaptável:** CST-115.

- **Comandos inline:** CST-080.

- **Nomes personalizados dos agentes:** CST-071.

- **Agentes padrão opcionais:** CST-071.

- **Contexto de conversas recentes:** CST-089.

- **Continuidade ao abrir um agente — manter a conversa em uso ou iniciar uma nova quando apropriado:** CST-145. {ia:"gpt-6-luna-pro"}

- **Plugins integrados aos agentes — tarefas com contexto delimitado, resultados utilizáveis e permissões preservadas:** CST-146. {ia:"gpt-6-luna-pro"}

- **Consulta entre agentes — continuidade por múltiplos turnos:** CST-090, CST-091, CST-092, CST-093.

- **Memórias dos agentes — investigar:** CST-085, CST-086, CST-087, CST-133, CST-134.

- **Arquivos no chat de IA:** CST-081, CST-122.

- **Análise de imagens:** CST-081.

- **Separação entre execução, revisão e resposta final:** CST-079.

- **Marca d’água de autoria em blocos gerados por IA:** CST-027, CST-028, CST-029, CST-030, CST-031, CST-032, CST-033, CST-035.

- **Geração de imagens:** CST-082, CST-138, CST-169, CST-173.

- **Geração de vídeo, motion graphics e revisão por agentes (Claude Motion como referência):** CST-170–CST-173, CST-176–CST-180, CST-187.

- **Skills e ferramentas de publicação para IA:** CST-176–CST-178, CST-182, CST-188.

- **Publicação HTML, PDF, livros, slides, imagem e curso:** CST-162–CST-175, CST-185.

- **Edição direta/bidirecional das notas no Estúdio:** CST-150–CST-155, CST-189.

- **API de IA própria e compartilhada:** Seção 7 — plataforma compartilhada.

- **Conhecimento completo das funcionalidades do Urbe pelos agentes:** CST-075.

- **Idioma padrão do sistema para agentes:** CST-077.

- **Widget de voz:** CST-055, CST-056, CST-057, CST-126, CST-127.

- **Gravações longas e transcrição em notas:** CST-058.

- **Duração da gravação:** CST-056.

- **Gravações consecutivas:** CST-055.

- **Checkbox obrigatório em todas as notas rápidas:** CST-053.

- **Organização mensal das notas rápidas:** CST-054.

- **Explorador e movimentação:** CST-036, CST-037, CST-038, CST-039.

- **Navegação móvel radial:** CST-043.

- **Tags herdadas por projeto:** CST-042.

- **Substituição do Editor de Páginas por Publicações e vínculo direto com o vault:** CST-061, CST-135, CST-149–CST-160.

- **Desenvolvimento de código e prévia HTML ao vivo:** CST-141. {ia:"gpt-6-luna-pro"}

- **Fim das composições antigas e proteção dos arquivos preexistentes:** CST-062, CST-149, CST-157.

- **Desempenho e teste móvel das Publicações:** CST-063, CST-114, CST-181, CST-186.

- **Ativos, imagens e referências vivas nas Publicações:** CST-059, CST-151, CST-153.

- **Tutorial em painel dedicado:** CST-103.

- **Tutorial somente leitura:** CST-103.

- **Configuração inicial:** CST-104.

- **Aberturas seguintes:** CST-104, CST-114.

- **Otimização das operações:** CST-098, CST-099.

- **Instruções personalizadas por `AGENTS.md`:** CST-076.

- **Desempenho e latência:** CST-114, CST-137.

- **Editor nativo:** CST-015, CST-016, CST-017, CST-128, CST-129.

- **Crash durante execução de agente com pesquisa e geração de imagens:** CST-111, CST-082, CST-138.

- **Travamento ao expandir etapas e ferramentas no chat:** CST-112.

- **Avisos repetidos “Urbe não está respondendo” durante uso da IA e do vault (beta.38):** CST-148. {ia:"gpt-6-luna-pro"}

- **Seleção de ferramentas:** CST-113.

- **Widget de voz:** CST-055, CST-056, CST-057, CST-126, CST-127.

- **Explorador:** CST-036, CST-037.

- **Seletores de links e incorporações de arquivos:** CST-040, CST-041.

- **Experiência móvel:** CST-015, CST-016, CST-017, CST-105.

- **Botão Voltar no Android:** CST-010.

- **Formulário — tipos de campo e seleção múltipla (beta 31; PR #763):** CST-047, CST-048.

- **Atualização visual da conversa de IA:** CST-008.

- **Encaminhamento de perguntas e aprovações entre agentes — limitação conhecida:** CST-091.

- **Verificação dos formulários no chat:** CST-047.

- **Classificação dos relatos:** CST-003.

- **Novo nome:** CST-107, CST-108, CST-130.

- **Intenção da marca:** CST-130.

- **Direção da logo:** CST-130.

- **Escopo visual:** CST-108, CST-130.

- **Estudo visual:** CST-130.

- **Verificações antes de concluir:** CST-107.

- **Páginas legadas — seleção e criação (PR #700):** histórico técnico referenciado por CST-135, mas a UX anterior está substituída por Publicações.

- **Transclusões no editor Visual (PRs #705 e #706):** CST-040, CST-041, CST-064.

- **Propriedades e modos de nota (PR #707):** CST-119, CST-120.

- **Captura e atualizações no Windows (PR #700):** CST-125, CST-127.

- **Desempenho em vaults grandes (PR #700):** CST-099, CST-114, CST-137.

- **Foco Android (PR #721):** CST-016, CST-017, CST-105.

- **Provedores de IA por agente (PR #702):** CST-071, CST-132.

- **Fluxo de imagens no chat (PRs #704 e #755):** CST-081, CST-082, CST-123.

- **IA local (PR #718):** CST-116, CST-117, CST-118.

- **Execução em segundo plano Android (PR #726):** CST-013.

- **Planejamento e revisão (PR #726):** CST-078, CST-079.

- **Atividade do Assistente (PRs #732, #738 e #755):** CST-112, CST-136.

- **Acesso contextual à IA (PR #747):** CST-080.

- **Interação e acompanhamento no chat (PR #755):** CST-121, CST-122, CST-123, CST-124.

- **Pesquisa pública (PRs #733 e #750):** CST-083, CST-084, CST-124.

- **Memória local tipada (PR #734):** CST-085, CST-086, CST-088, CST-133. (CST-116–118 tratam de IA local, não de memória.)

- **Gerenciamento e ferramentas de memória (PR #734):** CST-086, CST-133.

- **Recuperação de memória (PR #737):** CST-087, CST-089, CST-098.

- **Editor nativo de grafos (PR #748):** CST-065, CST-066.

- **Grafos no vault e ferramentas de leitura (PR #748):** CST-067, CST-068, CST-134.

- **Espaços e histórico por agente (PR #757):** CST-014, CST-074.

- **Agentes predefinidos (PR #757):** CST-071.

- **Permissões e exclusão de agentes (PR #761):** CST-072, CST-073, CST-074.

- **Multichat (PR #761):** CST-014, CST-100.

- **Consulta entre agentes (PR #761):** CST-090, CST-091, CST-092, CST-093.

- **Formulários no chat (beta 31; PR #763, UC-90 e UC-90.1):** CST-047, CST-048, CST-049, CST-051.

- **Formulários reutilizáveis (beta 31; PR #763, UC-90 e UC-90.1):** CST-044, CST-045, CST-049, CST-050.

- **Mensagens durante execução (beta 31; PR #763, UC-90 e UC-90.1):** CST-009.

- **Robustez da apresentação (beta 31; PR #763, UC-90 e UC-90.1):** CST-136.

- **Eficiência de leitura e escrita (beta 31; PR #763, UC-90 e UC-90.1):** CST-098.

- **Métricas parciais de eficiência (beta 31; PR #763, UC-90 e UC-90.1):** CST-094, CST-095, CST-096, CST-098.

### 6.2. Matriz integral de 131 entradas do FeaturesCheck (fonte histórica 31/10/2026)

A tabela abaixo é **exhaustiva em relação às 131 linhas de checkbox do arquivo anexado**. A coluna de status reproduz o estado da fonte: `[x]` significa implementação relatada no FeaturesCheck, **não** entrega verificada nesta consolidação. A coluna CST indica a tarefa normativa de validação, adaptação ou proteção. O conteúdo integral de cada entrada, inclusive ressalvas, PRs e exemplos, está reproduzido sem edição na seção 13. Os números de linha são os do FeaturesCheck recebido nesta revisão.

| Linha do FeaturesCheck | Estado original | Entrada | Destino na consolidação |
|---:|:---:|---|---|
| 7 | `[ ]` | Desempenho e fluidez | CST-099, CST-114, CST-137, CST-181 |
| 8 | `[ ]` | Métrica de execução útil do Assistente | CST-094, CST-095, CST-096, CST-097, CST-098 |
| 9 | `[ ]` | Fluxo de trabalho | CST-140, CST-063, CST-105 |
| 10 | `[ ]` | Experiência móvel | CST-015, CST-016, CST-017, CST-105 |
| 11 | `[x]` | Navegação radial no celular | CST-043, CST-143 |
| 21 | `[ ]` | Renderização de matemática no editor | CST-142 |
| 22 | `[ ]` | Menu radial sobre o controle de leitura/edição | CST-143 |
| 23 | `[ ]` | Interrupções e perda de texto durante a edição — crítico | CST-144 |
| 24 | `[ ]` | Numeração de linhas no editor | CST-147 |
| 26 | `[ ]` | Confiabilidade do editor | CST-015, CST-016, CST-017, CST-128, CST-129, CST-142, CST-144 |
| 27 | `[x]` | Estado da barra de formatação | CST-018. |
| 28 | `[x]` | Código em linha e blocos | CST-020. |
| 29 | `[x]` | Comentários em notas | CST-022. |
| 30 | `[x]` | Comentários em páginas | CST-022, CST-060, CST-184 |
| 31 | `[x]` | Revisão de alterações da IA | CST-034. |
| 32 | `[x]` | Marca de autoria da IA | CST-027, CST-028, CST-029, CST-030, CST-031, CST-032, CST-033, CST-035. |
| 33 | `[x]` | Painel de referência fixado | CST-023. |
| 34 | `[x]` | Tags estilizadas | CST-024. |
| 35 | `[x]` | Modelos reutilizáveis de notas | CST-044, CST-045, CST-046, CST-049, CST-050. |
| 36 | `[x]` | Blocos com horário | CST-025. |
| 37 | `[x]` | Relógio, cronômetro e timer | CST-052. |
| 38 | `[ ]` | Notas com blocos aninháveis | CST-026. |
| 39 | `[x]` | Criar nota pelo editor | CST-021. |
| 40 | `[x]` | Barra de ferramentas móvel | CST-019. |
| 41 | `[ ]` | Ferramentas de matemática e visualização | CST-069, CST-164, CST-183 |
| 45 | `[x]` | Instruções por blocos na criação de agentes | CST-070. |
| 46 | `[x]` | Planejamento dos agentes | CST-078. |
| 47 | `[x]` | Caixa de perguntas adaptável | CST-115. |
| 48 | `[x]` | Comandos inline | CST-080. |
| 49 | `[x]` | Nomes personalizados dos agentes | CST-071. |
| 50 | `[x]` | Agentes padrão opcionais | CST-071. |
| 51 | `[x]` | Contexto de conversas recentes | CST-089. |
| 52 | `[ ]` | Continuidade ao abrir um agente | CST-074, CST-145 |
| 53 | `[x]` | Consulta entre agentes — continuidade por múltiplos turnos | CST-090, CST-091, CST-092, CST-093. |
| 54 | `[ ]` | Plugins integrados aos agentes | CST-072, CST-073, CST-146 |
| 55 | `[ ]` | Memórias dos agentes — investigar | CST-085, CST-086, CST-087, CST-133, CST-134. |
| 56 | `[x]` | Arquivos no chat de IA | CST-081, CST-122. |
| 57 | `[x]` | Análise de imagens | CST-081. |
| 58 | `[x]` | Separação entre execução, revisão e resposta final | CST-079. |
| 59 | `[x]` | Marca d’água de autoria em blocos gerados por IA | CST-027, CST-028, CST-029, CST-030, CST-031, CST-032, CST-033, CST-035. |
| 60 | `[ ]` | Geração de imagens | CST-082, CST-138, CST-169, CST-173 |
| 61 | `[ ]` | API de IA própria e compartilhada | CST-075; seção 7 (plataforma de IA compartilhada) |
| 62 | `[x]` | Conhecimento completo das funcionalidades do Urbe pelos agentes | CST-075. |
| 63 | `[x]` | Idioma padrão do sistema para agentes | CST-077. |
| 67 | `[ ]` | Widget de voz | CST-055, CST-056, CST-057, CST-126, CST-127 |
| 68 | `[ ]` | Gravações longas e transcrição em notas | CST-058. |
| 69 | `[x]` | Duração da gravação | CST-056. |
| 70 | `[x]` | Gravações consecutivas | CST-055. |
| 71 | `[x]` | Checkbox obrigatório em todas as notas rápidas | CST-053. |
| 72 | `[x]` | Organização mensal das notas rápidas | CST-054. |
| 76 | `[ ]` | Explorador e movimentação | CST-036, CST-037, CST-038, CST-039. |
| 77 | `[x]` | Navegação móvel radial | CST-043. |
| 78 | `[x]` | Tags herdadas por projeto | CST-042. |
| 82 | `[ ]` | Editor de páginas robusto e integrado | CST-061, CST-149, CST-158, CST-159, CST-161 — conceito legado substituído por Publicações |
| 83 | `[x]` | Composições dentro de Páginas | CST-062, CST-157, CST-160 — histórico legado preservado; interface descontinuada |
| 84 | `[ ]` | Desempenho e teste móvel das páginas | CST-063, CST-114, CST-181, CST-186 |
| 85 | `[x]` | Imagens que não aparecem nas páginas | CST-059, CST-163 — correção histórica sujeita a regressão em Publicações |
| 89 | `[x]` | Tutorial em painel dedicado | CST-103. |
| 90 | `[x]` | Tutorial somente leitura | CST-103. |
| 94 | `[x]` | Configuração inicial | CST-104 |
| 95 | `[ ]` | Aberturas seguintes | CST-104, CST-114. |
| 96 | `[ ]` | Otimização das operações | CST-098, CST-099, CST-114 |
| 97 | `[x]` | Instruções personalizadas por \`AGENTS.md\` | CST-076. |
| 101 | `[ ]` | Desempenho e latência | CST-099, CST-114, CST-137 |
| 102 | `[ ]` | Ação Enter/Enviar e foco do teclado do dispositivo — editor e IA | CST-016, CST-017, CST-105, CST-144 |
| 103 | `[ ]` | Editor nativo | CST-015, CST-016, CST-017, CST-128, CST-129, CST-144 |
| 104 | `[ ]` | Crash durante execução de agente com pesquisa e geração de imagens | CST-111, CST-082, CST-138 |
| 105 | `[ ]` | Avisos repetidos “Urbe não está respondendo” durante uso da IA (beta.38) | CST-148, CST-011, CST-114 |
| 106 | `[ ]` | Travamento ao expandir etapas e ferramentas no chat | CST-112, CST-136 |
| 107 | `[ ]` | Seleção de ferramentas | CST-113, CST-072, CST-073 |
| 108 | `[ ]` | Widget de voz | CST-055, CST-056, CST-057, CST-126 |
| 109 | `[ ]` | Explorador | CST-036, CST-037, CST-038, CST-039 |
| 110 | `[ ]` | Seletores de links e incorporações de arquivos | CST-040, CST-041, CST-064 |
| 111 | `[ ]` | Experiência móvel | CST-105, CST-137 |
| 112 | `[ ]` | Botão Voltar no Android | CST-010 |
| 113 | `[ ]` | Formulário — tipos de campo e seleção múltipla (beta 31; PR #763) | CST-047, CST-048 |
| 114 | `[ ]` | Atualização visual da conversa de IA | CST-008 |
| 115 | `[ ]` | Encaminhamento de perguntas e aprovações entre agentes — limitação conhecida | CST-091, CST-092, CST-093 |
| 116 | `[ ]` | Verificação dos formulários no chat | CST-047, CST-048, CST-049 |
| 117 | `[ ]` | Confiabilidade de edições longas pelo agente | CST-005, CST-034, CST-098, CST-193 |
| 118 | `[ ]` | Classificação dos relatos | CST-003, CST-001 |
| 122 | `[ ]` | Novo nome | CST-107, CST-108, CST-130 |
| 123 | `[ ]` | Intenção da marca | CST-130 |
| 124 | `[ ]` | Direção da logo | CST-108, CST-130 |
| 125 | `[ ]` | Escopo visual | CST-108, CST-130 |
| 126 | `[ ]` | Estudo visual | CST-130, CST-157 — arquivo de identidade no formato legado preservado |
| 127 | `[ ]` | Verificações antes de concluir | CST-107, CST-130 |
| 133 | `[x]` | Páginas — seleção e criação (PR #700) | CST-135, CST-149, CST-157, CST-160 — funcionalidade histórica do editor removido |
| 134 | `[x]` | Transclusões no editor Visual (PRs #705 e #706) | CST-040, CST-041, CST-064 |
| 135 | `[x]` | Propriedades e modos de nota (PR #707) | CST-119, CST-120 |
| 136 | `[x]` | Captura e atualizações no Windows (PR #700) | CST-125, CST-127 |
| 137 | `[x]` | Desempenho em vaults grandes (PR #700) | CST-099, CST-114, CST-137 |
| 138 | `[x]` | Foco Android (PR #721) | CST-015, CST-016, CST-017, CST-105 |
| 139 | `[x]` | Provedores de IA por agente (PR #702) | CST-071, CST-132 |
| 140 | `[x]` | Fluxo de imagens no chat (PRs #704 e #755) | CST-081, CST-082, CST-123 |
| 141 | `[x]` | IA local (PR #718) | CST-116, CST-117, CST-118 |
| 142 | `[x]` | Execução em segundo plano Android (PR #726) | CST-013 |
| 143 | `[x]` | Planejamento e revisão (PR #726) | CST-078, CST-079 |
| 144 | `[x]` | Atividade do Assistente (PRs #732, #738 e #755) | CST-112, CST-136 |
| 145 | `[x]` | Acesso contextual à IA (PR #747) | CST-080 |
| 146 | `[x]` | Interação e acompanhamento no chat (PR #755) | CST-121, CST-122, CST-123, CST-124 |
| 147 | `[x]` | Pesquisa pública (PRs #733 e #750) | CST-083, CST-084, CST-124 |
| 148 | `[x]` | Memória local tipada (PR #734) | CST-085, CST-086, CST-088, CST-133 |
| 149 | `[x]` | Gerenciamento e ferramentas de memória (PR #734) | CST-085, CST-086, CST-133 |
| 150 | `[x]` | Recuperação de memória (PR #737) | CST-087, CST-089, CST-098, CST-134 |
| 151 | `[x]` | Editor nativo de grafos (PR #748) | CST-065, CST-066 |
| 152 | `[x]` | Grafos no vault e ferramentas de leitura (PR #748) | CST-067, CST-068, CST-134 |
| 153 | `[x]` | Espaços e histórico por agente (PR #757) | CST-014, CST-074, CST-145 |
| 154 | `[x]` | Agentes predefinidos (PR #757) | CST-071 |
| 155 | `[x]` | Permissões e exclusão de agentes (PR #761) | CST-072, CST-073, CST-074 |
| 156 | `[x]` | Multichat (PR #761) | CST-014, CST-100 |
| 157 | `[x]` | Consulta entre agentes (PR #761) | CST-090, CST-091, CST-092, CST-093 |
| 158 | `[x]` | Formulários no chat (beta 31; PR #763, UC-90 e UC-90.1) | CST-047, CST-048, CST-049, CST-051 |
| 159 | `[x]` | Formulários reutilizáveis (beta 31; PR #763, UC-90 e UC-90.1) | CST-044, CST-045, CST-049, CST-050 |
| 160 | `[x]` | Mensagens durante execução (beta 31; PR #763, UC-90 e UC-90.1) | CST-009 |
| 161 | `[x]` | Robustez da apresentação (beta 31; PR #763, UC-90 e UC-90.1) | CST-136 |
| 162 | `[x]` | Eficiência de leitura e escrita (beta 31; PR #763, UC-90 e UC-90.1) | CST-005, CST-098, CST-193 |
| 163 | `[x]` | Métricas parciais de eficiência (beta 31; PR #763, UC-90 e UC-90.1) | CST-094, CST-096, CST-098 |
| 169 | `[x]` | Compartilhar um arquivo ou uma nota com outro app pelo menu do Android (CST-036). **Falta:** conferir no aparelho. | CST-036, CST-195 |
| 170 | `[x]` | Até 20 anexos por mensagem, dentro de 30 MB; fotos grandes ou deitadas vão de pé e reduzidas, em vez de recusadas (CST-081). **Falta:** conferir no aparelho. | CST-081, CST-194, CST-195 |
| 171 | `[x]` | Rascunhos do chat, com anexos, guardados no aparelho e restaurados depois de o Android encerrar o app (CST-122). **Falta:** conferir no aparelho. | CST-122, CST-195 |
| 172 | `[x]` | Barra de formatação arrumada pelo toque em Personalização → Editor (CST-019). **Falta:** conferir no aparelho. | CST-019, CST-195 |
| 173 | `[x]` | Ícone nas tags, além da cor (CST-024). **Falta:** conferir no aparelho. | CST-024, CST-195 |
| 174 | `[x]` | Links Markdown \`[texto](Nota.md)\` acompanham renomear e mover (CST-101). **Falta:** conferir no aparelho. | CST-101, CST-195 |
| 175 | `[x]` | Permissões: uma mais restrita vale na hora; um especialista em automático pede aprovação quando quem pediu também pede; mudar \`AGENTS.md\` sempre pede aprovação (CST-073, 093, 084). **Falta:** conferir no aparelho. | CST-072, CST-073, CST-084, CST-093, CST-195 |
| 176 | `[x]` | Ordem das instruções definida: o pedido atual, depois o agente, depois o \`AGENTS.md\`, por último o Urbe (CST-076). **Falta:** conferir no aparelho. | CST-076, CST-195 |
| 177 | `[x]` | Um agente criado de um modelo mantém as ferramentas do modelo; excluir um agente leva as lembranças privadas dele (CST-071, 074). **Falta:** conferir no aparelho. | CST-071, CST-074, CST-085, CST-195 |
| 178 | `[x]` | Pesquisa na internet: uma página não faz o agente enviar trechos das notas; um endereço guardado numa nota abre (CST-084). **Falta:** conferir no aparelho. | CST-084, CST-195 |
| 179 | `[x]` | Relatório de erros sem chaves, trechos de notas nem caminhos, e só dos últimos 30 dias (CST-097). **Falta:** conferir no aparelho. | CST-097, CST-195 |
| 180 | `[x]` | O cartão de aprovação mostra todas as linhas mudadas; qualquer tag filtra a Área de trabalho; Abrir ao lado alcança qualquer nota (DEC-0059). **Falta:** conferir no aparelho. | CST-034, CST-036, CST-066, CST-195 |
| 181 | `[x]` | Áreas de toque de pelo menos 40 px nas telas principais e na Personalização, e contraste WCAG AA nos temas (CST-105). **Falta:** conferir no aparelho. | CST-105, CST-195 |

**Conferência:** 131/131 itens mapeados; 73 `[x]` históricos e 58 `[ ]` históricos. O mapa não altera os estados das tarefas CST. Os casos de páginas foram redirecionados para Publicações; memória tipada mapeia CST-085/086/088/133, e o crash do carrossel mapeia CST-111.

### 6.3. Regra de precedência documental

1. Decisões explícitas posteriores (principalmente seção 1.1 Publicações) prevalecem sobre formulações antigas do FeaturesCheck e de listas anteriores. O editor de páginas legado é exemplo deliberado de requisito superado.
2. O texto original do FeaturesCheck permanece íntegro na seção 13 para não perder problemas, hipóteses, PRs, ressalvas ou checkboxes; não é uma segunda lista normativa que exija manter tecnologias antigas.
3. Cada linha desta matriz aponta a itens CST abertos, que exigem evidência de execução. Uma implementação histórica `[x]` não prova aceite nem converte automaticamente a respectiva tarefa CST em concluída.
4. Divergências entre um registro histórico e implementação atual exigem reprodução e atualização da tarefa de destino, preservando ambos os registros. Não apagar problemas por mera existência de PR ou testes automatizados.

## 7. Direção posterior — plataforma compartilhada de IA

O pedido de uma API/sistema de IA próprio e compartilhável permanece. O código examinado **já contém** `apps/ecosystem-ai/src/AgentRuntime.Core`, independente do `AssistantAgent` usado pelo Urbe, com replay, retomada, grants, ledger e integração de mudanças em contratos próprios. Portanto, a orientação anterior de “primeiro consolidar tudo no Urbe e depois extrair” **não é uma sequência obrigatória** nem licença para copiar o núcleo ou criar um terceiro runner. A ordem correta é **(i)** estabilizar e caracterizar os dois runtimes, **(ii)** elaborar matriz de equivalência e portas, **(iii)** compartilhar apenas capacidades com testes de paridade comprovados e revisão de extração NN-022, **(iv)** migrar consumidores sem regressão e sem acoplamento entre Products. Publicações, editor, memória e vault continuam específicos do Urbe; mecanismos comuns não devem conhecer seus schemas concretos.

- [ ] **PLAT-001 — Definir escopo e arquitetura da plataforma compartilhada após estabilizar os fluxos relevantes do Urbe.**

  **Estado inicial:** proposta pendente de definição.  
  **Frente:** expansão posterior.  
  **Dependências:** contratos de provedores, ferramentas, permissões, memória, contexto e artefatos comprovados no Urbe.  
  **Critério de conclusão:** comparar explicitamente `AssistantAgent` e `AgentRuntime.Core`, com testes de contrato/migração e definição de ownership de runtime, sessões, permissões, memória, tool registry, execução e verificação; documentar fronteiras entre biblioteca comum e recursos próprios do Urbe, versão/compatibilidade, gestão de credenciais, distribuição e validação em **dois consumidores reais** quando aplicável. Qualquer extração passa pelo NN-022 e por decisões materiais aprovadas; não criar serviço remoto, API pública, dependência entre Urbe/Lunet ou acoplamento a provedor como efeito colateral. Referência operacional: CST-199, CST-200, CST-205, CST-216, CST-219.

## 8. Limites e decisões preservadas

- Marca Numa e direção da logo já aprovadas não devem ser redesenhadas por conveniência. Consultar o estudo original e preservar cores/visual aprovados. Verificar disponibilidade não é afirmar que ela existe.
- Menu radial tem como resultado desejado substituir as abas móveis. Protótipo e teste ajudam a executar a decisão, não a diluí-la em avaliação indefinida.
- Horário por bloco significa última edição; a tarefa técnica define sua persistência e comportamento, não reabre o objetivo.
- Comentários aparecem na edição e prévia apropriadas e são omitidos das saídas definidas; preservar conteúdo não significa exportar comentários.
- Corrigir ou transformar texto humano sem criar conteúdo novo não transfere automaticamente autoria à IA. Selos são administrados pelo aplicativo.
- Memória privada e compartilhada precisam de escopos claros mesmo quando o usuário usa um único vault. Não pressupor necessidade de múltiplos vaults.
- Embeddings, imagens offline e recuperação durável após morte do processo não devem ser anunciados como prontos sem implementação e evidência específicas.
- Reduzir releituras, repetições e custo deve preservar qualidade e contexto suficiente. O caso da revisão longa de protoidioma é exemplo qualitativo para investigar, não benchmark cronometrado nem prova de que toda revisão é desperdício.

---

## 9. Especificação de produto — Publicações (decisão normativa desta revisão)

### 9.1. Terminologia e fronteira do produto

- **Vault:** arquivos locais verdadeiros, governados pelos atuais contratos de arquivo, identidade, histórico, autorização e recuperação. A palavra "API" neste plano significa **interfaces internas estáveis para leitura, escrita, observação e referência**; não exige servidor, endpoint HTTP, duplicação de banco ou sincronização com a nuvem.
- **Fonte:** nota/trecho/bloco/ativo real do vault. Toda fonte possui identidade, localização e modo de acesso; vínculo quebrado é estado diagnosticável, nunca conteúdo substituído silenciosamente.
- **Nota editorial:** nota real criada ou escolhida para conteúdo específico de um produto (narração, roteiro, resumo, explicação, tradução, fala ou exercício). Ela pode ter uma ou várias fontes originais com relacionamento de derivação. Não é texto escondido em estado interno do estúdio.
- **Publicação:** projeto de produção editável com IDs/referências, estratégia narrativa, estrutura, composições, estilo, canais e histórico de renders. **Não armazena o texto canônico que já pertence às notas.** Pode registrar propriedades de composição, filtros, parâmetros, keyframes e consultas tipadas.
- **Produto/exportação:** arquivo/pacote efetivamente gerado para terceiros (HTML, PDF, EPUB, PPTX, MP4 etc.), com manifest e hash/snapshot das fontes usadas. É **estático enquanto distribuído**; reexportar usa o estado atual do vault e do projeto. Não prometer que um PDF enviado a alguém mude quando a nota local mudar.
- **Motor:** componente que converte a composição para uma representação visual, temporal ou um formato de entrega. **Skill:** modo de saber criar com qualidade. **Ferramenta de agente:** ação autorizada que manipula fontes/publicação/render/export. **Plugin:** extensão de capacidades ou integração externa sob permissões. Essas quatro coisas não são sinônimas.

### 9.2. Invariantes da ligação direta e bidirecional

1. Uma fonte possui **um proprietário de conteúdo**: seu arquivo do vault. O estúdio não guarda cópia editável paralela nem implementa algoritmo de sincronização entre versões espelhadas. A prévia pode guardar pixels, DOM e dados **derivados descartáveis** em cache, identificados pelo hash da fonte.
2. Ao editar texto-fonte clicando na prévia, a operação resolve primeiro a referência e a posição estável da nota/bloco; informa **qual arquivo será alterado**, invoca os serviços de escrita do Urbe e atualiza todas as projeções que utilizam aquela referência. Evitar cálculo por offset de caracteres frágil após mudança externa; conferir identidade e versão, com patch semântico e proteção de conflito.
3. Ao editar a nota no editor comum, agentes, ferramentas ou aplicativo externo, o produto aberto observa a alteração confirmada e atualiza apenas dependentes. Se a atualização afetar composição sensível (quebra de página, fala, duração, slide), sinalizar necessidade de revisão da saída, sem apagar o conteúdo editorial existente.
4. Se o usuário selecionar uma frase de origem e pedir "transforme em resumo para o slide", **não** encurtar silenciosamente a nota original. Perguntar/aplicar comportamento explícito: *editar a fonte* quando se deseja mudar a própria nota; *criar/adaptar nota editorial vinculada* quando o produto pede outra redação. A nota editorial passa a ser a única fonte de verdade desse texto adaptado, com link para a pesquisa.
5. O que é exclusivamente apresentação — tamanho, margem, ordem das cenas, cor, ritmo, recorte, fontes, transições, duração, movimento, layout, projeção de mapa — é dado do projeto Publicação. Não contaminar notas científicas com CSS ou keyframes de produtos específicos. Conteúdo de títulos, legendas, roteiro, narração e atividades, quando editável, pertence a notas reais.
6. Uma fonte pode ser usada por várias publicações. Alterá-la uma vez atualiza todas. Cada publicação pode aplicar estilo e reorganização distintos sem forçar as demais a copiar esse estilo.
7. Renomear/mover/restaurar arquivo preserva referências por identidade. Excluir fonte oferece estado "ausente" e alternativas de reparar/substituir/remover vínculo; nunca recria uma nota substituta sem consentimento.
8. Todas as alterações respeitam autoria da IA, histórico, permissões, diff, backup e conflito externo. Agentes não podem obter escrita irrestrita por estarem dentro de Publicações; não podem remover selos `{ia:...}` administrados pelo app.
9. **Publicação editável ≠ publicação exportada.** A primeira é viva e diretamente ligada ao vault; a segunda é um snapshot entregue ao público, com procedência e opção de regenerar. Projetos HTML exportados podem conter interações autônomas, mas não ganham acesso ao vault do autor.
10. O trabalho **offline/local-first é padrão**. Internet pode ser usada por pesquisa, modelos externos, imagens/vídeos generativos, TTS ou publicação remota autorizada, nunca como condição obrigatória para editar a publicação ou suas fontes.

**Cenário de aceitação fundamental:** `Tectônica.md` aparece em um livro, numa apresentação e num explainer. Alterar o parágrafo da nota dentro do estúdio do livro altera `Tectônica.md`, e as outras duas publicações são atualizadas/assinaladas para revisão conforme suas dependências. Alterar o título de um slide **não** modifica a nota se esse título for texto editorial próprio: ele está em uma nota editorial vinculada. Reabrir app offline e mover `Tectônica.md` conserva os vínculos. Todos os passos são passíveis de undo/recuperação, conforme contrato da operação.

### 9.3. Representação conceitual — sem fixar prematuramente schema definitivo

O exemplo abaixo ilustra a **ausência de cópia do texto**. Nomes dos campos e extensão são **provisórios**, sujeitos a contratos, testes e versionamento do CST-150:

```yaml
publication:
  id: pub-mundos-01
  title: Geração científica de mundos
  audience: estudantes
  purpose: explicar fundamentos
  sources:
    - ref: 'vault-note-id:tectonica#^placas'
      role: material-base
    - ref: 'vault-note-id:roteiro-video#^abertura'
      role: texto-editorial
      derivesFrom: 'vault-note-id:tectonica#^placas'
  compositions:
    - id: livro
      kind: book
      nodes:
        - sourceRef: 'vault-note-id:tectonica#^placas'
          treatment: corpo
    - id: video
      kind: timeline
      scenes:
        - sourceRef: 'vault-note-id:roteiro-video#^abertura'
          start: 0
          duration: 8
          animationRef: 'motion-style:revelacao-de-texto'
  outputProfiles:
    - html
    - pdf
    - mp4
```

As referências são simbólicas, não a afirmação de que os IDs e seletores atuais do Urbe já possuem esse contrato. A implementação deve estudar `DocumentModel`, `ArtifactModel`, aliases, identificadores de blocos e o vault atual antes de fixar o schema. O projeto só armazena **origens e instruções de composição**; cada texto reside num arquivo real. Um índice/cache derivado pode ser materializado para renderização, mas é descartável e recuperável da origem.

### 9.4. HTML é linguagem principal de apresentação, não conversor obrigatório

- A base HTML/CSS/SVG deve alimentar **web, leitura interativa, slides web, prévia de componentes, layout responsivo e parte do layout impresso**. É um excelente denominador comum de composição visual, sobretudo no Android, onde prévia/artefatos web podem ser isolados do aplicativo.
- **Semântica antes da aparência:** estruturas de capítulo, bibliografia, citações, figura, tabela, atividade de curso, slide, tempo de cena, áudio e keyframe não devem ser reduzidas a markup final antes de escolher o adaptador. Preserve metadados para uma versão editável de PPTX, um DOCX semântico e um vídeo com timeline.
- O renderizador HTML do app pode utilizar recursos web **dentro de um host isolado** sem converter o UI principal C#/Avalonia em um web app; JavaScript pode fazer parte do **artefato exportado**, desde que não receba privilégios irrestritos sobre o vault.
- PDF impresso precisa de paginação independente; a impressão WebView do Android pode servir como fallback explícito, **não** como garantia de diagramação editorial. Alguns formatos perdem interatividade por natureza: a saída deve sinalizar e oferecer fallback adequado.
- Cada adaptador declara se preserva **estética, editabilidade, acessibilidade, pesquisa, interatividade, mídia, sincronismo audiovisual e portabilidade**. Qualidade máxima em uma dimensão não prova máxima em todas.

### 9.5. Fluxo de autoria e experiência de uso

**Entrada contextual:** selecionar uma nota, pasta, conjunto de notas, imagem, pesquisa ou mensagem do agente → **Criar Publicação**. A pessoa pode escolher o objetivo e formato desde o início ou deixar isso em aberto. O app cria o projeto com referências reais. Não inventar um assistente obrigatório de vários passos nem forçar template.

**Produção:** uma área visual dominante do produto, edição direta da fonte onde semanticamente possível, capacidade de abrir/referenciar nota original, composição livre, presets opcionais, inspeção de referências, cena/slide/capítulo na mesma superfície contextual, sem um novo editor de texto concorrente. Alterar finalidade/público/formato pode propor outra composição do mesmo conjunto de notas, **não** criar outro vault ou duplicar todo o conteúdo.

**Revisão:** prévia de saída fiel, revisão de fontes e direitos, inconsistências, legibilidade, cortes, orfandade de mídia, áudio/legendas, diferenças de formato; a IA pode sugerir e aplicar alterações conforme autorização, sempre com acesso à origem da correção. Mostrar o que é observação, o que é hipótese e o que foi efetivamente validado por render/execução.

**Distribuição:** exportação de verdade (arquivo válido e verificável) com opções por formato e destino; pacote offline quando suportado; nome, estado, progresso, diagnóstico e informação sobre limitações/compatibilidade. Arquivos enviados/publicados são snapshots; o projeto local continua ligado às notas.

**Duas portas, um motor:** pessoa criando manualmente e agente criando por comando usam a mesma API. O usuário pode passar da criação manual para a IA ou vice-versa sem trocar de ferramenta, perder contexto ou autorizar outras ações implicitamente.

### 9.6. Contrato de produção audiovisual inspirado no Claude Motion

**A referência é o recurso oficial da Anthropic**, não sites independentes que também usam "Claude Motion" no nome. De acordo com a ajuda oficial e anúncio de 08/10/2026, o Claude Motion cria vídeos explicativos curtos a partir de **código para animar texto, gráficos, formas e imagens**; permite revisar pela conversa ou pelo editor e exportar MP4. Não gera automaticamente filmagens fotorrealistas de pessoas. A integração Runway anunciada como parceira permite adicionar trechos generativos e edição, mas isso é um estágio opcional distinto.

**Capacidades a reproduzir como princípios, não código fechado:**

1. Agente lê fontes selecionadas e produz briefing, objetivos, roteiro com referência a notas, estrutura de cenas e storyboard.
2. O resultado é uma **composição temporal editável**: cenas, objetos semânticos, timeline, transições, curvas de animação, posições, tipografia, gráficos, dados, fps/proporção, trilhas de áudio e legendas.
3. O agente opera ferramentas para compor por código/declarações e renderizar a prévia; não precisa de modelo de vídeo para produzir motion graphics. Uma cena deve poder trocar "20%" por "18%", mudar duração e ser renderizada novamente sem refazer desnecessariamente todas as outras cenas.
4. O editor permite ajustes manuais de elementos/timing e revisão em linguagem natural, sem gerar um MP4 novo como único modo de edição. **Prévia e export usam a mesma definição de cena**, sem divergências visuais não declaradas.
5. Renderização exporta **MP4 real**, com codec/áudio/fps/duração testados. Conteúdos editáveis (roteiro, legendas, números, fórmulas) fazem referência a notas/arquivos do vault; keyframes/timing ficam no projeto. Mudança numa nota-fonte invalida cenas dependentes e sinaliza a necessidade de renderizar/validar nova versão.
6. Produção cinematográfica com cenas realistas poderá chamar **geradores externos** mediante autorização, custo e contrato próprios. Não confundir essa modalidade com renderização programática local ou afirmar que todo modelo de IA já gera vídeo.
7. O app oferece presets úteis (vídeo educativo, animação de dados, lançamento de produto, documentário narrado, aula, tutorial, shorts vertical), mas permite composição livre, câmera/3D apenas quando demonstrada viabilidade e não promete recursos indisponíveis.

**Referências de implementação:** Motion Canvas (biblioteca TS+editor, preview e render de sequências com FFmpeg); Remotion (componentes/código e render de vídeos, sujeito a licença); FFmpeg (codec/mux), SVG/Canvas/CSS/Three.js (elementos visuais), Playwright/Chromium apenas onde viáveis e licenciados. **Não há decisão aprovada de embarcar runtime Node/Chromium no Android.** Avaliar adaptador em C#, renderer nativo ou worker/serviço opcional isolado, desempenho offline, consumo, tamanho APK, licença e testes de equivalência antes da escolha.

**Caso mínimo de aceite:** abrir `Clima.md`, pedir "vídeo explicativo de 30 segundos"; agente lê a fonte permitida, produz storyboard (três cenas), mostra prévia e exporta um MP4 real. Usuário altera o texto-fonte pelo próprio vídeo e comprova que `Clima.md` mudou; ajusta apenas o tempo da cena e comprova que `Clima.md` **não** mudou; reexporta, abre o MP4, verifica trilha/legenda/duração e reabre o projeto depois de reiniciar o Urbe.

### 9.7. O que foi aproveitado e o que deve ser descartado do sistema antigo

**Aproveitar como conhecimento e componentes avaliáveis, após auditoria:**

- Editor de notas integrado, leitura Markdown, referência por `[[...]]`/`![[...]]`, seleção de fonte por pasta/tag, notas completas ou por trecho, comentários e autoria; isso alimenta Publicações e é domínio do Urbe, não do editor velho.
- Blocos reutilizáveis, sistemas de temas, tipografia e cores; seções e layouts livres com caixas/texto/imagem/botão/lista/quote/vídeo/tabela/código/fórmula/SVG; preservar **a capacidade de composição livre**, não a interface de montagem ou o velho JSON como dogma.
- Capa, página de título, capítulos, partes, sumário, hierarquia de títulos, índices, regras de livro e navegação; transformar em componentes semânticos novos.
- Assistente capaz de aprender o schema da ferramenta e escrever publicações; reformular `page_schema`/`write_page` em ações com diffs, render e contratos de Publicação.
- Prévia HTML isolada, inlining de assets locais, diagnóstico de HTML/preview, testes de compatibilidade e parte do código de renderização segura; reaproveitamento condicionado a qualidade e remoção de dependências do editor antigo.
- Edição contextual da prévia e comentários que desaparecem da saída pública; agora precisa ser **edição da nota de origem**, não edição de string duplicada.
- Templates próprios, variantes de tema e criação contextual de uma nota/pasta; reinterpretar como presets opcionais de produto sobre fontes reais.

**Descartar como decisão de produto:** interface do `PageStudioView` anterior; obrigação de `.page.json` como produto central; construção baseada em preencher um schema engessado; exportação PDF que depende exclusivamente do diálogo de impressão; navegação/menus paralelos que duplicam o editor; modelos que escondem conteúdo textual dentro do JSON; métodos que achatam todo tipo de mídia em uma página HTML antes de escolher o produto. Código legado pode ser removido mesmo durante beta, desde que nada do vault seja apagado nem outro módulo seja quebrado.

**Arquivos críticos:** uma nota comum e um estudo de identidade podem existir como `.md`, `.page.json`, `.html` ou outro arquivo. O fato de a interface velha sair **não** autoriza eliminar `Páginas/Identidade Numa — estudo de logo.page.json` ou perder decisões de marca. Preservar conteúdo e disponibilizar leitura como arquivo; oferecer importador opcional quando fizer sentido. Não migrar automaticamente o vault do usuário em nome de "limpar" betas.

### 9.8. Fases, gates e aceitação de release

- **Fase A — diagnóstico/remoção controlada:** mapear módulos, ferramentas de agentes, templates, rotas de navegação, testes e arquivos legados; proteger fonte; remover interface antiga sem regressões gerais. Gate: build, testes, arquivos do vault intactos.
- **Fase B — núcleo de Publicações e ligação direta:** contrato de fontes e escrita bidirecional, modelo semântico, projeto de publicação, experiência inicial, HTML com assets. Gate: duas publicações usam uma nota sem cópia; editar em qualquer superfície reflete em todas; conflitos recuperáveis.
- **Fase C — obras estáticas profissionais:** PDF paginado, livros, slides, imagens, testes de qualidade, exportação real e confirmação de arquivo. Gate: outputs abertos em leitores/aplicativos independentes, inspeção de páginas e validação no Android.
- **Fase D — audiovisual inspirado em Claude Motion:** roteiro vinculado, cena/timeline editável, preview, render real MP4, voz/legenda e workflows de agentes. Gate: caso do vídeo de 30 s, fontes vivas e re-render verificável.
- **Fase E — cursos e distribuição extensível:** módulos, exercícios, standards educacionais, adaptadores externos, publicações remotas opt-in, ecossistema de skills. Gate: pacote reproduzível e testado em plataforma compatível.

Estas fases organizam dependências, **não** autorizam entregar fragmentos quebrados como "produto final" nem impõem bloqueio desnecessário a correções do editor, dados ou IA em paralelo. Cada capacidade deve fechar com testes pertinentes e evidência real de artefato, não só aprovação de testes unitários.

### 9.9. IA operativa dentro do produto — contrato de integração transversal (revisão 2.4)

**A arquitetura de IA não substitui nem compete com a arquitetura de conteúdo.** O vault permanece autoridade; uma ação manual e a mesma ação solicitada por um agente devem chegar ao mesmo serviço de domínio. O LLM **planeja e propõe**, mas o host verifica versão, identidade, tipo, escopo, autorização e efeito. Respostas de ferramentas, skills, documentos e plugins são dados até serem validadas. O estado de execução não é guardado somente no histórico de chat; histórico não equivale a task/run/recibo.

**Objetos mínimos que não devem ser confundidos:** `SourceRef` (nota/ativo/trecho, ID e revisão); `EvidenceRef` (fragmento ou resultado observável, citação/versão/limitação); `OperationProposal` (ação tipada, pré-condições, diff/prévia, risco e destino); `TaskRun` (objetivo, entradas, passos, aprovações, chamadas, efeitos, cancelamento/retomada); `ArtifactRef` (resultado real verificado com formato e hash); `DerivedDependency` (por que o resultado depende de fontes/versões). Os nomes são **conceituais**, não schemas aprovados; usar tipos existentes quando satisfizerem o contrato.

O ciclo comum é: **seleção/objetivo contextual → recuperação de fontes do vault com evidência → plano proporcional → capability autorizada (tool/skill) → operação no serviço nativo ou executor isolado → artefato real → verificador por tipo → diff/resultado contextual → atualização apenas dos derivados dependentes**. Perguntas, formulários e aprovações podem interromper o fluxo sem perder o run. O usuário pode executar manualmente as operações sem modelo configurado. A UI mostra etapas úteis, não despeja implementação e logs de programação para a pessoa.

**Máquinas de estado e fronteiras:** `Conversation` organiza o diálogo; `Plan` comunica intenção; `TaskRun` persiste o trabalho; `Approval` concede a ação específica, não poderes gerais; `Publication` compõe materiais e referências; `Export` é snapshot imutável. Não salvar conteúdo de notas em registros de execução como verdade secundária; persistir apenas IDs, versões, hashes, propostas isoladas e recibos necessários, sujeitos à privacidade. Um checkpoint de run não edita a nota original; um commit aprovado edita somente o original, com conflito/undo.

**Recuperação de conhecimento:** índice léxico já existente não deve ser descartado. Consultas combinam títulos, texto, links, aliases, tags, propriedades e, se benchmark justificar, semântica vetorial. A pergunta “quais decisões deste projeto contradizem decisões anteriores?” exige identificar afirmações, versões e relações, comparar evidências e **distinguir hipótese de fato**, não apenas mostrar uma lista de notas com palavras similares. A auditoria do modelo é uma hipótese de resposta, nunca evidência de que algo foi encontrado no vault. Múltiplas notas divergentes devem aparecer como divergência.

**Operações semânticas, não regravação desnecessária:** texto parcial usa editor/serviço canônico; objetos ricos usam operações estruturadas e IDs/versões estáveis. `edit_graph` é exemplo existente de operação estruturada, mas sua integração com permissões e UI continua sujeita a teste. `write_note` pode existir para substituição integral explícita, não ser caminho padrão para atualizar propriedade, parágrafo ou linha de tabela. Essa distinção evita perda de conteúdo durante revisões grandes.

**Invariantes de experiência:** não criar modo IDE obrigatório, novo editor de notas, motor paralelo de grafos ou assistente concorrente; manter notebook/editor de código como experiência opcional compatível com qualquer projeto C# permitido (especialização Lunet apenas quando pertinente); simplicidade progressiva, touch e IME acima de displays de logs. Se o executor precisa de host remoto ou codec ausente, o usuário vê necessidade, custo, privacidade, limitação e alternativas **antes** de autorizar.

### 9.10. Função dos sistemas de IA e dos sistemas de publicação (sem tarefas repetidas)

| Domínio | Responsável canônico | Consumidor/integração | Critério que distingue escopos |
|---|---|---|---|
| Fonte editável e IDs | Editor/vault (CST-004/005/151/224) | Agentes, plugins, Publicações | Uma escrita real; nenhum JSON de Publicação guarda cópia da nota |
| Progresso de metas e checklists | Estado canônico da nota e componente nativo (CST-227/228) | Editor, templates, agentes, plugins, Publicações | Valor manual persiste na fonte; percentual de checklist é derivado dos itens reais, não salvo como segunda verdade |
| Busca com evidências | KnowledgeIndex + serviço de recuperação (CST-223) | Assistente, plugins, fontes de Publicação | Nota/trecho/revisão citáveis; sem inventar semântica |
| Proposta e autorização | Capabilities/runtimes (CST-072/199/200/219) | UI, plugin, agente | Mesmo handler e policy em qualquer entrada |
| Continuidade de trabalho | Run/eventos (CST-216/225) | Chat e superfícies de trabalho | Retomar execução ≠ reabrir conversa |
| Skills | Carregador do fluxo editorial (CST-177) + governança global (CST-214) | Agentes de qualquer especialidade | Skill ensina; não concede tool, nem executa script por si |
| Revisão | Núcleo compartilhado de verificadores (CST-213) | QA editorial de Publicações (CST-179) | Validar formato ≠ avaliar direção visual do produto |
| Criatividade multiagente | Fluxo editorial (CST-178) | Coordenação genérica de especialistas (CST-217) | Não duplicar motor de delegação |
| Vídeo | Documento/timeline (CST-170) + encoder (CST-209) | Fluxo MP4 de Publicações (CST-171/172) | Codificador não substitui autoria/timeline; IA não substitui render |
| Impacto incremental | Referências e cache de Publicações (CST-153/181) + impactos gerais (CST-226) | Vault, plugins, relatórios de agente | Invalidar resultado derivado, nunca alterar fonte humana automaticamente |
| Segurança computacional | Executor isolado (CST-201) + grants (CST-219) | Code Mode (CST-202), testes e ferramentas | Programação não obtém acesso nativo ao vault por ser código |

## 10. Referências externas, códigos analisados e temas de investigação

**Critério de leitura:** links abaixo são **orientações para estudo de código/documentação**, não módulos aprovados ou automaticamente compatíveis. A investigação incluiu leitura estática de componentes do Ecosystem (branch `main`) e de exemplos representativos de projetos externos, além de documentação oficial e relatos de problemas de produto. Não houve execução comparativa dessas bibliotecas no Android nesta revisão. Testes, licenças e decisões continuam pendentes (CST-188 e CST-190).

### 10.1. Código atual do Urbe — pontos de partida obrigatórios

| Área | Fonte de código / documento | O que verificar antes de apagar/reutilizar |
| --- | --- | --- |
| Modelo de artefatos | [`Urbe.Core/ArtifactModel.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/ArtifactModel.cs) | Classificação de arquivos versus modelo de Publicação editável |
| Motor legado | [`Urbe.Core/Pages/PageEngine.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/Pages/PageEngine.cs), [`PageRenderer.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/PageRenderer.cs) | Layout HTML existente, sem assumir que seus schemas devem sobreviver |
| Livros | [`PageBookRenderer.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/PageBookRenderer.cs) | Capa, sumário, capítulo, livro impresso e semântica editorial |
| Temas e templates | [`PageThemeCatalog.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/PageThemeCatalog.cs), [`Pages/PageTemplates.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/Pages/PageTemplates.cs) | Design tokens, componentes reaproveitáveis e presets livres |
| Agentes | [`AssistantPageTools.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantPageTools.cs) | Substituir `page_schema`/`write_page`; permissões, diffs, autoria e testes |
| Estúdio a substituir | [`PageStudioView.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Client/PageStudioView.cs), [`MainView.Studio.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Client/MainView.Studio.cs) | Remoção da UX velha, salvamento, prévia, menus, chamadas |
| HTML e export | [`MainView.Pages.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Client/MainView.Pages.cs), [`LiveHtmlPane.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Client/LiveHtmlPane.cs) | Recursos locais, prévia ao vivo e exportação HTML existentes |
| Android | [`AndroidContentViewers.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Android/AndroidContentViewers.cs), [`ContentViewers.cs`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/src/Urbe.Client/ContentViewers.cs) | WebView isolado, impressão, PDF e limites de processo/IME |
| Histórico de PDF | [`PATCHNOTES-beta.38.md`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/csharp/PATCHNOTES-beta.38.md) | Corrige lifecycle e papel, mas reconhece limites do WebView para paginação editorial |
| Requisitos velhos úteis | [`docs/csharp/PAGES-PLAN.md`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/docs/csharp/PAGES-PLAN.md), [`CONSOLIDATION.md`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/urbe/docs/csharp/CONSOLIDATION.md) | Notas por pasta, blocos livres, edição, comentários, modelo, testes; reescrever frente a nova direção |

### 10.2. Sistemas e estudos de referência

| Sistema / fonte | Código ou documentação | Aprendizado / limite para o Urbe |
| --- | --- | --- |
| **Pandoc** | [Readers.hs](https://github.com/jgm/pandoc/blob/main/src/Text/Pandoc/Readers.hs), [Writers.hs](https://github.com/jgm/pandoc/blob/main/src/Text/Pandoc/Writers.hs) | Leitores/escritores com representação semântica intermediária; HTML não é obrigatório para todos os formatos |
| **Quarto** | [Código](https://github.com/quarto-dev/quarto-cli), [render.ts](https://github.com/quarto-dev/quarto-cli/blob/main/src/command/render/render.ts), [formatos](https://quarto.org/docs/reference/formats/), [apresentações](https://quarto.org/docs/presentations/) | Projeto de publicação, obras em vários formatos, narração+dados+proveniência; não exige copiar seu runtime |
| **Pressbooks** | [Código-fonte](https://github.com/pressbooks/pressbooks) | Publicação de livros em web, EPUB e PDF orientada a HTML/CSS; tecnologia WordPress não é modelo obrigatório |
| **Paged.js** | [Código](https://github.com/pagedjs/pagedjs), [especificações impressas](https://pagedjs.org/en/documentation/3-w3c-specifications-for-printing/) | Layout paginado em HTML/CSS, margens, footnotes/fragmentação; MIT (reconferir na versão adotada) |
| **Vivliostyle** | [Código](https://github.com/vivliostyle/vivliostyle.js), [Core](https://github.com/vivliostyle/vivliostyle.js/blob/master/packages/core/README.md) | Tipografia CSS e EPUB; **AGPL-3.0**, revisar implicações antes de incorporar |
| **QuestPDF** | [Código](https://github.com/QuestPDF/QuestPDF), [licença 2026](https://www.questpdf.com/license/community.html) | Composição PDF nativa .NET; licença **source-available** comercial/Community, não OSI, requer revisão jurídica/técnica |
| **reveal.js** | [Código](https://github.com/hakimel/reveal.js), [documentação](https://revealjs.com/) | Slides web acessíveis e interativos, notas de apresentador, navegação |
| **Marp** | [CLI](https://github.com/marp-team/marp-cli), [limitação PPTX](https://github.com/orgs/marp-team/discussions/82) | Exportação de slides: grande fidelidade pode significar slides achatados e não editáveis; modo editável possui limitações |
| **Open XML SDK** | [C# SDK](https://github.com/dotnet/Open-XML-SDK), [PresentationML](https://learn.microsoft.com/pt-br/office/open-xml/presentation/overview) | Gerar/editável PPTX/DOCX estruturalmente sem depender de screenshot |
| **Claude Motion (Anthropic)** | [Ajuda oficial](https://support.claude.com/pt/articles/17454997-comece-com-claude-motion), [anúncio](https://claude.com/de/resources/articles/dashboards-and-motion) | Código que anima conteúdo, correções pontuais via conversa/editor, MP4; **produto proprietário**, referência de experiência, não código a embutir |
| **Runway + Claude Motion** | [Anúncio da integração, 08/10/2026](https://runway.com/news/company-news/runway-claude-motion) | Motion programático + conteúdo generativo externo e edição são modalidades diferentes |
| **Motion Canvas** | [Código](https://github.com/motion-canvas/motion-canvas), [editor/arquitetura](https://motioncanvas.io/docs/), [FFmpeg exporter](https://motioncanvas.io/docs/rendering/video/) | Cenas vetoriais programáveis, editor e prévia, exportador audiovisual; testar licença, maturidade, build Android e limites de sincronização |
| **Remotion** | [Código](https://github.com/remotion-dev/remotion), [produto](https://www.remotion.dev/), [licença e restrições](https://www.remotion.dev/docs/license/pricing) | Vídeo por componentes, dados, assets, render e automação; **não presumir licença de incorporação gratuita** |
| **FFmpeg** | [Código](https://github.com/FFmpeg/FFmpeg), [documentação](https://ffmpeg.org/documentation.html) | Encoders, multiplexação, áudio/legendas e MP4; observar compilação mobile, codecs e componentes GPL/LGPL |
| **Three.js** | [Código](https://github.com/mrdoob/three.js), [docs](https://threejs.org/docs/) | 3D web e animação, útil como adaptador eventual, não motor obrigatório para motion 2D |
| **Blender** | [Código](https://github.com/blender/blender), [manual](https://docs.blender.org/manual/en/latest/) | Cinema 3D programável, referência para fluxos avançados; não presumir execução local viável em Android |
| **eXeLearning** | [Código](https://github.com/exelearning/exelearning), [arquitetura/exportadores](https://github.com/exelearning/exelearning/blob/main/doc/architecture.md), [Scorm12Exporter](https://github.com/exelearning/exelearning/blob/main/src/shared/export/exporters/Scorm12Exporter.ts) | Estratégias de exportação separadas para HTML, SCORM, EPUB, IMS; curso é formato semântico próprio |
| **H5P** | [Tipos de conteúdo](https://h5p.org/content-types-and-applications), [vídeo interativo](https://h5p.org/interactive-video) | Exercícios, interatividade, feedback e distribuição em plataformas educacionais |
| **Agent Skills** | [Especificação aberta](https://agentskills.io/specification), [guia](https://developers.cloudflare.com/agents/skills/) | SKILL.md + assets/scripts/referências; carregamento contextual e expertise portátil, separado de permissões e executores |
| **WCAG / acessibilidade** | [W3C WCAG](https://www.w3.org/WAI/standards-guidelines/wcag/) | Critérios para experiência de leitura/controle/contraste/alternativas; ampliar com verificadores específicos por formato |
| **EPUBCheck** | [Código](https://github.com/w3c/epubcheck) | Validação estrutural de EPUB no processo de exportação |

### 10.3. Evidência da comunidade, problemas recorrentes e como usá-los

- [Marp — discussão "Exported PPTX cannot re-edit"](https://github.com/orgs/marp-team/discussions/82): contraste explícito entre slides rasterizados de alta fidelidade e PPTX editável; validar requisitos do público antes de escolher exportador.
- [Marp CLI — aviso oficial `--pptx-editable`](https://github.com/marp-team/marp-cli/blob/main/README.md): a opção editável é experimental, pode degradar layouts e depende de ferramentas adicionais. **Lição:** não anunciar editabilidade equivalente sem testes.
- [eXeLearning — releases e correções](https://github.com/exelearning/exelearning/releases): registros de bugs de mídia, cache, links internos, fontes e acessibilidade em exportação demonstram necessidade de pacote/manifest/testes reais, não apenas render local.
- [Motion Canvas — documentação de render/exportador](https://motioncanvas.io/docs/rendering/video/): o próprio mantenedor alerta que o exportador FFmpeg pode ter funcionalidades incompletas; investigar integração em vez de presumir produção pronta.
- [Quarto — discussão de formatos](https://quarto.org/docs/presentations/): diversos formatos têm capacidades distintas; recomenda revelar vantagens e limites por destino.
- Issues/discussões e fóruns adicionais de Obsidian, Quarto, Pandoc, Reveal.js, Remotion, Motion Canvas, Vivliostyle, eXeLearning e GitHub devem ser estudados **por problema e versão** antes de comprometer uma decisão, registrando as evidências favoráveis/desfavoráveis. Não confundir debate individual com consenso científico ou amostragem representativa.

### 10.4. Critérios de comparação para cada biblioteca/skill

Antes da adoção, comparar: (a) qualidade de render verificável; (b) precisão semântica e round-trip; (c) edição/binding ao vault; (d) licença e redistribuição comercial; (e) Android/Windows/C#/AOT e tamanho APK; (f) consumo de memória/bateria/tempo; (g) execução offline; (h) segurança e isolamento; (i) maturidade, manutenção, testes e feedback comunitário; (j) expansão por plugins/skills; (k) UX e acessibilidade; (l) documentação e custo de longo prazo. A seleção deve ficar num registro de decisão arquitetural (ADR) com benchmark e alternativa rejeitada, sem impor framework JS ao aplicativo C# porque ele foi usado num demo bonito.

## 11. Registro da revisão documental de 31/10/2026 (revisões 2.1 e 2.2)

- **Motivo:** o antigo Editor de Páginas e a exportação existente não representam a ambição de transformar conhecimento do Urbe em produtos públicos profissionais; decisão explícita de descartá-lo durante beta.
- **Alterações:** seção 1.1 com visão aprovada; reorientação de CST-035, CST-059 a CST-064, CST-082, CST-103, CST-105, CST-135; ajuste de rastreabilidade; novas tarefas CST-149 a CST-192; especificação e pesquisas nas seções 9 e 10.
- **Correção do requisito de vínculo:** Publicação não mantém cópia sincronizada das notas; é uma composição **diretamente vinculada** aos arquivos do vault, com edição bidirecional real. Adaptações têm sua **própria nota editorial** quando não podem substituir o texto de origem; todo texto editável permanece no vault.
- **Integração de IA:** agentes, ferramentas e skills desde a concepção do fluxo, com estudo de Claude Motion, Motion Canvas e Remotion para vídeos, sem depender de modelo fotorrealista; autorização e autoria mantidas.
- **Legado:** os arquivos do vault e estudos de identidade permanecem preservados. Não houve alteração de código no repositório, compilação, migração, criação de PR nem validação de release nesta revisão; tarefas novas continuam abertas.
- **Regra de continuidade:** preservar os demais requisitos existentes do documento; nenhuma tarefa de correção do editor de notas, perda de dados ou estabilidade foi cancelada por esta mudança de Publicações.
- **Revisão 2.2 — absorção integral do FeaturesCheck:** 131 entradas históricas confrontadas com a checklist, matriz linha a linha no 6.2, novos CST-193–197, erros de rastreabilidade corrigidos, critérios de teste fortalecidos e transcrição integral na seção 13. Não houve alteração de código, PR, release ou validação física nesta reconciliação.

## 12. Resultado da reconciliação completa de 31/10/2026

**Arquivos comparados:** `Numa Product Consolidation - Publicacoes.md` (revisão documental 2.1) e `FeaturesCheck.md` (anexo do usuário). Este documento 2.2 é a consolidação resultante; o arquivo histórico não precisa ser mantido como documento normativo paralelo.

- **Cobertura:** 131 checkboxes do FeaturesCheck, cada qual com linha de origem, estado literal e destino CST na tabela 6.2. As demais linhas (explicações, cenários, observações, atualizações, PRs e dados de marca) estão preservadas integralmente na seção 13.
- **Erros reais corrigidos:** mapeamento do crash do carrossel para CST-111 (não apenas geração de imagem); memória tipada para os itens de memória CST-085/086/088/133 (não IA local CST-116–118); inclusão de rastreabilidade de transclusões em CST-040/041/064.
- **Lacunas de escopo transformadas em tarefas:** CST-193 protege edições longas por agentes; CST-194 detalha anexos e EXIF; CST-195 exige prova física das 13 entregas da beta.37; CST-196 garante atualização dos links Markdown normais; CST-197 cobre tags sem truncamento e Abrir ao lado. Requisitos coincidentes foram fortalecidos nos critérios CST existentes.
- **Conflito deliberado, resolvido:** o FeaturesCheck histórico contém `Editor de páginas robusto e integrado` como direção anterior. A decisão superior, posterior e explícita do proprietário **substitui essa superfície por Publicações**. Permanecem exigências úteis de criação contextual, fontes vinculadas, composição livre, comentários, modelos, identidade e prévia; não permanece obrigação de reintroduzir a UI legada.
- **Estados históricos não são testes:** 73 caixas `[x]` e 58 caixas `[ ]` refletem apenas o FeaturesCheck enviado; todos os itens novos e os itens CST ainda abertos continuam `[ ]`, salvo posterior verificação com evidências. Frases `Falta: conferir no aparelho` continuam vinculantes.
- **Material preservado:** a transcrição integral está a seguir. Seus carimbos `{ia:...}` e registros de PR permanecem como recebidos; nenhum foi normalizado ou removido da fonte histórica. As ressalvas do texto original, inclusive sobre resultados não confirmados, continuam aplicáveis.

## 13. Anexo histórico integral — FeaturesCheck original (fonte absorvida)

Este bloco é uma **cópia textual fiel do arquivo recebido**, conservada para auditoria e recuperação de detalhes. **Não é uma segunda especificação ativa**. Para executar tarefas, use as seções normativas 1–12, especialmente 4 e 6.2. A decisão posterior sobre Publicações prevalece sobre pedidos antigos de manter o Editor de Páginas. Qualquer `[x]` deste anexo representa o estado histórico registrado, nunca teste de aceitação comprovado por esta revisão.

- Arquivo: `FeaturesCheck.md`
- Bytes UTF-8: 50498
- SHA-256 do original: `5eba41e0bf13d25edbef4441429a28938370d67806751153a6848c4e080c754a`

~~~~~~~~markdown
# FeaturesCheck

> 🔔 **Notificação — objetivo deste arquivo:** registrar features implementadas e features a serem implementadas. Cada feature deve ter seu próprio checkbox: **marcado (`[x]`) significa que o escopo descrito está implementado; desmarcado (`[ ]`) significa que o escopo ainda está pendente, mesmo que parte dele já exista**. Quando houver implementação parcial, registre no texto o que está pronto e o que falta. Relatos de problemas podem precisar de verificação, e uma implementação registrada não garante funcionamento universal. {ia:"gpt-luna-latest"}

## Prioridades gerais e experiência de uso

- [ ] **Desempenho e fluidez:** manter respostas rápidas e consistentes, sem travamentos ou degradação conforme o vault cresce. Priorizar editores de notas e páginas e fluxos de importação, criação, exclusão e captura rápida.
- [ ] **Métrica de execução útil do Assistente:** medir desempenho como tempo + trabalho/retrabalho + qualidade verificada, não apenas “respondeu rápido” nem número bruto de chamadas. **Implementado, conforme informado pelo usuário:** contadores de leituras reaproveitadas, gravações sem alteração e contexto economizado. **Pendente:** telemetria completa, incluindo tempos, percentis, retrabalho, conclusão e qualidade verificada. Em tarefas com ferramentas, registrar (quando disponível) tempo total e tempo até o primeiro resultado útil; quantidade e tipo de chamadas; falhas/rejeições e novas tentativas; releituras/buscas repetidas; edições concluídas; verificações pós-edição; e se o pedido foi resolvido. {ia:"gpt-6-luna"} Distinguir espera de rede/provedor, execução de ferramentas e raciocínio do agente quando a instrumentação conseguir separá-los; não inferir causas ou durações que não foram medidas. Avaliar em amostras por classe de tarefa e complexidade, comparando mediana e percentis de latência junto a retrabalho e taxa de conclusão/verificação. Não otimizar para reduzir chamadas à custa de contexto insuficiente, qualidade ou segurança. **Privacidade:** não registrar conteúdo de notas, prompts ou dados pessoais por padrão; preferir telemetria agregada, minimizada e consentida, com política de retenção e opção de desativação. {ia:"gpt-6-luna"} **Atualização 30/10:** tempos (total, primeiro resultado útil, modelo/rede, ferramentas, espera por aprovação), chamadas, falhas, repetições, edições e verificações por resposta, com mediana e p90, sem conteúdo das notas (CST-094/095/096) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** amostras por classe de tarefa com modelos reais.
- [ ] **Fluxo de trabalho:** reduzir interrupções e perda de foco durante a produção; considerar acessibilidade, eficiência, atalhos, personalização e rapidez como prioridades.
- [ ] **Experiência móvel:** revisar adaptação visual, alinhamento, áreas de toque, teclado, cursor e rolagem. Verificar que o editor abra o teclado com confiabilidade e permita tocar no ponto desejado sem toques repetidos. **Atualização 30/10:** áreas de toque das telas principais e da Personalização com pelo menos 40 px e contraste WCAG AA nos temas (CST-105) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** teclado, cursor e rolagem no aparelho.
- [x] **Navegação radial no celular:** substituir as abas por um menu radial no canto inferior direito, facilitando a navegação e limpando o layout. **Atualização 30/10:** implementado (CST-043) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.

### Caso de análise — revisão longa do sistema de protoidioma (25/10/2026)

Observação qualitativa deste episódio, sem cronômetros por chamada: a revisão incluiu releituras e verificações repetidas, chamadas de edição rejeitadas por não corresponderem ao trecho exato, e retrabalho para corrigir anotações editoriais introduzidas durante as tentativas de limpeza. Isso mostra categorias que a telemetria proposta deve tornar visíveis: chamadas sem alteração persistida, ciclos de leitura–edição repetidos, falhas/rejeições e verificações posteriores que detectam defeitos. Também houve valor em algumas verificações que encontraram inconsistências reais, então contagem de operações isolada não mede desperdício. Não há nesta conversa timestamps nem duração por chamada; não se afirma quanto tempo cada fator consumiu nem que cada etapa tenha sido inútil. Usar como exemplo exploratório, não como benchmark ou prova de frequência geral.

## Editor de notas e escrita  

> **Critério crítico de produto, definido pelo usuário:** o editor é parte central do produto. Enquanto não for plenamente confiável — sem falhas que interrompam a escrita, causem perda de texto ou comprometam a leitura e edição do conteúdo — **não há produto pronto para lançamento**. Os problemas abaixo são bloqueadores, não detalhes cosméticos. {ia:"gpt-6-luna-pro"}

- [ ] **Renderização de matemática no editor:** o usuário relata que fórmulas frequentemente se sobrepõem ao texto ou ficam cortadas, incluindo equações. Corrigir e verificar a apresentação sem sobreposição nem recorte em leitura e edição. {ia:"gpt-6-luna-pro"}
- [ ] **Menu radial sobre o controle de leitura/edição:** o usuário relata que o menu radial se sobrepõe ao botão de alternância entre leitura e edição, dificultando ou impedindo seu uso. Ajustar o posicionamento e verificar que ambos os controles permaneçam acessíveis em telas e situações pertinentes. {ia:"gpt-6-luna-pro"}
- [ ] **Interrupções e perda de texto durante a edição — crítico:** o usuário relata que isso aconteceu incontáveis vezes: ao tentar clicar, uma interação inesperada interrompe a escrita e ele perde o texto, precisando reescrevê-lo. Clique falso, atualização do editor ou outra causa são possibilidades mencionadas, não causas confirmadas. Investigar, reproduzir e garantir preservação e recuperação do rascunho após toques, atualizações e interrupções. Este problema bloqueia a prontidão do produto enquanto não estiver resolvido. {ia:"gpt-6-luna-pro"}
- [ ] **Numeração de linhas no editor:** mostrar na lateral a numeração das linhas do conteúdo, para facilitar identificar e referenciar uma linha específica. A numeração deve acompanhar corretamente o conteúdo durante a edição e a navegação. Ver **CST-147** em [[Numa Product Consolidation]]. {ia:"gpt-6-luna-pro"}

- [ ] **Confiabilidade do editor:** verificar e corrigir seleção de palavras por toque duplo, comportamento do cursor e dos blocos, rolagem indesejada ao entrar e sair de blocos, texto que aparece em negrito sem intenção e demora para salvar ou sair.
- [x] **Estado da barra de formatação:** os botões refletem a formatação encontrada no Markdown sob o cursor, nos modos Visual e Fonte. A correção existe; eventuais inconsistências ainda precisam ser reproduzidas.
- [x] **Código em linha e blocos:** inserir código inline sem acrescentar automaticamente a palavra “código”, alternar entre código e texto e criar um bloco de código vazio.
- [x] **Comentários em notas:** permitem comentar uma palavra ou seleção, mostrar o marcador 💬, consultar comentários existentes e acrescentar novos.
- [x] **Comentários em páginas:** a prévia do estúdio permite consultar e adicionar comentários ao conteúdo da página ou à nota incorporada. Comentários ficam de fora da exportação e da impressão.
- [x] **Revisão de alterações da IA:** o cartão de aprovação oferece “Editar”, permitindo modificar o texto proposto antes de aprovar sua gravação.
- [x] **Marca de autoria da IA:** **Implementado:** aplicação automática de `{ia:"modelo"}` nas operações de escrita e apresentação discreta no editor. **Pendente:** marca por bloco, proteção contra remoção/alteração, distinção entre geração e simples correção e substituição correta de marcas antigas. Ver detalhes em **Assistente de IA e agentes**. {ia:"gpt-6-luna"} **Atualização 30/10:** selo por bloco, protegido no Visual e na Fonte, correção não vira autoria, regeneração substitui o selo (CST-027 a 035) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Painel de referência fixado:** permite fixar a seleção ou a nota inteira no topo do editor, com rolagem própria e permanência ao trocar de nota.
- [x] **Tags estilizadas:** parágrafos como `#Todo:` recebem apresentação própria, com rótulo, fundo e cor. Há configuração de cores em Personalização → Tags. {ia:"gpt-6-luna"}
- [x] **Modelos reutilizáveis de notas:** **Implementado:** criação a partir de modelos Markdown, preenchimento de título/data/campos e formulário completo para templates reconhecidos. **Pendente:** extensão própria para identificar templates e possibilidade de fechar o formulário e continuar preenchendo a ficha diretamente no editor. **Observação:** campos escritos apenas entre colchetes não equivalem automaticamente a campos de formulário configurados. {ia:"gpt-6-luna"} **Atualização 30/10:** extensão própria `.modelo.md`, preencher a ficha no editor depois de fechar o formulário, `[Texto]` convertido em campos e até 128 campos (CST-044, 046, 050, 045) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Blocos com horário:** mostrar em cada bloco do editor o horário de sua última edição. **Atualização 30/10:** horário da última edição em cada bloco, opcional em Personalização → Editor (CST-025) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Relógio, cronômetro e timer:** **Implementado:** plugin nativo `ClockPlugin`, com relógio, iniciar/pausar/zerar cronômetro e temporizador. **Pendente:** verificar o relato de que o recurso não apareceu no editor e sua disponibilidade no fluxo esperado pelo usuário. {ia:"gpt-6-luna"} **Atualização 30/10:** conferido que o plugin funciona no editor; vem desligado por padrão e é ligado em Personalização → Plugins (CST-052). **Falta:** decidir se deve vir ligado.
- [ ] **Notas com blocos aninháveis:** **Implementado:** contêineres Markdown `:::tipo ... :::`, inclusive aninhados, com apresentação no Visual e suporte nas páginas. **Pendente:** sistema geral de componentes reutilizáveis e aninháveis na amplitude proposta. {ia:"gpt-6-luna"}
- [x] **Criar nota pelo editor:** verificar e corrigir a criação de uma nova nota iniciada diretamente no editor, relatada como não funcional. **Atualização 30/10:** corrigido (CST-021) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Barra de ferramentas móvel:** em telas estreitas, a barra fica na parte inferior do editor, junto à região do teclado, com rolagem horizontal. {ia:"gpt-6-luna"}
- [ ] **Ferramentas de matemática e visualização:** ampliar o suporte a matemática e permitir visualizar, no próprio editor, gráficos e fenômenos de física definidos por equações. Avaliar painéis com gráficos cartesianos e previews em tempo real configurados pelo usuário. **Atualização 30/10:** gráficos de funções no editor, com parâmetros que se arrastam (CST-069). **Falta:** física e prévia ao vivo enquanto se digita.

## Assistente de IA e agentes

- [x] **Instruções por blocos na criação de agentes:** disponibilizar a configuração de instruções por blocos na aba **Criar novo agente**, não somente na aba **Configurar** após a criação. Cada bloco deve ter um campo de título opcional e um campo para o texto da instrução. **Atualização 30/10:** disponível já na criação (CST-070) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Planejamento dos agentes:** `update_plan` adaptativo e instruções para investigar contexto, riscos, dependências, critérios de aceitação e perguntar uma questão relevante por vez estão implementados. **Pendente:** verificar a qualidade e consistência desse comportamento nos diferentes modelos. {ia:"gpt-6-luna"}
- [x] **Caixa de perguntas adaptável:** a ferramenta `ask_user` e a interação de pergunta/resposta estão implementadas. **Pendente:** indicador de dúvidas resolvidas e nível de compreensão, com atualização conforme surgem ou desaparecem dúvidas. {ia:"gpt-6-luna"} **Atualização 30/10:** o indicador de dúvidas resolvidas e compreensão foi feito (CST-115).
- [x] **Comandos inline:** a sintaxe `&NomeDoAgente&: pedido` é reconhecida no editor. Há execução contextual, sugestões de alteração com aceitar/recusar e acesso pelo miniassistente. {ia:"gpt-6-luna"}
- [x] **Nomes personalizados dos agentes:** há nomes editáveis e ferramenta `set_my_name`, com orientação para confirmar uma mudança sugerida durante a conversa. Os nomes são usados nos comandos inline.
- [x] **Agentes padrão opcionais:** “Novo agente” oferece modelos predefinidos como opções. Eles não precisam ser adicionados automaticamente. {ia:"gpt-6-luna"} 
- [x] **Contexto de conversas recentes:** **Implementado:** histórico separado por agente, retomada explícita e memória episódica de execuções que alteraram arquivos. **Pendente:** consulta geral a resumos de conversas recentes e leitura detalhada de conversas específicas como ferramentas do agente. {ia:"gpt-6-luna"} **Atualização 30/10:** ferramentas para consultar resumos de conversas recentes e ler uma conversa específica, só do próprio agente (CST-089) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [ ] **Continuidade ao abrir um agente:** o usuário relata que criar um chat novo toda vez que abre o agente não está funcionando bem. O app deve distinguir quando o usuário ainda está utilizando uma conversa e mantê-la aberta/retomável, em vez de descartá-la ou substituí-la; quando não estiver em uso, avaliar se é mais prático iniciar uma conversa nova, sem perder o histórico nem rascunhos relevantes. Definir e verificar como o sistema reconhece esses estados e preserva a intenção do usuário. {ia:"gpt-6-luna-pro"}
- [x] **Consulta entre agentes — continuidade por múltiplos turnos:** a consulta permite acompanhar o especialista em múltiplos turnos. Essa continuidade não encaminha automaticamente perguntas ou aprovações pelo chat solicitante; quando o especialista precisa de resposta, o código orienta abrir a conversa dele. {ia:"gpt-6-luna"}
- [ ] **Plugins integrados aos agentes:** permitir que plugins autorizados convoquem agentes já configurados, em vez de depender somente de funções predefinidas. O plugin define a experiência e a tarefa; o agente investiga notas e demais fontes pertinentes, raciocina e utiliza ferramentas permitidas. O plugin pode fixar um agente ou deixar o usuário escolher, e deve delimitar o contexto (projeto, pasta, nota ou conjunto de informações). Resultados devem ser estruturados para apresentação e reutilização pelo plugin; a execução de mudanças é opcional, separada da recomendação e sujeita às permissões e aprovações existentes, sem conceder acesso irrestrito ao vault. A interface deve mostrar progresso, permitir interrupção e disponibilizar o resultado. Exemplo de experiência: **Próxima tarefa**, que analisa o estado e as dependências de um projeto e recomenda uma próxima ação concreta com justificativa, escopo e critério de conclusão. A arquitetura deve permitir diversos plugins e, futuramente, coordenação de agentes, sem exigir IA própria para cada plugin. Ver **CST-146** em [[Numa Product Consolidation]]. {ia:"gpt-6-luna-pro"}
- [ ] **Memórias dos agentes — investigar:** **Implementado:** memória tipada, escopo privado por agente, recuperação contextual e ferramentas de gerenciamento. **Pendente:** investigar a visibilidade nas configurações de cada agente e a confiabilidade da recuperação relatada pelo usuário. {ia:"gpt-6-luna"} **Atualização 30/10:** o gerenciador diz quais lembranças são privadas de cada agente e quais são compartilhadas, com filtros, e se foram registradas pelo app, declaradas pelo agente, ditas por você ou verificadas (CST-085, 088) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** investigar a recuperação com exemplos reais.
- [x] **Arquivos no chat de IA:** permite anexar arquivos do dispositivo e notas do vault, com apresentação e remoção dos anexos antes do envio. {ia:"gpt-6-luna"}
- [x] **Análise de imagens:** imagens podem ser enviadas aos provedores multimodais. O funcionamento depende de um modelo compatível. {ia:"gpt-6-luna"}
- [x] **Separação entre execução, revisão e resposta final:** manter interno o rascunho usado na revisão; não deixá-lo vazar para a conversa nem substituir ou ocultar a resposta final do agente. Relato do usuário: após o agente realizar a tarefa e entregar um resultado, o fluxo de ferramentas exige uma revisão; a resposta final ao usuário acaba falando apenas da revisão (por exemplo, que a tarefa foi revisada e a qualidade confirmada), sem dizer claramente o que o agente fez ou entregou. Verificar o fluxo e assegurar que a revisão permaneça interna e que a resposta final comunique de forma útil o trabalho realmente realizado, sem alegar ações que não ocorreram. {ia:"gpt-6-luna"} **Atualização 30/10:** a revisão fica interna e a resposta final diz o que foi feito (CST-079) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Marca d’água de autoria em blocos gerados por IA:** aplicar a marca em cada bloco que a IA criar ou reescrever do zero, não apenas no último. Distinguir conteúdo gerado por IA de conteúdo do usuário. Não aplicar quando a solicitação for apenas correção ortográfica nem a conteúdo de propriedade intelectual do usuário; corrigir ortografia não deve ser tratado como geração de conteúdo por IA. Considerar a mesma distinção quando a IA apenas corrige ou transforma conteúdo do usuário sem gerar conteúdo novo. A marca `{ia:modelo}` é um selo de autoria gerenciado pelo aplicativo, não conteúdo editável pelo agente: deve ser impossível alterá-la ou removê-la tanto na fonte quanto no editor visual. Quando uma operação substituir/regerar conteúdo de IA, o aplicativo deve substituir a marca antiga pela nova, sem acumular marcas. Não instruir agentes a editar o selo; a tentativa de removê-lo já levou agentes a entrar em loop. {ia:"gpt-6-luna"} **Atualização 30/10:** implementado como na Marca de autoria acima (CST-027 a 035) (beta.37; ver CONSOLIDATION.md no Ecosystem).
- [ ] **Geração de imagens:** melhorar e expandir as ferramentas de geração de imagem e facilitar que modelos mais simples obtenham bons resultados com ferramentas adequadas. {ia:"gpt-6-luna"} {ia:"gpt-6-luna"} {ia:"gpt-6-luna"}
- [ ] **API de IA própria e compartilhada:** avaliar transformar a API/sistema de IA do Urbe em uma plataforma própria, trabalhada e atualizada independentemente, que distribua funcionalidades automaticamente aos aplicativos que a utilizarem. Definir arquitetura e escopo com o responsável pelo desenvolvimento.
- [x] **Conhecimento completo das funcionalidades do Urbe pelos agentes:** criar e manter uma referência abrangente, atualizada junto com o aplicativo e consultável pelos agentes, que cubra todas as funcionalidades, comandos, ferramentas e fluxos de uso do Urbe. O agente deve ser especialmente especialista no editor de notas: conhecer seus recursos de edição, comandos e formas de uso, inclusive os menos óbvios e particulares do Urbe, e conseguir orientar o usuário e escolher a ferramenta adequada durante uma tarefa. Não presumir que um modelo de IA conheça recursos específicos do aplicativo apenas por conhecer outros editores ou Markdown; o conhecimento deve acompanhar as mudanças do Urbe. {ia:"gpt-6-luna"} **Atualização 30/10:** ferramenta `urbe_guide`, que consulta o tutorial do app, atualizado junto com ele (CST-075) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir com modelos reais.
- [x] **Idioma padrão do sistema para agentes:** identificar o idioma do sistema e usá-lo como idioma padrão de resposta dos agentes. Permitir que o usuário escolha outro idioma nas configurações. Os agentes devem responder, a priori, no idioma configurado, mas seguir instruções explícitas do usuário para usar outro idioma. {ia:"gpt-6-luna"} **Atualização 30/10:** idioma do sistema como padrão, configurável, e pedidos explícitos respeitados (CST-077) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.

## Captura rápida, áudio e transcrição

- [ ] **Widget de voz:** **Implementado:** no Android, gravação por serviço, notificação, transcrição, fila persistida e tentativa de recuperação de gravações pendentes. **Pendente:** experiência equivalente em todas as plataformas, integração com tela de bloqueio e validação das condições de desligamento/interrupção. **Observação:** no Windows, o fluxo existente abre uma caixa de nota rápida e utiliza o ditado do sistema. {ia:"gpt-6-luna"}
- [ ] **Gravações longas e transcrição em notas:** permitir importar ou gravar áudio longo, como uma aula, transcrevê-lo e usar IA para organizar a transcrição em uma nota pronta para revisão. **Atualização 30/10:** transcrever um áudio do vault (uma aula) numa nota ao lado, com os horários (CST-058). **Falta:** a IA organizar a transcrição e a validação no aparelho.
- [x] **Duração da gravação:** rever o limite da gravação rápida, relatado como curto demais e causador de pressa que impede organizar o pensamento. **Atualização 30/10:** gravação rápida sem pressa, até 15 min, com aviso antes do fim (CST-056) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Gravações consecutivas:** permitir iniciar outra anotação rápida sem esperar que a anterior termine de ser salva e sem exigir que o aplicativo principal seja aberto. **Atualização 30/10:** nova gravação sem esperar a anterior nem abrir o app (CST-055) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Checkbox obrigatório em todas as notas rápidas:** toda anotação rápida deve ser criada como item de checklist com checkbox, independentemente da origem (usuário, agente de IA ou widget) e do conteúdo do bloco. A regra é obrigatória mesmo se quem registrar a nota esquecer de incluir o checkbox manualmente. {ia:"gpt-6-luna"} **Atualização 30/10:** toda nota rápida é item de checklist, venha do widget, do app ou do agente (CST-053) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Organização mensal das notas rápidas:** novas entradas são agrupadas em `Notas rápidas/aaaa-mm.md`, com seções por dia e horário em cada entrada. Arquivos diários antigos são preservados. {ia:"gpt-6-luna"}

## Explorador, organização e navegação

- [ ] **Explorador e movimentação:** **Implementado:** listagem de diferentes tipos de arquivo com extensão/ícone, opções de pasta, movimentação de pastas e notas e arrastar arquivos para pastas. **Pendente:** verificar os defeitos relatados e completar a busca no seletor de destino. {ia:"gpt-6-luna"} **Atualização 30/10:** busca no seletor Mover para, com teste; Compartilhar arquivos e notas com outro app (CST-039, 036) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** verificar os defeitos relatados no aparelho.
- [x] **Navegação móvel radial:** substituir as abas no celular por um menu radial no canto inferior direito; ver também a proposta em **Prioridades gerais e experiência de uso**. **Atualização 30/10:** implementado (CST-043) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.
- [x] **Tags herdadas por projeto:** avaliar permitir que uma pasta de projeto defina tags aplicáveis às pastas e arquivos dentro dela. Exemplo: o projeto Droerni poderia ter `#worldbuilding`, herdada pelo seu conteúdo, para catalogar além da navegação por diretórios. Investigar também se propriedades gerais poderiam ser herdadas por projeto e como se relacionariam com tags; comportamento e implementação ainda precisam ser definidos. {ia:"gpt-6-luna"} **Atualização 30/10:** as tags da nota da pasta (`Droerni/Droerni.md`) valem para tudo dentro dela, com a origem visível; recusáveis por nota (DEC-0058, CST-042) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** propriedades herdadas além das tags.

## Páginas e produção de artefatos

- [ ] **Editor de páginas robusto e integrado:** tratar o editor de páginas como expansão complementar do editor de notas e do Explorador, com ferramentas flexíveis e fluidas para diferentes modelos, em vez de limitar ferramentas aos modelos existentes. Ampliar personalização, integração com o vault e inteligência para trabalhar com notas.
- [x] **Composições dentro de Páginas:** a interface separada foi retirada. Existe conversão das composições antigas em páginas, preservando os arquivos de origem. {ia:"gpt-6-luna"}
- [ ] **Desempenho e teste móvel das páginas:** verificar rapidez, usabilidade e adaptação ao celular; o relato original informa que não foi possível testá-las por completo.
- [x] **Imagens que não aparecem nas páginas:** investigar o relato do usuário de que, ao criar uma página e adicionar uma imagem, ela não foi exibida na página. Verificar resolução do caminho/diretório da imagem, valor de `src`, tratamento de caminhos relativos e mecanismo de carregamento/renderização; diretório ou `src` são hipóteses a investigar, não causas confirmadas. Registrar um caso reproduzível e conferir se a imagem aparece na prévia e na página final. {ia:"gpt-6-luna"} **Atualização 30/10:** causa encontrada e corrigida (CST-059) (beta.37; ver CONSOLIDATION.md no Ecosystem). **Falta:** conferir no aparelho.

## Tutorial e documentação

- [x] **Tutorial em painel dedicado:** há índice por seções, pesquisa, navegação entre artigos e leitura dentro do aplicativo. {ia:"gpt-6-luna"}
- [x] **Tutorial somente leitura:** a documentação é exibida sem editar os artigos originais nem criar automaticamente notas de tutorial no vault. Existe documentação específica do aplicativo nativo. {ia:"gpt-6-luna"}

## Configuração inicial e abertura do aplicativo

- [x] **Configuração inicial:** há fluxo para escolher a pasta do vault, aparência e configuração do Assistente, com opção de adiar e possibilidade de refazer depois. {ia:"gpt-6-luna"}
- [ ] **Aberturas seguintes:** **Implementado:** restauração da última nota e apresentação antecipada da estrutura conhecida enquanto o vault é lido. **Pendente:** abertura imediata da nota antes da leitura completa e confirmação de desempenho com vaults grandes no aparelho. {ia:"gpt-6-luna"}
- [ ] **Otimização das operações:** **Implementado:** salvamento assíncrono, operações de lixeira em lote e otimizações de leitura/escrita. **Pendente:** validar os objetivos gerais de fluidez, importação, criação e captura rápida no dispositivo real. {ia:"gpt-6-luna"}
- [x] **Instruções personalizadas por `AGENTS.md`:** `Personalização/AGENTS.md` é lido pelo Assistente, com instruções gerais e seções por modelo. Essa capacidade existe além das instruções próprias de cada agente. {ia:"gpt-6-luna"}

## Relatos que precisam de nova verificação

- [ ] **Desempenho e latência:** o usuário relata que os tempos de resposta (ms) estão ridiculamente altos em praticamente tudo no aplicativo — abrir, salvar, renderizar, enviar formulários e navegar, entre outras ações — e que isso precisa mudar. Verificar a latência nas operações e refinar o desempenho. {ia:"gpt-6-luna"}
- [ ] **Ação Enter/Enviar e foco do teclado do dispositivo — editor e IA:** o usuário relata que, ao abrir o teclado e digitar, o botão de ação exibido no lugar habitual do Enter se comporta como envio/conclusão, embora nesse contexto devesse inserir uma nova linha e manter o teclado aberto para continuar escrevendo. Ao tocá-lo, a linha é criada como desejado, mas a digitação é tratada como concluída e o teclado fecha. O mesmo comportamento ocorre na entrada da IA. Além disso, às vezes, sem padrão identificado, tocar numa linha para continuar digitando não abre o teclado; isso acontece frequentemente depois de usar o botão do teclado para inserir a próxima linha. A ação deve respeitar o contexto: inserir nova linha e permitir continuar digitando quando esse for o uso esperado; só deve funcionar como Enter de envio quando isso for coerente com o campo e a interação. Investigar e corrigir a configuração/comportamento do IME e o foco após a quebra de linha sem presumir a causa técnica, e verificar em ambos os fluxos. {ia:"gpt-6-luna-pro"}
- [ ] **Editor nativo:** verificar dificuldade relatada de seleção, posicionamento do cursor, foco do teclado, rolagem e salvamento.
- [ ] **Crash durante execução de agente com pesquisa e geração de imagens:** investigar o relato de que o aplicativo fechou quando um agente configurado com o prompt de carrossel de notícias chegou perto do fim da tarefa. O prompt pede pesquisa e verificação de notícias, uma capa e até nove slides como imagens separadas, possíveis novas gerações para corrigir texto e resposta final contendo somente as imagens. Isso torna a execução potencialmente longa e pode envolver várias chamadas de ferramentas e resultados de imagem; são hipóteses para investigação, não causas confirmadas. Examinar histórico/telemetria das ferramentas e logs de crash para identificar a etapa e distinguir falha do aplicativo, do provedor ou da ferramenta; verificar número de gerações e tentativas, tamanho dos resultados, apresentação das imagens e possibilidade de retomar uma execução interrompida. O registro existente de correção de crash com argumentos/planos malformados (PR #763; ver **Implementações registradas**) não confirma relação com este relato. **Atualização 30/10:** causa provável encontrada e corrigida; a pesquisa também não para mais em 12 buscas e 20 páginas (CST-111, 083) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta confirmar no aparelho com o agente do carrossel.
- [ ] **Avisos repetidos “Urbe não está respondendo” durante uso da IA (beta.38):** o usuário relata que, enquanto usa a IA e trabalha no vault, o Android exibe repetidamente “Urbe não está respondendo”, embora o app aparentemente continue responsivo. Ao tocar em “Aguardar”, consegue continuar normalmente, mas o aviso volta a aparecer até fechar o app. O usuário suspeita que possa ter relação com o uso da IA, mas essa associação e a causa técnica ainda não foram confirmadas. Investigar e reproduzir na beta.38, correlacionando os avisos com a atividade da IA e do vault, e examinar logs/diagnósticos para distinguir bloqueio real da thread principal, lentidão momentânea e falso positivo do sistema; verificar se ocorre também sem IA e registrar aparelho, duração e passos quando disponíveis. {ia:"gpt-6-luna-pro"}
- [ ] **Travamento ao expandir etapas e ferramentas no chat:** o usuário relata que expandir no chat muitos passos e ferramentas utilizados durante a execução pode fazer o aplicativo travar. Investigar e reproduzir o comportamento, verificando se a quantidade de etapas/ferramentas ou a renderização do painel expandido contribui; essas são hipóteses, não causas confirmadas. **Atualização 30/10:** corrigido no código (CST-112) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta medir no aparelho.
- [ ] **Seleção de ferramentas:** verificar a inconsistência relatada. {ia:"gpt-6-luna"}
- [ ] **Widget de voz:** {ia:"gpt-6-luna"} verificar otimização e UX; o relato indica que chegou a funcionar, mas foi considerado insuficiente nesses aspectos.
- [ ] **Explorador:** verificar os problemas relatados na seleção, nas opções de pasta e no arrastar e soltar.
- [ ] **Seletores de links e incorporações de arquivos:** verificar o relato de que, ao escrever `[[arquivo]]` ou `![[arquivo]]`, a lista de sugestões mostra apenas notas `.md`; permitir localizar e linkar qualquer tipo de arquivo. Distinguir a possibilidade de criar o link do suporte para exibir uma incorporação: há registro de suporte a imagens, HTML, PDF, áudio, vídeo e código nos PRs #705 e #706 (ver **Implementações registradas**), mas isso não confirma que todos os tipos de arquivo sejam listados pelos seletores. {ia:"gpt-6-luna"} **Atualização 30/10:** corrigido: `[[` e `![[` sugerem qualquer arquivo do vault (CST-040, 041) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta conferir no aparelho.
- [ ] **Experiência móvel:** verificar desalinhamento e falta de otimização relatados. {ia:"gpt-6-luna"}
- [ ] **Botão Voltar no Android:** o usuário relata que a função/botão Voltar fecha o aplicativo em vez de retornar pelas abas abertas. Investigar e corrigir a navegação para que o botão volte à aba anterior quando houver histórico de abas; verificar também o comportamento quando não houver aba anterior. {ia:"gpt-6-luna"} **Atualização 30/10:** corrigido (CST-010) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta conferir no aparelho.
- [ ] **Formulário — tipos de campo e seleção múltipla (beta 31; PR #763):** no teste do usuário, a validação de tipo funcionou parcialmente: ao digitar letras em um campo numérico, a validação impediu a entrada, mas ainda assim permitiu inserir o valor inválido. No campo de seleção múltipla, o usuário só conseguiu selecionar um item. A ferramenta é utilizável no geral, mas esses comportamentos precisam ser verificados e refinados. **Atualização 30/10:** corrigido (CST-047) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta conferir no aparelho com o Gboard.
- [ ] **Atualização visual da conversa de IA:** o usuário relata que, às vezes, uma ação/input é processado, mas a tela não reflete que aconteceu, como se a interface não atualizasse. Solução temporária observada: fechar e reabrir a aba de IA e abrir novamente a conversa; então o conteúdo aparece atualizado. Investigar sincronização/atualização da interface e verificar se o estado real da conversa é preservado enquanto a visualização fica desatualizada. Relato ainda não verificado tecnicamente. {ia:"gpt-6-luna"} **Atualização 30/10:** corrigido em duas partes (CST-008) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta conferir no aparelho.
- [ ] **Encaminhamento de perguntas e aprovações entre agentes — limitação conhecida:** a consulta e o acompanhamento por múltiplos turnos estão implementados, mas não encaminham automaticamente perguntas ou aprovações pelo chat solicitante. Quando o especialista precisa de resposta, o código orienta o usuário a abrir a conversa dele. O encaminhamento e retorno automáticos pelo chat solicitante permanecem fora do comportamento implementado; esta linha registra essa lacuna, não uma falha na continuidade já existente. {ia:"gpt-6-luna"} **Atualização 30/10:** feito: perguntas, formulários e aprovações do especialista aparecem na conversa que pediu, e quem pediu fica sabendo o que o especialista mudou no vault (CST-091, 092) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta conferir no aparelho.
- [ ] **Verificação dos formulários no chat:** relatos específicos de validação numérica e seleção múltipla permanecem pendentes de reprodução e verificação; a ferramenta de formulários continua registrada como implementada. {ia:"gpt-6-luna"} {ia:"gpt-6-luna"}
- [ ] **Confiabilidade de edições longas pelo agente:** em uma revisão dos templates de dicionário, o agente repetiu edições após falhas de correspondência exata, recebeu leituras repetidas sem confirmar o estado mais recente, reescreveu arquivos inteiros com conteúdo desatualizado e fez correções adicionais que criaram inconsistências. Ao final, informou contagens de verbetes incorretas e declarou a revisão concluída sem validação confiável. Investigar e reproduzir; melhorar a atualização/leitura do estado do arquivo após gravações, impedir substituições integrais baseadas em conteúdo desatualizado, exigir conferência do diff e das contagens antes de concluir e interromper/replanejar após erros repetidos de ferramenta. Episódio envolveu [[TEMPLATE - DICIONARIO A]], [[TEMPLATE - DICIONARIO AB]], [[TEMPLATE - DICIONARIO ABC]] e [[BASE - Inventários e Transformações]]. Um caso observado não confirma, por si só, frequência geral nem causa técnica. {ia:"gpt-6-luna"} **Atualização 30/10:** corrigido no código (CST-098) (beta.37; ver CONSOLIDATION.md no Ecosystem). Falta conferir com modelos reais.
- [ ] **Classificação dos relatos:** confirmar os problemas antes de convertê-los em tarefas de correção; estas observações descrevem a experiência quando foram registradas. {ia:"gpt-6-luna"} {ia:"gpt-luna-latest"} {ia:"gpt-luna-latest"} {ia:"gpt-6-luna"} {ia:"gpt-6-luna"} {ia:"gpt-6-luna"} {ia:"gpt-6-luna"}

## Identidade visual e marca — proposta aprovada

- [ ] **Novo nome:** implementar a proposta aprovada de nome Numa. A intenção original era buscar uma identidade nova, com nome em inglês, fácil de lembrar e pronunciar em português, sem exigir que o nome descreva o aplicativo ou tenha conceito literal; o produto pode construir sua identidade própria.
- [ ] **Intenção da marca:** preservar a inspiração e criatividade, evocando a sensação de “é nesse lugar que eu produzo o meu melhor”.
- [ ] **Direção da logo:** implementar o símbolo abstrato e orgânico aprovado, com curvas suaves que se abrem em torno de um espaço central, sugerindo possibilidade e criação sem representar literalmente uma ferramenta. O usuário aprovou o estudo visual e declarou que amou o resultado.
- [ ] **Escopo visual:** preservar cores, visual e demais elementos atuais que já agradam; a mudança proposta concentra-se no nome e na logo.
- [ ] **Estudo visual:** consultar o estudo da logo em `Páginas/Identidade Numa — estudo de logo.page.json`. {ia:"gpt-6-luna-pro"}
- [ ] **Verificações antes de concluir:** confirmar se nome e logo foram implementados no aplicativo e verificar disponibilidade jurídica, comercial, em domínios e lojas; esses pontos não foram confirmados no registro da proposta. {ia:"gpt-luna-latest"} {ia:"gpt-6-luna-pro"} {ia:"gpt-6-luna"} **Atualização 30/10:** pesquisa preliminar achou a marca NUMA em software (numa group GmbH, de hospedagem) e o app de notas "Numa Booknote"; a escolha está aberta no portal (DEC-0060). INPI, domínios e lojas ainda não foram verificados.

## Implementações registradas — beta 18 à beta 31

Registros históricos de mudanças no Urbe C# nativo posteriores à beta 18 e até a beta 31 (25/10/2026). PRs indicam a origem da implementação, não validação universal nem garantia de funcionamento em todo aparelho.

- [x] ** {ia:"gpt-6-luna"}Páginas — seleção e criação (PR #700):** foram introduzidas seleção de notas por pasta, ligações e busca, criação contextual, modelos e edição de campos na prévia. A proposta de um editor de páginas mais amplo e a validação móvel continuam registradas acima.
- [x] ** {ia:"gpt-6-luna"}Transclusões no editor Visual (PRs #705 e #706):** foram adicionadas incorporações de notas, imagens, HTML, PDF, áudio, vídeo e código, com referências a títulos, blocos e linhas.
- [x] ** {ia:"gpt-6-luna"}Propriedades e modos de nota (PR #707):** propriedades podem ser editadas por formulário; aliases são usados em links e backlinks; há modos de leitura Visual e Fonte.
- [x] ** {ia:"gpt-6-luna"}Captura e atualizações no Windows (PR #700):** captura por Ctrl+Alt+N que abre uma caixa de texto de nota rápida e utiliza o ditado do Windows, além da bandeja do sistema e verificação manual de atualizações compatíveis com a plataforma. Esse fluxo não equivale à gravação direta em segundo plano feita pelo widget Android, nem valida os relatos de desempenho e UX do widget. {ia:"gpt-6-luna"}
- [x] ** {ia:"gpt-6-luna"}Desempenho em vaults grandes (PR #700):** existem testes automatizados de desempenho e otimizações de leitura/escrita registradas mais abaixo; isso não comprova fluidez no celular nem substitui a validação em dispositivo real e a métrica abrangente de execução proposta acima. {ia:"gpt-6-luna"}
- [x] ** {ia:"gpt-6-luna"}Foco Android (PR #721):** foram introduzidas uma política de foco do teclado e uma ferramenta de diagnóstico de foco, IME, toques e rolagem. A existência dessas ferramentas não confirma que os problemas de foco e interação estejam resolvidos em aparelhos Android.
- [x] ** {ia:"gpt-6-luna"}Provedores de IA por agente (PR #702):** cada agente pode ter provedor e modelo de conversação próprios, independentes do modelo padrão. A geração de imagens também tem seleção separada de provedor/modelo e alternativa automática.
- [x] ** {ia:"gpt-6-luna"}Fluxo de imagens no chat (PRs #704 e #755):** foram adicionadas prévias de imagens geradas, preços e unidades, compositor de anexos, zoom e exportação de imagens; isso não atesta qualidade ou disponibilidade de cada provedor.
- [x] ** {ia:"gpt-6-luna"}IA local (PR #718):** foi introduzido provedor local offline com Gemma 4, llama.cpp e download verificado sob solicitação. Geração de imagens offline não está concluída.
- [x] ** {ia:"gpt-6-luna"}Execução em segundo plano Android (PR #726):** serviço e notificações permitem continuidade enquanto o app está minimizado; não há garantia de recuperação durável se o processo morrer.
- [x] ** {ia:"gpt-6-luna"}Planejamento e revisão (PR #726):** foram introduzidos planejamento sem número fixo artificial de passos, profundidade proporcional à tarefa e revisão crítica de artefatos. Isso registra a implementação dessas capacidades, não comprova qualidade em todos os fluxos. A separação entre revisão interna e resposta final não está resolvida: o fluxo ainda solicita uma nova resposta após a revisão obrigatória; ver o relato acima em **Assistente de IA e agentes**. {ia:"gpt-6-luna"}
- [x] ** {ia:"gpt-6-luna"}Atividade do Assistente (PRs #732, #738 e #755):** atividade compacta e expansível exibe estados de ferramentas, parâmetros, resultados e planos.
- [x] ** {ia:"gpt-6-luna"}Acesso contextual à IA (PR #747):** foram introduzidos botão flutuante de IA e miniassistente contextual para comandos inline, intervenções e resultados; não elimina a proposta mais ampla de integração inline descrita acima.
- [x] ** {ia:"gpt-6-luna"}Interação e acompanhamento no chat (PR #755):** seleção e cópia de texto e código, preservação de rascunhos, acompanhamento de tokens/custos e painel consultável de fontes.
- [x] ** {ia:"gpt-6-luna"}Pesquisa pública (PRs #733 e #750):** foram adicionadas `web_search` e `web_fetch`, com busca gratuita sem chave como padrão, Brave opcional, leitura de fontes verificáveis e proteções de rede. O serviço público é best-effort e depende de disponibilidade externa; a governança DEC-0057 permanece pendente. Resultados de busca não são, por si, confirmação factual.
- [x] ** {ia:"gpt-6-luna"}Memória local tipada (PR #734):** foram introduzidos tipos para fatos, preferências, decisões, objetivos, episódios, procedimentos e observações, além de escopos, proveniência, confiança, estados, conflitos e substituição de registros.
- [x] ** {ia:"gpt-6-luna"}Gerenciamento e ferramentas de memória (PR #734):** gerenciador nativo e `recall_memory`, `learn_memory`, `review_memory`, `supersede_memory` e `forget_memory`.
- [x] ** {ia:"gpt-6-luna"}Recuperação de memória (PR #737):** recuperação paginada, orçamento contextual adaptativo e remoção de cotas artificiais. Embeddings vetoriais e benchmark externo não estão registrados como implementados.
- [x] ** {ia:"gpt-6-luna"}Editor nativo de grafos (PR #748):** canvas com arquivos `.grafo.json`, nós, referências ao vault, conexões, formas, grupos, desenhos e recursos iniciais de edição e desfazer/refazer; a extensão desses recursos não deve ser presumida além do que foi implementado.
- [x] ** {ia:"gpt-6-luna"}Grafos no vault e ferramentas de leitura (PR #748):** incorporação navegável em Markdown, projeção estrutural de pastas, arquivos, notas e wikilinks, e ferramentas paginadas `list_graphs` e `read_graph`. Edição de grafos por IA permanece pendente.
- [x] ** {ia:"gpt-6-luna"}Espaços e histórico por agente (PR #757):** espaços de trabalho individualizados, históricos separados, conversa nova ao selecionar agente e retomada explícita de conversas anteriores.
- [x] ** {ia:"gpt-6-luna"}Agentes predefinidos (PR #757):** templates opcionais foram introduzidos, preservando perfis existentes; relacionam-se à proposta de agentes padrão opcionais acima, sem implicar adoção automática.
- [x] ** {ia:"gpt-6-luna"}Permissões e exclusão de agentes (PR #761):** configuração efetiva de permissões separa escopo de ferramentas e aprovação. Exclusão exige confirmação e remove os históricos verificados do agente, sem apagar arquivos do vault.
- [x] ** {ia:"gpt-6-luna"}Multichat (PR #761):** execuções, estados, aprovações, intervenções e cancelamentos são independentes entre chats.
- [x] ** {ia:"gpt-6-luna"}Consulta entre agentes (PR #761):** `consult_agent` oferece consentimento, isolamento de modelos e memórias, resultados e imagens delegadas, acompanhamento por múltiplos turnos e proteção contra delegação recursiva não supervisionada. O encaminhamento automático de perguntas ou aprovações pelo chat solicitante não está incluído; quando o especialista precisa de resposta, o código orienta abrir a conversa dele. Ver a limitação em **Relatos que precisam de nova verificação**. {ia:"gpt-6-luna"} {ia:"gpt-6-luna"}
- [x] ** {ia:"gpt-6-luna"}Formulários no chat (beta 31; PR #763, UC-90 e UC-90.1):** `request_form` introduz campos de texto curto/longo, números e inteiros, data e horário, seleção simples/múltipla, checkbox, alternância, escala, lista, nota, pasta, cor, cálculo e grupos repetíveis; inclui visibilidade condicional, validação, valores iniciais, rascunhos persistentes e envio/cancelamento explícitos. A feature está implementada; relatos específicos de validação numérica e seleção múltipla permanecem pendentes de verificação em **Relatos que precisam de nova verificação**. {ia:"gpt-6-luna"}
- [x] ** {ia:"gpt-6-luna"}Formulários reutilizáveis (beta 31; PR #763, UC-90 e UC-90.1):** formulários podem ser usados em templates Markdown e salvos como modelos. São isolados por agente e conversa, preservam permissões e aprovação de escrita e detectam divergência entre a versão da nota e a usada pelo formulário. Isso não implica identificação de templates por extensão própria nem preenchimento alternado entre formulário e editor; esses pontos seguem pendentes em **Modelos reutilizáveis de notas**. {ia:"gpt-6-luna"}
- [x] ** {ia:"gpt-6-luna"}Mensagens durante execução (beta 31; PR #763, UC-90 e UC-90.1):** fila persistida conserva a ordem de processamento das mensagens enviadas enquanto uma execução está ativa.
- [x] ** {ia:"gpt-6-luna"}Robustez da apresentação (beta 31; PR #763, UC-90 e UC-90.1):** correção de crash ao apresentar argumentos e planos malformados; resultados extensos são paginados.
- [x] ** {ia:"gpt-6-luna"}Eficiência de leitura e escrita (beta 31; PR #763, UC-90 e UC-90.1):** otimizações incluem revisão por versão do documento, consultas de trechos específicos, prevenção de gravações idênticas, reutilização de evidências contextuais e sinalização de ciclos sem progresso.
- [x] ** {ia:"gpt-6-luna"}Métricas parciais de eficiência (beta 31; PR #763, UC-90 e UC-90.1):** são contabilizadas leituras reutilizadas, gravações sem alteração e caracteres de contexto economizados. Isso não corresponde à proposta mais abrangente de telemetria descrita em Prioridades gerais. {ia:"gpt-6-luna"}



## Entregas da beta.37 que não estavam nesta lista (30/10/2026)

- [x] Compartilhar um arquivo ou uma nota com outro app pelo menu do Android (CST-036). **Falta:** conferir no aparelho.
- [x] Até 20 anexos por mensagem, dentro de 30 MB; fotos grandes ou deitadas vão de pé e reduzidas, em vez de recusadas (CST-081). **Falta:** conferir no aparelho.
- [x] Rascunhos do chat, com anexos, guardados no aparelho e restaurados depois de o Android encerrar o app (CST-122). **Falta:** conferir no aparelho.
- [x] Barra de formatação arrumada pelo toque em Personalização → Editor (CST-019). **Falta:** conferir no aparelho.
- [x] Ícone nas tags, além da cor (CST-024). **Falta:** conferir no aparelho.
- [x] Links Markdown `[texto](Nota.md)` acompanham renomear e mover (CST-101). **Falta:** conferir no aparelho.
- [x] Permissões: uma mais restrita vale na hora; um especialista em automático pede aprovação quando quem pediu também pede; mudar `AGENTS.md` sempre pede aprovação (CST-073, 093, 084). **Falta:** conferir no aparelho.
- [x] Ordem das instruções definida: o pedido atual, depois o agente, depois o `AGENTS.md`, por último o Urbe (CST-076). **Falta:** conferir no aparelho.
- [x] Um agente criado de um modelo mantém as ferramentas do modelo; excluir um agente leva as lembranças privadas dele (CST-071, 074). **Falta:** conferir no aparelho.
- [x] Pesquisa na internet: uma página não faz o agente enviar trechos das notas; um endereço guardado numa nota abre (CST-084). **Falta:** conferir no aparelho.
- [x] Relatório de erros sem chaves, trechos de notas nem caminhos, e só dos últimos 30 dias (CST-097). **Falta:** conferir no aparelho.
- [x] O cartão de aprovação mostra todas as linhas mudadas; qualquer tag filtra a Área de trabalho; Abrir ao lado alcança qualquer nota (DEC-0059). **Falta:** conferir no aparelho.
- [x] Áreas de toque de pelo menos 40 px nas telas principais e na Personalização, e contraste WCAG AA nos temas (CST-105). **Falta:** conferir no aparelho.
~~~~~~~~

## 14. Auditoria comparativa da IA — evidências de 2.3 reconciliadas com o produto em 2.4

### 14.1. Diagnóstico diretamente observado na main do Ecosystem

- `apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantAgent.cs`: ciclo de agente, aprovação, delegação, preparação/encurtamento de histórico, execução de leituras paralelas, revisão pós-escrita. A revisão pós-escrita solicita ao próprio modelo reler seus resultados, não substitui teste de formato e render visual independente. O caminho de aprovação sem callback assume `AiApproval.Approve`; a UI usual do Urbe fornece callback, mas novos hosts exigem falha fechada.
- `AssistantTools.cs`: ferramentas centrais do Urbe atuam em notas, arquivos e organização; `edit_note` substitui trecho exato, `write_note` reescreve arquivo integralmente. Não foi demonstrada API estruturada transversal de componentes (CST-224), embora existam ferramentas especializadas. Não declarar vídeo operacional porque o agente consegue gerar roteiro/HTML.
- `AssistantImageTools.cs`: `DrawAsync(prompt, size, CancellationToken.None)` não encaminha cancelamento da tarefa; testar efeitos e gastos ao interromper.
- `AssistantWebResearch.cs`: `AiWebConfig.Allowed(agentId) => Enabled` controla a rede por flag geral; `AgentAccess` legado não é usado ali para autorização individual. Revisar intenção normativa, não presumir vazamento sem evidência.
- `AssistantMemoryEngine.cs` e `KnowledgeIndex.cs`: recuperação baseada em termos/tokens/aliases e metadados, com escopos de memória; sem classificação vetorial híbrida geral demonstrada. Validar qualidade de consulta sobre notas e relações antes de adotar embeddings. Semântica inferida pelo LLM não substitui citações para trechos reais (CST-223).
- `apps/urbe/csharp/src/Urbe.Client/AssistantPanel.cs`: catálogo central de ferramentas, IA local com subconjunto e `AssistantPageTools.All` legado; `page_schema`/`write_page` ainda criam `.page.json`, divergindo da substituição por Publicações (CST-149/176). Não foi encontrado executor de código geral nem motor MP4 consumido pelo agente na versão examinada.
- `apps/ecosystem-ai/src/AgentRuntime.Core/{AgentRunner,Tools,RunState,Ledger,ChangeIntegration}.cs`: outro runtime no próprio Ecosystem possui event journal/replay, ResumeAsync, grants intersectados, orçamento, verificação e integração de ChangeSet. Avaliar reúso sem acoplamento ou reescrita total.
- `Urbe.Core.csproj` e `Urbe.Client.csproj` examinados não referenciam diretamente `AgentRuntime.Core`; a convergência ainda não foi comprovada em execução no Urbe.
- `AssistantGraphTools.cs`: existe **`edit_graph`**, operação de escrita estruturada com prévia e lote de operações; corrigir a leitura antiga de que edição de grafos por IA seria inexistente. O checkbox CST-068 permanece aberto até verificar integração, persistência, permissões e regressões em aparelho.
- `NativePlugins.cs` e `PluginHost.cs`: há `IPluginApi` com leitura/escrita de notas, comandos, eventos e painéis, mas não foi encontrada operação tipada para convocar agente a partir de plugin. A integração é requisito de CST-146, não feature pronta.
- `AssistantWebResearch.cs` e `AssistantImageTools.cs`: a flag `Allowed(agentId)` usa `Enabled` geral; chamada de imagem vista em `DrawAsync(..., CancellationToken.None)` não propaga o cancelamento. São pontos de inspeção para CST-219, **não exploração nem falha de segurança provada**.

### 14.2. Engenharia aberta: implementar padrões, não importar produtos inteiros

| Referência | Código/parte examinada | Principal ensinamento para a IA nativa |
|---|---|---|
| Claude Code/Claude Motion | Documentação oficial, skills públicas Anthropic; core de Claude Code não é aberto | Ambiente de produção por código, tarefas editáveis, skills, inspeção, vídeo como cena programada |
| OpenAI Codex | `openai/codex/codex-rs/core/src/{compact.rs,exec_policy.rs,tools/orchestrator.rs,tools/code_mode/execute_handler.rs}` | Orquestrar approval, sandbox, ferramentas e compactação rastreável separadamente da UI; Code Mode não dispensa verificação de grants |
| OpenCode | `anomalyco/opencode/packages/core/src/{skill/discovery.ts,codemode/execution.ts,session/compaction.ts,session/run-coordinator.ts}` | Skill discovery seguro, compaction por contexto, coordenação de trabalhos independentes e Code Mode contextual |
| Qwen Code | `QwenLM/qwen-code/packages/core/src/{skills/skill-activation.ts,code-mode/tool-call-runtime.ts}` e diretórios `sandbox/`, `managed-runtime/` | Ativação de skill por paths, tool-call runtime com escopo; evitar injetar todas skills em todo prompt e não importar host Node ao Android |
| Goose | `aaif-goose/goose/crates/goose/src/{skills/mod.rs,permission}` e `crates/goose-agent/src/{subagent.rs,skills}` | Skills instaláveis com escopos, permissões próprias, subagentes e integrações sem depender da UI final |
| Cline | `cline/cline`, projeto de agente SDK/editor/CLI | Aprovação, integração do trabalho com ferramentas reais e desenvolvimento com diffs |
| OpenHands | `OpenHands/OpenHands`, serviço de agentes e UI | Tarefas como operações sobre ambientes, resultados e capacidade de execução |
| Aider | `Aider-AI/aider`, edição conversacional Git | Diffs, engenharia de alterações e organização de contexto de repositórios |
| LangGraph | `langchain-ai/langgraph/libs/langgraph/langgraph/pregel` | Estado durável, interrupção e retomada da execução |
| MCP C# SDK | `modelcontextprotocol/csharp-sdk/src/ModelContextProtocol.Extensions.Skills` | Ferramentas, recursos, skills interoperáveis com contratos tipados e validação |
| Motion Canvas | `motion-canvas/motion-canvas` e FFmpeg exporter | Gráficos/quadros programáticos com timeline e vídeo real |
| Manim | `ManimCommunity/manim`, `3b1b/manim` | Matemática como base de composição e animação científica |
| Android Media | APIs oficiais MediaCodec/MediaMuxer | Encoder nativo como candidato para MP4 no Android; validar disponibilidade por dispositivo |

### 14.3. O que as skills revelam sobre lacunas de produto

- `anthropics/skills/skills/pptx/SKILL.md`: editar XML/OOXML, criar/validar relações de arquivo, capturar miniaturas — exige geração real e validação estrutural.
- `pdf/SKILL.md`: extração, formulário, OCR, composição e fluxos de inspeção — exige ingestão confiável e render paginado.
- `frontend-design/SKILL.md`: tipografia, estrutura e intenção visual específicos — exige direção visual que respeite assunto/público e uma ferramenta de prévia.
- `webapp-testing/SKILL.md`: capturar tela, testar DOM/interação e verificar erros — exige agente capaz de OBSERVAR resultados, além de escrevê-los.
- `mcp-builder/SKILL.md`: sistemas externos úteis precisam de ferramentas bem descritas e paginadas — exige capacidades versionadas, schemas e permissões.
- `skill-creator/SKILL.md`: criar casos de teste, comparar com/sem skill e rever — exige avaliação real de skills, não instalação cega.
- Qwen Code possui `skills/skill-activation.ts` com descoberta condicional por arquivos/caminhos: evitar contexto inchado pelo catálogo de skills.

**Comunidade:** relatos sobre workflows de agentes apontam dificuldades de contexto extenso, execução sem estado compartilhado, permissões e revisão do trabalho. Usar issues/discussões e Reddit como hipóteses de casos de regressão, não porcentagens gerais ou consenso automático. Para cada melhoria testar no Urbe e registrar se resolveu o caso; não prometer que copiar uma skill copia a capacidade de código do Claude.

**Issues concretas examinadas (evidência de casos, não prevalência):**

- **OpenCode #4659 e #11314:** propostas sobre compactação/limiar de contexto em sessões longas — [#4659](https://github.com/anomalyco/opencode/issues/4659), [#11314](https://github.com/anomalyco/opencode/issues/11314). **Requisito Urbe:** preservar estado e decisões canônicas separadamente da poda de mensagens, medir regressões de contexto.
- **OpenCode #12661 e #10288:** demanda por coordenação de equipes e experiência móvel — [#12661](https://github.com/anomalyco/opencode/issues/12661), [#10288](https://github.com/anomalyco/opencode/issues/10288). **Requisito Urbe:** orquestração de agentes com evidências e UX Android nativa.
- **Cline #4389, #4359 e #12388:** relatos de arquivos grandes, corrupção/perda de histórico e falhas de checkpoint — [#4389](https://github.com/cline/cline/issues/4389), [#4359](https://github.com/cline/cline/issues/4359), [#12388](https://github.com/cline/cline/issues/12388). **Requisito Urbe:** stream de arquivos grandes, logs robustos, recuperação comprovada após crash, checkpoints verificados.
- **Qwen Code #176, #10860 e #3870:** chamadas de ferramenta incompatíveis em modelos locais, discrepância de política shell e ferramentas nativas em extensões — [#176](https://github.com/QwenLM/qwen-code/issues/176), [#10860](https://github.com/QwenLM/qwen-code/issues/10860), [#3870](https://github.com/QwenLM/qwen-code/issues/3870). **Requisito Urbe:** testar compatibilidade de schema/modelo, permissões coerentes em cada host e capacidade nativa extensível sem exigir um processo MCP por ferramenta.
- **Goose #6915 e #11043:** relato de consumo elevado em tarefa simples e proposta de empacotamento interoperável de plugins/skills — [#6915](https://github.com/aaif-goose/goose/issues/6915), [#11043](https://github.com/aaif-goose/goose/issues/11043). **Requisito Urbe:** métrica de custo por resultado verificado, instalação/confiabilidade separadas de autorização de execução.
- **Motion Canvas #1052, #1129 e #1177:** problemas de duração na exportação de trecho, proposta de paralelização e áudio sem vídeo — [#1052](https://github.com/motion-canvas/motion-canvas/issues/1052), [#1129](https://github.com/motion-canvas/motion-canvas/issues/1129), [#1177](https://github.com/motion-canvas/motion-canvas/issues/1177). **Requisito Urbe:** testes de duração real, quadros parciais, sincronia e formato final; não assumir que renderizar quadros produz MP4 correto automaticamente.

Todos os relatos precisam ser validados contra o estado atual dos respectivos projetos e contra casos reproduzíveis no Urbe; uma issue aberta não prova que o software continue com a falha nem que a reclamação represente a maioria.

### 14.4. Arquitetura-alvo

Fluxo: **usuário/vault → tarefa/objetivo → agente + skill relevante → registry de capabilities → executor isolado / ferramenta tipada → resultados com evidências → verificadores → publicação/projeto/nota real → exportação**.

Caminhos principais: (1) computação sem scripts para operações simples; (2) código interpretado/executado em worker isolado e autorizado para tarefas amplas; (3) adaptadores de compilação, testes e hospedagem por plataforma, opcionais e verificados; (4) engines de documentos e mídia especializados reutilizados por todos os agentes; (5) multimodalidade via provedores configurados com escolha/custo/privacidade transparentes.

O Urbe mantém a UI nativa C#/Avalonia. HTML/JS/SVG podem ser **artefatos** e superfícies isoladas de prévia, mas não substituem a UI; Python/TS/FFmpeg não se tornam dependências obrigatórias do APK por simples comparação. Runtime de código não deve ter acesso automático a segredos nem ao vault; alterações persistidas passam pelos contratos canônicos com versão, histórico, autoria e permissões. Execução Android precisa de protótipo e gate de segurança; NativeAOT Android continua experimental segundo a documentação 2026 e não deve ser tratado como suporte comprovado de JIT/Reflection.Emit.

### 14.5. Aceite transversal do Urbe de ponta

Em dispositivo Android físico e Windows, o usuário fornece materiais do vault e solicita: **(a)** executar análise estatística com código/cálculo real, **(b)** criar gráfico SVG editável e simulador, **(c)** produzir apresentação e PDF válidos, **(d)** criar animação por código de 30 segundos com áudio e exportar MP4 reproduzível, **(e)** inspecionar screenshot/quadros, corrigir e reexportar, **(f)** reabrir todo o projeto sem perder vínculos, **(g)** cancelar/reiniciar sem duplicar efeitos, **(h)** verificar um projeto C# existente com diffs, teste/build quando a toolchain do host suportar, **(i)** demonstrar permissões, logs, custos, validação e limitações honestas. A ausência de runner/codec/sandbox real é bloqueio explícito, não sucesso simulado.

### 14.6. Referências complementares a manter acessíveis no próprio plano

- https://github.com/QwenLM/qwen-code/tree/main/packages/core/src
- https://github.com/anomalyco/opencode/tree/main/packages/core/src
- https://github.com/aaif-goose/goose/tree/main/crates/goose-agent/src
- https://github.com/cline/cline
- https://github.com/Aider-AI/aider
- https://github.com/OpenHands/OpenHands
- https://github.com/openai/codex/tree/main/codex-rs/core/src
- https://github.com/manimCommunity/manim
- https://github.com/motion-canvas/motion-canvas
- https://github.com/modelcontextprotocol/csharp-sdk/tree/main/src/ModelContextProtocol.Extensions.Skills
- https://developer.android.com/media/platform/supported-formats
- https://learn.microsoft.com/dotnet/android/messages/xa1040
- https://learn.microsoft.com/dotnet/core/deploying/native-aot/
- https://github.com/anthropics/skills
- https://agentskills.io/specification

---

## 15. Reconciliação da pesquisa de IA ao roadmap inteiro (revisão 2.4 — 31/10/2026)

Esta seção é **rastreabilidade e justificativa de roadmap**, não nova lista normativa nem declaração de implementação. Cada problema deve ter um ID CST responsável, evidência de código, referência de estudo e teste de aceite. `CST-198` permanece aberto para benchmarking real; inspecionar fontes e formular hipótese não satisfaz teste no APK.

### 15.1. Matriz de produto: funcionalidade → evidência → melhoria nativa → checklist responsável

| Superfície/fluxo consolidado | Implementação/limite observado em leitura estática | Aprendizado da pesquisa | Critério adicional de produto e tarefa responsável |
|---|---|---|---|
| Editor de notas, seleções, blocos | `edit_note` troca strings; `write_note` substitui nota inteira; editor visual é nativo | Codex/OpenCode fazem edição rastreável; diffs não resolvem sozinhos documentos ricos | Operação semântica única para UI/agentes/plugins, patch com revisão, undo e teste IME: **CST-224**, 015–035, 193, 205 |
| Vault com notas, propriedades e tags | `KnowledgeIndex.Search` combina título/caminho/tokens; relações existentes | Obsidian/plugins RAG, LangGraph; recuperar e fundamentar são problemas distintos | Busca com IDs/revisões/trechos citáveis, inferência separada de evidência e testes de cem notas: **CST-223**, 042, 075, 087, 119–120, 134 |
| Grafos autorais e relações | **`edit_graph` presente** em `AssistantGraphTools`, com operações atômicas; uso ponta a ponta não testado aqui | Skills de diagramação revelam necessidade de edição estruturada e inspeção | Não recriar tool; validar criação/edição/preview/permissões e relações não factuais: **CST-068**, 066–067, 224 |
| Captura, formulário, notas rápidas | Há captura e formulários nativos; problemas/etapas descritos na seção 4.6 | Ferramentas agentes avançadas coletam dados estruturados, sem confundir resposta com permissão | IA usa os mesmos campos e notas, sem novo formulário concorrente: **CST-045–051**, 053–058, 224–225 |
| Memória e conversas | Memória com escopos e busca lexical; chat é separado da execução persistente | Codex/OpenCode compactação; LangGraph checkpoints; perguntas sobre trabalho extenso | Estado canônico fora do prompt, memória distinguida de fatos do vault, retomada idempotente: **CST-085–089**, 098, 199, 216, 223, 225 |
| Agentes e especialistas | `consult_agent` já existe; isolamento parcial comprovado por código; handoff precisa de UX | Goose/Qwen subagentes; a comunidade relata custo/coordenação perdida | Resultado com fonte/artefato e escopo, sem multiplicar agentes por reflexo: **CST-090–093**, 178, 217, 225 |
| Plugins como experiência inteligente | `IPluginApi` tem notas/comandos/eventos/painéis, não ação de convocar agente encontrada | MCP-builder: ferramentas bem descritas e controles no host | Plugin Próxima tarefa solicita agente, recebe recomendação estruturada, propõe sem executar: **CST-146**, 200, 219, 225 |
| Publicações, antes Páginas | `AssistantPageTools` inclui `page_schema`/`write_page` antigos | Skills PPTX/PDF/design testam render, semântica, layout e arquivos reais | Remover roteamento antigo; `SourceRef` vivo com edição bidirecional; apresentação/MP4 reais: **CST-149–192**, 209, 226 |
| Imagem, áudio e vídeo | `generate_image` grava arquivo; pipeline MP4 para agente não encontrado | Claude Motion, Motion Canvas, Manim, skills de criação audiovisual | Cena e roteiro em nota editorial, render/codec compartilhado, QA de duração e som: **CST-170–173**, 179–180, 209–210, 213 |
| Matemática, dados e código | Recursos de matemática do editor; sem executor geral de código no catálogo principal | Codex/Qwen Code Code Mode; Manim e ferramentas científicas | Cálculo simples nativo, execução avançada isolada, resultados reutilizáveis na nota e publicação: **CST-069**, 141, 201–208, 221 |
| Skills, integração externa | Ferramentas do Urbe e plugin host; loader completo de skills não demonstrado | Anthropic `pptx`, `pdf`, `webapp-testing`, `frontend-design`, `skill-creator`; OpenAI curated; Qwen ativação condicional | `SKILL.md` validado com lazy loading, teste A/B e tool real, MCP opcional sem poderes extras: **CST-177**, 188, 214–215 |
| Qualidade, erros, UX móvel | Revisão pós-escrita pelo próprio LLM; ANR e editor são bloqueadores relatados | Skills criativas e de testes exigem screenshots, abertura real e evidências | Verificação de arquivo/render e experiencia touch-first, sem terminal obrigatório: **CST-079**, 111–114, 142–144, 179, 213, 218, 222, 225 |
| Coerência após mudança de fonte | Links/backlinks e propostas de cache, mas não há cadeia transversal demonstrada | Durable execution/derivação de outputs: identificar versões de entrada | Uma fonte alterada sinaliza evidências, plugin e publicações dependentes sem reescrever original: **CST-153**, 181, 184, **226** |

**O que esta matriz NÃO afirma:** não comprovamos benchmark humano/automático com o mesmo modelo; não demonstramos que qualquer skill melhora o Urbe; não validamos execução completa de `edit_graph`, vídeo, Code Mode ou MCP no Android; não confirmamos que embutir repositórios abertos resulte em compatibilidade de licença/CPU. A avaliação técnica desses aspectos permanece aberta.

### 15.2. O que cada skill examinada ensina e onde deve virar ferramenta nativa

| Skill/caso de estudo | Necessidade que ela revela | Destino no Urbe — não depender de skill para capacidade elementar |
|---|---|---|
| `anthropics/skills/skills/pptx/SKILL.md` | OOXML, relações, gráficos editáveis, miniaturas, validação do pacote | Exportador de apresentação editável **CST-168**, QA **CST-185/213**, skill de direção narrativa **CST-177** |
| `anthropics/skills/skills/pdf/SKILL.md` | PDF paginado, leitura, tabelas, formulário, OCR, erro de fonte | Motor de PDF **CST-164**, ingestão **CST-212**, QA **CST-213** |
| `anthropics/skills/skills/frontend-design/SKILL.md` | Identidade contextual, tipografia, grids, público, legibilidade | Sistema de composição visual **CST-158/161**, UX acessível **CST-183**, revisão de prévia **CST-179** |
| `anthropics/skills/skills/webapp-testing/SKILL.md` | É preciso inspecionar DOM/screenshot, interagir e reproduzir falhas | Testes de prévias/HTML isolados **CST-211**, QA geral **CST-213**; sem WebView privilegiado |
| `anthropics/skills/skills/skill-creator/SKILL.md` | Comparar com/sem skill, casos-teste, iteração, triggers e falhas | Catálogo e benchmark **CST-214**, avaliação global **CST-218** |
| `anthropics/skills/skills/mcp-builder/SKILL.md` | Schemas curtos, paginação, nome adequado de ferramenta, controles | Registry de capabilities **CST-200**, MCP opcional **CST-215** |
| `openai/skills/skills/.curated/` | Playwright, planilhas, Figma, documentos, notebooks, segurança e deployment surgem como **fluxos** com requisitos próprios | Catálogo de capacidades **CST-200**, dados **CST-207**, testes **CST-211/213**, autorização **CST-219**; testar alternativas no Android |
| Qwen Code `skill-activation.ts` e Goose skills | Evitar enviar todos os procedimentos ao modelo; distinguir skill, instrução e poder | Carregamento progressivo **CST-177/214**; manifesto/autorização no host, não no Markdown |

**Regra de produto:** uma skill ensina *como* produzir com qualidade; o motor nativo faz a operação repetível, segura e testável. Instalar dez skills não substitui geração de MP4, PDF paginado, PPTX editável, extração confiável ou recuperação de trechos. Catálogos grandes precisam de seleção adequada; skills especializadas podem ajudar e skills desnecessárias podem degradar custo/qualidade. Medir sem pressupor ganho.

### 15.3. Comunidades: incidentes como casos de regressão, nunca métricas de mercado

As referências de **OpenCode #4659/#11314** (compactação), **Cline #4359/#12388** (histórico e checkpoint), **Qwen Code #176** (tool calls e modelo local), **Goose #6915** (gasto excessivo) e **Motion Canvas #1052** (duração MP4) foram registradas na seção 14.3 com URLs. Elas geram respectivamente cinco famílias de testes: preservar objetivo após compactação; sobreviver a crash sem corromper ou duplicar escrita; tool schema/modelo compatível; custo por resultado efetivamente entregue; duração/sincronia/exportação MP4 reproduzível. Feedback de plugins Obsidian e ecossistema Agent Skills reforça a necessidade de fontes clicáveis, execução transparente, reversibilidade e isolamento. Para afirmar recorrência ou impacto de mercado, falta coleta amostral rigorosa; não presumir consenso com base em relatos pontuais.

**Não perseguir feature por feature dos concorrentes:** o aprendizado de Codex/Claude Code/OpenCode é a mecânica de executar e verificar; o aprendizado de Obsidian é o conhecimento navegável; Claude Motion inspira produção editável. A diferenciação do Urbe é **uso contextual e integrado de tudo isso sobre a mesma fonte de verdade**. Todos os casos levam a CSTs existentes ou às quatro lacunas novas, sem duplicação de runtime/editor.

### 15.4. Provas de aceite de produto — repetíveis e com resultado observável

**Prova A — Conhecimento e edição concorrente (CST-223/224/218).** Criar fixture de 100 notas com decisões conflitantes, aliases, tags, links, fatos repetidos, sinônimos e fonte ausente; pedir ao agente identificar contradições e justificar cada uma com `SourceRef`/trecho/revisão. Enquanto o run lê, editar outra nota. Aceite: informação de versão anterior é sinalizada, mudanças preservadas, abstenção quando faltam fatos, relatórios de busca com precision/recall e sucesso de revisão registrada no mesmo hardware/modelo. Não aceitar uma resposta plausível sem citação ou que sobrescreva edição humana.

**Prova B — Produção multiformato viva (CST-151–155/168/170–172/187/209/213/226).** Duas Publicações utilizam uma mesma nota: artigo HTML, PDF paginado, apresentação PPTX editável e MP4 com narração/legenda. Editar texto da fonte dentro de Publicações e fora delas; variar apenas um dado para avaliar invalidação seletiva. Aceite: um único conteúdo fonte, nota editorial separada quando adaptação, cada formato abre em software compatível, vídeo possui duração/quadros/áudio reais, versões e hashes batem com manifest, nenhum exportado se torna fonte concorrente.

**Prova C — Autonomia e recuperação (CST-146/199/200/216/217/219/225).** Plugin Próxima tarefa consulta agente com escopo restrito ao projeto e devolve ação/justificativa/fontes/critério. Revogar permissão em execução, matar processo após ferramenta e reabrir. Aceite: nenhuma escrita/custo/geração duplicada, efeitos já aplicados identificados, regra de aprovação aplicada pela API do host e resultado no plugin sem copiar memórias de outros agentes. Rejeição e cancelamento têm estado útil.

**Prova D — Usabilidade sem código (CST-158/201/206/211/221/222/225).** Pessoa abre uma nota, pede gráfico científico e vídeo sem informar linguagem ou CLI; acompanha fonte, prévia, alteração e exportação, podendo editar manualmente. Aceite: recursos suportados funcionam com a UI nativa, limitações/host remoto são revelados antes de custos, testes touch/IME não sofrem regressão; o mesmo recurso permanece operável sem agente em fluxo manual.

**Prova E — Regressão de confiabilidade (CST-011/111/112/142–144/148/193).** Em moto g32, alternar nota/chat/formulário, executar revisão longa, abrir passos grandes, navegar, desligar tela, responder aprovação e voltar. Aceite: nenhum texto descartado, áudio recuperável quando salvo, UI responsiva, nenhum falso sucesso; gravar reproduções, métricas e regressões Android reais. O produto não está pronto enquanto os bloqueadores explícitos do editor não passarem.

**Métricas mínimas para A–E:** sucesso verificado, qualidade humana quando relevante, taxa de erro/retrabalho, latência de primeiro resultado útil e total, tokens/custo com origem medida ou indisponível, memória e temperatura do dispositivo onde aplicável, quantidade de arquivos realmente gerados/abertos, prevenção de efeitos repetidos, preservação de referências e satisfação de critérios de acessibilidade. Não fixar números arbitrários antes de benchmark; definir thresholds após baseline CST-114/137.

### 15.5. Decisões e pendências arquiteturais a manter explícitas

1. **Não fazer migração grande imediata para Ecosystem AI:** `AgentRuntime.Core` já existe, mas o Urbe tem ferramentas editoriais e memória próprias. `CST-199` compara e cria portas; `PLAT-001` decide extração; `CST-200` registra as capabilities sem duplicar execução.
2. **Não confundir inferência com fonte:** `CST-223` liga KnowledgeIndex, notas, memórias e grafos; `CST-224` cuida da alteração segura; `CST-226` identifica derivados afetados. Busca vetorial não é obrigação nem licença para indexar conteúdo privado externamente.
3. **Não impor ferramentas de programação ao usuário:** `CST-201/202` são motores opcionais para tarefas avançadas; `CST-225` é a experiência contextual; sem scripts o produto continua editando, pesquisando, preenchendo formulários e publicando formatos suportados.
4. **Não duplicar ferramentas de publicação:** separar timeline (`CST-170`), codec (`CST-209`), produto MP4 (`CST-171`); QA específico (`CST-179`) e infraestrutura (`CST-213`); skills de Publicações (`CST-177`) e gestão geral (`CST-214`); fluxo editorial (`CST-178`) e handoff genérico (`CST-217`).
5. **Não prometer capacidades sem prova no dispositivo:** dependências de .NET, codecs, OCR, navegadores ou execução de código precisam de teste Android real, consumo, segurança, licença, modo offline e justificativa da integração. **CST-190/218/222** mantêm gates.

### 15.6. Registro das alterações documentais 2.4

- Fonte desta revisão: versão 2.3 anexada, preservada como original; comparação aplicada ao código `main` do Urbe, Ecosystem AI e implementações abertas de Codex, OpenCode, Qwen Code, Goose, LangGraph e skills Anthropic/OpenAI examinadas na pesquisa anterior.
- Itens antigos **CST-001–222 mantidos com IDs e estado [ ]**; ajustes de descrição, critérios e dependências onde cabíveis; **quatro novos itens CST-223–226** para lacunas de recuperação fundamentada, operação semântica, experiência contextual e impacto geral de mudanças.
- Corrigida a evidência de `edit_graph` no CST-068; `AssistantPageTools` legado registrado no CST-149; sobreposições 170/171/209, 177/214, 178/217 e 179/213 diferenciadas; dependências do renderizador MP4 e mecanismos de QA revistas.
- Autoridade de dados e arquivos do usuário, Publicações com notas vivas, UI C# nativa, Android prioritário, plataforma de IA compartilhável por revisão, segurança e testes permanecem intactos.
- O texto histórico íntegro de FeaturesCheck (seção 13) e a matriz de rastreabilidade original (seção 6.2) **não foram modificados**. Nenhuma checklist foi fechada por inspeção estática; não há release, PR ou build validado nesta edição.


## 16. Registro da revisão documental 2.5 — 01/11/2026

**Origem da decisão:** conversa com o proprietário durante o acompanhamento de leitura de *Coraline*: uma barra exibindo páginas lidas/total revelou um requisito mais geral para o Urbe. Checkboxes mostram quais tarefas faltam; a barra precisa mostrar **quanto trabalho já foi concluído e quanto ainda falta**. A decisão expressa é: **implementação nativa**, e não um plugin/HTML obrigatório.

**Alterações desta versão:**

- Seção 1.3 explicita a decisão de produto, o vínculo com os dados reais e a regra de não depender de WebView/JS.
- Seção 4.16 adiciona duas tarefas abertas: **CST-227** (componente de progresso nativo com meta numérica editável, persistência e UX) e **CST-228** (cálculo automático de tarefas concluídas/restantes por escopo, integrando checkboxes).
- As seções 3, 6.1 e 9.10 incluem referências cruzadas para as novas entregas. Os IDs CST-001–226 permanecem estáveis; não há fechamento ou renumeração de tarefas anteriores.
- A cobertura histórica do FeaturesCheck (seção 6.2) e a transcrição fiel do original (seção 13) ficam **intocadas**, porque esta necessidade surgiu posteriormente. O registro histórico da pesquisa de IA (seções 14–15) também permanece íntegro.

**Regras para implementação:** estudar a implementação vigente de editor nativo, checkboxes, propriedades, persistência, renderização, formulários e extensões antes de codificar; testes antes da implementação e regressões relevantes; sem perda de dados; sem alterar marcações por inferência; sem interface paralela. Aceite depende de teste real Android/Windows e evidência por tarefa. **Esta revisão editou somente o plano, não o repositório do aplicativo.**


## 17. Execução de continuidade — 01/11/2026 (CST-142/144/152/153/162)

**Base observada:** `main@c02a42e`, Publicações em `f364110c` (PR #876), protocolo de seis etapas no PR #874; otimização do índice preservada no PR #877. A continuação corrige e amplia #876, sem outro estúdio ou roadmap concorrente. Autoridade: plano 2.5 recebido do proprietário, incluindo todas as decisões e itens anteriores.

| Requisito | Evidência e comportamento implementado | Estado real |
|---|---|---|
| CST-142 | Teste geométrico de fração alta confirma que o parágrafo seguinte começa depois da fórmula; screenshot `native-math-natural-height.png`. Corrigida consulta do teste aos Runs efetivos do MarkdownReader; equações em bloco mais largas que telas de 320/412 dp passaram a oferecer rolagem horizontal nativa, preservando escala e altura, com teste de extensão/viewport e screenshots. | Verificado automaticamente no cenário; matriz Android/IME/DPI e fórmulas largas ainda pendente. |
| CST-144 | Reproduzida perda de texto após dois leave-saves: primeira gravação falha, segunda aguarda e reabertura recuperava o rascunho antigo. Novo teste falhou com texto antigo e passou após dar precedência ao snapshot pendente mais novo. | Corrigido e testado esse cenário; journal durável e morte de processo ainda pendentes. |
| CST-152/153 | Ida ao editor canônico, edição da nota real e retorno ao estúdio salvam a fonte e atualizam a exportação; o texto não é copiado para `.publication.json`. Composição que falha ao salvar fica recuperável nesta sessão, inclusive após fechar/reabrir o estúdio; status visível e ação de retry. | Testes com filesystem real e falhas injetadas; recuperação não é durável após morte do processo. |
| CST-005/152/162 | Baseline da composição impede sobrescrever alteração concorrente pelo agente; gravações enfileiradas vinculadas ao store/ID original. Diálogos atrasados não alteram outra publicação. Exportação depende de composição salva e sem conflito. | Automático nos cenários descritos; conflito mantém as versões e exige comparação, sem auto-merge. |
| CST-162 | Testes demonstraram vazamento de comentários editoriais e LaTeX cru no HTML. Exportação agora usa `NoteMarks.StripComments` (autoria preservada e código literal intacto), o parser Markdown canônico e SVG matemático do MathRenderer, offline e com rótulo acessível. Notas originais não são alteradas. | Vermelho → verde reproduzido; mídia empacotada, links internos multiformato e formatos além de HTML ainda pendentes. |

**Verificação local:** SDK .NET 10.0.100 preparado nesta sessão; build Release portátil sem warnings/erros. Testes executados diretamente pelos assemblies xUnit, pois o comando `dotnet test` tentou abrir named pipe não permitido no executor; mesma suíte e mesmos asserts, sem ignorar falhas. Core: **1.242 aprovados**; UI completa final: **440 aprovados**. Após ampliar o caso de fórmulas largas em 320/412 dp, **10 testes focados aprovados**, com rolagem horizontal nativa sem encolher a fórmula, edição de fórmulas e retorno à publicação. CI final registrado no handoff `HO-20261101-urbe-continuity-editor-publications`.

**Pesquisa/confronto adicional:** fonte oficial Avalonia `src/Avalonia.Controls/TextBlock.cs` (https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Controls/TextBlock.cs) examinada: layout usa métricas do TextLayout/EmbeddedControlRun, `LineHeight` aceita NaN e controles inline entram na árvore visual para evitar corte. Issues oficiais https://github.com/AvaloniaUI/Avalonia/issues/21786 e https://github.com/AvaloniaUI/Avalonia/issues/13873 demonstram diferenças reais de altura/arredondamento e expectativas de alinhamento; são relatos de cenários específicos, não diagnóstico automático do Urbe. Incorporada prova geométrica e screenshot; rejeitada mitigação global de desligar layout rounding sem reprodução no Urbe. A pesquisa anterior em `PUBLICATIONS-RESEARCH-2026-11-01.md` continua válida como evidência complementar, não como roadmap.

**Não há fechamento de CST nem alegação de editor/estúdio completos, DEVICE, APK publicado ou release nesta revisão.** O protocolo já está escrito em todos os seis AGENTS pelo PR #874; sua integração constitucional segue pendente no fluxo governado. Não duplicar essa mudança nem contornar a integração.


### 17.1. Continuidade de matemática e imagens — mesmo incremento

- **CST-142:** a rolagem horizontal também foi aplicada à prévia nativa de `FormulaPane`. Teste em 320 dp falhou antes da mudança e passou depois; duas extremidades são alcançáveis sem encolher o texto matemático. Screenshots de nota e painel inspecionadas; fórmulas curtas mantêm centralização. Ainda não equivale à matriz completa de IME/toque/DPI Android.
- **CST-017/111 — caso específico demonstrado:** `ImagePreview_DetachResizeAndReattachDoesNotReadADisposedBitmap` reproduziu `ObjectDisposedException` em `Bitmap.get_Size → ZoomImageView.Resize → SetZoom` após detach. A correção guarda dimensões imutáveis, libera pixels ao sair e recarrega a imagem a partir dos mesmos bytes codificados da sessão ao reanexar; mantém zoom e não relê o vault. O teste passou, assim como o fluxo real de prévia de arquivos. Isso **não identifica a causa** dos crashes antigos do carrossel nem do SIGSEGV (exit 139) observado em uma execução de CI do PR #877; foi solicitada nova execução isolada dessa última, depois cancelada pela concorrência do workflow, sem enfraquecer testes ou afirmar resolução do SIGSEGV.
- **CST-114:** composições já gravadas não ficam retendo o store de um vault antigo no cache de recuperação; drafts sujos permanecem retidos para retry. Não se afirma melhoria numérica de memória sem baseline.
- **Prova de integração intermediária:** os cinco jobs `urbe-checks` do commit `90c2606` passaram (run `41349966043`), incluindo builds Android/Windows e testes portáteis/E2E. As alterações posteriores de imagem/painel/cache têm sua própria rodada local/CI no handoff, sem herdar validação de um commit diferente.
- **Próximos formatos:** a investigação da especificação oficial EPUB 3.3 (https://www.w3.org/TR/epub-33/) confirmou exigências de pacote/manifest/spine, navegação XHTML e identificação ZIP específica; um HTML renomeado não entrega EPUB. Não se anuncia novo formato enquanto o adaptador, os recursos empacotados e o arquivo real não forem demonstrados. Isso permanece nos IDs de publicação existentes; não foi criado roadmap paralelo.

**Resultado local final deste incremento:** **1.242 testes Core e 444 testes UI aprovados**, sem falhas/skips; build Release portátil sem warnings/erros e 22 checks de consistência aprovados. O lote final inclui prévia matemática, ciclo de imagem e liberação do cache limpo. Os três entrypoints documentais C# agora encaminham ao plano 2.5 e preservam evidências históricas. A integração/CI do último commit e a validação Android real continuam registradas separadamente no handoff.


## 18. Continuidade para a release beta.39 — 01/11/2026

**Base:** main `7eb10c8`, após integração governada do PR #876. Escopo autorizado: continuar a consolidação e finalizar a release, com a decisão de abas explícitas acima. Não reabrir decisões anteriores nem confundir beta publicada com produto integralmente pronto.

- **CST-229:** reprodução vermelha de histórico automático, nova política de reutilização, botão + fixo fora da rolagem, seletor pesquisável existente, alvos de 48 dp e persistência de fechamento/aba ativa. O seletor usado para escolher nota não cria arquivos por engano; fechamento antigo não cancela uma seleção mais nova. Registro local descrito no DATA-CATALOG; nenhuma nota é migrada/apagada.
- **CST-149/157/158/160, parcialmente:** menus de nota/Área/pasta e paleta criam Publicações; ação nova do painel legado também cria Publicação. Cards e seleção de páginas antigas abrem a visualização, não o editor retirado. JSON, exportações e arquivos antigos são preservados. O módulo legado e suas tools ainda têm trabalho de retirada/adaptação; não marcar CST-149 completo por esconder entrypoints.
- **CST-160, parcialmente:** criação por pasta inclui notas atuais e subpastas por identidade, com fronteira de caminho exata e ordenação estável, sem Markdown duplicado no projeto. Não é consulta dinâmica de futuras notas; o critério completo permanece aberto.
- **CST-162/164, parcialmente:** impressão pelo adapter existente Android/Windows usa composição salva e fontes atuais, com CSS de impressão A4, margens e quebras por seção. O serviço da plataforma oferece salvar PDF; **diálogo aberto não é arquivo salvo nem motor editorial concluído**. Teste de contrato verifica título, fontes, matemática, omissão de comentários e papel; arquivo/paginação Android real continuam separados.

**Pesquisa e confronto das abas:** documentação oficial https://code.visualstudio.com/docs/editing/getting-started/userinterface e código https://github.com/microsoft/vscode/blob/main/src/vs/workbench/common/editor/editorGroupModel.ts foram examinados. O grupo distingue preview/pinned e substitui o preview; o Urbe adapta a ideia ao slot ativo e rejeita pin automático por digitação, porque o proprietário exige aba extra explícita. A issue oficial https://github.com/microsoft/vscode/issues/246058 mostra preferência diferente entre contextos e desconforto com gestos implícitos; é evidência qualitativa, não consenso. Mantidos um seletor já existente e o writer canônico do Urbe, sem importar editor web ou novo domínio.

**Entrega planejada:** beta.39 pelo workflow nativo existente, identidade/certificado preservados. Evidências locais, CI, integração e distribuição ficam no handoff `HO-20261101-urbe-beta39-explicit-tabs-publications`. Nenhum CST é fechado automaticamente com a tag da release.
