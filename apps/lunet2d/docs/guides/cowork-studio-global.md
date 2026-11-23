# IA compacta e Studios sem projeto — incremento 21/11/2026

## O que esta branch implementa
- O botão **✦ IA** no editor abre um compositor pequeno e não modal. Digite e envie sem sair do código. O pop-up mostra o arquivo de contexto e o modo ativo (Auto, Ensinar ou Fazer); **Fechar** oculta o teclado e devolve o foco ao editor sem cancelar a tarefa. Use **Expandir** para o histórico completo, diffs e checkpoints; o serviço de execução existente continua mesmo fechando o pop-up.
- Uma cápsula no rodapé mostra "Trabalhando" e o último passo real. Eventos não abrem nem reconstroem o histórico de Markdown a cada chamada: a conversa completa é atualizada no máximo a cada 400 ms enquanto visível; sincronização do editor durante etapas de escrita em intervalos de 500 ms, e sempre no término.
- Perguntas conceituais curtas (que não se refiram a código/arquivo/projeto, não tenham seleção nem erros/anexos) não carregam o conteúdo de um arquivo irrelevante nem fazem parsing do mapa Roslyn completo. Mudanças de código e perguntas específicas continuam com o contexto integral.
- **Home → Studios** deixa de escolher um jogo. Sprite Studio, Music Studio e Sound Designer trabalham em arquivos reais e persistentes em `FilesDir/StudioWorkspace`, independentemente de projetos.
- O usuário pode manter como rascunho, retomar edição, publicar fonte+derivado na Biblioteca pessoal (versão imutável) e **Usar em…** qualquer jogo por cópia com colisões verificadas. Nada é associado a um jogo automaticamente.

## Limitações honestas
- Tilemap, paleta, partículas e dados ainda precisam de sessão global; os atalhos de criação independente não devem fingir que esses Studios estão prontos.
- A IA ainda utiliza o provedor/modelo configurado; network/provider e geração longa podem continuar lentos. A cápsula exibe **etapas reais**, não raciocínio privado nem percentuais inventados. Ferramentas geradoras de áudio da IA para a biblioteca serão um incremento separado, com permissões e testes.
- O APK Android e a experiência de edição em aparelho não foram validados nesta branch. Não declarar pronto/entregue apenas porque o código foi enviado.

## Teste dirigido
1. Abra projeto e toque em **✦ IA**. Verifique pop-up pequeno, editor visível e campo de texto. Envie um pedido curto. Feche o pop-up: o indicador deve continuar mostrando tarefa. Toque nele para reabrir, ou **Expandir** para revisão. Verifique cancelar e retomar o editor.
2. Pergunte conceito geral ("O que é o SpriteBatch?"), depois analise um arquivo específico; confira contexto correto e diferenças de latência com a build anterior.
3. Volte à Home → Studios **sem projeto selecionado**. Crie um sprite, feche e mantenha no Studio, reabra em **Minhas criações**, publique. Envie para dois projetos distintos e verifique que cada cópia é independente.
4. Crie música, exporte WAV, salve/publicar. Crie efeito, mantenha fonte `.sfx.json` + `.wav`. Teste reiniciar app e funcionar offline.
5. Tente importar um asset com mesmo nome já existente no destino: sem sobrescrita automática. Confira ZIP e APK do projeto.
6. No moto g32, medir fluidez do editor enquanto o agente trabalha, custo de abrir o chat grande e tempo até primeira etapa do modelo. Registrar captura/log e Passou/Falhou por etapa.

## Arquitetura e segurança
- `StudioAssetWorkspace`: pasta comum com caminhos limitados a `Assets/` ou `Content/`, gravação atômica de origem editável e derivado.
- `ReusableAssetLibrary.PublishWorkspace`: versão cópias SHA; não cria `LunetProject`, não usa symlinks e não altera os jogos.
- `LunetAiSession.IsGeneralExplanation`: fast path de contexto sem perder dados necessários para alterações.
- Projeto e biblioteca continuam offline-first, fonte canônica. Nada depende de chave de IA nem upload de assets.

## Evolução necessária
1. Decoupling de todos os outros Studios para a mesma sessão global e exportação SAF direta dos rascunhos.
2. Ferramentas IA para gerar áudio (WAV + `.sfx.json` / `.song.json`), tilemap, palette, particles e data com preflight, checkpoint, review e destino escolhido.
3. Telemetria **local e opt-in** de fases de latência (preparar contexto, aguardar provider, rodar ferramenta, verificar build), sem dados de usuário.
4. Logs/CI e screenshots Android; obter e validar APK canônico, sem alegar release antes.
