# Cliente C# do Urbe — UC-8

Base de composição aprovada por DEC-0035-A / ADR-0025. O roadmap e os gates
continuam em [`../docs/csharp/ROADMAP.md`](../docs/csharp/ROADMAP.md).
A tela inicial identifica um cliente em construção; ainda não abre nem grava vaults.

| Projeto | Responsabilidade | Dependências locais |
|---|---|---|
| Urbe.Core | Domínio puro .NET; identidade estável, dados nos próximos itens | nenhuma |
| Urbe.UI | Componentes Razor e estilos usados pelos dois hosts | Core |
| Urbe.Web | Bootstrap WASM/PWA estático | UI |
| Urbe.App | Composição MAUI Blazor Hybrid Windows/Android | UI |
| Urbe.Core.Tests | Identidade, limites arquiteturais e composição | Core |

`Urbe.Portable.slnx` compila em qualquer SO com .NET 10; `Urbe.Native.slnx`
exige workloads MAUI. Interfaces de plataforma serão adicionadas quando houver
operações concretas, sem pastas ou contratos vazios. Core/UI não conhecem hosts
nativos ou outros produtos do plano de controle. Nenhuma capacidade de filesystem,
credenciais, IA ou plugins foi ativada.

## Verificação reproduzível

Na pasta `apps/urbe/csharp`, com SDK .NET 10 e Node 22:

```sh
dotnet restore Urbe.Portable.slnx
dotnet build Urbe.Portable.slnx -c Release --no-restore
dotnet test --project tests/Urbe.Core.Tests -c Release --no-build
dotnet publish src/Urbe.Web -c Release --no-restore -o artifacts/web
npm ci --prefix ..
npx --prefix .. playwright install chromium
node tests/web-smoke.mjs artifacts/web/wwwroot
```

O smoke serve os arquivos estáticos publicados, abre a UI real em Chromium,
verifica o ID do Product e a RCL, recarrega offline em `/` e `/preview/` e verifica
que um escopo não apaga o cache do outro. É prova de bootstrap, **não** aceite
completo UC-23/UC-29, instalação PWA ou validação física.

Android (JDK/SDK Android compatíveis com o workload):

```sh
dotnet workload install maui-android
dotnet build src/Urbe.App/Urbe.App.csproj -c Debug -f net10.0-android -p:TargetFrameworks=net10.0-android
```

Windows (Windows + WebView2 + workload):

```powershell
dotnet workload install maui-windows
dotnet build src/Urbe.App/Urbe.App.csproj -c Debug -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0
```

`urbe-checks` executa os checks JS existentes, a solução portátil, testes, publish
estático/smoke e os dois builds nativos, inclusive no ref combinado passado pelo
integrador. Nenhum job publica, assina ou cria release.

## Teste manual desta base

1. Rode `dotnet run --project src/Urbe.Web`; abra a URL indicada.
2. Confirme o título Urbe e o aviso de cliente em construção.
3. Em viewport horizontal de celular, confirme leitura sem rolagem horizontal.
4. Para experimentar offline, use o build **publicado** servido por HTTP local;
   o service worker de desenvolvimento deliberadamente não oferece offline.
5. Não use esta base para notas reais: leitura/escrita são UC-9/UC-10.

Os metadados `app.urbe.csharp.dev` / `Urbe Dev` / `0.0.0` são privados do build
de desenvolvimento; não alteram a identidade ou versão pública, assinatura ou
canais do produto existente. `package.json` continua a autoridade da versão do
Product até UC-32. UC-27 define os metadados de distribuição. Não há pacote de
release nesta etapa. UC-24/25/29 ainda precisam de instalação e aparelho reais.

Bootstrap e worker PWA derivam do template Microsoft .NET 10 (MIT); o worker usa
o próprio escopo como namespace do cache. JavaScript aqui só conecta APIs Web,
sem regras de domínio ou autoridade de dados.
