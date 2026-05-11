using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using ProductCatalogImport;
using ProductCatalogImport.Csv;
using ProductCatalogImport.Sql;

// Resolve paths from tool project folder (not bin output), so relative paths in appsettings work.
var toolProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
Directory.SetCurrentDirectory(toolProjectDir);

var configuration = new ConfigurationBuilder()
    .SetBasePath(toolProjectDir)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var importOptions = new ImportOptions();
configuration.GetSection(ImportOptions.SectionName).Bind(importOptions);

ApplyImportArgs(args, importOptions);

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString)
    && !string.IsNullOrWhiteSpace(importOptions.ProductsApiSettingsDirectory))
{
    var apiDir = Path.GetFullPath(Path.Combine(toolProjectDir, importOptions.ProductsApiSettingsDirectory));
    var apiConfig = new ConfigurationBuilder()
        .SetBasePath(apiDir)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    connectionString = apiConfig.GetConnectionString("DefaultConnection");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Missing connection string. Set ConnectionStrings:DefaultConnection in appsettings.Development.json " +
        "for this tool, or in src/ProductsApi (see Import:ProductsApiSettingsDirectory).");
    return 1;
}

var csvPath = Path.GetFullPath(Path.Combine(toolProjectDir, importOptions.CsvPath));
if (!File.Exists(csvPath))
{
    Console.Error.WriteLine($"CSV not found: {csvPath}");
    return 1;
}

Console.WriteLine(
    $"Reading up to {importOptions.MaxRows} data rows (skipping first {importOptions.SkipRows}) from: {csvPath}");
Console.WriteLine(
    $"Root category: '{importOptions.RootCategoryName}', USD via INR÷{importOptions.InrPerUsd} (configure Import:InrPerUsd).");

var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    MissingFieldFound = null,
    BadDataFound = null,
};

using var reader = new StreamReader(csvPath);
using var csv = new CsvReader(reader, csvConfig);
csv.Context.RegisterClassMap<ElectronicsRowMap>();

var batch = csv.GetRecords<ElectronicsRow>()
    .Skip(importOptions.SkipRows)
    .Take(importOptions.MaxRows)
    .ToList();
Console.WriteLine($"Loaded {batch.Count} rows from CSV.");

var importer = new CatalogImporter(connectionString, importOptions);
await importer.ImportAsync(batch);

return 0;

static void ApplyImportArgs(string[] args, ImportOptions o)
{
    // Flags: --skip 500  |  --skip=500  |  --take 500  |  --take=500
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.Equals("--skip", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
            && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip))
        {
            o.SkipRows = skip;
            i++;
            continue;
        }

        if (a.StartsWith("--skip=", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(a.AsSpan(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out var skipEq))
        {
            o.SkipRows = skipEq;
            continue;
        }

        if (a.Equals("--take", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
            && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var take))
        {
            o.MaxRows = take;
            i++;
            continue;
        }

        if (a.StartsWith("--take=", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(a.AsSpan(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out var takeEq))
        {
            o.MaxRows = takeEq;
            continue;
        }
    }

    // Positional: [maxRows] [csvPath] — omit tokens consumed by flags above.
    var positionals = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.StartsWith("--", StringComparison.Ordinal))
        {
            if (a.Equals("--skip", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--take", StringComparison.OrdinalIgnoreCase))
                i++;

            continue;
        }

        positionals.Add(a);
    }
    if (positionals.Count > 0
        && int.TryParse(positionals[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxFromArg))
        o.MaxRows = maxFromArg;

    if (positionals.Count > 1 && !string.IsNullOrWhiteSpace(positionals[1]))
        o.CsvPath = positionals[1];
}
