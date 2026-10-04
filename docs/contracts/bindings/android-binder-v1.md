# Binding Android Binder v1 da Host API

Status: implementação de P5-4, subordinada à Host API v1 e à ADR-0027/DEC-0037-A. Este arquivo define apenas o binding Android; não cria uma segunda Host API.

## Descoberta

O provider declara o action de serviço:

`org.ecosystem.capability.HOST_V1`

O caller consulta serviços que anunciam esse action e transforma o resultado escolhido em `ComponentName` explícito antes de `bindService`. O Product não codifica nome de outro Product. Android package visibility usa `<queries><intent>...`.

Mais de um provider compatível é tratado como ambíguo até existir UX de escolha; o primeiro slice falha fechado.

## Interface Binder

Descriptor: `org.ecosystem.capability.android.IHostV1`

Transações síncronas, a partir de `IBinder.FirstCallTransaction`:

| Offset | Operação |
|---:|---|
| 0 | `pair.begin` |
| 1 | `session.challenge` |
| 2 | `session.open` |
| 3 | `discover` |
| 4 | `invoke` |
| 5 | `cancel` |
| 6 | `close` |
| 7 | `revoke` |

Cada Parcel contém `WriteInterfaceToken(descriptor)` seguido da string JSON. Em `session.open`, o caller acrescenta um `IBinder` de lifecycle com `WriteStrongBinder`; o provider registra `LinkToDeath` nesse token e fecha/cancela a sessão se o processo do caller desaparecer. O reply usa `WriteNoException()` + JSON. Payload nunca escolhe UID, pacote, signatário, actor efetivo ou concede grants. `revoke` só reduz permissions já capturadas pela sessão; a lista de permissions participa da assinatura do request.

## Política de confiança do primeiro ensaio

A allowlist bilateral está em `docs/contracts/bindings/android-binder-v1.trust.json` e é empacotada nos dois APKs. Ela fixa pacote + fingerprint do signatário observado para provider e callers autorizados. Chaves de desenvolvimento públicas continuam sem provar autoria: a allowlist reduz superfície de descoberta/abuso, enquanto o pareamento humano + chaves por instalação fornecem a confiança criptográfica da instalação.

Mudança de pacote/signatário falha fechado. Mudança da chave por instalação exige reset explícito e novo pareamento; nunca há aceitação automática por compartilhar o mesmo certificado de desenvolvimento.

## Identidade e pareamento

1. O provider captura `Binder.CallingUid` dentro da transação, antes de qualquer trabalho assíncrono.
2. O package manager resolve pacote e signatário do UID. Ambiguidade, ausência ou múltiplos signatários falham fechado.
3. Cada instalação cria uma chave EC P-256 não exportável no Android Keystore; só a chave pública sai do app.
4. `pair.begin` cria um pedido pendente limitado e um código decimal de seis dígitos derivado das duas chaves públicas + nonces aleatórios.
5. O usuário compara o mesmo código nas duas superfícies e aprova/reprova no provider.
6. A aprovação persiste: chave pública da instalação, package id e fingerprint do signatário observado. Atualização com o mesmo signatário mantém o pareamento; troca de chave de instalação ou signatário exige novo pareamento.
7. `session.challenge` só atende par aprovado. O provider assina um challenge com sua chave de instalação; o caller verifica a chave pública do provider que participou do pareamento.
8. `session.open` exige assinatura do caller sobre o challenge e o contexto. Challenge é de uso único e expira.
9. Operações de sessão continuam vinculadas ao peer autenticado; `sessionId` isolado nunca transfere autoridade.

Instalação, assinatura do APK, pareamento e grants são camadas distintas. Nenhuma delas, sozinha, concede capability.

## Budgets v1

- JSON por transação: máximo **256 KiB UTF-8**.
- Chave pública codificada: máximo **2 KiB**.
- Assinatura codificada: máximo **2 KiB**.
- Pending pairings: máximo **4**, expiração **2 min**.
- Challenges: máximo **8**, expiração **30 s**, uso único.
- Sessões no provider: máximo **4**.
- Sessão ociosa: expira após **2 min**; death-recipient do caller encerra antes disso quando o processo morre.
- Uma invoke ativa por sessão.
- Deadline padrão: **10 s**; máximo **30 s**.
- IDs/nonces: máximo **128 caracteres**.
- `text.inspect` continua com seu limite de contrato próprio; o binding pode rejeitar um payload antes do handler se exceder o budget de wire.

O limite de wire é deliberadamente menor que o buffer nominal do Binder, que é compartilhado pelo processo sob concorrência. Não existe tentativa de “usar até 1 MiB”.

## Erros do binding

Erros de transporte são separados dos erros Host API/capability:

- `PROTOCOL_UNSUPPORTED`
- `FRAME_TOO_LARGE`
- `PEER_UNTRUSTED`
- `PEER_DISCONNECTED`
- `PAIRING_REQUIRED`
- `PAIRING_PENDING`
- `PAIRING_REJECTED`
- `CHALLENGE_INVALID`
- `SIGNATURE_INVALID`
- `DEADLINE_INVALID`
- `DEADLINE_EXCEEDED`
- `SESSION_BUSY`
- `PROVIDER_UNAVAILABLE`

Erros são sanitizados; exceções, paths, texto privado e fingerprints não necessários não atravessam a fronteira.

## Standalone

Nenhum Product depende deste binding para seu domínio essencial. Provider ausente, morto ou não pareado = integração indisponível explícita. Nenhum fallback por rede/GitHub.

## Evidência

Critérios de fechamento: matriz IPC-01..IPC-18 em `docs/architecture/ipc-transport-readiness.md` e roteiro `docs/validation/P5-4-binder-ipc.md`.
