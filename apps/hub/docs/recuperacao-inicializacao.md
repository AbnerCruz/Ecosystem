# Hub Android — recuperação de falhas gerenciadas na abertura (HUB-205)

O Hub não pode apresentar uma falha de leitura do cache/Registry como instalação
autorizada. Caso uma exceção **gerenciada** interrompa a montagem inicial da
Activity ou o início do lifecycle, o cliente bloqueia os controles de
instalação/abertura, cancela a atualização e substitui a tela incompleta por
uma página nativa de diagnóstico.

A tela mostra apenas uma categoria genérica e um identificador SHA-256
truncado do tipo/HResult/stack da exceção. **Não exibe mensagem original,
caminhos de armazenamento, conteúdo de cache, token, arquivo ou stack trace.**
A ação **Tentar novamente** recria a Activity sem apagar dados nem reabrir
automaticamente o fluxo de instalação. Repetição de falha continua visível
para o operador.

O erro não altera o Registry, a assinatura, o download, o PackageInstaller,
as sessões IPC nem o armazenamento de outros Products. Este fallback é apenas
de apresentação/controle de inicialização: não recupera gravações inconsistentes
nem confirma integridade de estado. Não apaga cache automaticamente.

## Limites e testes

- Testes determinísticos em `Hub.Tests`: códigos opacos, mensagem privada não
  exposta, seleção de explicação genérica e identidade estável para o mesmo erro.
- CI `hub-ci`: Core + compilação do APK Android, além de consistency.
- **DEVICE pendente (NN-017)**: abrir o Hub normalmente, sem rede e com rede;
  validar retorno após interrupção de rede e lifecycle; em APK de teste, injetar
  uma falha gerenciada na fase de montagem (sem mexer em usuário real) e
  verificar que a tela de diagnóstico aparece e o botão reinicia a Activity.
  Verificar que nenhum botão de instalação/abertura fica acessível nesse estado.
- Um **SIGABRT nativo antes de `OnCreate`** não passa pelo `catch` de C#.
  Nesse caso coletar `adb logcat` e examinar a pilha nativa separadamente.

Sem stack trace do relato original de fechamento imediato do Hub, esta
mudança não deve ser apresentada como correção comprovada daquela causa.
