using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The data half of src/customize/plugins.js (1.8.4-beta): the plugin folder, the templates "Novo plugin"
/// writes, the metadata read from the code without running it and the fingerprint the owner's
/// approval is bound to. Urbe.Core does NOT run plugins: they are full-trust JavaScript.
/// </summary>
public static class LegacyPlugins
{
    public const string Folder = "Personalização/plugins/";
    private const string Marker = "\u0001";

    public static readonly (string Id, string Name, string Description, string Code)[] TemplateData =
    [
        ("basico", "Básico", "Um comando na paleta que mostra um aviso", "// \u0001 — plugin do Urbe. Documentação: Tutorial/Personalização/Plugins.\nurbe.plugin({\n  nome: '\u0001',\n  versao: '1.0',\n  descricao: 'Mostra quantas notas existem',\n\n  ligar(api) {\n    api.comando({\n      titulo: 'Contar notas',\n      executar() {\n        const total = api.notas.listar().length;\n        api.aviso('Esta cidade tem ' + total + ' notas.');\n      }\n    });\n  }\n});\n"),
        ("diario", "Diário", "Botão que abre (ou cria) a nota do dia", "// \u0001 — cria uma nota por dia em Diário/ e abre com um toque.\nurbe.plugin({\n  nome: '\u0001',\n  versao: '1.0',\n  descricao: 'Nota do dia com um toque',\n\n  ligar(api) {\n    function abrirHoje() {\n      const hoje = new Date().toISOString().slice(0, 10);\n      const caminho = 'Diário/' + hoje + '.md';\n      if (!api.notas.existe(caminho)) {\n        api.notas.escrever(caminho, '# ' + hoje.split('-').reverse().join('/') + '\\n\\n');\n      }\n      api.notas.abrir(caminho);\n    }\n    api.comando({ titulo: 'Abrir nota de hoje', executar: abrirHoje });\n    api.botao({ icone: 'clock', titulo: 'Nota de hoje', executar: abrirHoje });\n  }\n});\n"),
        ("assistente", "Ferramenta do Assistente", "Ensina uma habilidade nova ao Assistente", "// \u0001 — o Assistente ganha uma ferramenta para saber a data e a hora.\nurbe.plugin({\n  nome: '\u0001',\n  versao: '1.0',\n  descricao: 'Data e hora para o Assistente',\n\n  ligar(api) {\n    api.ia.ferramenta({\n      nome: 'agora',\n      descricao: 'Devolve a data e a hora atuais do aparelho do usuário.',\n      executar() {\n        return new Date().toLocaleString('pt-BR');\n      }\n    });\n  }\n});\n"),
    ];


    public sealed record Template(string Id, string Name, string Description);
    public static IReadOnlyList<Template> Templates { get; } = TemplateData.Select(t => new Template(t.Id, t.Name, t.Description)).ToArray();
    public sealed record Info(string Path, string Name, string Version, string Description, int Size);

    /// <summary>novo(nome, modelo): path (first free "name.js", "name 2.js"...) and code.</summary>
    public static (string Path, string Content) Create(string? name, string? template, Func<string, bool> exists)
    {
        name = LegacyCustomize.JsTrim(name ?? "");
        if (name.Length == 0) name = "Meu plugin";
        var m = TemplateData.FirstOrDefault(t => t.Id == template);
        if (m.Id is null) m = TemplateData[0];
        var basePath = Folder + Regex.Replace(name, @"[\\/:*?""<>|]", "-");
        var p = basePath + ".js";
        for (int i = 2; exists(p); i++) p = basePath + " " + i + ".js";
        return (p, m.Code.Replace(Marker, name.Replace("'", "\\'")));
    }

    /// <summary>ler(d): name, version and description read from the code without executing it.</summary>
    public static Info Read(string path, string title, string content)
    {
        var m = Regex.Match(content, @"nome\s*:\s*(['""`])([^'""`\n]{1,80})\1");
        var v = Regex.Match(content, @"versao\s*:\s*(['""`])([^'""`\n]{1,20})\1");
        var d = Regex.Match(content, @"descricao\s*:\s*(['""`])([^'""`\n]{1,200})\1");
        return new Info(path, m.Success ? m.Groups[2].Value : Regex.Replace(title, @"\.js$", "", RegexOptions.IgnoreCase),
            v.Success ? v.Groups[2].Value : "", d.Success ? d.Groups[2].Value : "", content.Length);
    }

    /// <summary>impressao(txt): "sha256:" + hex of the UTF-8 bytes.</summary>
    public static string Fingerprint(string content) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
