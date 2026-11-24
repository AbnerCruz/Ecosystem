using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Android.Editor;
using Lunet.Core;
using Lunet.Editor;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private EditorDocuments _documents = new();
    private readonly Dictionary<string, (string Text, UndoHistory History)> _documentHistories = new(StringComparer.Ordinal);
    private readonly Stack<(string Path, EditorViewport View)> _navigationHistory = new();
    private LinearLayout? _documentTabs;
    private View? _documentTabsStrip;
    private View? _documentEmpty;
    private string? _documentsFile;

    private void LoadEditorDocuments()
    {
        var id = _project is { } project ? StableGameIdForEditor(project.Name) : null;
        _documentsFile = Guid.TryParse(id, out var gameId)
            ? System.IO.Path.Combine(FilesDir!.AbsolutePath!, "editor-sessions", gameId.ToString("N") + ".json") : null;
        _documents = _documentsFile is { } path ? EditorDocuments.Load(path) : new();
        _documents.Reconcile(_project!.ListFiles().Where(IsTextFile));
        _navigationHistory.Clear();
        _documentHistories.Clear();
    }

    private string? StableGameIdForEditor(string name)
    {
        var id = LauncherProjectCatalog.UniqueIdForName(_launcherCatalog, name);
        return LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, id) == name ? id : null;
    }

    private void CaptureDocument()
    {
        if (_editor is not { } editor || _openFile is not { } path) return;
        _documents.Capture(CurrentEditorViewport(editor));
        _documentHistories[path] = (editor.Text ?? "", editor.History);
    }

    private static EditorViewport CurrentEditorViewport(CodeEditText editor) =>
        new(editor.SelectionStart, editor.SelectionEnd, editor.ScrollX, editor.ScrollY);

    private void PersistDocuments()
    {
        if (_documentsFile is not { } path) return;
        try { _documents.Save(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { global::Android.Util.Log.Warn("Lunet", "Não foi possível guardar a posição do editor."); }
    }

    private void RestoreDocumentViewport(EditorViewport view)
    {
        if (_editor is not { } editor) return;
        var length = editor.Text?.Length ?? 0;
        editor.SetSelection(Math.Clamp(view.Start, 0, length), Math.Clamp(view.End, 0, length));
        var version = editor.Version;
        editor.Post(() =>
        {
            if (!ReferenceEquals(editor, _editor) || editor.Version != version || editor.Layout is not { } layout) return;
            int maxY = Math.Max(0, layout.Height + editor.ExtendedPaddingTop + editor.ExtendedPaddingBottom - editor.Height);
            editor.ScrollTo(Math.Max(0, view.X), Math.Clamp(view.Y, 0, maxY));
        });
    }

    private View BuildDocumentTabs()
    {
        _documentTabs = LunetUi.Row(this, 2);
        var scroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        scroll.AddView(_documentTabs);
        _documentTabsStrip = scroll;
        return scroll;
    }

    private void RefreshDocumentTabs()
    {
        if (_documentTabs is not { } row) return;
        row.RemoveAllViews();
        if (_navigationHistory.Count > 0)
            row.AddView(LunetUi.Icon(this, "↶", "Voltar à posição anterior", () => BackToEditorPosition(), keepsKeyboard: true),
                new LinearLayout.LayoutParams(Dp(48), Dp(40)));
        foreach (var path in _documents.Tabs)
        {
            bool selected = path == _openFile;
            var label = System.IO.Path.GetFileName(path);
            var button = LunetUi.Button(this, label, () => OpenFile(path), selected ? ButtonStyle.Selected : ButtonStyle.Quiet,
                keepsKeyboard: true, description: "Aba " + path);
            button.TextSize = 12;
            button.SetSingleLine();
            row.AddView(button, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
            if (selected) row.AddView(LunetUi.Icon(this, "×", "Fechar aba " + path, CloseEditorTab, keepsKeyboard: true),
                new LinearLayout.LayoutParams(Dp(48), Dp(40)));
        }
    }

    private void CloseEditorTab()
    {
        if (_openFile is null || !SaveCurrent()) return;
        CaptureDocument();
        _documents.Close();
        _openFile = null;
        _session.Unload();
        if (_documents.Current is { } next) OpenFile(next);
        else ShowEditorWithoutFile();
        PersistDocuments();
    }

    private void ShowEditorWithoutFile()
    {
        _editor?.LoadText("");
        if (_editor is not null) _editor.Visibility = ViewStates.Gone;
        if (_minimap is not null) _minimap.Visibility = ViewStates.Gone;
        if (_documentEmpty is not null) _documentEmpty.Visibility = ViewStates.Visible;
        _status!.Text = "";
        InvalidateEditorAnalysis();
        RefreshDocumentTabs();
        RefreshUndoButtons();
        RefreshKeyboard();
    }

    private bool BackToEditorPosition()
    {
        if (_project is null) return false;
        while (_navigationHistory.TryPeek(out var missing) && !_project.ListFiles().Contains(missing.Path))
            _navigationHistory.Pop();
        if (!_navigationHistory.TryPeek(out var previous)) return false;
        if (!OpenFile(previous.Path)) return true;
        _navigationHistory.Pop();
        RestoreDocumentViewport(previous.View);
        RefreshDocumentTabs();
        return true;
    }
}
