# UC-16 — adoção proposta do renderer matemático

Esta nota consolida a passagem do probe para uma dependência candidata de produção.
A autoridade arquitetural é o ADR-0012; enquanto ele estiver `Proposed`, o PR
permanece sujeito ao gate crítico de integração.

## Grafo escolhido

`Urbe.Core -> CSharpMath.Rendering 1.0.0-pre.2 -> CSharpMath >= 1.0.0-pre.2`
e `System.Numerics.Vectors >= 4.6.1`.

O Core fixa explicitamente CSharpMath e CSharpMath.Rendering em
`[1.0.0-pre.2]` para impedir que uma restauração futura troque silenciosamente a
versão avaliada. Não há referência a CSharpMath.VectSharp, VectSharp,
VectSharp.SVG, SkiaSharp ou biblioteca matemática JavaScript.

## Licenças auditadas

- CSharpMath / CSharpMath.Rendering: MIT, copyright William Jockusch e Hadrian Tang;
- Typography incluído no renderer: MIT;
- Latin Modern Math incorporada no pacote: GUST Font License;
- Cyrillic Modern e AMS Blackboard Bold incorporadas: SIL Open Font License.

Antes do primeiro artefato C# distribuído (G-C4/UC-27), os textos de licença e
atribuições dos pacotes/fontes devem ser materializados no pacote instalável e na
documentação de terceiros. Esta fatia não distribui o cliente C#.

## Fronteira de segurança

`MathRenderer.RenderSvg` aceita TeX em memória, limita tamanho/fonte, preserva o
texto original e converte falha do parser/renderer em diagnóstico. O backend SVG do
Urbe só emite `path`, `line` e retângulos em um `svg`; não possui IO, rede,
DOM, script ou resolução de URL.

## Lacunas deliberadas

O renderer ainda não declara paridade total com KaTeX. Os comandos já identificados
como incompatíveis e macros são trabalho subsequente da UC-16. Eles não são
reescritos silenciosamente nesta fatia.
