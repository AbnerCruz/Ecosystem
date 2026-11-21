# Publicações — o que as comunidades relatam (16/11/2026)

Pesquisa feita antes de fechar as mudanças de exportação e áudio do PR #952. As fontes são relatos de usuários e documentação de produtos; servem de referência, não de regra.

## Imagens que somem ao exportar (CST-162, CST-163)

- **Relatos:** usuários do Obsidian dizem que o HTML exportado leva só uma referência relativa no lugar da imagem ([fórum Obsidian](https://forum.obsidian.md/t/html-export-isnt-embedding-images-i-just-a-relative-reference-instead-of-an-image/50500)). Imagens com caminho relativo não aparecem fora do app ([pedido antigo](https://forum.obsidian.md/t/support-img-and-video-tag-with-src-relative-path-format/18647)), e imagens também somem na exportação para PDF ([fórum](https://forum.obsidian.md/t/why-does-images-and-other-stuff-dont-get-exported-to-pdf/81976)). Plugins nasceram só para embutir imagens em base64 ([Export as Base64](https://community.obsidian.md/plugins/export-img-to-base)) ou para avisar quais imagens faltam ([Owen Exporter](https://community.obsidian.md/plugins/owen-exporter)).
- **Decisão no Urbe:** as imagens vão dentro do HTML, então o arquivo abre sozinho, inclusive sem internet. `![[foto.png]]` também vira imagem em EPUB, Word, ODT, PPTX e PDF. Uma imagem ausente é dita no lugar dela, nunca deixa um quadro vazio. Áudio e vídeo, pesados demais para embutir, vão no pacote ZIP em `media/`, com manifesto.

## Música do vídeo (CST-172)

- **Padrões observados:** o invideo aplica 2 s de saída à música por padrão ([ajuda invideo](https://help-studio.invideo.io/en/articles/9405105-how-to-fade-music)). O Vocal Video aplica 5 s de saída no fim ([ajuda Vocal Video](https://help.vocalvideo.com/article/24-understanding-vocal-video-audio-settings)). O Kdenlive tem padrão de cerca de 3 s, e usuários pedem para mudá-lo ([fórum KDE](https://forum.kde.org/viewtopic.php%3Ff=270&t=126170.html)). O Narakeet deixa a música em volume cheio do início ao fim, e o próprio produto sugere suavizar as pontas ([Narakeet](https://www.narakeet.com/news/2020/05/18/video-background-music-effects.html)). No Premiere Elements, pedem para ajustar o padrão ([Adobe Community](https://community.adobe.com/t5/premiere-elements/default-fade-settings/m-p/10053439)).
- **Decisão no Urbe:** música entra em 1 s e sai em 2 s, o mesmo valor de saída do invideo. Voz e efeitos ficam sem fade. O padrão não é imposto: **Ajustar áudios do vídeo** muda o volume, a entrada e a saída de cada trilha.

## Pendente

Validação no aparelho e retorno do proprietário.
