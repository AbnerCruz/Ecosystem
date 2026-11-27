# Lunet — Plano de evolução do Framework após F8 (E1–E3)

**Decisão de desenvolvimento:** 24/11/2026. **Estado:** aprovado para execução após F8; nenhuma implementação concluída por este documento. **Canônico:** AbnerCruz/Ecosystem, apps/lunet2d. **Objetivo:** entregar ordenação de renderização, imports globais visíveis e acesso contextual opcional em um ciclo curto, sem reforma geral. A ordem é E1 (capacidade), E2 (ergonomia de projeto), E3 (ergonomia de execução). E1 e E2 são independentes e podem ter PRs pequenos em paralelo após F8; E3 não depende dos imports para funcionar.

## Registro de início — 24/11/2026

Base consultada: `9b26d2988b54ff6f8d8ded03f054c28a6bcddb00`. O pré-requisito pós-F8 ainda não foi atendido: [F7 #1033](https://github.com/AbnerCruz/Ecosystem/pull/1033) e [F8 #1035](https://github.com/AbnerCruz/Ecosystem/pull/1035) estão abertas. Handoffs de reconciliação registrados nas respectivas branches. No head anterior da F8, testes e build APK passaram; screenshots falharam no download da imagem Android (Premature EOF) e CHK-ARCH-REFS sinalizou a menção textual a outro produto neste plano. A redação abaixo foi corrigida sem mudar o escopo. Não há baseline pós-F8 executado nem implementação E1–E3 neste registro. Todos os checkboxes de implementação e validação permanecem abertos até evidência correspondente.

## 0. Regras de execução e fontes

- [ ] Sincronizar com main depois do merge de F8; ler o plano F1–F8 em docs/plans/lunet-plano-desenvolvimento-2026-11-23.md e os contratos atuais. Registrar SHA de início; os estudos foram feitos em 952659e8e7f08a3a8ebdd8d60186697aef6c7b5a (F4, PR #1028), não presumir que a árvore atual é idêntica.
- [ ] Ler os quatro estudos: [Lunet-estudo-importacoes-globais.md](../research/framework-evolution-2026-11-24/Lunet-estudo-importacoes-globais.md), [Lunet-estudo-dependencias-contextuais.md](../research/framework-evolution-2026-11-24/Lunet-estudo-dependencias-contextuais.md), [Lunet-estudo-ordenacao-renderizacao.md](../research/framework-evolution-2026-11-24/Lunet-estudo-ordenacao-renderizacao.md), [Lunet-consolidacao-arquitetural.md](../research/framework-evolution-2026-11-24/Lunet-consolidacao-arquitetural.md). Este plano incorpora suas decisões operacionais e prevalece sobre hipóteses de API nos estudos; não reabrir pesquisa geral nem repetir F1–F8. Referências e caminhos abaixo permitem revalidar mudanças pontuais.
- [ ] Usar TDD, diffs focados, sem dependências novas, sem alterações em outros produtos ou no Studio ou reestruturação de editor/engine. Seguir MANIFEST, protocolo de estudo e ADD-0035. Preservar API explícita, builds existentes e código do usuário; sem migração silenciosa.
- [ ] Registrar baseline de testes Debug/Release, diagnóstico e alocação em main antes de modificar. Estudos relataram 1179/1179 Debug e 132/133 no recorte gráfico Release, com falha preexistente de alocação toque/gestos; reproduzir, distinguir de regressão e não maquiar números.
- [ ] Marcar checkbox somente com evidência (teste/arquivo/commit; execução não realizada fica pendente ou bloqueada com motivo). Atualizar este documento em cada PR. Usar CI e testes específicos por fase; suíte completa no gate final. Não criar oito novas fases nem novos sistemas para problemas fora deste escopo.

## Registro de execução candidata — PR #1037

Base da branch de trabalho: F8 reconciliada `0a7d1b0871d825c3d8c306882eb604337996121a`, contendo F7 e a correção documental da main `720586aba966930c8edf8753009559c7d7306a97`. A integração F7/F8 ainda não estava confirmada quando a implementação candidata foi preparada; não autoriza merge antecipado.

- [x] Código candidato E1: `2200cb64`, refinado em `1544136d`: fila de quads completos e descritores de ordem, desempate ordinal, guardas, duas sobrecargas com chave, cancelamento e limpeza.
- [x] Código candidato E2: `46ae6c56`: fonte real, Explorer, presets, metadado SDK e workflow cloud. Essencial é provisório; Amplo continua experimental até a matriz de referências Android.
- [x] Código candidato E3: `44407feb`, compatibilidade de tipos Android `66ef4e84` e `bafee571`: fachada ThreadStatic, geração da sessão, escopos síncronos e restrição gráfica de fases.
- [x] Testes escritos: SortedBatchTests, ContextTests, ProjectImportsTests, FrameworkEvolutionSampleTests, atualização de jornadas/templates; exemplos em samples/framework-evolution-*.
- [ ] Baseline real Debug/Release pós-F8, CI da revisão E1–E3, testes completos dos consumidores, desempenho, paridade de SDK e Android, screenshots de imports, referência gerada e release integrada.

Os checkboxes acima distinguem código candidato de validação. Os checkboxes normativos E1–E3 e do gate final abaixo permanecem abertos onde o critério integral ainda não foi comprovado. O SDK .NET, adb e acesso Git direto não estão disponíveis no ambiente de trabalho. Não foram executados testes localmente. A restrição a Graphics/Content em Pause/Resume/UnloadContent é deliberadamente conservadora; nenhum teste em EGL físico foi feito. A referência pública foi descrita no guia; a regeneração mecânica da referência depende do SDK.

## E1 — Ordenação nativa de renderização (primeira prioridade)

### Contrato decidido

SpriteBatch existente continua com Begin/Draw/End e envio por textura/capacidade, sem alteração de assinaturas, timing, exceções ou pixels. Novo caminho opt-in: BeginSorted(...), ordem ascendente por padrão (menor valor desenhado antes), opção descendente (maior primeiro); empates sempre na sequência de submissão. Chave float finita, qualquer intervalo, negativa permitida, -0/+0 iguais, rejeitar NaN e infinitos antes de enfileirar. Sem depth test GPU. Nomes propostos: SpriteSortDirection.Ascending/Descending, BeginSorted para as variantes normal, câmera, câmera+região e material; parâmetros de estado iguais aos Begin correspondentes. Não trocar Begin atual por novo overload com parâmetro opcional.

API mínima: SetOrder(float order) no lote ordenado aberto (chave corrente inicial 0, não muda estado gráfico, válida para chamadas Draw/DrawString e emissores compostos subsequentes; reset a cada BeginSorted). Overloads Draw(texture, position, color, float order) e Draw(texture, destination, source, color, rotation, origin, float order) com chave explícita por operação, sem alterar chave corrente. Demais overloads existentes, atlas, sprite, texto, nine-slice, debug, partículas e tilemap herdam a chave corrente através do emissor único de quads. Não criar uma sobrecarga para cada método apenas por simetria. CancelSorted() abandona comandos pendentes e fecha o lote, sem envio; End fecha e limpa mesmo em exceção no modo ordenado; não promete rollback de pixels já enviados. Nenhuma mudança de estado de passagem durante lote ordenado. Preservar o caminho legado.

```csharp
batch.BeginSorted(camera);
batch.Draw(playerTexture, playerFeet - playerPivot, Color.White, order: playerFeet.Y);
batch.Draw(treeTexture, treeFeet - treePivot, Color.White, order: treeFeet.Y);
batch.End();
```

Y-sort usa âncora do chão escolhida pelo jogo, não canto da textura, glifo ou câmera. Camadas distintas podem ser passagens ou chaves calculadas pelo jogo; grupos recebem mesma chave e mantêm ordem interna consecutiva. Documentar lista reutilizada com chave lexicográfica (layer, feet.Y, ordinal) para casos complexos. Não criar ECS, render graph, fila de cena obrigatória, comparador arbitrário, sort por textura/material, Immediate ou Deferred adicional neste ciclo. Ordenação estável por chave resolve o requisito urgente.

### Implementação e testes

- [ ] E1.1 — Auditar SpriteBatch.cs, SpriteVertex.cs, GraphicsDevice.cs, RenderState, Shader.cs, Texture2D.cs, RenderTarget2D.cs, IGraphicsBackend e GlesBackend/SoftwareGraphicsBackend/HeadlessGraphicsBackend; localizar consumidores e testes existentes. Criar backend gravador (se existente, estender fixture) com textura, vértices, estado, target, matriz, uniform e sequência.
- [ ] E1.2 — Adicionar API BeginSorted/SetOrder/Draw com chave/CancelSorted mantendo todas as assinaturas antigas e comportamento de Begin. Testar chamadas com null/default, argumentos nomeados, method groups e API binária antiga (fixture assembly compilado antes).
- [ ] E1.3 — Reutilizar cálculo de vértices do emissor atual. Em sorted, guardar quads completos capturados em Draw (valores, textura, chave e ordinal); arrays/listas reutilizáveis com crescimento amortizado, ordenar índices/descritores por chave e ordinal sem capturas por quad. Não enviar no Draw mesmo com mudança de textura ou >2048 quads. Ordenar todo lote em End, depois alimentar o buffer físico existente por trechos contíguos de mesma textura, sem reagrupar empates.
- [ ] E1.4 — Proteger integridade da passagem: usar guarda por dispositivo ou revisões monotônicas de estado para rejeitar Clear, mudança de target/projeção/viewport e mutação de uniforms do shader ativo durante a passagem sorted, inclusive A→B→A. Validar origem e vida de textura/shader/target antes de emitir; não usar handle como identidade. Definir exclusividade de lote sorted no dispositivo, rejeitar outra passagem concorrente, inclusive legado já aberto; tracking mínimo de Begin/End sem alterar permissões entre lotes legados. Não aumentar custo por quad no modo antigo. Estado/recursos devem permanecer válidos até End; mutação incompatível deve falhar explicitamente, não enviar no alvo errado.
- [ ] E1.5 — End sorted usa try/finally para liberar guarda, limpar contadores e referências usadas, inclusive backend falho; CancelSorted limpa sem emitir; falha depois de envio parcial é diagnosticável. Testar reuso do lote, target descartado, recurso de outro dispositivo com handle igual, shader descartado, uniform alterado, câmera capturada em Begin e exceção antes/depois do upload. Não prometer recuperação de contexto EGL já perdido.
- [x] E1.6 — *(26/11: evidência em `SortedBatchTests` — crescente e decrescente, empates, 2047/2048/2049/4097, NaN e +∞ — e `SortedBatchEdgeTests` — ±0, −∞, A/B/A, textura consecutiva num lote só, atalho de retângulo no modo ordenado; fronteira do modo antigo em `CameraTests`, 2049 quads.)* Testes de sequência: A/B/A, empates multi-textura, asc/desc, negativos, ±0, NaN, infinito; chaves mínimas chegando por último; 2047/2048/2049/4097 quads; nenhuma chamada antes de End sorted; modo antigo ainda envia nas mesmas fronteiras; textura repetida consecutiva agrupa, alternada não muda ordem.
- [x] E1.7 — *(27/11: `SortedBatchConsumerTests` prova vértices idênticos entre o modo antigo e o ordenado com chave única para textura recortada, atlas, Sprite, fonte/glifos, nine-slice, DebugDraw, partículas, tilemap e Scene2D.)* Testes consumidores: textura recortada, atlas, sprite, fontes/glifos, nine-slice, DebugDraw, partículas, tilemap e Scene2D. Validar posição/UV/rotação/tint e chave composta via SetOrder; não reordenar callbacks de cena. Câmeras, split screen, materiais e FBO como passagens separadas.
- [ ] E1.8 — Teste de pixels no software: personagem e árvore atrás/na frente; três sprites semitransparentes intercalados, empates e alvos opacos. Estudos mediram vermelho–azul–vermelho RGBA 160,0,64,255 versus vermelho–vermelho–azul 96,0,128,255; reproduzir com fixture equivalente, não usar headless como oráculo de pixels. Comparar no mesmo backend; registrar divergência de alfa GLES/CPU sem ampliar escopo para reforma de blending.
- [ ] E1.9 — Medir Release após aquecimento em cenas já ordenadas, inversas, aleatórias com seed, empates e multi-textura; pelo menos 100, 2049 e 10000 quads. Registrar alocação/GC, retenção após pico, CPU Draw/End, chamadas e bytes. Legado sem nova alocação recorrente atribuível à feature; sorted reutiliza buffers e limpa referências. Sem limite arbitrário que descarte sprites; crescimento falha explicitamente se recurso esgotado. Hardware indisponível não autoriza afirmar desempenho Android.
- [ ] E1.10 — Incluir exemplo jogável top-down de pé/tronco e duas passagens (mundo/HUD), demonstrando submissão fora da ordem visual, mais guia curto e referência pública de API. Testes Debug/Release e CI da fase. Merge somente com gate verde; não bloquear E2 por preferência estética.

## E2 — Importações globais reais e Explorer organizado

### Contrato decidido

Fonte única e visível: Project/Imports.cs, arquivo C# comum na pasta física do jogo, acessível no Explorer pelo grupo recolhível “Projeto” → “Importações” (mostra caminho real e abre editor padrão; sem duplicar entradas ou ocultar da busca/ZIP). Sem arquivo solto na raiz, dezenas de linhas em Game.cs, injeção invisível, namespace artificial ou nova linguagem/configuração paralela. Nenhum painel complexo bidirecional nesta entrega: edição de fonte e dois presets explícitos com preview/diff; nunca sobrescrever edição manual silenciosamente.

Oferecer perfil Amplo (preferência do proprietário) como padrão de novos projetos **se** a matriz de referências e bindings passar, e perfil Essencial como alternativa. Amplo inclui todos os 24 namespaces válidos auditados:

```csharp
global using System;
global using System.Numerics;
global using System.Collections.Generic;
global using System.Linq;
global using System.Text;
global using System.Text.Json;
global using System.IO;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Diagnostics;
global using System.Reflection;
global using System.Collections.Concurrent;
global using System.Text.RegularExpressions;
global using Lunet;
global using Lunet.Graphics;
global using Lunet.Input;
global using Lunet.Content;
global using Lunet.Audio;
global using Lunet.Physics;
global using Lunet.Scenes;
global using Lunet.UI;
global using Lunet.Pathfinding;
global using Lunet.Storage;
global using Lunet.Networking;
```

Essencial: System, System.Numerics, System.Collections.Generic, Lunet, Lunet.Graphics, Lunet.Input, Lunet.Content, Lunet.Audio. Os dois outros nomes solicitados, Lunet.Multiplayer e Lunet.Inspection, não existem como namespaces de jogo no snapshot; tipos públicos já estão em Lunet (SplitScreen, TurnOrder, atributos de inspeção). Não criar duplicatas; se F8 tiver alterado os nomes, auditar antes de incluir. Não importar Lunet.Runtime.Inspection. Colisões conhecidas: Lunet.Input.Pointer/System.Reflection.Pointer, Diagnostics.ThreadState/Threading.ThreadState, extensão LINQ e tipos de terceiros. Documentar qualificação e aliases locais/globais; não impor alias que troque binding de código existente. Nos templates novos resolver colisões de forma explícita. Se Amplo falhar em rota suportada, corrigir paridade; se tecnicamente impossível, manter Essencial como default e Amplo opt-in somente para referências realmente presentes, com diagnóstico e bloqueio documentados (não declarar cumprimento dos 24 sem teste).

### Implementação e testes

- [ ] E2.1 — Auditar ProjectStore, ProjectTemplates, LearningTrackOne/CreateLearningProject, LunetProject.ListFiles/LoadSources/ExportZip, CodeAnalyzer, GameCompiler, LoadedAssembliesReferenceProvider, MainActivity e build offline, runtime-template/Lunet.GameCode.csproj, Directory.Build.props, GitHubGameBuildWorkflow, WorkspaceSession e Lunet.Server. Verificar namespaces públicos nas referências de cada host, não em pastas.
- [ ] E2.2 — Gerar Project/Imports.cs em todos os novos templates/cursos; remover apenas diretivas locais redundantes de código gerado (evitar CS8933 e warnings-as-errors). Preservar imports locais não cobertos e todos os projetos antigos. Blank continua vazio visualmente; Game.cs não recebe bloco global. Opção explícita para adotar preset em projeto antigo, com diff e aviso de ambiguidades.
- [ ] E2.3 — Resolver SDK implícito vs Roslyn: para novos projetos adotar modo explícito versionado em metadados existentes de projeto (lunet.json, apenas política de SDK, não lista de imports), desabilitando ImplicitUsings no build de GameCode para esse modo; workflow e build externo documentado passam a mesma política, inclusive se Imports.cs for apagado. Manter política legada para projetos antigos; não alterar Directory.Build.props de outros projetos. Não inserir fontes sintéticas em Roslyn. Se o esquema atual já tiver mecanismo equivalente, reutilizá-lo. Garantir inventário idêntico (exclusões .lunet/bin/obj/Tests e subpastas relevantes) sem tocar fontes de usuário.
- [ ] E2.4 — Explorer Android: grupo Projeto recolhível por padrão, Importações abre arquivo real e identifica Project/Imports.cs. Reutilizar editor, menus, estado de recolhimento, busca, copiar caminho, salvar/undo/redo, renomear/excluir e atualizar análise semântica. Em árvore física normal e ZIP permanece arquivo comum; nenhuma duplicação/ocultação de fontes. Se removido, não recriar automaticamente; ação explícita recria preset. Presets Amplo/Essencial mostram diff e requerem aceitação antes de substituir texto customizado; sem parser/reformatador especial.
- [ ] E2.5 — Testes Roslyn e CodeAnalyzer com duas fontes: alias, nome local, extensão LINQ, colisão Pointer/ThreadState/Vector2, namespace ausente, import duplicado, edição/remoção/renomeio, #if e cache frio/quente. Comparar diagnósticos e identidade de símbolo nos caminhos equivalentes; erros esperados não são regressões. Mostrar origem qualificada do tipo usando mecanismos já existentes.
- [ ] E2.6 — Compilar templates/cursos/samples com ambos presets; validar projeto antigo sem alterações, preview rápido/isolado, ZIP round-trip (nomes/bytes), APK offline, SDK/cloud, desktop e servidor --check. Confirmar que apagar imports remove nomes no modo explícito também no SDK; nenhuma duplicação SDK. Validar autocomplete, diagnósticos em arquivo irmão, editor e runtime. Registrar separadamente ambiente não disponível.
- [ ] E2.7 — Documentar origem System.Numerics, namespace vs assembly, global using, como ativar/remover, aliases/qualificação, imports opcionais por domínio, política de build e exportação externa. Screenshot do Explorer recolhido/expandido e editor de imports no Android/emulador. CI verde e merge.

## E3 — Dependências contextuais opcionais (pequena fachada experimental)

### Contrato decidido

Criar Lunet.Context, fachada estática somente leitura que resolve o Game corrente de um callback síncrono de host, sem armazenar instâncias globais permanentes. Acesso explícito Context.Input ou escolha do usuário: using static Lunet.Context; (por arquivo) ou global using static em Imports.cs (opt-in, não default). Manter Game e todas as APIs explícitas. Propriedades iniciais Input, Content, Graphics, Audio, Save, Services, Timers, Log, Dispatcher, delegando aos objetos reais do Game, nunca a registro substituível para serviços centrais. Sem GameContext/UpdateContext/RenderContext públicos, sem DI container, sem injeção/reflexão e sem batch/câmera/scene/PhysicsWorld globais. SpriteBatch contextual adiado; Draw(batch) permanece. Sem construtor SpriteBatch() e factories sem device nesta primeira entrega (podem ser avaliados depois, não são necessários para a fachada). Serviços do usuário permanecem customizáveis por interfaces em GameServices; substituição não troca dispositivo ou conteúdo já anexados.

```csharp
using static Lunet.Context;
// Classe independente, dentro de callback válido:
var texture = Content.LoadTexture("player.png");
if (Input.IsKeyDown(Keys.Right)) { /* mover */ }
// Renderização: batch.Draw(texture, position, Color.White);
```

Escopo físico síncrono por thread (ThreadStatic com save/restore LIFO em try/finally, sem AsyncLocal/ExecutionContext), instalado internamente pelo host somente durante callbacks autorizados. Nunca expor setter público ou propagação automática para tarefas. Start após Attach, Initialize, LoadContent, Update/timers/áudio, Draw, Pause/Resume, UnloadContent e ações Dispatcher executadas pelo host; Step/Headless também. Capturar Dispatcher explicitamente durante callback para Post em worker, não ler Context em worker. Escopo não se estende a continuação async; thread-local não distingue uma continuação indevida bombeada dentro do callback de outro jogo: documentar que async/await, async void, reentrada arbitrária via pump e referências retidas não recebem garantias ambientais. Não prometer sandbox de GPU. Acesso sem escopo/host encerrado falha com InvalidOperationException clara. Autoridade GPU é separada: Graphics e Content só em fase/thread/contexto gráfico autorizado pelo host (headless conforme backend); se não puder garantir EGL corrente no teardown, não liberar acesso contextual gráfico naquela fase e documentar; não mudar comportamento explícito silenciosamente. Validação de propriedade não torna referências retidas thread-safe.

### Implementação e testes

- [ ] E3.1 — Auditar Game.Attach/Run*, GameHost.Start/Tick/Step/Stop/Pause/Resume/Guard, GameServices, Dispatcher, GameLoader, HeadlessGameRunner, PreviewHost/PreviewRenderer, IsolatedPreview, StandaloneGameActivity e backends atuais pós-F8. Identificar fase e afinidade gráfica por host; evitar alterações fora do mínimo necessário.
- [ ] E3.2 — Implementar escopo interno sem alocação por entidade/callback no caminho normal (token struct interno ou try/finally direto), identidade de sessão válida e restauração do contexto anterior em reentrada A→B→A. Invalidar após Stop/falha de inicialização; não manter referência estática após saída. Cada getter lê o Game real, sem lookup de dicionário para propriedade central. Sem novo registro de serviços.
- [ ] E3.3 — Instalar escopo em callbacks síncronos listados; Dispatcher executa cada ação em contexto de host e restaura mesmo se lançar; Headless runner pode mudar de thread entre ticks, não fixar identidade do jogo a uma thread de toda a sessão. Autoridade gráfica por fase/host, inclusive Preview e standalone no teardown; testar backend gravador. Não propagar ambient para Task.Run/continuações. Não bloquear API explícita fora do contrato novo.
- [ ] E3.4 — Testar sem host/antes de Attach/depois de Stop, dois hosts na mesma thread e em threads distintas, A→B→A, falha em Initialize/Load/Update/Draw/Unload, callbacks de timer/Dispatcher, Pause/Resume/Step, stop duplo, reinício e headless. Verificar ReferenceEquals com propriedades explícitas, serviço customizado por interface, troca no registro sem alterar instâncias centrais, Task.Run fora do escopo, continuação bombeada em B como limite documentado, falta de contexto EGL e limpeza de referências/ALC quando aplicável.
- [ ] E3.5 — Criar dois exemplos completos equivalentes, um com composição explícita e outro com Context, incluindo Player em arquivo independente, input/texture e duas passagens explícitas. Comparar repasses, linhas, acessos e legibilidade; medir overhead e alocação em Release, sem número inventado. Testar using static local/global e nomes herdados (Content/Input/Services) para colisões; Context qualificado sempre disponível.
- [ ] E3.6 — Se os testes de isolamento, afinidade ou equivalência falharem, não promover fachada insegura: corrigir de forma pontual ou manter API experimental restrita aos serviços seguros e documentar o bloqueio dos demais. Não transformar experimento em revisão geral; não exigir novo estudo/aval para cada detalhe interno. Se critérios passarem, entregar fachada opt-in documentada, com aviso claro de escopo síncrono e caráter experimental. CI verde, merge.

## Gate final — uma release, não três releases intermediárias

- [ ] Integrar E1/E2/E3 em main após F8; revisar diferenças e atualizar todos os checkboxes com SHA/teste e exceções justificadas. Não sobrescrever plano anterior.
- [ ] Executar suíte completa Debug e Release, testes de arquitetura, jornada, API/compilação de exemplos, regressões e workflow de APK. Resolver falhas novas; falhas preexistentes precisam de reprodução e justificativa (nunca “passou” se falhou).
- [ ] Validar jogo de referência com imports visíveis, Context opcional em classe independente, ordenação estável e câmera/HUD; rodar também versão explícita sem Context/imports globais e jogo antigo sem migração.
- [ ] Android: rodar Preview rápido/isolado e APK independente; obter screenshots/evidência de renderização e editor em emulador/hardware disponível, reportando plataforma exata. Medir no moto g32/Android 13 se houver acesso; não bloquear publicação exclusivamente por falta de aparelho físico conforme ADD-0035, mas registrar como não validado. Não afirmar teste físico inexistente.
- [ ] Atualizar referência de API e guias completos e compiláveis, templates/cursos pertinentes, changelog/patchnotes; explicar compatibilidade, opt-in, limites, atalhos e diagnóstico. Não usar nomes de beta como estrutura de documentação.
- [ ] Publicar uma release APK somente do commit integrado validado, identificar tag, SHA, workflow, URL do APK e limitações. Confirmar que o artefato corresponde à main; não confundir build de PR com release. Entregar relatório objetivo do que foi implementado, testes e itens não concluídos.

## Referências de trabalho (fixadas pelos estudos; não exigem nova rodada de pesquisa)

- Estudos internos: base [952659e8](https://github.com/AbnerCruz/Ecosystem/tree/952659e8e7f08a3a8ebdd8d60186697aef6c7b5a/apps/lunet2d), PR [#1028](https://github.com/AbnerCruz/Ecosystem/pull/1028), ADR 0002/0006/0007/0012, docs/governance/protocolo-estudo-desenvolvimento.md e ADD-0035.
- Rendering: [SpriteBatch](https://github.com/AbnerCruz/Ecosystem/blob/952659e8e7f08a3a8ebdd8d60186697aef6c7b5a/apps/lunet2d/src/Lunet.Framework/Graphics/SpriteBatch.cs), [GraphicsDevice](https://github.com/AbnerCruz/Ecosystem/blob/952659e8e7f08a3a8ebdd8d60186697aef6c7b5a/apps/lunet2d/src/Lunet.Framework/Graphics/GraphicsDevice.cs), [MonoGame SpriteSortMode](https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.SpriteSortMode.html), [MonoGame SpriteBatch](https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.SpriteBatch.html), [FNA](https://github.com/FNA-XNA/FNA), [LibGDX SpriteBatch](https://github.com/libgdx/libgdx), [Godot CanvasItem](https://docs.godotengine.org/en/stable/classes/class_canvasitem.html), [Khronos Transparency Sorting](https://wikis.khronos.org/opengl/Transparency_Sorting). Comparar sem copiar semântica instável ou licenças.
- Imports: [C# global using](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-10.0/globalusingdirective), [Roslyn testes](https://github.com/dotnet/roslyn/blob/20b549bac1409a0df9f4575557c71f6a7e506989/src/Compilers/CSharp/Test/Semantic/Semantics/GlobalUsingDirectiveTests.cs), [SDK GenerateGlobalUsings](https://github.com/dotnet/sdk/blob/5db7f0f18d18c99acfa82e6663a84b1122195380/src/Tasks/Microsoft.NET.Build.Tasks/GenerateGlobalUsings.cs), [Visual Studio file nesting](https://learn.microsoft.com/visualstudio/ide/file-nesting-solution-explorer), [SDK colisão #20747](https://github.com/dotnet/sdk/issues/20747), [Roslyn extensão #24096](https://github.com/dotnet/roslyn/issues/24096), [Raylib-cs](https://github.com/raylib-cs/raylib-cs).
- Contexto: [Game](https://github.com/AbnerCruz/Ecosystem/blob/952659e8e7f08a3a8ebdd8d60186697aef6c7b5a/apps/lunet2d/src/Lunet.Framework/Game.cs), [GameHost](https://github.com/AbnerCruz/Ecosystem/blob/952659e8e7f08a3a8ebdd8d60186697aef6c7b5a/apps/lunet2d/src/Lunet.Framework/GameHost.cs), [GameServices](https://github.com/AbnerCruz/Ecosystem/blob/952659e8e7f08a3a8ebdd8d60186697aef6c7b5a/apps/lunet2d/src/Lunet.Framework/Utilities/GameServices.cs), [LibGDX Gdx](https://github.com/libgdx/libgdx/blob/master/gdx/src/com/badlogic/gdx/Gdx.java), [ThreadStatic](https://learn.microsoft.com/dotnet/api/system.threadstaticattribute), [AsyncLocal](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1), [ExecutionContext](https://learn.microsoft.com/dotnet/api/system.threading.executioncontext), [Compatibilidade .NET](https://learn.microsoft.com/dotnet/standard/library-guidance/breaking-changes).

**Fora do ciclo:** API gráfica de baixo nível, buffers públicos, overhaul de recursos/DI, batch contextual, sort por textura/material e comparadores arbitrários, editor bidirecional de imports, mudança de linguagem, expansão iOS/web, reorganização de Studio. Não são tarefas pendentes obrigatórias deste plano. Prioridade: entregar três melhorias verificáveis e uma release, com menor alteração necessária.

### Evidência intermediária de CI

- Revisão `1544136d0038ed0b5accd426b5836a8e25caf60b`: [suíte Debug](https://github.com/AbnerCruz/Ecosystem/actions/runs/45462258155/job/138687886019) — 1211 aprovados e 7 falhas (fixtures antigos e referência de API). As falhas foram tratadas na revisão seguinte: fixtures usam as fontes completas, árvore inclui Imports.cs, Quick Fix considera import global e documentação de novos membros foi completada. Revalidação pendente. A compilação Android anterior também detectou colisões com Android.Content.Context; qualificações adicionais foram aplicadas em `1b5a7586`. Não considerar gate integral aprovado até novo CI.
