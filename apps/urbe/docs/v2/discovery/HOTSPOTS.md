# Hotspots fora de `app.js` (RM-F2-01, REQ-022)

> Avaliação por responsabilidade e acoplamento dos sete módulos que a SPEC §3.1 lista como hotspots. **Nenhuma extração é feita por este item** (REQ-022): o relatório produz decisões registradas e, quando cabe, insumo para novos REQ. As métricas são reproduzíveis (`node tools/hotspots.mjs`) e conferidas por `tests/hotspots.mjs`.
> Separação (AGENTS.md §3): **FATO** = medido/lido no código; **INFERÊNCIA** = leitura do agente; **DECISÃO PROPOSTA** = recomendação para o item que for executar a mudança — nenhuma é decisão do proprietário e nenhuma muda boundary, dados ou contrato.

## 1. Métricas [F]

Definições: *Linhas* = linhas do arquivo; *Funções* = declarações `function` + `const f = (…) =>`; *Fan-out* = módulos de `requires ∪ uses` no manifesto; *Fan-in* = módulos que dependem dele no manifesto ou que referenciam, por texto, os globais/serviços que ele provê; *Provê* = globais + serviços; *DOM* = acessos a `document.`, `querySelector`, `innerHTML`, `addEventListener`; *Armaz.* = acessos diretos a `localStorage`, `indexedDB`, `FS.`, `Disco.`; *Exceções* = linhas do módulo em `BOUNDARY-EXCEPTIONS.md`. São indicadores de onde olhar, não notas de qualidade: contagem por expressão regular, sem análise sintática.

<!-- hotspots:begin -->
| Arquivo | Camada | Linhas | Funções | Fan-out | Fan-in | Provê | DOM | Armaz. | Exceções de boundary |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `src/pages/engine.js` | feature | 678 | 51 | 3 | 4 | 1 | 19 | 2 | 1 |
| `src/pages/studio.js` | feature | 592 | 126 | 11 | 1 | 2 | 92 | 0 | 1 |
| `src/pages/free.js` | feature | 273 | 33 | 1 | 0 | 0 | 0 | 0 | 0 |
| `src/world/life.js` | feature | 442 | 36 | 3 | 0 | 1 | 6 | 0 | 1 |
| `src/ai/ui.js` | feature | 407 | 65 | 9 | 2 | 1 | 70 | 0 | 0 |
| `src/customize/panel.js` | feature | 307 | 57 | 7 | 0 | 1 | 35 | 0 | 0 |
| `src/world/pixel-art.js` | feature | 384 | 39 | 0 | 3 | 1 | 1 | 0 | 0 |

_Base: 73 módulos em `src/modules.json`; reproduza com `node tools/hotspots.mjs`._
<!-- hotspots:end -->

## 2. Leitura por arquivo e decisão

| Arquivo | Responsabilidades observadas [F/I] | Acoplamento | Decisão proposta |
|---|---|---|---|
| `src/pages/engine.js` | Motor **puro** (sem DOM de montagem): valida/normaliza `.page.json`, descreve blocos, gera a saída exportada. Uma responsabilidade coesa. | Fan-in 4 (`pages/free`, `pages/templates`, `pages/ai-tools`, `pages/studio`). Única exceção: usa `renderMarkdown` provido por `app.js`. | **Manter.** A exceção sai com **RM-F2-09** (Markdown vai para o editor); não há o que extrair do arquivo. |
| `src/pages/studio.js` | Editor visual: prévia em iframe, lista de seções com arraste, inspetor, tema, JSON, desfazer/refazer, salvamento. Várias responsabilidades de UI no mesmo arquivo (126 funções, 92 acessos a DOM). | Maior fan-out do grupo (11 módulos); fan-in 1 (`app`). Exceção `studio → ai/ui` (reutiliza o painel do agente). | **Adiar a divisão** (nenhum consumidor novo a exige; NN-020 vale dentro do Urbe). **Exceção `studio → ai/ui`: manter listada**; resolvê-la por serviço de painel é mudança de boundary → exige item novo + ADR (AGENTS.md §3), então fica como **achado**, não como decisão. |
| `src/pages/free.js` | Árvore de elementos do layout livre, estilos responsivos por breakpoint, conversão. Sem DOM. | Fan-out 1 (engine), fan-in 0 no grafo; sem acesso direto a `document.`. | **Manter.** Coeso e sem exceções. |
| `src/world/life.js` | Vida do mundo (luz, clima, água, fauna, fumaça…): simulação e desenho em um módulo de fase `late`. | Exceção `life → app.js` (`world.life.host`). Fan-in 0 no grafo estático: `app.js` provê o serviço `world.life.host` e `life.js` o consome (`requires` de `app.js`); nenhum módulo referencia `UrbeVida`. | **Manter o módulo; a exceção sai com RM-F2-16** (host extraído). Divisão interna (luz × clima × fauna) só se um item de mundo exigir. |
| `src/ai/ui.js` | Interface do Assistente: cartões de ferramenta, aprovação com prévia, parar/desfazer, troca de agente/provedor/modelo. | Fan-out 9, fan-in 2 (`app`, `pages/studio`). Sem exceções próprias. | **Manter.** Candidato a *Workspace* (U-05 em `docs/architecture/candidates.md`), mas só será mapeado na Fase 6 e sem reescrita; **sem extração** (sem segundo consumidor, `local-first.md`). |
| `src/customize/panel.js` | Tela de Personalização: tema, texturas com editor de pixels, estilos CSS, plugins, JSON. | Fan-out 7, fan-in 0: é acionado pelo comando `ui.customize` (`panel.js:303`), usado pelas Configurações (`ui/settings.js`); nenhum módulo referencia `UrbeCustomizePanel`. Sem exceções próprias; as exceções de `customize/*` são dos módulos vizinhos (`customize.js`, `plugins.js`). | **Manter.** Dependência de `world/pixel-art.js` é direta e descendente (aceita pela camada). |
| `src/world/pixel-art.js` | Geração procedural de texturas/sprites; sem estado; sem dependências. | Fan-out 0, fan-in 3 (`customize/customize`, `customize/panel`, `app`). | **Manter.** É o módulo mais isolado do grupo; nada a fazer. |

## 3. Conclusões

- **Nenhum dos sete deve ser extraído ou dividido agora.** Em nenhum há consumidor externo, exceção que a divisão resolva nem requisito que a exija; as três exceções de boundary do grupo saem por itens que já existem (RM-F2-09, RM-F2-16) ou dependem de uma decisão de boundary (`studio → ai/ui`).
- **Achado para o ledger (proposta, não decisão):** `pages/studio.js` concentra o maior acoplamento de UI fora de `app.js`. Se for tratado, o caminho é um item novo que torne o painel do agente um serviço (`BOUNDARY-EXCEPTIONS.md` remove a linha no mesmo PR) e ADR da fronteira `pages` × `ai`.
- **Escopo do item:** análise estática por regex; não mede custo em execução (performance fica em RM-F5-*), nem testes por módulo (RM-F2-02/18).
