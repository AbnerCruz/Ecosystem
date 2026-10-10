# ADR 0008 — Rasterização TrueType portátil em C#

Status: Aceita para o incremento LUNET-430 da Fase 4 (não conclui fontes nem a Fase 4).

## Contexto

O Lunet já usa `SpriteFont.FromBitmap` e `SpriteBatch.DrawString`, mas não converte fontes TrueType reais no aparelho. Qualidade tipográfica importa em telas Android; depender de APIs do sistema ou de fontes instaladas prejudicaria exportação de jogos multiplataforma.

## Decisão

Usar o código-fonte C# de `StbTrueTypeSharp` fixado no commit `5985efcf9ae295508cda180a15dfcf11ba18578d`, em `Lunet.Framework/ThirdParty`, sob domínio público conforme README upstream. Em vez de dependência NuGet, o código é compilado com `STBSHARP_INTERNAL`, preservando a regra de zero PackageReferences do Framework; nenhum tipo de terceiros aparece na API pública. O código unsafe é restrito ao parser interno e ao `TrueTypeFont.Bake`. Glyph sets limitados são rasterizados na carga em atlas RGBA; a fonte renderizável reutiliza a API pública `SpriteFont.FromBitmap`.

`ContentManager.LoadTrueTypeFont` cacheia o arquivo dentro de `Content` e libera texturas no mesmo ciclo dos assets existentes. `TrueTypeFont.Bake` permite bytes vindos de outras fontes e devolve um objeto IDisposable dono do atlas. Código existente de fontes bitmap não é alterado.

## Limites e alternativas

Limites explícitos para tamanho de arquivo, fonte, conjunto de Unicode e atlas protegem a memória. Sem shaping, kerning, hinting, OTF/CFF, fontes de cor, rasterização sob demanda ou integração visual com o Studio. Fontes arbitrárias e fontes do dispositivo não são carregadas implicitamente. O uso de biblioteca managed evita dependência nativa Android/desktop; um backend com FreeType/HarfBuzz poderia ser avaliado depois para texto complexo. CI testa segurança de API e backend, mas legibilidade visual ainda precisa de aparelho.

A escolha é local ao Product, sem contrato novo de plataforma nem edição de projetos salvos.
