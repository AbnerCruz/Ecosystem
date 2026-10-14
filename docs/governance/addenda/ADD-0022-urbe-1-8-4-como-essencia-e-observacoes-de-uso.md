# ADD-0022 — Ordem do proprietário: a 1.8.4 é referência de essência, não de defeitos; observações de uso viram o rumo do Urbe

**Data:** 2026-10-13. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0045. **Escopo:** produto Urbe e o cliente C# (`apps/urbe/csharp`). **Complementa** ADD-0020/DEC-0043 (não a substitui): o Urbe continua sendo a 1.8.4-beta em C# nativo.

## O que o proprietário disse (fatos, com as palavras dele)

1. Entregou 19 observações escritas enquanto usava a 1.8.4-beta (arquivo `Observações.md` da sessão; texto integral reproduzido no fim deste documento) e pediu: *"não sai fazendo direto, quero primeiro que você analise [...] eu fui anotando ideias soltas de algo que talvez nem exista, então é bem delicado"*.
2. Depois da análise, respondeu às quatro perguntas:
   - **Regra de paridade** (trocar "1.8.4 exata" por "1.8.4 como referência de essência; defeitos e melhorias aprovadas podem divergir"): *"Aceito."*
   - **Formato dos recursos novos** (sintaxe no `.md` ou dados em `.urbe/`): *"Sintaxe .md"*.
   - **Composições** (remover, convertendo as existentes em páginas): *"Sim"*.
   - **"Urbe como sistema operacional"**: *"Essa é a parte que menos tenho segurança de como vai ficar, e talvez eu nem venha gostar..."*
3. Delegação: *"Vou deixar você tomar as decisões, sei que você vai conseguir fazer tudo de forma coerente. Pode começar"*.

## Decisões do proprietário

1. **A 1.8.4-beta é a referência de essência** do Urbe (mundo procedural, pixel-art, sensação de aquário vivo, cidade em tela cheia, dados em `.md` comuns), **não de defeitos**. Um comportamento da 1.8.4 que o proprietário apontou como defeito ou que este registro lista como melhoria **não é portado**: o cliente C# o corrige. Toda divergência cita DEC-0045 no código ou no ROADMAP; onde existe oráculo da 1.8.4, o oráculo continua provando o porte fiel com os valores originais pedidos explicitamente, e o produto usa os valores novos.
2. **Recursos novos de escrita se expressam como sintaxe Markdown** no próprio `.md` (legível em qualquer editor), não como dados escondidos em `.urbe/`. Preferências de apresentação (cores, estilos) continuam na personalização.
3. **Composições saem como recurso próprio**: passam a ser páginas, sob o editor de páginas. As composições existentes são convertidas sem perda; nenhum arquivo do usuário é apagado.

## Decisões delegadas ao agente (revisáveis pelo proprietário a qualquer momento)

Tomadas por Claude Code por delegação expressa. Não são preferência do proprietário apresentada como dele; ele pode trocar qualquer uma sem nova justificativa.

| Observação | Decisão | Item |
|---|---|---|
| Eventos frequentes demais; "odiei o arco-íris" | Primeiro evento entre 1 e 2,5 min, depois um a cada 3–7 min (1.8.4: 14–26 s e 40–95 s); sem arco-íris | UC-36 |
| NPCs somem de repente | Não zerar a população ao refazer as ruas (só sai quem perdeu a casa ou a rua); não remover quem ainda está à vista | UC-36 |
| NPCs vivendo suas vidas (água, madeira, plantações) | Ofícios ligados a decorações do mundo (poço, roça, lenha): as decorações viram o cenário da vida | UC-42 |
| Ferramentas do editor/mundo dessincronizadas | O estado do botão vem de uma única fonte (a ferramenta ativa) | UC-37 |
| Código começa com a palavra "código" e ocupa a linha | Botão de código em linha (`` ` ``) separado do bloco; bloco começa vazio | UC-37 |
| Voltar do editor vai ao mundo | Voltar retorna à origem (Explorer, cidade ou nota anterior) | UC-37 |
| `[[nova nota]]` não cria pelo editor | Tocar num link para nota inexistente oferece criá-la | UC-37 |
| Barra de ferramentas longe do teclado | No celular a barra acompanha o teclado | UC-37 |
| Aba de notas disfuncional | Chama-se **Explorer**; mostra todos os arquivos com extensão e ícone; opções de pasta; arrastar para mover | UC-38 |
| Consultar referência enquanto escreve | **Fixar em painel**: o trecho selecionado fica fixo no topo; o editor rola embaixo. Só interface, sem dado novo | UC-39 |
| Tutorial no mundo | Painel dedicado (navegação, busca, texto e imagens), aberto por Configurações → Tutorial; a primeira abertura não cria mais a pasta `Tutorial/` no mundo | UC-40 |
| Mundo: contornos, muralhas, decoração, menu `+` | Menu `+` = Construção, Região, Decoração e Importar, cada um com tipo opcional; bairro da raiz com muralha; sub-bairros com contorno de baixa opacidade; contorno ligável na personalização; construção conforme a extensão (padrão `.md`); visual da construção editável; nome do bairro raiz visível no zoom máximo. Campos novos em `.urbe/mapa.json` só depois de provar que a 1.8.4 os preserva; senão, arquivo à parte | UC-41 |
| Comentários | Sintaxe `%%comentário%%` (em linha ou em bloco); visíveis e destacados no editor, omitidos em páginas/HTML exportados | UC-43 |
| Tags estilizadas (`#Todo:`) | O `.md` fica `#Tag: texto`; o estilo de cada tag é configurável (com modelos padrão) e vale para o parágrafo que começa com a tag | UC-43 |
| Modelos de nota e notas em blocos de blocos | Um conceito só, **Modelo/Componente**: modelos são notas comuns numa pasta de modelos; blocos aninhados usam contêineres Markdown `:::tipo` … `:::`; sobre o modelo de blocos das Páginas no `Urbe.Core` | UC-44 (parte do UC-35) |
| Composições | Ver decisão 3 | UC-45 |
| Relógio/cronômetro/timer | Plugin first-party da API nativa de plugins (DEC-0044), não recurso do núcleo | UC-46 |
| Urbe como sistema operacional | **Exploração, não direção.** Por último, depois do UC-35: protótipo mínimo e reversível em que cada bairro lembra o que estava aberto (notas, painéis fixados); o proprietário decide pelo uso se continua. Não muda arquitetura nem `PRODUCT-DIRECTION.md` §3 ("não troca a cidade por um workspace") | UC-47 |

**Ordem:** a ordem de ADD-0020/ROADMAP continua. UC-36 a UC-40 entram **antes do beta Android** (sem eles o beta não serve ao uso diário: o tutorial apareceria no vault novo do beta). UC-41 a UC-43 vêm depois do beta. UC-44 a UC-47 pertencem à fase de produção de artefatos (UC-35).

## O que não muda

- A 1.8.4-beta publicada continua intacta e é o app de uso diário; nada aqui altera seus arquivos ou o `.urbe/mapa.json` dela sem prova de compatibilidade.
- Sem redesenho de arte: sprites, paleta, terreno e HUD continuam os da 1.8.4. Mudanças visuais novas (muralha, decorações) usam o mesmo estilo de pixel-art.
- DEC-0042 (sem migração histórica obrigatória) e ADR-0032 (nativo sem WebView) continuam valendo.
- Validação de sensação (ritmo dos eventos, moradores, toque) é humana no aparelho (NN-017); testes automatizados não a substituem.
- PRs críticos continuam dependendo da autorização de integração do proprietário (ADD-0012/ADR-0015).

## Texto integral das observações (2026-10-13)

1. Precisei fazer uma citação em [[English Translation]] para anotar um comentário, seria interessante poder fazer de fato a opção de adicionar comentários. Nas páginas html do editor tais comentários também devem ser adaptados.
2. O código já começa com a palavra código ao clicar no atalho, e também não permite fazer um pequeno código no meio do texto corrido, ele ocupa a linha inteira.
3. No celular uma funcionalidade se torna complicada quando envolve produzir tendo outra coisa como referência [...] Podemos fazer uma funcionalidade como selecionar um texto e clicar em "Fixar em painel" então no topo da tela ficaria um painel fixo com todo o conteúdo selecionado e fora da área desse painel continua o editor.
4. Tags com estilização customizável sendo #Todo uma delas [...] cria a estilização dedicada [...] configurada pelo usuário (ou dos modelos padrão).
5. Ideia de criar templates de notas e reutilizar [...] ficha de personagem.
6. Um relógio/cronômetro/timer nativo no editor.
7. A seleção de ferramentas do editor está muito bugada [...] a visualização está desconectada do estado real.
8. A aba de notas [...] deve se chamar explorer e constar todos os tipos de arquivos cada qual com sua respectiva extensão, e se possível ícone [...] ao selecionar uma pasta não aparecem as opções, ao segurar e arrastar não acontece nada.
9. No mundo os NPCs estão sumindo de repente, e seria legal se eles fizessem outras coisas além de andar de uma casa para outra, por exemplo buscar água, cortar madeira, construir plantações.
10. Os eventos estão muito frequentes [...] E eu odiei o arco-íris.
11. No zoom máximo ajudaria muito poder ver o nome da pasta "mãe" [...].
12. A visualização das cidades no zoom não está legal [...] contorno [...] muralha [...] as estruturas estão muito feias, falta decoração [...] no símbolo "+" há duas opções para a mesma coisa [...] deve ser apenas o de posicionar uma construção, o de desenhar região e o de importar, e adicionar um de decoração [...] As construções dependem da extensão [...] ao clicar em uma construção nós podemos editar o visual dela.
13. As composições são no fundo páginas html então pode ser removido e deixado sob a tutela do próprio editor de páginas.
14. No editor de texto quando clicamos em voltar ele volta para o mundo e não para o explorador de arquivos.
15. A barra de ferramentas poderia ser mais próxima do teclado no mobile.
16. Não gostei do tutorial spawnando no mundo. Prefiro um painel dedicado com um tutorial por texto/imagens [...] com barra de navegação, pesquisa e visualização.
17. Tentei criar uma nova nota direto pelo editor [[nova nota]] mas não funciona.
18. No editor [...] construir um texto com base em blocos de blocos [...] com componentes reutilizáveis [...] não para criar sites, criar notas, notas organizadas.
19. E se o Urbe for um sistema operacional e as cidades/regiões/pastas forem áreas de trabalho? [...] adaptado à ideia central de ser uma simulação de aquário interativa.

Referências: DEC-0043, DEC-0044, DEC-0045; ADD-0020; ADR-0032; `apps/urbe/docs/csharp/ROADMAP.md`; `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`. MANIFEST NN-009, NN-011, NN-017, NN-018, NN-020.
