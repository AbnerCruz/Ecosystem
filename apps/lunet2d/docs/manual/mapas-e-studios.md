# Organize ideias e reutilize criações

## Mapas mentais em Notas

No editor do jogo, abra **Notas** e toque em **Mapa mental**. Toque num tópico para selecionar e acrescente filhos com **Tópico**. Arraste um tópico até outro para mudar de ramo, ou use **Mover**. Arraste o fundo para navegar, faça pinça para ampliar e use **Centralizar** para recuperar o início. **Recolher/expandir**, **Desfazer** e **Refazer** ficam na faixa de ações, que rola na horizontal.

**Ligar** cria ou remove uma ligação entre dois tópicos. **Referência** liga um tópico a uma nota ou material do projeto. Se o arquivo for movido ou excluído, a referência fica visível como ausente e pode ser escolhida de novo. **Apresentar** percorre os filhos do tópico selecionado, um ramo de cada vez. O mapa acompanha o ZIP do projeto e preserva as notas Markdown e os esboços PNG.

## Criar sem abrir um jogo

Na Home, abra **Studios** e escolha uma ferramenta. Use **Minhas criações** para reabrir rascunhos. Para compartilhar dentro do seu próprio Lunet, escolha **Publicar na Biblioteca** e preencha título, licença, autoria, atribuição e tags. Em **Biblioteca pessoal**, escolha uma versão e o jogo de destino.

A versão na Biblioteca é imutável. O jogo recebe uma cópia editável com suas dependências. Arquivos em conflito são preservados. Se quiser importar de novo para o mesmo jogo, resolva os caminhos conflitantes primeiro. Para uma paleta importada, abra a cópia e escolha **Usar como paleta ativa**. As cores dos sprites existentes são preservadas.

## O que vai junto

- **Sprite e animação** têm fonte com camadas e frames, FPS e spritesheet PNG. Abra a cópia no Sprite Studio. Use o exemplo de animação do editor para carregar a folha no jogo.
- **Tile** tem mapa, atlas PNG e fonte do atlas quando disponível. Camadas e colisão ficam no mapa. Use `Content.LoadTileMap` e o caminho do atlas para testar.
- **Música e efeitos** levam a composição ou preset e o WAV renderizado. O **Editor WAV** guarda o próprio arquivo PCM editável.
- **Paleta** tem valores ordenados e nomeados, JSON e GPL.
- **Partículas** levam o preset. O Studio usa a forma gerada localmente. Para aparência de jogo com textura própria, passe a textura escolhida a `Content.LoadParticleEmitter`.
- **Dados** levam a tabela, seu esquema e as tabelas referenciadas. Os nomes usados nas colunas de referência são preservados. No projeto, o Data Studio pode gerar uma classe para leitura com `Content.LoadJson`.
