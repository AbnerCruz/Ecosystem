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
ativa recebe cancelamento; não há serviço/background nesta prévia. Se a inicialização falhar,
o app exibe um diagnóstico curto (tipo de erro e código), sem mostrar dados privados.

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

## Compatibilidade de inicialização

A prévia usa Mono JIT, com `PublishTrimmed=false`, `RunAOTCompilation=false` e
`AndroidEnableProfiledAot=false`. O build anterior com AOT abortava antes de
`OnCreate`, com SIGABRT e a assertion `klass->instance_size == instance_size`
(reproduzido no emulador Android 13, CI run 38656644999). Um catch C# na Activity
não intercepta esse abort nativo. Os catálogos continuam usando reflexão e os
mesmos formatos; ativar AOT novamente exige repetir o teste de abertura e DEVICE.
A escolha pode afetar o desempenho de inicialização; não houve benchmark no moto g32.

### Diretório privado Android

O Host resolve `FilesDir.CanonicalPath` antes de construir catálogo, journal e
workspaces. O Android pode expor o mesmo diretório por um alias em `/data/data`
ou `/data/user/0`; passar o caminho absoluto sem resolver esse alias fazia o
roster recusar a tela do projeto com “Roster não aceita diretórios simbólicos”.
A proteção dos stores permanece: links em catálogo, roster, locks e workspaces
não são liberados. Não há exceção global para symlinks.

Para projetos criados pela prévia anterior, o Host atualiza somente o caminho
do seu próprio workspace GUID sob o alias privado, após conferir o destino
canônico exato. IDs, sessões, mensagens e arquivos permanecem; nenhum arquivo
é movido e o schema v1 não muda. Caminhos que não resolvem exatamente à pasta GUID privada não são reescritos. A troca
usa lock/revisão/checksum do catálogo e recusa caminho alterado por concorrência.

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

O build no GitHub também executa `tools/smoke-android-preview.sh`: compila o mesmo app Release para x86_64 e o instala
num emulador Android 13 Google APIs x86_64, abre a Activity pelo
launcher e exige processo vivo e tela inicial de projetos. Falha de boot, instalação,
crash ou tela de diagnóstico reprova o teste; logcat acompanha a evidência. O ensaio
exige KVM no runner e usa apenas um emulador novo, sem dados nem provider real.
O ensaio também cria projeto, agente e conversa offline e mata/reabre o processo.
Num emulador descartável com adb root, transforma o diretório privado em link
para o mesmo conteúdo, confirmando abertura do projeto, rebind do caminho e
preservação de IDs/histórico/roster. Essa montagem existe somente no teste;
o APK não altera os aliases do sistema nem recebe root.
O APK distribuído continua arm64; esse ensaio não substitui o teste no moto g32.

Falhas de inicialização da Activity são capturadas numa tela local segura; o código de diagnóstico é derivado do tipo/stack sem exibir mensagem, caminho, conversa ou token. O app não tenta reparar nem apagar o catálogo automaticamente.

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
