# ADR-0018 — Instalação e launcher do Hub

## Status

Aceito — DEC-0030-A, escolhida pelo proprietário no portal (Issue #112, 2026-10-03; registro em `docs/governance/responses/DEC-0030.md`). A política aceita não concede permissões Android nem aprova integração de implementação por si só.

## Contexto

P4-2 entrega bytes conferidos em cache privado. SHA-256 informado por um canal não comprova identidade, autoria, compatibilidade ou instalação. P4-3 exige modelo explícito antes de implementar a mudança de um Hub read-only para ações sobre aplicativos. Cada Product continua distribuído e utilizável sem Hub (NN-023).

As identidades observadas são `io.lunet.studio` em `apps/lunet2d/src/Lunet.Android/Lunet.Android.csproj` e `app.urbe` em `apps/urbe/native/android/app/build.gradle`. São evidências das autoridades de build, não um registro paralelo a ser embutido no Hub. A assinatura de desenvolvimento estável do Lunet usa chave pública versionada (ADR local 0004); a do Hub segue DEC-0029-A. Fixar esses certificados limita substituições acidentais, mas não autentica autoria contra quem possui a chave pública de desenvolvimento.

## Problema

Definir em que condições o usuário pode instalar/atualizar e abrir um pacote pelo Hub, quais permissões ficam necessárias e qual confiança cabe ao canal de desenvolvimento. A política não pode confundir download verificado com aplicativo confiável ou silenciosamente ampliar o papel de agentes/plugins.

## Opções

| Opção | Primeira instalação | Atualização | Custo e limite |
|---|---|---|---|
| A — Identidade e certificado previamente aprovados (recomendada) | Pacote canônico e certificado/linhagem aprovada, além do hash | Mesmas verificações e compatibilidade exigida pelo Android | Exige registrar fingerprints públicos por Product/canal antes de habilitar; certificado de chave de desenvolvimento pública não prova autoria |
| B — Identidade e canal declarado, sem pin inicial | Pacote canônico, hash e consentimento, confiando no canal HTTPS declarado | Compatibilidade de assinatura e versão imposta pelo Android | Menos preparação; primeira instalação admite outro assinante se o canal for comprometido |
| C — Adiar instalador | Download continua disponível; instalação independente | Atualização pelo canal independente | Implementar apenas versão instalada/launcher; gate de instalação permanece pendente |

## Decisão

Adotada a opção A pelo proprietário na DEC-0030, registrada pela automação após a Issue #112. Conferir identidade canônica e certificado/linhagem previamente aprovados é requisito antes de instalar/atualizar. Sem identidade ou certificado aprovado, oferecer o canal independente; não cair para B. Esta aplicação da decisão não ativa instalador, altera chaves/canais ou aprova fingerprints ainda não registrados.

O modelo comum às opções A/B está detalhado em [modelo-de-instalacao.md](../../apps/hub/docs/modelo-de-instalacao.md). A alternativa escolhida deve orientar os requisitos e a validação de P4-3; A não pode cair silenciosamente para B na ausência de certificado aprovado.

## Consequências

A/B introduzem uma ação sensível restrita à interface e ao consentimento do usuário. P4-3 deve definir concessões explícitas para instalação e abertura, registrar permissões correspondentes no contrato e solicitar `REQUEST_INSTALL_PACKAGES` pelo Android somente no fluxo necessário. Nenhuma concessão se transfere a agentes/plugins. Esse PR futuro é crítico e terá a autorização exigida pelo integrador.

Nenhuma opção exige inventariar todos os aplicativos (`QUERY_ALL_PACKAGES`), permissões de administrador/root, desinstalação automática ou alteração dos canais atuais. Ausência de APK, identidade ou certificado aprovado aparece com acesso ao canal independente. Pacote instalado/atualizado somente será anunciado após resultado e consulta reais do sistema. A validação em aparelho e o gate P4-4 permanecem humanos.

## Alternativas rejeitadas

As opções B e C não foram escolhidas pelo proprietário na DEC-0030-A. Instalação silenciosa, confiar apenas no nome do asset/hash e extrair um Service de instalação sem consumidor real estão excluídos da proposta por NN-001, NN-016, NN-020 e NN-022.

## Referências

- MANIFEST: NN-001, NN-003, NN-011, NN-016, NN-017, NN-023; ROADMAP P4-2..P4-6.
- [ADR-0013](0013-hub-read-only-fase-3.md), [ADR-0015](0015-integrador-automatico.md), [catálogo de permissões](../contracts/permissions.json).
- [DEC-0030](../governance/decisions.json), Issue [#109](https://github.com/AbnerCruz/Ecosystem/issues/109).
- [Assinatura de desenvolvimento do Lunet](../../apps/lunet2d/docs/adr/0004-assinatura-de-desenvolvimento-estavel.md).
