# ADD-0036 — Lunet: o desenvolvimento passa para a casca única em Avalonia

**Data:** 25/11/2026. **Autoridade:** ordem explícita do proprietário nesta conversa. **Registro:** DEC-0068. **Escopo:** Lunet2D (interface, heads e ordem de trabalho). **Precedência:** substitui, onde diverge, o ADR 0012 do Lunet §1 ("nenhuma troca de UI Android sem decisão própria") e DEC-0062-A ("Android nativo não muda agora"); complementa ADD-0034, ADD-0035 e ADD-0035-lunet-sem-espelho.

## Ordem recebida

O proprietário enviou o estudo *"Lunet — o que foi pedido, o que foi entregue e o caminho até o potencial real"* (24/11/2026) e ordenou:

> "Dado o seguinte, assume o desenvolvimento em avalonia."

## O que isso significa

1. **A interface do Lunet passa a ser uma só, em Avalonia** (`apps/lunet2d/src/Lunet.Client`), compartilhada pelas cabeças desktop (`Lunet.Desktop`) e Android (a criar sobre o mesmo `Lunet.Client`). O desktop deixa de ser um app à parte com botões cinza.
2. **Todo trabalho novo de interface do Lunet acontece em `Lunet.Client`.** A casca de Views Android (`Lunet.Android`, `MainActivity.*`) recebe só correções que protejam dados e a distribuição atual até a cabeça Avalonia Android substituí-la; nenhuma tela nova é empilhada nela.
3. **Fundações preservadas, não reescritas (NN-005):** Framework, Compiler, Runtime, Editor (`PieceTable`, `CodeAnalyzer`), Studio, Git, empacotador de APK, Launcher e IA continuam; a casca nova os consome.
4. **Ordem de trabalho:** as ondas do estudo, registradas em [`apps/lunet2d/docs/plans/lunet-casca-avalonia-2026-11-25.md`](../../../apps/lunet2d/docs/plans/lunet-casca-avalonia-2026-11-25.md). Explorar, novos cursos, mapas mentais e novas APIs isoladas do Framework ficam congelados até o portão da Onda 2.
5. **Família com o Numa sem dependência entre Products (NN-002):** o Lunet usa a mesma linguagem visual do Numa (superfícies, espaçamento, raios, tipografia, toque de 48 dp) com acento próprio (violeta). Extrair um `Ecosystem.Ui` compartilhado exige a Extraction Review (NN-022) com dois consumidores reais; até lá os valores são copiados e declarados.
6. **Toda mudança de interface traz capturas** da casca real (headless, celular e largo), geradas no CI. Capturas headless **não** são teste no aparelho (ADD-0035).

## O que não muda

- A distribuição Android atual (`io.lunet.studio`, assinatura, versionCode monotônico) continua até a cabeça Avalonia Android existir e publicar por cima da instalada.
- Dados do usuário: os projetos são as mesmas pastas e formatos; a casca nova lê e grava os mesmos arquivos.
- Estágio da versão continua do proprietário.
