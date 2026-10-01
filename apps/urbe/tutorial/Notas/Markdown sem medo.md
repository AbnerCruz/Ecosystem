# Markdown sem medo

**Markdown** é um jeito simples de formatar texto usando símbolos comuns do teclado. As notas do Urbe são arquivos Markdown (`.md`), então abrem em qualquer outro programa, até num bloco de notas.

Você **não precisa decorar nada**: o modo Visual e a barra de ferramentas fazem tudo por você. Mas, se quiser escrever no modo Fonte, esta é a colinha.

## Texto

| Você escreve | Aparece |
|---|---|
| `**negrito**` | **negrito** |
| `_itálico_` | _itálico_ |
| `~~riscado~~` | ~~riscado~~ |
| `` `código` `` | `código` |
| `[site](https://exemplo.com)` | [site](https://exemplo.com) |

## Títulos

Um `#` no começo da linha é um título grande; `##` é médio; `###` é pequeno.

## Listas

```
- item
- outro item
  - item dentro do item (dois espaços antes)

1. primeiro
2. segundo

- [ ] tarefa por fazer
- [x] tarefa feita
```

Veja funcionando:

- [ ] tarefa por fazer
- [x] tarefa feita
- lista
  - com um item dentro

## Citações e caixas de destaque

Uma linha começando com `>` vira citação:

> Ideias são como casas: ficam melhores quando têm ruas.

Com `> [!tip]` você cria uma **caixa de destaque** (callout). Os tipos são `note`, `info`, `tip`, `success`, `question`, `warning`, `danger`, `bug`, `example`, `quote`, `abstract` e `todo`.

```
> [!warning] Cuidado
> Este é um aviso importante.
```

> [!warning] Cuidado
> Este é um aviso importante.

## Tabelas

```
| Fruta  | Preço |
|--------|------:|
| Maçã   |  2,00 |
| Banana |  1,50 |
```

| Fruta  | Preço |
|--------|------:|
| Maçã   |  2,00 |
| Banana |  1,50 |

Os `:` na segunda linha alinham a coluna: `:---` à esquerda, `:---:` no centro e `---:` à direita.

## Código

Três crases antes e depois fazem um bloco de código. Escreva a linguagem depois das crases de abertura:

```js
function ola(nome) {
  return 'Olá, ' + nome;
}
```

## Linha separadora

Três traços `---` numa linha sozinha.

---

## Imagens

`![descrição](endereço-da-imagem.png)` mostra uma imagem.

## E mais

- Links entre notas: [[Tutorial/Notas/Links entre notas|Links entre notas]].
- Tags e propriedades: [[Tutorial/Notas/Tags e propriedades|Tags e propriedades]].
- Fórmulas: [[Tutorial/Matemática/Fórmulas em qualquer nota|Fórmulas em qualquer nota]].
