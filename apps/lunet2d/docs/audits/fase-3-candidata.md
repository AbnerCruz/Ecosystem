# Fase 3 — candidato e validação integral

Tarefa **LUNET-303**, [Issue #200](https://github.com/AbnerCruz/Ecosystem/issues/200). O escopo/estado da fase continua em [ROADMAP](../../ROADMAP.md); a resposta humana é registrada na verificação do handoff `HO-20261004-lunet-f3-candidate`. A auditoria técnica permanece em [fase-3.md](fase-3.md).

## Candidato exato

- Versão: **0.0.1-dev.1000008**, Android arm64.
- [Baixar o APK](https://github.com/AbnerCruz/Ecosystem/releases/download/lunet2d-v0.0.1-dev.1000008/Lunet-0.0.1-dev.1000008-arm64.apk) · [release e notas](https://github.com/AbnerCruz/Ecosystem/releases/tag/lunet2d-v0.0.1-dev.1000008).
- Commit publicado: `1ff4386bae93edf027db81c8de6579640d2e58ed`; inclui os PRs #116 e #152.
- SHA-256 informado pelo canal: `9e3881cbcc2a9b1d2208410756fbf19631d0922aa59818cce666abc750c70146`. O digest foi conferido nos metadados da release; esta entrega não afirma ter verificado novamente os bytes do APK.
- CI do código: [Lunet testes](https://github.com/AbnerCruz/Ecosystem/actions/runs/37149331446) e [APK do PR](https://github.com/AbnerCruz/Ecosystem/actions/runs/37149331442) aprovados; a release contém o artefato instalável.

Faça backup dos projetos por **exportar ZIP** antes de atualizar. Instale por cima da versão anterior; se a instalação exigir desinstalar, interrompa e relate antes de apagar dados. Use um projeto descartável `TesteF3`, modelo Coletor de moedas. Os testes básicos e a documentação devem funcionar offline; GitHub só é usado no bloco opcional H7–H8.

## O que executar

Siga os passos completos do [roteiro A–J](fase-3-roteiro.md), inclusive onde tocar, resultados esperados e regressões. Esta página fixa o candidato; não substitui o roteiro.

| Bloco | O que conferir |
|---|---|
| A | Run/Stop, persistência, arquivos e exportação |
| B–C | Editor, autocompletar, navegação, renomear, formatar e correções rápidas |
| D–E | Dobrar código, cursores, busca, painéis, rotação e configurações |
| F | Documentação com modo avião |
| G | Inspector e Preview rápido/isolado, incluindo recuperação |
| H | Git local; teclado físico e GitHub são opcionais nos passos assinalados |
| I | Recuperação depois de fechar o app |
| J1 | Medir o editor em 2 mil, 10 mil e 40 mil linhas; compartilhar o relatório e conferir que o arquivo original foi restaurado |

Se um teste falhar, pare esse fluxo e relate o código do passo, resultado esperado/observado, modelo do aparelho, Android e versão do Lunet. Não execute o teste G6 de laço infinito com projeto importante aberto; use o projeto de teste conforme o roteiro.

## Como responder e aprovar

Envie por bloco: `A1–A5 passaram; B2 falhou: ...; J1: relatório ...`. Use **passou / falhou / não testei**. Os opcionais não testados devem ficar identificados; não devem ser relatados como aprovados.

J1 é necessário para avaliar a pendência de virtualização do editor. O relatório e eventuais correções precisam ser analisados antes do encerramento. Quando as pendências obrigatórias estiverem resolvidas, aprove explicitamente o **gate da Fase 3** no portal. Havendo falha ou item obrigatório não testado, relate os resultados e mantenha a aprovação pendente.

Aprovação da importação ZIP (#121), C6 (#155) e atualização/uso standalone P4-4 (#163) já estão registradas. Nenhuma delas aprova a experiência de IDE inteira. O registro de build `docs/validation/lunet2d/0.0.1-dev.1000008.json` continua válido para seu objetivo P4-4; esta verificação é o gate distinto da Fase 3.

## Limites e próxima etapa

Esta entrega não implementa a Fase 4, IPC, Host API no Lunet nem extração compartilhada. Após o retorno, atualizar a auditoria por item, corrigir/retestar falhas e resolver J1. Só então avaliar a conclusão da fase e a prontidão de um futuro segundo Host; aprovar a IDE não escolhe automaticamente transporte nem concede grants runtime.
