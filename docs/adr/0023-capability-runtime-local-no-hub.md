# ADR-0023 — Capability Runtime experimental local no Hub

## Status

Proposto — detalhe do slice experimental P5-2, dentro da direção ADR-0011/0012 e Fase 5. Não aceita protocolo público, transporte IPC, novo componente ou dependência entre Products. Tais decisões serão registradas em P5-3/P5-4/P5-5 antes de sua implementação pública.

## Contexto

A Fase 2 estabilizou o vocabulário de Context, capabilities, versões e permissions. A Fase 5 precisa de enforcement em execução. Nenhum segundo Product está atualmente integrado como Host desta Tool; dois contextos de teste não autorizam extração (NN-022).

## Problema

Provar discovery e execução local com Context/grants capturados pelo Host, sem inventar plataforma compartilhada antes de consumidores reais.

## Opções

1. Publicar imediatamente SDK/IPC transversal: antecipa contrato e custo sem prova de consumidor.
2. Implementar Tool diretamente na Activity: não prova a fronteira nem o enforcement.
3. API em processo, local ao Hub, e adapter Android separado: valida a fronteira com testes negativos e uso real no Hub.

## Decisão

Proposta experimental local, opção 3:

- `Hub.Core/Capabilities` contém definições confiáveis, sessão, envelope e Tool; somente o adapter Android conhece widgets e o Product concreto.
- `ecosystem-local/0` identifica API experimental, não protocolo público congelado. `System.Version` recebe exatamente três segmentos; mesma major e versão mínima, major zero exige versão exata. Não implementa ranges ou prereleases SemVer da Fase 2.
- Registry é derivado das definições fornecidas pelo Host, sem persistência/catálogo paralelo; capability ambígua é recusada. Manifest/contratos existentes mantêm sua autoridade.
- Context é capturado imutavelmente, começa em ecosystem e segue a ordem canônica; scope deve ser prefixo exato. Validação de existência dos Products continua no Registry canônico, não neste slice em memória.
- Grants são a interseção imutável de concessões explícitas e permissões declaradas pelo Host; deny-by-default tanto em discovery como em dispatch. Texto enviado não concede permissão ou muda Context/ator.
- Sessão permite uma chamada ativa, no máximo 256 identidades aceitas e journal de 64 frames; request repetido não reexecuta. Falhas de validação não consomem a identidade.
- Frames têm sequência, correlação e Context fixo; eventos de abertura/fechamento, progresso e erros tipados. Journal não contém input/output nem mensagem arbitrária de exceção.
- Fechar revoga discovery e cancela chamada ativa; resultado/progresso tardio não vira sucesso. Cancelamento cooperativo não é sandbox nem interrompe código arbitrário à força.
- Tool `text.inspect` aceita apenas `{text:string}`, até 100.000 unidades UTF-16; conta Unicode scalar values, tokens separados por whitespace e linhas LF. Input/output são clonados. Não abre arquivos, rede, provider ou credenciais.
- Android pede a análise por ação explícita e fornece `ui.display` já declarado pelo Hub; texto não é persistido. Activity/dialog fecha a sessão. O contrato matemático da Tool não conhece Activity, Product ou seu nome.

## Consequências

Prova local executável e adapter do primeiro Host real. Duas sessões com projetos diferentes verificam portabilidade de contexto, não integração com segundo Host real. P5-2 e gate continuam sujeitos a integração/CI e validação Android.

Nenhuma capability pública é adicionada ao manifest, nenhum componente compartilhado é extraído e nenhum Product passa a depender do Hub. API não deve ser usada entre processos. Publicação de contrato, autenticidade de caller IPC, orçamento/timeout de handlers arbitrários e segundo Host são itens posteriores.

## Alternativas rejeitadas

Extração sem consumidor contradiz ADR-0011 e NN-022. Tool embutida na UI duplicaria validação e não provaria lifecycle. Usar GitHub como IPC viola NN-015.

## Referências

MANIFEST §14–16/20/21/30; AGENTS.md; NN-001/003/010/015/016/017/018/020/022/023; ADR-0004/0011/0012; context.schema.json; permissions.json; ROADMAP P5-1..P5-7; Issue #170.
