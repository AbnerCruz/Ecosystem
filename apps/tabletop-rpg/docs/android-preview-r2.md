# RPG-020 — Escolha do shell Android nativo C# e preview instalável

## Decisão de implementação — validação no celular

O Tabletop RPG necessita de APK físico para obter validação real do jogador.
Para RPG-020, foi escolhido o **shell Android nativo em C# / .NET 10
(`net10.0-android`)** e layout Android `Activity`/views, sem WebView, JavaScript,
servidor ou camada híbrida. A engine C# existente permanece independente da UI.
O identificador Android é `app.tabletoprpg.preview`, a fim de não substituir
outros Products ou versões futuras.

A escolha é local ao Product e documenta uma **prévia de RPG-020/021**;
não altera contratos de outro Product ou o manifesto. Mudança para
outro shell ou extração compartilhada exige revisão de arquitetura.

### Alvos

- Android arm64, API 29+, prioridade em aparelho físico e toque.
- Builds headless continuam `net10.0`; o Android só referencia
  `TabletopRpg.Core`, `TabletopRpg.Persistence` e
  `TabletopRpg.QuickStart`.
- Publicação de **APK de desenvolvimento** via GitHub Actions
  `.github/workflows/tabletop-rpg-android-preview.yml` da branch isolada,
  após `dotnet test`, `dotnet workload install android`, build e
  verificação da assinatura. A Action anexa o APK como artifact e publica
  prerelease numerada na página de releases do repositório.
- A assinatura Debug neste primeiro preview é efêmera no runner:
  a atualização entre builds pode exigir desinstalar, com perda de dados.
  Para beta distribuível, preparar uma chave de desenvolvimento estável
  versionada/separada (aprovação e verificação de certificado), sem
  reutilizar a chave de outro Product.

### Fluxo funcional do APK

1. Tela inicial com três temas de `QuickStartFactory` e lista das campanhas
   existentes armazenadas no dispositivo.
2. Criar campanha gera de imediato um mundo/sessão válidos, um
   humano, um agente de companhia, Mestre e NPC, missão, cena e recursos,
   sem chamadas LLM ou pagamento.
3. Tela de sessão exibe local, missão, personagem/vida e opções de jogo.
4. Fala do humano usa `SpeakIntent` autorizado no `SessionEngine`;
   percepção/força usam `SkillCheckIntent` e rolagem d20 real.
5. História de ações e resultados, com gravação em seguida a cada
   ação, encerramento e reabertura de sessão.
6. Persistência: `FileCampaignStore` v2 existente no diretório privado
   do app, com gravação atômica e backup recuperável. Um metadado
   `.ui.json` sidecar guarda somente IDs estáveis para identificar o
   personagem humano no shell; dados da campanha nunca têm segundo
   esquema de verdade.

### O que NÃO está nesta prévia

- Conexão real de provedor, Player Agent/Mestre IA ativo ou respostas de
  NPC. Os assentos existem, mas o app **não simula** modelo inexistente.
- Ficha detalhada, inventário editável, diário, rolagem customizada,
  UI completa RPG-022/023/024, lifecycle/DEVICE RPG-025.
- Multiplayer, rede, conta, Internet permission, exportação por tela,
  atualização automática ou release de produção.
- Gate G-R1 exige mesa solo real com agentes conectados; G-R2 exige
  experiência mobile testada ponta a ponta. APK de preview **não** significa
  que os gates foram aprovados.

### Testes reais no Android

1. Instalar o APK da prerelease em Android arm64 API 29+.
2. Abrir sem rede e criar cada tema; verificar cena, objetivo, personagem.
3. Falar, lançar d20 de percepção/força, encerrar a sessão.
4. Fechar o app, reabrir e retomar a campanha; conferir eventos.
5. Fechar forçadamente e reabrir; verificar que dados persistem.
6. Registrar crash, toque impreciso, teclado encobrindo campos e
   deslocamento/rotação. Não declarar DEVICE validado até teste físico.

O projeto não usa modelos nem redes por padrão; operação pay-as-you-go e
modelo/provedor agnóstico permanecem decisões para o host apropriado,
sem teto financeiro obrigatório e sem polling quando IA for conectada.

**Dependência de integração:** PR #526 (RPG-016), atualmente stacked
sobre as etapas R1 anteriores. O shell fica em branch/PR separado para
teste antes de promover à main.
