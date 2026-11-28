# LUNET-546 · Aleatoriedade e geração procedural

A entrega reúne o Random e as correções de estabilidade no PR [#1070](https://github.com/AbnerCruz/Ecosystem/pull/1070). A implementação substitui a sequência anterior sem adicionar uma opção de compatibilidade.

## Relações no Lunet

- Framework contém o Xoshiro256**, SplitMix64, Philox, estado e operações de alto nível. `GameHost` fornece o mesmo contexto de jogo ao Preview, Runtime, servidor e APK. Os serviços registram o fluxo principal por tipo, e o visual tem sua propriedade separada.
- Roslyn compila as formas Amplo, Essencial e explícita. O SDK externo compila os mesmos arquivos físicos de importação. `System.Drawing.Primitives` contém Point e Rectangle. RectangleF e Color continuam usando os tipos do Lunet.
- IDE e assistente recebem as APIs pelo catálogo gerado, manual offline e referência rápida. O template CoinCatcher usa o RNG do jogo. O emissor de partículas mantém seu fluxo explícito. Geração de som e simulação de rede têm streams próprios.
- O exemplo Moedas e sementes testa reprodução de posição, loot, campo indexado e distribuição no círculo. O APK de smoke verifica os vetores no processo do jogo.

## Verificação

- [x] Reproduzir estouro do intervalo inteiro e inclusão do limite superior do float na implementação original.
- [x] Implementar a API sequencial, distribuições, geometria e campo indexado. Usar vetores independentes para Xoshiro, saltos, Fork e Philox.
- [x] Exercitar intervalos extremos, aritmética checked, estado no SaveData, amostragem e permutações pequenas. Validar o compilador do Preview e o SDK externo.
- [x] Compilar e executar o exemplo headless. Verificar igualdade de imagem ao repetir a seed. Atualizar manual, catálogo e ajuda da IA.
- [x] Executar o SmallCrush e PractRand com saída C# na primeira execução. SmallCrush 1.2.3: 15 estatísticas aprovadas. PractRand 0.96: 1 GiB em cada seed 0, 42 e ulong.MaxValue, 227 resultados por execução, sem anomalias na marca final. Projeções de bits altos, baixos e invertidos também chegaram a 1 GiB. As marcações isoladas `unusual` de bits baixos em medidas intermediárias não reapareceram na marca final.
- [ ] Revalidar as baterias estatísticas sobre a reconstrução. O processo anterior de Crush e os arquivos brutos foram perdidos na reinicialização do workspace. BigCrush continua sem resultado.
- [ ] Confirmar pipeline Windows x64, emulador Android, integração e APK assinado. Headless não cobre o Moto G32.

Na primeira rodada, o benchmark antes da reinicialização em .NET 10.0.15 Release JIT Linux x64 registrou mediana de 4,76 ns para uma palavra Xoshiro, 2,21 ns para Xorshift64*, 9,24 ns para duas palavras PCG32, 12,48 ns para 8 bytes de System.Random e 43,21 ns para uma consulta Philox. As amostras tiveram dispersão elevada em VM. A versão foi medida com sete repetições e aquecimento. O contador de alocação não registrou alocação por sorteio escalar, geometria, pesos, embaralhamento ou Philox. Essas medidas não são uma previsão para Android.
