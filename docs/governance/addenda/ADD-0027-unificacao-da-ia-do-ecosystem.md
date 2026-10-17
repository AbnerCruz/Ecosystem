# ADD-0027 — Unificação da Inteligência Artificial do Ecosystem

**Data:** 2026-10-16. **Autoridade:** ordem explícita do proprietário, missão
“Unificação da Inteligência Artificial do Ecosystem”, registrada por Codex.
**Registro:** DEC-0050. **Escopo:** todos os Products e seus futuros agentes.
Complementa MANIFEST §20/39, ADR-0011 e ADR-0017; não autoriza extração automática.

## Diretriz permanente

> “O objetivo não é copiar o assistente para cada aplicativo. É construir uma
> infraestrutura verdadeiramente compartilhável, extensível e independente,
> preservando o melhor de ambos os sistemas.”

O **assistente nativo do Urbe** é a referência funcional e de experiência de
usuário, incluindo melhorias recentes e futuras compatíveis. O
**AgentRuntime.Core existente no Ecosystem AI** é a principal referência de
infraestrutura. Não criar terceiro Runtime. Cada responsabilidade compartilhada
terá uma única autoridade; domínio e dados continuam pertencendo ao Product.

Todo desenvolvimento, revisão, integração e distribuição acontece em
`AbnerCruz/Ecosystem`, base `main`. Espelhos não são autoridades de implementação.

## Requisitos da missão

1. Auditar MANIFEST, AGENTS, ROADMAP, ADRs, `agent-runtime.md`, código e testes de
   `Urbe.Core/Assistant`, `Urbe.Client/AssistantPanel*`, `AgentRuntime.Core`,
   `AgentWorkspace`, providers, configuração, ferramentas e consumidores previstos.
   Conferir PRs/Issues/handoffs recentes. Documentação histórica não prova estado.
2. Oferecer núcleo agnóstico de modelos/provedores e adaptadores de domínio.
   OpenAI, Anthropic, Gemini, OpenRouter, Ollama e APIs compatíveis entram conforme
   suporte demonstrado: streaming, tools, discovery, erros, preferências salvas,
   seleção rápida e credenciais em armazenamento nativo seguro.
3. Agentes especializados/personalizáveis com instruções, contexto, memória,
   histórico, planejamento e ferramentas autorizadas. Delegação/subagentes/equipes
   só quando autorizados e úteis; cancelamento, continuação, recuperação, proteção
   contra loops/repetição improdutiva e injeção de instruções.
4. Capacidades versionadas e descoberta contextual. Leitura/escrita/execução só
   mediante permissões; prévia antes/depois, aprovação, edição/rejeição, desfazer
   quando seguro e tecnicamente possível, registros auditáveis e validação
   independente quando aplicável.
5. Experiência conversacional nativa em C#, mobile-first: agentes, modelos,
   permissões, cartões de execução, andamento, histórico, configuração reutilizável,
   tokens e custos acessíveis. Sem introduzir WebView como substituto da interface
   nativa. Consistência de comportamento não exige telas idênticas.
6. Evitar chamadas desnecessárias, usar contexto/memória com eficiência e observar
   custos. Orçamentos são configuráveis conforme cada Product. **O RPG permanece
   pay-as-you-go sem teto financeiro obrigatório**; políticas do Ecosystem AI não
   podem ser impostas ao RPG por conveniência de integração.

## Domínios que permanecem específicos

- **Urbe:** notas, arquivos, páginas, personalização, plugins e experiência atual.
- **Lunet2D:** código, projetos, cenas, sprites, jogos, diagnóstico e tooling da IDE.
- **Tabletop RPG:** personagens, NPCs e Mestre; visões privadas, campanha, intents,
  regras e eventos continuam sob o motor próprio. Agente genérico não o substitui.
- **Math Authoring:** aulas, explicações, fórmulas, animações e documentos por
  capacidades próprias quando seu roadmap/maturidade permitir.
- **Ecosystem AI:** projetos, sessões, tarefas, agentes, equipes, verificações,
  histórico e execução supervisionada continuam sendo valor do Product.

## Restrições e gates

- Avaliar/reutilizar AgentRuntime.Core; nenhum terceiro Runtime ou cópia de loop.
- Nenhuma dependência direta entre Products, nem dependência essencial do Hub.
- Nenhuma pasta `shared` genérica, reescrita geral ou mudança de identidade.
- Extraction Review obrigatória (NN-022): consumidores reais, benefício, contrato,
  custo de evolução e saldo demonstrados, com ADR antes de promoção. Dois
  contextos/testes headless não equivalem a dois Products consumidores em produção.
- O loop do Urbe só é substituído após paridade comprovada. Preservar oráculos,
  melhorias recentes, contratos, autorização contextual, isolamento e dados.
- Cada integração tem testes automatizados nos consumidores e operação standalone.
- PRs pequenos/verificáveis, compatíveis com main; coordenar arquivos em trabalho
  paralelo. Issues, ADRs, handoffs, CI e aprovações críticas continuam obrigatórios.
- Não declarar concluído algo pendente de aprovação; CI não substitui DEVICE.

## Execução incremental e aceite

**A:** matriz atual de equivalência, alternativas, riscos, ownership e migração.
**B:** aprimorar o Runtime existente com capacidades generalizáveis verificadas,
sem copiar indiscriminadamente o Urbe. **C:** integrar o consumidor elegível de
menor risco por contrato/adaptador e demonstrar consumo real antes da promoção
quando exigida. **D:** demais Products conforme dependências/roadmaps. **E:**
configuração, modelos, permissões, histórico e acompanhamento consistentes,
preservando UX própria. **F:** builds/regressão/arquitetura/integração; APKs e
artefatos desktop necessários com links verificáveis; DEVICE permanece separado.

A missão exige dois aplicativos reais usando implementações compartilhadas,
sem cópias divergentes/dependência entre Products; paridade do Urbe; configuração
e segredos confiáveis; tools só autorizadas no contexto; mudanças supervisionadas;
testes nos consumidores; domínio essencial independente de IA/Hub; nenhuma
regressão conhecida escondida. O primeiro PR não encerra a missão.

A cada incremento, informar implementação, consumidores efetivos, recursos do
Urbe preservados/incorporados, resultados dos testes, commits/PRs/Issues/builds,
pendências e próximo incremento. Distinguir implementado, integrado e validado
no aparelho.

> “A arquitetura deve servir aos produtos. Os produtos não devem ser
> sacrificados para satisfazer a arquitetura.”
