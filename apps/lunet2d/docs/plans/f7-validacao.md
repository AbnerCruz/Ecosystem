# F7 · Launcher de jogos — LUNET-525 / #1032

Base examinada: main e2d03e2dc8fda9f79c779b379ffadab48b889ff9; F6 #1031 head 8b5e7d7c918a28e1f529bfb93e542d4727fd4e3e. A branch F7 deriva do head da F6 para preservar Home e Studios. Não é a Fase 7 Networking.

## Estudo e decisões

Antes: MainActivity.Home.PlayFromLauncher abria o projeto e chamava Run; o editor e Preview compartilhavam os saves de teste. GameCompiler reutiliza a última compilação por fonte, não uma revisão jogável persistida. ProjectManifest e LauncherProjectCatalog já oferecem identidade por GameId.

- MonoGame [Game.cs](https://github.com/MonoGame/MonoGame/blob/develop/MonoGame.Framework/Game.cs): preservar loop e lifecycle no host, sem introduzir outro framework.
- [Godot Android](https://docs.godotengine.org/en/4.7/tutorials/editor/using_the_android_editor.html): distinguir janela de execução e instalação; as limitações da plataforma e ausência de C# no editor Godot não se transferem automaticamente ao Lunet.
- [Android Developers: dynamic code loading](https://developer.android.com/privacy-and-security/risks/dynamic-code-loading?hl=pt-BR): armazenamento privado, verificação de integridade e origem local. O processo :gameplay não é isolamento de segurança: código C# executa com a UID do Lunet; não prometer sandbox de código hostil.
- A referência itch perform-launch indicada no plano não pôde ser aberta nesta sessão. As discussões comunitárias e a comparação visual completa com a prévia aprovada não foram verificadas integralmente; registrar isso como limite, não como evidência de estudo completo.

## Implementação proposta no código

- Nova aba Jogar, grade de dois cartões, título, pasta/identidade, ícone opcional Content/icon.png e fallback; abrir partida e editar são ações distintas. Projetos sem ID persistente/duplicado são identificados e não iniciados silenciosamente.
- PlayableRevision captura manifestos, fontes C# fora de Tests, Content e Assets em staging privado, rejeita links e alterações detectadas durante captura, ignora notas não executáveis e cache de projeto. Chave SHA-256 com caminhos, bytes e identidade de engine/runtime/compiler. O cache tem envelope verificado e publicação atômica, sem executar versão anterior automaticamente.
- GameplayActivity não exportada, em processo :gameplay, reaproveita PreviewRenderer e PreviewHost com ferramentas do editor ocultas, orientação declarada, toque, teclado, sensores, áudio e image picker. O Preview existente permanece distinto. Saves gameplay ficam em FilesDir/gameplay/<GameId>/saves, sem migrar ou apagar os saves de Preview. O APK standalone permanece o caminho de distribuição.
- Erros de compilação mostram diagnóstico; erros de inicialização ou falha do host retornam à grade, com opção de editar. Activity result e estado salvo restauram identidade de uma sessão interrompida. Cache/staging são descartáveis e não fazem parte de projetos.

## Evidência e lacunas

Testes adicionados: PlayableRevisionTests (identidade por fonte/asset/manifesto/toolchain, movimentação de pasta, independência de snapshot, cache corrompido, compilador real frio/quente, erro após build válido e dois GameIds), LauncherPreferencesTests (Jogar persistido). **Não executados nesta sessão:** ambiente sem SDK .NET e sem conexão direta Git/Android. GitHub Actions deve compilar e testar a revisão integrada. Não declarar a frente encerrada antes do resultado.

Roteiro Android/emulador ainda necessário: criar jogo vertical com toque e save, jogo horizontal e jogo com asset; abrir de Jogar, tocar/áudio, voltar diretamente à grade; repetir build quente; editar fonte/asset/configuração; provocar erro e corrigir; interromper, reabrir, girar e matar atividade; conferir saves isolados e exportar APK pelo editor. A matriz ProjectJourneyTests/RegressionProjectTests existente continua relevante, mas não substitui a execução de GameplayActivity. Ausência de teste físico é limitação, não requisito de aprovação humana (DEC-0065).

Limites de implementação: status da grade é a última execução ou compilação ainda não verificada; não afirmar que a revisão corrente está compilada sem capturá-la. Cache privado descartável, ainda sem política explícita de poda por tamanho. Sessões abandonadas quando Android perde o resultado podem permanecer no cache até limpeza. Segurança contra código hostil não é entregue. Não há fallback automático para build antigo. Nenhuma release F7 publicada neste registro.
