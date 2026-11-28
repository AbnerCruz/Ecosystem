# Ordenação de sprites, importações e contexto

## Ordenar pela posição dos pés

O lote convencional continua disponível: Begin, Draw e End. Ele envia os quads quando muda a textura, quando enche o buffer físico ou quando termina o lote. Para uma vista em que um ator pode ficar atrás ou à frente de uma árvore, abra uma passagem ordenada:

```csharp
batch.BeginSorted(camera);
batch.Draw(playerTexture, playerFeet - playerPivot, Color.White, order: playerFeet.Y);
batch.SetOrder(treeFeet.Y);
batch.Draw(trunk, treeFeet - trunkPivot, Color.White);
batch.Draw(crown, treeFeet - crownPivot, Color.White);
batch.End();

batch.Begin(); // HUD fora da câmera
batch.DrawString(font, "Pontos: 10", new Vector2(8, 8), Color.White);
batch.End();
```

A chave é finita e pode ser negativa. Ascending (padrão) desenha menor antes; Descending inverte somente a chave. Empates, inclusive -0 e +0, mantêm a sequência de submissão. SetOrder altera a chave corrente (inicialmente zero); uma chave explícita de Draw vale só para aquela operação. Texto, atlas, sprite, partículas, tilemap, nine-slice e DebugDraw usam a chave corrente. A câmera é capturada na abertura. End ordena o lote inteiro antes de enviar trechos consecutivos da mesma textura; não reagrupa texturas separadas por outros sprites. CancelSorted abandona o lote sem envio.

Não altere alvo, resolução, viewport, clear ou uniforms do shader ativo durante a passagem. Abra passagens separadas para câmera, região, material, render target e HUD. Recursos precisam permanecer vivos até End. End libera a guarda mesmo se o backend falhar; não desfaz pixels enviados. Não há depth test, classificação automática por textura nem limite arbitrário que descarte sprites. Escolha a âncora do chão, não o topo do sprite. Para grupos mais elaborados, organize uma lista reutilizada pelo par (camada, Y dos pés) e use a ordem original como terceiro desempate.

## Importações são código real

Nos projetos novos, abra `Project/Imports.cs` no Explorador de arquivos. O arquivo físico é Project/Imports.cs e entra no ZIP, na análise e na compilação. Uma diretiva global using vale para os demais arquivos C# do jogo; ela não instala um assembly nem importa namespaces filhos automaticamente.

O padrão Amplo reúne os 24 namespaces do estudo e `global using static Lunet.Context;` (25 diretivas). O preset Essencial reúne os oito namespaces básicos. O arquivo é editável: remova, acrescente ou qualifique imports conforme seu projeto. Projetos existentes não são alterados automaticamente; a troca de preset com diff na casca Avalonia é uma frente de UI separada. Não remova imports locais que ainda são necessários. A remoção ou mudança de uma global pode alterar diagnósticos em outros arquivos.

Em caso de ambiguidade, qualifique o tipo ou declare um alias local, por exemplo `using GamePointer = Lunet.Input.Pointer;`. System.Reflection.Pointer e Lunet.Input.Pointer têm o mesmo nome; System.Threading.ThreadState e System.Diagnostics.ThreadState também. Extensões LINQ podem introduzir novos candidatos. Vector2 vem de System.Numerics. As APIs de multiplayer local e atributos de inspeção ficam em Lunet; não existem os namespaces de jogo Lunet.Multiplayer e Lunet.Inspection.

O manifesto lunet.json registra sdkImportsVersion=1 quando a política SDK é explícita. O SDK externo deve usar essa política mesmo se Imports.cs for apagado:

```sh
dotnet build runtime-template/Lunet.GameCode.csproj -c Release \
  -p:GameSourceRoot=/caminho/do/jogo -p:LunetSdkImportsVersion=1
```

Projetos legados sem essa propriedade usam 0 (política anterior). O workflow de compilação cloud lê o manifesto. Preview e APK offline compilam os arquivos reais sem sintetizar usings. É necessário regenerar workflows cloud antigos para receber a política atual; não há migração silenciosa.

## Context: disponível por padrão, uso opcional

A classe independente Player pode receber InputState e GraphicsDevice explicitamente, ou escolher Lunet.Context. Um `using static Lunet.Context;` local permite ler Input e Graphics diretamente; `global using static Lunet.Context;` vem no Imports.cs de novos projetos com o perfil Amplo. O desenvolvedor pode removê-lo ou usar um import local. As propriedades retornam as mesmas instâncias do Game, não um registro global substituível.

```csharp
public sealed class Player
{
    private Texture2D texture = null!;
    public Vector2 Feet { get; private set; } = new(180, 320);

    public void Load() => texture = Texture2D.CreateCircle(Graphics, 32, Color.Yellow);
    public void Update()
    {
        if (Input.TryGetPointer(out var pointer)) Feet = pointer;
    }
    public void Draw(SpriteBatch batch) =>
        batch.Draw(texture, Feet - new Vector2(16, 32), Color.White, order: Feet.Y);
}
```

Use apenas dentro de callbacks síncronos do host: Initialize, LoadContent, Update, Draw, timers, Dispatcher, Pause/Resume e UnloadContent. Graphics e Content ficam indisponíveis em Pause, Resume e UnloadContent, por prudência com o contexto EGL; demais serviços ainda podem ser consultados durante teardown válido. No Android, o backend verifica a thread do renderizador. Isso não transforma a GPU em sandbox nem garante que referências retidas sejam thread-safe.

Não consulte Context em workers, continuations de await ou async void. Capture explicitamente o Dispatcher antes de postar uma ação. Uma continuação indevida bombeada durante o callback de outro jogo pode observar aquele jogo; não use contexto ambiental em código assíncrono. Não há SpriteBatch, câmera, cena ou mundo físico global.

Os projetos completos em samples/framework-evolution-explicit e samples/framework-evolution-context demonstram a mesma árvore e personagem, com passagem de mundo e HUD. Cada versão usa uma classe Player independente; a versão contextual evita repassar GraphicsDevice, InputState e ViewSize, mantendo câmera e batch explícitos. Abra o projeto e toque acima/abaixo da base do tronco para comparar a sobreposição. As texturas são geradas em código para permitir execução offline.
