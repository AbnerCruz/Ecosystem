# Regressão: falha na assinatura offline do APK Android (2026-10-26)

## Evidência inicial
No projeto Bosque Lumi, a ação «Gerar APK do jogo» exibe «Falha ao gerar APK local / Specified cast is not valid». O stack trace não aparece na UI; o ponto da falha ainda não está confirmado. No código atual, AndroidGameApkSigner.Sign obtém Java.Security.IKey via AndroidKeyStore e o converte para IPrivateKey com cast C# direto.

## Referências
- Android Developers: Android Keystore, recuperar PrivateKeyEntry e usar Signature.
- Microsoft Learn: Android.Runtime.Extensions.JavaCast<TResult>, conversão JNI verificada.
- Xamarin.Essentials/SecureStorage.android.cs: GetKey(...).JavaCast<IPrivateKey>().

## Testes ANTES da alteração de código
1. Teste automático AndroidGameApkSignerRegressionTests: impedir retorno ao cast direto em código-fonte, exigir assinatura-verificação e diagnóstico de etapa. Esperado: falhar na base anterior, passar após correção.
2. CI: testes portáteis, consistency, build de APK da IDE, apksigner verify e aapt dump badging do APK de jogo (workflow lunet2d-release).
3. Android DEVICE obrigatório: sem internet, abrir projeto Bosque Lumi; Run (baseline); Gerar APK do jogo; escolher documento, instalar, iniciar com controles e assets. Verificar PackageId e app independente do Lunet.
4. Repetir exportação do MESMO GameId e PackageId após pequena edição e atualizar o APK instalado, sem perder dados. A assinatura deve ser estável enquanto o Lunet permanecer instalado.
5. Projeto inválido: código C# com erro mostra Problemas; PackageId inválido é rejeitado; interrupção/cancelamento não produz APK parcial.
6. Falha forçada ou simulação de Keystore ausente: mensagem identifica a etapa e o log contém stack trace sem expor segredo criptográfico; nenhuma substituição por assinatura improvisada.
7. Regressão Android: Preview, Sprite/Tile/Audio Studio, Assets e exportação SAF continuam funcionais.

## Distinção dos gates
Teste de fonte e build de CI NÃO comprovam AndroidKeyStore em aparelho. A validação física precisa ser registrada separadamente, mesmo com o pipeline verde.
