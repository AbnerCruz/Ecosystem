# ADR-0051 — Urbe: execução de código isolada por plataforma (CST-201)

## Status

Proposto — aguarda decisão do proprietário. Nenhum código de execução foi escrito.

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

Pendente (proposta abaixo; o proprietário decide).


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

## Decisão pendente

O proprietário escolhe em DEC-0066 (portal). Até a decisão, nenhum código de execução é escrito.
