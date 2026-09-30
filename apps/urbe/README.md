# Urbe

**Suas notas em Markdown, desenhadas como uma cidade.**

Cada nota vira uma casa, cada pasta um bairro e cada `[[link]]` uma rua. Moradores andam entre as notas ligadas, o mundo é um continente formado por placas tectônicas, com serras, clima e rios que descem até o mar, e tudo continua sendo arquivo `.md` comum, que abre em qualquer outro programa.

> **Versão 1.8.2 beta.** App de verdade para **Windows** e **Android**, que guarda as notas numa pasta real e se atualiza sozinho. Também abre no navegador.

## Instalar

Baixe em **[Lançamentos](https://github.com/AbnerCruz/Urbe/releases/latest)**:

- **Windows**: `Urbe-Setup-(versão).exe`. As notas ficam em `Documentos\Urbe` (ou na pasta que você escolher). O app se atualiza sozinho.
- **Android**: `Urbe-(versão).apk`. As notas ficam em `Documentos/Urbe`. O app avisa quando sai versão nova.

Editar as notas por fora (Explorer, Obsidian, OneDrive…) funciona: o Urbe percebe as mudanças.

## Urbe 2.0

A próxima geração do projeto está em fase de **descoberta e auditoria**, antes de novas grandes implementações. A documentação de planejamento fica em [docs/v2/](docs/v2/README.md), com auditoria da 1.x, Requirement Ledger e processo de decisões arquiteturais.

## O que o Urbe faz

- **Cidade**: mundo em pixel-art gerado a partir das suas notas, com mapa, dia e noite, moradores e animais.
- **Editor**: modo Visual e modo Fonte (Markdown), com tabelas, callouts, tarefas, listas aninhadas, links entre notas, tags, propriedades, abas, painel dividido, localizar e substituir, versões anteriores e lixeira.
- **Matemática**: fórmulas LaTeX em qualquer nota (KaTeX embutido), com editor visual, símbolos, modelos e autocompletar.
- **Páginas**: estúdio para montar sites e **livros** (34 blocos, **layout livre** peça por peça e responsivo com 19 peças e 24 composições prontas, 13 modelos carregáveis dentro do editor, 10 temas, blocos próprios e CSS livre) a partir das notas, exportando HTML de arquivo único ou PDF pronto para imprimir, com tamanho de página, margens espelhadas e números de página.
- **Composições**: junta várias notas num documento pronto para imprimir.
- **Assistente de IA**: agentes que leem, buscam, escrevem e organizam o vault com ferramentas, mostrando cada passo e pedindo aprovação. Funciona com OpenRouter, Anthropic, OpenAI, Google Gemini, Ollama (local) ou qualquer API compatível com a da OpenAI.
- **Personalização**: temas (inclusive claro, sépia e alto contraste), cores, fontes, densidade, editor, cidade, texturas desenhadas pixel a pixel, estilos CSS e **plugins** com uma API em português.
- **Tutorial**: uma pasta com 46 notas que explica tudo, criada na primeira vez que o app abre (no app instalado, como arquivos de verdade).

## Desenvolvimento

O app é web estático, sem etapa de build. O mesmo código roda no navegador e dentro dos apps instalados:

- `native/desktop/`: casca **Electron** (Windows). `npm run desktop` abre o app; `npm run desktop:build` gera o instalador.
- `native/android/`: projeto **Capacitor** (Android). `npm run android:sync` copia o app para o projeto.
- `src/native/bridge.js`: a ponte. No app instalado, dá ao Urbe "handles" de pasta sobre o disco real, com a mesma interface do File System Access do navegador.
- `.github/workflows/app.yml`: a cada versão nova na `main`, constrói o `.exe` e o `.apk` e publica o lançamento que os apps usam para se atualizar.

No navegador:

```bash
# qualquer servidor de arquivos estáticos serve
python3 -m http.server 8080
# depois abra http://localhost:8080
```

Publicar é só copiar a pasta para uma hospedagem estática (GitHub Pages, Netlify…). O service worker (`sw.js`) guarda tudo para uso offline.

## Onde ficam os dados

Tudo fica **no aparelho**. No app instalado, numa pasta de verdade (`Documentos/Urbe`). No navegador, no armazenamento do navegador ou, no Chrome e no Edge de computador, numa pasta escolhida por você. Dentro do vault:

```
Tutorial/          notas do tutorial
Personalização/    tema.json, temas/, estilos/, texturas/, plugins/
Páginas/           páginas (.page.json) e modelos
.urbe/             mapa da cidade, lixeira, histórico e journal de recuperação
```

O Assistente envia ao provedor escolhido só o que precisa ler para responder. As chaves de API ficam apenas no aparelho.

## Estrutura do código

| Pasta | Conteúdo |
|---|---|
| `src/core/` | Núcleo: eventos, comandos, documentos, índice de conhecimento, histórico, lixeira, agendador, atalhos |
| `src/persistence/` | Gravação do vault com journal |
| `src/editor/` | Sessão, abas, painel dividido, localizar, ferramentas do modo Visual |
| `src/explorer/` | Aba Notas (árvore, seleção, operações) |
| `src/world/` | Terreno procedural, arte em pixel-art, worker de chunks, projeção e ruas |
| `src/ai/` | Provedores, ferramentas, agentes, loop agêntico e interface do Assistente |
| `src/pages/` | Motor, modelos, estúdio e ferramentas de páginas |
| `src/math/` | Fórmulas: núcleo e editor |
| `src/customize/` | Personalização, plugins, tela e ferramentas do Assistente |
| `src/tutorial/` | Pasta Tutorial (conteúdo gerado de `tutorial/`) |
| `src/ui/` | Diálogos, ícones, paleta de comandos, busca, configurações |
| `src/app.js` | Casca da cidade e integração (código histórico em camadas) |
| `tutorial/` | Fonte das notas do Tutorial, em Markdown |
| `tests/` | Testes em Node (sem dependências) |

Mais detalhes em [ARCHITECTURE.md](ARCHITECTURE.md). O histórico de versões está no [CHANGELOG.md](CHANGELOG.md).

## Desenvolvimento

```bash
# testes (os mesmos do CI)
node tests/core.mjs            # e os demais arquivos em tests/

# depois de editar tutorial/*.md
node tools/build-tutorial.mjs  # gera src/tutorial/content.js e confere links e tags
```

O CI (`.github/workflows/structural-checks.yml`) confere a sintaxe de todos os arquivos e roda todos os testes.

### Plugins

Plugins são arquivos `.js` em `Personalização/plugins/`:

```js
urbe.plugin({
  nome: 'Olá',
  ligar(api) {
    api.comando({ titulo: 'Dizer olá', executar: () => api.aviso('Olá!') });
  }
});
```

Nenhum plugin roda sem o usuário ligar, e qualquer mudança no código pede nova aprovação. A referência completa da API está no Tutorial (`Tutorial/Personalização/Referência de plugins.md`).

## Licença

Urbe © 2026 Abner P. S. Cruz — **todos os direitos reservados** (ver [`LICENSE`](LICENSE)). O código é público para leitura; não há licença de uso, modificação ou redistribuição para terceiros sem autorização por escrito. Componentes de terceiros mantêm as suas licenças ([`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)).
