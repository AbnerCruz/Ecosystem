# Urbe nativo — 2.0.0-beta.27

24/10/2026 · canal beta Android e Windows nativos em C# / Avalonia

## Incluído desde beta.26

- PR #747 — Assistente contextual em botão flutuante discreto e miniassistente para perguntas, aprovações e andamento sem exigir abrir a conversa inteira; aprimoramentos de UI.
- PR #748 — novo espaço de trabalho Grafos: canvas nativo, nós de texto, nota, arquivo, pasta, grupo e forma; vínculos entre nós, rabiscos e operações de edição, incluindo desfazer/refazer; referências ao vault e projeção visual de estruturas e wikilinks.
- PR #748 — arquivos `.grafo.json` e incorporação interativa de grafos em Markdown, com acesso ao editor completo.
- PR #750 — pesquisa web padrão sem cadastrar chave de buscador nem configurar permissões por agente, com `web_search` e `web_fetch`; feed público RSS best-effort; configurações avançadas opcionais para Brave.
- Mantidos: identidade `app.urbe.csharp`, certificado de assinatura do canal beta, vault local como fonte de verdade e demais funções existentes.

## Riscos e verificação

- O feed RSS gratuito não tem API contratada nem garantia de resultados, cobertura, idioma ou disponibilidade. O uso do modelo de IA pode ter custo separado.
- Consulta pública envia os termos da pesquisa ao buscador; abertura de URLs envia solicitações aos sites. Não autoriza transmissão de documentos do vault.
- A decisão de governança DEC-0057 segue registrada como pendente no repositório; publicação da beta não deve ser anunciada como resolução dessa decisão.
- Os testes automáticos e a assinatura do APK precisam ser confirmados pelo workflow `urbe-csharp-mobile-release.yml` sobre a `main` integrada.
- Validação Android físico: atualizar uma beta anterior, abrir grafos, salvar/reabrir, gestos/teclado, pesquisa e assistente contextual. Não declarar validação humana por CI.

## Distribuição

Publicar somente pelo integrador confiável após checks e autorização aplicável, sem criar tags manualmente nem distribuir APK de prévia com identidade diferente. Conferir APK Android, ZIP Windows e hashes SHA-256 da release imutável.
