# Linguagem visual do Ecosystem (v1.0)

**Autoridade:** normativa para a interface de todos os Products e superfícies do Ecosystem (Lunet, Urbe, Hub, portal e casca). Ordem do proprietário de 26/11/2026, registrada em [ADD-0037](../governance/addenda/ADD-0037-linguagem-visual.md) / DEC-0069. **Valores:** [`tokens.json`](tokens.json), a autoridade única (NN-001). Cada Product copia os valores para o próprio código e o teste dele confere a cópia; não há dependência entre Products (NN-002). **Estudo e fontes:** [`estudo-visual-2026-11-26.md`](estudo-visual-2026-11-26.md). **Vitrine:** a página [`site/design/`](../../site/design/index.html), publicada no portal em https://abnercruz.github.io/Ecosystem/design/, mostra cada componente funcionando. Ela lê `tokens.json` na hora, então nunca diverge.

Preferência do proprietário: **preto, azul, branco e roxo**. Esta linguagem é escura, calma e precisa: quase preto frio, texto branco suave, violeta para a ação e azul para a informação.

---

## 0. Princípios

1. **Conteúdo primeiro, cromo mínimo.** Código, jogo, nota e arte são o que importa; a moldura recua.
2. **Profundidade por tom, não por linha.** Superfícies mais altas são mais claras. A borda é exceção.
3. **Um acento manda por tela.** Uma ação principal cheia; o resto é tonal ou texto.
4. **Ritmo de 4 pt.** Todo espaço, tamanho e raio vem da escala. Espaçamento inconsistente é o que mais "barateia" uma tela.
5. **Polegar primeiro.** No celular, ações frequentes ficam na metade de baixo. Alvos de 48 dp.
6. **Movimento com propósito.** O movimento mostra de onde veio e para onde foi. Curto, sem enfeite.
7. **Acessível por construção.** Contraste WCAG AA e APCA, foco visível, rótulos sempre visíveis.

## 1. Cor

### 1.1 Escalas

Doze passos neutros ("ink") com leve matiz frio, gerados em LCH, e dois acentos. Os valores exatos estão em `tokens.json`.

| Passo | Ink | Papel |
|---|---|---|
| 1 | `#0B0B0F` | fundo do app |
| 2 | `#111116` | fundo sutil: barras, barra lateral, teclado |
| 3 | `#17171E` | superfície: campos, linhas de lista, blocos |
| 4 | `#1D1D26` | superfície elevada e hover de superfície |
| 5 | `#24242F` | sobreposição (folhas, menus) e pressionado |
| 6 | `#2C2C38` | divisória |
| 7 | `#383846` | borda (quando necessária) |
| 8 | `#4A4A5C` | borda forte |
| 9 | `#6E6E82` | ícone e texto desativado |
| 10 | `#9494A9` | texto terciário, placeholder, legenda (APCA Lc 45) |
| 11 | `#B4B4C6` | texto secundário (Lc 62) |
| 12 | `#EDEDF3` | texto principal (Lc 96) |

| Acento | soft (fundo) | 9 (sólido) | 10 (hover) | 11 (texto/ícone) | 12 (texto forte) |
|---|---|---|---|---|---|
| **Violeta**: marca e ação | `#6E56F8` a 16% | `#6E56F8` (branco 4,8:1) | `#7A64FF` | `#A594FF` (7,8:1) | `#D9D1FF` |
| **Azul**: informação, link, seleção | `#2563EB` a 16% | `#2563EB` (branco 5,2:1) | `#3B74F0` | `#7CB3FF` (9,1:1) | `#CFE2FF` |

Semânticas: verde `#4ADE80` (sucesso), âmbar `#FBBF24` (aviso), vermelho `#FF6B7A` (erro), cada uma com `soft` e `9` sólido.

### 1.2 Regras

- **Nunca preto puro na interface.** `#000` só no palco do jogo, em vídeo e imagem em tela cheia.
- **Nunca `#FFFFFF` em texto corrido.** O texto principal é o ink 12. Branco puro só sobre o sólido do acento (texto do botão principal).
- **60-30-10 no escuro:** cerca de 70–80% de ink 1–5, 15–25% de texto e no máximo 10% de acento.
- **Acento sólido (passo 9) só na ação principal** e no indicador de seleção forte. Seleção comum usa o `soft` e o texto no passo 11.
- **Cada Product tem um acento líder:** Lunet = violeta, Hub = azul, Urbe = definido no roadmap dele. O outro acento continua disponível no papel dele (no Lunet, azul é informação e link).
- **Gradiente:** só violeta → azul, em momentos de marca (abertura, estado vazio ilustrado, cabeçalho de destaque). Nunca em botão, campo ou cartão comum.
- **Cor nunca é o único sinal:** erro tem ícone e texto; seleção tem forma ou peso.

## 2. Tipografia

- **Interface:** Inter (OFL, embutida no app). **Código:** JetBrains Mono (OFL), ou a mono do sistema até ser embutida.
- **Escala** (tamanho/altura de linha, peso):

| Papel | Tamanho | Linha | Peso | Uso |
|---|---|---|---|---|
| caption | 12 | 16 | 500 | metadados, contadores, legendas |
| label | 13 | 18 | 500 | rótulo de campo, cabeçalho de seção (maiúsculas, tracking +0,04 em) |
| bodySmall | 14 | 20 | 400 | texto denso, subtítulo de linha |
| callout | 15 | 22 | 500 | botão (600), título de linha de lista (600) |
| body | 16 | 24 | 400 | texto corrido, conteúdo de campo |
| titleSmall | 17 | 24 | 600 | barra superior |
| title | 20 | 28 | 600 | título de página ou folha |
| headline | 24 | 32 | 700 | título de tela principal |
| display | 32 | 40 | 700 | abertura, estado vazio de destaque |
| code | 14 | 22 | 400 | editor (mono) |

- Tracking negativo leve a partir de 20 px (−1% a −2%); positivo só em rótulos em maiúsculas.
- **Hierarquia por peso e tamanho antes de cor.** No máximo 3 tamanhos por tela.
- Números que se comparam (tempo, FPS, contagens, tabelas) usam algarismos tabulares.
- Texto em sentence case ("Novo jogo", não "Novo Jogo"). Linhas de leitura com 60–75 caracteres no máximo.

## 3. Espaço e layout

- Grade de **4 pt**; os espaços usados são 4, 8, 12, 16, 20, 24, 32, 40, 48 e 64.
- **Margens:** 16 no celular, 24 no tablet, 32 no desktop. Conteúdo de leitura com até cerca de 760 px de largura.
- **Proximidade:** dentro de um grupo, 8–12; entre grupos, 24–32. O espaço separa antes da linha.
- **Alturas:** controle 48 (celular) ou 40 (desktop compacto), controle pequeno 32, barra superior 56, navegação inferior 64.
- **Zona do polegar:** ação principal no rodapé ou como botão flutuante; ações destrutivas longe dela.
- **Densidade:** no celular, confortável; no desktop, compacta (−8 nas alturas), com a mesma escala.

## 4. Forma, bordas e sombras

- **Raios:** xs 6 (chip pequeno, caixa de seleção), s 8, **m 10 (campo, botão)**, l 14 (cartão, bloco), xl 20 (folha, diálogo), pill.
- **Raios concêntricos:** raio interno = raio externo − espaçamento. Nunca raio interno maior que o externo.
- **Bordas:** padrão é **sem borda**. Quando o tom não separa (por exemplo, item sobre imagem, tabela densa), use 1 px `overlay.hairline` (branco a 7%) ou ink 6. Botões e campos não têm borda no estado normal.
- **Sombras:** só em elementos flutuantes (folha, menu, diálogo, botão flutuante), usando `shadow.overlay` mais um destaque de 1 px no topo (luz vindo de cima). Superfícies no plano não têm sombra.

## 5. Elevação

| Nível | Tom | O que vive aqui |
|---|---|---|
| 0 | ink 1 | fundo da tela |
| 1 | ink 2–3 | barras, campos, linhas, blocos de conteúdo |
| 2 | ink 4 | cartão em destaque, hover, menus fixos |
| 3 | ink 5 + sombra | folhas, menus, diálogos, toasts |

**No máximo 4 níveis visíveis por tela. Nunca cartão dentro de cartão.** Listas são linhas sobre o fundo (com espaço ou divisória recuada), não uma pilha de caixas.

## 6. Componentes

Todo Product implementa estes componentes uma vez e as telas só os usam (nada de cor ou tamanho na mão em tela).

### 6.1 Botões

| Tipo | Visual | Uso |
|---|---|---|
| **Principal** | sólido acento 9, texto branco 15/600 | uma por tela: a ação que conclui |
| **Tonal** | ink 4, texto ink 12 | ações secundárias |
| **Fantasma** | sem fundo, texto ink 12 (ou acento 11) | ações terciárias, em barras |
| **Destrutivo** | vermelho soft, texto vermelho 11; confirmação em vermelho 9 | apagar, sair sem salvar |
| **Ícone** | alvo 48, desenho 20–24, sem fundo; hover overlay | barras e linhas |

- Altura 48 (celular) ou 40 (desktop); raio m; padding horizontal 16 (20 com só texto); ícone de 20 à esquerda com 8 de espaço.
- Sem borda. Hover: +overlay.hover. Pressionado: +overlay.pressed e escala 0,97 por 90 ms. Desativado: 40% de opacidade (nunca o cinza do tema). Carregando: indicador no lugar do ícone, com largura fixa.

### 6.2 Campos (input, área de texto, seleção)

- **Rótulo fora e acima** (label 13/500, ink 11), sempre visível. Dica ou erro abaixo (caption 12; erro em vermelho 11 com ícone).
- **Campo:** fundo ink 3, **sem borda**, raio m, altura 48 (área de texto: mínimo 96), padding 14 × 12, texto body 16 ink 12, placeholder ink 10 (só exemplo, nunca o rótulo).
- **Hover:** fundo ink 4. **Foco:** anel de 2 px no acento 11, fundo ink 4, sem deslocamento do conteúdo. **Erro:** anel de 2 px em vermelho 11 e mensagem abaixo. **Desativado:** 40%.
- Seleção (combo) = campo + chevron de 16 à direita; a lista abre como menu (nível 3) ou como folha no celular.
- Ícones de prefixo e sufixo em ink 10 (20 px); botão de limpar aparece quando há texto.
- Busca: campo com ícone de lupa e raio pill.
- Teclado certo para cada tipo (número, URL, e-mail).

### 6.3 Seleção e ajuste

- **Interruptor:** trilho 44 × 24 em ink 6, ligado em acento 9, polegar branco de 18; transição de 150 ms.
- **Segmentado** (2–4 opções): trilho ink 3, opção ativa ink 5 com texto ink 12, raio m, altura 40.
- **Chip:** altura 32, raio pill, ink 3 com texto ink 11; selecionado = acento soft com texto acento 11.
- **Slider:** trilho de 4 px em ink 6, preenchimento em acento 9, polegar branco de 20 com sombra; rótulo e valor acima (valor em tabular).
- **Caixa de seleção e rádio:** 20 px, raio xs ou círculo, borda 1,5 px ink 8; marcado em acento 9 com ✓ branco.

### 6.4 Listas, cartões e blocos

- **Linha de lista:** altura 56–72, guia de 40 (ícone em tile ink 4 de raio s, ou miniatura), título callout 15/600, subtítulo bodySmall 14 ink 11, trailing (chevron 16 ink 10, valor ou ação). Pressionar = overlay. Divisória recuada (começa no texto) em ink 6, ou só 4 de espaço entre linhas.
- **Cartão:** só para agrupar conteúdo heterogêneo (capa de jogo, resumo); fundo ink 3, raio l, padding 16, sem borda.
- **Bloco de criação (tile):** ink 3, raio l, ícone 22 em acento 11 em cima, rótulo 13/600 embaixo; o bloco principal é sólido.
- **Cabeçalho de seção:** label 13/600 em maiúsculas, ink 10, 24 acima e 8 abaixo.

### 6.5 Barras, navegação e folhas

- **Barra superior:** 56, fundo ink 1 (ou ink 2 quando rola sob ela), título 17/600, ações-ícone à direita, voltar à esquerda.
- **Navegação inferior:** 64, fundo ink 2 com divisória superior ink 6, indicador pill 56 × 32 em acento soft atrás do ícone, rótulo 12/600 (ativo em acento 11, inativo em ink 10).
- **Folha inferior (sheet):** fundo ink 5, raio xl no topo, alça 36 × 4 em ink 8, padding 24, título 20/600, **botão de fechar visível**, scrim `overlay.scrim` que fecha. Altura máxima de 90%. Uma folha por vez. Nada de confirmação crítica em folha.
- **Diálogo** (desktop e confirmação): ink 5, raio xl, largura de até 440, sombra overlay, ações à direita (principal por último).
- **Toast/snackbar:** ink 5, raio m, acima da navegação; ação em acento 11; some em 6–8 s.
- **Menu:** ink 5, raio m, sombra overlay; itens de 44 com ícone 20.

### 6.6 Estados e feedback

| Estado | Regra |
|---|---|
| hover | + `overlay.hover` (branco a 4%) |
| pressionado | + `overlay.pressed` (branco a 8%) e escala 0,97 |
| selecionado | acento soft + texto/ícone acento 11 |
| foco (teclado) | anel de 2 px no acento 11, deslocamento de 2 (fora do controle) |
| desativado | 40% de opacidade, sem cursor de mão |
| erro | vermelho 11 em texto e ícone + anel; mensagem diz o que fazer |
| vazio | ícone de 40 em acento 11, título 20, texto ink 11, uma ação |
| carregando | esqueleto em ink 3 → ink 4 pulsando a cada 1,2 s, ou indicador no lugar da ação |

## 7. Ícones

- Um só conjunto de linha por Product: grade 24, **traço de 1,75**, pontas e junções arredondadas, sem misturar com ícones preenchidos (exceto play e estados ativos).
- Tamanhos 16, 20 (padrão em botões e linhas) e 24 (navegação). A cor segue o papel do texto ao lado.
- Ícone sozinho sempre tem nome acessível e dica (tooltip no desktop).

## 8. Movimento

| Token | ms | Uso |
|---|---|---|
| instant | 90 | pressionar, troca de cor |
| fast | 150 | hover, fade, interruptor, chip |
| base | 220 | painel, aba, expandir |
| slow | 300 | folha, troca de página |
| slower | 400 | transição grande (raro) |

- **Curvas:** padrão `cubic-bezier(0.2, 0, 0, 1)`; entrada `(0, 0, 0, 1)` (desacelera); saída `(0.3, 0, 1, 1)` a cerca de 70% da duração da entrada.
- Gestos (arrastar folha, pinça) usam mola, ou seguem o dedo e soltam com a curva de entrada.
- Animar só opacidade e transformação (deslocar, escalar). Nunca animar o layout quadro a quadro.
- Folha entra subindo 24 px + fade (300 ms); página nova entra deslizando 16 px + fade (220 ms).
- Nada em laço infinito, exceto progresso. **Respeitar "reduzir movimento":** vira fade de 90 ms.

## 9. Efeitos

- **Brilho (glow):** no máximo um por tela, e só no elemento principal (botão flutuante principal, destaque da abertura). Halo do acento a 25% com desfoque de 24.
- **Vidro e desfoque:** não no Android (custo e legibilidade). No desktop, só em sobreposição nativa, nunca sob texto corrido.
- **Gradiente:** ver §1.2. **Textura e ruído:** não.
- **Ilustração:** linhas do conjunto de ícones em escala maior, acento 11 sobre ink 1–3.

## 10. Escrita na interface

- Verbos diretos no botão ("Criar jogo", "Salvar cópia"). Sentence case. PT-BR.
- O erro diz o que aconteceu e o que fazer. Nunca "Erro desconhecido".
- Estados vazios explicam o que aparece ali e oferecem a primeira ação.

## 11. Acessibilidade e contraste

| Par | WCAG 2 | APCA (mínimo) |
|---|---|---|
| texto corrido (16) | 4,5:1 | Lc 75 |
| texto secundário | 4,5:1 | Lc 60 |
| texto terciário, placeholder, legenda | 4,5:1 | Lc 45 |
| texto grande (≥ 20/600) | 3:1 | Lc 60 |
| componentes e foco | 3:1 | — |

- Alvos de 48 × 48; tamanho de fonte do sistema respeitado; "reduzir movimento" respeitado.
- Cada Product tem um teste que verifica os pares de texto e fundo dos tokens.

## 12. Anti-padrões (o que faz parecer barato)

- Campo com a mesma cor do fundo e borda cinza; botão secundário com contorno.
- Cartão dentro de cartão; borda em toda superfície; mais de 4 tons na mesma tela.
- Acento em tudo (chips, botões, títulos) e várias ações principais competindo.
- Gradiente em botão, borda brilhante, vidro desfocado sob texto.
- Espaçamentos fora da escala; alinhamentos "quase" iguais.
- Branco puro em texto corrido; preto puro como fundo de interface.
- Controles do tema padrão (cinza do Fluent, do Material) misturados aos componentes da casa.
- Placeholder no lugar de rótulo; erro só em cor.
- Movimento lento (> 400 ms) ou em todo lugar; animação que bloqueia a próxima ação.

## 13. Revisão de tela (checklist)

1. Só componentes da casa? Nenhum controle com estilo padrão do tema?
2. Uma ação principal, na zona do polegar?
3. Espaços todos da escala de 4? Margens certas?
4. No máximo 3 tamanhos de fonte e 4 tons de superfície? Nenhum cartão dentro de cartão?
5. Rótulos visíveis, foco visível, erros com texto?
6. Contraste AA e APCA nos pares usados?
7. Estados (hover, pressionado, desativado, vazio, carregando) definidos?
8. Transições com os tokens de movimento?
9. Captura no celular e no largo anexada ao PR (capturas não são teste no aparelho; ADD-0035).

## 14. Adoção

- **Lunet:** adota a v1.0 na casca Avalonia (`Lunet.Client/Ui`): tokens, estilos globais dos controles, Inter embutida e componentes.
- **Urbe, Hub e portal:** adotam ao tocar a fundação visual de cada um, pelo roadmap próprio. Divergência nova é proibida: tela nova já segue esta linguagem.
- Mudar valores de `tokens.json` ou regras deste documento exige registro (decisão do proprietário ou ADR do Ecosystem) e versão nova (semver: valor = patch, papel novo = minor, troca de base = major).
- Extrair um pacote de UI compartilhado entre Products continua sujeito à Extraction Review (NN-022); até lá, cada Product copia e declara a versão.
