# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.58
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

## Vídeos feitos pelo Assistente

- **O Assistente entrega o vídeo pronto.** Ao pedir um vídeo, ele narra o roteiro com a voz do aparelho, alonga as cenas que ficariam curtas para a fala, gera o MP4 conferido e as legendas dentro do vault e diz onde estão. Nenhum arquivo existente é substituído.
- **Sem pedir permissão a cada quadro.** Com o agente em "autônomo", olhar os quadros do vídeo não abre mais uma janela de autorização a cada vez.
- **Gráficos e fórmulas aparecem.** As instruções do Assistente mandavam a função e o TeX para o campo errado, e eles saíam vazios. Corrigido, e o motor aceita os dois campos.
- **O selo da IA não aparece mais no vídeo.** Títulos e legendas de notas escritas pelo Assistente não mostram mais o selo de autoria, e os trechos dessas notas podem virar cenas pelo título.
- **Erros que dizem o que fazer.** Fonte ou trecho ausente, propriedade ou curva desconhecida: a mensagem diz qual e lista o que existe. Cenas escritas com outro identificador são mantidas, na ordem, em vez de trocadas por cenas vazias.
- **O Assistente sabe o que o motor faz.** Recebe um exemplo completo de cena e orientações de qualidade (uma ideia por cena, paleta única, nada sob a legenda, elementos que entram animados), em vez de criar publicações de teste.

## Motion graphics

- **Curvas de animação.** Suave, desacelerar, acelerar, constante, ressalto e degrau no inspetor; o Assistente também usa qualquer cubic-bezier.
- **Traços que se desenham.** Seta, sublinhado, visto e círculo, ou qualquer desenho vetorial (SVG).
- **Grupos.** Um objeto pode seguir outro em posição, escala, rotação e opacidade.
- **Linha e seta** como formas; largura e altura animáveis.

## Não testado no aparelho

- Narração e geração do MP4 pelo Assistente foram testadas no computador (com o codificador do PC) e com voz simulada; a voz e o codificador do Android não foram usados nesta versão.
