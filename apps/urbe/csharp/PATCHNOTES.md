# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.32  
**Canal:** Urbe 2.0 nativo, Android e Windows  
**Publicação:** exclusivamente pelo workflow oficial após integração e gates  
**Identidade Android:** app.urbe.csharp

> Este arquivo acompanha o corte beta.32. Não confirma que a release foi publicada; confira o tag `urbe-csharp-v2.0.0-beta.32` e os assets oficiais. Versões anteriores estão no histórico.

## Edição estável

- Toque, foco e seleção deixam de causar deslocamentos automáticos do viewport da nota.
- Digitação conserva o caret visível sem forçar a linha para o terço superior; gestos manuais de leitura têm prioridade.
- **Selecionar nota inteira**, acessível em Mais ações, sem limitar a seleção ao bloco atual.
- Estatísticas de textos extensos deixam o caminho crítico do teclado; salvamento e conteúdo preservados.

## Chat da IA

- O Assistente respeita a posição de leitura durante o streaming, com comando **Ir à última mensagem**.
- Mensagens próprias podem ser copiadas, ter trechos selecionados e ser editadas para reenviar sem reescrever histórico nem iniciar cobrança automaticamente.

## Compatibilidade e distribuição

- APK nativo Android e ZIP Windows serão publicados pelo mesmo canal e certificado beta anteriores.
- Sem mudanças no formato das notas. Conserve backups antes de atualizar; não desinstale para atualizar.
- Não confundir compilação no CI com teste real do Android ou release pública.

**Detalhes técnicos e testes:** PATCHNOTES-beta.32.md, Issue #805, PR #808.
