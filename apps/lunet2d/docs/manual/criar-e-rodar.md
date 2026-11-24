# Crie e rode um projeto vazio

Abra **Projetos**, toque em **Novo jogo**, selecione o modelo **Em branco** e abra `Game.cs`. Use orientação vertical. Substitua o conteúdo do arquivo por este exemplo completo.

```csharp full
using Lunet;
using Lunet.Graphics;

public sealed class PrimeiroJogo : Game
{
    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
    }
}
```

Toque em **Rodar**. A tela preta é o resultado esperado. Volte ao código, troque `Color.Black` por `Color.CornflowerBlue` e toque em **Rodar** de novo. A mudança de cor confirma que o Preview está executando a nova versão.

## Próximo passo

[Faça seu primeiro jogo responder ao toque](guide:responder-ao-toque)


## Jogar sem abrir o editor

Na área de trabalho, escolha **Jogar**. Toque no cartão de um jogo local para preparar a revisão salva e iniciar uma partida sem instalar APK. O botão de opções do cartão abre o editor. Para personalizar a imagem do cartão, coloque um PNG em `Content/icon.png`; projetos antigos usam a inicial do título.

Na primeira execução, a preparação compila o código. A próxima execução pode reaproveitar a compilação da mesma revisão. Alterações no código, recursos ou configuração invalidam a revisão anterior. Se a compilação falhar, o Lunet mostra o diagnóstico e oferece abrir o editor; não inicia uma versão antiga silenciosamente.

Volte com o botão Voltar do Android. O Launcher usa saves próprios por jogo, separados dos saves de testes do Preview, que permanecem preservados. A opção **Gerar APK** no editor continua sendo o caminho para compartilhar e instalar o jogo fora do Lunet.
