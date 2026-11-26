# Conclusão integral do Lunet — relatório vivo (26/11/2026)

**Missão:** o documento "LUNET — GOAL de continuidade, consolidação e conclusão integral" (26/11). Este é o único relatório da missão. Ele guarda o estado recebido, os erros encontrados (com causa, correção e prova) e a matriz de estado.

**Rótulos de estado:** "Código" quer dizer só integrado à `main`. "Release" quer dizer publicado. "Aparelho" quer dizer que alguém conferiu num celular. Nada aqui foi testado num celular.

## H0 — Estado recebido

| Item | Fato verificado |
|---|---|
| `main` ao assumir | `c32c498` (merge do #1053, trabalho do agente GPT: estabilizar música e reabrir WAV sem jogo) |
| `VERSION` na `main` | `0.0.1-dev.1000609` |
| Releases | 1000606, 1000607, 1000608 e 1000609 publicadas com APK arm64. A 1000609 cobre o #1053. |
| PRs abertos do Lunet | Só o #1038 ("publicar E1–E3 como dev.1000593"). Está obsoleto: o E1–E3 já está na `main` (`cd258e55`) e foi publicado em releases posteriores. Não deve ser publicado com tag velha (§2 do GOAL). |
| Trabalho do GPT | Integrado: commits `8a62d80` e `90d1108`, merge `c32c498`. Nenhum commit sem PR encontrado no remoto. Não foi feito cherry-pick redundante. |
| Branch de trabalho | `ccr-0dc592a4-9d3w98`, avançada por fast-forward até a `main`. O force push foi recusado pela política, e não foi necessário. |

## Erros encontrados e corrigidos nesta missão

| # | Erro | Causa comprovada | Correção | Prova |
|---|---|---|---|---|
| E-01 | O build dos testes quebrou com CS0121 ("a chamada é ambígua") em `CameraTests` linhas 234 e 250, depois de acrescentar `Draw(Texture2D, RectangleF, Color)` | `new(40, 50)` sem tipo explícito converte para qualquer tipo de destino. Com a nova sobrecarga, `Draw(textura, new(x, y), cor)` passou a combinar com `Vector2` e com `RectangleF`. Jogos de usuários que escrevem assim também quebrariam. | `[OverloadResolutionPriority(-1)]` na nova sobrecarga, o mesmo recurso que o `SpriteBatch` já usa. O código antigo continua resolvendo para `Vector2`. | `RectangleDrawTests.DrawTextureRectangleColor_…` (chamada `Draw(tex, new(5, 6), cor)`), mais `CameraTests` verdes. O agente de teste reproduziu o erro antes da correção. |
| E-02 | Na aba Código, o botão **Rodar** cobria as teclas da barra de código | O botão flutuante ficava a 16 dp do fundo do editor, e a barra de código também fica no fundo do editor. Com um painel aberto, quando o editor recupera o foco, a barra volta e fica embaixo do botão. | `WorkspaceView.PlaceRunButton`: o botão sobe pela altura da barra e acompanha o redimensionamento dela. | Captura `celular-9a-objetivos` (antes e depois) e `RunButtonLayoutTests.Rodar_NeverCoversTheCodeBar` (retângulos sem interseção). |

## Entregas (código na branch e depois na `main`; nada conferido num celular)

| Frente | O que mudou | Prova |
|---|---|---|
| Anexo B, parte A | `System.Drawing.Primitives` entrou na lista obrigatória do compilador do jogo, sem `System.Drawing.Common`. `Size` e `Point` funcionam com alias e não colidem com `Color` nem com `RectangleF`. | `SizeOverloadTests.TheGameCompilerSeesSizeAndPoint_…`, que usa o `GameCompiler` real |
| Anexo B, parte B | `RectangleF(Vector2, Vector2)`. Sobrecargas com `Size` em `CreateSolid`, `FromPixels`, `SetVirtualResolution` e `RenderTarget2D`, cada uma encaminhando para a escalar (mesma validação). | `RectangleDrawTests`, `SizeOverloadTests` |
| Anexo B, parte C | `Draw(RectangleF, Color)` e `Draw(Texture2D, RectangleF, Color)`. Produzem os mesmos vértices, UV, cor e lotes que `FillRect` e a sobrecarga completa, reaproveitam o pixel branco e não alocam no caminho aquecido. | `RectangleDrawTests` |
| H6 — Objetivos | Apoio **Objetivos** na IDE: mostra a missão ligada ao jogo pelo GameId, a lista de objetivos gravada no mesmo arquivo de progresso de Aprender, os critérios de aceite, a referência offline e uma pista no modo Estudo. A pista não escreve código nem envia nada sozinha. | `ObjectivesTests`, captura `9a-objetivos` |

## Pendências conhecidas (não declaradas como feitas)

- **Anexo B, parte A:** falta conferir no Preview do Android e no APK exportado, com trimming, que o `System.Drawing.Primitives` carrega no aparelho. Isso depende do CI de APK e do celular.
- **#1038:** precisa ser fechado administrativamente, com prova de que as releases posteriores já contêm o E1–E3. Fechar PR de outro agente fica a cargo do proprietário ou do integrador.
