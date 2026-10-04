# P5-4 — Candidato DEVICE do IPC Binder autenticado

Estado: **pronto para validação DEVICE; ainda não aprovado**.

Este objeto valida o primeiro binding IPC real da Host API v1. O código permanece isolado no PR crítico #210; este registro não integra runtime nem antecipa aprovação.

## Candidato congelado

- PR de implementação: #210 — `chatgpt/p5-4-binder-ipc`.
- Head do código: `b001eafda4baa16868d626eff402a2f7f57f6918`.
- Estado combinado realmente testado pelo CI: merge-ref `c69f909cffccc1369ee611e883fb8dbae0b8050b` = head acima sobre `main` `1d69d5e055238d602283abe1c508743f3378020c`.
- Transporte: Android Bound Service/Binder, DEC-0037-A / ADR-0027.
- Capability do ensaio: `text.inspect@1.0.0`.

### Hub Android

- CI: run `37221326822`.
- Hub.Core: **270/270** testes.
- APK Android: **verde**; certificado de desenvolvimento estável conferido.
- Artifact: `hub-debug-apk`, ID `11310570442`.
- Digest do artifact ZIP: `sha256:4e81f3477ac6e3b95b0d91e7833af383b8200a28933051ffdaa0ce229f8a4d11`.
- SHA-256 do APK extraído: `ebd38055c50871298071f79e379f3364741d594fb05b1f6f2378d307473e40c4`.

### Caller Android

- Lunet CI: run `37221326904`, **321/321** testes.
- Build APK: run `37221326943`, **verde**.
- Artifact: `lunet-apk`, ID `11310540558`.
- Digest do artifact ZIP: `sha256:9bdc1b4be8a7dc7cacd94034fc35b0f12e1166e4ab08aa660ac2998a125eb357`.
- SHA-256 do APK extraído: `17e9913e569890f254c9074a2960e4002238c886c81690d1ff1eceed600c3422`.

### Consistência

Run `37221326838`: **verde completo** — foundation checks, self-test de cada checker e simulação do integrador.

## O que já está provado sem aparelho

- sessão externa vinculada a peer autenticado e isolamento contra roubo de `sessionId`;
- Context e grants definidos pelo Host; `session.open` rejeita Context vindo do caller;
- deny-by-default e revogação que só reduz grants;
- replay/request duplicado sem segundo efeito;
- cancelamento com descarte de resultado tardio;
- deadline finito inclusive contra handler não cooperativo;
- budgets de sessões/requests/journal e expiração por idle;
- erros, output inválido e falha do provider sanitizados;
- JSON <= 256 KiB e Parcel <= 640 KiB no binding;
- nenhum Product referencia código do provider concreto;
- ausência do provider é modelada como integração opcional indisponível.

## Matriz IPC-01..IPC-18

| ID | Estado antes do DEVICE | O que o aparelho precisa provar |
|---|---|---|
| IPC-01 | CI parcial | Dois APKs/processos pareiam e `text.inspect` devolve resultado correto por Binder. |
| IPC-02 | CI parcial | O caller candidato é reconhecido pelo UID/pacote/signatário reais do Android. |
| IPC-03 | CI parcial | A sessão real nasce da identidade Binder, não de identidade enviada no payload. |
| IPC-04 | DEVICE | Pin da chave do provider e rotação/reinstalação exigem novo pareamento. |
| IPC-05 | N/A justificado | Binding v1 não possui callback/progress remoto; reabrir se essa superfície for adicionada. |
| IPC-06 | CI | Sessão/cancel/close/revoke de outro peer são recusados. |
| IPC-07 | CI parcial | Discovery real apresenta somente capability permitida no Context. |
| IPC-08 | CI | Context/grants são Host-owned; caller não possui edição/elevação de escopo. |
| IPC-09 | CI | Protocolo, versão e input inválidos falham antes do handler. |
| IPC-10 | CI parcial | Caminho Binder real funciona dentro dos budgets já impostos. |
| IPC-11 | CI | Replay/duplicata não reexecutam efeito. |
| IPC-12 | CI | Cancelamento é terminal e tardios são descartados. |
| IPC-13 | CI parcial | Revogar pareamento no provider invalida confiança para a próxima sessão. |
| IPC-14 | CI | Deadline limita a espera e tardios não viram sucesso. |
| IPC-15 | DEVICE | `LinkToDeath` fecha sessão quando o processo caller morre; provider morto não quebra o Product. |
| IPC-16 | CI parcial | Transporte real opera com budgets sem afetar uso normal. |
| IPC-17 | CI | Falha/output inválido não vazam exceção, path ou conteúdo privado. |
| IPC-18 | DEVICE | Caller permanece utilizável sem provider instalado/rodando. |

## Candidato DEVICE final — v3

Use somente a pre-release `p5-4-device-v3-2e397ce3` para os gates C/D.

- estado-fonte dos APKs: `2e397ce33fb2df1241770a599c2ad1aefe13f678`;
- mesmo estado integrado na `main`: `48782858d75e75ec71337345e054a7b38235aa7d`;
- Hub: `Ecosystem-Hub-P5-4-v3-2e397ce3.apk`, versionCode **114**, SHA-256 `84f249470d3ebf89449d9819f0d26a7bcc3648518b9bf787327f4b5937570c2b`;
- Lunet: `Lunet-P5-4-v3-2e397ce3-arm64.apk`, versionCode **1000087**, SHA-256 `19ff0cd491ae3f5e383c2938f8e2a4b714851481a070b255aa5c96662d1ac8e0`;
- release: `https://github.com/AbnerCruz/Ecosystem/releases/tag/p5-4-device-v3-2e397ce3`.

Instale ambos **por cima** dos candidatos atuais. Não limpe dados nem desinstale: C/D precisam observar persistência, rotação e lifecycle reais. O v2 fica como evidência histórica do fluxo B, mas está superseded como candidato final.

## Roteiro físico

### A — Standalone / IPC-18

1. Instale **somente o Lunet candidato**.
2. Abra o Lunet, crie ou abra um projeto, edite algo e execute o fluxo normal.
3. Na tela de projetos, toque **Conexões** → **Conectar e testar**.
4. Esperado: provider indisponível/conexão indisponível, sem crash. IDE/framework continuam funcionando.
5. Não aprove o gate se o Lunet depender do provider para abrir, editar, executar ou listar projetos.

### B — Primeiro pareamento / IPC-01, 02, 03, 07, 08 e 10

1. Instale o **Hub candidato** mantendo o Lunet.
2. Lunet → **Conexões** → **Conectar e testar**.
3. Anote o código de 6 dígitos exibido.
4. Abra o aplicativo provider → **Conexões locais**.
5. Confirme que o código é exatamente igual. Se não for, **reprove e interrompa o teste**.
6. Aprove a conexão.
7. Volte ao Lunet → **Conexões** → **Conectar e testar**.
8. Esperado: título **Conexão autenticada** e contagens coerentes de caracteres, palavras e linhas.
9. No provider, **Conexões locais** deve mostrar a instalação aprovada.

### C — Persistência, revogação e rotação / IPC-04 e 13

1. Feche/reabra ambos os apps e repita **Conectar e testar**. Não deve solicitar aprovação novamente.
2. Provider → **Conexões locais** → **Revogar**.
3. Lunet → **Conexões** → **Conectar e testar**.
4. Esperado: novo código de pareamento; a conexão antiga não é restaurada.
5. Aprove novamente e confirme `text.inspect`.
6. Lunet → **Conexões** → **Esquecer conexão**.
7. Esperado: o app informa que apagou a confiança **e girou a chave**.
8. Na próxima conexão, deve aparecer outro pareamento mesmo com os mesmos package/signatários.

### D — Morte do processo / IPC-15

1. Com o pareamento válido, Lunet → **Conexões** → **Abrir sessão por 60 s (teste de lifecycle)**.
2. Assim que aparecer a mensagem de sessão aberta, abra o provider → **Conexões locais**.
3. Esperado: **Sessões IPC abertas agora: 1**. Fora dessa janela, ver **0** é normal: `Conectar e testar` fecha a sessão ao terminar e o pareamento continua válido.
4. Volte às configurações do Android e **force a parada do Lunet** antes dos 60 s.
5. Abra/atualize **Conexões locais** no provider.
6. Esperado: **Sessões IPC abertas agora: 0**. O death-recipient deve ter encerrado a sessão; a instalação continua pareada.
7. Reabra o Lunet e confirme que o app continua funcional; a sessão antiga não reaparece.
8. Por fim, force a parada do provider e tente **Conectar e testar** no Lunet.
9. Esperado: indisponibilidade/erro sanitizado, sem crash e sem afetar o restante da IDE.

## Como responder

No portal, aprove P5-4 apenas se A–D passarem integralmente. Se algo falhar, reprove e escreva no comentário o passo e o comportamento observado, por exemplo:

`B8 falhou: depois de aprovar o código, o Lunet mostrou PROVIDER_UNAVAILABLE.`

Não desinstale/limpe dados para “fazer passar” antes de registrar a falha; isso apagaria evidência de pairing/lifecycle.

## Critério de fechamento

P5-4 só pode virar `[x]` depois de:
- A–D passarem no aparelho;
- validação humana crítica ser registrada canonicamente;
- implementação crítica estar integrada (PR #222 já integrado em `f440c109`);
- estado pós-integração ser reconciliado.

## Resultado

**DEVICE parcial verde; candidato final v3 publicado para C/D.**

O proprietário confirmou no aparelho que o fluxo real está funcionando: o pareamento existente é reconhecido, a sessão autentica, `discover` encontra a capability e `text.inspect@1.0.0` executa corretamente.

O indicador observado como `0` depois do comando não representa desconexão. O cliente fecha a sessão transitória no `finally` e mantém a confiança de pareamento. A UI foi corrigida para distinguir:

- **instalações pareadas** — confiança persistente entre as instalações;
- **sessões IPC abertas agora** — sessões transitórias abertas durante operação ou teste de lifecycle.

P5-4 permanece aberto somente até concluir **C** (revogação/rotação) e **D** (process death / `LinkToDeath`) no aparelho e registrar a validação crítica final.
