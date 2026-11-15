# LUNET-510–512 — Estudo de Home, Studios independentes e exploração de referências
**Data:** 15/11/2026 · **Fonte:** somente `AbnerCruz/Ecosystem/apps/lunet2d` (`main` inspecionada em `5e2a8fa`); estudo com pesquisa externa. **Estado:** PROPOSTA DE UX E ARQUITETURA, sem código funcional implementado, sem DEVICE. **Ordem do proprietário:** transformar o menu principal em Home/área de trabalho contendo projetos, configurações, docs, Studios e exploração; tornar os Studios utilizáveis fora ou dentro de projetos, com reutilização de assets entre jogos; exploração offline de jogos/gêneros/mecânicas/estilos para inspiração, IA opcional, distinta do futuro catálogo de jogos publicados no Lunet.

## 0. Conclusão executiva e limites

A ideia corrige um problema estrutural: o Lunet tem ferramentas e projetos, mas a navegação ainda é **launcher de projetos**, e o Studio assume um `LunetProject` obrigatório. Recomenda-se **Home shell navegável sem projeto**, **Library global de assets** e **Explorer de inspiração offline** — três superfícies distintas, integradas pelos mesmos contextos (sem duplicar ferramentas). Os dados e artes do jogador devem continuar locais e exportáveis; não criar obrigatoriedade de nuvem, servidor ou IA. A implementação deve ser incremental, preservando a IDE, código de jogos, prévias, empacotador APK e o atual formato de projeto.

**Não confundir:** Explorer de inspiração = pesquisa e aprendizagem sobre jogos existentes; futuro catálogo = distribuição/descoberta de jogos construídos/publicados em Lunet. Esses domínios têm modelos, direitos de conteúdo, curadoria e ciclos próprios. Nada nesta proposta implementa uma loja, abre jogos protegidos no app ou transforma referências em assets licenciados.

## 1. Auditoria interna: interfaces atuais e acoplamentos comprovados

1. `src/Lunet.Android/MainActivity.cs:119`: `ShowProjects()` zera `_project`, desfaz Preview, sessão, editor, painéis e monta lista vertical com título “Seus jogos”, botões Novo jogo/Importar ZIP/Clonar e lista de projetos recentes. A única configuração de topo é o ícone ⚙; não há Home de ferramentas e biblioteca independente.
2. `src/Lunet.Android/MainActivity.Studio.cs`: `ShowSpriteAssets()`, `OpenSpriteStudio()` e `ShowTileMaps()` exigem `_project != null`; escrita usa `project.WriteText()` e `_project.Import()`. Não basta criar atalhos da Home: é preciso **separar a sessão do Studio do diretório de destino**.
3. `src/Lunet.Core/ProjectStore.cs`: `ProjectStore` lista e cria projetos por subpastas de um root, com `lunet.json` e `Game.cs`. `Create(ProjectCreationSettings)` já suporta configuração completa e `ProjectTemplate.Blank`; não reintroduzir demos/bolinha.
4. `src/Lunet.Android/MainActivity.Ai.cs` e `src/Lunet.Ai`: IA já tem configuração própria, permissões, contexto de arquivo/projeto, histórico; Home deve expor **configurações globais** sem inicializar projeto ou forçar chat.
5. `apps/lunet2d/ROADMAP.md` e SPEC §13: Sprite/Tile/Audio/Palette/Particle/Data Studios parcialmente implementados; UI Studio, Animation Studio, Physics Studio e Atlas Studio ainda precisam de fechamento. Redesenhar a Home não os transforma em completos.
6. A análise de 14/11 (`docs/architecture/audits/lunet-diagnostico-geral-2026-11-14.md`) avaliou uma revisão anterior da UX e não contém as últimas correções de IA/Studios. Esta pesquisa complementa a auditoria incremental de 15/11.

## 2. Pesquisa externa + lições práticas

| Referência | Observação concreta | Aplicação e ressalva |
|---|---|---|
| Android adaptive navigation ([Android Developers](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-and-nav-patterns)) | Navegação deve adaptar-se ao espaço: barra compacta e rail em telas expandidas | No celular usar poucos destinos primários; tablet/desktop usar rail/painéis. Não copiar uma dashboard desktop inteira no Android |
| Android offline-first ([arquitetura oficial](https://developer.android.com/topic/architecture/data-layer/offline-first)) | Fonte de leitura local mesmo com sincronização opcional | Catálogo e biblioteca funcionam sem rede. Atualizações online apenas enriquecem cache local |
| Godot AssetLib ([documentação](https://docs.godotengine.org/en/latest/community/asset_library/using_assetlib.html)) | Distinção entre projetos/templates e assets usados no editor; busca por categoria | Separar criação de jogos, edição de recursos e descoberta; não confundir loja com biblioteca pessoal |
| Unity Asset Manager ([conceitos](https://docs.unity.com/en-us/asset-manager/basic-concepts), [ações](https://docs.unity.com/en-us/asset-manager/asset-action-menu)) | Asset pode ter arquivos-fonte/derivados, metadados e vínculos a mais de um projeto | Modelo `AssetPackage` com hash, revisão, formatos e importação por cópia; não copiar dependência de cloud/organização |
| itch.io ([buscar e navegar](https://itch.io/docs/creators/getting-indexed)) | Texto pesquisado e navegação por tags atendem intenções distintas | Busca de título + descoberta multifacetada; tags de mecânica/arte devem ser próprias |
| Wikidata ([licenciamento CC0](https://www.wikidata.org/wiki/Wikidata:Licensing), [propriedades de videogames](https://www.wikidata.org/wiki/Wikidata:WikiProject_Video_games/Properties)) | Dados estruturados livres sob CC0; P136 gênero, P4151 mecânica, P14603 estilo visual | Fonte inicial admissível com curadoria, mas cobertura de mecânicas/estilos é desigual e não garante thumbnails licenciadas |
| IGDB ([API e termos](https://api-docs.igdb.com/index.html)) | API suporta cache local e prevê atribuição/parceria para uso comercial | Investigar como enriquecimento futuro; não colocar credenciais Twitch nem imagens sem licença em APK |
| OpenGameArt ([licenças](https://opengameart.org/content/faq)) | Reúne ativos sob CC0, CC-BY, CC-BY-SA e outras condições | Biblioteca deve registrar origem/licença/atribuição por asset; referência artística de jogo comercial não é asset reutilizável |
| Discussões mobile Godot ([Reddit, 2025](https://www.reddit.com/r/godot/comments/1pk5p9z/android_godot_app_is_too_tight_to_be_usable/), [2026](https://www.reddit.com/r/godot/comments/1t4cx4c/i_made_a_prototype_in_godot_mobile_because_i_cant/)) | Relatos de tela apertada, painel escondendo editor, toque impreciso | Área de trabalho mobile com navegação por superfícies e editor maximizado, não janelas sobrepostas. Relatos qualitativos, não benchmark |

**Confronto crítico:** “Studios fora do projeto” é correto como **ponto de entrada e local de edição**, não implica que projetos devam referenciar uma única cópia mutável da biblioteca. O contrário gera jogos quebrados quando a biblioteca muda ou é apagada. Recomendo copies imutáveis/versionadas no projeto, com opção posterior de atualizar explicitamente a fonte importada.

## 3. Home / área de trabalho: arquitetura de informação e UX

### Destinos de primeiro nível

- **Início/Home:** continuar projeto recente, projetos fixados, “Novo jogo” com configuração completa, atalhos de Studios, referências salvas, notas/ajuda contextual, status dos builds em andamento. Nunca abrir projeto automaticamente.
- **Projetos:** grade/lista buscável com ordenação, favoritos/fixados, previews locais leves, renomeação/duplicação/importação ZIP/clonagem, backups/export e ação destrutiva com confirmação. Projetos existentes continuam abrindo no editor preservando sessão.
- **Studios:** grade de ferramentas (Sprite, Tile, Audio/Music, Palette, Particle, Data, e futuras Animation/Atlas/UI/Physics), cada uma abrível **sem projeto**, com seletor claro `Biblioteca pessoal` ou `Projeto <nome>`. Status dos Studios não implementados deve ser honesto, sem botão falso.
- **Biblioteca:** assets próprios organizados por tipo, tags, coleções, autor/licença, versão e miniaturas; abrir no Studio e “Usar em…” um ou vários projetos. Destaque a natureza da posse: assets do usuário ≠ screenshots de jogos de referência.
- **Explorar:** biblioteca de referências de jogos/gêneros/mecânicas, offline-first e sem integração com uma loja; seleção de jogos e “Salvar inspiração”, notas sobre mecânicas e link público quando conectado.
- **Documentação:** API offline, guias e tutoriais; acesso contextual direto do editor e Studios; não exigir conexão para ler o básico.
- **Configurações globais:** aparência (tema claro/escuro, cor/acento, tamanho da fonte, fonte monoespaçada do editor, densidade), preferências/idioma/teclado, IA e modelos/permissões, caminhos de armazenamento/exportação, acessibilidade e diagnóstico. **Configuração do projeto** (orientação, tamanho lógico, PackageId, version) permanece própria do jogo; não misturar.

**Layout proposto:** na janela compacta: barra de navegação com quatro destinos prioritários `Home · Projetos · Studios · Explorar`, acesso a Biblioteca via Home/Studios, Docs via busca/ícone e Configurações pelo topo. Em telas amplas: rail expandido e conteúdo em painéis. O modelo de navegação é uma recomendação de UX, não obrigação de usar Jetpack Compose num projeto em C#/Android Views.

**Wireflow:** abrir app sem projeto → Home → (a) clicar projeto e abrir editor; (b) Studio → Biblioteca → criar/reutilizar asset → usar em projeto → voltar ao Studio sem perder edição; (c) Explorar → filtrar → guardar referência → opcionalmente associar ideia a um projeto → abrir projeto; (d) Configurações globais → voltar à Home. Back deve voltar de maneira previsível, sem fechar app indevidamente, e salvar estado selecionado.

**Persistência de UI:** manter última seção, ordem/fixações, busca e filtros de inspiração; Home não deve compilar projeto nem carregar arquivos gráficos grandes na thread principal. Projeto aberto e sessão de edição continuam preservados; sair para Home não deve descartar alterações não salvas ou abortar IA que está trabalhando. Não alegar que serviço foreground sobrevive a force-stop.

## 4. Studios autônomos e biblioteca global

### Duas sessões, um Studio

`StudioSession` (modelo de autoria, sem Android) recebe `IAssetWorkspace` com `RootPath`, `OwnerKind=Library|Project`, `Permissions`, `Save/Import`, versão e eventos. A UI Android fornece o workspace escolhido, sem usar `_project!` como requisito; o mesmo editor é chamado de dentro do projeto e da Home. **Não criar segundo Sprite Studio** nem duplicar código.

**Biblioteca global local (proposta):** `AppData/Lunet/Library/<asset-id>/`, com pacote imutável por versão: fontes `.sprite.json/.song.json/.tilemap.json` quando disponíveis, derivados `.png/.wav`, `asset.json` (id, title, kind, tags, creator, sourceUrl, license, attribution, hash sha256, version, formatVersion, created/updated). Cada recurso conserva seus dependentes (spritesheet, paleta, tileset); export/import em pacote ZIP com caminhos validados. Não depender de um projeto para existir, nem exportar catálogo inteiro involuntariamente com um jogo.

**Usar em projeto:** `Library → selecionar asset(s) → Usar em… → projeto(s) → prévia de arquivos → importar cópia atômica para Assets/ e Content/ conforme tipo → atualizar índice de uso`. Ao importar, copiar *fonte editável* e *derivado de runtime*; atualizar referências relativas sem mudar código arbitrariamente; conflitos de nome/hash → renomear ou sobrescrever só mediante escolha explícita e backup. Para 3 projetos, 3 cópias independentes; alteração no asset de origem oferece “Atualização disponível”, não sobrescreve projetos silenciosamente.

**Semântica alternativa:** links diretos/compartilhamento de arquivo economizam espaço, mas são inseguros para APK offline, ZIP, Git e projetos que mudam de aparelho. Não adotar links como padrão. Revisões e uma opção futura de “sincronizar esta cópia manualmente” podem vir depois com testes de integridade.

**Segurança/backup:** excluir um asset da Library **não apaga** seus usos existentes nos projetos; asset editado em um projeto **não altera** a versão global sem `Salvar na biblioteca`; importar/licenciar verifica compatibilidade, SHA, extensão, tamanho, path traversal e quota de memória; arquivos grandes e previews não bloqueiam UI. Credenciais de IA não entram no asset package.

**Testes necessários:** criar sem projeto, fechar/reabrir, export/import, usar em dois projetos, renomear, importar mesmo id/version, atualização controlada, desfazer conflito, apagar Library preservando projetos, atualizar derivado sem perder fonte, APK offline instala com recurso copiado e modelo carregado. DEVICE com assets grandes e falta de espaço.

## 5. Explorar: inspiração por jogos, gêneros e mecânicas (não loja)

### Taxonomia com facetas ortogonais

Evitar um único campo `genre`: permitir **gênero** (plataforma, RPG, estratégia, simulação, puzzle, ação, survival); **mecânicas** (construção, crafting, exploração, voo/montaria, combate em turnos, geração procedural, automação, agricultura, furtividade); **perspectiva** (top-down, isométrica, lateral, primeira pessoa); **arte** (pixel art, vetorial, low-poly, pintura, monocromático); **modo** (solo, coop, multiplayer); **ritmo/estrutura** (real-time, turnos, roguelite, sandbox, narrativa); **plataforma** (Android, PC, console), ano e acessibilidade. “2D/3D” é dimensão de apresentação, não gênero.

Filtros: OR entre opções de **mesma faceta**, AND entre facetas diferentes como padrão, com modo explícito “qualquer um/todos” para mecânicas; busca por título, descrição e sinônimos PT-BR/EN, sem depender de texto em inglês exato; contadores e mensagem útil quando 0 resultados. Ordenação por relevância/alfabética/ano e agrupamento por mecânica para exploração pedagógica. Cada ficha explica **por que apareceu** (tags que coincidiram), e apresenta 3–5 aprendizados de design, não só capa.

**Exemplo:** `perspectiva: top-down` + `mecânica: montaria/voo` + `visual: pixel art` → catálogo retorna referências correspondentes com evidência da tag; se nenhuma existir, recomendar ampliar facetas, não inventar jogo ou tag.

### Arquitetura local-first e fontes

- **V1 offline obrigatória:** banco/arquivo embarcado com **amostra curada e verificável** (por exemplo, 50–100 entradas iniciais sujeitas a licença e qualidade, não prometer cobertura universal), títulos, IDs, metadados, fontes e referências por link. Pesquisa e filtros 100% locais; imagens apenas próprias/licenciadas, ou placeholders tipográficos. Não embutir screenshots comerciais sem direito.
- **Metadados:** Wikidata CC0 serve como fonte auxiliar, porém as declarações de mecânicas/estilos podem ser escassas. Criar camada editorial de taxonomia do Lunet (tabelas locais) com `source/provenance`, idioma, revisão e confiança por afirmação; evitar assumir tags de outro site como verdade. IGDB pode enriquecer mediante integração/termos/atribuição, sem ser pré-requisito.
- **Armazenamento:** começar com catálogo JSON versionado + índice em memória para conjunto pequeno. Se o tamanho, consultas e histórico justificarem, medir SQLite/FTS5 no Android/.NET com testes; não acoplar domínio portátil a Room/Kotlin por reflexo.
- **Atualização opcional:** pacotes de catálogo versionados com migração, hash, origem, data, licença e cache local; “Atualizar referências” só com rede disponível; app sempre funciona após desconectar, sem tela vazia. Conflitos preservam favoritos/notas locais.
- **Interação:** salvar referência em coleção de inspiração, adicionar observações, selecionar vários jogos e comparar: mecânicas, UI, câmera, progressão e trade-offs. Uma relação opcional `inspiration-reference → projectId` gera notas vinculadas, mas nunca copia assets/código do jogo comercial para o projeto.
- **IA opcional:** `Comparar referências`, `Decompor gameplay`, `Propor mecânicas combinadas`, `Transformar em protótipo de design`. No modo offline sem provedor local, os filtros e resumos curados continuam disponíveis. Enviar apenas cartões e filtros escolhidos, com consentimento e transparência se provedor remoto; não enviar o código/arquivos do projeto por abrir Explorar. IA não fabrica atribuição, licença ou fato de jogo.
- **Não implementar agora:** streaming de gameplay de terceiros, scraping agressivo, reprodução de vídeos dentro da base offline, catálogo de jogos Lunet, marketplace de assets, feed de tendências sempre-on, recomendador que exija login.

### Direitos e diferenças essenciais

**Jogo de referência** pode ter metadados descritivos indexados e links; sprites/screenshots/trilhas comerciais não viram recursos reutilizáveis. **Asset reutilizável** precisa de autoria/licença/permissões explícitas, e sua licença/atribuição acompanha cada importação. Não empacotar capinhas de lojas ou imagens obtidas sem licença num APK offline; para primeira versão usar título/texto e imagens de licença verificada.

## 6. Critérios de aceite e roadmap incremental

**LUNET-510 — Home shell mobile-first (estudo, implementar depois):**
- Tela inicial abre **sem projeto**, mostra continuar/criar, acesso direto aos destinos, configurações globais, docs e status, com persistência e Back seguro.
- Retrato e paisagem + tablet, sem teclado abrindo sozinho e sem reduzir o editor a poucas linhas; prints do Android e teste de acessibilidade (alvos e leitores de tela).
- Nenhum projeto, chat/IA ou alteração em andamento desaparece por trocar Home/editor.

**LUNET-511 — Studios sem projeto + biblioteca local:**
- Abrir um Studio da Home, criar asset sem projeto, fechar/reabrir, importar em 2 projetos sem eles compartilharem ponteiro editável, editar versão global sem sobrescrever projeto, exportar o projeto e gerar APK contendo os recursos.
- Compatibilidade dos projetos existentes sem exigir migração destrutiva; falha de espaço/permissão com rollback, checksums/licença/proveniência visíveis.

**LUNET-512 — Explorar offline gêneros/mecânicas/referências:**
- Iniciar sem rede e encontrar jogos conhecidos por busca e filtros combinados; mostrar origem/tag responsável e textos curados. Filtro vazio, sem resultados, favoritos/coleções e notas sobrevivem a reinícios.
- Distinguir jogo de referência do catálogo/publicação Lunet e de asset reutilizável; nenhum screenshot não licenciado no pacote base.
- IA e atualização online opcionais, nunca pré-requisito de uso; nenhuma chave ou código de projeto transmitido sem consentimento.

**Ordem sugerida:** consolidar primeiro modelo de navegação e persistência da Home (510); construir contexto `IAssetWorkspace` e pacote/biblioteca (511); entregar base offline pequena, correta e ampliável do Explorer (512); só depois IA/integrações remotas. Pode haver trabalho paralelo se contratos forem isolados e testes não dependerem de decisões abertas.

## 7. Riscos, contrapontos e decisões ainda não consolidadas

- **Home carregada:** projetos + 10 Studios + exploração + docs + configurações em uma página causariam a mesma poluição que se deseja eliminar. Usar seções escaneáveis, favoritos/personalização e descoberta progressiva, não grade infinita ou menu de 30 ícones.
- **Asset global único:** se `Assets/palette.json` é a autoridade dentro de um projeto hoje, a Library não pode alterá-lo por trás. Evitar alterar IDs e cache de runtime sem migrar explicitamente.
- **Portabilidade:** não escolher Compose/Avalonia como tecnologia definitiva da Home apenas por referência de UX; o spike Avalonia já autorizado (DEC-0062) tem critérios próprios. Reaproveitar modelos e componentes, adaptar host depois.
- **Curadoria:** filtros profundos exigem descrições confiáveis; uma lista de milhares de títulos sem mecânicas confiáveis gera resultados inúteis. Começar pequeno com tags verificadas e explicação do match.
- **Online/copyright:** dados de terceiros são licenças diferentes, e imagens de jogos não são automaticamente livres. Nunca misturar referências com asset store.
- **Autorização:** o proprietário autorizou **estudo e registro** desta direção agora. A escolha definitiva de novo formato global de asset, migrações, mudança de especificação/host e permissões de rede exige ADR/decisão, quando NN-011 aplicável. LUNET-510–512 são itens propostos, não features concluídas nem promessas de release.

## 8. Orientação ao próximo agente

1. Reler `MANIFEST.md`, `AGENTS.md`, `apps/lunet2d/AGENTS.md`, SPEC/ROADMAP/ADR 0007, DEC-0062, ADD-0034/0035, LUNET-509 e este estudo; verificar main, PRs e status atual. **Somente Ecosystem.**
2. Criar protótipo de fluxo da Home no Android em retrato e tablet, com screenshot de emulador e validação DEVICE (moto g32, Android 13 ou dispositivo-alvo disponível) **antes** de expandir o menu.
3. Especificar `IAssetWorkspace` e testes de fonte/derivado/importação multi-projeto, sem mover projetos existentes ou criar links globais frágeis; validar export ZIP e APK.
4. Especificar schema de catálogo offline/curadoria/atribuição e teste de busca multifacetada com exemplos, antes de conectores e IA.
5. Reportar falhas e progresso nos handoffs, sem marcar `[x]` antes de conclusão/CI/DEVICE; não refazer Framework/Compiler para construir Home.
