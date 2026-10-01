# Inspector

O Inspector mostra e edita as variáveis do jogo **enquanto ele roda**. Abra o Preview e toque em 🔍.

## O que aparece

- Campos e propriedades **públicos** da sua classe `Game` e dos tipos do seu projeto que ela usa.
- Números, textos, `bool`, enums, `Vector2` e cores têm editor próprio. Listas mostram os primeiros itens.
- Mudanças valem na hora, na thread do jogo. Elas não são salvas no código: ao reiniciar, os valores voltam ao que está escrito.

## Atributos

```csharp
public class Player
{
    [Range(0, 10), Tooltip("Velocidade em pixels por segundo")]
    public float Speed = 4;

    [ReadOnly] public int Frame;
    [Hidden] public int Interno;
    [Inspect] private int _vidas = 3;

    [Multiline(3)] public string Notas = "";
    [Color] public string Cor = "#FF8800";
    [File(".png")] public string Sprite = "hero.png";
    [Asset(AssetKind.Sound)] public string Pulo = "jump.wav";

    [Group("Combate")] public int Dano = 2;
}
```

- `Range` vira um controle deslizante e limita o valor.
- `ReadOnly` mostra sem deixar editar; `Hidden` esconde; `Inspect` mostra um membro privado.
- `Group` agrupa campos sob um título.

## Inspector customizado

Crie uma classe que herda de `Inspector<T>` e o app a usa no lugar da lista automática:

```csharp
public sealed class TorreInspector : Inspector<Torre>
{
    public override void OnInspect(InspectorContext ui, Torre torre)
    {
        ui.Header("Torre");
        ui.Slider("Ângulo", () => torre.Angulo, v => torre.Angulo = v, 0, 360);
        ui.Field("Disparos", () => torre.Disparos);
        ui.Button("Atirar", () => torre.Atirar());
    }
}
```
