# Changelog

## 0.0.1-dev.1000599 — Inspector e Desempenho ao lado do jogo

- **Inspector:** com o jogo rodando, o botão de controles deslizantes na barra do jogo (ou Painéis → Inspector) mostra os valores do jogo ao vivo.
  - Mude um número, uma cor, um texto, liga/desliga ou uma opção, e o jogo reage na hora. Com o jogo pausado, o quadro é redesenhado na mesma hora.
  - Campos com `[Range]` ganham um controle deslizante. Valores inválidos são recusados com uma mensagem, sem mexer no jogo.
  - A mudança vale até o jogo parar; o código não muda. Com mais de 12 campos aparece um filtro.
- **Desempenho:** o botão de pulso mostra o tempo de cada quadro num gráfico, com as linhas de 60 e 30 quadros por segundo.
  - Mostra média, o tempo abaixo do qual ficam 95% dos quadros, o pior quadro e quantos passaram de 16,7 ms.
  - Tocar numa barra mostra aquele quadro: Update, Draw e passos.
  - Os quadros são guardados mesmo com o painel fechado. **Copiar relatório** copia os números em texto.
- No celular, Inspector, Desempenho e IA abrem numa gaveta de meia tela, com o jogo visível acima.

## 0.0.1-dev.1000598 — Volta onde você parou

- Ao abrir um jogo, o editor volta ao último arquivo, ao cursor e à linha em que você estava. Isso vale também depois de fechar o app.
- Os arquivos que você abriu aparecem primeiro na faixa de arquivos.

## 0.0.1-dev.1000597 — Um só Lunet, com a interface nova

- O Lunet abre na interface nova, com seus jogos, abas e o teclado próprio da IDE. Ele atualiza por cima do app instalado e encontra os mesmos jogos.
- Nada some: Jogar, Studios, Aprender, Biblioteca, Explorar e Ajustes ficam na tela inicial. Dentro de um jogo, o botão **Mais** abre IA, Recursos, Notas, Git, Gerar APK e o Editor completo. Essas telas ainda são as anteriores; voltar salva e retorna à interface nova, que já mostra o que mudou.
- **Código com cores e números de linha:** o editor mostra o C# colorido, a numeração das linhas e a linha do cursor.
- **Teclado redesenhado:**
  - Uma faixa no topo com as abas (ícones) e sugestões do que você está digitando: tipos e métodos da API e do seu jogo. Tocar completa, como no Gboard.
  - Letras maiores em teclas claras, modificadores em teclas escuras e Enter em destaque.
  - Um balão mostra a tecla tocada; os acentos aparecem acima da tecla quando você segura.
  - O toque nas teclas vibra levemente.
- **Erros enquanto você digita:** o código com erro ganha um sublinhado ondulado (vermelho para erro, amarelo para aviso), a barra de topo diz quantos erros há e o painel Problemas lista cada um. Tocar num item leva à linha, sem precisar rodar o jogo.
- **Sugestões inteligentes:** depois de `.` o teclado sugere os membros reais daquele objeto, e dentro de uma chamada mostra a assinatura com o parâmetro atual entre colchetes.
- **Paleta de comandos:** a lupa na faixa do teclado (ou Ctrl+Shift+P no computador) abre a paleta.
  - Comandos: formatar, ir para a definição, ver referências, renomear em todos os arquivos, correções rápidas, comentar, duplicar, mover ou apagar linhas.
  - Atalhos de busca: `@` mostra a estrutura do arquivo, `:` vai a uma linha, `#` busca no jogo inteiro e `/` busca no arquivo.
- **IA do jogo na interface nova** (Painéis → IA do jogo):
  - **Estudar** é o padrão: a IA explica e não mexe em nada. **Fazer** é escolhido para uma mensagem só.
  - Tudo é salvo antes de enviar. Quando ela altera o jogo, aparecem os arquivos mudados, o diff de cada um e o botão **Desfazer estas alterações**.
  - Parar cancela de verdade.
  - A chave é configurada uma vez e vale para o app inteiro.
- O app separado "Lunet Nova" deixa de existir. Quem o instalou pode desinstalá-lo; os jogos criados nele ficam na pasta dele e não aparecem no Lunet.

## 0.0.1-dev.1000596 — Teclado próprio da IDE (Lunet Nova)

- No editor de código do celular, o Lunet Nova usa o **teclado próprio da IDE** no lugar do teclado do sistema, com três abas na mesma superfície:
  - **Teclas:** teclado completo como o do Gboard, com números em cima, Shift (dois toques travam as maiúsculas) e acentos no toque longo (á ã ç…). Arrastar no espaço move o cursor; arrastar o apagar para a esquerda apaga palavras; Enter mantém a indentação.
  - **Atalhos:** as teclas de código de deslize, mais Desfazer, Refazer, início e fim da linha.
  - **Métodos:** a API do Lunet e os métodos do seu jogo, por namespace e classe, com Recentes e Mais usados no topo. Um toque insere a chamada com o cursor dentro dos parênteses.
- Trocar de aba não mexe no cursor nem na seleção. A tecla 🌐 volta ao teclado do sistema (ditado, outros idiomas), e "Teclado Lunet" traz o nosso de volta.
- Com teclado físico conectado, o teclado Lunet sai da frente. Com leitor de tela ligado, o padrão é o teclado do sistema.
- As Notas continuam com o teclado do sistema.

## 0.0.1-dev.1000595 — Lunet Nova no celular

- Novo app **Lunet Nova** (`Lunet-Nova-…-arm64.apk` nesta release): a interface nova em Avalonia, no Android. Instala **ao lado** do Lunet atual, sem substituí-lo; cada um tem seus próprios jogos.
- Barra de código acima do teclado: dez teclas de 48 dp; toque dá o símbolo do meio, deslizar para cima, baixo, esquerda ou direita dá o símbolo do canto. `{` cria o bloco já indentado; `(`, `[` e aspas fecham o par e envolvem a seleção; setas e Tab.
- Com o teclado aberto, as abas e a folha de Console/Problemas saem da frente para o código ficar visível.
- O app atual (`Lunet-…-arm64.apk`) segue igual.

## 0.0.1-dev.1000594 — Casca nova em Avalonia (fatia 1)

- Nova interface única do Lunet (`Lunet.Client`), usada hoje pelo app de computador: lista de jogos, e dentro de cada jogo as abas Código, Jogo, Arte, Mapa, Som e Notas.
- O jogo roda dentro da área de trabalho, ao lado do código em telas largas, com pausar, avançar um quadro, parar e velocidade de 0,25× a 2×.
- Console e Problemas abrem como folha inferior no celular e coluna lateral em tela larga, sem diálogos.
- O app Android desta versão não muda: a cabeça Android da casca nova é a próxima etapa.

## Próxima versão — evolução do Framework (candidata, não publicada)

- SpriteBatch: passagem ordenada opt-in, chaves explícitas e estáveis, guarda de passagem e cancelamento; Begin convencional preservado.
- Projetos novos: Project/Imports.cs visível, presets e política de compilação explícita. Essencial provisório até validação do Amplo.
- Context experimental para callbacks síncronos, com acesso aos objetos reais do jogo e dependências explícitas preservadas.
- Dois jogos de referência e testes de regressão. Estado de validação no plano de evolução de 24/11.

## 0.0.1-dev.1000591 — F6 · Mapas mentais e Studios

- Mapas mentais em Notas, com ramos, ligações, arraste, zoom, histórico, referências e apresentação.
- Tilemaps, paletas, partículas, dados e edição WAV disponíveis no Studio global. Publicação e cópias independentes pela Biblioteca.
- Pacotes de tile e tabelas levam suas dependências. Importações registram a origem e preservam arquivos em conflito.

## 0.0.1-dev.1000590 — F5 · Aprender e manual offline

- Dez missões com referência direta, botão de Preview e pista contextual em Estudo.
- Retomada por GameId após renomear. Marcações isoladas por jogo com recuperação da gravação anterior.
- Manual com Tutoriais, Tarefas, Referência de API e Conceitos. Percurso de jogo em branco até APK com exemplos executáveis.
- [Escopo e conferência da F5](docs/learning/F5-ENTREGA.md).

## 0.0.1-dev.1000589 — F4 · Editor nativo e teclado

- Complemento de programação sobre o teclado Android com desfazer/refazer fixos, pares, blocos, recuo, cursor e busca.
- Parâmetro ativo e sobrecargas de uma chamada com Roslyn. Busca por símbolo, definição, documentação offline e retorno à posição anterior.
- Aba padrão substituível, abas extras por escolha explícita e restauração de arquivo, seleção e rolagem por jogo.
- [Guia e validação da F4](docs/guides/f4-editor-keyboard.md).

## 0.0.1-dev.1000588 — F3 · IA global e co-work

- Modelo por jogo, fallback para o padrão global, catálogo pesquisável e ID manual.
- Estudo é padrão e Fazer exige seleção explícita. É possível verificar ou desativar o contexto do projeto e escolher uma referência offline.
- Texto em streaming nos dois chats, rascunhos privados, histórico parcial e registro de ferramentas. Checkpoints com revisão confirmada permitem revisar e desfazer sem substituir edições posteriores.
- [Guia da F3](docs/guides/f3-ai-workspace.md).

## 0.0.1-dev.1000587 — F2: área de trabalho, primeiro uso e aparência

- Barra de destinos em retrato e navegação lateral em janelas largas. A Home preserva rolagem e volta à origem. Projetos fixados podem ser reordenados.
- Primeiro uso em três etapas. Os projetos internos continuam acessíveis. A pasta escolhida pelo Android guarda revisões verificadas com recuperação por cópia de trabalho.
- Tema claro, escuro ou do sistema e fonte do editor com prévia. As escolhas persistem com as preferências anteriores.
- [Guia da F2 e limites do provedor Android](docs/guides/f2-workspace.md).

## 0.0.1-dev.1000586 — F2/1: Home e continuidade por GameId (candidata)

- Continuar edição, Jogar e Criar separados na Home; fixados e recentes em listas distintas.
- Projeto identificado por `GameId` e não nome de pasta; migração atômica V1→V2 das preferências e MRU dos oito últimos projetos.
- Projeto ausente, manifesto danificado ou ID duplicado não vira atalho para outro jogo; não altera arquivos do projeto.
- Jogar verifica recuperações pendentes; Preview aberto da Home devolve à Home e erros de compilação retornam ao editor; Back respeita a origem.
- Testes xUnit e três novas capturas Android 13; SAF, primeira instalação, temas e F2 completa ainda pendentes.

## 0.0.1-dev.1000585 — Recuperação de Studios globais e co-work (candidata; CI pendente)

- Recuperação seletiva: Sprite, Music e Sound Designer globais sem jogo; rascunhos e publicação de versões na biblioteca existente.
- Assistente compacto no editor, passos reais, acesso ao histórico, contexto e modo visíveis, fechamento com foco restaurado, redução de renders e atualização do disco menos frequente.
- Segurança: WAV final consistente antes de publicar; recusa de links no workspace e testes de independência entre jogos.
- Pendente: CI, integração, APK publicado e demais frentes F2–F8; não confundir com release 1000584.

# Changelog

## 0.0.1-dev.1000584 — Trilha inicial Aprender completa

- **Aprender no editor:** missão, quatro passos, rubrica de teste, referências e progresso visíveis numa aba do painel já usado por Console/Problemas. Abertura automática ao iniciar pelo curso, vínculo por GameId, seleção entre múltiplas missões do mesmo jogo e restauração após Preview; layouts e abas horizontais suportados.

- **T01:** todos os cinco cursos e dez missões de Primeiros jogos e orientação no Lunet, com 40 objetivos, rubricas de Preview, pistas da documentação offline e desafio de transferência; outras 55 entradas permanecem Disponível em breve.
- **Navegação e progresso:** páginas por curso/projeto, associação independente por GameId, projetos comuns em branco e resume. Barra do total 40 objetivos e de cada missão.
- **Integridade:** progresso V2 por projeto, leitura/migração V1 não destrutiva, escrita atômica, validação contra IDs inexistentes. LRN-004-P2 inicia projeto landscape.
- **Metodologia:** nenhuma solução pré-codificada; checkbox significa declaração do aluno, não verificação automática. IA dá pistas por escolha. Sem DEVICE afirmado; CI será confirmado no PR.
- **Testes:** LearningTrackOneTests e atualização de LearningFeatureTests; guia e estudo técnico do T01.


## 0.0.1-dev.1000583 — Lunet Aprender e IA global

- **Aprender:** 60 cursos de 12 trilhas listados. Somente LRN-001-P1 (Aprendendo movimento) liberado; demais 119 projetos identificados como “Disponível em breve”.
- **Primeiro desafio:** quatro objetivos (quadrado, movimento, círculo e vitória), criação/associação de projeto comum, marcação declarativa, barra de progresso e persistência local.
- **IA da Home:** chat textual geral com histórico separado e sem acesso a ferramentas de arquivos ou projeto; configuração completa de provedor, modelo, chave e permissões acessível na área de trabalho.
- **IA dos jogos:** conversas por GameId preservadas, seleção apenas de modelo na interface do projeto; o chat contextual permanece no editor.
- **Documentação:** manifesto, matriz com 60 cursos e 120 projetos, pesquisa, plano técnico, guia de uso e testes.
- **Limites:** primeira versão não verifica automaticamente jogabilidade ou competência; nenhum curso inteiro foi concluído, pois LRN-001-P2 ainda está indisponível; CI não demonstra teste físico no Android.

- LUNET-511 (PR #962, CI/DEVICE pendentes): publicação de revisões rejeita diretórios de IDs da biblioteca que sejam links simbólicos antes de enumerar manifestos ou criar staging; regressão xUnit protege arquivos externos.

- LUNET-511: manifestos da biblioteca que declaram um arquivo como ancestral de outro (inclusive diferenças de caixa) são recusados antes de importar/exportar; regressão xUnit preserva Game.cs, ZIP ausente e manifesto original. CI e DEVICE ainda pendentes.

- LUNET-511 (incremento em PR #962, CI/DEVICE pendentes): a busca offline da biblioteca também encontra assets pelos nomes e caminhos dos arquivos-fonte e derivados (por exemplo `.sprite.json` e `.png`), preservando metadados e versões.
- LUNET-511 (PR #962, CI/DEVICE pendentes): prévia e importação agora rejeitam colisões de nomes que diferem apenas por maiúsculas/minúsculas, inclusive em diretórios ancestrais, protegendo a portabilidade dos assets entre Android e desktops; duas regressões xUnit adicionadas.
- LUNET-511 (PR #962, CI/DEVICE pendentes): importação de manifestos SHA-256 minúsculos também compara hashes sem diferenciar caixa durante staging e rollback; regressão existente reforçada para verificar os bytes realmente importados.
- LUNET-511 (PR #962, CI/DEVICE pendentes): importação e exportação ZIP aceitam SHA-256 hexadecimal em minúsculas nos manifestos, coerentemente com a verificação de integridade; regressão cobre importação, ZIP e preservação dos metadados.

- LUNET-511 (PR #962, CI/DEVICE pendentes): importação agora rejeita colisões com arquivos ancestrais durante o preflight, antes de criar staging; teste xUnit cobre preservação de arquivos do projeto e recusa sem importação parcial.


- LUNET-511 (PR #962, CI/DEVICE pendentes): prévia de importação identifica também colisões em diretórios ancestrais ocupados por arquivos, antes de iniciar a cópia; regressão xUnit adicionada.
- LUNET-511 (PR #962, CI/DEVICE pendentes): antes de importar um asset, o Android apresenta os caminhos em conflito sem alterar o projeto; a importação ainda revalida o destino. Regressão xUnit para prévia sem efeitos colaterais e recusa de sobrescrita.


- LUNET-511 (integridade de manifestos, CI/DEVICE pendentes): leitura da biblioteca rejeita tipo de asset desconhecido e data de criação ausente, sem alterar o manifesto ou o projeto importador; regressões xUnit cobrem ambos os campos.



- LUNET-511 (PR #962, CI/DEVICE pendentes): verificação de integridade sob demanda na Biblioteca Android, conferindo SHA-256 e tamanho dos arquivos da versão sem apagar dados danificados; regressão xUnit adicionada.

- LUNET-511 (SAF, DEVICE pendente): a exportação ZIP da biblioteca trata ausência ou bloqueio do seletor de documentos Android sem encerrar a Activity e sem deixar exportação pendente; conteúdo da Library permanece intacto. Validação Android 13 necessária.

- LUNET-511 (CI/DEVICE pendentes): formulário Android de publicação agora aceita tags separadas por vírgula para pesquisa offline; regressão cobre persistência das tags após reinício. Corrigido teste de busca Unicode decomposto que usava sequências de escape literais em vez de caracteres combinantes reais.

- LUNET-511 (busca global, CI/DEVICE pendentes): busca de assets agora aceita termos sem acentos, letras maiúsculas e Unicode decomposto, preservando intactos nomes e metadados originais. Regressão xUnit para título, autor, atribuição e tags.

- LUNET-511 (CI/DEVICE pendentes): Biblioteca pessoal ganhou pesquisa offline combinando termos de título, tipo, autoria, licença, atribuição e tags; interface Android permite buscar e limpar filtros sem modificar versões imutáveis. Regressão xUnit verifica múltiplas versões e persistência.

- LUNET-511 (integridade, CI/DEVICE pendentes): exportação ZIP agora calcula SHA-256 e tamanho dos bytes efetivamente gravados no arquivo compactado, fechando a janela entre a checagem prévia e a cópia; em caso de alteração concorrente a exportação falha e remove o ZIP temporário. Teste xUnit verifica os hashes e tamanhos das entradas exportadas contra o manifesto imutável.

- LUNET-511 (CI pendente): removida anotação xUnit `[Fact]` duplicada no teste de exportação ZIP de versões, que impedia compilar toda a suíte (CS0579). O teste de versões e atribuição permanece ativo; DEVICE ainda pendente.

- LUNET-511 (correção de CI): callback SAF agora usa `global::Android.Net.Uri` para evitar resolução incorreta de namespace; teste de exportação ZIP compara atribuição após desserializar JSON (Unicode escapado), preservando a verificação semântica. Ainda aguardando novo CI/DEVICE.

- LUNET-511 (incremento Android, CI/DEVICE pendentes): Biblioteca global oferece Exportar ZIP para cada versão imutável usando o seletor de documentos do Android (SAF); o ZIP é verificado em cache antes de abrir o destino, o estado da seleção sobrevive à recriação da Activity e o temporário é limpo. Regressão verifica que revisões antigas/novas mantêm bytes e metadados de licença distintos.

- LUNET-511 (incremento, CI/DEVICE pendentes): exportação ZIP autocontida de uma versão imutável da Library, com manifesto, fontes e derivados; validação SHA-256, rejeição de destino existente e limpeza de arquivo temporário em falhas. Regressões xUnit cobrem conteúdo, colisão e corrupção. Exposição pelo Android/SAF ainda pendente.

- LUNET-511 (integridade, CI/DEVICE pendentes): leitura de versões da Library rejeita metadados persistidos ausentes/inválidos (título, licença, autor, atribuição e tags), além de hashes SHA-256 com caracteres não hexadecimais. Testes xUnit parametrizados verificam falha controlada, preservação do manifesto e ausência de escrita no jogo de destino.

- LUNET-511 (integridade, CI/DEVICE pendentes): importação de assets recusa manifestos locais com ID, lista de arquivos, caminho ou hash nulos, emitindo erro de dados inválidos em vez de exceção inesperada. Regressões verificam preservação do projeto de destino e do manifesto original.

- LUNET-511 (biblioteca em desenvolvimento, CI/DEVICE pendentes): biblioteca local de assets versionados, com publicação de fontes e derivados, hashes SHA-256 e metadados de origem/licença; importação por cópia para outros jogos, preflight de colisões, recusa de dados corrompidos e rollback sem apagar mudanças concorrentes. Interface de publicação e uso entre projetos conectada à Home e à seção Studios, ainda sem teste físico.

- LUNET-512 (segurança de leitura, CI/DEVICE pendentes): campos inválidos em JSON persistido, incluindo notas longas e associação de projeto fora dos limites, agora são reportados como dados inválidos sem exceção não tratada no Android. O arquivo original é preservado. Testes de regressão cobrem ambos os campos.

- LUNET-512 (integridade de dados, CI/DEVICE pendentes): referências curadas retiradas do catálogo embutido continuam acessíveis como snapshots locais, preservando favoritos, notas e associação a projetos após atualizar o aplicativo e salvar novamente. Identificadores pessoais inválidos continuam rejeitados; referências curadas que retornarem recebem metadados oficiais com dados pessoais preservados.

- LUNET-512 (correção de estabilidade, CI anterior verde; DEVICE pendente): abertura de links externos do Explorar trata ausência de navegador e bloqueio de Activity no Android, informando o problema sem encerrar o aplicativo. PR #959; não altera dados, estágio ou versão.

- LUNET-512 (expansão em PR; CI/DEVICE pendentes): catálogo de Explorar ampliado de 12 para **131 jogos de referência curados**, com mecânicas e observações de estudo pesquisáveis (incluindo voo/montarias e controles mobile). Links de pesquisa de gameplay no YouTube sob demanda, sem rede automática ou vídeos embutidos; lista Android paginada em blocos de 18 para não inflar a Home. Novos testes protegem IDs, metadados, busca e migração de notas/favoritos.

- LUNET-510 (PR em andamento, DEVICE pendente): Home mobile-first sem abrir projeto, quatro destinos (Início, Projetos, Studios e Explorar), pesquisa de jogos, fixados, último projeto aberto, documentação offline e acesso a configurações; preferências locais persistidas fora dos jogos com gravação atômica. Studios ainda vinculados a projetos até LUNET-511.

- LUNET-512 (implementação em PR, CI/DEVICE pendentes): Explorar diretamente do launcher — catálogo offline de jogos e gêneros para inspiração, filtros combinados por mecânica/perspectiva/estilo, busca por texto, notas, favoritos, criação/edição/exclusão de referências pessoais e associação opcional a projetos. Sem capturar arte protegida, sem loja e sem internet obrigatória.

- LUNET-513 (em revisão, DEVICE pendente): o desfazer da IA agora compara cada arquivo com os bytes realmente escritos pela execução. Quando algum arquivo foi alterado depois (inclusive fonte e PNG de sprite), a reversão inteira é bloqueada antes de tocar no projeto; checkpoints persistem hashes para sobreviver ao reinício e checkpoints antigos sem estado final confiável não sobrescrevem trabalho mais recente.

- LUNET-508: jogos podem pedir uma foto da galeria durante a partida (`IGameImagePicker`), pelo seletor do Android, sem permissão de armazenamento; a foto chega em pé e reduzida a no máximo 1024 px. Funciona no Preview rápido e no APK do jogo. Guia `docs/guides/fotos-no-jogo.md`.

- LUNET-507: a IA edita só os trechos necessários e não consegue mais apagar um arquivo inteiro (regravações que removem a maior parte do código ou usam "..." são recusadas); toda resposta mostra o que mudou com *Desfazer tudo*, também depois de cancelar, de uma falha ou de reabrir o app; a IA recebe o código real do arquivo aberto e precisa lê-lo antes de falar dele; o editor acompanha as mudanças da IA e nunca grava por cima delas sem perguntar.

- LUNET-506: autotile no Tile Studio — Terreno → escolha 16 ou 47 tiles a partir do tile selecionado; pintar e apagar ajustam bordas e cantos sozinhos.

- LUNET-505: Data Studio — dados do jogo (inimigos, itens, fases, diálogos) como tabelas: colunas com tipo e regras, opções, referências entre tabelas, validação com destaque, ordenar, CSV e classe C# gerada para o jogo ler os dados tipados.

- LUNET-482 (DEC-0062, alternativa A): primeira versão do Lunet para desktop (Windows, Linux, macOS) em Avalonia — abre projetos, edita, compila e roda o jogo com prévia, mouse e teclado. Rasterizador em CPU com o mesmo resultado do Android (2000 sprites em 4 ms). Ainda é um spike: falta medir num computador com tela real. O Android não mudou.
- Correção: o guia do modelo de partículas (`docs/guides/particulas.md`) havia sido substituído pelo do Particle Studio; foi restaurado e o novo guia está em `docs/guides/particle-studio.md`.

- LUNET-504: Particle Studio — efeitos de fogo, fumaça, faíscas, explosão, chuva, neve, magia, poeira, bolhas e confete com prévia ao vivo; o Framework ganha direção e abertura, formas de nascimento, resistência do ar, variações, giro, gradiente de cor e brilho aditivo, e o jogo carrega o efeito salvo com `Content.LoadParticleEmitter`. Efeitos antigos continuam idênticos.

- LUNET-503: multijogador no mesmo aparelho — tela dividida para até 4 jogadores com toques por região e uma câmera por jogador (`SpriteBatch.Begin(camera, região)`), e `TurnOrder` para jogos por vez (hot-seat) e partidas assíncronas com proteção contra jogadas sobre um estado antigo.

- LUNET-502: servidor dedicado headless — `lunet-server` compila a pasta de um projeto e roda o jogo sem tela no Windows, Linux ou macOS; `Game.IsHeadless` diz ao jogo que ele é o servidor; `HeadlessGraphicsBackend` e `HeadlessGameRunner` também servem para testes automatizados.

- LUNET-501: multiplayer realtime no Lunet.Networking — transporte UDP (LiteNetLib interno), transporte em memória para jogar no mesmo aparelho e simulador de rede ruim; sessões com host, servidor dedicado e clientes (versão e jogo conferidos, lista de jogadores, mensagens tipadas, proteção contra flood), relógio sincronizado, objetos e variáveis de rede com interpolação, predição com reconciliação e descoberta de partidas na rede local. O cenário do gate da Fase 7 (dois clientes em tempo real com latência, jitter e perda simulados) passa nos testes; falta validar com dois aparelhos.

- LUNET-500: o Audio Studio compõe músicas (faixas, padrões, arranjo, instrumentos sintetizados com filtro e envelope, bateria, eco e reverb, escalas, swing) com geradores de melodia, acordes, arpejo, baixo e batida, e desenha efeitos sonoros complexos (moeda, laser, explosão, power-up, pulo, magia…) com variações. Exporta WAV para `Content/Audio/` mantendo a fonte editável (`.song.json`/`.sfx.json`).

- LUNET-499: Palette Studio — a paleta oficial do jogo (`Assets/palette.json`): começar de uma paleta inicial, das artes do projeto ou vazia; rampas de tons (sombras mais frias, luzes mais quentes), harmonias, contraste para texto/HUD, importar/exportar `.gpl` e hex, e exportar `Data/GamePalette.cs` para o código usar as mesmas cores. O Sprite Studio mostra as cores do projeto primeiro e ajusta um sprite à paleta; a IA usa a paleta oficial.

- LUNET-498: a IA entende se você quer aprender ou que ela faça (modos Auto/Ensinar/Fazer, botão *Fazer por mim*), começa cada pedido conhecendo o projeto (mapa de arquivos, classes e membros + referência rápida da API), organiza código em arquivos e pastas por responsabilidade e avisa quando um arquivo cresce demais, pesquisa código de referência na internet com proteções, e as configurações ganharam chave por provedor, lista de modelos do provedor e *Testar conexão*. Validação no aparelho pendente.

- LUNET-497 / PR #932: histórico local integral e persistido desde o pedido; IA continua em foreground service Android fora do chat; painel exibe a tarefa e o progresso ao retornar. Caminhos de sprite corrigidos e compatíveis com `Content/` legado; verificação da existência e assinatura PNG na compilação da IA. 886 testes e CI Android verdes; teste físico do proprietário pendente.

- Fixed / Novo projeto (14/11, revisão solicitada): `Game.cs` volta a incluir `Initialize`, `LoadContent`, `Update`, `Draw`, `UnloadContent`, `SpriteBatch.Begin/End` e fundo básico, mas sem a bola, textura ou movimento por toque. O launcher não exibe modelos demo/protótipos: formulário único para nome, orientação, resolução, updates/s, Fit/Fill, versão e ID Android opcional. Configurações persistidas no `lunet.json` e refletidas no código inicial; demos continuam disponíveis apenas para testes internos.

- LUNET-491 (feito, sem validação em aparelho): Sprite Studio ganha seleção por retângulo, laço e varinha, com mover, redimensionar, girar, espelhar, duplicar e apagar sobre camada flutuante (grava só ao aplicar; cancelar e desfazer). Áudio: prévia com cursor, toque na onda, repetir e ajuste ao vivo, inclusive ao criar efeitos. Assets: prévia real de sprite animado, imagem, som, fonte e dados, também a partir do Explorer. Ícones: catálogo vetorial de contorno substitui caracteres Unicode em toda a interface. Roteiro: `docs/guides/validacao-studios-icones.md`.
- LUNET-492 (Fase A, estudo): viabilidade de desktop/navegador/iOS conferida contra o código; `PlatformPortabilityTests` fiscaliza que as bibliotecas portáteis não usam APIs de plataforma. ADR 0012 Proposto e DEC-0062 pendente; nenhum host novo.

- LUNET-470 / Workbench mobile-first (em revisão, CI/DEVICE pendentes): nova entrada de projetos, workspace de código em foco com navegação Código/Arquivos/Erros/Console/Ações, command palette em português com busca sem distinção de caixa/acentos, drawer modal com scrim, alvos de toque de 48dp, toolbar sem captura de foco e Back contextual. Testes de contrato registrados antes da implementação, pesquisa rastreável em `docs/research/mobile-workbench-ux-2026-11-01.md`. Não afirma redesign dos Studios, compilação verificada nem release publicada.

- Fixed / host standalone: entrega Game.OnPause na thread GL antes de suspendê-la, usando o mesmo PauseForBackground do PreviewHost. Regressão provada por smoke Android que falhou antes e passou após a correção; JSON privado e restore após force-stop conferidos.

- F4 / Correção de CI integrado do PR #856: criação e destruição de mundos Box2D passam por mutex global de ciclo de vida, como exige a documentação oficial do Box2D. Mantém simulação de mundos distintos paralela; teste de regressão com 256 ciclos concorrentes. O teste PhysicsWorldTests.DynamicBallFallsOntoGround havia falhado de forma intermitente no estado combinado; CI novo e DEVICE pendentes.

- F7 / LUNET-460 (em revisão, CI/DEVICE pendentes): primeiro corte em assembly independente `Lunet.Networking` para LAN confiável em C#: host e clientes, IDs atribuídos pelo host, mensagens limitadas, rejeição de estado enviado por cliente e fila assíncrona consumível pelo jogo. Jogo standalone recebe permissão INTERNET. Não é multiplayer realtime completo nem seguro para internet pública. Consulte ADR 0011 e docs/guides/networking-foundation.md.

- LUNET-451 / Issue #842 (em revisão): três projetos Lunet de referência versionados (portrait, landscape e full-stack com Sprite Studio fonte, tilemap/collision, áudio WAV, física, UI e save). Testes xUnit anteriores aos fixtures compilam/executam os jogos e validam catálogo, formatos e ZIP. Sem alteração no motor/assinatura; CI, APK e DEVICE pendentes.

- LUNET-450 / Issue #834 (em verificação): teste E2E de projeto criado no Lunet, código e assets editados, Preview headless, ZIP e empacotamento offline do mesmo jogo; workflow preparado para instalar, abrir e capturar logs/tela do jogo no Android 13. Não altera o empacotador de produção, as chaves do usuário nem substitui testes físicos da IDE. Conferir CI e roteiro DEVICE antes de declarar conclusão.

- Documentação / LUNET-449: inventário auditável de 27/10/2026 confronta código na main, testes, PRs, handoffs, aprovações DEVICE e releases; reconcilia o estado do painel Assets (#795/#800), Audio Studio (#801/#806) e geração local de APK (#804/#814/#817/#824) sem alterar executáveis ou concluir os gates F4/F5/F9.


- F9: corrigido conflito real `INSTALL_FAILED_CONFLICTING_PROVIDER` entre dois APKs gerados: o `mono.MonoRuntimeProvider` agora recebe `android:authorities` único por projeto, além do `PackageId`, preservando a inicialização .NET. CI valida os dois providers e instalação simultânea no Android 13.


- Pré-beta / F9: jogos criados ou importados com identidades Android independentes e estáveis, resolução explícita para colisões legadas, orientação Vertical/Horizontal persistente no projeto e aplicada ao `android:screenOrientation` binário antes de assinar o APK. Smoke CI instala dois jogos juntos e verifica manifestos; DEVICE físico ainda precisa validar.


- Standalone Smoke (artefato de CI): cada execução agora utiliza um package ID distinto, evitando conflitos de atualização entre APKs assinados por chaves debug efêmeras; o CI passa a verificar assinatura e alinhamento do APK de diagnóstico.

- Pré-beta / correção de instalação do APK do jogo: alinha em 4 bytes os dados dos arquivos ZIP armazenados, especialmente `resources.arsc`, para Android 11+; testes de regressão de offsets, `zipalign` no CI e instalação do pacote exportado em emulador Android 13. DEVICE físico ainda requer validação.

- Pré-beta / LUNET-448 (correção de regressão): conversão JNI verificada de chave RSA do Android Keystore na geração de APK offline do jogo (evita `InvalidCastException` do downcast gerenciado); verificação da assinatura antes do resultado e indicação/log da etapa de falha. Contratos de regressão portáteis e roteiro DEVICE preparados antes da correção; testes reais no aparelho ainda pendentes.

- Pré-beta / LUNET-447: primeiro Audio Studio nativo com síntese de seno/quadrada/triangular/ruído dentro do Lunet, importação de WAV PCM, waveform, recorte, ganho, marcadores SMPL de loop, reprodução e exportação de cópia WAV16 em Content/Audio sem sobrescrever o original. Integra Arte, menu Ferramentas e Assets. Testes portáteis; CI/APK/DEVICE pendentes. Não é DAW nem editor MP3/OGG.

- Pré-beta / LUNET-446: painel Assets no workspace com categorias, busca, preview de imagens/map tileset, escuta de áudio, acesso direto aos editores Sprite/Tile/JSON e exemplo de carregamento C#. Catálogo deriva de ListFiles sem índice paralelo; CI/DEVICE pendentes.

- Pré-beta / LUNET-445 (#787): runtime-template Android separado da IDE, compilador GameCode com `GameSourceRoot` e prova por workflow GitHub Actions que empacota Game.dll, Content e Assets num APK de jogo distinto; CI de prova e fluxo cloud integrado na IDE (Arquivo → Gerar APK), consentimento explícito, repositório privado próprio, upload ZIP, workflow, monitoramento, download do artifact e exportação/instalação no Android. Assinatura debug temporária; offline e DEVICE ainda pendentes.

- Pré-beta / LUNET-444 (#785): Tile Studio cria mapas e tilesets completos dentro do Lunet sem importação; gera PNG + JSON editável e integra editor de pixels acessível pelo próprio mapa. Suporte inicial de atlas 64/128px, tiles 16/32/64 e tamanhos de mapa 16/32/64. CI e DEVICE pendentes.

- Pré-beta / LUNET-443 (#783): Sprite Studio passa a gerar e atualizar a spritesheet PNG com todos os frames automaticamente ao salvar o `.sprite.json` (mantendo FPS/frames editáveis); botão de uso exibe SpriteSheet/SpriteAnimationClip/SpriteAnimator. Corrigido carregamento de `Assets/` no Preview rápido/isolado sem afetar `Content/`. DEVICE pendente.

- Pré-beta / LUNET-442 (#779): Sprite Studio ampliado com linha, retângulo contornado/preenchido, flip vertical, zoom e deslocamento, grade opcional e cor ARGB personalizada. Preserva o JSON v1 e frames existentes; CI/DEVICE pendentes.

- Pré-beta / LUNET-441 (#777): Tile Studio Android inicial com paleta visual de PNG, pintura/borracha por toque, camadas, visibilidade e colisão, undo/redo e mapas JSON v1 editáveis no projeto. Sem duplicar o TileMap, alterar o áudio ou prometer APK do jogo; DEVICE pendente.

- Pré-beta: primeiro Sprite/Animation Studio nativo (LUNET-440), desenho por toque, frames, playback, JSON editável e spritesheet PNG. Prioridade de execução revista por ordem do proprietário: editores/UI/build antes de refinamentos adicionais de áudio.

## 0.0.1 — em desenvolvimento

### Added

- LUNET-438: música WAV PCM mono/estéreo 8/16 bits em streaming no AudioTrack Android, mantendo loop, volume, fades e pausa/retomada do mixer existente. Profiler/relatório mostram o contador nativo de underruns apenas das faixas medidas; efeitos SoundPool e formatos comprimidos permanecem explicitamente não medidos. Sem novo formato de projeto ou API do jogo.

### Fixed

- LUNET-438: pausa do mixer enfileirada na thread GL antes de suspender o Preview, sem depender de um próximo quadro para interromper o áudio em segundo plano.

- LUNET-432 (em revisão): toques capturados não provocam clique fantasma em botões nem inércia antiga após mudar o layout; Bounds iguais não interrompem gestos, e sliders continuam seguindo o dedo como na API anterior.

- LUNET-426 (em revisão): corrigir CRLF, Unicode fora do BMP e validação de layout/escala na fonte bitmap embutida; medida e desenho coerentes, sem enviar coordenadas infinitas ao OpenGL.

- LUNET-424 (em revisão): Preview Android agora preserva o início e o fim de toques rápidos entre eventos de 90/120 Hz e os Updates fixos, mesmo quando o dedo move antes da atualização. Buffer interno sem List/ToArray por MotionEvent; evita falhas intermitentes de captura Pressed nos joysticks opt-in.

- SAT calcula separação correta quando um polígono contém outro; polígonos sem área não produzem empurrão inválido.
- Medições de alocação isoladas de outras coleções de testes, preservando a exigência de zero bytes no loop; falha concorrente reproduzida também na base anterior.
- Correções rápidas removem apenas a diretiva using, preservando código na mesma linha e comentários; ordenação preserva global using, CRLF e comentários, e recusa blocos ambíguos/condicionais.


### Added

- LUNET-435 / #629: cenários Juntas e Filtros/eventos dentro do módulo Colisões do Laboratório; pêndulo, trilho, impulso, colisão filtrada, raycast e contadores de entrada/saída de sensores e contatos, com reinício e troca segura de mundo. SAT e carga 1/64 preservados; validação Android específica ainda pendente.

- Fase 4 / Tilemaps completos (em revisão): ponte imutável `TileMap.Create/WithTile/WithLayerSettings/RenameLayer/AddLayer/RemoveLayer/MoveLayer` para autoria de mapas JSON v1. Preserva dados antigos, valida camadas/células/IDs e atualiza maior tile após remoção; o Tile Studio visual continua Fase 5.

- LUNET-435 (em revisão): cenário opcional de 64 corpos Box2D no módulo Colisões do Laboratório, comparável com o caso de 1 corpo pelo Profiler, reset seguro e testes de integração; desempenho no Android ainda precisa de medição real.

- LUNET-436 / Issue #495: jogo reproduzível de validação conjunta do gate, descarte seguro do atlas de `SpriteFont.CreateDefault` e exportação `TileMap.ToJson()` compatível com JSON v1, preservando camadas, visibilidade, colisão e Unicode; auditoria integral da Fase 4 e roteiro consolidado. Aprovações anteriores reconciliadas sem concluir o gate; underruns de áudio e demais bloqueios reais continuam explícitos.

- Fase 4 / LUNET-414: aba Profiler no workspace preserva a última captura medida no Preview rápido/isolado após Stop e permite compartilhar o relatório; coleta continua opt-in em ▥ Perf. Não altera projetos ou executa jogo em segundo plano.

- LUNET-414 (em revisão): métrica opt-in visível no Profiler do Preview para tamanho estimado RGBA8 de texturas vivas, pico e quantidade de objetos GL (inclui render targets sem double count), com testes. VRAM total, buffers, GPU time e áudio underruns continuam não medidos.

- LUNET-435 (Fase 4, Laboratório em revisão): tela Colisões alterna SAT original e simulação real Box2D (bola, chão, sensor e reset) no mesmo módulo 10/12, sem alterar projetos existentes.

- LUNET-435 (Fase 4, expansão em revisão): cápsulas/segmentos, juntas revoluta/prismática, rotação e força de corpos, filtro por categoria, remoção de fixtures, contatos de fim e eventos de sensores (início/fim), com testes e documentação gerada; não altera projetos existentes.

- LUNET-435 (em revisão): primeiro incremento de física completa com backend Box2D.NET em C# gerenciado, corpos, fixtures de círculo/caixa, gravidade, juntas de distância, contatos iniciados e raycast. Sem alterar jogos salvos; validação Android e itens avançados pendentes.

- LUNET-433 (em revisão): grade de interface 2D responsiva para menus e inventários, com cálculo de colunas por largura, altura total e consulta virtualizada O(1), hit-test e TouchGridView com seleção, rolagem e renderização de janela, compatível com TouchScrollArea; exemplo C# offline e sem alocação no layout.

- LUNET-430 (em revisão): font atlas TrueType gerado offline em C# (`TrueTypeFont.Bake` e `Content.LoadTrueTypeFont`), integração com SpriteBatch, métricas proporcionais, cache de fonte, testes e limites de memória.

- LUNET-431 (em revisão): layout responsivo de UI horizontal/vertical com flex, espaçamento, padding e alinhamento transversal; converte safe area em bounds para controles mobile, sem GC por quadro.

- LUNET-429 (em revisão): `TouchListView` para listas grandes de missão/inventário com scroll inercial, hit-test por linha, seleção sem clique acidental ao arrastar e desenho apenas das linhas visíveis.

- LUNET-428 (em revisão): TouchToggle para menus mobile de opções, com paletas on/off, captura por dedo, testes e guia offline executável.

- LUNET-427 (em revisão): TouchScrollArea para painéis verticais de inventário, missão e menus mobile, com inércia, limites, multitoque, indicador visual proporcional de scroll (GetThumbBounds) e clip por SpriteBatch; testes e guia offline.

- LUNET-425 (em revisão): mediana/P95, pior frame e engasgos por cadência observada no Profiler de Preview Android, com janela circular sem GC e cobertura para 60/90/120 Hz. Não confundir frame pacing com tempo GPU.

- LUNET-423 (em revisão): precisão dos controles virtuais, resposta exponencial/sensibilidade, zona morta contínua opcional, identificação/reserva do dedo multitouch e cancelamento; padrões antigos preservados.

- LUNET-422 (em revisão): escala Fit/Fill em GraphicsDevice e GameConfiguration para preencher a tela preservando proporção (Fill recorta excedentes); alternância testável no módulo Gráficos do Laboratório 2.0 e compatibilidade dos toques.

- LUNET-421 (em revisão): Camera2D.Follow estável entre taxas de atualização, ClampToWorld com zoom/rotação e opção LIMITES na página Câmera do Laboratório, com testes de qualidade e alocação.

- LUNET-420 (em revisão): instrumentação opt-in de tempos CPU Update/Draw e contagem de passos do GameHost, exibidas e agregadas no Profiler Android; não mede GPU e não altera projetos existentes.

- LUNET-419 (em revisão): botão Perf no Preview Android, com janela de métricas reais de FPS, CPU Tick, draw calls, triângulos, alocações gerenciadas, heap e GC. Dados coletados só quando ligado; não inventa tempo GPU, métricas de áudio ou custo separado de Update/Draw.

- LUNET-418 (em revisão): Laboratório 2.0 com índice, 12 módulos offline prontos para Run, navegação anterior/próxima e testes reais de animação, partículas, tilemap/A*, SAT, bitmap fonts, nine-slice e UiTheme oficial. Template separado do ProjectTemplates, sem alterar projetos existentes.

- LUNET-416 (em revisão): `Camera2D.GetWorldViewBounds` calcula a AABB visível sob zoom/rotação para recortar TileMap.Draw sem corte de cantos, com testes de precisão e alocação.

- LUNET-415 (integrado PR #358; DEVICE pendente): `TileMap` JSON v1 em camadas, tileset, colisão, viewport culling e integração com A* e colisão AABB de personagens, com guia offline e testes no host portátil. Tile Studio/DEVICE pendentes.

- Helpers `DebugDraw.Polygon`, `Ray`, `Axes` e `Grid`: geometria de depuração transformada, limites/validação antes de desenhar e zero alocação no loop; página 7 pronta no Laboratório (LUNET-413).

- `Lunet.Scenes.Scene2D`, `Entity2D` e `Component2D`: organização opcional de movimento/desenho, ownership exclusivo, percursos em ordem de inserção sem alocação e flags independentes; sexta página pronta para Run no Laboratório (LUNET-412).

- `Lunet.UI.TouchButton`/`TouchButtonStyle`: clique ao soltar com captura por ID, estados/cancelamento/disable e desenho de fundo/texto com cores configuráveis, sem alocação no loop; quinta página executável do Laboratório com clique, arraste, disable, pausa e reposicionamento por LayoutRect (LUNET-410).

- `Lunet.UI.LayoutRect`: âncoras, pivôs, margens Fixed/Stretch e composição de retângulos aninhados, sem alocação por cálculo; guia offline de UI com redimensionamento e toque alinhado (LUNET-409).

- `UiTheme`: paletas locais imutáveis Dark/Light/HighContrast aplicáveis a botões, sliders, fundo, painel e texto, sem estado global; teste de desenho e ausência de alocação por quadro (LUNET-417, em revisão).

- `TouchSlider` e `TouchSliderStyle`: arraste contínuo ou em passos, captura por dedo, cancelamento e estado desabilitado; demonstração executável na página 4 do Laboratório, sem copiar código (LUNET-411).

- `SpriteFont.FromBitmap` e `BitmapGlyph`: fontes bitmap personalizadas sobre textura/atlas, métricas proporcionais, deslocamento, Unicode/fallback e layout sem alocação no loop; guia offline de placar (LUNET-408).

- APIs `SpriteAnimationClip`/`SpriteAnimator`, `Tween` e `ParticleEmitter`/`ParticleSettings`: animação, transições e partículas code-first, sem alocação no loop. Guias offline completos para projetos Em branco.
- `NineSlice` e `SpriteBatch.Draw(panel, ...)`: painéis/botões redimensionáveis com cantos preservados, recorte de atlas, escala das bordas e compressão proporcional, sem alocação por desenho; guia offline interativo (LUNET-407).


- `Lunet.Pathfinding`: A* em grade com custos/obstáculos, quatro/oito vizinhos sem cortar cantos, buffers reutilizáveis sem alocação por busca e guia interativo offline (LUNET-406).

- `Camera2D`: posição, zoom, rotação, conversão mundo/vista e toque no mundo; lote com câmera opcional em `SpriteBatch`, HUD sem câmera, terceira página do modelo Laboratório e guia offline interativo (LUNET-403).
- Consultas `Geometry.Distance`, `ClosestPoint` e `Intersection` para círculos, pontos e AABBs, sem alocação; guia offline com demonstração de colisão no Preview (LUNET-402).
- Candidato exato e entrega do roteiro integral da Fase 3 no portal (LUNET-303); aprovações ZIP/C6 reconciliadas sem aprovar o gate inteiro.

- Identidade Framework / IDE / Studio code-first, um Product e um projeto, política Visual ↔ Source e backlog classificado (ADR 0007).

- Importar ZIP na tela de projetos: restaura projetos exportados pelo Lunet, aceita lunet.json na raiz, cria cópia numerada quando o nome já existe e rejeita ZIP inválido sem deixar projeto parcial.

- Ferramentas → "Medir o editor com arquivos grandes" (tempos no aparelho para decidir sobre virtualização) e auditoria técnica da Fase 3 com roteiro de teste.
- Preview isolado (opção em Configurações): o jogo roda em processo separado, e travas ou falta de memória não derrubam o IDE (ADR 0006).
- API do framework com exemplo em cada tipo, parâmetros e retornos descritos; testes garantem a cobertura e que os exemplos compilam.
- Recuperação de alterações não salvas com até cinco versões por arquivo; a tela de recuperação permite escolher a versão, e o menu Ferramentas oferece acesso manual enquanto houver um buffer pendente.
- `AgentsChat.md`: registro assíncrono de entregas, revisões, bloqueios e passagem de trabalho entre agentes.

- Git no aparelho (`Lunet.Git`, C# puro, ADR 0005): status, diff, commit, histórico, ramos, reverter, mesclar, buscar, pull, push (HTTPS com token) e clonar; painel em ⋯ → Ferramentas → Git.
- Inspector do jogo em execução (botão 🔍 no Preview): campos por reflexão, atributos `Range`, `ReadOnly`, `Hidden`, `Multiline`, `Color`, `File`, `Asset`, `Group`, `Tooltip`, `Inspect` e `Inspector<T>` customizado; edição ao vivo.
- Classificação de mudanças entre Runs (só corpos × reinício necessário) no status.
- Layout do workspace: painel embaixo ou à direita (automático por orientação), divisória arrastável, largura do Explorer, layouts salvos.
- IDE: formatar documento, renomear símbolo (recusa conflitos), correções rápidas (using faltando/sobrando, ";", "você quis dizer", ordenar usings), estrutura do arquivo, informações do símbolo, dobrar código, multi-cursor, minimapa, atalhos de teclado físico, comandos de linha (duplicar, apagar, mover, comentar, indentar), configurações e exportar logs.
- Compilação incremental: árvores de sintaxe reaproveitadas e resultado em cache quando nada mudou.
- `PieceTable` (estrutura de texto para documentos grandes) e menu ⋯ reorganizado em categorias.
- Busca no projeto inteiro (maiúsculas, palavra inteira, regex) e item "Documentação do símbolo" no menu; campo de busca da documentação legível.
- Documentação offline no app: painel com guias, busca e navegação por namespaces, tipos e membros (gerado de XML docs por `Lunet.Docs`); "Explicar na Documentação" na dica do símbolo; API do framework documentada.
- Lista de projetos: toque longo abre exportar ZIP e excluir projeto (com confirmação).
- `Sprite` (região, origem, cor, escala, rotação) e o contexto OpenGL preservado ao ir para segundo plano.
- Preview pede o modo de tela de maior taxa de atualização (90/120 Hz onde houver).
- Conteúdo: `Content.LoadJson<T>`, `Content.LoadAtlas` (`TextureAtlas` com regiões e pivô), `Content.UnloadTexture` e `Localization` (`Game.Localization`, idiomas em `Data/strings.<idioma>.json`, reserva e formatação).
- Entrada: `TouchCollection` (sem alocação) e `Pointer` unificado com bordas de pressionar/soltar.
- Gráficos: `BlendState`, `SamplerState`, `Viewport`, recorte por lote (`Begin(clip:)`), `RenderTarget2D`, `Shader`/`Material`, `DebugDraw`, `PixelPerfect`, `Density` e `SafeArea`; backend OpenGL ES com FBOs, blend, scissor e programas de shader.
- Modelo "Laboratório" com segunda página de gráficos (mistura, shader, amostragem, recorte, alvo de desenho, pixel perfect e área segura).
- Áudio: `AudioMixer` (`Audio`) com barramentos Master/Sfx/Music e próprios, fade de efeitos e de música, `Music` em streaming (`Content.LoadMusic`) e pausa automática em segundo plano; backend Android com MediaPlayer.
- Entrada: `Gamepad` (botões, sticks, gatilhos), giroscópio e `Haptics.Vibrate`; app em segundo plano pausa o jogo.
- Modelo de projeto "Laboratório", que testa música, volume, fade, vibração, acelerômetro, giroscópio, controle, pinça/giro, joystick e botão de tela.
- Framework: `GameServices`, `Dispatcher` (thread do jogo), `Timers`, `ObjectPool<T>`, `Transform2D`, `Ray2D`, `Geometry` (segmentos, polígonos, SAT), gestos DoubleTap/Pinch/Rotate e `VirtualStick`/`VirtualButton`.
- Explorer: painel lateral com a árvore do projeto (pastas recolhíveis, novo arquivo/pasta, renomear, excluir com confirmação); operações protegidas contra sair do projeto e contra apagar `lunet.json`/entrada.
- Recuperação após crash: buffers de trabalho em `.lunet/autosave` a cada pausa na digitação, apagados ao salvar; o app oferece recuperar ao reabrir.
- `LICENSE.md`: licença proprietária provisória (sem redistribuição, modificação ou exploração comercial sem autorização; código exibido no app é só educacional; jogos dos usuários pertencem a eles).
- Planejamento no repositório: especificação integral (`docs/SPEC.md`), ROADMAP cobrindo as 16 fases, critérios, riscos e aceitação, rotina de desenvolvimento com auditoria de fechamento de fase (`docs/DEVELOPMENT.md`, `docs/audits/`), `CLAUDE.md` e `tools/roadmap-status.sh`; testes que garantem que o ROADMAP cobre todas as fases do spec.
- `Lunet.slnx` e `release-notes.json` nas releases.
- Editor de código: realce de sintaxe C#, números de linha, desfazer/refazer (com junção de digitação), indentação automática, autocompletar em chips, localizar/substituir (com regex), ir para definição, dica do símbolo, referências e diagnósticos ao vivo (Roslyn). Biblioteca `Lunet.Editor` testada em desktop.
- Fonte bitmap embutida (`SpriteFont`, `SpriteBatch.DrawString`) com acentos do português; `SpriteSheet`; filtro de textura (`TextureFilter.Point`).
- Salvamento (`Save.Save/Load`) em JSON por chave, gravação atômica; teclado (`Input.IsKeyDown/IsKeyPressed`) e acelerômetro; `MathEx`, `RandomSource` (determinístico) e `Circle`.
- Modelo de projeto "Demo: Coletor de moedas", que exercita texto, gestos, som, salvamento e aleatório; escolha de modelo ao criar projeto.
- Gestos: Tap, LongPress, Drag e Swipe (`Input.Gestures`), entregues a um único passo de `Update`.
- Áudio: `SoundEffect`/`SoundInstance` (volume, pan, pitch, loop), `Content.LoadSound` e backend Android com SoundPool.
- Pipeline de conteúdo: decodificador PNG próprio, `ContentManager` com cache, `IContentSource` e importação de PNG para `Content/Textures` pelo app.
- Framework: `Game`, passo fixo, `SpriteBatch`, `Texture2D`, resolução virtual com letterbox e toque.
- Compilador Roslyn com diagnósticos (arquivo/linha/coluna) e referências em memória.
- Runtime: carregamento do jogo em contexto descartável; exceções do jogo não derrubam o app.
- Projetos: `lunet.json`, template inicial, salvamento atômico, proteção contra caminhos fora do projeto, ZIP.
- App: botão Run, Preview OpenGL ES com Stop/Restart/Pause/Step, painéis Problemas e Console, referência rápida.
- CI: testes, APK e releases de desenvolvimento a partir da `main`; testes de regras de dependência.

- Estrutura Android inicial, armazenamento local de projetos C# e exportação ZIP.
- Workflow de build e publicação do APK como artifact de CI.

### Changed

- A rotina de fechamento de cada fase agora exige roteiro detalhado de teste no aparelho e aprovação explícita do usuário, registrados na auditoria antes de avançar.
- Instruções compartilhadas migradas de `CLAUDE.md` para `AGENTS.md`, com referências e teste de existência atualizados.
- ROADMAP: itens sem validação em aparelho voltam a pendentes; resumo da Fase 3 e README alinhados ao estado registrado.

### Fixed

- Inspector vazio no modelo Coletor de moedas: `score`, `best` e `lives` agora aparecem (`[Inspect]`).
- Build Android do painel Git: `System.IO.Path` explícito e remoção da atribuição indevida ao parâmetro de progresso `_` na inicialização do repositório.

- Barra do editor: com 6 botões o ⋯ (menu, onde fica "Exportar projeto (ZIP)") saía da tela em aparelhos estreitos; os botões agora dividem a largura.
- **Perda de código ao sair do Preview:** ao parar o jogo o editor era recriado vazio e o salvamento seguinte gravava esse texto vazio por cima do arquivo. Agora só se salva o que o editor realmente carregou (`EditorSession`), e o texto é salvo antes de a tela ser recriada. Também evita regravar um arquivo renomeado ou apagado.
- Ao criar projeto, escolher outro modelo agora desmarca o anterior (os botões de opção não tinham id).
- Laboratório: botão para ligar/desligar o pixel perfect e botão de resolução virtual alternativa; em aparelhos com escala já inteira o pixel perfect não muda nada, e agora dá para forçar uma escala fracionada.
- As releases de desenvolvimento agora usam sempre a mesma chave de assinatura; antes cada build tinha uma chave nova e o Android recusava atualizar uma sobre a outra. **Ao instalar esta versão, desinstale a anterior uma única vez (exporte os projetos em ZIP antes).**

### Performance

- O caminho quente do framework (Tick, SpriteBatch, texto, gestos, timers) não aloca por quadro, verificado por teste; `Input.Gestures` passou a ser `ReadOnlySpan<Gesture>` e `DrawState` virou struct.

### Deprecated

- Nada.

### Removed

- Nada.

### Security

- Projetos ficam no armazenamento privado do aplicativo nesta etapa.
