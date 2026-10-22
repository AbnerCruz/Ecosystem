# ADR-0035 — Atlas Forge: implementação isolada e incorporação gradual

## Status

Aceito — DEC-0054, opção A, aprovada pelo proprietário em 2026-10-22 após revisar a entrega: “Pode por no ecosystem meu rei”. Fonte: [ADD-0031](../governance/addenda/ADD-0031-atlas-forge-no-ecosystem.md). Aplicação independente; sem Tool compartilhada ou capability pública.

## Contexto

O proprietário forneceu mandato para construir o Atlas Forge, ambiente de autoria de mundos esféricos de alta fantasia, e pediu execução: “Faça o projeto, dê o seu melhor. É idealmente uma ferramenta do ecosystem”. O mandato fixa HTML5/CSS/JavaScript modular/Canvas, aplicação independente, primeiro corte vertical verificável e incorporação governada. Exclui reintroduzir simulação no Urbe, depender do Hub ou promover infraestrutura especulativa.

FATO OBSERVADO: a main auditada foi `64cb8d121eec5ff834112e3f5c0a712e27868290`. Só o mandato foi recebido; arquivos reais da Alpha não estavam disponíveis. Na primeira entrega, a aplicação isolada em `docs/proposals/atlas-forge/` implementou geometria, geração, edição, clima/drenagem, notas, armazenamento e publicação offline, com verificações locais registradas. Naquele primeiro corte nenhum componente foi adicionado ao manifest.

## Problema

Entregar software agora e definir a incorporação futura sem interpretar “ferramenta” informal como autorização para Tool global. O padrão C# do MANIFEST §4 abrange novos aplicativos e ferramentas compartilhadas; o stack web solicitado deve ficar explícito na decisão de incorporação. A implementação local não extrai código de outro Product nem muda contratos existentes.

## Opções

1. Incorporar como aplicação independente `atlas-forge`, em `apps/atlas-forge`, mantendo domínio/engine/editor em módulos JS/Canvas locais. Distribuição offline HTML; integração por contratos futuros conforme consumidor real. Recomendado.
2. Manter por enquanto como proposta executável isolada no próprio Ecosystem. Não registrar componente ou capability até haver evidência adicional de uso e decisão de incorporação.
3. Criar imediatamente Tool global em `tools/` ou embutir no Urbe/Hub. Não recomendado: falta segundo consumidor real/contrato; Urbe nativo não admite WebView e sua simulação foi retirada.

## Decisão

**Opção 1 aceita pelo proprietário**, registrada em DEC-0054/ADD-0031. O código canônico passa para `apps/atlas-forge`, com registro em `ecosystem.json`, instruções/documentação locais e CI seletivo/reutilizável. A proposta original permanece como proveniência e evidência, sem segunda cópia do código. A incorporação é implementada no PR #658; integração Git, publicação de produção e validação DEVICE não são inferidas desta decisão.

A opção 1 inclui exceção local e explícita de JavaScript/Canvas para este editor web, conforme mandato; não altera padrão C# da plataforma. O funcionamento e a publicação do Atlas independem de Hub, Lunet e Urbe. Nenhuma capability pública é criada por este ADR.

## Consequências

- A proposta pode ser executada, testada e revista agora. Código e decisões residem no Ecosystem; nenhum repositório paralelo ou espelho é criado.
- Incorporação possui manifest, documentação local, CI seletivo e perfil alvo sem canal de produção fictício. Publicação de release e eventual capability ficam para tarefas próprias.
- `WorldState` é autoridade local do projeto; caches e viewer são projeções. Projetos/notas pertencem ao usuário, sem conta/rede obrigatórias.
- Uma futura Tool multi-host exige consumidor concreto, Extraction Review, contrato e evidência de integração. Reuso potencial fica no handoff; não criar Library/Service global preventivamente.
- Migração do contrato Alpha é aproximada e baseada no documento, com fixture sintética. Corpus real e validação Android permanecem pendentes; CI não os substitui.
- O integrador classifica o PR pela política da main. Não há merge manual, release automática ou autorização simulada.

## Alternativas rejeitadas

A opção 3 é incompatível com NN-002/003/007/020/022 e a direção do Urbe (ADR-0034/ADD-0028). Reescrever o editor em C#/WASM sem ganho medido contraria o mandato. Criar apps/atlas-forge e marcar registro como aceito silenciosamente elimina a distinção entre proposta e decisão.

## Referências

MANIFEST §4/6/12/24/29/48/49; NN-001/002/003/005/007/008/009/010/011/014/016/017/018/019/020/021/022/023; ADR-0001/0006/0011/0015/0034; DEC-0054; Issue [AF-001 #657](https://github.com/AbnerCruz/Ecosystem/issues/657); [mandato](../proposals/atlas-forge/MISSION.md); [software executável](../../apps/atlas-forge/README.md); [arquitetura](../architecture/atlas-forge.md).
