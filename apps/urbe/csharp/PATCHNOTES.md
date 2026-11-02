# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.45
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial após integração e verificação; versão preparada não significa publicada
**Identidade Android:** app.urbe.csharp e certificado beta existente

## Correção do crash ao enviar mensagens e anexos (CST-013 / UC-63, PR #900)

- Corrigido o caminho de inicialização do serviço Android da IA que causava `ForegroundServiceDidNotStartInTimeException` ao tocar em **Enviar**, inclusive com anexos.
- O serviço é iniciado com o aplicativo visível e promovido no `OnCreate`. Atualizações de aprovação utilizam a instância existente, sem disparar outra inicialização de foreground service.
- Uma execução que termina antes de o Android criar o serviço limpa o pedido pendente. O serviço continua sem reinício automático de agentes após morte do processo.
- Mensagens, anexos, modelos, credenciais, permissões e formato do vault não foram alterados por esta correção.

## Verificação e atualização

- A correção do PR #900 passou no estado combinado com a main: **1.303 testes Core e 465 testes Client**, builds Android/Windows, e2e e smoke Android de abrir/minimizar/retornar.
- O smoke não envia mensagens ao agente. A confirmação de envio de texto, imagem e PDF no Android físico, com notificações concedidas/negadas e minimizar/retornar, continua pendente.
- Esta beta preserva a identidade e o certificado da família beta existente para atualização por cima. Não desinstale para atualizar; mantenha o backup habitual.
- Funcionalidades de leitura imersiva e navegação PDF da beta.44 permanecem incluídas. EPUB/slides/DOCX internos completos, browser/YouTube, anotações semânticas e OCR local continuam pendentes na consolidação.

## Rastreabilidade

- Correção integrada: PR #900, commit `456bf7eb457690cb97a7853b42b3587d99572c4b`.
- Autorização explícita de publicação do proprietário nesta sessão: “Me da a release com isso”.
- Publicação exclusivamente por `urbe-csharp-mobile-release.yml`; conferir os ativos assinados e checksums da tag antes de anunciar a release.
