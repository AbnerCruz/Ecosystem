# P6-5 R4 — Cadastro e gestão local de projetos e sessões

Incremento funcional do Product Ecosystem AI, sem servidor ou integração com
provedores. Projeto e sessão são registrados no LocalProjectStore já existente
com o mesmo schema, travamento de escritor, checksum e escrita atômica.

Este recurso **não é a interface Android final**. A CLI executa operações
locais reais e oferece um menu de terminal para validar a navegação e a
gestão do catálogo antes de criar uma superfície gráfica com permissões.

## Como usar

No diretório apps/ecosystem-ai, com .NET 10:

~~~bash
# Menu interativo em terminal, sem endpoint, modelo ou token.
dotnet run --project src/EcosystemAi.Cli -- \
  --manage --catalog /dados/privados/ecosystem-ai

# Alternativa direta, útil também para scripts:
dotnet run --project src/EcosystemAi.Cli -- \
  --create-project --catalog /dados/privados/ecosystem-ai \
  --project /pasta/de/codigo/existente --project-name "Meu aplicativo"

# O comando acima retorna o ID estável do projeto. Para criar a sessão:
dotnet run --project src/EcosystemAi.Cli -- \
  --create-session --catalog /dados/privados/ecosystem-ai \
  --project-id ID_PROJETO --session-title "Implementação"

# A consulta anterior permanece:
dotnet run --project src/EcosystemAi.Cli -- \
  --list --catalog /dados/privados/ecosystem-ai
~~~

Menu: listar projetos/sessões, vincular um diretório existente como projeto,
criar sessão e ler o histórico já registrado. A saída via EOF ou opção 0
encerra, sem processos de background.

**Garantias e limites:** criar um projeto não cria, importa, renomeia ou
modifica arquivos dentro de seu workspace. A pasta do projeto já deve existir.
O catálogo dos novos projetos deve ficar fora do workspace acessível ao agente;
diretórios com ancestrais simbólicos não são aceitos nessa operação. Criar sessão
exige que o catálogo e o projeto existam, e não inicia um modelo nem gera
mensagens artificiais. Falhas deixam o catálogo na revisão anterior.
Operações de gestão recusam flags de execução de agente e não demandam chave de API.

Um menu de terminal **não substitui** a UX mobile-first exigida pela P6-5:
ainda faltam edição de projetos, gestão visual de agentes/equipes/tarefas,
escolha das capabilities e execução de tarefas na própria UI. Esses passos
devem consumir os mesmos AgentRuntime e AgentWorkspace existentes, sem
motor de agentes ou ledger paralelo.

Testes de regressão: R4CatalogManagementTests verifica cadastro real,
revisão atômica, workspace intocado, menu, exclusividade das flags,
recusa de catálogo dentro do workspace e EOF sem mutações.
