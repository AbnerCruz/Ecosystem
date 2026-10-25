# Áudio

O áudio é dividido em barramentos (`AudioBus`) controlados por um `AudioMixer`, por exemplo música, efeitos e interface.

- `SoundEffect` e `SoundInstance`: sons curtos, com volume, tom e balanço.
- `Music`: faixas longas, uma por vez, com transição.
- Cada barramento tem volume próprio, ideal para um menu de opções.

Se o aparelho não tiver saída de áudio, o jogo continua rodando com o backend nulo.


## Música WAV e diagnóstico no Android (LUNET-438)

`Content.LoadMusic("Audio/loop.wav")` e `Audio.PlayMusic` continuam com a mesma API.
WAV RIFF com PCM inteiro mono/estéreo, 8/16 bits e 4–192 kHz usa AudioTrack em
streaming. A saída ainda deve aceitar a taxa solicitada; falhas aparecem no Console.
O worker mantém um buffer limitado (máximo 256 KiB), lê apenas o chunk `data`,
preenche a saída antes de tocar, respeita escritas parciais e encerra em EOF sem
deixar uma saída vazia tocando. Não decodifica toda a música para PCM em memória.
O arquivo original continua sendo o cache já utilizado por `LoadMusic`.

OGG, MP3 e WAV com codificação não suportada por esse leitor permanecem no
MediaPlayer. SoundEffect/SoundInstance mantêm SoundPool, pan e pitch. Nenhuma
permissão adicional, conta, pacote ou dependência de outro Product é necessária.
Arquivos PCM estruturalmente corrompidos produzem erro, em vez de um loop vazio.

No Preview → Perf, a linha **Audio underruns (música WAV PCM, sessão)** usa
`AudioTrack.getUnderrunCount`, o contador de esvaziamentos do buffer de escrita da
aplicação desde a criação da saída. Zero significa que essa saída foi medida e
não registrou ocorrências; antes de uma faixa PCM mensurável o valor é indisponível.
A soma preserva faixas PCM já encerradas/trocadas na mesma sessão do backend.
Restart/novo Run criam uma sessão nova. Abrir/fechar Perf não zera a sessão de áudio.
O relatório permanece disponível no workspace e na exportação existentes.
**Não mede** efeitos SoundPool, música no MediaPlayer, latência, falhas de hardware
nem todo o mixer do Android. A lacuna de cobertura integral do Profiler segue aberta.

Referências: [AudioTrack/underruns e Stop](https://developer.android.com/reference/android/media/AudioTrack),
[escrita não bloqueante no binding C#](https://learn.microsoft.com/en-us/dotnet/api/android.media.audiotrack.write).

### Roteiro no aparelho

1. Atualizar pelo APK desta entrega sem desinstalar. Offline, criar um **Laboratório**
   novo para preservar projetos anteriores. Abrir Run → módulo **Audio / sensores**.
2. Acionar a música do Laboratório (usa `Audio/loop.wav`), esperar alguns segundos
   e abrir **Perf**. Deve tocar continuamente, inclusive em repetição, e aparecer
   um número real na linha de underruns WAV, com efeitos/comprimidos não medidos.
   Não há exigência de número positivo: zero é um resultado válido.
3. Testar volume, fade e os efeitos. Pausar/retomar; ir à tela inicial do Android e
   voltar. O áudio pausa no segundo plano e retoma sem começar do zero.
4. Trocar/reiniciar/Stop durante a música e também com o jogo pausado: sem travar
   nem deixar música tocando depois de fechar o Preview. Repetir em Preview isolado.
5. Depois de Stop, abrir Profiler no workspace e compartilhar: mesmo contador e
   rótulo de cobertura da última captura. Novo Run reinicia a contagem da sessão.
6. Regressão: tocar OGG/MP3 já existentes num projeto, se disponível, e confirmar
   reprodução e volume/loop no MediaPlayer. Sem WAV tocado nessa sessão, underruns
   continuam não medidos. Testar também faixa WAV curta com `loop: false`: termina
   uma vez, não repete e não trava Stop.

Responder 1–6 com passou/falhou/não testado e versão do APK. Em falha, anexar o
Console/relatório e informar o arquivo/formato. Testes portáteis não aprovam áudio
físico, Bluetooth, rotas de saída ou lifecycle Android.
