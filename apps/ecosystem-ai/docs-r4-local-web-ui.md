# P6-5 R4 — Painel web local e responsivo (C#)

Slice funcional do Product **Ecosystem AI**. O painel é servido por ASP.NET Core
incluso no .NET 10, restrito a **127.0.0.1**, sem JavaScript, serviços externos,
container, account ou sessão paralela. O código lê o catálogo canônico
`LocalProjectStore` e os POSTs chamam `CliCatalogManagement`.

**Não é APK Android e não libera a P6-5 inteira**: é um host local para a
superfície visual que já permite usar o Product em um navegador **no mesmo
dispositivo que executa o processo**. Mobile-first refere-se ao layout, não
a distribuição nativa.

## Como usar

Com .NET 10 instalado, no diretório `apps/ecosystem-ai`:

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --web-ui --catalog /dados/privados/ecosystem-ai --port 8765
```

Abra **http://127.0.0.1:8765/** no navegador desse mesmo dispositivo.
A porta pode ser omitida (8765). Use Ctrl+C para encerrar.

A tela exibe projetos, sessões, mensagens, receipts, verificação e
custos registrados (em unidades mínimas e por moeda). Permite:
- vincular uma pasta **já existente** a um novo projeto no catálogo;
- criar sessões em um projeto registrado;
- abrir/fechar sessões para ler mensagens e execuções.

Os arquivos reais dos projetos não são alterados por esses formulários.
Para conversar com modelo e executar ferramentas, continue usando
`--chat`/CLI (com as permissões e custos próprios do Runtime). A UI
local **não executa modelo nem gasta créditos**.

## Limites e segurança

- Bind exato `127.0.0.1:porta`, host HTTP estritamente validado contra
  rebinding; não expor em `0.0.0.0`, reverse proxy ou outra máquina.
- Formulários POST exigem token CSRF aleatório de 256 bits em memória,
  com comparação em tempo constante e validação de Origin quando presente.
- Apenas `application/x-www-form-urlencoded`, com Content-Length máximo
  de 32 KiB. POSTs aceitam somente campos esperados; nenhum endpoint de
  grant, gasto, execução ou edição de arquivos foi criado.
- CSP sem scripts/recursos remotos, sem framing, sem cache, sem referer e
  strings de usuário HTML-encoded em todo o painel.
- O catálogo permanece fora do workspace; projetos já existentes são
  verificados na abertura/leitura. Novos vínculos recusam diretórios
  com links simbólicos ancestrais. Não existe endpoint de exportação dos
  arquivos dos projetos.
- O navegador **visualiza as conversas em texto claro**. Só use esta UI
  no próprio dispositivo de confiança; não a publique nem a torne remota.
- As sessões/receipts vêm do mesmo catálogo. O HTML do painel é transitório,
  não cria uma segunda fonte de verdade e não migra dados históricos.

## Testes e critérios de aceite

`R4WebUiTests`: renderização mobile, escaping de texto malicioso, nenhuma
alteração da revisão em GET, HTTP local de ponta a ponta criando projeto e
sessão no store real, arquivos do workspace intocados, recusa de Host
malicioso, Origin externa, CSRF inválido, conteúdo não suportado,
campos estranhos, catálogo dentro do workspace e flags incompatíveis.
Rodar `dotnet test --project apps/ecosystem-ai/tests/AgentRuntime.Tests`
e `dotnet run tests/consistency/Check.cs`.

## Pendências reais da P6-5

Faltam visualização/configuração de agentes e equipes, gestão de tarefas,
execução de agente no UI, secret store e decisão do Host mobile/Android.
Não existe API remota para manipular provider, orçamento nem grant. Não
afirmar que este slice encerra R4.
