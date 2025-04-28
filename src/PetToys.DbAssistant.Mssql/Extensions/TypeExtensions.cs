using System;
using System.Linq;

namespace PetToys.DbAssistant.Mssql.Extensions;

internal static class TypeExtensions
{
    private static readonly Type[] SupportedTypes =
    [
        typeof(bool),
        typeof(char),
        typeof(string),
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(DateTime),
        typeof(Guid),
        typeof(byte[]),
        typeof(char[]),
    ];

    public static bool IsSupportedType(this Type type) => SupportedTypes.Contains(type);
}
