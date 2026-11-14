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

    /// <summary>
    /// Grava <paramref name="currentText"/> no arquivo carregado. Devolve falso, sem tocar em disco, se não há arquivo
    /// carregado. Se o arquivo mudou em disco desde a carga (por exemplo, a IA o editou), nunca grava por cima em
    /// silêncio: sem edição local, nada é gravado (o editor só está desatualizado); com edição local, lança
    /// <see cref="EditorConflictException"/> e o chamador decide com a pessoa.
    /// </summary>
    public bool TrySave(LunetProject project, string currentText, AutosaveJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (Path is null) return false;
        var exists = File.Exists(System.IO.Path.Combine(project.Directory, Path));
        if (exists && DiskText(project) is { } disk && disk != SavedText)
        {
            if (currentText == SavedText) return false;
            if (currentText == disk) { SavedText = disk; journal?.Discard(Path); return true; }
            journal?.WriteBuffer(Path, currentText);
            throw new EditorConflictException(Path, disk);
        }
        if (currentText != SavedText || !exists)
            project.WriteText(Path, currentText);
        SavedText = currentText;
        journal?.Discard(Path);
        return true;
    }

    /// <summary>Grava o texto do editor mesmo que o disco tenha mudado (a pessoa escolheu manter a versão dela).</summary>
    public void ForceSave(LunetProject project, string currentText, AutosaveJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (Path is null) return;
        project.WriteText(Path, currentText);
        SavedText = currentText;
        journal?.Discard(Path);
    }

    /// <summary>Verdadeiro se o arquivo carregado foi alterado em disco por outra parte desde a carga ou o último salvamento.</summary>
    public bool ChangedOnDisk(LunetProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Path is not null && DiskText(project) is { } disk && disk != SavedText;
    }

    private string? DiskText(LunetProject project)
    {
        try { return project.ReadText(Path!); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException) { return null; }
    }
}

/// <summary>O arquivo do editor mudou em disco e o editor também tem edições: gravar perderia uma das duas versões.</summary>
public sealed class EditorConflictException(string path, string diskText)
    : IOException($"O arquivo {path} foi alterado fora do editor (por exemplo, pela IA) enquanto você também o editava.")
{
    public string FilePath { get; } = path;
    public string DiskText { get; } = diskText;
}
