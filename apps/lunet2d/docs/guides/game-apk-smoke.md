# APK independente do jogo — Lunet LUNET-445 / Issue #787

A opção Arquivo → Gerar APK do jogo entrega, por um build online autorizado pelo usuário, um APK independente da IDE. Não confundir com o APK do Lunet Studio.

## Como utilizar

1. Em Git → Conta Git, configure um token do GitHub com acesso a repositórios privados e workflows. Token clássico: permissões repo e workflow.
2. Abra o projeto e execute Run para confirmar que o Game.cs compila.
3. Abra ⋯ → Arquivo → Gerar APK do jogo.
4. Leia e aceite o aviso: o Lunet envia um snapshot ZIP do jogo (código e assets) ao repositório PRIVADO lunet-game-builds na sua conta GitHub. Um repositório público com esse nome é recusado.
5. O app cria um workflow na conta GitHub (primeiro uso), envia o snapshot por GameId, solicita build Android, mostra progresso, baixa o APK, abre o seletor de arquivos para exportar e oferece a opção de instalar.
6. Após instalar, execute o jogo independente do Lunet.

É necessária internet, token GitHub autorizado, GitHub Actions disponível e recursos de build na conta. O usuário controla o repositório. Nenhuma credencial é incluída no jogo ou nos inputs do workflow; o token existente do GitAccountStore fica no armazenamento privado do app (ainda sem proteção via Android Keystore).

## Arquitetura

Lunet.GameCode.csproj compila C# do projeto (Game.cs e outros), com exclusão de Tests e pastas de cache. Lunet.Game.Android.csproj empacota Game.dll, Content e Assets e executa o host OpenGL e áudio reais, sem UI da IDE. A implementação do cliente HTTP fica em Lunet.Core; a interface Android é MainActivity.GameBuild.cs. O PackageId é lido do lunet.json, isolado do io.lunet.studio. O fluxo usa a ferramenta oficial do Android no GitHub e gera um APK com assinatura de desenvolvimento.

## Roteiro DEVICE — pendente

1. Atualizar a IDE para a release que inclua o PR #788, com backup dos projetos.
2. Criar Jogo Vazio e confirmar execução com Run.
3. Configurar token com permissão repo e workflow, autorizar Gerar APK.
4. Confirmar no GitHub que lunet-game-builds é privado; verificar o workflow e o ZIP do projeto.
5. Após o build, salvar game.apk, instalar e abrir o jogo sem a IDE. A bola amarela deve seguir o toque.
6. Criar um sprite/tileset no Lunet, carregar no Game.cs, repetir e conferir os assets.
7. Testar token inválido, repositório público pré-existente, cancelamento, falta de internet e build que falha. Falhas não devem apagar o projeto local.

## Limitações conhecidas

O build local offline no Android ainda não foi implementado. O GitHub Actions depende da conta e pode ter custos/limites próprios. O ZIP tem limite operacional de 30 MiB no cliente inicial; APKs muito grandes são recusados na memória. A assinatura de desenvolvimento da toolchain não é persistente por jogo: para atualizar o mesmo pacote pode ser necessário desinstalar a versão anterior. Ainda faltam assinatura estável/release, AAB, suporte robusto a projetos grandes e validação real no aparelho; não marcar F9 concluída apenas por CI verde.

O workflow lunet2d-game-apk-smoke gera um APK de demonstração no CI. GitHubGameBuildTests cobre o contrato cliente HTTP com transporte simulado, não substitui o teste completo na conta do usuário.
