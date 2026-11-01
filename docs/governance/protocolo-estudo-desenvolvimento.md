# PROTOCOLO-ESTUDO-DESENVOLVIMENTO — Investigação, consolidação e entrega

> **Status:** diretriz operacional expressamente determinada pelo proprietário em 01/11/2026.
> **Aplicação:** todos os agentes e todos os Products do Ecosystem. Complementa `MANIFEST.md`, `AGENTS.md`, documentos normativos e decisões locais; não altera sua precedência nem dispensa ADR, permissões, integração ou DEVICE.
> **Objetivo:** impedir programação sem entendimento e pesquisa sem decisões executáveis. Este é o protocolo canônico das seis etapas; arquivos `AGENTS.md` devem referenciá-lo, não criar versões divergentes.

## 0. Reconhecer o tipo de missão e o estado atual

Antes de agir, classificar o mandato como **estudo**, **consolidação**, **desenvolvimento** ou uma combinação explícita. *Estudar* descobre e compreende; *consolidar* decide e registra tarefas; *desenvolver* implementa e valida. Uma missão só de estudo **não autoriza alterar código**. Missão de desenvolvimento **não autoriza pular** estudo e confronto crítico: reaproveitar evidências recentes, mas conferir validade. Identificar o Product, o escopo autorizado, os documentos/decisões vigentes e trabalho concorrente (main, branches, PRs, Issues e handoffs). Registrar a base observada e os bloqueios reais.

## 1. Auditoria interna — conhecer profundamente o produto

Inspecionar implementação e comportamento efetivos: código, arquitetura, componentes, dependências, contratos, testes, build, documentos, decisões, roadmap, issues, PRs, handoffs e trabalho em curso. Identificar capacidades verificadas, parciais, ausentes, legadas, quebradas e apenas propostas; não converter relatos em causas técnicas. Não presumir que documentação representa a main. Antes de criar recurso, buscar equivalentes existentes e sua autoridade de dados. Registrar evidências com `caminho:linha`, commit, cenário ou teste quando disponível.

**Saída:** inventário rastreável de estado atual, hipóteses, riscos e lacunas; distinção explícita entre fato, inferência e proposta.

## 2. Pesquisa externa abrangente — compreender o estado da arte

Investigar produtos concorrentes e adjacentes, diferentes filosofias, bibliotecas, frameworks e soluções open source reais. **Abrir e estudar código** (não somente README), contratos, ADRs, documentação, artigos técnicos, benchmarks, pesquisas científicas quando aplicáveis e testes. Comparar decisões de arquitetura, trade-offs, desempenho, compatibilidade, segurança, UX e manutenção. Buscar alternativas que desafiem a ideia inicial; evitar catálogos superficiais e copiar tecnologia/propriedade de terceiros.

**Saída:** referências verificáveis com o mecanismo técnico aprendido, suas limitações, condições de aplicação e a relevância para o Product.

## 3. Comunidades — investigar experiência real

Consultar issues, discussões, avaliações, fóruns, propostas e relatos de usuários/desenvolvedores. Catalogar **aprovam / rejeitam / desejam**, incluindo problemas recorrentes de acessibilidade, desempenho, fluxo, ergonomia, manutenção e expectativas frustradas. Distinguir relato isolado, padrão repetido e defeito comprovado; não inventar prevalência ou transformar opinião em estatística. Identificar também demandas não atendidas pela concorrência.

**Saída:** síntese de necessidades e antipadrões com links/evidências e grau de confiança.

## 4. Confronto crítico — realidade externa × código × identidade do produto

Retornar à implementação real e comparar descoberta por descoberta: **já existe e funciona? incompleta? falta? contradiz a visão? aumenta acoplamento ou duplica autoridade?** Classificar em **incorporar / adaptar / rejeitar / investigar mais**, justificando alternativas, impactos, custo, risco, segurança, migração, performance, UX e compatibilidade. Preservar o diferencial do Product, o vault/dados do usuário e os boundaries do Ecosystem; não fazer reescrita ou extração sem prova. Para interfaces, estudar UX como frente própria: elegância, simplicidade progressiva, acessibilidade, conforto, touch/teclado, fluidez e performance.

**Saída:** matriz comparativa rastreável e decisões fundamentadas, inclusive justificativas para NÃO implementar.

## 5. Consolidação — converter conhecimento em execução

Atualizar a **fonte canônica** do Product (requirements/roadmap/plano vigente), sem criar roadmap paralelo concorrente. Cada entrega tem ID estável, escopo verificável, prioridade, dependências reais, decisão e justificativa, critérios de aceitação, testes esperados, status honesto e referências ao inventário/pesquisa. Dividir itens grandes por resultado observável sem perder rastreabilidade. Preservar descobertas e razões de rejeição. **Código presente ≠ tarefa fechada**; checklist marcado exige integração, evidência e validação pertinentes. Mudança normativa segue ADR/decisão do proprietário (NN-009/011).

**Saída:** tarefas executáveis e verificáveis, prontas para implementação autorizada; não um amontoado de features.

## 6. Implementação e validação — entregar comportamento real

Quando há autorização de desenvolvimento, priorizar risco de perda de dados, defeitos que bloqueiam uso e dependências indispensáveis; **TDD**: escrever reprodução/testes antes da correção sempre que viável. Implementar na autoridade canônica, integrar operações compartilhadas, evitar gambiarras/duplicações e adicionar regressões. Executar build e testes, comparar desempenho com baseline, observar funcionamento, gerar screenshots quando pertinentes e testar cenários reais. Distinguir rigorosamente:
- **Implementado:** alteração escrita, com commit/branch identificável.
- **Verificado automaticamente:** teste/build/CI executado, versão e resultados registrados.
- **Validado no aparelho:** somente quando houve validação física efetiva (NN-017); APK gerado não equivale a aceito.
- **Integrado/entregue:** PR, estado da integração, artefatos e evidências rastreáveis.

Nenhum agente afirma "corrigido" sem reprodução/evidência suficiente. Tarefas com DEVICE pendente permanecem abertas ou em review; registrar o que falta sem travar incrementos independentes.

## 7. Autonomia responsável e continuidade

- Reduzir ao mínimo aprovações e testes manuais do proprietário: decisões locais em escopo aprovado são autônomas; escalar **somente** direção estrutural/crítica ou ações exigidas pela política de integração. Não inventar consentimento ou validação.
- Buscar entregas substanciais e coerentes, com muitas melhorias compatíveis **sem diluir testes, rastreabilidade, reversibilidade ou qualidade**. Quantidade não é critério de pronto.
- Quando CI, review, terceiro, dispositivo ou outra dependência estiver esperando, registrar o impedimento e **avançar no próximo trabalho independente e autorizado**, sem disputar arquivos de agente ativo.
- Respeitar limites da sessão: não alegar execução contínua ou em segundo plano quando não existe agente/scheduler efetivamente executando.
- Registrar handoff em `docs/governance/handoffs/`: base, branch, tarefa, decisões, arquivos, evidência, bloqueios, próxima ação. A implementação integra via fluxo governado; não fazer merge manual para contornar CI/gates.

## 8. Aplicação específica por Product

| Product | Confronto obrigatório adicional |
|---|---|
| **Urbe / IA** | Editor e persistência confiáveis primeiro; agent tools e UI executam as mesmas operações canônicas; estudar código/agents/skills multimodais e integração, sem converter o Urbe em IDE ou criar segunda fonte de verdade. Publicações referencia notas reais; não replicar Editor de Páginas descontinuado. |
| **Lunet2D** | **Três estudos distintos e ordenados:** framework → editor → estúdio, validando independência e integração por contratos; UX e acessibilidade do editor/estúdio como investigação independente. |
| **Atlas Forge** | Exigir fórmulas, unidades, modelos físicos e fontes científicas; confrontar geografia causal, visual, LOD e performance com implementação e testes observáveis, não apenas aparência. |
| **Ecosystem / plataforma** | Verificar contratos, governança, ownership, reuso comprovado e independência dos Products; nunca promover abstração por mera semelhança (NN-002/004/020/022). |
| **Outros Products** | Aplicar as seis etapas e invariantes do componente, sem impor receitas ou tecnologias de outro Product. |

## 9. Checklist de fiscalização do agente

Uma entrega de estudo/consolidação deve permitir responder: **qual commit e código foram vistos? quais soluções/códigos externos foram estudados? o que a comunidade aprova, rejeita e deseja? que decisões foram confrontadas com o estado real? em quais IDs canônicos viraram trabalho?** Uma entrega de desenvolvimento deve responder adicionalmente: **quais testes foram escritos primeiro, executados, passaram/falharam, quais artefatos foram produzidos e o que permanece sem DEVICE/aceite?** Ausência de evidência deve ser declarada, nunca preenchida por suposição.
