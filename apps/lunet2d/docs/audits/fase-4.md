# Auditoria da Fase 4 — Framework Advanced

- Data: 2026-10-11.
- Responsável: ChatGPT / LUNET-436, Issue #495.
- Base auditada: `c68b9ae82a1b354351625319d60042fadea29e9f` (main); alterações locais: round-trip TileMap v1 e documentação/API.
- Release existente com os sistemas anteriores: [1000321](https://github.com/AbnerCruz/Ecosystem/releases/tag/lunet2d-v0.0.1-dev.1000321), commit `a7c1ab25180d51cf9d85e84f2a6fc5a7d07d3a21`, ancestral da base auditada.
- **Não é candidata final da fase:** novo ToJson e painel workspace do PR #493 ainda dependem de integração/release; há lacunas técnicas abaixo.

## Reconciliação em 17/10/2026 — Issue #629

Base atual consultada: `fccefa75bce650506eb8acbfee81a35df7fc4d9b`. Os trechos abaixo mantêm a auditoria de 11/10 como histórico; esta seção atualiza as pendências que mudaram desde então.

- Round-trip/jogo conjunto: aprovação explícita #500, registrada em `HO-20261011-lunet-f4-audit`. Não equivale à aprovação de requisitos ainda ausentes.
- Profiler no workspace: PR #493 integrado e DEVICE aprovado #502; não pedir novamente o aceite já registrado.
- Carga física: PR #499 integrado e roteiro 1/64 corpos aprovado #503. Não há números brutos anexados para publicar como benchmark.
- Ponte de edição TileMap v1: integrada; compatibilidade aprovada #515. A UI Tile Studio continua ausente e a aprovação de compatibilidade não cria esse editor.
- Física avançada: novos cenários executáveis no Laboratório (juntas, filtros, sensores e contatos), Issue #629; CI/APK e DEVICE específico ainda devem ser conferidos. O cenário básico e SAT/carga são preservados.
- Permanecem abertos: contador real de audio underruns; conexão UI com Tile Studio; aceite específico nine-slice; validação avançada da física e gate integral. Nenhuma fase foi adiantada nem requisito transferido silenciosamente.

## 1. Gate (§28)

Jogos 2D substanciais apenas com APIs oficiais. O Laboratório executa 12 módulos sobre o framework público. O guia `gate-framework-advanced` agora fornece um jogo completo reproduzível com mapa/colisão física/sensores/câmera/animação/partículas/UI/áudio, vitória, derrota e reinício; sua execução portátil não equivale à aprovação no aparelho. Gate integral **não aprovado**. Fase 4 permanece em andamento; Fase 5 não iniciada.

## 2. Itens do ROADMAP

A matriz reconcilia aprovações humanas canônicas, não interpreta silêncio como aprovação. A suíte integral compila e executa guias com GameCompiler/GameHost, testa arquitetura, fronteiras e alocações; Android real continua separado. Handoffs sem aprovação específica permanecem abertos. Paths abaixo são relativos à raiz do repositório.

| Item | Código presente | Testes presentes | Guia/objeto aprovado | Evidência humana canônica |
| --- | --- | --- | --- | --- |
| LUNET-404 | `ParticleEmitter.cs`, `ParticleSettings.cs`, `SpriteAnimationClip.cs` | `AnimationTests.cs`, `ParticleTests.cs` | `apps/lunet2d/docs/guides/animacao.md` | DEVICE [#458](https://github.com/AbnerCruz/Ecosystem/issues/458); `HO-20261008-lunet-animation-api.json` |
| LUNET-410 | `ProjectTemplates.cs`, `LayoutRect.cs` | `ButtonLaboratoryTests.cs`, `DemoTemplateTests.cs` | `apps/lunet2d/docs/guides/laboratorio-botoes.md` | DEVICE [#456](https://github.com/AbnerCruz/Ecosystem/issues/456); `HO-20261008-lunet-buttons-laboratory.json` |
| LUNET-416 | `Camera2D.cs` | `CameraTests.cs` | `apps/lunet2d/docs/guides/tilemaps.md` | DEVICE [#455](https://github.com/AbnerCruz/Ecosystem/issues/455); `HO-20261008-lunet-camera-culling.json` |
| LUNET-421 | `Camera2D.cs`, `LaboratoryTemplate.cs` | `CameraTests.cs`, `DemoTemplateTests.cs` | `apps/lunet2d/docs/guides/camera.md` | DEVICE [#454](https://github.com/AbnerCruz/Ecosystem/issues/454); `HO-20261008-lunet-camera-follow-bounds.json` |
| LUNET-403 | `SpriteBatch.cs`, `Camera2D.cs`, `ProjectTemplates.cs` | `CameraTests.cs`, `DemoTemplateTests.cs` | `apps/lunet2d/docs/guides/camera.md` | DEVICE [#304](https://github.com/AbnerCruz/Ecosystem/issues/304); `HO-20261008-lunet-camera.json` |
| LUNET-402 | `Geometry.cs` | `CollisionTests.cs`, `PerformanceTests.cs` | `apps/lunet2d/docs/guides/colisao.md` | DEVICE [#303](https://github.com/AbnerCruz/Ecosystem/issues/303); `HO-20261008-lunet-collision.json` |
| LUNET-420 | `GameHost.cs`, `PreviewFrameStatistics.cs`, `PreviewRenderer.cs` | `GameHostProfilingTests.cs`, `PreviewFrameStatisticsTests.cs` | `apps/lunet2d/docs/guides/profiler-preview.md` | DEVICE [#453](https://github.com/AbnerCruz/Ecosystem/issues/453); `HO-20261008-lunet-cpu-phases.json` |
| LUNET-413 | `ProjectTemplates.cs`, `DebugDraw.cs`, `DebugShapes.cs` | `ButtonLaboratoryTests.cs`, `DebugShapeTests.cs` | `apps/lunet2d/docs/guides/laboratorio-debug.md` | DEVICE [#452](https://github.com/AbnerCruz/Ecosystem/issues/452); `HO-20261008-lunet-debug-shapes.json` |
| LUNET-404 | `MainActivity.cs`, `ProjectStore.cs`, `ProjectTemplates.cs` | `AdvancedTemplateTests.cs` | `apps/lunet2d/docs/guides/animacao.md` | DEVICE [#475](https://github.com/AbnerCruz/Ecosystem/issues/475); `HO-20261008-lunet-demo-templates.json` |
| LUNET-418 | `ProjectTemplates.cs`, `LaboratoryTemplate.cs` | `LaboratoryV2Tests.cs`, `DemoTemplateTests.cs` | `apps/lunet2d/docs/guides/laboratorio.md` | DEVICE [#451](https://github.com/AbnerCruz/Ecosystem/issues/451); `HO-20261008-lunet-laboratory-v2.json` |
| LUNET-419 | `PreviewFrameStatistics.cs`, `GlesBackend.cs`, `PreviewRenderer.cs` | `PreviewFrameStatisticsTests.cs` | `apps/lunet2d/docs/guides/profiler-preview.md` | DEVICE [#450](https://github.com/AbnerCruz/Ecosystem/issues/450); `HO-20261008-lunet-live-profiler.json` |
| LUNET-405 | `ParticleEmitter.cs`, `ParticleSettings.cs`, `SpriteAnimationClip.cs` | `AnimationTests.cs`, `ParticleTests.cs` | `apps/lunet2d/docs/guides/particulas.md` | DEVICE [#449](https://github.com/AbnerCruz/Ecosystem/issues/449); `HO-20261008-lunet-particles-api.json` |
| LUNET-406 | `GridPoint.cs`, `GridPathfinder.cs` | `PathfindingTests.cs` | `apps/lunet2d/docs/guides/pathfinding.md` | DEVICE [#448](https://github.com/AbnerCruz/Ecosystem/issues/448); `HO-20261008-lunet-pathfinding.json` |
| LUNET-408 | `NineSlice.cs`, `SpriteBatch.cs` | `NineSliceTests.cs` | `apps/lunet2d/docs/guides/fontes-bitmap.md` | DEVICE [#457](https://github.com/AbnerCruz/Ecosystem/issues/457); `HO-20261008-lunet-pr328-nine-slice-reconciliation.json` |
| LUNET-409 | `ProjectTemplates.cs`, `TouchSlider.cs`, `TouchSliderStyle.cs` | `DemoTemplateTests.cs`, `TouchSliderTests.cs` | `apps/lunet2d/docs/guides/layout-ui.md` | DEVICE [#447](https://github.com/AbnerCruz/Ecosystem/issues/447); `HO-20261008-lunet-pr330-slider-reconciliation.json` |
| LUNET-412 | `ProjectTemplates.cs`, `Scene2D.cs` | `ButtonLaboratoryTests.cs`, `DemoTemplateTests.cs` | `apps/lunet2d/docs/guides/laboratorio-cenas.md` | DEVICE [#446](https://github.com/AbnerCruz/Ecosystem/issues/446); `HO-20261008-lunet-scene2d.json` |
| LUNET-415 | `TileMap.cs`, `ContentManager.cs` | `TileMapTests.cs` | `apps/lunet2d/docs/guides/tilemaps.md` | DEVICE [#445](https://github.com/AbnerCruz/Ecosystem/issues/445); `HO-20261008-lunet-tilemaps.json` |
| LUNET-411 | `ProjectTemplates.cs`, `TouchSlider.cs`, `TouchSliderStyle.cs` | `DemoTemplateTests.cs`, `TouchSliderTests.cs` | `apps/lunet2d/docs/guides/sliders-ui.md` | DEVICE [#444](https://github.com/AbnerCruz/Ecosystem/issues/444); `HO-20261008-lunet-touch-slider.json` |
| LUNET-417 | `UiTheme.cs` | `UiThemeTests.cs` | `apps/lunet2d/docs/guides/temas-ui.md` | DEVICE [#443](https://github.com/AbnerCruz/Ecosystem/issues/443); `HO-20261008-lunet-ui-themes.json` |
| LUNET-426 | `SpriteFont.cs` | `DefaultBitmapFontTests.cs` | `apps/lunet2d/docs/guides/fontes-bitmap.md` | DEVICE [#442](https://github.com/AbnerCruz/Ecosystem/issues/442); `HO-20261009-lunet-default-font.json` |
| LUNET-425 | `PreviewFrameStatistics.cs`, `PreviewHost.cs` | `PreviewFrameStatisticsTests.cs` | `apps/lunet2d/docs/guides/profiler-preview.md` | DEVICE [#440](https://github.com/AbnerCruz/Ecosystem/issues/440); `HO-20261009-lunet-frame-pacing.json` |
| LUNET-424 | `GameHost.cs`, `PreviewRenderer.cs`, `PreviewHost.cs` | `TouchFrameBufferTests.cs` | `apps/lunet2d/docs/guides/entrada.md` | DEVICE [#439](https://github.com/AbnerCruz/Ecosystem/issues/439); `HO-20261009-lunet-touch-frame-buffer.json` |
| LUNET-427 | `TouchScrollArea.cs` | `TouchScrollAreaTests.cs` | `apps/lunet2d/docs/guides/scroll-ui.md` | DEVICE [#438](https://github.com/AbnerCruz/Ecosystem/issues/438); `HO-20261009-lunet-touch-scroll.json` |
| LUNET-428 | `TouchToggle.cs` | `TouchToggleTests.cs` | `apps/lunet2d/docs/guides/toggle-ui.md` | DEVICE [#437](https://github.com/AbnerCruz/Ecosystem/issues/437); `HO-20261009-lunet-touch-toggle.json` |
| LUNET-422 | `ViewportScalingMode.cs`, `GraphicsDevice.cs`, `GameConfiguration.cs` | `FrameworkTests.cs`, `DemoTemplateTests.cs` | `apps/lunet2d/docs/guides/graficos.md` | DEVICE [#436](https://github.com/AbnerCruz/Ecosystem/issues/436); `HO-20261009-lunet-viewport-fit-fill.json` |
| LUNET-423 | `VirtualControls.cs` | `VirtualControlPrecisionTests.cs` | `apps/lunet2d/docs/guides/entrada.md` | DEVICE [#435](https://github.com/AbnerCruz/Ecosystem/issues/435); `HO-20261009-lunet-virtual-controls.json` |
| LUNET-431 | `UiStackLayout.cs` | `UiStackLayoutTests.cs` | `apps/lunet2d/docs/guides/stack-ui.md` | DEVICE [#434](https://github.com/AbnerCruz/Ecosystem/issues/434); `HO-20261010-lunet-responsive-ui.json` |
| LUNET-429 | `TouchListView.cs` | `TouchListViewTests.cs` | `apps/lunet2d/docs/guides/lista-ui.md` | DEVICE [#433](https://github.com/AbnerCruz/Ecosystem/issues/433); `HO-20261010-lunet-touch-list.json` |
| LUNET-432 | `TouchButton.cs`, `TouchSlider.cs`, `TouchScrollArea.cs` | `UiRelayoutGestureTests.cs` | `apps/lunet2d/docs/guides/stack-ui.md` | DEVICE [#432](https://github.com/AbnerCruz/Ecosystem/issues/432); `HO-20261010-lunet-touch-relayout.json` |
| LUNET-430 | `TrueTypeFont.cs`, `ContentManager.cs` | `TrueTypeFontTests.cs` | `apps/lunet2d/docs/guides/fontes-truetype.md` | DEVICE [#431](https://github.com/AbnerCruz/Ecosystem/issues/431); `HO-20261010-lunet-truetype.json` |
| LUNET-433 | `UiGridLayout.cs`, `TouchGridView.cs` | `UiGridLayoutTests.cs`, `TouchGridViewTests.cs` | `apps/lunet2d/docs/guides/grade-ui.md` | DEVICE [#474](https://github.com/AbnerCruz/Ecosystem/issues/474); `HO-20261010-lunet-ui-grid.json` |

| Requisito restante | Código/testes existentes | Estado preciso |
| --- | --- | --- |
| Física completa | `PhysicsWorld.cs`, `PhysicsWorldTests.cs`, `AdvancedPhysicsTests.cs`; ADR 0009; guia `fisica-box2d` | PRs #478/#480/#484 integrados; base e Lab aprovados #482/#488. Juntas revoluta/prismática, filtros, sensor fim/contato fim não têm aprovação DEVICE específica no handoff advanced; performance real pendente. |
| Nine-slice | `NineSlice.cs`, `NineSliceTests.cs`, guia `nine-slice`; Lab módulo Fontes e Painéis | Módulo Lab aprovado #367/#451; roteiro específico do handoff nine-slice continua pending. Não alterar essa verificação por inferência. |
| UI layout/âncoras/temas | `LayoutRect`, `UiTheme`, controles por toque, stack/grid; testes respectivos | Implementados e subitens aprovados; parent aberto até resolver roteiro específico nine-slice e regressão conjunta. Árvore/foco/Studio não são adicionados ao escopo do parent por conveniência. |
| Tilemaps completos | `TileMap`, ContentManager, A*, Camera2D; `TileMapTests` | Runtime/colisão aprovados #445/#455. `ToJson` e teste de round-trip implementados nesta mudança; conexão efetiva com Studio ausente. |
| Profiler básico | `PreviewFrameStatistics`, GameHost, GlesBackend, PreviewHost; testes de fases, memória, GC e frame pacing | Métricas reais existentes aprovadas; workspace #493 ainda aberto. **Underruns ausentes**. |
| Auditoria/gate | Este documento e roteiro abaixo | Auditoria técnica registrada, sem aprovação de fechamento. |

## 3. Requisitos do spec ainda não cobertos

1. §23 exige **audio underruns**. O backend atual SoundPool/MediaPlayer não publica o contador. A implementação atual mostra indisponibilidade; falha ao tocar som não é underrun. Para fechar, é necessário backend que exponha a medição real, com regressão de efeitos, música, pan/pitch/loop/fade e lifecycle. Não foi transferido automaticamente à Fase 14.
2. O ROADMAP exige conexão do tilemap ao formato definitivo do Tile Studio, mas o Studio é da Fase 5. Runtime v1 + round-trip são verificáveis agora; integração com um editor inexistente não é. O requisito continua aberto, sem inventar editor ou adiamento autorizado.
3. Falta validar fisicamente o conjunto avançado de juntas/filtros/contatos, medir desempenho da física e executar no aparelho o jogo conjunto do guia `gate-framework-advanced` para o gate.
4. O painel workspace e seu caminho rápido/isolado precisam de DEVICE após o PR #493 e release.

GPU time e VRAM total são explicitamente indisponíveis, mas não entram como novos requisitos de fechamento: §23 pede render time e asset memory, já medidos como CPU Draw e armazenamento estimado RGBA8 devidamente rotulado.

## 4. Princípios (§2)

- [x] Framework e assets operam offline; ToJson não usa rede.
- [x] Nenhuma conta, IA ou telemetria obrigatória adicionada.
- [x] Projetos continuam arquivos comuns; exportação retorna JSON sem I/O.
- [x] Tipos Box2D internos não aparecem na API pública; teste de arquitetura preservado.
- [x] Laboratório usa APIs públicas; novos Studios/plugins continuam nas suas fases, sem simulação de implementação.

## 5. Qualidade (§30)

- [x] Nenhum TODO/NotImplemented crítico encontrado no Framework/Runtime na busca desta auditoria; lacunas funcionais descritas acima.
- [x] Parser/exportador preservam validação, formato v1 e immutabilidade do mapa.
- [x] Novas APIs têm XML, catálogo offline gerado e guia; testes de round-trip/Unicode/cultura/colisão/A*, ownership de atlas e jogo conjunto.
- [x] Sem novo painel decorativo ou controle sem comportamento nesta mudança.
- [ ] Qualidade integral Android (exceções/lifecycle/driver) ainda depende do roteiro no aparelho; testes portáteis não a certificam.

## 6. Arquitetura

- [x] `ArchitectureTests` e catálogo API na suíte integral.
- [x] ADRs 0008 (TTF) e 0009 (Box2D) presentes; ToJson usa o contrato v1 existente, sem formato novo nem dependência extra.
- [x] Mudança restrita a Lunet e registros de handoff; nenhuma alteração em outros produtos ou no integrador.

## 7. Regressão

- [x] Build .NET 10: 0 warnings, 0 errors.
- [x] 661/661 testes, 0 failures/errors/skips (656 da base + 2 round-trip + 2 ownership de fonte + 1 jogo conjunto); catálogo API regenerado e conferido.
- [x] 22/22 checks de consistency nesta revisão; ROADMAP × Issues raiz não verificado localmente sem snapshot (CI verifica).
- [x] APK anterior 1000321 publicado pelo CI, com os sistemas auditados anteriores.
- [ ] APK desta revisão: aguarda PR/CI/integração, não confundir com 1000321.
- [ ] Instalação e regressão no aparelho desta revisão ainda não executadas.

## 8. Documentação

ROADMAP reconciliado com aprovações, CHANGELOG e AgentsChat atualizados, guia tilemaps e catálogo API atualizados. README continua apontando para as autoridades; não duplica estado de fase.

## 9. Riscos (§32)

| Risco prioritário | Evidência / limite |
| --- | --- |
| Roslyn no Android | Gate IDE aprovado #204; nova revisão exige regressão Run/diagnósticos. |
| Preview executa compilação | Fases 1–3 aprovadas; Laboratório integrado e testes reais GameHost. |
| Renderer funcional/performance | Fases anteriores e Lab aprovados; profiler real disponível. Sem números de aparelho inventados; física avançada ainda precisa de medida. |
| APK do jogo | Continua no escopo da Fase 9; APK da IDE no CI não comprova empacotamento offline de jogos. |

## 10. Pendências herdadas e próxima fase

Nenhuma lacuna foi movida de fase nesta auditoria. Permanecem os bloqueios da seção 3, roteiro nine-slice e regressão conjunta. Só iniciar Fase 5 após resolução formal e validação integral da candidata.

## 11. Roteiro e validação do usuário

Roteiro consolidado em [fase-4-roteiro.md](fase-4-roteiro.md), com pré-condições explícitas. Ainda não foi enviado como candidata final: faltam requisitos e APK exato desta revisão. Aprovações antigas permanecem nas respectivas Issues/handoffs; não se pede repetir todos os subitens para reconstituir registros já existentes.

- Gate da fase: **aguardando implementação completa e depois validação explícita**.
- Aparelho/Android/performance: não informados para a nova candidata.
- Resposta da nova revisão: não recebida, não inferida.

## Decisão

**Fase não concluída.** A auditoria elimina pendências administrativas falsas e delimita bloqueios técnicos reais; não transforma validações parciais em aprovação integral.
