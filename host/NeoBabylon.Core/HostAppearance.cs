using System.IO;

namespace NeoBabylon.Core;

public enum HostAppearance
{
    Dark,
    Light
}

public static class HostAppearanceParser
{
    public static HostAppearance Parse(string? value) => value switch
    {
        "dark" => HostAppearance.Dark,
        "light" => HostAppearance.Light,
        _ => throw new InvalidDataException("Host appearance must be exactly dark or light.")
    };
}
