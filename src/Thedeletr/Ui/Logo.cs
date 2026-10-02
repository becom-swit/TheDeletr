using Spectre.Console;
using System.Reflection;

namespace Thedeletr.Ui;

public static class Logo
{
    public const string AppName = "TheDeletr";

    // Width of the "The" part, used to color "The" and "Deletr" differently.
    private const int _unicodeSplit = 25;
    private const int _asciiSplit = 15;

    private static readonly string[] _unicodeLines =
    [
        "████████╗██╗  ██╗███████╗██████╗ ███████╗██╗     ███████╗████████╗██████╗ ",
        "╚══██╔══╝██║  ██║██╔════╝██╔══██╗██╔════╝██║     ██╔════╝╚══██╔══╝██╔══██╗",
        "   ██║   ███████║█████╗  ██║  ██║█████╗  ██║     █████╗     ██║   ██████╔╝",
        "   ██║   ██╔══██║██╔══╝  ██║  ██║██╔══╝  ██║     ██╔══╝     ██║   ██╔══██╗",
        "   ██║   ██║  ██║███████╗██████╔╝███████╗███████╗███████╗   ██║   ██║  ██║",
        "   ╚═╝   ╚═╝  ╚═╝╚══════╝╚═════╝ ╚══════╝╚══════╝╚══════╝   ╚═╝   ╚═╝  ╚═╝",
    ];

    private static readonly string[] _asciiLines =
    [
        "  ________         ____       __     __      ",
        " /_  __/ /_  ___  / __ \\___  / /__  / /______",
        "  / / / __ \\/ _ \\/ / / / _ \\/ / _ \\/ __/ ___/",
        " / / / / / /  __/ /_/ /  __/ /  __/ /_/ /    ",
        "/_/ /_/ /_/\\___/_____/\\___/_/\\___/\\__/_/     ",
    ];

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static void Write(IAnsiConsole console)
    {
        var unicode = console.Profile.Capabilities.Unicode;
        var lines = unicode ? _unicodeLines : _asciiLines;
        var split = unicode ? _unicodeSplit : _asciiSplit;

        console.WriteLine();
        foreach (var line in lines)
        {
            console.MarkupLine($"[grey70]{Markup.Escape(line[..split])}[/][red]{Markup.Escape(line[split..])}[/]");
        }

        console.MarkupLine($"[grey]  delete files older than a given date  ·  v{Version}[/]");
        console.WriteLine();
    }
}
