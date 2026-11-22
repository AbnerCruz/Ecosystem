using System.Collections.Generic;
using System.Linq;

namespace Lunet.Core;

/// <summary>Um desafio autoral, construído em um projeto Lunet comum pelo estudante.</summary>
public sealed record LearningProject(
    string Id, string CourseId, string Title, string Mission,
    IReadOnlyList<LearningStep> Steps, string Investigate,
    string Acceptance, string Transfer, string Orientation = "portrait");

/// <summary>
/// T01 — dez projetos publicáveis. Objetivos coincidem com a matriz editorial;
/// pistas indicam APIs reais, mas nunca inserem solução oculta em Game.cs.
/// </summary>
public static class LearningTrackOne
{
    private static LearningProject Create(string id, string title, string mission,
        string investigate, string acceptance, string transfer, string orientation,
        params string[] objectives)
    {
        if (objectives.Length != 4) throw new ArgumentException("Todo projeto T01 precisa de quatro etapas.");
        return new LearningProject(id, id[..7], title, mission,
            objectives.Select((objective, index) => new LearningStep($"{id}-S{index + 1}", objective)).ToArray(),
            investigate, acceptance, transfer, orientation);
    }

    public static IReadOnlyList<LearningProject> Projects { get; } = new[]
    {
        Create("LRN-001-P1", "Aprendendo movimento",
            "Crie um jogo de uma tela: mova um quadrado e alcance um círculo para vencer. Escolha seu próprio algoritmo.",
            "Documentação offline: Primeiros passos, Gráficos, Entrada e Colisão. Explore Game.Update, Game.Draw, SpriteBatch.FillRect, Input.TryGetPointer e Circle. Comece por uma única forma.",
            "No Preview, as formas aparecem separadas; o quadrado responde ao toque; não há vitória antes do contato; o encontro vence uma vez; reiniciar restaura a posição.",
            "Troque as posições iniciais e o tamanho das figuras sem quebrar a condição de vitória.",
            "portrait",
            "Desenhe um quadrado visível na tela.",
            "Faça o quadrado se mover por toque ou controle virtual.",
            "Desenhe um círculo distante da posição inicial do quadrado.",
            "Quando o quadrado tocar o círculo, encerre a partida com uma indicação de vitória. Permita reiniciar."),

        Create("LRN-001-P2", "Caça ao alvo",
            "Transforme o movimento em um jogo de coleta: três alvos, nenhuma contagem duplicada e vitória apenas ao capturar todos.",
            "Documentação offline: Entrada, Colisão e Gráficos. Investigue listas, RectangleF/Circle, distância entre posições e remoção de itens coletados.",
            "No Preview, cada alvo aparece e some apenas após contato; dois contatos no mesmo lugar não pontuam duas vezes; a vitória surge somente após os três; reiniciar repõe todos.",
            "Crie uma quarta posição de alvo sem copiar três blocos de lógica idênticos.",
            "portrait",
            "Posicione três alvos em lugares diferentes.",
            "Permita mover o mesmo personagem entre eles.",
            "Retire cada alvo após contato.",
            "Finalize apenas após coletar todos e reiniciar corretamente."),

        Create("LRN-002-P1", "Fuja da borda",
            "Construa uma arena com limites e duas fases de partida: jogando e derrotado.",
            "Documentação offline: Gráficos, Entrada e Colisão. Investigue estados por enum, coordenadas virtuais e RectangleF; diferencie posição do centro e limites do objeto.",
            "Cruzou a borda: derrota visível; dentro da arena: partida ativa; o reinício repõe o estado e a posição sem acumular efeitos.",
            "Acrescente uma borda de espessura variável e prove que a regra ainda funciona.",
            "portrait",
            "Desenhe um jogador móvel e uma arena delimitada.",
            "Mantenha o jogo ativo enquanto permanecer na arena.",
            "Ao ultrapassar um limite, apresente derrota.",
            "Adicione reinício que limpa estado e posição."),

        Create("LRN-002-P2", "Desafio de três vidas",
            "Adicione um perigo móvel, invulnerabilidade curta entre colisões e derrota após três danos válidos.",
            "Documentação offline: Loop e tempo, Colisão e Entrada. Use GameTime.DeltaSeconds para mover o perigo; procure uma transição de contato e um temporizador contra dano duplicado.",
            "Um mesmo contato contínuo não subtrai várias vidas; três impactos válidos encerram a rodada; reiniciar recupera vidas, posições e perigos.",
            "Experimente outra velocidade do perigo e confira se a janela de dano continua justa.",
            "portrait",
            "Crie um perigo que cruza a arena.",
            "Subtraia uma vida apenas ao contato válido.",
            "Mostre tela de derrota após três danos.",
            "Reinicie vidas, perigos e posição sem criar estados duplicados."),

        Create("LRN-003-P1", "Colecionador cronometrado",
            "Faça um jogo curto de reflexos com alvo que reaparece, placar e fim de rodada por tempo.",
            "Documentação offline: Loop e tempo, Entrada e Fontes bitmap. Investigue SpriteFont.CreateDefault, DrawString, DeltaSeconds e gerador de posições reproduzíveis.",
            "Durante a rodada, tempo e pontos mudam de modo coerente; ao zerar o tempo, o placar congela; tocar para tentar de novo começa do zero.",
            "Permita escolher entre duas durações sem duplicar o loop do jogo.",
            "portrait",
            "Mostre um contador de pontos visível.",
            "Crie alvo que aparece em posição nova ao ser tocado.",
            "Defina duração finita da rodada.",
            "Finalize com placar coerente e botão de tentar de novo."),

        Create("LRN-003-P2", "Corrida de recordes",
            "Meça o tempo necessário para chegar até uma meta e mantenha o melhor tempo da sessão.",
            "Documentação offline: Loop e tempo, Entrada, Colisão e Debug. Considere estado correndo/pausado/concluído; conte tempo apenas durante a corrida.",
            "A contagem para exatamente na meta, não continua durante pausa, mostra o melhor tempo desta sessão e não acelera ao reiniciar.",
            "Crie duas metas de distâncias diferentes usando o mesmo cronômetro.",
            "portrait",
            "Calcule tempo até tocar a meta.",
            "Exiba tempo durante a jogada.",
            "Guarde o melhor valor dentro da sessão.",
            "Teste pausa e reinício sem duplicar ou acelerar o cronômetro."),

        Create("LRN-004-P1", "Labirinto de dedo",
            "Crie um labirinto legível e percorra-o arrastando um personagem sem saltos de posição entre dedos.",
            "Documentação offline: Entrada, Colisão e Gráficos. Investigue TouchCollection, TouchPoint, identificação do dedo, Cancelled e colisão com parede.",
            "Arrastar move continuamente sem atravessar paredes; perder ou trocar o dedo não teletransporta; soltar/cancelar interrompe; chegar à saída conclui.",
            "Mude o traçado do labirinto e teste se início e fim continuam acessíveis.",
            "portrait",
            "Construa labirinto simples de linhas ou blocos.",
            "Implemente movimentação contínua por arrasto.",
            "Não teletransporte o jogador ao trocar de dedo.",
            "Valide início, saída e cancelamento do gesto."),

        Create("LRN-004-P2", "Arena com dois controles",
            "Crie uma arena horizontal com movimento e mira simultâneos, respeitando cada polegar.",
            "Documentação offline: Entrada, seção Joysticks com dedos separados. Explore VirtualStick.Update(Input, excludedTouchId), TouchId, RequireFreshPress e Cancel().",
            "Movimento à esquerda e mira à direita funcionam juntos; cada dedo mantém sua função; ao pausar/retomar, entradas antigas são liberadas.",
            "Faça a direção de mira persistir após soltar o segundo dedo, sem prender o identificador do toque.",
            "landscape",
            "Crie movimento com analógico virtual esquerdo.",
            "Controle uma seta de direção com toque direito.",
            "Permita tocar simultaneamente sem confundir dedos.",
            "Pausar e retomar libera capturas antigas de toque."),

        Create("LRN-005-P1", "Mini-arcade de um minuto",
            "Construa um minijogo autoral de sessenta segundos com menu, ameaça crescente, pontos e derrota.",
            "Documentação offline: Loop e tempo, Entrada, Áudio e Fontes bitmap. Projete estados menu/jogo/derrota; faça spawn limitado e libere recursos no encerramento.",
            "O menu não inicia sozinho; após iniciar surgem pontos e ameaças progressivas; o fim mostra resultado; reiniciar não cria objetos ou eventos antigos.",
            "Teste duas dificuldades com progressão diferente e a mesma lógica de menu.",
            "portrait",
            "Desenhe tela inicial e botão iniciar.",
            "Implemente mecânica com pontuação e ameaça crescente.",
            "Mostre derrota e pontuação ao fim.",
            "Reinicie sem acumular objetos ou eventos antigos."),

        Create("LRN-005-P2", "Microjogo autoral",
            "Crie um microjogo próprio de uma tela. Defina uma regra compreensível e entregue uma partida completa sem soluções fornecidas.",
            "Documentação offline: Primeiros passos, Entrada, Colisão, Loop e tempo e Áudio. Utilize somente APIs que conseguir executar no Preview; nenhuma dependência de IA ou Studio é obrigatória.",
            "Uma pessoa entende controles, objetivo e final sem explicação externa; é possível jogar, vencer ou perder e reiniciar; abre nos Projetos sem usar Aprender.",
            "Peça a alguém para testar sem instruções suas; documente um problema observado e corrija-o.",
            "portrait",
            "Escolha uma regra original de uma tela.",
            "Documente controle, objetivo e condições de fim.",
            "Implemente início, partida e conclusão com feedback.",
            "Entregue jogo reproduzível no Preview sem depender da aba Aprender.")
    };
}
