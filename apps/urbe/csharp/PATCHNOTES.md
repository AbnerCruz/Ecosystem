# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.48
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## Estudo: a fonte e a nota na mesma tela

Nova aba **Estudo** no menu redondo do canto (no computador, na barra da esquerda), para pesquisar, ler e escrever sem trocar de app:

- O editor abre com a sua nota e, junto dela, o **painel de fonte**. Nele você escolhe **Pesquisar na web**, **Vídeo do YouTube** (cole o link) ou qualquer arquivo do vault: PDF, livro EPUB, imagem…
- No celular a fonte fica em cima e a nota embaixo; no computador, a fonte fica à esquerda. **Arraste a divisória** para dar mais espaço a uma das duas.
- Um link tocado dentro da nota abre no painel, sem tirar a nota da tela.
- Selecione um trecho e use **Adicionar seleção à nota** (cita com a origem) ou **Perguntar à IA**. No YouTube, **Anotar instante** guarda o ponto exato do vídeo.

## Livros em PDF no Assistente

- Antes, um PDF acima de 20 MB era recusado, e mesmo os menores iam inteiros na mensagem, além do que os provedores aceitam. Agora um **livro** (mais de 90 páginas ou 16 MB) fica guardado na pasta **Livros** do vault e o Assistente o lê por partes: encontra onde o assunto aparece, lê as páginas necessárias, olha figuras e fórmulas quando você permite e cita a página de cada informação. PDFs curtos continuam indo inteiros.
- O livro fica disponível também no leitor e na aba Estudo.

## Celular

- O menu redondo some enquanto você escreve (flutuava por cima do texto com o teclado aberto) e volta quando o bloco fecha.
- No menu ⋯ da nota as descrições quebram linha em vez de serem cortadas.
- Gaveta do Explorer: "+ Nova" vira "+" em tela estreita, e o título não aparece mais cortado.
- Assistente: o cabeçalho tem 4 botões em vez de 6 (configurações e exportar foram para o ⋯).
- Área de trabalho: a trilha de pastas mostra a pasta em que você está.
- Menu redondo com quatro destinos (Assistente, Notas, Estudo, Grafos) em arco maior, sem rótulos encostando.

## O que ainda falta conferir no aparelho

Os testes automáticos (496 de interface e 1.338 do núcleo) passam. O navegador e o YouTube dentro do painel, a divisória arrastada com o dedo e um livro grande anexado precisam ser conferidos no moto g32.
