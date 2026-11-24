# Tempo e laço do jogo

## Update e Draw

`Update(GameTime time)` avança as regras, lê a entrada e muda o estado. `Draw(GameTime time)` representa o estado atual na tela e pode rodar mais ou menos vezes que `Update`. Alterar pontos ou cronômetros em `Draw` amarra as regras ao ritmo da renderização.

## Segundos e velocidade

`DeltaSeconds` é medido em segundos. Em `Update`, vale o passo fixo da simulação. Com a configuração inicial de 60 atualizações por segundo, equivale a aproximadamente 0,0167 s. Para mover 90 unidades virtuais por segundo, some `90f * time.DeltaSeconds` à posição em cada atualização. Em `Draw`, `DeltaSeconds` descreve o intervalo real do quadro.

`TotalSeconds` é o tempo simulado acumulado desde o início do jogo. Ele acompanha os passos executados pela simulação. Para um cronômetro que conta apenas durante a rodada, acumule `DeltaSeconds` em um campo enquanto a partida estiver ativa e zere ao reiniciar.

[Referência de GameTime](id:T:Lunet.GameTime)

[Carregue e desenhe um sprite](guide:desenhar-sprite)
