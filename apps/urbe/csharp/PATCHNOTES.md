# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.44
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial após integração e verificação; versão preparada não significa publicada
**Identidade Android:** app.urbe.csharp e certificado beta existente

## Leitura imersiva no próprio Urbe (CST-231/233, PR #894)

- Visualizador de arquivos com controle **Tela cheia / Sair da tela cheia**: a fonte ocupa a área útil do Urbe sem reabrir o documento, alterá-lo ou substituir a interface nativa.
- No Android, o primeiro botão **Voltar** sai do modo expandido; o seguinte fecha o visualizador. Tecla Escape segue hierarquia equivalente.
- PDFs no Android passam a mostrar posição **página atual / total** e comandos **Anterior / Próxima**. Salto a página física reutiliza o renderizador já existente e mantém somente os bitmaps próximos da viewport.
- Reconhecimento de arquivos **EPUB, PPT/PPTX/ODP, DOC/DOCX/ODT/RTF**, com rótulos corretos e MIME para abertura em aplicativo externo. Arquivos sem leitor interno não são carregados inteiros na memória somente para exibir um aviso.
- O arquivo original do vault permanece intacto; não houve mudança em esquema de dados, permissões ou motor de IA.

## Fundamentos já disponíveis desde beta.43

- Agentes podem inspecionar PDFs autorizados do vault e anexos da conversa: quantidade de páginas, hash SHA-256, inventário paginado de texto, imagens e desenhos vetoriais.
- Leitura e busca textual por páginas, com cursor e identificação de leitura parcial; análise visual de uma página/imagem requer autorização explícita e modelo multimodal configurado.
- A análise de imagens, gráficos e fórmulas geradas ou digitalizadas é interpretação assistida: não representa certificação de exatidão.
- Editor Fonte/Visual, progresso nativo de checklists, numeração de linhas e Publicações com HTML/EPUB3 permanecem disponíveis conforme versões anteriores.

## Pendências ainda abertas na consolidação 2.6

- **Ainda não são leitores internos completos:** EPUB, apresentações e DOCX/ODT usam fallback externo. O PDF no Windows ainda precisa de renderizador próprio.
- O modo expandido ocupa a área do Urbe; não ativa tela cheia do sistema operacional nem oculta suas barras.
- PDF ainda não tem seleção/destaque textual, anotações e referências universais de trechos no visualizador. Páginas são desenhadas como bitmaps; o componente mantém um frame por página e exige evolução para livros extensos.
- Ainda não foram implementados o navegador geral, YouTube embutido, integração simultânea Explorer/editor/navegador e estudo assistido contextual para qualquer formato.
- OCR local offline e verificação matemática independente permanecem pendentes; modelos multimodais precisam de consentimento, revisão e tratamento de mídia.
- Testes automatizados e emulador não substituem teste de toque, bateria, acessibilidade, desempenho e fluxo real no Android físico.

## Estabilização e distribuição

**Pré-requisitos desta beta.44:** os PRs #894 (leitor) e #895 (consolidação 2.6) já estão integrados à `main`. Ainda são obrigatórios CI combinado aprovado no PR de release, autorização do proprietário para a mudança crítica de distribuição e geração dos pacotes Android/Windows exclusivamente pelo workflow assinado `urbe-csharp-mobile-release.yml`. Uma branch de release ou APK de prévia não comprova publicação.

Consulte a consolidação Numa 2.6 (CST-229–246) e os handoffs para o escopo completo de estudo, navegação e produção.
