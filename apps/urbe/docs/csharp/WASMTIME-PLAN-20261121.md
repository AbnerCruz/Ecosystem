# Urbe — plano Wasmtime para execução de código (CST-201–212)

Data: 21/11/2026. Decisão de origem: DEC-0066 alternativa B / [ADR-0051](../../../../docs/adr/0051-urbe-execucao-de-codigo-isolada.md).
Documento de estudo: nenhum código foi alterado. Separação: **FATO OBSERVADO** (verificado nesta data com fonte), **INFERÊNCIA**, **PROPOSTA**.

## 1. Binding .NET do Wasmtime

**FATO OBSERVADO** (NuGet `https://api.nuget.org/v3-flatcontainer/wasmtime/index.json` e conteúdo do `.nupkg` baixado):

- Pacote `Wasmtime` **50.0.1** é o mais recente (versões anteriores no índice: 48.0.2, 44.0.0, 34.0.2, 22.0.0…). Publicado em 06/11/2026; nativos de 29/10/2026. Repositório `bytecodealliance/wasmtime-dotnet`, commit `4aac1a59`.
- Licença: `Apache-2.0 WITH LLVM-exception`.
- TFMs: `net10.0`, `net8.0`, `netstandard2.0`, `netstandard2.1` (`Wasmtime.Dotnet.dll`, ~660 KB). `.nupkg` com 51,9 MB.
- Nativos em `runtimes/*/native`:

| RID | arquivo | tamanho |
|---|---|---|
| linux-x64 | libwasmtime.so | 29,7 MB |
| linux-arm64 | libwasmtime.so | 26,0 MB |
| osx-x64 / osx-arm64 | libwasmtime.dylib | 26,8 / 23,5 MB |
| win-x64 | wasmtime.dll | 21,3 MB |
| win-arm64 | wasmtime.dll | 16,1 MB |

- **`android-arm64` NÃO está no NuGet.** (O `linux-arm64` é glibc; não carrega no Android/bionic.)

**FATO OBSERVADO** (release `v50.0.1` do `bytecodealliance/wasmtime`): o upstream publica `wasmtime-v50.0.1-aarch64-android-c-api.tar.xz` (13,2 MB), com:
- `lib/libwasmtime.so` — 25,8 MB (completo, com Cranelift);
- `min/lib/libwasmtime.so` — **1,23 MB** (build mínimo: sem compilador; só carrega módulos pré-compilados);
- `.a` estáticos correspondentes (58,6 MB / 2,8 MB).

Logo, **não é preciso compilar** o `libwasmtime.so` para Android: basta empacotar o artefato oficial da mesma versão (50.0.1) do binding, como `AndroidNativeLibrary` em `Urbe.Android` (`lib/arm64-v8a/libwasmtime.so`). O binding faz P/Invoke para `wasmtime`, que resolve `libwasmtime.so`.

**Se for preciso compilar** (para cortar mais ou ativar features): crate `crates/c-api` do repositório wasmtime; `rustup target add aarch64-linux-android`; NDK r27 (LTS) com `cargo-ndk` ou `CC/AR/linker` = `aarch64-linux-android24-clang`; `cargo build --release -p wasmtime-c-api --target aarch64-linux-android --no-default-features --features "wasi,cranelift"` (ou `pulley` sem `cranelift`, para interpretar bytecode sem JIT), `profile.release`: `opt-level="s"`, `lto=true`, `codegen-units=1`, `panic="abort"`, `strip=true`. Tamanho esperado (**INFERÊNCIA** a partir dos artefatos oficiais): 1–3 MB sem compilador; 15–26 MB com Cranelift. A lista exata de features deve ser conferida no `crates/c-api/Cargo.toml` da tag usada.

**Ponto de atenção (INFERÊNCIA):** o build `min` exige módulos pré-compilados (`Module.Deserialize`) gerados com a mesma versão e configuração do Wasmtime para aarch64 (`wasmtime compile --target aarch64-linux-android`, ou bytecode Pulley). Isso evita JIT no Android (W^X, SELinux) e reduz o APK, mas amarra o `.cwasm` à versão exata. JIT do Cranelift em Android exige memória executável (mmap PROT_EXEC), que funciona no processo do app, mas deve ser provado no moto g32.

## 2. Runtime de linguagem em WASI

| Candidato | origem do `.wasm` | tamanho | licença | observação |
|---|---|---|---|---|
| CPython 3.13 WASI (`brettcannon/cpython-wasi-build`, release `v3.13.0`, `python-3.13.0-wasi_sdk-24.zip`) | build oficial do CPython (Tier 2 WASI, PEP 11), publicado pelo mantenedor | zip 13,7 MB (wasm + stdlib) | PSF-2.0 | sem threads, sem sockets, sem pip com extensões C; stdlib pura funciona (math, statistics, json, csv, decimal, fractions, datetime) |
| CPython 3.12.0 (`vmware-labs/webassembly-language-runtimes`, tag `python/3.12.0+20231211-040d5a6`) | `python-3.12.0.wasm` (stdlib embutida) | 26,3 MB | PSF-2.0 / Apache-2.0 | arquivo único; projeto sem release desde 2023 (**não adotar como base**) |
| QuickJS via Javy (`bytecodealliance/javy`, `v8.0.0` verificado existente) | Javy gera `.wasm` a partir de JS; plugin QuickJS | o CLI tem 14,3 MB (gz); o plugin QuickJS ~1 MB (**INFERÊNCIA**) | Apache-2.0 WITH LLVM-exception (Javy), MIT (QuickJS) | JS lido de stdin por `javy`-modo dinâmico; início em poucos ms |
| QuickJS-NG compilado com wasi-sdk (`qjs.wasm`) | compilar no CI | ~1–1,5 MB (**INFERÊNCIA**) | MIT | `qjs script.js` com args; stdin/stdout WASI |

Não foi possível listar as releases mais recentes via API do GitHub nesta sessão (acesso à API bloqueado; downloads diretos funcionaram). A versão final de cada artefato deve ser fixada por hash no PR que a introduzir.

**Passagem de dados (todos, via WASI preview1):** `WasiConfiguration.WithArgs(...)` (ex.: `python /work/main.py`), `WithStandardInput(path)` / `WithStandardOutput(path)` / `WithStandardError(path)` (arquivos temporários) e `WithPreopenedDirectory(hostTemp, "/work")`. Python precisa também de `/lib` (stdlib) pré-aberta como somente leitura e `PYTHONHOME` via `WithEnvironment`.

**Início (INFERÊNCIA, a medir):** CPython ~100–300 ms no desktop após o módulo compilado (compilar o `python.wasm` de ~25 MB com Cranelift leva segundos; por isso usar cache `Module.Serialize` em disco ou `.cwasm` pronto). QuickJS < 20 ms. No moto g32 multiplicar por 3–5.

**PROPOSTA:** Python (CPython 3.13 WASI oficial) como linguagem principal — é a linguagem em que os modelos escrevem melhor cálculo e dados; QuickJS como segunda opção leve se o tamanho do APK pesar. Gráficos: o código devolve dados (JSON/CSV) e o Urbe desenha com o renderizador próprio; nada de matplotlib (extensões C).

## 3. Limites no Wasmtime .NET

- **CPU:** `config.WithFuelConsumption(true)` + `store.Fuel = N` (determinístico, custo ~10–30% de desempenho), ou `config.WithEpochInterruption(true)` + `store.SetEpochDeadline(1)` + `engine.IncrementEpoch()` num `Timer` (barato, por tempo de relógio). **PROPOSTA:** epoch com prazo de 10 s padrão e combustível como teto secundário; ao estourar, `TrapException` → resultado "tempo esgotado".
- **Memória:** `store.SetLimits(memorySize: 256 MB, tableElements: ..., instances: 1, tables: 4, memories: 1)` (StoreLimits). Ajustar para o moto g32 (4 GB RAM): 128–256 MB.
- **Arquivos:** só `WithPreopenedDirectory(copiaTemp, "/work")` de uma pasta temporária nova por execução, com a cópia apenas dos arquivos que a execução recebeu; stdlib somente leitura. Nunca pré-abrir o vault. Saídas lidas de `/work/out` e copiadas para o vault só pelo Urbe, com confirmação/gravação verificada. Pasta apagada no fim.
- **Rede:** WASI preview1 do Wasmtime não tem sockets; o binding não expõe rede. Não usar componentes WASI p2 com `wasi:sockets`. Nenhuma função host além de WASI.
- **Outros:** `WithInheritEnvironment` nunca; ambiente vazio; relógio e aleatoriedade WASI permitidos; um `Store` por execução, descartado; execução fora da thread de UI (`Task.Run`) com `CancellationToken` ligado ao epoch.

## 4. Plano de implementação (PROPOSTA)

1. **Urbe.Core** — `Execution/IAssistantCodeRunner.cs`: `Task<CodeRunResult> RunAsync(CodeRunRequest req, CancellationToken ct)`; `CodeRunRequest` (linguagem, código, arquivos de entrada nomeados, limites); `CodeRunResult` (stdout, stderr, arquivos de saída, motivo de término: ok/erro/tempo/memória/indisponível, duração). Implementação nula `UnavailableCodeRunner` (o produto funciona sem). Ferramenta do Assistente `executar_codigo` em `Assistant/` que usa a interface — Urbe.Core não referencia Wasmtime.
2. **Novo projeto `Urbe.Execution.Wasm`** (net10.0, referência `Wasmtime` 50.0.1): `WasmtimeCodeRunner : IAssistantCodeRunner`, cache de módulo serializado por hash+versão, montagem da pasta temporária, limites da §3. Testes headless em `Urbe.Core.Tests`/novo projeto de teste: soma, timeout por laço infinito, estouro de memória, tentativa de abrir `/etc/passwd` ou `../` falha, sem rede.
3. **Desktop (Windows primeiro):** `Urbe.Desktop` referencia `Urbe.Execution.Wasm`; o NuGet já traz `win-x64`. Runtime Python baixado sob demanda (não embutido) para a pasta de dados do app, com SHA-256 fixo; com cache `.cwasm`.
4. **Android:** empacotar `libwasmtime.so` do artefato oficial `aarch64-android-c-api` 50.0.1 (versão casada com o NuGet; teste de arquitetura que confere as duas versões). Começar pelo `lib/` completo (JIT) para provar; medir; avaliar `min/` + `.cwasm` pré-compilado no CI para APK menor. Python como download opcional (~14 MB) na primeira execução.
5. **UI (Urbe.Client):** bloco de execução no chat do Assistente com código, saída e "parar"; sem terminal.
6. **DEVICE:** prova no moto g32 — tamanho do APK, tempo de primeira execução e de execuções seguintes, memória de pico, interrupção. Registrar em `known_issues` o que não foi testado no aparelho.

## 5. Riscos

- Binding sem `android-arm64` no NuGet: solução manual de empacotamento, com risco de divergência de versão entre `Wasmtime.Dotnet.dll` e `libwasmtime.so` (mitigar com teste/verificação de versão no build).
- APK: +25,8 MB com o `.so` completo; +1,2 MB com o `min` (exige pré-compilação). Python +14 MB (download sob demanda).
- JIT no Android: memória executável pode falhar em alguns aparelhos/políticas; o caminho `min` + `.cwasm` (ou Pulley) elimina isso.
- Compilação inicial do `python.wasm` lenta no celular: obrigatório cache serializado.
- Python WASI sem extensões C (numpy/pandas indisponíveis): limitar a stdlib e dizer isso ao modelo no prompt da ferramenta.
- Projeto vmware-labs parado; preferir o build CPython oficial.
- Atualizações do Wasmtime são mensais (major a cada mês): fixar versão e atualizar em itens próprios.
- Segurança depende de nunca expor funções host além de WASI e de nunca pré-abrir o vault.
