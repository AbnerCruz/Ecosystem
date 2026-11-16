# Home e navegação no Lunet — LUNET-510

## Comportamento implementado

A tela Início é uma área de trabalho sem jogo aberto. A preferência de última seção pode reabrir Projetos, Studios ou Explorar, mas nunca um jogo automaticamente. A barra de navegação oferece Início, Projetos, Studios e Explorar; botões têm pelo menos 48 dp de altura. Projetos recentes e fixados são acessíveis na Home, assim como Documentação (API offline) e Configurações.

A tela Projetos preserva novo jogo com formulário completo, importação ZIP, clonagem Git, exportação e exclusão confirmada. Acrescenta pesquisa por nome e fixação/desafixação por estrela ou menu. A tela Studios oferece escolha explícita de um projeto existente e abre o painel de Studios real; não há Library global fictícia nem criação de projeto oculto. Explorar é o módulo offline LUNET-512 existente.

Os dados da Home são armazenados em launcher-preferences.json no armazenamento privado do aplicativo, fora de Projects/. Guardam seção, último projeto aberto e nomes fixados. O ProjectStore continua a autoridade exclusiva dos diretórios dos jogos; referências a um projeto excluído deixam de aparecer. A persistência é atômica. Um JSON inválido permanece preservado e não impede o acesso aos jogos.

Fontes externas da pesquisa já consolidada: documentação oficial Android sobre acessibilidade de Views https://developer.android.com/guide/topics/ui/accessibility/views/apps-views e arquitetura offline-first https://developer.android.com/topic/architecture/data-layer/offline-first. O modelo mantém a interface Android nativa em C# e evita exigir rede.

## Testes automatizados e critérios físicos

LauncherPreferencesTests verifica abertura sem alterar jogos, persistência entre reinícios, fixados, último jogo, restauração de seção, limpeza de atalhos após exclusão, nomes/seções inválidos, cópia defensiva dos fixados e arquivo corrompido preservado.

Validação DEVICE (moto g32 Android 13):

1. Abra o app sem jogo; confira Home, orientação, rolagem e que nenhum projeto abre automaticamente.
2. Crie jogo com Game.cs inicial, edite, volte à Home, reabra; confira salvamento, tela e último projeto.
3. Fixe dois jogos e pesquise; feche/reabra; confira estado dos fixados, última seção e exclusão de um projeto sem afetar o outro.
4. Entre em Studios, selecione jogo e abra Sprite/Tile/Audio; volte ao editor e à Home; confira que não foi criado projeto temporário.
5. Entre em Explorar, salve nota, volte à Home e retorne; confira persistência.
6. Abra Documentação offline e Configurações pela Home. Confira Back, teclado/IME, rolagem, foco, acessibilidade e continuidade do trabalho da IA fora do chat.
7. Simule preferências corrompidas e falha ao salvar; confirme aviso sem exclusão do arquivo antigo e sem alteração dos arquivos de jogo.

Limites: Home ainda necessita personalização global avançada e validação física; Studios independentes e Biblioteca global pertencem ao LUNET-511. Não marcar LUNET-510 completo com base apenas no CI.
