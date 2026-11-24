# Área de trabalho, aparência e pasta de projetos

## Primeiro uso

O Lunet permite manter os projetos internos ou escolher uma pasta no seletor do Android. A escolha de tema e fonte vem a seguir. A configuração de IA é opcional. Cada etapa confirmada é salva nas preferências privadas. Em Configurações é possível rever a pasta e a aparência, inclusive voltar ao tema escuro e à fonte monoespaçada.

Em janelas compactas os destinos ficam embaixo. A partir de 600 dp a mesma navegação ocupa a lateral. Início, Projetos, Studios, Explorar e Aprender retomam a rolagem na mesma sessão. A busca de jogos é restaurada quando o Android recria a atividade. O menu de um projeto fixado permite movê-lo ao início. O botão Voltar fecha o chat global e os diálogos e recupera a área de origem do editor e do jogo.

## Armazenamento

Em **Configurações → Pasta de projetos**, selecione uma pasta para guardar a primeira revisão dos projetos atuais ou recuperar uma revisão do Lunet já existente. O armazenamento interno original permanece acessível pelo mesmo menu. Para levar as mudanças da cópia de trabalho à pasta, use **Gravar revisão na pasta**. O menu da pasta permite liberar o acesso, selecionar uma revisão anterior e alternar com a cópia local preservada antes da recuperação. Durante a edição, o Lunet grava na cópia de trabalho privada, inclusive quando o provedor ou a rede estão indisponíveis.

Cada revisão é um ZIP completo da árvore de projetos. Inclui manifestos, GameId, fontes, notas e objetos Git. Configurações, chaves, contas e progresso global continuam no armazenamento privado. A exportação ZIP de um jogo é uma ação separada. A cópia de trabalho mantém a estrutura de arquivos que o compilador Roslyn, o Git, os Studios e o importador já usam.

O Lunet solicita as permissões de leitura e escrita pelo seletor do Android e as persiste. Na gravação, inventaria caminhos locais, relê os arquivos contra o ZIP, grava um documento provisório no provedor, confere o SHA-256, renomeia o documento e confere o resultado novamente. A troca da cópia ativa acontece após essa verificação. Na recuperação, confere o SHA-256 antes de extrair em uma pasta separada. Uma versão externa divergente ou a revogação de acesso interrompem a publicação e preservam as cópias. O usuário pode reconectar a pasta, exportar projetos locais ou escolher uma revisão para recuperar.

A gravação é explícita e precisa de espaço para o ZIP temporário e para a cópia de trabalho. O provedor precisa aceitar a criação, releitura e renomeação de documentos. Cada nova revisão ocupa espaço proporcional à árvore completa. As cópias de trabalho são locais e as revisões anteriores continuam disponíveis na pasta. Duas instalações podem gerar revisões concorrentes, pois o contrato Android não oferece comparação e troca atômica entre escritores. Os nomes de revisão são únicos e ambas as versões continuam recuperáveis.

## Implementação e verificação

- [Lunet F2/1](f2-home-continuity-gameid.md) estabeleceu a continuidade por GameId e o tratamento de registros legados.
- [Android, seletor e permissões SAF](https://developer.android.com/training/data-storage/shared/documents-files), [navegação adaptativa Android](https://developer.android.com/develop/adaptive-apps/guides/get-started-with-adaptive-apps) e [Material Navigation Rail](https://github.com/material-components/material-components-android/blob/master/docs/components/NavigationRail.md) orientaram a seleção e a posição dos destinos.
- [VS Code Profiles](https://code.visualstudio.com/docs/configure/profiles) e [Unity Workspace](https://docs.unity3d.com/6000.0/Documentation/Manual/CustomizingYourWorkspace.html) mostram a utilidade de prévias e padrões restauráveis. O Lunet conserva o layout da IDE e as preferências do editor já existentes.
- [Termux](https://github.com/termux/termux-tasker#termux-storage-access-framework-saf) documenta a visibilidade variável de pastas ocultas por provedor. O Lunet usa um documento por revisão para reduzir as chamadas Android por arquivo e guardar a árvore do Git.

Os testes de núcleo exercitam projetos com acentos, assets binários, notas, Git, compilação, importação ZIP, restauração e revisões corrompidas. As capturas da interface incluem primeiro uso e aparência. A operação com o provedor Android e seus limites de tempo e E/S precisam de evidência própria. A validação física depende de relatos de uso e não é etapa de aprovação.

Medição local em Linux, uma amostra por cenário, sem chamadas ao provedor. Um asset de 64 MiB gerou 67.110.765 bytes em 330 ms e foi recuperado em 254 ms. Uma árvore de 2.048 assets de 8 KiB gerou 17.051.201 bytes em 140 ms e foi recuperada em 113 ms. Os tempos cobrem a criação, validação e restauração do ZIP no filesystem local. Downloads, leitura de volta do provedor e transferências entre volumes afetam a duração no Android.
