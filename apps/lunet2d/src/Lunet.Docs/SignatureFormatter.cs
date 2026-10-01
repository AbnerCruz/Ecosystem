using System.Reflection;
using System.Text;

namespace Lunet.Docs;

/// <summary>Assinaturas em C# a partir de reflexão (com <c>?</c> de nulabilidade quando os metadados existem).</summary>
public static class SignatureFormatter
{
    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte", [typeof(short)] = "short",
        [typeof(ushort)] = "ushort", [typeof(int)] = "int", [typeof(uint)] = "uint", [typeof(long)] = "long", [typeof(ulong)] = "ulong",
        [typeof(float)] = "float", [typeof(double)] = "double", [typeof(decimal)] = "decimal", [typeof(char)] = "char",
        [typeof(string)] = "string", [typeof(object)] = "object",
    };

    public static string TypeName(Type type, NullabilityInfo? nullability = null)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!, nullability?.ElementType);
        if (type.IsPointer) return TypeName(type.GetElementType()!) + "*";
        if (type.IsArray)
            return TypeName(type.GetElementType()!, nullability?.ElementType) + "[" + new string(',', type.GetArrayRank() - 1) + "]" + NullMark(type, nullability);
        if (Keywords.TryGetValue(type, out var keyword)) return keyword + NullMark(type, nullability);
        if (type.IsGenericParameter) return type.Name;
        if (System.Nullable.GetUnderlyingType(type) is { } underlying) return TypeName(underlying) + "?";
        if (type.IsGenericType)
        {
            var name = BaseName(type.Name);
            var args = type.GetGenericArguments();
            var formatted = string.Join(", ", args.Select((a, i) => TypeName(a, nullability is { GenericTypeArguments.Length: > 0 } && i < nullability.GenericTypeArguments.Length ? nullability.GenericTypeArguments[i] : null)));
            return name + "<" + formatted + ">" + NullMark(type, nullability);
        }
        return (type.DeclaringType is { } outer ? TypeName(outer) + "." : "") + type.Name + NullMark(type, nullability);
    }

    private static string NullMark(Type type, NullabilityInfo? info) =>
        info is not null && !type.IsValueType && info.ReadState == NullabilityState.Nullable ? "?" : "";

    public static string Type(Type type)
    {
        var builder = new StringBuilder();
        builder.Append(type.IsPublic || type.IsNestedPublic ? "public " : "internal ");
        if (type.IsEnum) return builder.Append("enum ").Append(type.Name).ToString();
        if (type.IsInterface) return builder.Append("interface ").Append(NameWithGenerics(type)).ToString();
        if (type.IsValueType)
        {
            if (type.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute")) builder.Append("readonly ");
            if (type.GetCustomAttributes().Any(a => a.GetType().Name == "IsByRefLikeAttribute")) builder.Append("ref ");
            return builder.Append("struct ").Append(NameWithGenerics(type)).ToString();
        }
        if (type.IsSealed && type.IsAbstract) builder.Append("static ");
        else if (type.IsSealed) builder.Append("sealed ");
        else if (type.IsAbstract) builder.Append("abstract ");
        builder.Append(type.BaseType == typeof(MulticastDelegate) ? "delegate " : "class ").Append(NameWithGenerics(type));
        var baseTypes = new List<string>();
        if (type.BaseType is { } b && b != typeof(object) && b != typeof(ValueType)) baseTypes.Add(TypeName(b));
        baseTypes.AddRange(type.GetInterfaces().Where(i => i.IsPublic).Select(i => TypeName(i)));
        if (baseTypes.Count > 0) builder.Append(" : ").Append(string.Join(", ", baseTypes));
        return builder.ToString();
    }

    private static string BaseName(string name)
    {
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    private static string NameWithGenerics(Type type) =>
        type.IsGenericType ? BaseName(type.Name) + "<" + string.Join(", ", type.GetGenericArguments().Select(a => a.Name)) + ">" : type.Name;

    public static string Method(MethodBase method)
    {
        var ctx = new NullabilityInfoContext();
        var builder = new StringBuilder(Access(method)).Append(' ');
        if (method.IsStatic) builder.Append("static ");
        else if (method.IsAbstract) builder.Append("abstract ");
        else if (method.IsVirtual && !method.IsFinal && method.DeclaringType is { IsInterface: false })
            builder.Append(method is MethodInfo m && m.GetBaseDefinition().DeclaringType != m.DeclaringType ? "override " : "virtual ");
        if (method is MethodInfo info)
            builder.Append(TypeName(info.ReturnType, ctx.Create(info.ReturnParameter))).Append(' ').Append(info.Name);
        else
            builder.Append(method.DeclaringType!.Name.Split('`')[0]);
        if (method.IsGenericMethodDefinition) builder.Append('<').Append(string.Join(", ", method.GetGenericArguments().Select(a => a.Name))).Append('>');
        builder.Append('(').Append(string.Join(", ", method.GetParameters().Select(p => Parameter(p, ctx)))).Append(')');
        return builder.ToString();
    }

    public static string Parameter(ParameterInfo p, NullabilityInfoContext? ctx = null)
    {
        ctx ??= new NullabilityInfoContext();
        var builder = new StringBuilder();
        if (p.IsOut) builder.Append("out ");
        else if (p.ParameterType.IsByRef) builder.Append(p.IsIn ? "in " : "ref ");
        else if (p.GetCustomAttributes().Any(a => a.GetType().Name == "ParamArrayAttribute") || p.IsDefined(typeof(ParamArrayAttribute), false)) builder.Append("params ");
        builder.Append(TypeName(p.ParameterType, ctx.Create(p))).Append(' ').Append(p.Name);
        if (p.HasDefaultValue) builder.Append(" = ").Append(DefaultText(p));
        return builder.ToString();
    }

    private static string DefaultText(ParameterInfo p) => p.DefaultValue switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        bool b => b ? "true" : "false",
        float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f",
        Enum e => e.GetType().Name + "." + e,
        var v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "default",
    };

    public static string Property(PropertyInfo property)
    {
        var ctx = new NullabilityInfoContext();
        var accessors = property.GetMethod is { } g && IsVisible(g) ? property.SetMethod is { } s && IsVisible(s) ? (IsInit(s) ? "{ get; init; }" : "{ get; set; }") : "{ get; }" : "{ set; }";
        var (method, _) = (property.GetMethod ?? property.SetMethod!, 0);
        var builder = new StringBuilder(Access(method)).Append(' ');
        if (method.IsStatic) builder.Append("static ");
        else if (method.IsAbstract) builder.Append("abstract ");
        var index = property.GetIndexParameters();
        builder.Append(TypeName(property.PropertyType, ctx.Create(property))).Append(' ');
        builder.Append(index.Length > 0 ? "this[" + string.Join(", ", index.Select(p => Parameter(p, ctx))) + "]" : property.Name);
        return builder.Append(' ').Append(accessors).ToString();
    }

    public static string Field(FieldInfo field)
    {
        var ctx = new NullabilityInfoContext();
        var builder = new StringBuilder(field.IsPublic ? "public " : "protected ");
        if (field.IsLiteral) builder.Append("const ");
        else
        {
            if (field.IsStatic) builder.Append("static ");
            if (field.IsInitOnly) builder.Append("readonly ");
        }
        builder.Append(TypeName(field.FieldType, ctx.Create(field))).Append(' ').Append(field.Name);
        if (field.IsLiteral && field.GetRawConstantValue() is { } value)
            builder.Append(" = ").Append(value is string s ? "\"" + s + "\"" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    public static string Event(EventInfo e)
    {
        var method = e.AddMethod!;
        return $"{Access(method)} {(method.IsStatic ? "static " : "")}event {TypeName(e.EventHandlerType!)} {e.Name}";
    }

    private static bool IsVisible(MethodBase m) => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;

    private static bool IsInit(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Any(t => t.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    private static string Access(MethodBase m) => m.IsPublic ? "public" : m.IsFamilyOrAssembly ? "protected internal" : "protected";
}
