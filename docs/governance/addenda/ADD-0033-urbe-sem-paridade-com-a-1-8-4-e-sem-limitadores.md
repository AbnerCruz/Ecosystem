# ADD-0033 — Ordem do proprietário: o Urbe não segue mais a 1.8.4, o desenvolvimento não para e a experiência não é limitada sem necessidade

**Data:** 2026-10-29. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0059. **Escopo:** produto Urbe e o cliente C# (`apps/urbe/csharp`), para todo agente presente ou futuro. **Substitui** a parte de DEC-0045/ADD-0022 e de AGENTS.md §1.2 que fazia da 1.8.4-beta a referência de regras e comportamento fora da simulação.

## O que o proprietário disse (com as palavras dele)

> Reparei algumas coisas também, pode esquecer tudo que for legado, não precisa manter nada igual a 1.8.4 o urbe está muito maduro e tem caminhado com as proprias pernas. Segundo, nunca pare o desenvolvimento. Por fim algo importantíssimo, os outros agentes sempre colocan alguns limitadores tentando resolver algum problema, e eu enxergo isso como preguiça e codigo porco. Odeio que a experiência do usuário seja limitada, mas por vezes é necessário ai nesse caso a gente faz o maximo possível para maximizar.

Junto com a ordem vieram o plano de consolidação Numa, o FeaturesCheck e as notas rápidas de outubro atualizados. No plano, a frente "documentos computacionais" (antigos CST-141 a CST-159) foi retirada e substituída por um **novo CST-141**: desenvolvimento de código no Urbe com prévia HTML que acompanha a edição ao vivo.

## Decisões

1. **A 1.8.4-beta deixa de ser referência de comportamento.** Nenhum agente mantém um comportamento, uma interface, um valor ou um limite só porque a 1.8.4 era assim. O Urbe evolui pelo que é melhor para quem usa: as observações do proprietário, o FeaturesCheck, o plano de consolidação e o ROADMAP C#.
2. **Os oráculos da 1.8.4 deixam de ser barreira.** Um oráculo continua valendo como teste de regressão enquanto o comportamento dele for o desejado. Quando uma melhoria diverge dele, o teste muda para o comportamento novo, e a mudança registra o motivo. Não se mantém defeito para passar no oráculo.
3. **Desenvolvimento contínuo.** O agente não para numa entrega: ao fechar um item, segue para o próximo (plano de consolidação, FeaturesCheck, ROADMAP). Interrompe só pelo que depende do proprietário (decisão, material, validação no aparelho) e, nesses casos, continua no próximo item que não depende.
4. **Sem limitadores por conveniência.** Teto de quantidade, tamanho, tempo, itens mostrados ou recursos desligados não é solução para um problema de desempenho ou de robustez. A solução é tornar o caminho rápido e robusto sem limitar a pessoa: paginação, carregamento sob demanda, virtualização, trabalho em segundo plano e cache. Quando um limite for de fato necessário (memória do aparelho, sistema operacional, segurança), ele fica no máximo possível, é explicado no código ou na documentação e não esconde dados.

## O que continua valendo (não é legado)

- **Os dados de quem usa não se perdem (INFERÊNCIA do agente, a confirmar pelo proprietário se ele quiser outro rumo).** Ler um vault da 1.8.4 (`.urbe/mapa.json`, `history.v2.json`, `trash.v2.json`, composições) é proteção dos arquivos da pessoa, não paridade de comportamento: continua. O que muda é o produto, não a obrigação de não apagar nem corromper o que já existe. `.urbe/mapa.json` e `.urbe/mundo.json` seguem intocados (AGENTS.md §1.2).
- **A simulação visual continua fora do produto** (DEC-0051/ADD-0028): é ordem própria do proprietário, não regra de paridade.
- A pilha nativa sem WebView como interface (ADR-0032), a Área de trabalho (DEC-0053), os gates, a integração governada, a validação no aparelho (NN-017) e as invariantes do MANIFEST.

## Aplicação

- AGENTS.md §1.2 ("Paridade fora da simulação") e `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md` passam a apontar para este registro.
- Documentos que ainda citam a 1.8.4 como referência são histórico. Ao tocar uma área, o agente segue esta ordem, sem reabrir a discussão.
