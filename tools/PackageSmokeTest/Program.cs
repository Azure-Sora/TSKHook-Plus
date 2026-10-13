using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TSKHook;

var root = Path.Combine(Path.GetTempPath(), "tsk-packages-" + Guid.NewGuid().ToString("N"));
var handler = new FixtureHandler();
using var client = new HttpClient(handler);
var store = new TranslationPackageStore(root);
var tests = 0;
try
{
    Configure("1", "译文一");
    Check(await store.UpdateAsync(client, "https://fixture/manifest", "1.4.0"), "first install");
    Check(store.ReadState().Current.Version == "1", "first version pointer");
    Check(!await store.UpdateAsync(client, "https://fixture/manifest", "1.4.0"), "same package no-op");
    Configure("2", "译文二");
    await store.UpdateAsync(client, "https://fixture/manifest", "1.4.0");
    Check(store.ReadState().Current.Version == "2" && store.ReadState().Previous.Version == "1", "update retains previous");
    await store.RollbackAsync();
    Check(store.ReadState().Current.Version == "1", "rollback");
    Configure("3", "译文三");
    handler.Asset = Encoding.UTF8.GetBytes("broken");
    await Reject("wrong content hash/length rejected");
    Check(store.ReadState().Current.Version == "1", "failed download preserves active state");
    handler.Fail = true;
    await Reject("offline response rejected"); handler.Fail = false;
    Check(store.ReadState().Current.Version == "1", "offline leaves existing package available");
    Configure("4", "译文四"); handler.Manifest.MinimumPluginVersion = "999.0.0";
    await Reject("incompatible minimum plugin rejected");
    Configure("5", "译文五");
    handler.Asset = handler.Asset.Concat(handler.Asset).ToArray();
    handler.Manifest.Size = handler.Asset.Length;
    handler.Manifest.Sha256 = Convert.ToHexString(SHA256.HashData(handler.Asset)).ToLowerInvariant();
    handler.Manifest.Entries = 2;
    await Reject("valid hash with duplicate key rejected");
    Check(store.ReadState().Current.Version == "1", "invalid parsed package preserves state");
    var text = "挑戦回数";
    var index = new UiTranslationIndex(new Dictionary<string, UiRuntimeTranslation>
    { ["unrelated"] = new(UiJsonTranslator.Sha256(text), "挑战次数") });
    var json = """{"result":{"battle_result_data":{"retry_data":{"cost_name":"挑戦回数"}}}}""";
    Check(!UiJsonTranslator.TryTranslate("BattleEnd", json, index, out var result, out _, out _) && result == json,
        "identifier is protected even against hash fallback");
    Console.WriteLine($"PASS: {tests} update/rollback/protection assertions");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
void Check(bool valid, string message) { tests++; if (!valid) throw new Exception(message); }
async Task Reject(string message)
{
    var rejected = false;
    try { await store.UpdateAsync(client, "https://fixture/manifest", "1.4.0"); }
    catch (Exception e) when (e is InvalidDataException or HttpRequestException) { rejected = true; }
    Check(rejected, message);
}
void Configure(string version, string translation)
{
    handler.Asset = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new RuntimeRow
    { Key = "item:id=1:name", SourceHash = UiJsonTranslator.Sha256("原文"), Translation = translation }, RuntimeAsset.JsonOptions) + "\n");
    handler.Manifest = new TranslationManifest { Version = version, DownloadUrl = "https://fixture/asset", Size = handler.Asset.Length,
        Sha256 = Convert.ToHexString(SHA256.HashData(handler.Asset)).ToLowerInvariant(), Entries = 1 };
}
sealed class FixtureHandler : HttpMessageHandler
{
    public byte[] Asset;
    public TranslationManifest Manifest;
    public bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        { RequestMessage = request, Content = new ByteArrayContent(request.RequestUri.AbsolutePath == "/manifest"
            ? JsonSerializer.SerializeToUtf8Bytes(Manifest, RuntimeAsset.JsonOptions) : Asset) });
}
