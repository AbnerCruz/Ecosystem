# Particle Studio

**Studio → Partículas.** Comece de um efeito pronto (fogo, fumaça, faíscas, explosão, chuva, neve, magia, poeira de passos, bolhas, confete) e ajuste:

- **Emissão:** partículas por segundo (0 = só burst), quantidade do burst, máximo vivo, forma de nascimento (ponto, círculo, retângulo, linha) e seu tamanho.
- **Movimento:** velocidade mínima e máxima, direção e abertura do cone (360° = todas as direções), gravidade, resistência do ar e giro.
- **Aparência:** vida e sua variação, tamanho inicial/final e variação, rotação inicial e mistura **Brilho (aditiva)** para fogo e magia.
- **Cores:** cor inicial e final ou um gradiente (Fogo, Gelo, Arco-íris).

A prévia roda o **mesmo emissor do jogo**: toque para um burst, arraste para mover o emissor, segure para trocar o fundo (escuro, claro, verde). **Salvar** grava `Content/Effects/<nome>.particles.json`.

## No jogo

```csharp
private ParticleEmitter _fogo = null!;

protected override void LoadContent()
{
    _fogo = Content.LoadParticleEmitter("Effects/fire.particles.json", Texture2D.CreateCircle(GraphicsDevice, 16, Color.White));
    _fogo.Start();                      // emissão contínua (se a taxa do arquivo for maior que 0)
}

protected override void Update(GameTime time)
{
    _fogo.Position = _tocha;            // move a origem; partículas já emitidas seguem seu caminho
    _fogo.Update(time.DeltaSeconds);
}

protected override void Draw(GameTime time)
{
    _batch.Begin(BlendState.Additive);  // use Additive quando o efeito tiver "Brilho"
    _fogo.Draw(_batch);
    _batch.End();
}
```

Bursts (explosão, faíscas, confete): `_efeito.Burst(90, posicao);` quando o evento acontecer.

## Validação no aparelho (DEVICE)

1. Abra cada predefinição e confira que a prévia é fluida (60 quadros) com o máximo de partículas.
2. Mexa em direção, abertura, gravidade, resistência e gradiente: a prévia acompanha na hora.
3. Salve, feche e reabra: os valores voltam iguais.
4. Use o efeito no jogo com o código acima e compare com a prévia.
