using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace TSKHook;

public sealed class TranslationManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; }
    public string MinimumPluginVersion { get; set; } = "1.4.0";
    public string DownloadUrl { get; set; }
    public string Sha256 { get; set; }
    public long Size { get; set; }
    public int Entries { get; set; }

    public void Validate(string pluginVersion)
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Version) || Version.Length > 100 ||
            !RuntimeAsset.IsSha256(Sha256) || Size <= 0 || Size > TranslationPackageStore.MaxPackageBytes || Entries <= 0)
            throw new InvalidDataException("Invalid translation manifest.");
        if (!System.Version.TryParse(MinimumPluginVersion, out var minimum) ||
            !System.Version.TryParse(pluginVersion.Split('-')[0], out var current) || current < minimum)
            throw new InvalidDataException("This translation package requires a newer plugin.");
        if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Translation download URL must use HTTPS without credentials.");
    }
}

public sealed class PackageState
{
    public TranslationManifest Current { get; set; }
    public TranslationManifest Previous { get; set; }
}

/// <summary>Immutable package files and one atomic active-state pointer; failed updates never overwrite the current package.</summary>
public sealed class TranslationPackageStore
{
    public const long MaxPackageBytes = 128L * 1024 * 1024;
    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);
    public TranslationPackageStore(string directory) => this.directory = Path.GetFullPath(directory);
    private string StatePath => Path.Combine(directory, "active.json");
    public string PackagePath(TranslationManifest manifest)
    {
        if (!RuntimeAsset.IsSha256(manifest?.Sha256)) throw new InvalidDataException("Invalid package identity.");
        return Path.Combine(directory, manifest.Sha256 + ".jsonl");
    }

    public PackageState ReadState() => File.Exists(StatePath)
        ? JsonSerializer.Deserialize<PackageState>(File.ReadAllText(StatePath), RuntimeAsset.JsonOptions)
          ?? throw new InvalidDataException("Invalid package state.")
        : new PackageState();

    public async Task<bool> UpdateAsync(HttpClient client, string manifestUrl, string pluginVersion,
        CancellationToken token = default)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Manifest URL must use HTTPS without credentials.");
        await gate.WaitAsync(token);
        try
        {
            using var buffer = new MemoryStream();
            await DownloadAsync(client, uri, buffer, 64 * 1024, token);
            var manifest = JsonSerializer.Deserialize<TranslationManifest>(buffer.ToArray(), RuntimeAsset.JsonOptions)
                           ?? throw new InvalidDataException("Empty manifest.");
            manifest.Validate(pluginVersion);
            var state = ReadState();
            var destination = PackagePath(manifest);
            if (state.Current?.Sha256 == manifest.Sha256 && File.Exists(destination))
            {
                Verify(destination, manifest);
                return false;
            }
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".download");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await DownloadAsync(client, new Uri(manifest.DownloadUrl), file, manifest.Size, token);
                Verify(temporary, manifest);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, destination, true);
                WriteState(new PackageState { Current = manifest, Previous = state.Current });
                return true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { gate.Release(); }
    }

    public async Task RollbackAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            var state = ReadState();
            if (state.Previous == null) throw new InvalidOperationException("No previous translation package is available.");
            Verify(PackagePath(state.Previous), state.Previous);
            token.ThrowIfCancellationRequested();
            WriteState(new PackageState { Current = state.Previous, Previous = state.Current });
        }
        finally { gate.Release(); }
    }

    public static void Verify(string path, TranslationManifest manifest)
    {
        var info = new FileInfo(path);
        if (info.Length != manifest.Size) throw new InvalidDataException("Translation package size mismatch.");
        using (var file = File.OpenRead(path))
        using (var sha = SHA256.Create())
            if (Convert.ToHexString(sha.ComputeHash(file)).ToLowerInvariant() != manifest.Sha256)
                throw new InvalidDataException("Translation package SHA-256 mismatch.");
        if (RuntimeAsset.Load(path).ExactCount != manifest.Entries)
            throw new InvalidDataException("Translation package entry count mismatch.");
    }

    private void WriteState(PackageState state)
    {
        var temporary = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, RuntimeAsset.JsonOptions));
            File.Move(temporary, StatePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task DownloadAsync(HttpClient client, Uri uri, Stream target, long limit, CancellationToken token)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new InvalidDataException("Insecure redirect refused.");
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Download exceeds expected size.");
        using var source = await response.Content.ReadAsStreamAsync(token);
        var bytes = new byte[65536];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(bytes.AsMemory(), token)) > 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Download exceeds expected size.");
            await target.WriteAsync(bytes.AsMemory(0, count), token);
        }
    }
}
