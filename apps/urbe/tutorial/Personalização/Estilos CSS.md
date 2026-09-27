# Estilos CSS

**CSS** é a linguagem que define a aparência das páginas da web. Com ela você muda qualquer detalhe do Urbe que as opções da tela não cobrem.

## Dois jeitos

1. **Estilos** (arquivos): em **Personalização → Estilos CSS → Novo estilo**. Cada estilo é um arquivo `.css` em `Personalização/estilos/`, com um interruptor para ligar e desligar. Toque no ✏️ para editar o arquivo.
2. **CSS rápido**: uma caixa de texto na mesma aba, para poucas regras. Fica guardado no `tema.json`.

## Variáveis úteis

O Urbe usa **variáveis** para as cores e medidas. Mudar uma variável muda o app todo:

| Variável | O que é |
|---|---|
| `--ui-accent` | Cor de destaque |
| `--ui-bg` | Fundo |
| `--ui-surface`, `--ui-surface-2`, `--ui-surface-3` | Painéis e botões |
| `--ui-text`, `--ui-text-2`, `--ui-text-3` | Texto normal, secundário e apagado |
| `--ui-line`, `--ui-line-2` | Linhas e bordas |
| `--ui-danger`, `--ui-ok`, `--ui-warn` | Perigo, sucesso e aviso |
| `--ui-font`, `--ui-editor-font`, `--ui-mono` | Fontes |
| `--ui-editor-size`, `--ui-editor-width` | Tamanho e largura do texto das notas |
| `--ui-r-s`, `--ui-r-m`, `--ui-r-l` | Arredondamento dos cantos |

## Exemplos

Títulos das notas na cor de destaque:

```css
#renderedPreview h1 { color: var(--ui-accent); }
```

Texto das notas justificado:

```css
#renderedPreview p { text-align: justify; }
```

Destacar as tarefas feitas:

```css
#renderedPreview li.task:has(input:checked) { opacity: .55; text-decoration: line-through; }
```

Usar uma fonte da internet (e depois escolher "Lora" em Texto → Outra…):

```css
@import url('https://fonts.googleapis.com/css2?family=Lora&display=swap');
```

## Se algo der errado

Desligue o estilo no interruptor, ou ligue o **Modo seguro** em **Plugins** (ele desliga todos os estilos e plugins sem apagar nada).

Próximo: [[Tutorial/Personalização/Plugins|Plugins]]
