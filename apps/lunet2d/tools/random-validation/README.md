# Validação da saída C# do Random

A partir de `apps/lunet2d`, compile o executável de teste. O benchmark usa aquecimento, sete medições e um acumulador impresso. Os geradores de comparação vivem neste harness.

```bash
dotnet build tools/random-validation -c Release
harness=tools/random-validation/bin/Release/net10.0/RandomValidation.dll
DOTNET_TieredCompilation=0 dotnet "$harness"
dotnet "$harness" stream 42 raw | RNG_test stdin64 -tlmax 1GB -tlmaxonly
dotnet "$harness" stream 42 reverse | RNG_test stdin64 -tlmax 1GB -tlmaxonly
dotnet "$harness" stream 42 upper | RNG_test stdin32 -tlmax 1GB -tlmaxonly
dotnet "$harness" stream 42 lower | RNG_test stdin32 -tlmax 1GB -tlmaxonly
```

Use também seeds 0 e 18446744073709551615. O modo `stream` escreve palavras little-endian até o leitor terminar. `upper` e `lower` emitem uma metade de 32 bits por palavra nova. `reverse` inverte a ordem dos bits em cada palavra.

Para TestU01 em Linux x64 little-endian, construa as bibliotecas estáticas da distribuição em `testu_src`.

```bash
cc -O2 -I"$testu_src/include" tools/random-validation/testu01-stdin.c \
  "$testu_src/testu01/.libs/libtestu01.a" "$testu_src/probdist/.libs/libprobdist.a" \
  "$testu_src/mylib/.libs/libmylib.a" -lm -o /tmp/lunet-testu01
dotnet "$harness" stream 42 raw | /tmp/lunet-testu01
dotnet "$harness" stream 42 raw | /tmp/lunet-testu01 crush
```

Para verificar a aritmética checked, construa o projeto com `-p:CheckForOverflowUnderflow=true --artifacts-path /tmp/lunet-checked` e execute o binário desse diretório com `check`.
