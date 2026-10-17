# ADR-0033 — Unificação incremental da IA com preservação do Urbe

## Status

Aceito **somente quanto à direção expressamente ordenada** em DEC-0050 / ADD-0027.
Não aprova extração, contrato público, pacote, migração de segredos, troca do loop
ou integração de IA em Product. Cada mudança crítica segue seu próprio gate.

## Contexto

O Urbe C# já possui AssistantAgent, providers OpenAI-compatible/Anthropic com
streaming e UI nativa; não é mais apenas uma semente JavaScript. O Ecosystem AI
já usa AgentRunner/WorkspaceSession com grants, ledger, event log e verificação.
A auditoria atual está em `docs/architecture/audits/ai-unification-2026-10-16.md`.
ADD-0027 exige uni-los, mantendo independência e a melhor experiência de ambos.

## Problema

Copiar o Urbe nos Products multiplica autoridades. Substituí-lo já pelo Runtime
perde streaming, prévias editáveis, desfazer e tools de domínio. Extrair todo o
Ecosystem AI cruza persistência/UX do Product e impõe seu ledger aos consumidores.

## Opções

1. Copiar assistentes completos: duplicação e manutenção divergente.
2. Trocar imediatamente o loop do Urbe: paridade não demonstrada.
3. Promover todo o Runtime/Workspace/armazenamento de uma vez: segundo consumidor
   em produção e contratos ainda não demonstrados.
4. Convergência incremental sobre o Runtime existente, com adaptadores e revisão
   de extração por responsabilidade após evidência de consumo real.

## Decisão

Opção 4, conforme a ordem do proprietário. A experiência nativa e as capacidades
verificadas do Urbe orientam os critérios de equivalência; AgentRuntime.Core é a
base de infraestrutura. A localização atual permanece até Extraction Review
positiva + ADR/contrato + provas de consumidores; não criar Runtime alternativo.

**Ownership pretendido (não implementa promoção):** modelos/tools/execução
agêntica genérica no Runtime/adapters; grants e Context derivados do Host;
segredos em adapters nativos por referência; preferências/histórico com escopo e
ownership explícitos; domínio, verificadores e UX especializados em cada Product.
Uma implementação compartilhada não implica um único arquivo de configuração ou
um segredo universal acessível a todos os apps.

Cada responsabilidade pode migrar separadamente. O primeiro candidato de consumo
adicional é o transporte de modelos do Urbe, sem mudar seu loop; a elegibilidade
é condicionada à suíte de equivalência, streaming/erros/usage, credenciais seguras
e integração standalone. Isso é **recomendação técnica**, não extração aprovada.
O Lunet continua na Fase 8 para IA de usuário; ADR-0031 só autoriza piloto headless.
O RPG preserva gateway/intents/perspectivas e pay-as-you-go sem teto obrigatório.
Math Authoring espera maturidade/roadmap. Ecosystem AI preserva seu Product.

## Consequências

Nenhum novo componente em ecosystem.json, dependência, permissão ou contrato
público nasce com este ADR. Melhoria interna do Runtime pode avançar desde já,
com testes e handoff; extração aguarda o gate. A decisão geral não deve ser
reaberta a cada PR; escolhas críticas concretas ainda não decididas exigem o
processo normal. A aprovação de integração crítica do PR é do proprietário pelo
integrador, e não decorre do status deste ADR.

## Alternativas rejeitadas

Opções 1–3 violam preservação, NN-001/002/020/022 ou antecipam prova de paridade.
Colocar tudo no Hub também viola NN-003/023. Compartilhar UI inteira sacrifica
os fluxos de domínio e não é exigido para comportamento consistente.

## Referências

MANIFEST §20/30/39/48/54; NN-001/002/003/004/005/006/007/009/010/011/016/017/018/020/022/023;
ADR-0011, ADR-0017, ADR-0031, ADR-0032; ADD-0027; DEC-0050; Issue #603.
