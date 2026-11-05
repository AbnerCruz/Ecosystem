# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.49
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## Escrever em notas longas sem travar

Em notas grandes, cada letra digitada no modo **Fonte** demorava a aparecer. A causa foi encontrada: as alças de seleção do toque (as "gotas" que aparecem para arrastar a seleção) recalculavam a posição a cada letra, **mesmo escondidas**, e para isso o texto inteiro da nota era refeito três a quatro vezes por tecla.

Agora as alças só acompanham o texto enquanto estão na tela. Elas continuam aparecendo e funcionando do mesmo jeito quando você toca no texto ou seleciona um trecho.

Medido no computador com uma nota de estudo de 14 KB:

- **Fonte:** de 127 para 77 ms por letra (cerca de 40% mais rápido).
- **Visual:** de 33 para 23 ms por letra ao editar um bloco.

O celular é mais lento que o computador, então a diferença lá deve ser maior em tempo absoluto. Ainda resta um custo no modo Fonte em notas muito grandes: o campo de texto do sistema refaz o layout da nota inteira duas vezes por letra. Isso só some com um editor que refaça apenas o parágrafo alterado, que é o próximo passo.

## O que ainda falta conferir no aparelho

Os testes automáticos (499 de interface) passam. A digitação no Fonte de uma nota grande e as alças de seleção por toque (aparecer, arrastar, sumir ao digitar) precisam ser conferidas no moto g32.
