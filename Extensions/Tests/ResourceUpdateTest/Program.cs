using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TSKHook;

var count = 0;
void Check(bool valid, string message) { count++; if (!valid) throw new Exception(message); }
async Task Reject(Func<Task> action, string message)
{
    try { await action(); } catch (Exception e) when (e is InvalidDataException or HttpRequestException or OperationCanceledException) { count++; return; }
    throw new Exception(message);
}
Check(ChineseTextConverter.Convert("體力與禮物、龍騎士", ChineseScript.Simplified) == "体力与礼物、龙骑士", "traditional to simplified");
Check(ChineseTextConverter.Convert("体力与礼物、龙骑士", ChineseScript.Traditional) == "體力與禮物、龍騎士", "simplified to traditional");
Check(ChineseTextConverter.Convert("髮型與發展", ChineseScript.Simplified) == "发型与发展", "single-character ambiguity uses fixed mapping");
Check(ChineseTextConverter.Convert("<sprite name=龍>體力 {禮物} １２3\n★", ChineseScript.Simplified) == "<sprite name=龍>体力 {禮物} １２3\n★", "markup placeholders numbers preserved");
var unchanged = "漢字 그대로 𠮷";
Check(ReferenceEquals(ChineseTextConverter.Convert(unchanged, ChineseScript.Original), unchanged), "default no conversion allocation");
Check(ReferenceEquals(ChineseTextConverter.Convert("ABC 123 𠮷", ChineseScript.Simplified), "ABC 123 𠮷"), "non-Chinese identity and supplementary unchanged");
Check(ChineseTextConverter.Convert(null, ChineseScript.Traditional) == null, "null handling");
var source = "報酬";
var index = new UiTranslationIndex(new Dictionary<string, UiRuntimeTranslation> { ["reward"] = new(UiJsonTranslator.Sha256(source), "禮物") });
Check(UiJsonTranslator.TryTranslate("Test", "{\"name\":\"報酬\",\"unknown\":\"龍\",\"id\":\"體力\"}", index,
    out var json, out _, out _, ChineseScript.Simplified), "converted selected JSON translation");
using (var doc = JsonDocument.Parse(json))
    Check(doc.RootElement.GetProperty("name").GetString() == "礼物" && doc.RootElement.GetProperty("unknown").GetString() == "龍" &&
        doc.RootElement.GetProperty("id").GetString() == "體力", "only selected translations converted");
Check(!UiJsonTranslator.TryTranslate("BattleEnd", "{\"retry_data\":{\"cost_name\":\"報酬\"}}", index, out _, out _, out _, ChineseScript.Simplified), "protected cost identifier survives conversion");

if (args.Length != 1) throw new ArgumentException("Pass the real image asset directory.");
var assets = Path.GetFullPath(args[0]);
var root = Path.Combine(Path.GetTempPath(), "tsk-resource-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var transport = new ResourceHandler();
using var client = new HttpClient(transport);
var store = new ImagePackageStore(Path.Combine(root, "images"));
var progress = new List<ResourceUpdateProgress>();
try
{
    await store.SeedLocalAsync(assets);
    var local = store.ReadState().Current;
    Configure("fixture-1");
    Check(await store.UpdateAsync(client, "https://fixture/images", "1.6.0", progress: progress.Add), "image install");
    Check(store.ReadState().Previous.Version == local.Version, "initial local image package retained for rollback");
    Check(progress.First().Stage == "Checking" && progress.Any(p => p.Stage == "Downloading" && p.Bytes == p.Total) &&
        progress.Any(p => p.Stage == "Verifying") && progress.Last().Stage == "Installing", "actual download and install progress");
    progress.Clear();
    Check(!await store.UpdateAsync(client, "https://fixture/images", "1.6.0", progress: progress.Add) && progress.Count == 1, "same image package verifies cache without download");
    Configure("fixture-2");
    await store.UpdateAsync(client, "https://fixture/images", "1.6.0");
    var current = store.ReadState().Current;
    var image = UiImageManifest.Read(store.PackagePath(current)).Sprites[0].File;
    File.WriteAllText(Path.Combine(store.PackagePath(current), image), "broken");
    Check(await store.UpdateAsync(client, "https://fixture/images", "1.6.0"), "corrupt image cache repaired");
    Check(store.ReadState().Previous.Version == "fixture-1", "cache repair keeps previous image version");
    Configure("fixture-3");
    transport.Bytes[^1] ^= 1;
    await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0"), "bad archive hash rejected");
    Check(store.ReadState().Current.Version == "fixture-2", "bad download preserves active images");
    Configure("fixture-3"); transport.Fail = true;
    await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0"), "offline image check rejected"); transport.Fail = false;
    Check(store.ReadState().Current.Version == "fixture-2", "offline preserves active images");
    transport.Manifest.MinimumPluginVersion = "99.0.0";
    await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0"), "minimum plugin version enforced");
    Configure("fixture-3"); transport.Manifest.ResourceType = "ui-text";
    await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0"), "wrong resource channel rejected");
    Configure("fixture-3");
    using (var interrupted = new CancellationTokenSource())
    {
        await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0", interrupted.Token,
            p => { if (p.Stage == "Downloading" && p.Bytes > 0) interrupted.Cancel(); }), "cancel during download preserves state");
    }
    Check(store.ReadState().Current.Version == "fixture-2" && !Directory.GetFiles(Path.Combine(root, "images"), "*.download").Any(),
        "interrupted download does not activate a package and cleans staging");
    Configure("fixture-3");
    using (var cancel = new CancellationTokenSource())
    {
        cancel.Cancel();
        await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0", cancel.Token), "cancel preserves state");
    }
    await store.RollbackAsync();
    Check(store.ReadState().Current.Version == "fixture-1", "independent image rollback");
    await store.RollbackAsync();
    Check(store.ReadState().Current.Version == "fixture-2", "image rollback reversible");
    var zip = Path.Combine(root, "malicious.zip");
    foreach (var names in new[] { new[] { "../escape.png" }, new[] { "..\\escape.png" }, new[] { "x.dll" }, new[] { "foo.png", "FOO.png" }, new[] { "x.png:ads" } })
    {
        if (File.Exists(zip)) File.Delete(zip);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var name in names) { using var output = archive.CreateEntry(name).Open(); output.WriteByte(1); }
        var target = Path.Combine(root, Guid.NewGuid().ToString("N"));
        await Reject(() => { ImagePackageStore.Extract(zip, target); return Task.CompletedTask; }, "unsafe ZIP entry rejected");
    }
    Check(!File.Exists(Path.Combine(root, "escape.png")), "ZIP traversal writes nothing outside staging");
    // Real manifest corruption must not replace valid active images.
    Configure("fixture-3");
    transport.Bytes = ZipBytes("fixture-other"); SetIdentity();
    await Reject(() => store.UpdateAsync(client, "https://fixture/images", "1.6.0"), "inner/outer version mismatch rejected");
    Check(store.ReadState().Current.Version == "fixture-2", "structural failure preserves active image state");
    Console.WriteLine($"PASS: {count} Chinese conversion / image update assertions");
}
finally { Directory.Delete(root, true); }

byte[] ZipBytes(string version)
{
    var manifest = UiImageManifest.Read(assets); manifest.Version = version;
    using var buffer = new MemoryStream();
    using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
    {
        using (var output = zip.CreateEntry("manifest.json").Open()) output.Write(JsonSerializer.SerializeToUtf8Bytes(manifest));
        foreach (var file in Directory.GetFiles(assets, "*.png"))
        { using var output = zip.CreateEntry(Path.GetFileName(file)).Open(); output.Write(File.ReadAllBytes(file)); }
    }
    return buffer.ToArray();
}
void SetIdentity()
{
    transport.Manifest.Size = transport.Bytes.Length;
    transport.Manifest.Sha256 = Convert.ToHexString(SHA256.HashData(transport.Bytes)).ToLowerInvariant();
}
void Configure(string version)
{
    transport.Bytes = ZipBytes(version);
    transport.Manifest = new() { Version = version, DownloadUrl = "https://fixture/archive" }; SetIdentity();
}
sealed class ResourceHandler : HttpMessageHandler
{
    public byte[] Bytes;
    public ImagePackageManifest Manifest;
    public bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        { RequestMessage = request, Content = new ByteArrayContent(request.RequestUri.AbsolutePath == "/images"
            ? JsonSerializer.SerializeToUtf8Bytes(Manifest, RuntimeAsset.JsonOptions) : Bytes) });
}
