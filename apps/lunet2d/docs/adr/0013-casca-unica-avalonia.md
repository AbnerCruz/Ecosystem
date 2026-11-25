# ADR 0013 — Casca única do Lunet em Avalonia (`Lunet.Client`)

**Data:** 2026-11-25
**Estado:** **Aceito** — ordem do proprietário registrada em [ADD-0036](../../../../docs/governance/addenda/ADD-0036-lunet-casca-avalonia.md) / DEC-0068.
**Substitui em parte:** [ADR 0012](0012-expansao-multiplataforma.md) §1 (Android de Views como referência intocada) e §4 (Avalonia só como spike). [ADR 0003](0003-editor-baseado-em-edittext.md) passa a valer só para a casca de Views enquanto ela existir.
**Fonte:** estudo de 24/11/2026 entregue pelo proprietário; resumo e ondas em [`docs/plans/lunet-casca-avalonia-2026-11-25.md`](../plans/lunet-casca-avalonia-2026-11-25.md).

## Contexto

O motor do Lunet é sólido (Framework, Roslyn, Runtime, APK offline, Git), mas a interface foi escrita como Views Android montadas à mão: `MainActivity` parcial em 37 arquivos, 191 `AlertDialog` como navegação, telas refeitas com `RemoveAllViews`, nenhum componente reutilizável, e um desktop Avalonia separado com outra cara. O Numa já roda a mesma interface Avalonia em Android e Windows.

## Decisão

1. **`src/Lunet.Client`** (net10.0, Avalonia 12.1.3) é a única interface do Lunet. Contém o estado de navegação (`Shell/ShellState`), a sessão de jogo embutida (`Shell/GameSession`), os tokens e componentes (`Ui/`) e as telas (`Views/`). Depende só de `Lunet.Host` e das bibliotecas portáveis do Lunet.
2. **Cabeças finas:** `Lunet.Desktop` (Windows/Linux/macOS) já hospeda `LunetShell`. A cabeça Android Avalonia (`Lunet.Android.Avalonia` ou equivalente) é a próxima fatia e publica por cima de `io.lunet.studio` quando cobrir o fluxo principal.
3. **Três níveis fixos:** Home (jogos) → Workspace de um jogo (abas Código · Jogo · Arte · Mapa · Som · Notas) → painéis de apoio (Console, Problemas; IA e Docs depois) como folha inferior no celular e coluna lateral a partir de 840 dp. **Nenhum fluxo principal em diálogo modal.**
4. **Jogo embutido:** em tela larga o jogo roda ao lado do código; barra com rodar/pausar, avançar um quadro, parar e velocidade 0,25×–2×. Os quadros vêm do rasterizador portável; uma superfície GPU por plataforma entra depois, medida.
5. **Editor:** nesta fatia o texto é um `TextBox` do Avalonia, declarado como provisório. A Onda 2 o troca por uma view própria sobre `Lunet.Editor.PieceTable` (linhas virtualizadas, IME PT-BR, teclas de deslize).
6. **Família visual com o Numa sem dependência (NN-002):** `LunetTokens` copia os valores dos tokens do Numa e troca só o acento (violeta `#a594ff`, AA sobre as superfícies). Todos os pares texto/fundo passam AA (teste). Extrair `Ecosystem.Ui` exige Extraction Review (NN-022): há dois consumidores reais candidatos, mas ainda não há contrato estável.
7. **Evidência visual:** `Lunet --screens <pasta>` renderiza a casca real headless (Skia) em 412×915 e 1280×800; o CI do Lunet anexa as capturas. Não é teste no aparelho.

## Consequências

- Positivas: uma interface para desktop e Android, componentes reaproveitáveis, estado de navegação testável sem UI, testes de UI headless no CI.
- Custos: a cabeça Android Avalonia e o embutimento do jogo/editor nativos precisam ser provados (partida a frio, IME, 60 FPS) — portão da Onda 1.
- Riscos: IME do Avalonia no Android (o Numa já convive com ele); desempenho do rasterizador em CPU para jogos grandes até a superfície GPU.

## Fiscalização

`LunetClientTests` (navegação, sessão de jogo, AA, casca headless sem diálogos), `DesktopHostTests`, e as capturas do CI.
