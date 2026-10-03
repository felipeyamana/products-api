using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Length is < 1 or > 2 || !Guid.TryParse(args[0], out var subject))
{
    Console.Error.WriteLine(
        "Usage: dotnet run scripts/New-ECommerceDevelopmentToken.cs -- <customer-guid> [ecommerce-api-project]");
    return 1;
}

var ecommerceProject = args.Length == 2
    ? Path.GetFullPath(args[1])
    : Path.GetFullPath(Path.Combine(
        Environment.CurrentDirectory,
        "..",
        "ECommerce",
        "server",
        "ECommerce.Api"));

if (!Directory.Exists(ecommerceProject))
{
    Console.Error.WriteLine($"ECommerce API project not found: {ecommerceProject}");
    return 1;
}

var configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
LoadJsonConfiguration(Path.Combine(ecommerceProject, "appsettings.json"), configuration);
LoadJsonConfiguration(Path.Combine(ecommerceProject, "appsettings.Development.json"), configuration);
LoadUserSecrets(ecommerceProject, configuration);

var issuer = Required("DownstreamTokens:Issuer");
var audience = Required("DownstreamTokens:Audience");
var keyId = Required("DownstreamTokens:KeyId");
var privateKey = Convert.FromBase64String(Required("DownstreamTokens:PrivateKey"));
var lifetimeMinutes = configuration.TryGetValue("DownstreamTokens:LifetimeMinutes", out var lifetimeValue) &&
    int.TryParse(lifetimeValue, out var configuredLifetime)
        ? configuredLifetime
        : 5;

if (lifetimeMinutes is < 1 or > 10)
{
    throw new InvalidOperationException("DownstreamTokens:LifetimeMinutes must be from 1 through 10.");
}

var now = DateTimeOffset.UtcNow;
var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
{
    alg = "RS256",
    typ = "JWT",
    kid = keyId
}));
var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
{
    iss = issuer,
    aud = audience,
    sub = subject.ToString(),
    jti = Guid.NewGuid().ToString("N"),
    iat = now.ToUnixTimeSeconds(),
    nbf = now.ToUnixTimeSeconds(),
    exp = now.AddMinutes(lifetimeMinutes).ToUnixTimeSeconds(),
    azp = "ecommerce-api-manual-test",
    scope = "cart:read cart:write addresses:read addresses:write orders:read orders:write",
    role = new[] { "CartUser", "CustomerUser", "OrderUser" }
}));
var unsignedToken = $"{header}.{payload}";

using var rsa = RSA.Create();
rsa.ImportPkcs8PrivateKey(privateKey, out var bytesRead);
if (bytesRead != privateKey.Length || rsa.KeySize < 2048)
{
    throw new CryptographicException("The configured private key is invalid.");
}

var signature = rsa.SignData(
    Encoding.ASCII.GetBytes(unsignedToken),
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);

Console.WriteLine($"{unsignedToken}.{Base64Url(signature)}");
return 0;

string Required(string key) =>
    configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException(
            $"{key} is missing from the ECommerce API configuration or user secrets.");

static string Base64Url(byte[] value) => Convert.ToBase64String(value)
    .TrimEnd('=')
    .Replace('+', '-')
    .Replace('/', '_');

static void LoadJsonConfiguration(
    string path,
    IDictionary<string, string> configuration)
{
    if (!File.Exists(path))
    {
        return;
    }

    using var document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("DownstreamTokens", out var section))
    {
        return;
    }

    foreach (var property in section.EnumerateObject())
    {
        configuration[$"DownstreamTokens:{property.Name}"] = property.Value.ToString();
    }
}

static void LoadUserSecrets(
    string projectPath,
    IDictionary<string, string> configuration)
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"user-secrets list --project \"{projectPath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }) ?? throw new InvalidOperationException("Could not start dotnet user-secrets.");

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"Could not read ECommerce user secrets: {error.Trim()}");
    }

    foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
    {
        var separator = line.IndexOf(" = ", StringComparison.Ordinal);
        if (separator <= 0)
        {
            continue;
        }

        var key = line[..separator];
        if (key.StartsWith("DownstreamTokens:", StringComparison.OrdinalIgnoreCase))
        {
            configuration[key] = line[(separator + 3)..];
        }
    }
}
