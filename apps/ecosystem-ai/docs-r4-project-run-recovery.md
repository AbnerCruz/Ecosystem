# Execuções sem recibo no projeto — P6-5

O processo pode morrer depois de gravar eventos e antes de salvar o recibo no
catálogo. Em **Execuções sem recibo neste projeto**, a tela do projeto consulta
o journal canônico e mostra os runs desse contexto exato que não estão
referenciados por recibo em nenhuma sessão do catálogo.

O inventário é uma projeção efêmera; não cria índice, migra dados, fabrica
recibo ou atribui conversa. Um run pode ter concluído no Runtime e ainda estar
sem recibo. Estado Running/Created também não prova causa, hora de interrupção
ou ausência de outro executor. Abrir a lista não chama modelo, retoma, cancela,
repara ou reenvia nada. Conferir o provedor antes de executar de novo.

A consulta roda fora da thread da interface. Cada arquivo passa pela mesma
validação de envelope, versão, checksum, ordem e replay do LocalRunEventLog.
Nome de arquivo precisa corresponder ao hash do RunId; links simbólicos não
são seguidos. Contextos divergentes dentro de um mesmo run são recusados.
Dados livres de prompts, respostas, tarefas, verificação e exceções não são
exibidos. Journal permanece fora do workspace acessível ao agente.

O padrão examina no máximo 100 arquivos JSON e 16 MiB de corpos lidos, cada
arquivo com o limite existente de 8 MiB. A enumeração é incremental e não
ordena todos os arquivos antes de limitar. Excesso sinaliza **consulta parcial**,
sem afirmar que os runs encontrados são os mais recentes de todo o journal.
O conjunto encontrado é ordenado pelo horário do último evento. Arquivos
inválidos/inacessíveis são contados, preservados e não impedem os válidos de
aparecer. Nenhuma mensagem de erro ou nome de arquivo inválido é mostrado.

Custo exibido é somente o já registrado nas respostas do Runtime. Ausência
significa **desconhecido**, e nenhuma contagem garante a fatura de chamadas
interrompidas. A lista não permite autorizar gasto nem oferece retry automático.

## Evidência e limite

`ProjectRunRecoveryTests` usa arquivos reais: reabertura, ausência de recibo,
isolamento de projeto, exclusão de recibo em qualquer sessão, ausência de
mutação, corrupção, nome incorreto, limites, cancelamento e symlinks. A extração
do decoder conserva a validação única usada por ReadRunAsync e pelo inventário.

**DEVICE pendente:** abrir a lista no projeto, conferir run sem recibo em build
de teste e aviso de dados parciais/inválidos; navegar para outra tela durante a
consulta, rolar resultados longos e confirmar que não inicia chamada/gasto.
Não prova fechamento de P6-5 nem recuperação automática da execução interrompida.
