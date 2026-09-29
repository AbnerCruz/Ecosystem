using System.Reflection;

namespace Lunet.Docs;

/// <summary>Gera a <see cref="ApiDocumentation"/> de um assembly a partir de reflexão e do XML de documentação.</summary>
public static class ApiDocGenerator
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static ApiDocumentation Generate(Assembly assembly, XmlDocReader xml, string version)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => !t.IsNested)
            .OrderBy(t => t.Namespace, StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => Describe(t, xml, version))
            .ToList();
        return new ApiDocumentation(assembly.GetName().Name ?? "", version, types);
    }

    private static TypeDoc Describe(Type type, XmlDocReader xml, string version)
    {
        var id = DocumentationId.For(type);
        var entry = xml.Get(id);
        var members = new List<MemberDoc>();

        foreach (var member in type.GetMembers(Declared).OrderBy(SortKey).ThenBy(m => m.Name, StringComparer.Ordinal))
        {
            var doc = Describe(member, xml, version);
            if (doc is not null) members.Add(doc);
        }
        return new TypeDoc(
            id,
            type.Name.Contains('`') ? type.Name[..type.Name.IndexOf('`')] : type.Name,
            type.Namespace ?? "",
            KindOf(type),
            SignatureFormatter.Type(type),
            entry?.Summary ?? "",
            entry?.Remarks ?? "",
            entry?.Examples ?? [],
            (entry?.Related ?? []).Select(Short).ToList(),
            entry?.Since ?? version,
            members);
    }

    private static int SortKey(MemberInfo m) => m switch
    {
        ConstructorInfo => 0,
        FieldInfo => 1,
        PropertyInfo => 2,
        EventInfo => 3,
        MethodInfo => 4,
        _ => 5,
    };

    /// <summary>Membros que não fazem parte da API que o usuário escreve: gerados pelo compilador (records), sobrescritas de object e construtores padrão sem parâmetros.</summary>
    private static bool IsNoise(MemberInfo member)
    {
        if (member.GetCustomAttributes().Any(a => a.GetType().Name is "CompilerGeneratedAttribute")) return true;
        if (member.Name.Contains('<') || member.Name is "EqualityContract" or "PrintMembers" or "Deconstruct") return true;
        if (member is MethodInfo { Name: "Equals" or "GetHashCode" or "ToString" or "GetType" or "Finalize" or "MemberwiseClone" }) return true;
        if (member is ConstructorInfo { IsStatic: false } c && c.GetParameters().Length == 0) return true;
        return false;
    }

    private static MemberDoc? Describe(MemberInfo member, XmlDocReader xml, string version)
    {
        if (IsNoise(member)) return null;
        string signature, kind;
        IReadOnlyList<ParameterInfo> parameters = [];
        string returnType = "";
        switch (member)
        {
            case ConstructorInfo c when IsVisible(c):
                (signature, kind, parameters) = (SignatureFormatter.Method(c), "constructor", c.GetParameters());
                break;
            case MethodInfo m when IsVisible(m) && !m.IsSpecialName:
                (signature, kind, parameters) = (SignatureFormatter.Method(m), "method", m.GetParameters());
                returnType = m.ReturnType == typeof(void) ? "" : SignatureFormatter.TypeName(m.ReturnType);
                break;
            case PropertyInfo p when (p.GetMethod is { } g && IsVisible(g)) || (p.SetMethod is { } s && IsVisible(s)):
                (signature, kind, parameters) = (SignatureFormatter.Property(p), "property", p.GetIndexParameters());
                break;
            case FieldInfo f when (f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly) && !f.IsSpecialName:
                (signature, kind) = (SignatureFormatter.Field(f), f.IsLiteral && f.DeclaringType!.IsEnum ? "enum value" : "field");
                break;
            case EventInfo e when e.AddMethod is { } add && IsVisible(add):
                (signature, kind) = (SignatureFormatter.Event(e), "event");
                break;
            default:
                return null;
        }

        var id = DocumentationId.For(member);
        var entry = xml.Get(id);
        var paramDocs = parameters.Select(p => new ParameterDoc(p.Name ?? "", SignatureFormatter.TypeName(p.ParameterType),
            entry is not null && p.Name is not null && entry.Parameters.TryGetValue(p.Name, out var text) ? text : "")).ToList();
        return new MemberDoc(
            id,
            member is ConstructorInfo ? member.DeclaringType!.Name.Split('`')[0] : member.Name,
            kind,
            signature,
            entry?.Summary ?? "",
            entry?.Remarks ?? "",
            paramDocs,
            entry?.Returns ?? "",
            entry?.Examples ?? [],
            (entry?.Related ?? []).Select(Short).ToList(),
            entry?.Since ?? version);
    }

    private static string Short(string cref) => cref;

    private static bool IsVisible(MethodBase m) => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;

    private static string KindOf(Type type) =>
        type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : type.BaseType == typeof(MulticastDelegate) ? "delegate" : "class";
}
