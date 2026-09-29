using System.Numerics;

namespace Lunet;

/// <summary>Funções matemáticas de uso comum em jogos.</summary>
public static class MathEx
{
    /// <summary>Interpolação linear entre dois valores.</summary>
    /// <param name="a">Valor inicial.</param>
    /// <param name="b">Valor final.</param>
    /// <param name="t">Fração de 0 a 1.</param>
    /// <returns>a + (b − a) × t.</returns>
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Limita o valor ao intervalo de 0 a 1.</summary>
    /// <param name="v">Valor.</param>
    /// <returns>O valor limitado.</returns>
    public static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    /// <summary>Converte <paramref name="v"/> do intervalo [a0,a1] para [b0,b1] (sem limitar).</summary>
    public static float Remap(float v, float a0, float a1, float b0, float b1) =>
        a1 == a0 ? b0 : b0 + (v - a0) / (a1 - a0) * (b1 - b0);

    /// <summary>Curva suave de 0 a 1 (aceleração e desaceleração nas pontas).</summary>
    /// <param name="t">Fração de 0 a 1.</param>
    /// <returns>Valor suavizado.</returns>
    public static float SmoothStep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Aproxima <paramref name="current"/> de <paramref name="target"/> sem ultrapassar.</summary>
    public static float MoveToward(float current, float target, float maxDelta) =>
        MathF.Abs(target - current) <= maxDelta ? target : current + MathF.Sign(target - current) * maxDelta;

    /// <summary>Aproxima o valor do alvo sem ultrapassá-lo.</summary>
    /// <param name="current">Valor atual.</param>
    /// <param name="target">Valor desejado.</param>
    /// <param name="maxDelta">Maior passo permitido.</param>
    /// <returns>Novo valor.</returns>
    public static Vector2 MoveToward(Vector2 current, Vector2 target, float maxDelta)
    {
        var diff = target - current;
        var length = diff.Length();
        return length <= maxDelta || length == 0 ? target : current + diff / length * maxDelta;
    }

    /// <summary>Converte graus em radianos.</summary>
    /// <param name="degrees">Ângulo em graus.</param>
    /// <returns>Ângulo em radianos.</returns>
    public static float ToRadians(float degrees) => degrees * (MathF.PI / 180f);
    /// <summary>Converte radianos em graus.</summary>
    /// <param name="radians">Ângulo em radianos.</param>
    /// <returns>Ângulo em graus.</returns>
    public static float ToDegrees(float radians) => radians * (180f / MathF.PI);

    /// <summary>Menor diferença angular (radianos) de <paramref name="from"/> para <paramref name="to"/>, em [−π, π].</summary>
    public static float AngleDifference(float from, float to)
    {
        var d = (to - from) % MathF.Tau;
        if (d > MathF.PI) d -= MathF.Tau;
        else if (d < -MathF.PI) d += MathF.Tau;
        return d;
    }
}
