# Narração neural — primeira medição (#1004, adendos)

**Data:** 20/11/2026 · **Onde:** servidor x64 do agente, 2 threads, sem GPU. **Não** é o moto g32 de referência nem um Android.

## O que foi testado

Motor: [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) pelo pacote NuGet oficial `org.k2fsa.sherpa.onnx` (há runtime para `android-arm64`, `android-x64`, `win-x64`, `win-arm64`, `linux-x64`). Frase pt-BR com números, ano, unidade e fórmula falada (cerca de 13,5 s de áudio):

> A energia cinética de um corpo de 2 quilogramas a 3 metros por segundo vale 9 joules. Em 1905, Einstein mostrou que E é igual a m c ao quadrado; a luz, por sua vez, viaja a cerca de 300 mil quilômetros por segundo.

| Modelo | Voz | Tamanho baixado | Síntese | RTF | Pico de memória |
|---|---|---|---|---|---|
| Kokoro-82M v1.0 int8 (multilíngue) | `pf_dora` (id 42) | 132 MB (183 MB extraído) | 12,3 s | 0,92 | 419 MB |
| Kokoro-82M v1.0 int8 | `pm_alex` (id 43) | — | 12,8 s | 0,94 | 420 MB |
| Piper `pt_BR-faber-medium` (VITS) | faber | 67 MB (79 MB extraído) | 0,96 s | 0,07 | 311 MB |

RTF = tempo de síntese ÷ duração do áudio (abaixo de 1 é mais rápido que a fala). As amostras WAV foram entregues ao proprietário para escuta. **A naturalidade não foi avaliada:** o agente não ouve áudio.

## Achados

1. **Licença do fonemizador (decisão do proprietário).** O sherpa-onnx 2.0 removeu o espeak-ng embutido: em pt-BR, a versão 2.0.2 falha ("No lexicon available" no Kokoro; "phoneme_codepoints is empty" no Piper) e pede um fonemizador externo. A 1.13.8 ainda embute o espeak-ng e funciona. **O espeak-ng é GPL-3.0**, assim como o `piper_phonemize`. Para distribuir narração pt-BR no APK por esse caminho, o app passa a incluir componente GPL. A alternativa é um fonemizador pt-BR com licença permissiva (léxico próprio, regras ou G2P neural), que precisa ser encontrado ou feito.
2. **Kokoro com léxico inglês corta palavras em português.** Com `lexicon-us-en.txt`, palavras portuguesas ficaram de fora ("OOV word skipped"), e o áudio saiu com 5,6 s em vez de 13,4 s. Em pt-BR o léxico não deve ir junto.
3. **Custo no celular (estimativa).** O Kokoro já fica perto do tempo real num servidor x64. Num Android modesto deve ficar algumas vezes mais lento que a fala. Serve para narrar um vídeo em segundo plano, cena a cena, mas não para leitura instantânea. Isso precisa ser medido no aparelho. O Piper deve ficar perto do tempo real ou abaixo dele.
4. **Licenças dos pesos.** Kokoro-82M: Apache-2.0 (arquivo `LICENSE` do pacote). Voz Piper faber: dataset CC0 (`MODEL_CARD`). O Piper original (OHF-Voice/piper1-gpl) é GPL; os pesos da voz têm licença própria.

## Decisão

**DEC-0067 — alternativa A** (proprietário, pelo portal, 21/11/2026; [registro](../../../../docs/governance/responses/DEC-0067.md)): o Urbe fonemiza o português com o **espeak-ng (GPL-3.0)**, distribuído junto do modelo neural baixado sob demanda, pelo sherpa-onnx 1.13.x (a linha que ainda embute o espeak-ng). Estado: em desenvolvimento; nada disto foi testado no aparelho.

Obrigações da GPL-3.0 a cumprir no download do modelo:
- o texto da GPL-3.0 (e as licenças dos pesos: Apache-2.0 do Kokoro, CC0 do dataset Piper) vai junto do download e é mostrado no app antes e depois de baixar;
- oferta do código-fonte correspondente do espeak-ng (link para a versão exata usada e para o código, mantido disponível enquanto o componente for distribuído);
- avisos de copyright e de ausência de garantia preservados; o componente GPL fica identificado como tal, separado do restante do app.

## Implementação (21/11/2026)

- `Urbe.Core/Voice/NeuralVoices.cs`: catálogo das vozes (Kokoro `pf_dora` e `pm_alex`, Piper faber) com endereço, tamanho e **SHA-256** de cada pacote; os hashes ficam ali, não neste documento.
- `Urbe.Client` (`NeuralSpeech`): síntese pelo sherpa-onnx 1.13.8 com espeak-ng embutido. O pacote é baixado uma vez quando a pessoa escolhe a voz em "Voz da narração", conferido por tamanho e SHA-256 e extraído na pasta privada `vozes` do app, junto de `LICENCAS-NUMA.txt`. Narração, podcast (o segundo apresentador usa a outra voz do modelo) e vídeos do Assistente usam essa voz.
- APK: cerca de 26 MB a mais (arm64: `libonnxruntime.so` 22 MB, `libsherpa-onnx-c-api.so` 4,5 MB). Os modelos não vão no APK.
- Testes: Core 1890/1890, Client 732/732. `NeuralSpeechTests` instala o modelo Piper real a partir de uma cópia local e sintetiza português; no CI esses testes são pulados sem `URBE_TTS_ARCHIVES`. Build Android Release com as bibliotecas nativas no APK; **não executado em aparelho**.

Estado das obrigações da GPL-3.0:
- feito: as licenças (GPL-3.0, Apache-2.0, CC0) vão junto do download e são mostradas antes de baixar; o link do código-fonte do espeak-ng (https://github.com/espeak-ng/espeak-ng) é informado;
- falta: mostrar as licenças de novo no app depois do download; fixar a versão exata do espeak-ng usada pelo sherpa-onnx 1.13.8 e a oferta do código-fonte correspondente a essa versão.

## Proposta anterior (histórico, antes da decisão)

- Um provedor único de síntese no app (`SpeechSynth`) com três fontes: a voz do sistema (que já existe, como alternativa explícita), um modelo neural local baixado sob demanda (com checksum, tamanho e licença visíveis) e, opcionalmente, um endpoint externo configurado pela pessoa. A narração de vídeo (`render_video`/`FitToVoices`) e a leitura em voz alta passam a usar esse provedor.
- Primeiro passo que não depende da decisão de licença: o provedor e a escolha de voz, com a voz do sistema. O motor neural entra quando a questão do fonemizador estiver decidida.
- Decisão necessária: aceitar GPL (espeak-ng) no APK para pt-BR, ou exigir um fonemizador permissivo antes do neural local.
