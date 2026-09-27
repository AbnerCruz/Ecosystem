# Cidade e texturas

## Vida na cidade

Em **Personalização → Cidade**:

- **Moradores**, **Animais**, **Nomes das casas** e **Nomes dos bairros**: ligue e desligue.
- **Hora do dia**: Dia, Tarde, Noite (com janelas acesas) ou Auto (segue o relógio).
- **Cores do chão**: toque num terreno (grama, floresta, deserto, rio…) e escolha outra cor. As sombras e os brilhos são ajustados sozinhos.

## Texturas próprias

Uma **textura** é o desenho que se repete no chão. Cada terreno usa um quadradinho de **16×16 pixels**. As construções são desenhos de **48×56 pixels**.

### Criar um pacote

1. **Personalização → Texturas → Novo pacote**. Dê um nome.
2. O pacote fica ativo na hora. Ele é um arquivo em `Personalização/texturas/`.

### Desenhar pixel a pixel

Toque no ✏️ de um terreno ou de uma construção. Abre um **editor de pixels**:

- **Lápis**: pinte tocando ou arrastando o dedo.
- **Balde**: pinta uma área inteira da mesma cor.
- **Conta-gotas**: pega uma cor do desenho.
- **Desfazer** e **Grade** (mostra ou esconde as linhas).
- Embaixo ficam as cores. O **＋** escolhe qualquer cor.
- **Salvar** aplica na cidade na hora.

O desenho começa pela textura atual, então dá para só dar uns retoques.

### Usar uma imagem

Toque no 🖼️ e escolha uma foto ou imagem do aparelho. O Urbe reduz a imagem para o tamanho certo.

### Voltar ao original

O botão ↶ de cada item volta ao desenho original. Para desligar tudo, escolha **Nenhum** na lista de pacotes.

### Nomes dos terrenos

`grama`, `prado`, `floresta`, `mata`, `pantano`, `taiga`, `tundra`, `neve`, `colinas`, `montanha`, `pico`, `deserto`, `savana`, `estepe`, `praia`, `rio`, `lago`, `mar`, `profundo`, e `agua` (rio, lago e mar de uma vez).

### Nomes das construções

`casa` (notas), `salao` (.html), `oficina` (.js), `tinturaria` (.css), `torre` (.json/.yaml), `mercado` (.csv), `armazem` (outros arquivos).

### O arquivo do pacote

O pacote é um JSON que você também pode escrever à mão (ou pedir ao Assistente):

```json
{
  "nome": "Meu pacote",
  "paleta": { "agua": ["#3a7bd5"] },
  "chao": {
    "grama": {
      "cores": { "a": "#6e9a47", "b": "#5d8a3c" },
      "pixels": [
        "aaaabaaaaaaabaaa",
        "... 16 linhas de 16 letras ..."
      ]
    },
    "deserto": { "imagem": "data:image/png;base64,..." }
  },
  "construcoes": {
    "casa": { "imagem": "data:image/png;base64,..." }
  }
}
```

Em `pixels`, cada letra é uma cor da lista `cores`, e um ponto `.` usa a cor base do terreno. Um terreno pode ter uma lista com até 4 variações, e o Urbe alterna entre elas para o chão não ficar repetitivo.

Próximo: [[Tutorial/Personalização/Estilos CSS|Estilos CSS]]
