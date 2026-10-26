# Audio Studio — primeiro editor WAV nativo (LUNET-447)

## Escopo
O Lunet possui reprodução de efeitos/música e agora disponibiliza uma primeira ferramenta visual de **produção de som no próprio Android**, na Fase 5. Este editor é **offline, em C#**, não exige Python ou aplicativos externos e não pretende substituir uma DAW.

**Abrir:** em um projeto, `Arte → Áudio`, ou `⋯ → Ferramentas → Audio Studio`. Em `Assets`, um WAV também dispõe de **Editar no Audio Studio**.

**Criar:** em `Arte → Áudio → + Criar efeito sonoro`, escolha frequência (40–5000 Hz), duração (0,05–5 s), seno/quadrada/triangular/ruído e confirme. A síntese é feita em C# no `Lunet.Studio`, em PCM mono 22050 Hz com ataque e decaimento. É possível ouvir e alterar a amostra antes de salvar.

**Importar:** use `Arte → Áudio → Importar WAV do aparelho` e escolha um WAV PCM. O seletor Android copia o arquivo validado para `Content/Audio/` com nome único. Não sobrescreve ativos existentes.

**Editar:** selecione um WAV do projeto ou um som recém-gerado. O gráfico de forma de onda mostra picos min/max. Controles permitem início/fim de recorte, ganho de 0 a 200%, loop com início/fim próprios, reprodução e parada. `Salvar cópia WAV` gera um novo arquivo em `Content/Audio/` com nome único; não altera a fonte nem apaga recursos. A gravação contém marcadores de loop RIFF `smpl`, quando habilitados. A pré-escuta da região de loop repete a região marcada; o player do jogo usa seu próprio parâmetro `loop`, que pode não usar `smpl`.

**Compatibilidade:** PCM RIFF/WAVE de 8 ou 16 bits, mono ou estéreo, 4–192 kHz, arquivo de entrada até 24 MiB e até 8 milhões de amostras decodificadas. O resultado exportado é sempre WAV PCM16 no mesmo número de canais e taxa de amostragem, e pode ser lido pelo runtime do Lunet. WAV float/comprimido, MP3 e OGG ainda não podem ser editados; MP3/OGG podem ser reproduzidos no Painel Assets. Dados inválidos e tamanhos excessivos mostram erro sem gravar. Grandes arquivos de música completos requerem outro fluxo futuro de edição por streaming.

## Visual ↔ Source e autoridade
O arquivo WAV existente é a única fonte de verdade. O editor mantém os PCM decodificados apenas em memória e escreve novos WAVs (incluindo metadados abertos `smpl`) sob `Content/Audio`. Não cria índice paralelo nem arquivos privados que o jogo precise para executar. Pré-escuta é guardada no cache interno do app, excluída ao parar/fechar; nunca se mistura aos arquivos do projeto. `Lunet.Framework` não depende do Studio.

Acesso no código do jogo:
```csharp
var jump = Content.LoadSound("Audio/meu-efeito-edit-xxxxxxxx.wav");
jump.Play();
```
A música usa `Content.LoadMusic("Audio/faixa.wav")` e `Audio.PlayMusic(...)` conforme o guia de áudio do framework.

## Validação no aparelho (pendente)
1. Abra `Arte → Áudio → + Criar efeito sonoro`, modifique onda/frequência/duração, confirme. A forma de onda precisa aparecer, `▶ Ouvir` precisa tocar e `■ Parar` silenciar.
2. Arraste início/fim, diminua volume, marque loop, escolha pontos de loop e ouça novamente. Teste fechar o diálogo durante reprodução e colocar o app em segundo plano: não deve continuar tocando.
3. Salve a cópia, abra **Assets → Áudio**, localize o novo WAV e reproduza; abra no editor e confira o conteúdo. Feche/reabra o app e repita.
4. Edite um WAV já existente; confirme que o WAV original continua inalterado. Abra o WAV exportado com `Content.LoadSound` no preview de um jogo.
5. Tente importar um WAV inválido e um MP3; a mensagem deve explicar a limitação sem criar arquivos inválidos.
6. Confirme que Sprite/Tile Studios, editor C#, preview e export ZIP continuam acessíveis.

Não concluir a Fase 5 por este incremento: edição de arquivos comprimidos, instrumentos/composição e outros Studios não fazem parte da implementação.
