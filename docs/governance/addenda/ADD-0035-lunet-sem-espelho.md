# ADD-0035 — Lunet exclusivamente no Ecosystem, sem espelho

**Data:** 15/11/2026. **Autoridade:** ordem explícita do proprietário nesta conversa. **Registro:** DEC-0064. **Escopo:** Lunet2D, distribuição e continuidade de agentes. **Precedência:** substitui, apenas para o Lunet, as instruções transitórias de espelhamento de DEC-0008-A/DEC-0014-A, complementando ADD-0023, ADD-0015 e ADD-0034.

## Ordem recebida

> “Foda-se o espelho. Só o ecosystem importa agora, não deveria nem ter mais esse negócio. Arruma isso e já aproveita pra registrar sua análise atual, ela deve orientar o próximo agente em seu desenvolvimento.”

## Regra operacional

1. O único repositório operacional do Lunet é **AbnerCruz/Ecosystem**. Código em `apps/lunet2d`; PRs/Issues/handoffs no Ecosystem; CI `.github/workflows/lunet2d-ci.yml`; builds/releases `.github/workflows/lunet2d-release.yml` ou pipeline oficial de reconciliação; tags `lunet2d-v*` no Ecosystem.
2. O repositório `AbnerCruz/Lunet2D` não é espelho, upstream, canal de distribuição, fallback nem destino de agentes. Não abrir PRs, consertar seu CI, sincronizar ou publicar lá. O histórico já importado fica preservado no Ecosystem (NN-012). O campo `source.repository` no manifesto é **proveniência histórica**, não autoridade operacional.
3. O workflow agendado `sync-from-ecosystem.yml` foi excluído da main do repositório antigo (commit `56f0beb169c9f9bf74a51f1b53340f0d0f69221e`). Remover do Ecosystem a automação `origin-sync` e o modelo legado; não a reativar.
4. Perfis `current` e `target` do Lunet não podem apontar para `source.repository`. A plataforma first-party permanece **futura**, sem substituir o canal real de releases diretas.
5. Preservar identidade `io.lunet.studio`, assinatura, versionCode monotônico, dados do usuário, testes, integração governada e maturidade escolhida pelo proprietário (ADD-0034). A exclusão/arquivamento do *repositório GitHub em si* requer acesso administrativo à conta; não é exigida para encerrar a operação.
6. A análise incremental `docs/architecture/audits/lunet-estado-atual-2026-11-15.md` deve orientar o próximo agente. Conferir sempre a `main` e os checks atuais; o relatório anterior de 14/11 é histórico e não reflete todas as implementações posteriores.

## Evidência/limite

O APK `lunet2d-v0.0.1-dev.1000575` já havia sido publicado nas Releases do Ecosystem em 14/11. O CI do repositório antigo aprovou 1.063 testes mas falhou no APK por referenciar contrato Binder localizado fora de sua árvore. Essa evidência confirma que o espelho antigo é inadequado para builds, **não** que o pipeline oficial do Ecosystem falhou. Este descomissionamento não altera código funcional do produto nem declara DEVICE aprovado.
