# Música e efeitos sonoros

O Audio Studio agora faz três coisas: **compor músicas**, **desenhar efeitos sonoros** e editar WAV (recorte, volume, loop).

## Compor uma música

**Studio → Áudio → ♪ Compor música.** Escolha "Vazia" ou um **começo pronto** a partir de uma sequência de acordes (pop, épico, clássico, aventura em menor…). O começo pronto já vem com melodia, acordes, baixo e bateria.

- **Faixas** (topo): cada uma tem um instrumento. Toque para editar; **segure** para trocar o instrumento, mudar volume, pan, eco, reverb, timbre (brilho, ataque, sustentação, vibrato), silenciar, solo, duplicar ou excluir. **＋ Faixa** adiciona.
- **Padrões**: trechos que se repetem (A, B, refrão…). Segure para duplicar e variar, mudar o tamanho (8 a 128 passos), transpor por grau da escala ou excluir.
- **Arranjo**: a ordem dos padrões na música. **＋** coloca o padrão atual no fim; segure para remover.
- **Grade**: as linhas são as notas **da escala** da música (não tem nota "errada"), ou as peças da bateria numa faixa de bateria. Toque para pôr ou tirar uma nota; arraste para a direita a partir de uma nota para esticá-la. **Duração** escolhe o tamanho das notas novas. As notas das outras faixas aparecem apagadas, para você acompanhar.
- **Geradores**: **✦ Melodia** (toque de novo para outra variação), **Acordes**, **Arpejo**, **Baixo** e **Batida** seguem a sequência de acordes escolhida.
- **⋯**: andamento (BPM), tom, escala (maior, menor, dórico, pentatônica, blues…), swing, notas visíveis, loop, volume geral, renomear e **Exportar WAV para o jogo**.
- **▶ Padrão** toca o padrão em repetição; **▶ Música** toca o arranjo inteiro. O que você ouve é o que será exportado.

A música fica em `Content/Audio/<nome>.song.json` (fonte editável) e o WAV em `Content/Audio/<nome>.wav`:

```csharp
var musica = Content.LoadMusic("Audio/tema.wav");
Audio.PlayMusic(musica, loop: true, fadeInSeconds: 1);
```

Com o loop ligado, o fim da música emenda no começo sem pausa (o eco e o reverb do fim continuam no início).

## Desenhar um efeito sonoro

**Studio → Áudio → ✦ Desenhar efeito sonoro.** Toque numa categoria (moeda, laser, explosão, power-up, dano, pulo, menu, magia, alarme) para gerar um som; toque de novo para outro. **Variação** faz um parecido. Ajuste frequência, deslize de tom, salto, vibrato, ataque, duração, impacto, decaimento, repetição, brilho, efeito retrô e volume. Cada mudança toca de novo.

**Salvar no jogo** grava `Content/Audio/<nome>.wav` e a fonte `<nome>.sfx.json` (abra de novo para continuar ajustando):

```csharp
var moeda = Content.LoadSound("Audio/moeda.wav");
moeda.Play();
```

## Com a IA

A IA conhece os dois formatos: pode escrever um `.song.json` (por exemplo, "faça uma música de chefe em menor harmônica, 140 BPM") ou um `.sfx.json`; abra no Audio Studio e exporte o WAV.

## Validação no aparelho (DEVICE)

1. Crie uma música com um começo pronto, toque **▶ Música** e confira melodia, acordes, baixo e bateria.
2. Ponha e tire notas, estique uma, gere outra melodia, troque o instrumento e mexa no eco/reverb; ouça a diferença.
3. Crie um padrão B duplicando o A, mude-o e monte o arranjo A A B A.
4. Exporte e toque no jogo com `Audio.PlayMusic`; confira que o loop não tem pausa.
5. Desenhe uma moeda e uma explosão, salve, toque no jogo com `Content.LoadSound(...).Play()`.
6. Feche e reabra: músicas e efeitos continuam editáveis.
