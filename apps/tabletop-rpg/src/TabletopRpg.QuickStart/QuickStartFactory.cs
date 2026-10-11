using TabletopRpg.Core;
using TabletopRpg.Director;
using TabletopRpg.Npc;
using TabletopRpg.PlayerAgent;

namespace TabletopRpg.QuickStart;

/// <summary>
/// RPG-016: start a solo tabletop session from zero with no scenario authoring,
/// provider configuration or model invocation. Incremental worldbuilding remains
/// the Game Master's job: this factory only creates a credible first situation.
/// </summary>
public static class QuickStartFactory
{
    private sealed record Seed(
        string CampaignTitle, string Pitch, string SceneName, string SceneDescription,
        string QuestTitle, string PublicClue, string HiddenFact,
        string CompanionName, string CompanionPersona, string CompanionGoal,
        string NpcName, string NpcRole, string NpcMotivation,
        string ThreatThread, string ClueThread, string PromiseThread,
        string OpeningText, string StarterItem);

    private static readonly IReadOnlyDictionary<QuickStartTheme, Seed> Seeds =
        new Dictionary<QuickStartTheme, Seed>
        {
            [QuickStartTheme.Fantasy] = new(
                "Ecos da Ponte Velha",
                "Uma ponte abandonada, viajantes desaparecidos e um pedido de socorro.",
                "Estalagem da Ponte",
                "Chove sobre a estrada antiga. Uma estalagem guarda abrigo e rumores.",
                "Descobrir por que os viajantes desapareceram",
                "O barqueiro viu uma luz azul sob a ponte após o anoitecer.",
                "Uma passagem antiga liga o porão da estalagem às galerias sob a ponte.",
                "Lia",
                "Uma batedora prudente, leal ao grupo, mas curiosa diante de mistérios.",
                "Proteger os companheiros e investigar a estrada.",
                "Tomas",
                "estalajadeiro",
                "Manter a estalagem segura enquanto evita chamar atenção para a ponte.",
                "Algo nas galerias pode ameaçar quem atravessa a ponte.",
                "A luz azul pode apontar um caminho para a passagem.",
                "Quem pediu socorro espera uma resposta.",
                "A tempestade fecha a estrada. Um viajante chega à estalagem pedindo ajuda para encontrar alguém desaparecido. O que você faz?",
                "torch"),
            [QuickStartTheme.Mystery] = new(
                "O Caso do Teatro Silencioso",
                "O desaparecimento de uma atriz transforma uma estreia em investigação.",
                "Saguão do Teatro",
                "Os últimos convidados partem. Um bilhete esquecido chama a atenção.",
                "Investigar o desaparecimento da atriz",
                "O bilhete indica que alguém mudou a ordem do espetáculo.",
                "O diretor falsificou a hora do ensaio para ocultar uma reunião nos bastidores.",
                "Caio",
                "Um investigador observador que busca provas antes de formular acusações.",
                "Identificar pistas sem culpar inocentes.",
                "Helena",
                "contrarregra",
                "Proteger a companhia e impedir um escândalo antes da estreia.",
                "Alguém tenta silenciar testemunhas.",
                "Um bilhete e um horário alterado podem explicar o desaparecimento.",
                "Os familiares da atriz aguardam notícias.",
                "O teatro está quase vazio quando um bilhete cai de uma partitura. Há passos no corredor. O que você faz?",
                "notebook"),
            [QuickStartTheme.Frontier] = new(
                "O Sinal do Posto Sete",
                "Um posto isolado deixa de responder e a caravana precisa decidir o caminho.",
                "Hangar de Fronteira",
                "Uma equipe prepara sua partida enquanto as comunicações falham.",
                "Descobrir por que o Posto Sete parou de responder",
                "A última transmissão contém interferência e uma coordenada incompleta.",
                "O posto interrompeu o sinal para proteger uma equipe presa em um túnel de manutenção.",
                "Nara",
                "Uma exploradora pragmática, cuidadosa com recursos e companheiros.",
                "Investigar o posto sem expor a caravana a riscos desnecessários.",
                "Ravi",
                "mecânico",
                "Manter os equipamentos funcionando e encontrar peças para a equipe.",
                "A rota pode ficar perigosa com os sistemas fora do ar.",
                "A coordenada incompleta pode indicar uma entrada alternativa.",
                "Uma equipe isolada aguarda resgate.",
                "A rádio chia uma última vez antes de ficar muda. O mapa ainda mostra o Posto Sete. Como você quer começar?",
                "medkit")
        };

    public static IReadOnlyList<QuickStartChoice> Choices { get; } =
        Array.AsReadOnly(Seeds.OrderBy(p => p.Key)
            .Select(p => new QuickStartChoice(p.Key, p.Value.CampaignTitle, p.Value.Pitch))
            .ToArray());

    public static QuickStartResult Create(QuickStartOptions? options = null)
    {
        options ??= new QuickStartOptions();
        if (!Seeds.TryGetValue(options.Theme, out var seed))
            throw new ArgumentOutOfRangeException(nameof(options), "Unknown quick-start theme.");
        var name = RequireName(options.HeroName, nameof(options.HeroName));
        var title = options.CampaignName is null ? seed.CampaignTitle :
            RequireName(options.CampaignName, nameof(options.CampaignName));

        // Build a new campaign in memory; failed validation never modifies a
        // saved campaign, never issues a network call and never spends tokens.
        var campaign = new Campaign(CampaignId.New(), title);
        var hero = new Character(CharacterId.New(), name,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["strength"] = 1, ["wisdom"] = 1, ["defense"] = 1
            });
        var companion = new Character(CharacterId.New(), seed.CompanionName,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["strength"] = 1, ["wisdom"] = 2, ["defense"] = 1
            });
        var npc = new Character(CharacterId.New(), seed.NpcName);
        campaign.AddCharacter(hero);
        campaign.AddCharacter(companion);
        campaign.AddCharacter(npc);

        var humanSeat = new Participant(ParticipantId.New(), "Jogador",
            ControllerKind.Human, SeatRole.Player, [hero.Id]);
        var companionSeat = new Participant(ParticipantId.New(), seed.CompanionName,
            ControllerKind.Agent, SeatRole.Player, [companion.Id]);
        var masterSeat = new Participant(ParticipantId.New(), "Mestre",
            ControllerKind.Agent, SeatRole.GameMaster, [npc.Id]);
        campaign.AddParticipant(humanSeat);
        campaign.AddParticipant(companionSeat);
        campaign.AddParticipant(masterSeat);

        var start = campaign.AddScene(SceneId.New(), seed.SceneName, seed.SceneDescription);
        var quest = campaign.AddQuest(QuestId.New(), seed.QuestTitle, start.Id);
        campaign.SetResource(hero.Id, "hp", 10, 10);
        campaign.SetResource(companion.Id, "hp", 10, 10);
        campaign.SetResource(npc.Id, "hp", 8, 8);
        campaign.AddItem(InventoryId.New(), hero.Id, seed.StarterItem, 1);
        campaign.EnterScene(start.Id);

        // The GM sees this truth, but neither player learns it implicitly.
        // The hook is a *belief* for the human only, not proven world truth.
        campaign.AddWorldFact(seed.HiddenFact);
        campaign.AddBelief(hero.Id, seed.PublicClue, "rumor", 0.55);
        campaign.Remember(hero.Id, "Comecei a jornada sem conhecer toda a situação.");
        campaign.StartSession(SessionId.New(), "Primeira sessão");

        var agentProfile = new PlayerAgentProfile(
            companionSeat.Id, companion.Id, seed.CompanionPersona,
            [seed.CompanionGoal],
            [new PlayerRelationship(hero.Name, "companheiro de viagem")]);

        var npcProfile = new NpcProfile(npc.Id, masterSeat.Id, start.Id,
            seed.NpcRole, seed.NpcMotivation, NpcDisposition.Cautious);
        var threads = Array.AsReadOnly(new[]
        {
            new DirectorThread("threat", DirectorThreadKind.Threat, seed.ThreatThread),
            new DirectorThread("clue", DirectorThreadKind.Clue, seed.ClueThread),
            new DirectorThread("promise", DirectorThreadKind.Promise, seed.PromiseThread)
        });

        return new QuickStartResult(
            campaign, humanSeat.Id, hero.Id, companionSeat.Id, companion.Id,
            masterSeat.Id, npc.Id, start.Id, quest.Id, agentProfile,
            npcProfile, threads, seed.OpeningText);
    }

    private static string RequireName(string? input, string parameter)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 80)
            throw new ArgumentException("Name must contain 1..80 characters.", parameter);
        return input.Trim();
    }
}
