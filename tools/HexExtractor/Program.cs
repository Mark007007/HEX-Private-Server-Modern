using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: HexExtractor <assembly.dll> [output.json] [namespace-prefix]");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[0]);

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 2;
}

var outputPath = args.Length >= 2
    ? Path.GetFullPath(args[1])
    : Path.Combine(
        Path.GetDirectoryName(assemblyPath)!,
        Path.GetFileNameWithoutExtension(assemblyPath) + ".catalog.json");

var namespacePrefix = args.Length >= 3 ? args[2] : string.Empty;

using var stream = File.OpenRead(assemblyPath);
using var peReader = new PEReader(stream);

if (!peReader.HasMetadata)
{
    Console.Error.WriteLine("Input is not a CLI/.NET assembly.");
    return 2;
}

var metadata = peReader.GetMetadataReader();

var assemblyName = "<module>";
if (metadata.IsAssembly)
{
    assemblyName = metadata.GetString(
        metadata.GetAssemblyDefinition().Name);
}

var types = new List<TypeCatalogEntry>();

foreach (var typeHandle in metadata.TypeDefinitions)
{
    var type = metadata.GetTypeDefinition(typeHandle);
    var ns = metadata.GetString(type.Namespace);
    var name = metadata.GetString(type.Name);

    if (namespacePrefix.Length != 0 &&
        !ns.StartsWith(namespacePrefix, StringComparison.Ordinal))
    {
        continue;
    }

    var fields = type.GetFields()
        .Select(fieldHandle =>
            metadata.GetString(metadata.GetFieldDefinition(fieldHandle).Name))
        .ToArray();

    var methods = type.GetMethods()
        .Select(methodHandle =>
            metadata.GetString(metadata.GetMethodDefinition(methodHandle).Name))
        .ToArray();

    types.Add(new TypeCatalogEntry(
        string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}",
        ns,
        name,
        fields,
        methods));
}

var references = metadata.AssemblyReferences
    .Select(handle =>
        metadata.GetString(
            metadata.GetAssemblyReference(handle).Name))
    .Distinct(StringComparer.Ordinal)
    .OrderBy(static x => x, StringComparer.Ordinal)
    .ToArray();

var catalog = new AssemblyCatalog
{
    AssemblyName = assemblyName,
    AssemblyPath = assemblyPath,
    NamespacePrefix = namespacePrefix,
    Types = types.OrderBy(static x => x.FullName, StringComparer.Ordinal).ToArray(),
    AssemblyReferences = references
};

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

await File.WriteAllTextAsync(
    outputPath,
    JsonSerializer.Serialize(catalog, options));

Console.WriteLine($"Assembly : {catalog.AssemblyName}");
Console.WriteLine($"Types    : {catalog.Types.Length}");
Console.WriteLine($"Fields   : {catalog.Types.Sum(static x => x.Fields.Length)}");
Console.WriteLine($"Methods  : {catalog.Types.Sum(static x => x.Methods.Length)}");
Console.WriteLine($"Refs     : {catalog.AssemblyReferences.Length}");
Console.WriteLine($"Catalog  : {outputPath}");

public sealed class AssemblyCatalog
{
    public required string AssemblyName { get; init; }
    public required string AssemblyPath { get; init; }
    public required string NamespacePrefix { get; init; }
    public required TypeCatalogEntry[] Types { get; init; }
    public required string[] AssemblyReferences { get; init; }
}

public sealed record TypeCatalogEntry(
    string FullName,
    string Namespace,
    string Name,
    string[] Fields,
    string[] Methods);
