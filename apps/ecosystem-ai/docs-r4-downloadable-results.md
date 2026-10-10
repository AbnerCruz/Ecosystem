# P6-5 — Resultados exportáveis e custos rastreáveis (R4)

O Ecosystem AI já apresenta projetos, sessões, execuções, agentes,
equipes e tarefas. Este incremento torna as **respostas dos agentes
baixáveis no próprio dispositivo** e oferece uma planilha CSV dos
**custos registrados**, sem criar outro mecanismo de geração ou
outra fonte financeira.

## Na UI local

Toda mensagem **do assistente** possui as ações **Salvar .md** e
**Salvar .txt**. São downloads diretos com `Content-Disposition:
attachment`, UTF-8 sem BOM e conteúdo exato **já redigido no
`LocalProjectStore`**. Não há chamada de modelo, nem leitura ou
modificação de arquivos do workspace. São arquivos derivados do
histórico da sessão, não uma alegação de que o modelo criou artefato
no disco.

O painel também apresenta **Exportar registro de custos (.csv)**.
Cada linha corresponde a um `RunReceipt` real do catálogo e inclui:
ProjectId, SessionId, RunId, RecordedAtUtc, Status, Verified,
CostMinor, Currency e CostEstimated. `CostMinor` é a unidade mínima
registrada; em USD equivale a centavos. O CSV não transforma
estimativas em cobranças reais, nem reconcilia faturas do provedor.
O relatório usa RFC 4180 (campos com aspas escapadas e quebras CRLF),
neutraliza fórmulas potenciais em IDs de runs e não inclui
nomes/prompts de usuários.

## Limites e fronteiras

- Disponível somente no servidor `127.0.0.1` já existente, sob a
  verificação de Host anti-DNS-rebinding, CSP e `Cache-Control: no-store`.
  Nenhum JavaScript, sessão externa, serviço de nuvem ou token em link.
- O servidor resolve ProjectId/SessionId/índice contra o catálogo
  canônico. Índices de mensagens do usuário, formatos arbitrários,
  projetos cruzados e valores fora da faixa retornam 404.
- Tipos de exportação permitidos: `text/markdown; charset=utf-8`,
  `text/plain; charset=utf-8` e `text/csv; charset=utf-8`. Nome
  do arquivo é gerado pelo servidor, sem caminhos ou títulos enviados.
- O catálogo já limita cada turno a 16 Ki caracteres. CSV limitado a
  2 MiB para impedir downloads desproporcionais.
- Conteúdos podem ser confidenciais: downloads locais são
  responsabilidade do usuário e do dispositivo, como acontece com
  o próprio catálogo. Não expor porta em interfaces remotas.
- A UI web continua sendo **superfície C# localhost**, não é APK
  Android nem alternativa web pública.
- Nenhum arquivo é criado no workspace pelo servidor; o navegador
  somente recebe bytes para salvar no local que o usuário selecionar.

Testes em `R4ProductDownloadsTests`: HTTP loopback real sem modelos,
integridade do texto Markdown/UTF-8, CSV de receipts com flags
verified/estimated, Host externo, resposta versus mensagem do usuário,
IDs e formatos inválidos, prevenção de CSV formula injection e
workspace intacto.

**P6-5 ainda exige validação real com provedor, experiência nativa
mobile-first e critérios materiais/integração de ChangeSets.** Esta
entrega melhora especificamente os requisitos de artefatos e custos
na experiência do Product, sem declarar gate encerrado.
