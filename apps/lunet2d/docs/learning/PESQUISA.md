# Lunet Aprender — pesquisa e confronto crítico

**22/11/2026.** Estudo de conteúdo, UX, arquitetura e regras de implementação. **Base examinada:** Ecosystem main 04640dfa (22/11/2026). A documentação não comprova execução ou instalação Android.

## 1. Auditoria interna

| Evidência | Achado e decisão |
|---|---|
| [MainActivity.Home.cs](../../src/Lunet.Android/MainActivity.Home.cs), LauncherPreferences.cs | O launcher é independente do projeto; Aprender deve ser quinto destino persistente, e IA global deve ser um recurso da Home, não sexta aba. |
| [MainActivity.Chat.cs](../../src/Lunet.Android/MainActivity.Chat.cs), [LunetAiSession.cs](../../src/Lunet.Ai/LunetAiSession.cs) | A sessão atual requer LunetProject e tem ferramentas de escrita. Chat global não pode inventar projeto temporário; exige conversa autônoma, somente texto, histórico separado. |
| [LunetAiSettings.cs](../../src/Lunet.Ai/LunetAiSettings.cs), MainActivity.Ai.cs | Provider, chave (Keystore), permissões e modelo já têm armazenamento global. Configuração só na área de trabalho, troca de modelo no projeto. |
| [ProjectStore.cs](../../src/Lunet.Core/ProjectStore.cs) | Projetos são pastas normais, com Game.cs vazio de objetos de demonstração. Aprender associa um projeto real, sem cópias opacas. |
| [Guias do Framework](../guides/primeiros-passos.md), [colisão](../guides/colisao.md), [entrada](../guides/entrada.md), [tempo](../guides/loop-e-tempo.md) | O primeiro desafio de desenhar/mover/encostar tem base de API documentada. Conteúdos avançados precisam de compatibilidade comprovada antes de habilitar. |
| [ADR 0007](../adr/0007-framework-ide-studio-code-first.md) | Código C# é fonte da verdade, Studios são opcionais; Aprender não implementa runtime secreto de gameplay. |

## 2. Referências externas

- [Harvard CS50 2D](https://cs50.harvard.edu/games/projects/): Pong, Flappy Bird, Breakout e projetos de complexidade crescente. Adotamos projetos funcionais progressivos, não códigos ou exercícios reproduzidos.
- [Godot: primeiro jogo 2D](https://docs.godotengine.org/en/stable/getting_started/first_2d_game/index.html), [fonte da documentação](https://github.com/godotengine/godot-docs/blob/master/getting_started/first_2d_game/index.rst): jogo com movimento, inimigos, placar e loop completo. Adotamos enunciados de entregas observáveis, mas não cenas/GDScript.
- [Unity Learn Pathways](https://learn.unity.com/pathways), [Junior Programmer](https://learn.unity.com/pathway/junior-programmer): trilhas, competências e projetos. Adaptamos progresso, sem exigir vídeo, conta ou credenciamento.
- [freeCodeCamp: estrutura de currículo](https://github.com/freeCodeCamp/freeCodeCamp/blob/main/curriculum/structure/curriculum.json), [gerador](https://github.com/freeCodeCamp/freeCodeCamp/blob/main/curriculum/src/build-curriculum.ts): inspeção de arquivos-fonte confirma separação conteúdo/UI e estrutura de IDs; adotamos conteúdo validável e versionado, sem a stack web.
- [IGDA Education SIG](https://www.edusig.org/): reforça abrangência multidisciplinar além de código (design, arte, áudio, produção). Não confundir currículo do Lunet com certificação profissional.
- [Pesquisa: worked-out examples e scaffolding em programação](https://journals.sagepub.com/doi/10.1177/07356331231174454): suporte gradual ajuda iniciantes a resolver problemas; escolha: IA oferece pistas e exemplos sob pedido, não solução imposta.
- [Android: navegação](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-and-nav-patterns), [offline-first](https://developer.android.com/topic/architecture/data-layer/offline-first): adoção de navegação compacta, conteúdo/progresso locais e rede opcional.

**Comunidades:** auditorias prévias [mobile workbench](../research/mobile-workbench-ux-2026-11-01.md) e [Home/Studios/Explorar](../../../../docs/architecture/audits/lunet-home-studios-explorar-2026-11-15.md) reúnem críticas recorrentes qualitativas sobre falta de espaço e foco no código no celular. Relatos não quantificam incidência; interface deve ser testada, não legitimada só com opiniões.

## 3. Confronto das alternativas

**Incorporar:** 60 cursos descobríveis desde o começo, com uma única entrega jogável inicial e demais explicitamente indisponíveis; seis estados honestos de progresso; projeto normal em C#; IA mentor explicativo; catálogo e estado local.

**Adaptar:** cinco destinos da barra (Aprender quinto), chat geral como tela da Home; seletor de modelo no projeto, configurações completas globais; lista de cursos paginada de 15 em 15 para não travar aparelhos modestos.

**Rejeitar:** criar projeto fictício para o chat geral (acesso indevido a arquivos), IA que realiza objetivos sem pedido, todas as aulas marcadas como disponíveis, certificado automático baseado em checkboxes, vídeos e cloud obrigatórios, quiz como único tipo de tarefa.

**Investigar depois:** avaliadores determinísticos do runtime, rubricas subjetivas, migração de progressos curriculares, internacionalização, conteúdo declarativo e ferramentas de mentor em projetos complexos.

## 4. Riscos e evidências

- Integridade de dados: progresso e chat global usam armazenamento privado próprio com escrita atômica; corrupção reportada sem sobrescrever o original.
- Separação de privilégio: chat global não pode receber ProjectStore, FileTools ou código do projeto. Histórico dos chats de projeto permanece por GameId.
- Honestidade pedagógica: checkboxes do primeiro projeto são declarações do estudante, não teste automatizado.
- Android: scroll/IME, fechamento/retorno, rotação, GPU e desempenho percebido não são demonstrados apenas por compilação; registrar ausência de teste físico conforme ADD-0035, sem exigir aprovação.
- Conteúdo de cursos futuros: a matriz é especificação; para liberá-los são necessárias implementação e compatibilidade real da API, testes, documentação e integração.
