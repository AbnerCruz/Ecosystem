# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.33  
**Canal:** Urbe 2.0 nativo, Android e Windows  
**Publicação:** exclusivamente pelo workflow oficial após integração e gates  
**Identidade Android:** app.urbe.csharp

> Este arquivo descreve a beta.33 preparada. Não é confirmação de publicação: confira se existe a tag `urbe-csharp-v2.0.0-beta.33` com APK Android assinado e ZIP Windows. Histórico em `PATCHNOTES-HISTORY.md`.

## Selos de autoria da IA

- As marcas `{ia:"modelo"}` passam a ser protegidas nos modos Visual e Fonte; exclusões, trocas e falsificações são recusadas.
- A gravação do vault valida a preservação das marcas, inclusive alterações propostas por agentes.
- Desfazer uma operação da IA continua possível quando restaura a versão exata anteriormente registrada.

## Correção de execução dos agentes

- O agente aprende a não tentar remover ou contornar a marca de autoria do Urbe, mesmo quando solicitado.
- `edit_note` aceita substituições vazias legítimas sem confundi-las com argumentos ausentes.
- Uma repetição sem progresso resulta em aviso e chance limitada de correção, sem abandonar imediatamente todas as etapas válidas.

## Funcionalidades preservadas da beta.32

- **Selecionar nota inteira** continua disponível no editor.
- Cópia e edição de mensagens, com leitura estável durante o streaming, continuam disponíveis.

## Compatibilidade e distribuição

- Identidade, assinatura beta e arquivos Markdown existentes inalterados; atualização por cima da beta.32.
- A integridade é garantida nas edições feitas pelo app, não contra alteração direta dos arquivos por aplicações externas.
- São necessários CI, integração aprovada e validação de instalação/interação Android em aparelho. Faça backup antes de atualizar.

**Detalhes técnicos e testes:** `PATCHNOTES-beta.33.md`, Issue #809, PR #810.
