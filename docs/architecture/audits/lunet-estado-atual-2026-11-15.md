# Lunet2D — auditoria incremental e orientação de desenvolvimento (15/11/2026)

**Autoridade única:** `AbnerCruz/Ecosystem`, `apps/lunet2d`, base `5e2a8fa0bd80836a7938a52f5d630c0fe9a41285`. **Estado:** estudo de código, documentação, histórico de CI e releases; nenhuma alteração funcional. **Limitação:** não executei dotnet nem APK num aparelho físico nesta sessão. Diferenciar sempre FATO no código, teste automatizado, DEVICE e proposta. O estudo anterior `docs/architecture/audits/lunet-diagnostico-geral-2026-11-14.md` é uma fotografia de outra revisão e suas lacunas de IA/Studios podem já ter sido implementadas.

## Quadro atual

- **Framework:** loop fixo, `Game` com `Initialize/LoadContent/Update/Draw/UnloadContent`, renderização, câmera, sprites, fontes, animação, áudio, armazenamento, TileMap/A*, física Box2D, UI e networking. Código portátil em `src/Lunet.Framework`; Roslyn e cache incremental em `src/Lunet.Compiler/GameCompiler.cs`; runtime desacoplado da UI. **Não reescrever** Framework/Compiler/Runtime para resolver problemas de interface.
- **IDE Android:** editor C# e serviços de análise/refatoração, Git, Inspector, docs offline, Preview, projetos/ZIP, logs e comando de compilação existem. `MainActivity.*` ainda concentra bastante navegação/estado. UX mobile, teclado, foco, performance e acessibilidade precisam de teste visual/DEVICE, não só xUnit.
- **Studios:** Sprite ganhou seleção (retângulo/laço/varinha), zoom/pan/transformações e frames; Tile ganhou autotile 16/47; Audio/Music tem composição, síntese, efeitos e WAV; Palette, Particle, Data e Assets têm implementação. Fase 5 ainda aberta: Sprite com camadas e simetria pendentes, integração Tile/UI/Animation/Atlas/Physics incompleta, validações no aparelho pendentes. O valor está em transformar Studios isolados num fluxo contínuo sobre arquivos reais.
- **IA:** modos Auto/Ensinar/Fazer, Keystore, mapa de projeto, leitura de código/linhas reais, edição por trecho, SHA, `LunetAiWriteGuard`, histórico em disco, checkpoints persistidos, cartão de alterações, serviço Android foreground e compilação existem. Arquivos de evidência: `src/Lunet.Ai/LunetAiEditSafety.cs`, `LunetAiCowork.cs`, `LunetAiSession.cs`, `src/Lunet.Android/AiRunCoordinator.cs`, `LunetAiForegroundService.cs`, `MainActivity.Ai.cs`. Testes próprios existem, **DEVICE pendente**.
- **APK do usuário e jogo:** pipeline canônico `.github/workflows/lunet2d-release.yml` gera e valida `dist/template/game-runtime.apk`, incorporado ao APK da IDE. `MainActivity.GameBuild.cs` compila o projeto com Roslyn e empacota o APK standalone offline. Release canônica verificada: `lunet2d-v0.0.1-dev.1000575`, publicada no Ecosystem em 14/11. Não inferir o estado de uma build posterior sem consultar artefatos reais.
- **Multiplayer:** UDP, host/client, servidor dedicado headless, simulação de perda, replicação/predição e multiplayer local até quatro jogadores; falta validar dois aparelhos, reconexão e uso prolongado real.
- **Desktop/web/iOS:** host experimental Avalonia + `SoftwareGraphicsBackend` implementado para desktop. Spike reportou 2.000 sprites 16×16 em 4 ms (mediana) no contêiner Linux CPU, **não** prova produto desktop completo; shaders customizados não são executados por esse rasterizador. Web e iOS seguem fases futuras.

## Evidências dos testes e limites

- No histórico do código sincronizado, CI `43750461500` em 15/11 aprovou **1.063/1.063** testes .NET. O job Android desse **pipeline legado** falhou com `XA2001` ao buscar `../../../../docs/contracts/bindings/android-binder-v1.trust.json`: essa dependência existe no monorepo, fora do antigo repositório. **Não tratar como falha de build oficial; não corrigir o espelho.** Rodar o CI canônico do Ecosystem para qualquer nova alteração.
- `ProjectJourneyTests` cria, edita, compila e executa projeto via classes de domínio e exporta ZIP, mas não opera visualmente a interface Android. Fase 4 e 5 seguem abertas no ROADMAP, fases 6–15 sem gates finais concluídos.

## Riscos que exigem testes dirigidos

1. **Desfazer IA sobrescreve mudanças posteriores:** `LunetAiCheckpoint.Revert` restaura bytes de antes do pedido; `MainActivity.Ai.cs` avisa que edições subsequentes nesses arquivos serão perdidas. Evoluir para merge/diff com versões e resolução de conflito. Testar cancelamento, reinício, edição concorrente e backup sem perda posterior.
2. **WriteGuard é heurístico:** protege gravações vazias, marcadores `... resto do código` e reduções abaixo de 60% em arquivos com >=15 linhas não vazias. Substituição explícita ainda pode remover trechos; teste semântico e diff são essenciais.
3. **IA em segundo plano não reinicia após force-stop:** `LunetAiForegroundService` retorna `NotSticky`; histórico/checkpoint sobrevive, mas a tarefa não se retoma. UI deve distinguir interrompido, cancelado e completo.
4. **Ergonomia e robustez:** build e xUnit não certificam IME, scroll, acessibilidade, memória, GL context loss, rotação nem qualidade do conteúdo visual. Testes no Android físico e screenshots obrigatórios em mudanças de UI.
5. **Fluxo dos Studios:** assets, animação, Preview e código podem divergir na experiência. Exigir testes end-to-end com a mesma fonte do projeto, não só modelos isolados.

## Próximas ações por prioridade

**P0 — Entrega oficial:** verificar a main, PRs e CI de `lunet2d-ci.yml` e `lunet2d-release.yml`, integridade do template embutido, certificado e versionCode. Validar no Android 13: criar projeto vazio (com esqueleto Game intacto) → editar C# → produzir sprite/tilemap/áudio → Preview → gerar APK offline → instalar/abrir jogo → reabrir projeto. Registrar screenshots/logs e Passou/Falhou por etapa.

**P0 — Integridade da IA:** testar leitura real, edições mínimas, cancelamento, diff, checkpoint reaberto, edição posterior pelo usuário, Revert sem perda e tarefas fora do chat. Implementar proteção de revisões posteriores antes de proclamar recuperação segura.

**P1 — Coesão dos Studios e UX:** integrar layers/timeline/Tile/Animation/UI/Physics e navegação mobile; medir performance, memória, teclado e orientação; fechar F5 apenas com evidência DEVICE.

**P2 — Rede e portabilidade:** multiplayer em dois Android reais sob interrupção/reconexão; completar desktop com publicação real antes de apostar em web/iOS.

## Solicitação posterior do proprietário — Home, Studios e Explorar

O estudo específico de 15/11 está em [`lunet-home-studios-explorar-2026-11-15.md`](lunet-home-studios-explorar-2026-11-15.md), com diagnóstico do acoplamento `MainActivity.ShowProjects`/`MainActivity.Studio`, pesquisa externa, arquitetura de biblioteca reutilizável e catálogo de referências offline. Itens propostos LUNET-510, 511 e 512 no ROADMAP; não declarar implementação antes de código, CI e DEVICE. Trata-se de uma direção paralela à necessidade P0 de estabilidade e integridade da IA.

## Protocolo para o próximo agente

- **NUNCA trabalhar no espelho**. Só `AbnerCruz/Ecosystem`; DEC-0064/ADD-0035/ADR-0050 prevalecem sobre o plano histórico de importação. O repositório externo pode permanecer arquivado até decisão administrativa; não é um fluxo de desenvolvimento.
- Ler `MANIFEST.md`, `AGENTS.md`, `apps/lunet2d/AGENTS.md`, SPEC, ROADMAP, ADD-0034, ADD-0035 e handoffs. Conferir main, branches, PRs e Issues antes de escolher incremento.
- Código do jogo é a autoridade, Studios trabalham sobre os mesmos arquivos e IA precisa de checkpoints seguros. Preservar módulos funcionais e não criar abstrações sem consumidor.
- PR integrado só com checks do estado combinado; governança reservada ao proprietário. Estágio de versão só muda por decisão do usuário. CI ≠ DEVICE; liberar uma nova versão não é atestado de aprovação física.

**Fontes:** `apps/lunet2d/ROADMAP.md`, `CHANGELOG.md`, `docs/guides/ia-lunet.md`, `docs/architecture/spike-avalonia-desktop-2026-11-14.md` (relativos ao Product), `.github/workflows/lunet2d-ci.yml` e `lunet2d-release.yml` da raiz.
