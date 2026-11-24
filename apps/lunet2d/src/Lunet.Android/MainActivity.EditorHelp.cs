using Android.App;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Lunet.Android.Editor;
using Lunet.Editor;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private CancellationTokenSource? _analysis;
    private long _analysisRevision;
    private int _helpVersion = -1, _helpCaret = -1;
    private bool _helpComposing, _helpFocused;
    private LinearLayout? _signatureRow;
    private CallSignatureHelp? _visibleSignature;
    private int _signatureIndex;
    private int? _dismissedCall;
    private int? _dismissedCompletionCaret;

    private View BuildEditorHelp()
    {
        var host = Vertical();
        _signatureRow = LunetUi.Row(this, 2);
        _signatureRow.SetBackgroundColor(LunetUi.Surface);
        _signatureRow.Visibility = ViewStates.Gone;
        host.AddView(_signatureRow);
        host.AddView(_chipScroll);
        return host;
    }

    private void InvalidateEditorAnalysis()
    {
        _analysis?.Cancel();
        _helpVersion = -1;
        _helpCaret = -1;
        _dismissedCall = null;
        _dismissedCompletionCaret = null;
        _visibleSignature = null;
        HideChips();
        if (_signatureRow is not null) _signatureRow.Visibility = ViewStates.Gone;
    }

    private bool DismissEditorHelp()
    {
        var visible = _visibleSignature is not null || _chipScroll?.Visibility == ViewStates.Visible;
        if (_visibleSignature is not null) _dismissedCall = _visibleSignature.CallStart;
        if (_editor is { } editor) _dismissedCompletionCaret = editor.SelectionStart;
        _visibleSignature = null;
        HideChips();
        if (_signatureRow is not null) _signatureRow.Visibility = ViewStates.Gone;
        return visible;
    }

    private void OnEditorStateChanged(CodeEditText editor)
    {
        if (!ReferenceEquals(_editor, editor)) return;
        RefreshUndoButtons();
        var caret = editor.SelectionStart;
        bool composition = editor.HasComposition;
        if (_helpVersion == editor.Version && _helpCaret == caret && _helpComposing == composition && _helpFocused == editor.HasFocus) return;
        if (_helpVersion != editor.Version || _helpCaret != caret) _dismissedCompletionCaret = null;
        _helpVersion = editor.Version;
        _helpCaret = caret;
        _helpComposing = composition;
        _helpFocused = editor.HasFocus;
        HideChips();
        if (_signatureRow is not null) _signatureRow.Visibility = ViewStates.Gone;
        ScheduleEditorAnalysis(editor, delay: 140);
    }

    private void OnEditorSettled(CodeEditText editor, string text, int version, int caret)
    {
        if (!ReferenceEquals(_editor, editor) || editor.Version != version || !editor.IsAttachedToWindow) return;
        _minimap?.Refresh(text);
        if (_openFile is { } path && _session.IsDirty(text))
        {
            try { _journal?.WriteBuffer(path, text); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Lunet.Core.ProjectException) { }
        }
        RefreshUndoButtons();
        if (_helpVersion != version || _helpCaret != caret || _helpComposing != editor.HasComposition)
            OnEditorStateChanged(editor);
    }

    private async void ScheduleEditorAnalysis(CodeEditText editor, int delay)
    {
        _analysis?.Cancel();
        if (_assistant is not { } assistant || _openFile is not { } path || !IsCSharpOpen) return;
        var request = new CancellationTokenSource();
        _analysis = request;
        var token = request.Token;
        int version = editor.Version, caret = editor.SelectionStart;
        bool show = editor.HasFocus && !editor.HasComposition && caret == editor.SelectionEnd;
        long revision = ++_analysisRevision;
        try
        {
            await Task.Delay(delay, token);
            if (!IsCurrentEditorSnapshot(editor, path, version, caret)) return;
            var result = await assistant.AnalyzeAsync(path, editor.Text ?? "", show ? caret : -1, version, revision, show, token);
            if (token.IsCancellationRequested || !IsCurrentEditorSnapshot(editor, path, result.Version, caret)) return;
            if (!ReferenceEquals(_problems, result.Diagnostics))
            {
                _problems = result.Diagnostics;
                if (!_showConsole && !_showProfiler && _layoutStore.Current.PanelTab != "learn") SetPanel(false);
            }
            editor.FoldRegions = result.Folds;
            if (show && editor.HasFocus && !editor.HasComposition)
            {
                ShowSignature(result.Signature);
                if (_dismissedCompletionCaret != caret) ShowChips(result.Completions, editor, path, version, caret);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { global::Android.Util.Log.Warn("Lunet", "Falha na análise do editor: " + ex.GetType().Name); }
        finally
        {
            if (ReferenceEquals(_analysis, request)) _analysis = null;
            request.Dispose();
        }
    }

    private bool IsCurrentEditorSnapshot(CodeEditText editor, string path, int version, int caret) =>
        ReferenceEquals(editor, _editor) && editor.IsAttachedToWindow && _project is not null && _openFile == path &&
        editor.Version == version && editor.SelectionStart == caret;

    private void HideChips()
    {
        _chips?.RemoveAllViews();
        if (_chipScroll is not null) _chipScroll.Visibility = ViewStates.Gone;
    }

    private void ShowChips(IReadOnlyList<CompletionItem> items, CodeEditText editor, string path, int version, int caret)
    {
        if (_chips is null || _chipScroll is null) return;
        _chips.RemoveAllViews();
        foreach (var item in items.Take(30))
        {
            var chip = LunetUi.Button(this, $"{Symbol(item.Kind)} {item.Label}", () =>
            {
                if (!IsCurrentEditorSnapshot(editor, path, version, caret) || editor.HasComposition || editor.SelectionEnd != caret) return;
                int position = item.ReplaceStart + item.InsertText.Length;
                editor.ApplyEdit(new(item.ReplaceStart, item.ReplaceLength, item.InsertText, position, position));
                HideChips();
            }, ButtonStyle.Secondary, keepsKeyboard: true, description: item.Label + ". " + item.Detail);
            chip.TextSize = 13;
            _chips.AddView(chip, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(44)));
        }
        _chipScroll.ScrollTo(0, 0);
        RefreshCompletionVisibility();
    }

    private void RefreshCompletionVisibility()
    {
        if (_chipScroll is null) return;
        bool signatureInLandscape = IsLandscape && _keyboardVisible && _signatureRow?.Visibility == ViewStates.Visible;
        _chipScroll.Visibility = _chips?.ChildCount > 0 && !signatureInLandscape ? ViewStates.Visible : ViewStates.Gone;
    }

    private void CloseCurrentSignature()
    {
        _dismissedCall = _visibleSignature?.CallStart;
        _visibleSignature = null;
        if (_signatureRow is not null) _signatureRow.Visibility = ViewStates.Gone;
        RefreshCompletionVisibility();
    }

    private void ShowSignature(CallSignatureHelp? help)
    {
        if (_signatureRow is not { } row) return;
        if (help?.CallStart != _dismissedCall) _dismissedCall = null;
        if (help is null || help.CallStart == _dismissedCall)
        {
            _visibleSignature = null;
            row.Visibility = ViewStates.Gone;
            return;
        }
        if (_visibleSignature?.CallStart != help.CallStart || _signatureIndex == _visibleSignature.Selected)
            _signatureIndex = help.Selected;
        _signatureIndex = Math.Clamp(_signatureIndex, 0, help.Signatures.Count - 1);
        _visibleSignature = help;
        DrawSignature();
    }

    private void DrawSignature()
    {
        if (_visibleSignature is not { } help || _signatureRow is not { } row) return;
        var signature = help.Signatures[_signatureIndex];
        var label = signature.Label + "(";
        var start = -1;
        var end = -1;
        for (int i = 0; i < signature.Parameters.Count; i++)
        {
            if (i > 0) label += ", ";
            if (i == signature.ActiveParameter) start = label.Length;
            label += signature.Parameters[i].Display;
            if (i == signature.ActiveParameter) end = label.Length;
        }
        using var styled = new SpannableString(label + ")");
        if (start >= 0)
        {
            styled.SetSpan(new StyleSpan(TypefaceStyle.Bold), start, end, SpanTypes.ExclusiveExclusive);
            styled.SetSpan(new ForegroundColorSpan(LunetUi.AccentText), start, end, SpanTypes.ExclusiveExclusive);
        }
        row.RemoveAllViews();
        if (help.Signatures.Count > 1)
        {
            var next = LunetUi.Button(this, $"{_signatureIndex + 1}/{help.Signatures.Count} ›", () =>
            {
                _signatureIndex = (_signatureIndex + 1) % help.Signatures.Count;
                DrawSignature();
            }, ButtonStyle.Quiet, keepsKeyboard: true, description: "Próxima sobrecarga");
            row.AddView(next, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(48)));
        }
        var text = LunetUi.Label(this, "", 13, LunetUi.Text);
        text.TextFormatted = styled;
        text.SetSingleLine(true);
        text.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
        var active = signature.ActiveParameter is int parameter ? ". Parâmetro ativo: " + signature.Parameters[parameter].Name : "";
        text.ContentDescription = label + ")" + active;
        var scroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        scroll.AddView(text);
        row.AddView(scroll, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        row.AddView(LunetUi.Icon(this, "×", "Fechar ajuda de assinatura", CloseCurrentSignature, keepsKeyboard: true),
            new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        row.SetGravity(GravityFlags.CenterVertical);
        row.Visibility = ViewStates.Visible;
        RefreshCompletionVisibility();
        if (start >= 0) scroll.Post(() => scroll.ScrollTo((int)text.Paint!.MeasureText(label[..start]) - Dp(12), 0));
    }

    private async void GoToDefinition()
    {
        if (_editor is not { } editor || _openFile is not { } path || _assistant is not { } assistant) return;
        var version = editor.Version;
        var caret = editor.SelectionStart;
        var source = editor.Text ?? "";
        var revision = ++_analysisRevision;
        try
        {
            var result = await assistant.RunAsync(a =>
            {
                assistant.UpdateSnapshot(path, source, revision);
                return (Location: a.GetDefinition(path, caret), Hover: a.GetHover(path, caret));
            });
            if (!IsCurrentEditorSnapshot(editor, path, version, caret)) return;
            if (result.Location is { } location) Jump(location.FilePath, location.Line, location.Column);
            else if (result.Hover is { DocumentationId: not null } hover &&
                     DocumentationPanel.TargetFor(this, hover.DocumentationId, source.Substring(hover.Start, hover.Length)) is { } target)
                DocumentationPanel.Open(this, target);
            else Toast.MakeText(this, "Definição ou documentação indisponível.", ToastLength.Short)?.Show();
        }
        catch (Exception ex) { global::Android.Util.Log.Warn("Lunet", "Falha ao navegar: " + ex.GetType().Name); }
    }

    private void ShowProjectSymbols()
    {
        if (_assistant is not { } assistant || _project is null) return;
        var form = Vertical();
        form.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        var input = new EditText(this) { Hint = "Nome do tipo ou método", ContentDescription = "Buscar símbolo pelo nome" };
        input.SetSingleLine(true);
        form.AddView(input);
        var results = Vertical();
        var scroll = new ScrollView(this);
        scroll.AddView(results);
        form.AddView(scroll, LunetUi.FillWidth(Dp(250)));
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Símbolos do projeto")!.SetView(form)!
            .SetNegativeButton("Fechar", (_, _) => { })!.Create();
        int generation = 0;
        async void Search()
        {
            int request = ++generation;
            string query = input.Text?.Trim() ?? "";
            results.RemoveAllViews();
            if (query.Length == 0) return;
            var path = _openFile;
            var source = _editor?.Text ?? "";
            long revision = ++_analysisRevision;
            await Task.Delay(140);
            if (!dialog.IsShowing || request != generation || !ReferenceEquals(_assistant, assistant)) return;
            try
            {
                var found = await assistant.RunAsync(a =>
                {
                    if (path is not null && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        assistant.UpdateSnapshot(path, source, revision);
                    return a.FindProjectSymbols(query);
                });
                if (!dialog.IsShowing || request != generation || !ReferenceEquals(_assistant, assistant)) return;
                if (found.Count == 0) results.AddView(LunetUi.Label(this, "Nenhum símbolo encontrado.", 14, LunetUi.TextMuted));
                foreach (var result in found)
                    results.AddView(LunetUi.Button(this, $"{result.Display}\n{result.Location.FilePath}:{result.Location.Line}", () =>
                    {
                        dialog.Dismiss();
                        Jump(result.Location.FilePath, result.Location.Line, result.Location.Column);
                    }, ButtonStyle.Quiet));
            }
            catch (Exception ex) { global::Android.Util.Log.Warn("Lunet", "Falha ao buscar símbolos: " + ex.GetType().Name); }
        }
        input.TextChanged += (_, _) => Search();
        dialog.Show();
    }
}
