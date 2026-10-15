# ADD-0025 — Ordem do proprietário: publicar o Urbe nativo como "beta 2.0"

**Data:** 2026-10-15. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0048. **Escopo:** cliente C# nativo do Urbe (`apps/urbe/csharp/src/Urbe.Android`) e seu pipeline de release. **Complementa** ADD-0024 (DEC-0047), ADD-0020 (DEC-0043/ADR-0032) e ADD-0015.

## O que o proprietário disse

*"Pode fazer a release e depois continuar o desenvolvimento. Chamemos de versão beta 2.0"* — depois de receber a primeira fatia do UC-48 (editor no toque, salvar rápido, barra inferior, estúdio de páginas no celular).

## Decisões do proprietário

1. A release beta do cliente nativo sai agora, sem esperar o fim do UC-48. Ela se chama **Urbe Beta 2.0** e é uma pré-release.
2. Depois da release o desenvolvimento continua (UC-48 e seguintes).

## Como a ordem é cumprida (aplicação, sem direção nova)

- **Canal único do Urbe C# no Android:** o pipeline já existente (`urbe-csharp-mobile-release.yml`, tags `urbe-csharp-v*`, identidade `app.urbe.csharp`, certificado beta público do canal) passa a publicar o **cliente nativo**. A interface Blazor (`Urbe.App`) está congelada desde ADR-0032 e deixa de ser publicada; não se cria terceiro cliente nem segundo canal (NN-001).
- **Versão:** `2.0.0-beta.10`, `versionCode` 2000010. Ela sucede os betas 2.0 anteriores do mesmo canal, por isso atualiza no lugar quem tem o beta C# instalado. A única fonte da versão publicada é `Urbe.Android.csproj` (`UrbeBetaVersion`/`UrbeBetaVersionCode`).
- **Prévia de PR:** o APK de prévia do CI continua com a identidade de prévia (`app.urbe.nativepreview`), instalável ao lado.
- **Dados:** o vault fica na pasta escolhida pela pessoa (SAF). Nada é migrado nem apagado. No canal, as preferências guardadas no aparelho pelo beta anterior não são lidas pelo cliente nativo.
- **Gates:** a release é pré-release. A validação no aparelho (DEVICE/G-N0, NN-017) continua pendente e é registrada no handoff. UC-25 e UC-27 só são concluídos depois dela.
