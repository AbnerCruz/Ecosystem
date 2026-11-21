# ADR-0051 — Urbe: execução de código isolada por plataforma (CST-201)

## Status

Aceito — decisão explícita do proprietário, DEC-0066, alternativa B (A2 — Wasmtime nativo), 21/11/2026. Nenhum código de execução foi escrito ainda.

## Contexto

O plano Numa 2.6 (§5, CST-201–204, 206–212) pede computação geral como serviço do produto: o Assistente e a pessoa executam código (cálculos, dados, gráficos, conversões) com execuções isoladas por plataforma, sem terminal como requisito. Hoje o Urbe tem cálculo próprio (`Urbe.Core/Calculus`), dados tabulares e skills; não executa código arbitrário.

## Problema

Executar código escrito por um modelo é executar código não confiável. A fronteira precisa impedir, por construção e não por filtro de texto:
- leitura ou escrita fora do que a execução recebeu (o vault, os segredos do app, outros arquivos);
- rede, salvo autorização explícita;
- consumo ilimitado de CPU e memória (o app no moto g32 não pode travar);
- persistência de efeitos depois da execução.

NN-016 (permissões antes da autonomia) e NN-011 exigem decisão explícita. Um interpretador embutido como Jint (JavaScript em .NET) **não é** fronteira de segurança: roda no mesmo processo, com acesso ao que o host expuser, e um erro de configuração vaza o app inteiro.

## Opções

**A. WebAssembly com WASI, no próprio app (recomendada para estudo).** Um runtime Wasm com limites de combustível (CPU) e memória executa interpretadores compilados para Wasm (Python, JavaScript). O módulo só vê os arquivos de uma pasta temporária entregue a ele, e não tem rede. A fronteira é a do runtime Wasm, feita para isso. Funciona offline. **Não verificado:** se o binding .NET do runtime escolhido (por exemplo Wasmtime) publica binários para `android-arm64`; a busca feita em 16/11/2026 não confirmou. Exige prova de conceito no moto g32 antes de qualquer adoção: tamanho do APK (um Python em Wasm tem dezenas de MB), tempo de início e memória.

**B. Processo separado com isolamento do sistema operacional, só no Windows.** AppContainer ou Job Object com limites, um Python ou Node instalado pela pessoa. Fronteira forte no Windows; não existe equivalente prático no Android (não se executa binário arbitrário fora do app). Deixaria o celular sem o recurso.

**C. Sandbox remoto opcional (provedor escolhido pela pessoa).** Execução num serviço externo isolado. Funciona em qualquer aparelho, mas envia código e dados a terceiros e precisa de internet e chave. Só como complemento explícito, como a geração de imagens.

**D. Não executar código; ampliar só o cálculo e os dados próprios.** Sem nova fronteira; não atende CST-201.

## Decisão

**A2 — Wasmtime nativo** (DEC-0066, alternativa B, escolhida pelo proprietário pelo portal em 21/11/2026; [registro](../governance/responses/DEC-0066.md)). O código geral escrito pela IA (CST-201–212) roda num runtime Wasmtime com WASI, limites de combustível (CPU) e memória, sem rede, vendo só a pasta temporária com as cópias liberadas.

- **Android:** o pacote `Wasmtime` para .NET não traz binário `android-arm64`; é preciso compilar a biblioteca C do Wasmtime com o NDK e manter esse binário (dezenas de MB no APK). **Trabalho não iniciado.**
- **Windows:** binários oficiais do pacote.
- **Elemento `code` do vídeo:** continua no Jint (Alternativa E abaixo), só desenho, sem acesso ao sistema; não passa para o Wasmtime.

Proposta original do agente (não escolhida): A1 (WACS) com C como complemento. As garantias gerais continuam: a execução recebe cópias dos arquivos que a pessoa ou a aprovação liberou, nunca o vault; o resultado volta como dado; cada execução aparece nas etapas do Assistente com código, saída e limites; aprovação no modo "perguntar".

Texto anterior da proposta:


A como caminho principal, depois de uma prova de conceito que meça Android e Windows; C como complemento opcional e explícito; B só se A falhar no Windows. Em qualquer opção: a execução recebe cópias dos arquivos que a pessoa ou a aprovação liberou, nunca o vault; o resultado volta como dado; cada execução aparece nas etapas do Assistente com código, saída e limites; aprovação no modo "perguntar".

## Alternativas rejeitadas

- Interpretador no mesmo processo (Jint, IronPython, Roslyn scripting) como fronteira: não isola memória, arquivos nem CPU; rejeitado como mecanismo de segurança.
- Filtrar o código por texto (lista de funções proibidas): contornável; rejeitado.

## Consequências

- Nova dependência nativa e aumento do APK (A) — critério de aceite da prova de conceito.
- Nova ferramenta no catálogo do Assistente, sob as políticas e aprovações existentes.
- Nenhuma mudança de dados do vault.

## Pesquisa de 19/11/2026 (fatos verificados)

- **Wasmtime para .NET não roda no Android.** O pacote `Wasmtime` 50.0.1 traz binários para `linux-x64/arm64`, `osx`, `win-x64/arm64` — nenhum para `android-arm64` (o `linux-arm64` é ligado à glibc, que o Android não tem). Usá-lo no celular exigiria compilar a biblioteca C do Wasmtime com o NDK e manter esse binário: dezenas de MB e uma cadeia de build nova.
- **Existe um interpretador WebAssembly só em C#**: [WACS](https://github.com/kelnishi/WACS) 0.16.14 (Apache-2.0, `net8.0`/`netstandard2.1`, sem código nativo), com WASI Preview 1 (`WACS.WASI.Preview1`) cuja raiz de arquivos é uma pasta escolhida pelo host. Rodaria igual no Android e no Windows, sem aumentar o APK com binário nativo. É interpretado: mais lento que o Wasmtime, adequado a scripts curtos.
- **Intérprete candidato**: QuickJS-ng 0.10.1 publica `qjs-wasi.wasm` (1,4 MB) — JavaScript completo dentro do Wasm. Um Python em Wasm tem dezenas de MB.
- **Ainda não medido**: velocidade, memória e limite de CPU do WACS com o QuickJS no aparelho. Esta sessão não executou o binário baixado (a política de execução do ambiente bloqueou) e nada foi integrado ao app.

Com isso, a opção A passa a ter duas variantes: **A1 — WACS (C# puro)**, igual nas duas plataformas, mais lento; **A2 — Wasmtime**, rápido no Windows, e no Android só com build próprio pelo NDK.

## Decisão registrada

DEC-0066 decidida em 21/11/2026: alternativa B (A2 — Wasmtime nativo). Ver a seção Decisão.

## Alternativa E — interpretador JavaScript em C# (Jint), só para desenho de vídeo

Registro de 21/11/2026, ligado à issue #1004 (o proprietário pediu código livre no vídeo, como no ChatGPT/Claude). **Não é a resposta à DEC-0066**; com a DEC-0066 decidida (Wasmtime para o código geral), o elemento `code` do vídeo continua aqui, no Jint.

- **Escopo restrito:** só o elemento `code` do vídeo. O JavaScript escrito pelo Assistente roda a cada quadro e só desenha. O motor recebe um objeto de desenho (canvas) e os parâmetros de tempo e instância (`t`, `i`, `n`, `u` e afins). Não há acesso ao vault, a arquivos, à rede, a segredos, ao CLR (`AllowClr` desligado) nem a qualquer outra API do host.
- **Limites por execução** (opções do Jint): `TimeoutInterval` (tempo por quadro), `MemoryLimit` (memória alocada), `MaxStatements` (número de instruções) e `LimitRecursion` (profundidade de chamadas). Estourar um limite interrompe só aquele elemento, com erro visível na revisão; o app e os dados seguem intactos.
- **Determinismo:** `Math.random` é substituído por um gerador com semente (da cena/elemento), para que o mesmo quadro saia igual na prévia, no render e na revisão medida.
- **Licença (verificada no nuget.org em 21/11/2026):** `Jint` 4.18.0 (última estável) — BSD-2-Clause; depende de `Acornima` 1.9.1 (parser, sucessor do Esprima no Jint 4.x) — BSD-3-Clause; esta depende de `System.Memory`/`System.Runtime.CompilerServices.Unsafe` (MIT, só em alvos antigos). Todas permissivas, compatíveis com distribuição do app.
- **Android/AOT:** código gerenciado puro, sem binário nativo; não aumenta o APK com bibliotecas por ABI e roda igual no Android e no Windows. Interpretado (sem geração de IL em tempo de execução), compatível com trimming/AOT, com cuidado de preservar os tipos expostos ao script.
- **Limite de segurança reconhecido:** como já dito em "Alternativas rejeitadas", um interpretador no mesmo processo não é fronteira de isolamento geral. Aqui ele é aceitável porque a superfície exposta é só a de desenho e os limites acima cortam CPU, memória e recursão; nada além disso é oferecido ao script.

A execução de código geral pelo Assistente (CST-201–212: arquivos, análise de dados, saída como dado) foi decidida na DEC-0066 (Wasmtime nativo); esta alternativa não a substitui.
