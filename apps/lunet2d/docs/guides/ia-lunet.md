# IA do Lunet — integração ao Agent Runtime compartilhado

## Estado e escopo (01/11/2026)

Esta implementação inicial habilita um agente de código no **Lunet IDE Android**. O
**Lunet Framework** e os jogos gerados não dependem de IA: nenhuma dependência
de IA foi adicionada ao Framework/Runtime do jogo.

A IA do editor de notas possui funcionalidades próprias de vault, porém os seus
contratos de ferramentas de notas não devem ser exportados diretamente. Copiá-los duplicaria
manutenção e transportaria contratos incorretos para o Lunet. Por isso o
núcleo compartilhado selecionado é o **AgentRuntime.Core** canônico,
com o provider **`AgentRuntime.Providers.ChatCompletions`**. O produto Lunet
implementa somente as ferramentas específicas do projeto em `Lunet.Ai`.

## Funções desta versão

- Abrir projeto → **⋯ → Ferramentas → IA · Assistente de código**.
- Configurar endpoint completo OpenAI-compatible terminando em
  `/chat/completions`, ID do modelo e chave do provider. **HTTPS obrigatório**,
  sem parâmetros de URL, fragmentos ou credenciais embutidas. Padrão: endpoint
  OpenRouter. Outros provedores que realmente ofereçam esse protocolo também
  funcionam; não confundir compatibilidade de rede com capacidades do modelo.
- Conversar sobre arquivos C# e dados de projeto. As seis capacidades são:
  `lunet.files.list`, `lunet.files.read` (texto e SHA256),
  `lunet.files.search` (busca limitada e posições verificáveis),
  `lunet.files.excerpt` (linhas paginadas de arquivo grande, SHA256 do arquivo inteiro),
  `lunet.files.write` (gravação atômica condicionada ao hash) e
  `lunet.files.replace` (substituição de um trecho exato único sob o mesmo hash).
  Arquivos de configuração C# em texto como `.csproj`, `.props` e `.slnx`
  também são elegíveis, preservadas as regras de permissões e isolamento.
- **Somente leitura por padrão**; autorizar escrita por caixa de seleção
  em cada abertura do painel.
- Escrita de textos usa gravação atômica, limita tamanho e recusa caminhos
  absolutos, `..`, pastas ocultas, binários e alteração do manifesto do jogo.
  Se outro processo alterar arquivo depois da leitura, a IA não o sobrescreve.
- Histórico textual por projeto gravado em área privada do app, separada
  da pasta do jogo. A chave **não é persistida**; precisa ser informada
  ao reiniciar o processo. A chave é registrada no redator de segredos do
  runtime e a interface avisa que trechos lidos podem ser transmitidos
  ao serviço externo de IA escolhido.
- Cancelar execução. A UI não presume compilação nem prova correção de
  programa a partir de uma resposta textual: verificação atual somente
  confirma a resposta e o hash dos arquivos gravados.

## Limites declarados

Não foram transferidos para o Lunet o painel inteiro do editor de notas, a memória
semântica, pesquisa web, geração de imagens/vídeos, agentes especialistas,
edição de sprite por IA, testes automatizados executados pelo agente, nem
ferramentas de build. A edição é inicialmente de arquivos de texto. O
provider precisa estar acessível; esta versão não integra execução de
modelo no dispositivo. Não foi implementado catálogo de preços do provider:
não interpretar os valores internos do ledger como custo real de API.
A aprovação atual protege a habilitação de escrita por sessão, não oferece
um diff visual de cada alteração. **Usar somente em projetos com backup**.
A substituição por trecho não cria um commit Git automaticamente e a
verificação de hash não substitui compilação, testes, revisão humana ou
controle de versão.

## Limite de autoridade e unificação

**Proibição do proprietário em 01/11/2026:** nenhum arquivo, configuração,
workflow ou comportamento do editor de notas pode ser alterado nesta iniciativa. A integração
usa os contratos do runtime genérico existentes; extensões especializadas vivem
exclusivamente no Lunet. A cópia compilada do núcleo é **uma projeção validada
byte a byte** do fonte canônico, e não uma segunda implementação independente.
O CI bloqueia divergências entre projeção e origem.

Isso **não** significa que toda a IA do editor de notas agora utiliza o mesmo núcleo:
componentes históricos continuam separados. Migrá-los ou alterá-los exige
autorização futura explícita. Fora do editor de notas, a arquitetura deve priorizar
capabilities compartilháveis no runtime canônico, mantendo só adapters
e ferramentas de projeto dentro dos aplicativos.
Dependências externas não são embutidas nos APKs dos jogos.

## Referências analisadas

- [Microsoft Agent Framework: samples C#](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples):
  desacoplar o agente de provider, capacidades e UI; aprovações e persistência.
- [Aider](https://github.com/Aider-AI/aider): inventário de código, leitura
  contextual, edição rastreável e validação; não copiar sua arquitetura Python.
- [Continue](https://github.com/continuedev/continue): separação do editor e
  ferramentas do agente (repositório read-only em 2026); não copiar UI/web.
- AgentRuntime compartilhado (gerado automaticamente e validado por projeção):
  políticas de execução, permissões e verificação existentes, portanto
  reutilizados diretamente.

## Plano de verificação

1. `dotnet test --project tests/Lunet.Tests`: testes de caminhos, comparação
   de SHA256, alterações concorrentes e arquitetura.
2. Workflow de PR `lunet2d-release`: compila APK e executa os testes e
   os gates existentes de exportação no emulador.
3. DEVICE: instalar a build no celular, abrir um projeto, conversar com
   provider de teste, confirmar leitura sem escrita, criar/alterar código
   com autorização, testar cancelamento e memória após reiniciar app,
   verificar reabertura do editor e execução de jogo.

CI verde não equivale a teste do provider real ou validação DEVICE.
