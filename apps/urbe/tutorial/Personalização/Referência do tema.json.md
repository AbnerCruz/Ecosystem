# Referência do tema.json

O arquivo `Personalização/tema.json` guarda a aparência. **Escreva só o que quiser mudar**: o resto usa o padrão. Salvar o arquivo aplica na hora. Se tiver erro, a aba **Arquivo** da Personalização mostra exatamente onde.

## Exemplo

```json
{
  "tema": "sepia",
  "cores": { "destaque": "#a0582a" },
  "texto": { "fonte": "serifada", "tamanhoEditor": 18 },
  "forma": { "cantos": "retos" },
  "editor": { "largura": "estreita" },
  "cidade": { "ambiente": "auto", "fauna": false }
}
```

## Todas as opções

| Chave | Valores | Padrão |
|---|---|---|
| `tema` | `escuro`, `meia-noite`, `floresta`, `oceano`, `vinho`, `claro`, `sepia`, `alto-contraste` ou o caminho de um tema salvo | `escuro` |
| `cores.destaque` | cor `#rrggbb` | do tema |
| `cores.fundo`, `superficie`, `superficie2`, `superficie3` | cor | do tema |
| `cores.linha`, `linha2` | cor | do tema |
| `cores.texto`, `texto2`, `texto3` | cor | do tema |
| `cores.perigo`, `sucesso`, `aviso`, `codigo` | cor | do tema |
| `texto.fonte` | `sistema`, `arredondada`, `humanista`, `serifada`, `leitura`, `mono`, `manuscrita` ou nome de fonte | `sistema` |
| `texto.fonteEditor` | igual a `fonte` | `sistema` |
| `texto.tamanho` | 12 a 22 | 15 |
| `texto.tamanhoEditor` | 13 a 28 | 17 |
| `texto.alturaLinha` | 1.2 a 2.2 | 1.7 |
| `forma.cantos` | `retos`, `suaves`, `redondos` | `suaves` |
| `forma.densidade` | `compacta`, `normal`, `confortavel` | `normal` |
| `forma.vidro` | `true`, `false` | `true` |
| `animacoes` | `sistema`, `ligadas`, `reduzidas` | `sistema` |
| `editor.largura` | `estreita`, `media`, `larga`, `total` | `media` |
| `editor.modoInicial` | `visual`, `texto` | `visual` |
| `editor.ortografia` | `true`, `false` | `true` |
| `cidade.moradores`, `fauna`, `nomes`, `bairros` | `true`, `false` | `true` |
| `cidade.ambiente` | `dia`, `entardecer`, `noite`, `auto` | `dia` |
| `cidade.texturas` | caminho de um pacote em `Personalização/texturas/`, ou `""` | `""` |
| `cidade.paleta` | `{ "terreno": ["#base", "#sombra", "#luz", "#detalhe"] }` | `{}` |
| `estilos` | `{ "Personalização/estilos/arquivo.css": true }` | `{}` |
| `css` | regras CSS extras | `""` |

Os nomes dos terrenos estão em [[Tutorial/Personalização/Cidade e texturas|Cidade e texturas]].

## Tema salvo

Um arquivo em `Personalização/temas/`:

```json
{
  "nome": "Meu tema",
  "claro": false,
  "base": "escuro",
  "cores": { "destaque": "#ff9f6b", "fundo": "#101014" }
}
```

Use no `tema.json` com `"tema": "Personalização/temas/Meu tema.json"`, ou só `"tema": "Meu tema"`.

Voltar: [[Tutorial/Personalização/Visão geral da personalização|Visão geral da personalização]]
