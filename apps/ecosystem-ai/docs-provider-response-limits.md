# Limites de resposta do adapter Chat Completions

O adapter existente limita cada corpo HTTP a **2.000.000 bytes**, antes de
interpretar JSON. `Content-Length` maior é recusado antes da leitura; sem esse
header, ou com tamanho subestimado, a leitura termina ao detectar o primeiro
byte excedente. O limite também se aplica aos bytes entregues pelo transporte
após sua eventual descompressão. A decodificação respeita o Content-Type/BOM
como antes. Respostas válidas exatamente no limite são aceitas.

Excesso retorna `response-too-large`. Interrupção de IO, transporte ou timeout
durante a leitura retorna `read-response`, sem copiar mensagens, payloads ou
exceções internas. Ambos são não transitórios: depois do POST, custo e efeito
remotos podem existir, portanto repetir automaticamente pode cobrar novamente.
Cancelamento solicitado pelo chamador continua sendo cancelamento. Recursos
HTTP e streams são liberados também em falhas.

Isso limita a materialização do corpo pelo adapter, não o uso total de memória
do processo nem buffers de handlers customizados. Não implementa streaming de
tokens, não cria provider/Runtime adicional e não altera o ledger. Erro após
resposta remota continua sem custo conciliável; não significa chamada gratuita.

Regressões determinísticas em `ChatCompletionsResponseReadTests`: tamanho sem
header/header incorreto, limite exato, UTF-8 multibyte, recusa antecipada,
IO/transporte/timeout, cancelamento, descarte e ausência de dados privados.
Não usam rede comercial nem validam UI/Android físico.
