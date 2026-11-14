# Roteiro de validação no aparelho — Studios, prévias e ícones (LUNET-491)

Pré-requisito: APK da release de desenvolvimento desta entrega. Marque cada passo; falhas voltam ao PR.

## 1. Ícones
1. Abra um projeto. Confira a barra superior (voltar, IA, Rodar, ⋯), a navegação inferior (Código, Arquivos, Studio, Erros, Console) e o Explorer: cada ícone deve ser um desenho nítido (não um caractere), com cor igual à do texto do botão, e o ícone do arquivo deve refletir o tipo (código, imagem, sprite, mapa, som, nota, dados).
2. Segure o dedo num botão só de ícone (ex.: desfazer): deve aparecer a dica com o nome (Android 8+).
3. Rode o jogo: barra do Preview (parar, reiniciar, pausar/continuar, avançar quadro, inspecionar, desempenho) com ícones; pausar troca para continuar.

## 2. Assets com prévia
1. Studio ▸ Assets do projeto. Toque num `.sprite.json`: deve abrir o painel com o sprite desenhado sobre fundo xadrez, animando na velocidade do arquivo (toque pausa/continua; contador de frames).
2. Toque numa PNG: imagem nítida, tamanho em pixels. Num `.wav`: onda + ▶ Ouvir, cursor andando, toque na onda toca dali. Num `.ogg`/`.mp3`: toca, sem onda. Numa fonte: texto de amostra. Num JSON: trecho do conteúdo.
3. No Explorer (☰ Arquivos), toque numa imagem, num som e num `.sprite.json`: abre a mesma prévia, e "Ver o arquivo como código" abre o JSON.

## 3. Audio Studio
1. Studio ▸ Áudio ▸ Criar efeito: mova frequência/duração e escolha a forma — a onda muda e, com ▶ tocando, o som reinicia com o ajuste. Crie o efeito.
2. Abra um WAV: ▶ Ouvir toca o trecho recortado com o volume atual; o cursor percorre a onda; mexer no início/fim/volume durante a reprodução atualiza o som; "Repetir" e loop SMPL repetem só a região do loop. Toque na onda para ouvir dali. Salvar cópia continua criando um novo arquivo.

## 4. Sprite Studio — seleção
1. Escolha o retângulo de seleção (ícone de cantos), arraste sobre parte do desenho: contorno tracejado e "N pixels selecionados".
2. Arraste de dentro da seleção: os pixels se movem inteiros, sem deixar rastro. Cantos redimensionam proporcionalmente, meios das bordas esticam, o círculo acima gira (encaixa em múltiplos de 15°). ✓ aplica, ✕ cancela (a imagem volta como estava).
3. Barra da seleção: girar 90°, espelhar H/V, duplicar (carimba uma cópia e continua movendo), apagar. Laço (à mão livre) e varinha (por cor; tolerância em Mais ▸) selecionam regiões. Mais ▸ Selecionar tudo.
4. Desfazer (toque com 2 dedos) durante o movimento cancela; depois de aplicar, desfaz o conjunto. Troque de frame/ferramenta: a seleção é aplicada. Feche e reabra: o resultado foi salvo (JSON + PNG).
