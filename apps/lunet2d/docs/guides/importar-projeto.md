# Importar um projeto ZIP

Na tela inicial de projetos, toque em **Importar ZIP** e selecione o arquivo.
Aguarde a mensagem com o nome do projeto; toque nele na lista para abrir.
A operação funciona offline quando o arquivo está salvo no aparelho.

Use um ZIP criado em **Exportar ZIP** (toque longo no projeto ou menu do editor).
Também é aceito um ZIP com `lunet.json` e os arquivos diretamente na raiz.
Se o projeto já existir, a cópia recebe `_2`, `_3` etc.; o original permanece
intacto. O código, conteúdo e identidade do jogo são preservados.

O ZIP deve conter um único projeto e seu arquivo de entrada. O limite é
256 MiB de ZIP, 512 MiB descompactados, 10.000 entradas e 1 MiB de manifesto.
ZIPs inválidos mostram uma mensagem e não aparecem na lista. Cancelar o
seletor mantém a lista como estava.

## Roteiro de teste no aparelho

1. Crie um projeto Coletor de moedas, altere uma linha e exporte seu ZIP.
2. Volte à tela de projetos, toque em Importar ZIP e selecione esse ZIP.
3. Confira a cópia com sufixo `_2`; abra, confira a linha alterada e execute.
   O som e as imagens devem funcionar como no original.
4. Abra o projeto original e confira que seu código permanece igual.
5. Feche e reabra o app; os dois projetos devem continuar disponíveis.
6. Cancele uma nova seleção; nenhum projeto deve aparecer.
7. Selecione um ZIP sem `lunet.json`: deve aparecer Falha ao importar ZIP,
   sem projeto parcial na lista.
8. Com o ZIP salvo localmente, repita a importação sem internet.

Validação Android pendente; os testes automáticos não substituem este roteiro.
