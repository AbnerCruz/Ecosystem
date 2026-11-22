using System.Collections.Generic;
using System.Linq;

namespace Lunet.Core;

/// <summary>Projeção inicial dos 60 cursos de docs/learning/MATRIZ-CURRICULAR.md.
/// Somente LRN-001-P1 está disponível; os demais NÃO estão implementados.
/// Nunca usar essa lista como prova de conclusão dos cursos.</summary>
public sealed record LearningCourse(string Id, string Title, string Track, bool Available);
public sealed record LearningStep(string Id, string Objective);
public static class LearningCatalog
{
    public const string FirstCourseId = "LRN-001";
    public const string FirstProjectId = "LRN-001-P1";
    public static IReadOnlyList<string> Tracks { get; } = new[]
    {
        "Primeiros jogos e orientação no Lunet",
        "C# aplicado a jogos",
        "Gêneros, mecânicas e game design",
        "Matemática, geometria e física",
        "Mundos, cenas e navegação",
        "Arte 2D, sprites e animação",
        "Gráficos avançados e GPU",
        "Áudio, interface, narrativa e acessibilidade",
        "Sistemas de jogo e inteligência artificial",
        "Dados, engenharia de software e performance",
        "Multiplayer e engenharia de redes",
        "Produção, qualidade e lançamento",
    };
    public static IReadOnlyList<LearningCourse> Courses { get; } = new LearningCourse[]
    {
        new("LRN-001", "Aprendendo movimento", Tracks[0], true),
        new("LRN-002", "Estados de partida", Tracks[0], false),
        new("LRN-003", "Pontos e tempo", Tracks[0], false),
        new("LRN-004", "Controles mobile", Tracks[0], false),
        new("LRN-005", "Microjogo completo", Tracks[0], false),
        new("LRN-006", "Tipos, variáveis e operações", Tracks[1], false),
        new("LRN-007", "Condições e repetição", Tracks[1], false),
        new("LRN-008", "Métodos, eventos e responsabilidade", Tracks[1], false),
        new("LRN-009", "Objetos e composição", Tracks[1], false),
        new("LRN-010", "Coleções, dados e erros", Tracks[1], false),
        new("LRN-011", "Pong e dinâmica de rebote", Tracks[2], false),
        new("LRN-012", "Breakout e progressão de fases", Tracks[2], false),
        new("LRN-013", "Runner e dificuldade", Tracks[2], false),
        new("LRN-014", "Puzzle, lógica e level design", Tracks[2], false),
        new("LRN-015", "Plataforma e movimento expressivo", Tracks[2], false),
        new("LRN-016", "Vetores e trigonometria", Tracks[3], false),
        new("LRN-017", "Geometria e colisões", Tracks[3], false),
        new("LRN-018", "Cinemática e tempo", Tracks[3], false),
        new("LRN-019", "Física rígida", Tracks[3], false),
        new("LRN-020", "Juntas e estabilidade", Tracks[3], false),
        new("LRN-021", "Câmera 2D", Tracks[4], false),
        new("LRN-022", "Tilemaps e colisão de terreno", Tracks[4], false),
        new("LRN-023", "Cenas, transições e fluxo", Tracks[4], false),
        new("LRN-024", "Geração procedural de mapas", Tracks[4], false),
        new("LRN-025", "Perspectivas, coordenadas e navegação", Tracks[4], false),
        new("LRN-026", "Pixel art e legibilidade", Tracks[5], false),
        new("LRN-027", "Spritesheets e recortes", Tracks[5], false),
        new("LRN-028", "Animação de personagens", Tracks[5], false),
        new("LRN-029", "Paletas, camadas e identidade visual", Tracks[5], false),
        new("LRN-030", "Partículas e linguagem visual", Tracks[5], false),
        new("LRN-031", "Resolução, viewport e pixels", Tracks[6], false),
        new("LRN-032", "Camadas e batching", Tracks[6], false),
        new("LRN-033", "Composição e transparência", Tracks[6], false),
        new("LRN-034", "Shaders 2D", Tracks[6], false),
        new("LRN-035", "Render targets, luz e pós-processo", Tracks[6], false),
        new("LRN-036", "Efeitos sonoros e mixagem", Tracks[7], false),
        new("LRN-037", "Música adaptativa", Tracks[7], false),
        new("LRN-038", "Menus e HUD", Tracks[7], false),
        new("LRN-039", "Acessibilidade e controles", Tracks[7], false),
        new("LRN-040", "Texto, diálogos e localização", Tracks[7], false),
        new("LRN-041", "Máquinas de estado de NPC", Tracks[8], false),
        new("LRN-042", "Pathfinding e navegação", Tracks[8], false),
        new("LRN-043", "Comportamento autônomo", Tracks[8], false),
        new("LRN-044", "Sistemas econômicos e crafting", Tracks[8], false),
        new("LRN-045", "Estratégia e simulação tática", Tracks[8], false),
        new("LRN-046", "Modelagem de dados e inventários", Tracks[9], false),
        new("LRN-047", "Salvamento e migração", Tracks[9], false),
        new("LRN-048", "Testes e depuração", Tracks[9], false),
        new("LRN-049", "Arquitetura e reuso", Tracks[9], false),
        new("LRN-050", "Otimização e profiling", Tracks[9], false),
        new("LRN-051", "Multiplayer local e turnos", Tracks[10], false),
        new("LRN-052", "Estado compartilhado assíncrono", Tracks[10], false),
        new("LRN-053", "Rede local em tempo real", Tracks[10], false),
        new("LRN-054", "Predição, interpolação e autoridade", Tracks[10], false),
        new("LRN-055", "Servidor dedicado, escala e segurança", Tracks[10], false),
        new("LRN-056", "Conceito, escopo e prototipagem", Tracks[11], false),
        new("LRN-057", "Playtest, balanceamento e iteração", Tracks[11], false),
        new("LRN-058", "Pipeline de produção e direitos", Tracks[11], false),
        new("LRN-059", "Distribuição e manutenção Android", Tracks[11], false),
        new("LRN-060", "Projeto autoral de conclusão", Tracks[11], false),
    };
    public static IReadOnlyList<LearningStep> FirstProjectSteps { get; } = new LearningStep[]
    {
        new("LRN-001-P1-S1", "Desenhe um quadrado visível na tela."),
        new("LRN-001-P1-S2", "Faça o quadrado se mover por toque ou controle virtual."),
        new("LRN-001-P1-S3", "Desenhe um círculo distante da posição inicial do quadrado."),
        new("LRN-001-P1-S4", "Quando o quadrado tocar o círculo, encerre a partida com uma indicação de vitória. Permita reiniciar."),
    };
    public static LearningCourse? Find(string id) => Courses.FirstOrDefault(c => c.Id == id);
    public static bool IsProjectAvailable(string id) => id == FirstProjectId;
}
