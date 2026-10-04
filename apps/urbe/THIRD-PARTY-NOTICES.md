# Avisos de componentes de terceiros

O Urbe (© 2026 Abner P. S. Cruz, todos os direitos reservados — ver `LICENSE`) inclui ou usa os componentes abaixo, cada um sob a sua própria licença.
Este arquivo é verificado por `tools/check-license.mjs` (toda dependência declarada e todo diretório de `vendor/` precisam estar listados).

## Incluídos no código-fonte (`vendor/`)

| Componente | Versão | Licença | Onde | Aviso de copyright / texto |
|---|---|---|---|---|
| KaTeX (com fontes) | 0.16.x | MIT | `vendor/katex/` | © 2013-2020 Khan Academy and other contributors — texto completo em `vendor/katex/LICENSE` |
| JSZip | 3.10.1 | MIT ou GPLv3 (opção MIT adotada) | `vendor/jszip/jszip.min.js` | © 2009-2016 Stuart Knightley — https://raw.github.com/Stuk/jszip/main/LICENSE.markdown |
| pako (embutido no JSZip) | — | MIT | `vendor/jszip/jszip.min.js` | © Vitaly Puzrin, Andrei Tuputcyn — https://github.com/nodeca/pako/blob/main/LICENSE |

## Carregados em tempo de execução (não embutidos)

| Componente | Versão | Licença | Uso |
|---|---|---|---|
| PDF.js (`pdfjs-dist`) | 6.3.289 | Apache-2.0 | pré-visualização de PDF, carregado do CDN jsDelivr (`app.js`, `sw.js`) |
| KaTeX (CDN) e Google Fonts | — | MIT / OFL | apenas em páginas HTML exportadas |

## Dependências dos aplicativos instalados (empacotadas nos binários)

| Componente | Licença | Uso |
|---|---|---|
| Electron / Chromium | MIT (Electron) e licenças do Chromium | Windows (`LICENSES.chromium.html` acompanha o instalador) |
| electron-updater | MIT | atualização automática no Windows |
| chokidar | MIT | vigia a pasta do vault no Windows |
| Capacitor (`@capacitor/core`, `@capacitor/android`, `@capacitor/filesystem`) | MIT | Android |
| AndroidX / bibliotecas do Android Gradle | Apache-2.0 | Android |

## Ferramentas de desenvolvimento (não distribuídas)

`electron-builder` (MIT), `@capacitor/cli` (MIT), `playwright` (Apache-2.0).

## Cliente C# em construção (`csharp/`, UC-8)

.NET / ASP.NET Core / Blazor / .NET MAUI (Microsoft e contribuidores, MIT),
com referências Microsoft fixadas no projeto ou fornecidas pelo workload .NET;
xUnit.net v3 (Apache-2.0), somente nos testes. Bootstrap e service workers derivados
do template Blazor WASM PWA do SDK .NET 10 (MIT). Este cliente não é distribuído
pelos canais públicos até UC-31; os avisos de runtime acompanham os futuros builds.
