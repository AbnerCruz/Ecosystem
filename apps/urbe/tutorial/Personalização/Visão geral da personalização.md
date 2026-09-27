# Visão geral da personalização

O Urbe pode ficar com a sua cara: cores, fontes, tamanho do texto, jeito do editor, vida na cidade, desenhos do chão e das casas, estilos próprios e até **plugins** que ensinam coisas novas ao aplicativo.

## Onde fica

**Configurações** (⚙️ no topo da Cidade) → **Personalização**. A tela tem estas abas:

| Aba | O que muda |
|---|---|
| **Aparência** | Tema, cor de destaque, cores uma a uma, cantos, densidade, efeito de vidro e animações |
| **Texto** | Fonte e tamanho da interface e das notas, altura da linha |
| **Editor** | Largura do texto, modo inicial (Visual ou Texto), corretor ortográfico |
| **Cidade** | Moradores, animais, nomes das casas e dos bairros, hora do dia, cor de cada terreno |
| **Texturas** | Desenhos próprios para o chão e para as construções |
| **Estilos CSS** | Trechos de CSS para mudar qualquer detalhe visual |
| **Plugins** | Programas que acrescentam funções ao Urbe |
| **Arquivo** | O arquivo `tema.json`, para editar tudo como texto |

Tudo aparece **na hora**, enquanto você mexe.

## Tudo fica em arquivos

A personalização mora numa pasta da sua cidade chamada **Personalização**:

```
Personalização/
  tema.json          ← aparência, texto, editor e cidade
  temas/             ← temas salvos
  estilos/           ← arquivos .css
  texturas/          ← pacotes de texturas (.json)
  plugins/           ← plugins (.js)
```

Por isso a sua personalização vai junto quando você exporta a cidade ou abre a mesma pasta em outro aparelho.

## Peça ao Assistente

O Assistente conhece o formato de tudo isso. Experimente: *"crie um tema com fundo creme, destaque verde-escuro e fonte serifada"*. Ele mostra a mudança antes de gravar.

## Voltar ao padrão

- **Personalização → Arquivo → Restaurar padrão**, ou o comando **Restaurar aparência padrão**.
- Se algo deixou a tela estranha, ligue o **Modo seguro** (veja [[Tutorial/Solução de problemas|Solução de problemas]]).

Próximo: [[Tutorial/Personalização/Temas, cores e fontes|Temas, cores e fontes]]
