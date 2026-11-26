# Lunet Studio — falha ao usar música e auditoria funcional

26/11/2026 · LUNET-500, LUNET-511, LUNET-536 A3/A4/A6 · base `f22eb24586f822d1104573a8b2b3aa55cb63a624`

## Diagnóstico

O relato não tem stack trace, composição nem a sequência de toques que provocou o encerramento. A inspeção da casca Avalonia identificou riscos distintos que podem produzir o sintoma. A ocorrência específica no aparelho ficou sem reprodução.

| Operação | Evidência no código anterior | Tratamento nesta entrega |
| --- | --- | --- |
| Abrir composição inválida | O construtor de `MusicStudioView` executava `MusicSong.Parse` diretamente. JSON malformado escapava do callback. Faixa, instrumento, padrão ou nota nulos podiam causar exceção na tela ou na síntese. | Validação dos elementos e abertura com mensagem e retorno. O arquivo original fica intacto. |
| Ouvir padrão ou música | `MusicStudioView` sintetizava toda a faixa e codificava WAV no clique. `ShellActivity` escrevia em cache e preparava o `MediaPlayer` na mesma thread. | Snapshot renderizado em segundo plano, cancelamento entre requisições, botão Parar, escrita assíncrona e `PrepareAsync`. Só a solicitação vigente pode começar a tocar. |
| Arrastar notas | Cada célula tentava sintetizar, escrever e reiniciar a prévia no dispositivo. | A primeira nota do gesto produz uma prévia curta. Uma prévia em andamento preserva a reprodução atual. |
| Salvar e voltar | O fechamento renderizava todo o arranjo no callback. No teto de 10 minutos, 44,1 kHz produzem cerca de 26,6 milhões de quadros. Somente três vetores de mixagem ocupam cerca de 304 MiB, além de buffers por faixa, efeitos, estéreo e PCM. | Renderização fora da thread de interface, reutilização do buffer de faixa e estimativa preventiva no Android. O par JSON/WAV só é alterado depois de renderizar e segue a gravação recuperável do `StudioAssetWorkspace`. |
| Arranjo vazio | Ouvir música usava o padrão selecionado, mas salvar produzia WAV sem amostras. | Ouvir e exportar usam o padrão selecionado quando não há arranjo. A ordem vazia permanece editável no JSON. |
| Reabrir WAV criado sem jogo | O editor salvava a cópia em `Content/Audio`, mas a lista global filtrava todos os WAVs. | Cópias independentes aparecem em Minhas criações e podem ser reabertas ou publicadas. Os WAVs derivados de músicas e efeitos seguem agrupados com a fonte. O limite de leitura é aplicado antes de alocar o arquivo. |

No Android, a estimativa conservadora usa 32 bytes por quadro e um orçamento de 96 MiB por renderização. Isso equivale a cerca de 69 s de conteúdo ao exportar em 44,1 kHz e 95 s na prévia em 32 kHz, incluindo a reserva de cauda no cálculo. O documento segue legível e editável acima disso. A prévia de um padrão continua disponível quando ele cabe no orçamento. Uma tentativa de exportação acima do orçamento mostra um erro, mantém a edição aberta e preserva o par gravado anteriormente. Para exportar arranjos maiores sem essa restrição, falta a renderização em blocos. O orçamento usa a estimativa de alocação da renderização e independe da memória disponível naquele momento.

O piano roll abre na região das notas da faixa e permite trocar de oitava. Isso torna editáveis notas graves e de bateria que o intervalo fixo a partir da tônica escondia. Os controles inferiores rolam em telas baixas, reservando espaço para a grade e mantendo o transporte acessível.

## Inventário do Studio

Inspeção da árvore `src/Lunet.Client/Views`, dos modelos e testes correspondentes, da [especificação §13](../SPEC.md#13-lunet-studio), do [inventário da casca](../plans/inventario-migracao-casca-2026-11-25.md) e da [Fase 5](../../ROADMAP.md#fase-5--lunet-studio-13).

| Ferramenta | Fluxo encontrado na casca | Lacunas para completar o escopo existente |
| --- | --- | --- |
| Música e efeitos | Piano roll, faixas, padrões, instrumento, volume/pan, mudo/solo, arranjo e fonte com WAV. Efeitos têm editor paramétrico e saída WAV. | Música ainda precisa de cursor de reprodução, edição dos parâmetros avançados presentes no modelo, geradores e renderização em blocos. A prévia de áudio depende do host Android. |
| WAV | Cria tom e edita recorte, ganho e loop. Salva cópia PCM, reabre e publica pelo catálogo global. | A casca de desktop ainda não liga uma saída de áudio. A edição WAV tem o escopo PCM do editor existente. |
| Sprite | Pixel art, ferramentas de desenho e seleção, quadros, prévia, desfazer e spritesheet. | Camadas por quadro e clips editáveis com eventos, conforme Sprite v2 e Animation Studio. |
| Mapa | Tileset editável, camadas, terreno/autotile e colisão. | Fluxo completo de propriedades de tile/célula e prova do mapa no jogo. |
| Partículas | Editor do emissor com prévia e parâmetros exportáveis. | Curvas de tamanho editáveis. |
| Paleta e dados | Edição de cores, GPL, geração de código e contraste. Tabelas com esquema, validação, CSV e classe C#. | Substituição de uma cor em todos os assets. Dados pede prova de ida e volta no fluxo publicar → importar → consumir. |
| Animação, atlas, UI e física | Há recursos parciais em ferramentas existentes e APIs de runtime. | O catálogo global ainda não oferece editores dedicados para esses fluxos da Fase 5. |
| Biblioteca | Rascunhos em pasta de Studio sem abrir jogo e publicação já utilizável. | A prova de ponta a ponta de criar, publicar, copiar para um jogo e jogar continua no LUNET-511/LUNET-536 A6. |

## Referências e decisões

- A documentação do Avalonia exige acesso a controles pelo dispatcher. A síntese e os arquivos vão para tarefas de fundo e os resultados voltam pelo contexto do chamador da UI. [Modelo de threads do Avalonia](https://docs.avaloniaui.net/docs/app-development/threading)
- A documentação Android distingue preparo, reprodução, erro e encerramento. O host prepara com `PrepareAsync`, trata conclusão e erro, libera o player e cancela a prévia quando a atividade pausa. [MediaPlayer no Android](https://developer.android.com/reference/android/media/MediaPlayer)
- O BeepBox mantém o sintetizador como unidade própria. Suas notas de versão registram problemas de toque no editor e a adição de indicação durante exportação. São precedentes para dar retorno durante processamento e manter o formato editável independente do áudio. [Código do sintetizador](https://github.com/johnnesky/beepbox/blob/main/synth/synth.ts) · [Notas de versão](https://github.com/johnnesky/beepbox/releases)
- Há um relato no LMMS de áudio presente na reprodução e ausente no arquivo final. É um caso individual, útil aqui como cenário de regressão para a divergência encontrada no arranjo vazio. [LMMS #7107](https://github.com/LMMS/lmms/issues/7107)

## Verificação

- Os 1.327 testes de Lunet passaram em Debug com .NET 10.0.404, incluindo a interface headless com Skia. Também passaram os 22 checks de consistência local do Ecosystem. Foram geradas capturas da casca em largura de celular e desktop.
- Regressões verificadas para abrir arquivo inválido sem alterar seu conteúdo, rejeitar faixa nula antes da reprodução, recusar uma renderização antes da alocação acima do orçamento, manter música editável no projeto e nos Studios globais, alcançar MIDI 36 na bateria e reabrir WAV global.
- Sem execução em aparelho físico. O CI do pull request é a evidência para integração e build Android. O smoke existente do emulador exercita entrada e jogo, enquanto os testes headless exercitam estados da tela e gravação. Nenhum deles confirma o caso físico do relato.
- Cenário de reprodução no Android, quando houver evidência disponível. Abrir Studios → Música, alternar rapidamente Padrão/Música/Parar, arrastar notas, selecionar Bateria, voltar durante uma prévia e reabrir o rascunho. Repetir por Recursos em um jogo. Conferir que notas, fonte, WAV e retorno ao catálogo sobrevivem e que o som não continua fora da tela.

O próximo incremento de áudio está registrado em LUNET-500 e LUNET-536 A3/A4. As demais lacunas permanecem nos itens correspondentes da Fase 5 e da Onda 3.
