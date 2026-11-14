using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Lunet.Ai;
using Lunet.Core;
using AndroidUri = Android.Net.Uri;

namespace Lunet.Android;

/// <summary>
/// LUNET-488/489: the AI's knowledge of Lunet (offline docs from the APK), image perception (BitmapFactory), file and
/// image attachments, and importing files into the project's Notes/.
/// </summary>
public sealed partial class MainActivity
{
    private const int AiAttachRequestCode = 4105;
    private const int NotesImportRequestCode = 4106;
    private static LunetAiKnowledge? _aiKnowledge;
    private readonly List<string> _aiAttachments = [];
    private LinearLayout? _aiAttachmentRow;

    /// <summary>The Framework API reference and guides already shipped in the APK, indexed once per process.</summary>
    private LunetAiKnowledge AiKnowledge()
    {
        if (_aiKnowledge is not null) return _aiKnowledge;
        var assets = Assets!;
        string Read(string path)
        {
            using var stream = assets.Open(path);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        string? api = null;
        try { api = Read("docs/lunet-framework.json"); } catch (Java.IO.IOException) { }
        var guides = new Dictionary<string, string>();
        foreach (var file in assets.List("docs/guides") ?? [])
            if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                guides[System.IO.Path.GetFileNameWithoutExtension(file)] = Read("docs/guides/" + file);
        return _aiKnowledge = LunetAiKnowledge.Create(api, guides);
    }

    /// <summary>PNG/JPG/WebP decoding for the AI; large photos are subsampled to keep memory bounded.</summary>
    private sealed class AndroidAiImages : ILunetAiImages
    {
        public LunetAiImage? Load(string fullPath, string relativePath)
        {
            using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(fullPath, bounds);
            if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;
            int sample = 1;
            while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= 512) sample *= 2;
            using var options = new BitmapFactory.Options { InSampleSize = sample, InPreferredConfig = Bitmap.Config.Argb8888 };
            using var bitmap = BitmapFactory.DecodeFile(fullPath, options);
            if (bitmap is null) return null;
            int w = bitmap.Width, h = bitmap.Height;
            var pixels = new int[w * h];
            bitmap.GetPixels(pixels, 0, w, 0, 0, w, h);
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = unchecked((uint)pixels[i]);
                rgba[i * 4] = (byte)(p >> 16); rgba[i * 4 + 1] = (byte)(p >> 8); rgba[i * 4 + 2] = (byte)p; rgba[i * 4 + 3] = (byte)(p >> 24);
            }
            return new LunetAiImage(w, h, rgba);
        }
    }

    // ---------- Picking and importing ----------

    private void PickFileForImport(int requestCode)
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "image/*", "text/*", "application/json", "application/pdf", "*/*" });
        intent.PutExtra(Intent.ExtraAllowMultiple, true);
        StartActivityForResult(intent, requestCode);
    }

    private bool HandleAttachmentResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode is not (AiAttachRequestCode or NotesImportRequestCode)) return false;
        if (resultCode != Result.Ok || data is null || _project is null) return true;
        var uris = new List<AndroidUri>();
        if (data.ClipData is { } clip)
            for (int i = 0; i < clip.ItemCount; i++) if (clip.GetItemAt(i)?.Uri is { } u) uris.Add(u);
        if (uris.Count == 0 && data.Data is { } single) uris.Add(single);
        var imported = new List<string>();
        foreach (var uri in uris.Take(10))
        {
            try { imported.Add(ImportIntoNotes(uri)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException or Java.Lang.SecurityException)
            {
                Toast.MakeText(this, "Não foi possível importar: " + ex.Message, ToastLength.Long)?.Show();
            }
        }
        if (imported.Count == 0) return true;
        if (requestCode == AiAttachRequestCode)
        {
            foreach (var path in imported) AddAiAttachment(path);
        }
        else
        {
            Toast.MakeText(this, imported.Count == 1 ? "Importado em " + imported[0] : imported.Count + " arquivos importados em Notes/", ToastLength.Short)?.Show();
            ShowNotes();
        }
        return true;
    }

    /// <summary>Copies a picked document into Notes/ (size-limited), returning its project path.</summary>
    private string ImportIntoNotes(AndroidUri uri)
    {
        string? name = null;
        long size = -1;
        using (var cursor = ContentResolver!.Query(uri, [IOpenableColumns.DisplayName, IOpenableColumns.Size], null, null, null))
        {
            if (cursor is not null && cursor.MoveToFirst())
            {
                name = cursor.GetString(0);
                if (!cursor.IsNull(1)) size = cursor.GetLong(1);
            }
        }
        if (size > ProjectNotes.MaxImportBytes) throw new IOException("arquivo maior que 64 MB");
        var path = ProjectNotes.ImportPath(_project!, name);
        using var input = ContentResolver.OpenInputStream(uri) ?? throw new IOException("arquivo indisponível");
        using var limited = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            limited.Write(buffer, 0, read);
            if (limited.Length > ProjectNotes.MaxImportBytes) throw new IOException("arquivo maior que 64 MB");
        }
        limited.Position = 0;
        _project!.Import(ProjectNotes.Folder, System.IO.Path.GetFileName(path), limited);
        return path;
    }

    // ---------- AI attachments ----------

    private void ShowAttachMenu()
    {
        if (_project is null) return;
        var notes = ProjectNotes.List(_project);
        var items = new List<(string Label, Action Run)>
        {
            ("📷  Imagem ou arquivo do aparelho…", () => PickFileForImport(AiAttachRequestCode)),
        };
        foreach (var note in notes.Take(30))
            items.Add(((note.Kind is NoteKind.Sketch or NoteKind.Image ? "▦  " : note.Kind == NoteKind.Text ? "¶  " : "·  ") + note.Path,
                () => AddAiAttachment(note.Path)));
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Anexar à conversa")!
            .SetItems(items.Select(i => i.Label).ToArray(), (_, e) => items[e.Which].Run())!
            .Show();
    }

    private void AddAiAttachment(string path)
    {
        if (!_aiAttachments.Contains(path, StringComparer.Ordinal)) _aiAttachments.Add(path);
        RefreshAttachmentRow();
    }

    private void RefreshAttachmentRow()
    {
        if (_aiAttachmentRow is null) return;
        _aiAttachmentRow.RemoveAllViews();
        foreach (var path in _aiAttachments.ToArray())
        {
            var captured = path;
            _aiAttachmentRow.AddView(LunetUi.Chip(this, (ProjectNotes.IsImage(path) ? "▦ " : "📎 ") + System.IO.Path.GetFileName(path) + "  ✕",
                () => { _aiAttachments.Remove(captured); RefreshAttachmentRow(); }, selected: true));
        }
        if (_aiAttachmentRow.Parent is View scroller)
            scroller.Visibility = _aiAttachments.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
        RefreshSendButton();
    }

    // ---------- Viewing imported images and files ----------

    private void OpenImageNote(string path)
    {
        if (_project is null) return;
        var root = OverlayScaffold(System.IO.Path.GetFileName(path), out var actions);
        actions.AddView(LunetUi.Button(this, "✦ Virar sprite", () =>
        {
            CloseOverlay();
            _aiAttachments.Clear();
            AddAiAttachment(path);
            OpenAiPanel("Crie um sprite a partir deste desenho, coerente com o estilo do projeto (paleta, tamanho e contorno). " +
                        "Depois me diga como usá-lo no jogo.");
        }, ButtonStyle.Primary), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        actions.AddView(LunetUi.Button(this, "✦ Perguntar", () =>
        {
            CloseOverlay();
            _aiAttachments.Clear();
            AddAiAttachment(path);
            OpenAiPanel("");
        }, ButtonStyle.Selected), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        var image = new ImageView(this);
        image.SetScaleType(ImageView.ScaleType.FitCenter!);
        image.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        using (var options = new BitmapFactory.Options { InSampleSize = 1 })
        {
            var full = System.IO.Path.Combine(_project.Directory, path);
            using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(full, bounds);
            while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (options.InSampleSize * 2) >= 2048) options.InSampleSize *= 2;
            var bitmap = BitmapFactory.DecodeFile(full, options);
            if (bitmap is not null)
            {
                image.SetImageBitmap(bitmap);
                // Pixel art stays crisp when scaled up.
                if (Math.Max(bitmap.Width, bitmap.Height) <= 256) image.Drawable!.SetFilterBitmap(false);
            }
        }
        root.AddView(image, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        var hint = LunetUi.Label(this, "A IA enxerga a imagem como uma grade de pixels e redesenha no estilo do projeto.", LunetUi.TextCaption, LunetUi.TextFaint);
        hint.SetPadding(Dp(16), 0, Dp(16), Dp(12));
        root.AddView(hint);
        ShowOverlay(root);
    }

    private void OpenOtherNote(ProjectNote note)
    {
        var path = note.Path;
        bool text = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        if (text) { CloseOverlay(); OpenFile(path); return; }
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(note.Title)!
            .SetMessage("Este tipo de arquivo fica guardado no projeto em " + path + ". O Lunet abre texto, Markdown e imagens; " +
                        "a IA lê arquivos de texto e imagens.")!
            .SetPositiveButton("OK", (_, _) => { })!
            .Show();
    }
}
