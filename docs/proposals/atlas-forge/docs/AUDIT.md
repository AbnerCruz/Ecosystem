# Auditoria de entrada — AF-001

## Fatos observados

- Repositório clonado de `AbnerCruz/Ecosystem`, branch main, snapshot inicial `64cb8d121eec5ff834112e3f5c0a712e27868290` (clone shallow; não é auditoria do histórico completo).
- Workspace inicialmente vazio de projetos. Único anexo recebido: mandato unificado. `atlasforge.html`, exemplo JSON, viewer Alpha, guia e ZIP não estavam acessíveis. A Parte III descreve a Alpha, mas não é seu código.
- `ecosystem.json` não declara Atlas Forge. Nenhum arquivo/branch/handoff com Atlas/Forge foi encontrado no inventário; busca por títulos nos até 100 primeiros Issues e PRs abertos não encontrou tarefa nomeada Atlas/Forge. Isso não prova ausência de todo histórico remoto.
- `gh` possui credencial funcional; desenvolvimento pode ser registrado em Issue/PR, sem necessidade de repositório alternativo. Nenhum segredo foi incorporado ao software.
- Node 24.19.0 e npm 11.9.0 disponíveis. SDK .NET não estava no PATH; instalado SDK 10.0.402 temporariamente em `/tmp/atlas-dotnet` e executado check real. Chromium instalado para testes.
- ADR-0034/ADD-0028 retiram simulação do Urbe e preservam seus arquivos. ADD-0030/DEC-0053 já alteraram a base do Urbe para Área de trabalho. Nenhum desses produtos foi modificado.
- Fontes consultadas diretamente: MANIFEST integral/NN, AGENTS, ARCHITECTURE, ROADMAP, ecosystem.json; product-model/local-first/capability-runtime; ADR-0001/0034, índice e template ADR; ADD-0028; communication, definition-of-done, multi-agent, integration-policy; schemas de decisões/handoff; handoff e ADR de constituição matemática como precedentes. Resumos não foram usados para substituir as fontes nas decisões estruturais.
- Baseline do Ecosystem: 22 checks passaram. Pendências preexistentes de consequências documentais DEC-0031/DEC-0041 foram avisos, não falhas.

## Inferências

O domínio tem valor de uso independente e é candidato a aplicação local. “Ferramenta do ecosystem” na conversa não comprova dois Hosts reais nem aprova extração global. O primeiro consumidor real é o próprio editor, suficiente para implementação isolada; promoção compartilhada necessita evidência adicional.

## Propostas

ADR-0035 / DEC-0054: incorporar futuramente como aplicação independente `atlas-forge` em `apps/atlas-forge`, mantendo JS/Canvas como exceção local expressa. Por enquanto, a implementação está em `docs/proposals/atlas-forge`, dentro do próprio Ecosystem, sem mudança no mapa canônico de componentes. Workflow verifica somente a proposta e gera artefatos de teste, não publica release/site.

## Decisões autorizadas e fronteiras

Mandato autoriza construir e testar de forma autônoma; estabelece stack e independência, edição persistente, notas por cópia e viewer offline. Não autoriza alteração da governança canônica, aceitação de ADR pelo agente, registro silencioso de capability global, merge crítico ou release. A implementação segue as autorizações; a incorporação fica proposta.

## Antes/depois verificável

Não há baseline de código da Alpha. Não se alega refatoração de um HTML inexistente ou paridade demonstrada. O antes observável é ausência de implementação Atlas acessível. O depois é o corte descrito na SPEC, com testes e artefatos locais. Fixture Alpha v1 é sintética; validar U16/escala e notas contra o corpus original é AF-003.
