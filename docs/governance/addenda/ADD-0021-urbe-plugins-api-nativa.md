# ADD-0021 — Ordem do proprietário: plugins do Urbe nativo terão uma API nativa em C#

**Data:** 2026-10-11. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0044. **Escopo:** produto Urbe e o cliente C# (`apps/urbe/csharp`). **Arquitetura derivada:** [ADR-0033](../../adr/0033-urbe-plugins-api-nativa.md) (Proposto).

## O que o proprietário disse (fato, com as palavras dele)

Diante das três alternativas registradas em DEC-0044 (1: só gerenciamento; 2: API nativa de plugins em C# com permissões e ADR; 3: motor de script isolado para os `.js` da 1.8.4), respondeu: *"Quanto a decisão, é a opção 2. API de Plugin nativo"*.

## Decisão consolidada

1. O Urbe nativo terá **plugins por uma API nativa em C#**, com **permissões declaradas e deny-by-default** (NN-016).
2. Os plugins `.js` da 1.8.4 **não serão executados** no app nativo: não haverá motor JavaScript. Quem quiser um plugin no nativo o escreve para a API nova.
3. Isto é **decisão de direção**, não autorização de implementação: a API só será construída depois de o **ADR-0033** ser aceito (NN-011), em itens próprios do ROADMAP C#.

## O que não muda

- Hoje a Personalização nativa só lista, aprova (SHA-256), cria e liga/desliga plugins, e avisa que não os executa. Esse comportamento continua até a API existir.
- A 1.8.4-beta publicada continua intacta, com seus plugins `.js`.
- Sem Hub, sem dependência cruzada com o Lunet2D (NN-002, NN-003); qualquer abstração compartilhada continua sujeita a NN-020/NN-022.
- PRs críticos continuam dependendo da autorização de integração do proprietário (ADD-0012/ADR-0015).

Referências: DEC-0044; ADR-0033; ADR-0032; `apps/urbe/src/customize/plugins.js`; `apps/urbe/docs/csharp/ROADMAP.md`. MANIFEST NN-006, NN-009, NN-011, NN-016, NN-020, NN-022.
