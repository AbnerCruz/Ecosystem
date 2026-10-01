namespace Lunet.Core;

/// <summary>
/// Sabe qual arquivo o editor realmente carregou. Só permite salvar quando há um arquivo carregado, o que impede
/// gravar por cima de um arquivo o texto de um editor recém-criado (e vazio), como acontecia ao voltar do Preview.
/// </summary>
public sealed class EditorSession
{
    /// <summary>Arquivo (relativo ao projeto) cujo conteúdo está no editor; nulo se o editor não tem nada carregado.</summary>
    public string? Path { get; private set; }

    /// <summary>Texto como está em disco desde o último carregamento ou salvamento.</summary>
    public string SavedText { get; private set; } = "";

    public bool HasFile => Path is not null;

    public void Load(string path, string text)
    {
        Path = path;
        SavedText = text;
    }

    /// <summary>O editor foi recriado ou fechado: nada do que ele contém pode ser gravado.</summary>
    public void Unload()
    {
        Path = null;
        SavedText = "";
    }

    public bool IsDirty(string currentText) => Path is not null && currentText != SavedText;

    /// <summary>Grava <paramref name="currentText"/> no arquivo carregado. Devolve falso, sem tocar em disco, se não há arquivo carregado.</summary>
    public bool TrySave(LunetProject project, string currentText, AutosaveJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (Path is null) return false;
        if (currentText != SavedText || !File.Exists(System.IO.Path.Combine(project.Directory, Path)))
            project.WriteText(Path, currentText);
        SavedText = currentText;
        journal?.Discard(Path);
        return true;
    }
}
