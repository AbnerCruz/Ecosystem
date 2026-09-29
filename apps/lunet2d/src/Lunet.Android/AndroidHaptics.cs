using Android.Content;
using Android.OS;
using Lunet.Input;

namespace Lunet.Android;

/// <summary>Vibração pelo motor do aparelho (permissão VIBRATE no manifesto).</summary>
internal sealed class AndroidHaptics(Context context) : IHaptics
{
    private readonly Vibrator? _vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;

    public void Vibrate(int milliseconds, float intensity = 1f)
    {
        if (_vibrator is null || !_vibrator.HasVibrator || milliseconds <= 0) return;
        var amplitude = _vibrator.HasAmplitudeControl
            ? System.Math.Clamp((int)(intensity * 255f), 1, 255)
            : VibrationEffect.DefaultAmplitude;
        _vibrator.Vibrate(VibrationEffect.CreateOneShot(System.Math.Min(milliseconds, 2000), amplitude));
    }

    public void Cancel() => _vibrator?.Cancel();
}
