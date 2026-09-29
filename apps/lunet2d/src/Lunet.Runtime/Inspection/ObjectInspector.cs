using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Lunet.Runtime.Inspection;

/// <summary>
/// Monta as linhas do Inspector para um objeto: usa um <see cref="Inspector{T}"/> customizado se o projeto tiver um para o tipo,
/// senão lista por reflexão os campos e propriedades públicos (mais os marcados com <see cref="InspectAttribute"/>),
/// respeitando os atributos <c>Range</c>, <c>ReadOnly</c>, <c>Hidden</c>, <c>Multiline</c>, <c>Color</c>, <c>File</c>, <c>Asset</c>, <c>Group</c> e <c>Tooltip</c>.
/// </summary>
public static class ObjectInspector
{
    private const int MaxDepth = 3;
    private const int MaxListItems = 32;

    /// <param name="target">Objeto a inspecionar (por exemplo, a instância do <c>Game</c> em execução).</param>
    /// <param name="customInspectors">Inspectors customizados disponíveis (veja <see cref="FindCustomInspectors"/>).</param>
    public static IReadOnlyList<InspectorItem> Build(object target, IEnumerable<IInspector>? customInspectors = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        var context = new InspectorContext();
        var customs = customInspectors?.ToList() ?? [];
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        context.Header(target.GetType().Name);
        AddObject(context, target, customs, 0, visited, getRoot: () => target, setRoot: null, trustedAssembly: target.GetType().Assembly);
        return context.Items;
    }

    /// <summary>Cria os inspectors customizados (classes que herdam de <see cref="Inspector{T}"/>) do assembly informado.</summary>
    public static IReadOnlyList<IInspector> FindCustomInspectors(Assembly assembly)
    {
        var result = new List<IInspector>();
        Type[] types;
        try { types = assembly.GetExportedTypes(); }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or NotSupportedException) { return result; }
        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(IInspector).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null) continue;
            result.Add((IInspector)Activator.CreateInstance(type)!);
        }
        return result;
    }

    private static void AddObject(InspectorContext ui, object instance, List<IInspector> customs, int depth, HashSet<object> visited,
        Func<object?> getRoot, Action<object?>? setRoot, Assembly trustedAssembly)
    {
        var type = instance.GetType();
        if (!type.IsValueType && !visited.Add(instance)) return;

        var custom = customs.FirstOrDefault(c => c.TargetType.IsInstanceOfType(instance));
        if (custom is not null)
        {
            void Run(int levels)
            {
                if (levels == 0) custom.Inspect(ui, instance);
                else ui.Indent(() => Run(levels - 1));
            }
            Run(depth);
            return;
        }

        string? currentGroup = null;
        foreach (var member in InspectableMembers(type, trustedAssembly))
        {
            var group = member.GetCustomAttribute<GroupAttribute>()?.Name;
            if (group != currentGroup && group is not null)
            {
                ui.Add(new InspectorItem { Kind = InspectorItemKind.Header, Label = group, Depth = depth });
            }
            currentGroup = group;
            AddMember(ui, member, instance, customs, depth, visited, getRoot, setRoot, trustedAssembly);
        }
    }

    private static IEnumerable<MemberInfo> InspectableMembers(Type type, Assembly trustedAssembly)
    {
        // Só os membros declarados no código do jogo: a base do framework (Game.Services etc.) não é assunto do jogador.
        var chain = new List<Type>();
        for (var t = type; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            if (t.Assembly == trustedAssembly) chain.Add(t);
        chain.Reverse();
        foreach (var t in chain)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var member in t.GetMembers(flags))
            {
                if (member is not (FieldInfo or PropertyInfo)) continue;
                if (member.Name.Contains('<') || member.GetCustomAttribute<CompilerGeneratedAttribute>() is not null) continue;
                if (member.GetCustomAttribute<HiddenAttribute>() is not null) continue;
                var explicitly = member.GetCustomAttribute<InspectAttribute>() is not null;
                switch (member)
                {
                    case FieldInfo field when field.IsPublic || explicitly:
                        yield return field;
                        break;
                    case PropertyInfo { CanRead: true } property when property.GetIndexParameters().Length == 0 &&
                        (explicitly || property.GetMethod is { IsPublic: true }):
                        yield return property;
                        break;
                }
            }
        }
    }

    private static void AddMember(InspectorContext ui, MemberInfo member, object owner, List<IInspector> customs, int depth,
        HashSet<object> visited, Func<object?> getOwner, Action<object?>? setOwner, Assembly trustedAssembly)
    {
        var type = member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;
        var label = member.Name;
        var tooltip = member.GetCustomAttribute<TooltipAttribute>()?.Text;
        var readOnly = member.GetCustomAttribute<ReadOnlyAttribute>() is not null ||
                       member is FieldInfo { IsInitOnly: true } ||
                       member is PropertyInfo { CanWrite: false } ||
                       member is PropertyInfo { SetMethod.IsPublic: false } && member.GetCustomAttribute<InspectAttribute>() is null;

        object? Get() => member is FieldInfo fi ? fi.GetValue(getOwner()) : ((PropertyInfo)member).GetValue(getOwner());
        void Set(object? value)
        {
            var target = getOwner();
            if (target is null) return;
            var coerced = InspectorValue.Coerce(type, value);
            if (member is FieldInfo fi) fi.SetValue(target, coerced);
            else ((PropertyInfo)member).SetValue(target, coerced);
            // Se o dono é uma struct (cópia encaixotada), grava de volta no pai.
            if (target.GetType().IsValueType) setOwner?.Invoke(target);
        }

        var kind = InspectorContext.InspectorFieldKindOf(type);
        var range = member.GetCustomAttribute<RangeAttribute>();
        var multiline = member.GetCustomAttribute<MultilineAttribute>();
        var isColorText = type == typeof(string) && member.GetCustomAttribute<ColorAttribute>() is not null;
        var file = member.GetCustomAttribute<FileAttribute>();
        var asset = member.GetCustomAttribute<AssetAttribute>();

        if (kind != InspectorFieldKind.Other)
        {
            Action<object?>? setter = readOnly ? null : range is null ? Set : v =>
            {
                var number = Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture);
                Set(Math.Clamp(number, range.Min, range.Max));
            };
            ui.Add(new InspectorItem
            {
                Kind = InspectorItemKind.Field,
                Label = label,
                FieldKind = isColorText ? InspectorFieldKind.Color : kind,
                ValueType = type,
                Getter = Get,
                Setter = setter,
                Min = range?.Min,
                Max = range?.Max,
                MultilineLines = multiline?.Lines ?? 0,
                Tooltip = tooltip,
                FileFilter = file?.Filter,
                Asset = asset?.Kind,
                EnumNames = type.IsEnum ? Enum.GetNames(type) : null,
                Depth = depth,
            });
            return;
        }

        var value = SafeGet(Get);
        if (value is null)
        {
            ui.Add(new InspectorItem { Kind = InspectorItemKind.Label, Label = $"{label}: (nulo)", Depth = depth });
            return;
        }

        if (value is IList list && value is not string)
        {
            AddList(ui, label, list, type, depth, readOnly);
            return;
        }

        var nestedTrusted = value.GetType().Assembly == trustedAssembly;
        if (nestedTrusted && depth < MaxDepth)
        {
            ui.Add(new InspectorItem { Kind = InspectorItemKind.Header, Label = label, Depth = depth, Tooltip = tooltip });
            AddObject(ui, value, customs, depth + 1, visited, Get, value.GetType().IsValueType ? Set : null, trustedAssembly);
            return;
        }

        ui.Add(new InspectorItem { Kind = InspectorItemKind.Label, Label = $"{label}: {value.GetType().Name}", Depth = depth, Tooltip = tooltip });
    }

    private static void AddList(InspectorContext ui, string label, IList list, Type listType, int depth, bool readOnly)
    {
        ui.Add(new InspectorItem { Kind = InspectorItemKind.Header, Label = $"{label} [{list.Count}]", Depth = depth });
        var elementType = listType.IsArray ? listType.GetElementType()! : listType.IsGenericType ? listType.GetGenericArguments()[0] : typeof(object);
        var fieldKind = InspectorContext.InspectorFieldKindOf(elementType);
        var shown = Math.Min(list.Count, MaxListItems);
        for (var i = 0; i < shown; i++)
        {
            var index = i;
            if (fieldKind == InspectorFieldKind.Other)
            {
                ui.Add(new InspectorItem { Kind = InspectorItemKind.Label, Label = $"[{index}] {SafeGet(() => list[index])?.GetType().Name ?? "(nulo)"}", Depth = depth + 1 });
                continue;
            }
            ui.Add(new InspectorItem
            {
                Kind = InspectorItemKind.Field,
                Label = $"[{index}]",
                FieldKind = fieldKind,
                ValueType = elementType,
                Getter = () => index < list.Count ? list[index] : null,
                Setter = readOnly ? null : v =>
                {
                    if (index < list.Count) list[index] = InspectorValue.Coerce(elementType, v);
                },
                EnumNames = elementType.IsEnum ? Enum.GetNames(elementType) : null,
                Depth = depth + 1,
            });
        }
        if (list.Count > shown)
            ui.Add(new InspectorItem { Kind = InspectorItemKind.Label, Label = $"… mais {list.Count - shown} itens", Depth = depth + 1 });
    }

    private static object? SafeGet(Func<object?> getter)
    {
        try { return getter(); }
        catch (Exception ex) when (ex is TargetInvocationException or InvalidOperationException or ArgumentOutOfRangeException) { return null; }
    }
}
