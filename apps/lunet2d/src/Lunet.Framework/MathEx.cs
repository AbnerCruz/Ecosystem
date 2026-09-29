using System.Numerics;

namespace Lunet;

/// <summary>Funções matemáticas de uso comum em jogos.</summary>
public static class MathEx
{
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    /// <summary>Converte <paramref name="v"/> do intervalo [a0,a1] para [b0,b1] (sem limitar).</summary>
    public static float Remap(float v, float a0, float a1, float b0, float b1) =>
        a1 == a0 ? b0 : b0 + (v - a0) / (a1 - a0) * (b1 - b0);

    public static float SmoothStep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Aproxima <paramref name="current"/> de <paramref name="target"/> sem ultrapassar.</summary>
    public static float MoveToward(float current, float target, float maxDelta) =>
        MathF.Abs(target - current) <= maxDelta ? target : current + MathF.Sign(target - current) * maxDelta;

    public static Vector2 MoveToward(Vector2 current, Vector2 target, float maxDelta)
    {
        var diff = target - current;
        var length = diff.Length();
        return length <= maxDelta || length == 0 ? target : current + diff / length * maxDelta;
    }

    public static float ToRadians(float degrees) => degrees * (MathF.PI / 180f);
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
