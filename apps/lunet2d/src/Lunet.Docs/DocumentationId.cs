using System.Reflection;
using System.Text;

namespace Lunet.Docs;

/// <summary>Identificadores de documentação no formato dos XML docs do C# (<c>T:</c>, <c>M:</c>, <c>P:</c>, <c>F:</c>, <c>E:</c>).</summary>
public static class DocumentationId
{
    public static string For(Type type) => "T:" + TypeName(type);

    public static string For(MemberInfo member) => member switch
    {
        Type t => For(t),
        ConstructorInfo c => "M:" + TypeName(c.DeclaringType!) + "." + (c.IsStatic ? "#cctor" : "#ctor") + Parameters(c.GetParameters(), c),
        MethodInfo m => "M:" + TypeName(m.DeclaringType!) + "." + m.Name.Replace('.', '#') + (m.IsGenericMethodDefinition ? "``" + m.GetGenericArguments().Length : "") + Parameters(m.GetParameters(), m),
        PropertyInfo p => "P:" + TypeName(p.DeclaringType!) + "." + p.Name + (p.GetIndexParameters().Length > 0 ? Parameters(p.GetIndexParameters(), p.GetMethod ?? p.SetMethod) : ""),
        FieldInfo f => "F:" + TypeName(f.DeclaringType!) + "." + f.Name,
        EventInfo e => "E:" + TypeName(e.DeclaringType!) + "." + e.Name,
        _ => throw new ArgumentException("Tipo de membro sem identificador de documentação."),
    };

    private static string Parameters(ParameterInfo[] parameters, MethodBase? owner)
    {
        if (parameters.Length == 0) return "";
        return "(" + string.Join(",", parameters.Select(p => ParameterType(p.ParameterType, owner))) + ")";
    }

    private static string ParameterType(Type type, MethodBase? owner)
    {
        if (type.IsByRef) return ParameterType(type.GetElementType()!, owner) + "@";
        if (type.IsPointer) return ParameterType(type.GetElementType()!, owner) + "*";
        if (type.IsArray)
        {
            var element = ParameterType(type.GetElementType()!, owner);
            return type.GetArrayRank() == 1 ? element + "[]" : element + "[" + string.Join(",", Enumerable.Repeat("0:", type.GetArrayRank())) + "]";
        }
        if (type.IsGenericParameter)
        {
            return type.DeclaringMethod is not null ? "``" + type.GenericParameterPosition : "`" + type.GenericParameterPosition;
        }
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var definition = type.GetGenericTypeDefinition();
            var name = TypeName(definition);
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];
            return name + "{" + string.Join(",", type.GetGenericArguments().Select(a => ParameterType(a, owner))) + "}";
        }
        return TypeName(type);
    }

    private static string TypeName(Type type)
    {
        var builder = new StringBuilder();
        if (type.DeclaringType is { } outer) builder.Append(TypeName(outer)).Append('.');
        else if (!string.IsNullOrEmpty(type.Namespace)) builder.Append(type.Namespace).Append('.');
        builder.Append(type.Name);
        return builder.ToString();
    }
}
