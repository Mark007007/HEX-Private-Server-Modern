using System.Reflection;
using System.Runtime.Loader;

if (args.Length == 0)
{
    Console.Error.WriteLine(
        "Usage: LegacyLoadProbe <Assembly-CSharp-firstpass.dll> [type ...]");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[0]);

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 2;
}

var directory = Path.GetDirectoryName(assemblyPath)!;
var context = new LegacyLoadContext(directory);

try
{
    var assembly = context.LoadFromAssemblyPath(assemblyPath);

    Console.WriteLine($"Loaded: {assembly.FullName}");

    var targets = args.Length > 1
        ? args.Skip(1).ToArray()
        : new[]
        {
            "Game.Shared.Mechanics.Card",
            "Game.Shared.Session",
            "Game.Shared.AuthoritativeSessionBase",
            "Game.Shared.Mechanics.Transactions.Transaction",
            "Game.Shared.Network.DataWrapper",
            "Game.Shared.Network.HConnect.Session"
        };

    foreach (var target in targets)
    {
        var type = assembly.GetType(
            target,
            throwOnError: false,
            ignoreCase: false);

        Console.WriteLine(
            type is null
                ? $"MISS  {target}"
                : $"FOUND {type.FullName}");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    return 1;
}
finally
{
    context.Unload();
}

sealed class LegacyLoadContext : AssemblyLoadContext
{
    private readonly string _directory;

    public LegacyLoadContext(string directory)
        : base(isCollectible: true)
    {
        _directory = directory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
            return null;

        var candidate = Path.Combine(
            _directory,
            assemblyName.Name + ".dll");

        if (!File.Exists(candidate))
            return null;

        try
        {
            return LoadFromAssemblyPath(candidate);
        }
        catch
        {
            return null;
        }
    }
}
