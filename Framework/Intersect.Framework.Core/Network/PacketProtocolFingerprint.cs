using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace Intersect.Network;

/// <summary>Identifies the ordinal packet table and its reachable MessagePack contracts.</summary>
public static class PacketProtocolFingerprint
{
    public static byte[] Compute(IEnumerable<KeyValuePair<ushort, Type>> packetTypes)
    {
        // HailPacket's encrypted payload currently differs between Debug and Release builds.
#if DEBUG
        var contract = new StringBuilder("CR2026-wire-1-debug\n");
#else
        var contract = new StringBuilder("CR2026-wire-1-release\n");
#endif
        var visited = new HashSet<Type>();
        foreach (var (key, type) in packetTypes.OrderBy(pair => pair.Key))
        {
            contract.Append(key.ToString(CultureInfo.InvariantCulture)).Append('=').Append(Name(type)).Append('\n');
            Describe(type, visited, contract);
        }
        return SHA256.HashData(Encoding.UTF8.GetBytes(contract.ToString()));
    }

    private static string Name(Type type) => type.IsArray ? Name(type.GetElementType()!) + "[" + type.GetArrayRank().ToString(CultureInfo.InvariantCulture) + "]" :
        type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(Name)) + ">" :
        type.FullName ?? type.Name;

    private static void Describe(Type type, HashSet<Type> visited, StringBuilder contract)
    {
        if (!visited.Add(type)) return;
        if (type.IsArray) { Describe(type.GetElementType()!, visited, contract); return; }
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments()) Describe(argument, visited, contract);
        if (type.IsEnum)
        {
            contract.Append("enum:").Append(Name(type)).Append(':').Append(Name(Enum.GetUnderlyingType(type)));
            foreach (var name in Enum.GetNames(type).Order(StringComparer.Ordinal))
            {
                var number = (IFormattable)Convert.ChangeType(Enum.Parse(type, name), Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
                contract.Append('|').Append(name).Append('=').Append(number.ToString(null, CultureInfo.InvariantCulture));
            }
            contract.Append('\n');
            return;
        }

        var options = type.GetCustomAttribute<MessagePackObjectAttribute>();
        var unions = type.GetCustomAttributes<UnionAttribute>().OrderBy(union => union.Key).ToArray();
        if (options == null && unions.Length == 0) return;
        contract.Append("type:").Append(Name(type)).Append("; named=").Append(options?.KeyAsPropertyName == true).Append('\n');
        foreach (var union in unions)
        {
            contract.Append("union:").Append(union.Key.ToString(CultureInfo.InvariantCulture)).Append('=').Append(Name(union.SubType)).Append('\n');
            Describe(union.SubType, visited, contract);
        }

        var members = new List<(string Key, Type Type)>();
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var valueType = member is PropertyInfo property && property.GetIndexParameters().Length == 0 ? property.PropertyType :
                    member is FieldInfo field ? field.FieldType : null;
                if (valueType == null || member.IsDefined(typeof(IgnoreMemberAttribute))) continue;
                var key = member.GetCustomAttribute<KeyAttribute>();
                if (key != null) members.Add((key.IntKey.HasValue ? "i:" + key.IntKey.Value.ToString(CultureInfo.InvariantCulture) : "s:" + key.StringKey, valueType));
                else if (options?.KeyAsPropertyName == true && (member is PropertyInfo p && p.GetMethod?.IsPublic == true || member is FieldInfo f && f.IsPublic))
                    members.Add(("s:" + member.Name, valueType));
            }
        }
        foreach (var member in members.OrderBy(member => member.Key, StringComparer.Ordinal).ThenBy(member => Name(member.Type), StringComparer.Ordinal))
        {
            contract.Append("member:").Append(member.Key).Append('=').Append(Name(member.Type)).Append('\n');
            Describe(member.Type, visited, contract);
        }
    }
}
