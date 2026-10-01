# Classificação de candidatos (P1-12)

> **PROPOSTA**, não autorização. Classifica o que existe hoje em Lunet2D e Urbe nos papéis do modelo de produto (Product Core, Product Shell, Tool, Workspace, Service, Library, Adapter; [`product-model.md`](product-model.md), ADD-0002). **Nada foi extraído, movido nem refatorado** por este documento (passo 9 de [`docs/migration/README.md`](../migration/README.md) §7). Refatorar e extrair é o passo 10, governado por [`refactoring.md`](refactoring.md) e pelo ADR-0009.
> **Revisão:** pendente do proprietário no portal (objeto: este documento).

## 1. Base: os mapas continuam válidos depois da migração (FATO, 2026-10-01)

Os mapas funcionais foram feitos nos inventários ([`inventory-lunet2d.md`](../migration/inventory-lunet2d.md) §12, [`inventory-urbe.md`](../migration/inventory-urbe.md) §12) sobre `be30e62` e `662ca5b`. Depois da importação:

- A árvore de `apps/<id>` é idêntica à das origens fora de `.github/` ([auditoria](../migration/audit-post-migration.md)); as únicas mudanças posteriores são o aviso de espelho no README do Lunet2D e o ajuste da checagem de workflows do Urbe (DEC-0016-A), que não tocam nenhum subsistema mapeado.
- Reconferido no monorepo: o grafo de `ProjectReference` do Lunet2D é o mesmo do inventário; o Urbe continua com **73 módulos** em `src/modules.json` (core 10 · persistence 10 · feature 40 · ui 6 · kit 3 · vendor 2 · native 1 · app 1) e `src/app.js` com 5 259 linhas.
- Os produtos foram validados depois da migração: 286/286 e 56/56 testes no novo caminho; Lunet2D `v0.0.1-dev.107` e Urbe Web validados pelo proprietário ([registros por build](../validation/)).
- `CHK-ARCH-REFS`: nenhum produto referencia o outro nem o Hub.

## 2. Dois tipos de mudança futura (é a distinção que mais importa)

| Tipo | O que é | Onde acontece | Pré-requisito |
|------|---------|---------------|---------------|
| **R — refatoração interna** | Reorganizar um produto **dentro de `apps/<id>`** nos papéis Core/Shell/Adapter, sem criar dependência fora do produto | no próprio produto | gate da Fase 1 + regras do ADR-0009 (ADR **do produto** quando muda a arquitetura dele) |
| **X — extração para o Ecosystem** | Tirar algo do produto para `platform/`, `tools/`, `workspaces/` ou um Service compartilhado | fora do produto | ADR **do Ecosystem**, as cinco respostas de NN-022, consumidor real com teste, e os contratos da fase indicada (Capability e Context: Fase 2; Host API: Fase 5; Agent Runtime: Fase 6; primeira Tool: Fase 7) |

Toda refatoração **R** abaixo é possível logo depois do gate. Toda extração **X** depende de fase futura.

## 3. Lunet2D

| Subsistema | Papel proposto | Confiança | Próximo passo possível | Tipo |
|------------|----------------|-----------|------------------------|------|
| S-01 `Lunet.Framework` | Product Core | alta | nenhum (já isolado, sem pacotes) | — |
| S-02 `Lunet.Runtime` | Product Core | alta | nenhum | — |
| S-03 `Lunet.Compiler` | Product Core | alta | nenhum agora; Service "Compiler/Build" só se outro produto precisar | X (sem consumidor hoje) |
| S-04 `Lunet.Core` | Product Core (projetos) **+** Product Shell (estado de UI) | média | **L-R1**: separar o Project System (`LunetProject`, `ProjectStore`, manifesto, autosave) do estado de UI (`WorkspaceLayout`, `EditorSettings`, `EditorSession`) em projetos distintos dentro do produto | R |
| S-05 `Lunet.Editor` | Tool (Editor) | média | **L-R3**: tirar das views Android a dependência do serviço (as views já estão em S-08); extração como Tool só na Fase 7 | R (preparo) · X (Fase 7) |
| S-06 `Lunet.Git` | Service (Git) | média | nenhum agora; extração só com consumidor real fora do Lunet (NN-022) | X |
| S-07 `Lunet.Docs` | Library | média | nenhum agora | X (sem consumidor hoje) |
| S-08 `Lunet.Android` | Product Shell (host da IDE e do Preview) + Adapters (GLES, áudio, háptica) | média | **L-R2**: quebrar `MainActivity` (1 114 linhas) em shell + painéis; **L-R4**: backends Android atrás de interfaces (Adapters) | R |
| S-09 `Lunet.Tests` | Product Core (testes) | alta | manter as regras de arquitetura do próprio Lunet atualizadas a cada R | — |
| S-10 pipeline/release | indeterminado (Packaging/Launcher, Fase 4) | baixa | nenhum agora; a origem segue publicando (DEC-0008-A) | — |
| S-11 governança própria | fica no produto | alta | referências qualificadas entre ADRs do Lunet e do Ecosystem (R-LUN-5) | — |

## 4. Urbe

| Subsistema | Papel proposto | Confiança | Próximo passo possível | Tipo |
|------------|----------------|-----------|------------------------|------|
| U-01 núcleo | Product Core | alta | nenhum | — |
| U-02 persistência | Product Core + Adapters (`idb`/`fsa`, já extraídos de `app.js` em RM-F1-10/11/12) | alta | nenhum estrutural; **formato do vault é dado do usuário** (só com aprovação, NN-005, REQ-007) | — |
| U-03 editor/explorer | Product Core (editor de Markdown do vault, não o editor de código do Lunet) | média | nenhum agora | — |
| U-04 mundo | Product Core | alta | nenhum | — |
| U-05 Assistente de IA | Workspace (Agent Workspace do Urbe) | média | só mapear na Fase 6 (MANIFEST §39); sem reescrita | X (Fase 6) |
| U-06 páginas | Product Core | média | nenhum agora | — |
| U-07 matemática | Library | média | nenhum agora | X (sem consumidor) |
| U-08 personalização/plugins | Product Core (plugins **próprios**, não o Plugin SDK do Ecosystem) | média | nenhum agora | — |
| U-09 composições | Product Core | alta | nenhum | — |
| U-10 UI/estilos | Product Shell | média | parte de **U-R1** | R |
| U-11 `src/app.js` | Product Shell (casca histórica) | alta | **U-R1**: continuar o programa do próprio Urbe (F2 da Urbe 2.0) que esvazia `app.js` em módulos de Shell, eliminando as exceções de `docs/v2/discovery/BOUNDARY-EXCEPTIONS.md` uma a uma | R |
| U-12 ponte nativa (Electron, Capacitor) | Adapters (plataformas) | alta | manter o contrato `docs/v2/contracts/native.md` | — |
| U-13 Web/PWA | Product Shell (web) | alta | nenhum; publicação segue DEC-0009-A até a Fase 2 | — |
| U-14 ferramentas/testes/tutorial | Product Core (verificações do produto) | alta | manter `check-modules` e as exceções de boundary em ordem a cada R | — |

## 5. Ordem sugerida (PROPOSTA)

1. **U-R1** e **L-R2** primeiro: são as maiores fontes de acoplamento (`app.js`, `MainActivity`) e o Urbe já tem itens próprios e a lista de exceções que mede o progresso.
2. **L-R1** (separar Project System e estado de UI): pequeno e prepara o Shell do Lunet.
3. **L-R4** e **L-R3**: Adapters Android e desacoplamento das views do Editor (preparo para a Fase 7, sem extrair).
4. Extrações **X**: só depois da Fase 2 (contratos), uma por ADR, começando pela que tiver consumidor real.

## 6. Proibições que continuam valendo

Criar Store, criar Product Shell **novo** compartilhado, transformar código em capabilities, reorganizar tudo em packages, reescrever o Urbe em C#, mudar formato de dados do usuário sem aprovação, e extrair para `platform/`, `tools/` ou `workspaces/` sem as cinco respostas de NN-022 (ADD-0002 §17, NN-005, NN-020, NN-022).
