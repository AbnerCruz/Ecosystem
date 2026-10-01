# Referência de plugins

Tudo o que a `api` oferece a um plugin. Para começar, veja [[Tutorial/Personalização/Plugins|Plugins]].

## Estrutura

```js
urbe.plugin({
  nome: 'Nome',          // aparece na tela de plugins
  versao: '1.0',
  descricao: 'O que faz',
  ligar(api) { /* obrigatório */ },
  desligar(api) { /* opcional */ }
});
```

## Mensagens e diálogos

| Código | Faz |
|---|---|
| `api.aviso(texto)` | Mensagem rápida na parte de baixo da tela |
| `api.dialogo.alert({ title, message })` | Caixa de aviso |
| `api.dialogo.confirm({ title, message })` | Pergunta sim/não (devolve `true` ou `false`) |
| `api.dialogo.prompt({ title, label, value })` | Pede um texto |
| `api.dialogo.choose({ title, options: [{ label, value }] })` | Lista para escolher |

Os diálogos devolvem uma *Promise*: use `await` dentro de funções `async`.

## Comandos e botões

| Código | Faz |
|---|---|
| `api.comando({ titulo, executar })` | Comando na paleta (Ctrl+K) |
| `api.botao({ icone, texto, titulo, executar })` | Botão na barra da Cidade |
| `api.executar(id, contexto)` | Roda qualquer comando do app |
| `api.comandos()` | Lista os comandos existentes |

Ícones disponíveis para `icone`: `search`, `map`, `clock`, `star`, `file`, `folder`, `sparkle`, `page`, `palette`, `brush`, `puzzle`, `image`, `sun`, `moon`, `book`, `edit`, `trash`, `check`, `list`, `task`, `code`, `link`, `layers`, `download`, `upload`, `copy`, `house`, `city`, `notes`, `settings`… Com `texto` você usa um emoji no lugar do ícone.

## Notas

| Código | Faz |
|---|---|
| `api.notas.listar(pasta?)` | Lista `{ id, caminho, titulo }` |
| `api.notas.ler(caminho)` | Devolve `{ id, caminho, titulo, conteudo, tags, links }` |
| `api.notas.existe(caminho)` | `true` ou `false` |
| `api.notas.escrever(caminho, texto)` | Cria ou substitui a nota |
| `api.notas.abrir(caminho)` | Abre no editor |
| `api.notas.atual()` | A nota aberta no editor (ou `null`) |
| `api.notas.buscar(texto)` | Procura no título e no conteúdo |

## Eventos

`api.eventos.on(nome, função)`. Nomes: `nota:criada`, `nota:alterada`, `nota:removida`, `cidade:aberta`, `salvo`, `comando`, `aparencia`. Para eventos de nota, a função recebe a nota.

## Aparência

- `api.estilo(css)` acrescenta CSS enquanto o plugin estiver ligado.
- `api.personalizacao.ler()` e `api.personalizacao.mudar({ ... })` leem e mudam o `tema.json` (veja [[Tutorial/Personalização/Referência do tema.json|Referência do tema.json]]).

## Mapa

| Código | Faz |
|---|---|
| `api.mapa.camada((ctx, vista) => { ... })` | Desenha sobre a cidade a cada quadro |
| `api.mapa.vista()` | O que está na tela agora |
| `api.mapa.ir(x, y, zoom)` | Leva a câmera até um ponto |
| `api.mapa.opcoes({ moradores, fauna, nomes, bairros, ambiente })` | Liga e desliga partes da cidade |
| `api.mapa.redesenhar()` | Pede um desenho novo |

A `vista` traz: `tile` (tamanho de um quadradinho do chão na tela), `zoom`, `centro`, `visivel` (área visível), `largura`, `altura`, `paraTela(x, y)` (converte posição do mundo para a tela), `casas()` (as casas visíveis, com `nome`, `x`, `y`, `w`, `h`, `id`) e `ambiente` (dia, entardecer ou noite). O `ctx` é o contexto de desenho de um *canvas* comum.

## Assistente

```js
api.ia.ferramenta({
  nome: 'contar_palavras',
  descricao: 'Conta as palavras de uma nota.',
  parametros: { type: 'object', properties: { caminho: { type: 'string' } } },
  escreve: false,
  executar(args) {
    const n = api.notas.ler(args.caminho);
    return n ? n.conteudo.split(/\s+/).length + ' palavras' : 'Nota não encontrada';
  }
});
```

## Tempo e memória

| Código | Faz |
|---|---|
| `api.intervalo(função, ms)` | Repete a cada tanto tempo |
| `api.depois(função, ms)` | Roda uma vez, depois de um tempo |
| `api.aoDesligar(função)` | Roda quando o plugin for desligado |
| `api.dados.ler(chave, padrão)` | Lê algo que o plugin guardou neste aparelho |
| `api.dados.gravar(chave, valor)` | Guarda um valor neste aparelho |

Tudo isso é desfeito automaticamente quando o plugin é desligado.
