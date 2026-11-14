using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidUri = Android.Net.Uri;
using Lunet.Content;

namespace Lunet.Android;

/// <summary>SAF image picker for games, with no storage permission or content URI exposed to game code.</summary>
internal sealed class AndroidGameImagePicker(Activity activity) : IGameImagePicker
{
    internal const int RequestCode = 4110;
    private const int MaxImageSide = 1024;
    private readonly object _gate = new();
    private PickedGameImage? _pending;
    private bool _active;

    public bool CanPick => true;

    public void Pick()
    {
        lock (_gate)
        {
            if (_active) return;
            _active = true;
            _pending = null;
        }
        activity.RunOnUiThread(() =>
        {
            try
            {
                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("image/*");
                activity.StartActivityForResult(intent, RequestCode);
            }
            catch (Exception ex) { Publish(new PickedGameImage(0, 0, null, ex.Message)); }
        });
    }

    /// <summary>Called by the hosting Activity. Decoding happens off the UI and GL threads.</summary>
    internal void Complete(AndroidUri? uri)
    {
        lock (_gate) { if (!_active) return; }
        if (uri is null) { Publish(new PickedGameImage(0, 0, null)); return; }
        _ = Task.Run(() =>
        {
            try
            {
                using var header = activity.ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Não foi possível abrir a foto.");
                using var options = new BitmapFactory.Options { InJustDecodeBounds = true };
                using (var ignored = BitmapFactory.DecodeStream(header, null, options)) { }
                if (options.OutWidth <= 0 || options.OutHeight <= 0)
                    throw new InvalidDataException("Imagem inválida ou formato não suportado.");

                int sample = 1;
                int largest = Math.Max(options.OutWidth, options.OutHeight);
                while ((long)largest > (long)sample * MaxImageSide) sample *= 2;
                using var data = activity.ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Não foi possível ler a foto.");
                using var decodeOptions = new BitmapFactory.Options { InSampleSize = sample };
                using var bitmap = BitmapFactory.DecodeStream(data, null, decodeOptions)
                    ?? throw new InvalidDataException("Não foi possível decodificar a foto.");
                int width = bitmap.Width, height = bitmap.Height;
                if (width <= 0 || height <= 0 || width > MaxImageSide || height > MaxImageSide)
                    throw new InvalidDataException("Dimensões da imagem inválidas.");

                var argb = new int[checked(width * height)];
                bitmap.GetPixels(argb, 0, width, 0, 0, width, height);
                var rgba = new byte[checked(argb.Length * 4)];
                for (int i = 0, p = 0; i < argb.Length; i++, p += 4)
                {
                    int color = argb[i];
                    rgba[p] = (byte)(color >> 16);
                    rgba[p + 1] = (byte)(color >> 8);
                    rgba[p + 2] = (byte)color;
                    rgba[p + 3] = (byte)(color >> 24);
                }
                Publish(new PickedGameImage(width, height, rgba));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OutOfMemoryException or
                                       Java.Lang.Exception or UnauthorizedAccessException or ArgumentException)
            {
                Publish(new PickedGameImage(0, 0, null, ex.Message));
            }
        });
    }

    public bool TryReceive(out PickedGameImage result)
    {
        lock (_gate)
        {
            if (_pending is null) { result = null!; return false; }
            result = _pending;
            _pending = null;
            return true;
        }
    }

    private void Publish(PickedGameImage result)
    {
        lock (_gate) { _pending = result; _active = false; }
    }
}
