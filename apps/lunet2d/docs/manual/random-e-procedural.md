# Sorteios, sementes e geração procedural

Dentro de um jogo, o gerador principal é `Rng`. A seed vem do sistema operacional. Para começar sempre pela mesma sequência, declare `public MeuJogo() : base(42) { }`.

No perfil Amplo, classes independentes podem usar `Rng` nos callbacks graças a `global using static Lunet.Context;`. No perfil Essencial, use `Context.Rng`. Com uma instância de jogo em mãos, use `jogo.Rng`. Num worker, use `new RandomSource()`, `new RandomSource(42)` ou um fluxo filho já criado. Cada `RandomSource` mutável exige um único dono ou sincronização externa. `RandomField` é imutável e pode ser compartilhado entre threads.

## Posição e raridade de uma moeda

```csharp
// Sorteie ao criar a moeda. A margem deixa um sprite de 32 px inteiro na área.
Vector2 pos = Rng.NextVector2(new Vector2(16, 16), new Vector2(784, 584));
string[] itens = ["Comum", "Rara", "Lendária"];
double[] pesos = [70, 25, 5];
string raridade = Rng.PickWeighted(itens, pesos);
bool bonus = Rng.Chance(0.20);
Color brilho = VisualRng.NextColor(180);
```

`NextInt(1, 7)` lança um dado de seis faces. Inteiros e reais usam limites superiores exclusivos. Limites iguais devolvem o valor informado. Entre inteiros adjacentes há uma saída possível e o estado fica intacto. Pesos são relativos, finitos, não negativos e precisam incluir ao menos um positivo. `Chance` recebe uma fração de 0 a 1 com resolução de 53 bits. Pesos positivos muito pequenos podem desaparecer na resolução do gerador e da aritmética.

`InsideCircle` distribui pontos pela área e `OnCircle` sorteia no contorno. O arredondamento pode deslocar a posição alguns ULPs. `NextPoint` usa `System.Drawing.Point`. `InsideRectangle` aceita `System.Drawing.Rectangle` ou `Lunet.RectangleF`. `NextColor` usa os três bytes superiores de uma palavra, na ordem RGB.

`Pick` escolhe por índice, `Shuffle` modifica a coleção e `Sample` monta um array sem repetir índices. Valores duplicados em posições distintas podem sair juntos na amostra.

## Fluxos de mundo e apresentação

```csharp
var root = new RandomSource(42);
var terrain = root.Fork("terrain");
var loot = root.Fork("loot");
var field = new RandomField(root.Seed);
float mineral = field.FloatAt(-3, 12, channel: 0);
RandomSource itensDaCelula = field.StreamAt(-3, 12, channel: 1);
```

O campo retorna o mesmo valor para a mesma seed, coordenadas, canal e amostra, qualquer que seja a ordem das consultas. Use noise e interpolação para terreno contínuo. `Fork` deriva um filho de uma chave textual ou numérica e da seed original, sem avançar o pai. Use `VisualRng` para efeitos que devem ficar independentes da sequência da simulação. `Jump` e `LongJump` avançam 2^128 ou 2^192 passos. Seeds derivadas de 64 bits podem ter colisões.

## Salvar a posição na sequência

```csharp
Save.Save("loot-rng", loot.CaptureState());
RandomState estado = Save.Load("loot-rng", loot.CaptureState());
loot = RandomSource.FromState(estado);
```

A captura inclui a versão, a seed e as quatro palavras do estado. Seeds sozinhas reiniciam a sequência. Em ferramentas externas que tratem JSON como números JavaScript, transporte palavras de 64 bits como strings hexadecimais de 16 dígitos.

Xoshiro256** produz uma sequência diferente do gerador anterior. Repetir o jogo inteiro depende também das entradas, da ordem de chamadas, da física e do tempo. Normal, exponencial e trigonometria podem variar nos últimos bits entre plataformas. Quando a saída de normal ou exponencial excede a faixa finita, elas lançam `OverflowException` após consumir o sorteio. Use a criptografia do sistema para segredos.

Abra `samples/random-coins`. **Nova seed** muda moedas, células e pontos do círculo. **Repetir seed** reconstrói o mesmo resultado.

- [Xoshiro256** e SplitMix64](https://prng.di.unimi.it/)
- [Redução de Lemire](https://arxiv.org/abs/1805.10941)
- [Random123 e Philox](https://github.com/DEShawResearch/random123)
