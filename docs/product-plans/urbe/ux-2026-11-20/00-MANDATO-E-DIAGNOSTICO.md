# Numa / Urbe — reconstrução da experiência de produto
**20/11/2026 · Orientação ao agente de desenvolvimento, originada da solicitação do proprietário.**
**Estado:** proposta de execução e diagnóstico; não altera automaticamente ADR, MANIFEST, grants, versão ou requisitos CST. A direção de produto expressa pelo proprietário deve ser registrada nos documentos canônicos conforme governança. Não é uma nova lista de features para marcar como concluídas.

## 1. Ordem de trabalho
Finalize a tarefa que estiver efetivamente em andamento, protegendo alterações e registrando seu resultado, testes e handoff. Não abandone correção urgente nem desfaça trabalho de outro agente. Em seguida, priorize esta frente transversal, antes de continuar a acrescentar funcionalidades desconectadas. Entregue incrementos seguros, não uma reescrita geral nem um período indefinido de pesquisa. O objetivo é recuperar a experiência como um todo, não maquiar telas. Se uma dependência bloquear uma fatia, continue pelas independentes autorizadas.

Leia os outros documentos deste conjunto, o plano Numa 2.6 (docs/product-plans/urbe/Numa_Consolidacao_2.6.md), o AGENTS raiz, MANIFEST, decisões vigentes, UX.md, PRODUCT-DIRECTION e ROADMAP C#. Atualize o roadmap e os contratos pertinentes sem substituir a consolidação, sem renumerar CST e sem declarar gates antigos automaticamente aprovados. A diretriz ADD-0035 vigente dispensa validação no aparelho como portão de integração; isso não transforma CI em evidência DEVICE. Registre as limitações em known_issues. O estágio continua beta e só o proprietário decide a promoção.

## 2. Dor a resolver (direção expressa pelo proprietário)
O editor alcançou um nível de qualidade que o proprietário considera excelente e praticamente completo. Ele não quer recomeçar o editor. Ainda assim, o produto inteiro não está pronto para sair do beta: a árvore de arquivos virou também depósito de estado interno, a IA produz objetos na ferramenta errada, painéis especializados faltam, navegação e composição visual são percebidas como desorganizadas, e usar o aplicativo aumenta o trabalho de organização. Funcionalidades isoladamente poderosas não se encaixam. Ele quer um assistente comparável à experiência dos melhores sistemas, não meramente um chat que chama ferramentas.

**Resultado desejado:** um ambiente local-first em que a pessoa escolhe o que quer fazer (estudar, escrever, produzir, consultar, organizar); vê resultados onde espera encontrá-los; tarefas do app não contaminam suas notas; e a IA escolhe as mesmas capacidades nativas que a UI manual. A pessoa não aprende a arquitetura interna para utilizar o produto. Estética, tempo, continuidade, confiança, organização e flexibilidade são requisitos funcionais.

## 3. Auditoria independente: evidências, não suposições
Snapshot examinado: [main 3f002029, beta.63](https://github.com/AbnerCruz/Ecosystem/commit/3f002029ce417c17dd9438636354bc9c499e5a74). Antes de implementar, reauditar HEAD, PRs, Issues e handoffs. Isto é amostragem estática, não auditoria exaustiva de todo o código nem teste do APK.

Prefixo dos caminhos de código: apps/urbe/csharp/src/. Os links fixam o snapshot, não a main móvel.

| Fato observado | Evidência | Consequência / investigação |
|---|---|---|
| Catálogo central agrega ferramentas de notas, Publicações, plugins, memória, etc., sem família explícita de flashcards; ToolsFor retorna Tools | [AssistantPanel.cs L331–346](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Client/AssistantPanel.cs#L331-L346) | Auditar ferramentas efetivas após filtros/grants/skills. Disponibilidade na UI não implica invocabilidade pelo modelo. |
| Guia usa busca por stems, soma ocorrências e aceita qualquer pontuação positiva, devolvendo até três trechos; não exige coincidência do conceito principal | [AssistantGuideTools.cs](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantGuideTools.cs) | Hipótese causal forte para páginas irrelevantes em consultas flashcards; reproduzir antes de aprovar correção. |
| Estudo abre editor e fonte; quando não há nota, pode criar Anotações de estudo.md | [MainView.Study.cs L15–39](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Client/MainView.Study.cs#L15-L39) | Estudar sem intenção de produzir nota não deve gerar arquivo canônico automaticamente. Rever fluxo em conformidade com o proprietário. |
| Estado privado da IA já existe: AssistantStore é local e fora do vault, conversa e anexos são descritos como privados; listagem já lê cabeçalhos | [AssistantStore.cs L270–330](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantStore.cs#L270-L330), [AssistantAgent.cs L19–27](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantAgent.cs#L19-L27) | Não afirmar que todos os anexos/chats vazam para o vault. Reaproveitar a separação existente, identificar caminhos restantes e consolidar. |
| Plugin autorizado é escrito em Personalização/plugins e ligado | [AssistantPluginTools.cs L16–37](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantPluginTools.cs#L16-L37) | Uma ferramenta de extensão não equivale a um objeto de estudo. Proteger permissões e intenção. |
| App já tem classificação/roteamento de artefatos | [ArtifactModel.cs](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/csharp/src/Urbe.Core/ArtifactModel.cs) | Estender/validar o contrato, não inventar catálogo concorrente. |
| SM-2, cartões pergunta::resposta e revisão da nota foram implementados | [core](https://github.com/AbnerCruz/Ecosystem/commit/1b15a116086a133d92e477e358d4de757e3ef018), [UI](https://github.com/AbnerCruz/Ecosystem/commit/3158e70c93b524614f31e50d0e38de1a6580b8a0) | Preservar parser e progresso existente quando válidos; oferecer visão global e API nativa. Confirmar estado corrente. |
| A beta.63 integrou create_video em uma chamada e correção do painel de etapas | [PR #1003](https://github.com/AbnerCruz/Ecosystem/pull/1003), [commit create_video](https://github.com/AbnerCruz/Ecosystem/commit/9963cad48d2c4ddbcc568133c06bee911ed73047), [render incremental](https://github.com/AbnerCruz/Ecosystem/commit/e5c6cf9e66373eaa26f6311ba55a573e26dc3357) | Provas de direção promissora, não reabrir por reflexo. Auditar resultados e aplicar o princípio tarefa semântica + render incremental a outras superfícies. |
| UX.md contém princípios bons e referências a cidade, HUD, estúdio legado e retomada incondicional; ROADMAP cita 2.5 enquanto AGENTS local cita 2.6 | [UX.md](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/docs/csharp/UX.md), [ROADMAP](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/docs/csharp/ROADMAP.md), [AGENTS](https://github.com/AbnerCruz/Ecosystem/blob/3f002029ce417c17dd9438636354bc9c499e5a74/apps/urbe/AGENTS.md) | Reconciliar normas e guia com decisões atuais: nada de reintroduzir cidade; Publicações substitui Páginas; base é área de trabalho; sessão recente retoma, sessão encerrada volta à home. |

### Incidente reproduzível da beta.60 (export local analisado, não anexado ao GitHub)
Pedido: dois cartões nativos sobre animais. 5 buscas do guia retornaram trechos sem instrução pertinente. Depois search_notes/read_notes recuperaram exemplo anterior de plugin, plugin_guide e write_plugin produziram um plugin ativado. A revisão obrigatória releu arquivo e links, mas não verificou o requisito de usar o recurso nativo. Resposta chamou o painel criado de nativo. Export: 13 chamadas de modelo, 12 chamadas de ferramenta, 83,444 s de execução, 68,733 s até primeiro resultado útil, 239.444 tokens de entrada acumulados, 192.938 cacheReadInput, 1.777 de saída, USD 0,008630155 reportados; repeats=0 e verified=1. Entrada acumulada NÃO equivale a tokens novos ou cobrança integral. O caso é concreto, não prova que toda execução seja assim. Reproduzir em beta.63 e registrar diferença.

A causa não é necessariamente o modelo. É uma cadeia: descrição e exposição de capacidade → recuperação do guia → escolha de operação → armazenamento → apresentação → verificação de intenção. Corrigir só prompt ou só estilo quebra a cadeia de novo.

## 4. Princípios vinculantes à execução proposta
1. Notas são a autoridade de conteúdo autoral em Markdown, não a única interface nem depósito obrigatório de qualquer dado.
2. Uma operação de domínio, múltiplas superfícies: manual, IA, plugins permitidos, contextual, biblioteca. Não duplicar dados ou criar um segundo editor.
3. Separar espaço de trabalho humano, objetos de produto, arquivos de apoio, estado privado e cache; acesso explícito ao espaço técnico.
4. Nenhum resultado é “nativo” porque foi criado por ferramenta nativa de plugin. Nativo é a capacidade do produto, com contrato e lifecycle efetivos.
5. Beleza é hierarquia de informação, tipografia, espaçamento, estados, movimento com sentido, responsividade e redução de esforço; não excesso de cards, sombras, gradientes e vidro.
6. Pesquisa aplicada, não quantidade ornamental: evidência externa → contraexemplo → adaptação → teste → decisão documentada.
7. Editor, vault local, backups, autoria, permissões, liberdade de composição e operações nativas permanecem. Não migrar UI para web, recriar cidade ou introduzir software proprietário sem análise.

## 5. Navegação dos documentos
- [01 — arquitetura da informação, dados e lifecycle](01-ARQUITETURA-DA-INFORMACAO.md)
- [02 — design, painéis e jornadas](02-DESIGN-E-JORNADAS.md)
- [03 — inteligência nativa e execução](03-ASSISTENTE.md)
- [04 — referências, método, roadmap e critérios](04-PESQUISA-E-ACEITE.md)

Este conjunto não prova que o app foi corrigido. O agente deve converter as propostas em requisitos/ADR/roadmap verificáveis e anexar evidência de cada incremento. O usuário quer a experiência efetiva, não outro documento de promessas.
