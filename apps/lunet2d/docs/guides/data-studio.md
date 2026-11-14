# Data Studio

Dados do jogo (inimigos, itens, fases, diálogos, lojas) ficam melhor numa **tabela** do que espalhados no código. **Studio → Dados.**

## Tabela

- Cada tabela é `Content/Data/<nome>.json`, uma lista de objetos, e o esquema fica ao lado em `<nome>.schema.json`. Uma tabela JSON que já exista abre direto: os tipos das colunas são deduzidos dos valores.
- **Toque numa célula** para editar. O teclado segue o tipo da coluna. Sim/não alterna com um toque; opções e referências abrem uma lista.
- **Toque no cabeçalho** para mudar a coluna: nome, tipo, chave única, obrigatório, mínimo e máximo, opções, tabela referenciada e descrição.
- **Toque no número da linha** para duplicar, mover ou excluir.
- **Ordenar**, **Problemas** (lista de células inválidas, que também aparecem destacadas) e **Classe C#**.

## Tipos

| Tipo | Exemplo | Regras |
|---|---|---|
| texto | `"Slime"` | — |
| inteiro | `10` | mínimo/máximo |
| número | `1.5` | mínimo/máximo |
| sim/não | `true` | — |
| opções | `"terra"` | uma das opções |
| referência | `"espada"` | precisa existir na chave de outra tabela |
| JSON | `{"x": 1}` | livre |

## No jogo

**Classe C#** gera `Data/<Classe>.cs`:

```csharp
var inimigos = Content.LoadJson<List<Inimigo>>("Data/inimigos.json");
foreach (var inimigo in inimigos) Log.Info($"{inimigo.Nome}: {inimigo.Vida} de vida");
```

Gere de novo quando mudar as colunas.

## Validação no aparelho (DEVICE)

1. Crie uma tabela "inimigos", adicione as colunas vida (inteiro, de 1 a 100) e tipo (opções terra/ar), e preencha 3 linhas.
2. Digite um valor inválido: ele é recusado com explicação. Um valor fora do limite aparece destacado em Problemas.
3. Crie a tabela "itens" e uma coluna drop em inimigos que referencia itens; escolha o drop pela lista.
4. Gere a classe C#, carregue no jogo com `LoadJson` e rode.
