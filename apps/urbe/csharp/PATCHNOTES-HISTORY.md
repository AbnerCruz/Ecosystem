# Urbe — Histórico compacto de versões

A descrição detalhada da versão preparada para publicação fica em PATCHNOTES.md. Ao preparar uma nova versão, transferir a anterior para este histórico em forma compacta. Não confundir PR, build de prévia e release publicada; uma versão preparada pode ainda não estar publicada.

## 2.0.0-beta.49 — 05/11/2026
- Digitação em notas longas mais leve: as alças de seleção de toque escondidas não refazem mais o texto inteiro a cada letra (Fonte ~40% mais rápido).
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.49

## 2.0.0-beta.48 — 04/11/2026
- Aba Estudo: navegador, YouTube oficial e arquivos do vault no painel de fonte ao lado da nota, com divisória arrastável.
- Livros PDF grandes ficam em Livros/ no vault e o Assistente os lê por partes, citando páginas.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.48

## 2.0.0-beta.47 — 04/11/2026
- Recuperação das regressões da beta.38–46: abertura do app mais leve (Assistente e motores de leitura só quando usados), abrir e trocar de nota ~3× mais rápido, índice do vault sem reconstrução a cada gravação (avisos de app sem resposta com a IA).
- Leitores redesenhados para o celular (tela cheia, barra embaixo, toque esconde as barras, pinça no PDF); abas só com duas ou mais; menu radial acima da barra do editor.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.47

## 2.0.0-beta.46 — 03/11/2026
- Painel fixado com escolha de arquivo; leitores de PDF, EPUB, slides, OCR local, navegador de estudo e YouTube; posição de leitura guardada.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.46

## 2.0.0-beta.45 — 02/11/2026
- Corrigido o fechamento do app ao enviar mensagem ou anexo ao Assistente (serviço Android em primeiro plano, PR #900).

## 2.0.0-beta.44 — 02/11/2026
- Leitura imersiva no próprio Urbe, hierarquia de Voltar/Escape e navegação PDF por página no Android.
- Reconhecimento de EPUB, apresentações e documentos com fallback externo explícito; não são leitores internos completos.
- Android e Windows publicados pelo workflow oficial.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.44

## 2.0.0-beta.43 — 02/11/2026
- Leitura documental pelo Assistente: inventário de PDFs, busca por página e texto, inspeção visual autorizada de uma página/imagem com multimodal, sem OCR local.
- Primeira publicação: Android nativo assinado em 02/11/2026; a distribuição Windows utiliza o mesmo tag e fluxo oficial.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.43

## 2.0.0-beta.42 — 02/11/2026
- Correção de Enter/IME multilinha nos editores; indicadores de progresso nativos, numeração de linhas e operações de undo/redo.
- Publicações com fontes por título/bloco, HTML de livros/slides, EPUB3 com validação EPUBCheck e ferramentas do Assistente.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.42

## 2.0.0-beta.41 — 01/11/2026
- Continuidade do planejamento em camadas, validação de evidências declaradas e abas mais compactas.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.41

## 2.0.0-beta.40 — 01/11/2026
- Planejamento adaptativo em camadas: grafo com dependências, critérios de aceite e revisão declarada; correção da perda de contexto na beta.41. Fonte: PATCHNOTES-beta.40.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.40

## 2.0.0-beta.39 — 01/11/2026
- Abas explícitas, editor mais confiável e estúdio de Publicações; PDF pelo diálogo de impressão. Fonte: PATCHNOTES-beta.39.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.39

## 2.0.0-beta.38 — 31/10/2026
- Correção do ciclo de impressão PDF Android e do papel de livros. Fonte: PATCHNOTES-beta.38.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.38

## 2.0.0-beta.37 — 30/10/2026
- Consolidação Numa: editor, permissões, agentes, acessibilidade, notas, tags e interface. Fonte: PATCHNOTES-beta.37.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.37

## 2.0.0-beta.36 — 28/10/2026
- Selos de autoria da IA administrados pelo Urbe, um por bloco, sem loop de remoção (PR #846). Fonte: PATCHNOTES-beta.36.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.36

## 2.0.0-beta.35 — 28/10/2026
- Mapas do Atlas incorporados nas notas, com salvar, descartar ou cancelar antes de navegar, trocar de aba ou voltar (PR #841).
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.35

## 2.0.0-beta.34 — 27/10/2026
- Anexos de texto reais no chat (HTML, código, até 384 KiB por arquivo), Desfazer e Refazer na barra e remoção do selo órfão ao apagar a linha. Fonte: PATCHNOTES-beta.34.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.34

## 2.0.0-beta.33 — 26/10/2026
- Proteção das marcas de autoria por bloqueio de gravação (substituída na beta.36 pelo motor de selos). Fonte: PATCHNOTES-beta.33.md.

## 2.0.0-beta.32 — 26/10/2026
- Edição/viewport estáveis, seleção da nota inteira e chat com posição de leitura preservada; cópia, seleção e edição de mensagens enviadas. Fonte: PATCHNOTES-beta.32.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.32

## 2.0.0-beta.31 — 25/10/2026
- Correção de crash e segundo envio do Assistente, formulários nativos, deduplicação segura de leituras/escritas e eficiência do executor. Fonte: PATCHNOTES-beta.31.md.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.31

## 2.0.0-beta.30 — 25/10/2026
- Release nativa Android/Windows publicada; descrição pública genérica, sem patchnote detalhado individual verificado.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.30

## 2.0.0-beta.29 — 25/10/2026
- Release nativa Android/Windows publicada; descrição pública genérica, sem patchnote detalhado individual verificado.
- https://github.com/AbnerCruz/Ecosystem/releases/tag/urbe-csharp-v2.0.0-beta.29

## 2.0.0-beta.28 — 24/10/2026
- Assistente com atividades organizadas, texto selecionável, clipboard, fontes, evidências, custos e imagens com exportação/zoom.
- Melhorias de retenção do histórico em memória e preservação de rascunhos. Fonte: PATCHNOTES-beta.28.md.

## 2.0.0-beta.27 — 24/10/2026
- Miniassistente contextual, grafos e incorporação de arquivos .grafo.json.
- Pesquisa web padrão sem chave de buscador; limitações do serviço gratuito. Fonte: PATCHNOTES-beta.27.md.

## 2.0.0-beta.26
- Restauração da linha de atividade do Assistente, perdida por regressão na beta.25; evidência no roadmap C#.

## 2.0.0-beta.25 — 24/10/2026
- Correções de foco, teclado e rolagem no editor e diagnóstico de teclado/toque. Fonte: PATCHNOTES-beta.25.md.

## Versões anteriores
- Consultar https://github.com/AbnerCruz/Ecosystem/releases?q=urbe-csharp-v.
- O catálogo remoto do aplicativo mostra as releases nativas com artefatos disponíveis para a plataforma.

