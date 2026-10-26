namespace Urbe.Client;

public sealed partial class MainView
{
    private bool _compatibilityLockedEditor;
    private bool _readOnlyBeforeCompatibility;

    private void ApplyEditorCompatibility(string path)
    {
        if (_compatibilityLockedEditor)
        {
            _compatibilityLockedEditor = false;
            Editor.ReadOnly = _readOnlyBeforeCompatibility;
        }
        if (_snapshot?.FutureFiles.Contains(path, StringComparer.OrdinalIgnoreCase) != true) return;
        _readOnlyBeforeCompatibility = Editor.ReadOnly;
        _compatibilityLockedEditor = true;
        Editor.ReadOnly = true;
        ShowLifeNotice("Arquivo de versão incompatível: leitura protegida. Consulte Configurações → Compatibilidade do vault para criar uma cópia.", Ui.ToastKind.Warning);
    }

    private void PreventUnsafeEditorToggle(bool readOnly)
    {
        if (_compatibilityLockedEditor && !readOnly)
        {
            Editor.ReadOnly = true;
            ShowLifeNotice("Este arquivo é incompatível com a versão instalada. Crie uma cópia para editar sem modificar o original.", Ui.ToastKind.Warning);
        }
    }
}
