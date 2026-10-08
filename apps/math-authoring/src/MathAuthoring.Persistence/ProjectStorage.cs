namespace MathAuthoring.Persistence;

public enum ProjectCopy
{
    Primary,
    Autosave,
    Backup,
    AutosaveBackup
}

public enum SaveCheckpoint
{
    AfterTempSync,
    AfterPublish
}

public sealed record ProjectRecovery(
    PortableProject Project,
    ProjectCopy SelectedCopy,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Local-filesystem adapter only. Does not assume Android SAF supports atomic
/// rename; Android document providers must import/export using ProjectPackage
/// streams and retain their own transaction boundary.
/// </summary>
public static class ProjectStorage
{
    public static void Save(string path, PortableProject project, Action<SaveCheckpoint>? fault = null) =>
        SaveFile(path, project, fault);

    public static void Autosave(string path, PortableProject project, Action<SaveCheckpoint>? fault = null) =>
        SaveFile(path + ".autosave", project, fault);

    public static ProjectRecovery Recover(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var candidates = new[]
        {
            (path, ProjectCopy.Primary),
            (path + ".autosave", ProjectCopy.Autosave),
            (path + ".bak", ProjectCopy.Backup),
            (path + ".autosave.bak", ProjectCopy.AutosaveBackup)
        };
        var recovered = new List<(PortableProject Project, ProjectCopy Copy)>();
        var warnings = new List<string>();
        bool found = false;
        foreach (var (candidate, copy) in candidates)
        {
            if (!File.Exists(candidate)) continue;
            found = true;
            try
            {
                using var stream = File.OpenRead(candidate);
                recovered.Add((ProjectPackage.Read(stream), copy));
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                warnings.Add($"{copy}: {e.GetType().Name}");
            }
        }
        if (recovered.Count == 0)
        {
            if (!found) throw new FileNotFoundException("Nenhum checkpoint de projeto encontrado.", path);
            throw new InvalidDataException("Todos os checkpoints estão inválidos: " + string.Join(", ", warnings));
        }

        if (recovered.Select(x => x.Project.Document.ProjectId).Distinct(StringComparer.Ordinal).Count() > 1)
            throw new InvalidDataException("Checkpoints pertencem a projetos diferentes; escolha manual necessária.");

        // Revision is the explicit source of freshness, not clock time.
        // Tie-break order is Primary, Autosave, Backup, AutosaveBackup.
        var selected = recovered.OrderByDescending(x => x.Project.Revision)
            .ThenBy(x => x.Copy)
            .First();
        return new ProjectRecovery(selected.Project, selected.Copy, warnings.AsReadOnly());
    }

    private static void SaveFile(string path, PortableProject project, Action<SaveCheckpoint>? fault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        var absolute = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(absolute)
            ?? throw new ArgumentException("Path sem diretório.", nameof(path));
        Directory.CreateDirectory(directory);
        var backup = absolute + ".bak";
        var temp = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            // Do not overwrite a damaged primary and then rotate its corrupted
            // bytes into the backup slot. Recovery must be explicit.
            if (File.Exists(absolute))
            {
                using var existing = File.OpenRead(absolute);
                var previous = ProjectPackage.Read(existing);
                if (!string.Equals(previous.Document.ProjectId, project.Document.ProjectId, StringComparison.Ordinal))
                    throw new InvalidDataException("Destino pertence a outro projeto.");
                if (project.Revision <= previous.Revision)
                    throw new InvalidDataException("Revisão de salvamento não avança o checkpoint existente.");
            }
            else if (File.Exists(backup))
            {
                throw new InvalidDataException("Backup existente sem primário: recuperar antes de salvar.");
            }

            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 8192, FileOptions.WriteThrough))
            {
                ProjectPackage.Write(stream, project);
                stream.Flush(flushToDisk: true);
            }

            // Read back the bytes to detect incomplete/corrupt temporary output.
            using (var verification = File.OpenRead(temp))
                _ = ProjectPackage.Read(verification);

            fault?.Invoke(SaveCheckpoint.AfterTempSync);

            if (File.Exists(absolute))
                File.Replace(temp, absolute, backup, ignoreMetadataErrors: true);
            else
                File.Move(temp, absolute);

            fault?.Invoke(SaveCheckpoint.AfterPublish);
        }
        finally
        {
            // A temporary candidate is never presented as a valid checkpoint.
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
