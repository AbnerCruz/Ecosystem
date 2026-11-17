# Biblioteca global de assets — LUNET-511

## Implementação atual

ReusableAssetLibrary publica cópias verificadas de arquivos presentes em Assets/ ou Content/ de um projeto, com seus derivados (como sprite JSON editável e PNG). Cada pacote é armazenado no diretório privado Library/<id>/v<versão>/files/, acompanhado de asset.json com nome, autor, licença, atribuição, tags, caminhos, SHA-256 e tamanho.

Versões já publicadas são imutáveis. Publicar revisão cria nova pasta vN, sem sobrescrever a anterior. ImportToProject executa preflight de todos os arquivos, compara SHA-256, impede colisões, prepara as cópias num diretório privado de staging e só move os arquivos após todos passarem; em falhas tenta rollback sem remover edição concorrente. Projetos recebem arquivos reais independentes da biblioteca, sem symlink e sem referências dinâmicas. A pasta de biblioteca não é incluída em ZIP ou APK dos jogos.

A interface MainActivity.AssetLibrary permite escolher projeto, asset e metadados para publicar, criar novas versões e copiar uma versão escolhida para um jogo. Não importa nada automaticamente. Licença e atribuição são preenchidas pelo usuário, que deve ter direito de reutilizar o material.

## Testes

ReusableAssetLibraryTests cobre publicação de fonte e derivado, importação por cópia, revisões sem alterar versão anterior, colisão sem sobrescrita, arquivos binários corrompidos, manifesto com traversal e bloqueio de arquivo fora de Assets/Content/.

## Gates pendentes

Build Android/CI da reconciliação, execução em dispositivo, uso dos mesmos Studios fora de projetos com IAssetWorkspace, fluxo de importação/exportação ZIP da biblioteca e validação real de projetos variados. A Biblioteca já possui acesso na Home e na seção Studios, sem projeto aberto; os Studios em si ainda dependem de projeto. Não declarar LUNET-511 completo até resolver estas pendências.

Referências técnicas e licenças: auditoria docs/architecture/audits/lunet-home-studios-explorar-2026-11-15.md, arquitetura offline-first do Android https://developer.android.com/topic/architecture/data-layer/offline-first e fontes licenciadas no OpenGameArt https://opengameart.org/content/faq.

## Conflitos de importação

Ao selecionar **Usar em…**, a biblioteca verifica se os caminhos do asset já existem no projeto escolhido. Se houver colisões, mostra os caminhos e não inicia a importação. Resolva os conflitos no projeto antes de tentar novamente. A verificação é apenas uma prévia: o núcleo repete os controles ao importar, pois os arquivos podem mudar entre a confirmação e a cópia. Validação em Android físico ainda pendente.
