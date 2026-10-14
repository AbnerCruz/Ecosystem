# Ecosystem AI — cliente Android nativo de prévia

Head C# `net10.0-android`, Android 10+ / arm64. Sem WebView, JavaScript,
servidor local ou SDK .NET instalado no aparelho. `EcosystemAi.Product` é
composição de Host: usa o mesmo `AgentWorkspace.WorkspaceSession`,
`AgentRunner`, ledger, provider adapter, `LocalProjectStore`, roster,
planejamento, pareceres e `LocalRunEventLog`. Não é outro Runtime.

## Uso

1. Instale `Ecosystem-AI-Android-Preview.apk` e crie um projeto.
2. Adicione arquivos textuais UTF-8 de até 64 KiB pelo seletor Android.
   São cópias no projeto privado; nome já existente é recusado, não sobrescrito.
3. Crie uma conversa. Agentes e equipes podem ser cadastrados no projeto,
   sem modelo nem gasto. Planejar/cancelar tarefas também funciona offline.
4. Em **Configurar IA e orçamento**, informe endpoint HTTPS completo de
   Chat Completions compatível, modelo, chave e os preços do provedor.
   Informe limites por execução/chamada e autorize o teto desta instância.
5. Envie uma tarefa. Opcionalmente declare um trecho exato obrigatório:
   o verificador confere o texto devolvido, não a declaração do modelo.
6. Selecione uma equipe para executar produtor e revisor diferentes, com
   duas cotas completas. Solicitação, critério e resposta chegam integralmente
   ao revisor; se não couberem no limite, não há parecer parcial nem retry.
7. Leia os recibos e salve respostas em `.md` pelo seletor de destino Android.
   Pareceres podem ser aceitos/rejeitados com justificativa; isso não integra
   arquivos nem significa aceite técnico do projeto inteiro.

Somente **Enviar/Executar agora** chama modelos. Nenhuma execução em GET,
idle, abertura do app ou criação de projeto. Ao sair da Activity, a execução
ativa recebe cancelamento; não há serviço/background nesta prévia.

## Dados, custos e segredos

- Catálogo v1, roster, planejamento, pareceres e journal usam os formatos
  atuais do Product, sob armazenamento privado do aplicativo. Os workspaces
  ficam separados dos registros, sem acesso do agente a catálogo ou journal.
- O token vive somente em memória. Não vai a SharedPreferences, Bundle,
  catálogo, journal ou prompts. O redactor do Runtime continua ativo.
  Capturas/snapshot do SO são desabilitados na Activity. Endpoint/modelo
  podem ser lembrados; chave e autorização financeira precisam ser dadas
  novamente após a morte do processo. Backup automático Android está desabilitado.
- O orçamento da instância reserva o teto inteiro de cada run. Não devolve
  reservas nem é teto distribuído entre processos. A autorização nova no
  diálogo é explícita; não existe renovação automática.
- Preço informado e ausência de usage do provedor podem gerar estimativa.
  Os recibos mostram **valor registrado**, não garantia da fatura. Ausência
  de resposta do provider é marcada estimada; não equivale a prova de custo zero.
- Agentes só recebem `files.read`. Importação/exportação são gestos do usuário,
  não concessão de `fs.write` ao agente. Sem apagar/sobrescrever arquivos.
- Presença de resposta verifica estrutura. Trecho exato verifica somente
  correspondência textual; não prova verdade, qualidade, testes executados,
  cumprimento de critérios arbitrários ou integração de ChangeSets.
- Não há restauração automática de um run após processo morto. O journal
  preserva a execução incompleta para auditoria; não reenviar sem conferir.

## Identidade e assinatura da prévia

Package de teste separado `io.ecosystem.ai.preview`; versão deriva de `VERSION`
e do run de CI. A chave `tools/ecosystem-ai-preview.keystore` é **pública,
apenas de desenvolvimento**, com senha pública `android` e alias
`ecoaipreview`. Não usar para uma distribuição de produção ou confiança IPC.
A assinatura estável permite atualizar esta prévia preservando seus dados.
Uma assinatura de produção futura requer seu próprio corte/decisão.

O workflow `ecosystem-ai-android-preview` apenas compila/verifica e anexa
artefatos de CI (30 dias); não publica release nem instala nada no aparelho.
O CI do Product usa o mesmo script de build para verificar o estado combinado.
O versionCode de teste deriva do minuto UTC da build, permitindo atualizações
entre os dois workflows; a versão pública deriva de VERSION e do run de CI.

## Verificação e gate

`R4NativeProductTests` prova composição real com HTTP mockado, leitura de
arquivo, journal/reabertura de catálogo, critério material de texto, recusa
quando o modelo mente, isolamento, custo/limites, cancelamento, concorrência,
produtor/revisor e decisão humana sobre parecer. Não é ensaio de provider
comercial nem DEVICE.

Roteiro em aparelho para o APK exato:

- **A:** instalar; criar projeto/conversa, importar um `.md`; fechar/reabrir,
  confirmar arquivo, conversa e agentes/equipes.
- **B:** configurar provider real; enviar uma tarefa de leitura; conferir
  resposta, custo registrado, modelo escolhido e ausência da chave nos exports.
- **C:** exigir trecho presente/ausente e conferir diferença de verificação;
  selecionar equipe e confirmar dois agentes/runs e parecer pendente.
- **D:** sair durante um run e conferir cancelamento/auditoria ao retornar;
  matar o processo e confirmar que não reinicia chamadas nem conserva token.
- **E:** exportar `.md`; abrir no editor escolhido e comparar conteúdo.
  Atualizar APK com mesma assinatura e confirmar que dados permanecem.
- **F:** testar layout/toque em retrato e paisagem, teclado, rolagem e
  conforto no moto g32 Android 13.

P6-5 só pode fechar com os requisitos completos e a evidência apropriada.
Este APK não prova integração real de ChangeSets nem conclui a Fase 7.
