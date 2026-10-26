# Urbe 2.0.0-beta.32 — Edição sem sequestro da tela

**Estado:** versão preparada para lançamento pelo workflow oficial (release só confirmada quando publicada).  
**Plataformas:** Android (APK nativo assinado), Windows (ZIP portátil).  
**Canal:** app.urbe.csharp, certificado beta inalterado.

## Editor

- A tela não deve seguir o cursor por simples toque, seleção ou foco. Ao digitar, o caret só move a página quando estiver prestes a sair da área visível, e não mais ao cruzar 2/3 da tela.
- A navegação manual com o dedo ou roda do mouse tem prioridade sobre o acompanhamento automático do caret.
- Melhor convivência de teclado, gestos e seleção nativa no modo Visual, preservando os blocos editáveis e a proteção contra toques durante rolagem.
- **Mais ações → Selecionar nota inteira**: seleciona o Markdown completo, em Fonte, e não apenas o bloco sob edição.
- Estatísticas em notas longas atualizadas depois de curta pausa, sem atrasar deliberadamente cada tecla; conteúdo, autosave e histórico continuam intactos.

## Assistente de IA

- Durante a geração, a conversa não força a tela de volta ao fim quando o usuário está lendo mensagens anteriores. **Ir à última mensagem** restabelece o acompanhamento.
- Nas mensagens enviadas pelo usuário: **Copiar**, **Selecionar trecho** e **Editar e reenviar**.
- Editar e reenviar prepara uma nova mensagem no compositor. O histórico anterior não é adulterado: respostas antigas continuam vinculadas ao pedido que realmente receberam. Novo envio só acontece quando o usuário tocar em Enviar.

## Segurança e distribuição

- Nenhuma alteração no formato das notas ou nos modelos de autorização de ferramentas do Assistente.
- A instalação é uma atualização por cima do mesmo canal beta, sem desinstalação e sem substituir o certificado.
- Faça backup das notas e das conversas importantes; dados privados do Assistente podem ser perdidos na desinstalação.

## Verificação

- Regressões de seleção integral, controle do viewport, caret e custo local de digitação adicionadas na suíte C#.
- Builds, CI e artefatos oficiais devem ser aprovados e inspecionados antes de chamar esta versão de publicada.
- Gesto, teclado/IME, seleção nativa, streaming com modelo real e fluidez precisam de validação física Android; build aprovado não é validação em dispositivo.

**Fonte:** Issue #805, PR #808 e `apps/urbe/docs/csharp/EDITOR-INTERACTION-STABILITY-20261026.md`.
