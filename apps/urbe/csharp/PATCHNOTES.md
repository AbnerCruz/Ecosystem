# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.47
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

Esta versão arruma o que piorou entre a beta.38 e a beta.46: o app mais pesado para abrir, telas de leitura amontoadas e partes do editor cobertas.

## Abrir o app mais leve

- O app voltava a construir o Assistente inteiro (conversas, agentes) toda vez que abria, mesmo sem usá-lo. Agora ele só é montado quando você o abre.
- Os motores de leitura de slides e de OCR (cerca de 7 MB) estavam dentro da parte principal do app, que o Android descompacta a cada abertura. Foram para um pacote separado, carregado só quando um desses arquivos é aberto.

## Editor

- A faixa de abas aparece só quando há duas ou mais abas abertas. Com uma nota só, o texto ganha essa linha de volta. As abas ficaram planas, com o nome da nota e um traço na aba ativa.
- **Abrir outra nota em nova aba** está no menu ⋯ do editor.
- O botão do menu radial no celular fica acima da barra de formatação e do rodapé, em vez de cobrir o último botão da barra e o seletor Leitura/Edição. O seletor voltou para a direita.

## Leitores (PDF, imagem, EPUB, navegador)

- Uma barra só, com ícones: no PDF, página anterior/próxima, número da página, zoom, texto selecionável, seleção e reconhecimento de texto (OCR).
- Sumário, links da página, página única/rolagem, anotações, abrir em outro app e renomear/mover/excluir ficam no ⋯.
- **Abrir em outro app** só aparece grande quando o Urbe não consegue mostrar o arquivo.
- Modo texto do PDF: Enter na caixa de página vai para a página; Enter na busca busca. Resultados em lista, com a página destacada.
- EPUB: capítulo anterior/próximo e a lista de capítulos numa linha; letra, entrelinha, tema e margens no botão de aparência; a busca abre pelo ícone.
- Navegador de estudo: endereço, voltar, avançar, recarregar e fechar numa linha. Copiar endereço, salvar referência e abrir no navegador do sistema no ⋯.
- O menu radial some enquanto um leitor está aberto (cobria os botões do leitor).

## Publicações e painel fixado

- Estúdio de Publicações com uma barra só: desfazer, refazer, composição, exportar HTML e ⋯ (renomear, EPUB, imprimir). Botões desativados não viram mais blocos cinza.
- No painel fixado, **Escolher arquivo** virou um ícone e não cobre mais o nome do arquivo.

## O que ainda falta conferir no aparelho

Os testes automáticos (492 de interface e os do núcleo) passam, mas não substituem o uso no moto g32: abertura do app, digitação, teclado e toque precisam ser conferidos no aparelho.
