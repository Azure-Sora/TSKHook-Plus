using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using TSKHook;

var checks = 0;
void Check(bool value,string message) { checks++; if (!value) throw new Exception(message); }
UiTranslationIndex Index(string value) => new(new Dictionary<string,UiRuntimeTranslation>
{
    ["menu"] = new(UiJsonTranslator.Sha256("メニュー"),value,true),
    ["template"] = new(UiJsonTranslator.Sha256("残り{0}回"),"剩余{0}次",true,"残り{0}回"),
    ["protected"] = new(UiJsonTranslator.Sha256("挑戦回数"),"挑战次数",true)
});
var index = Index("菜单");
var cache = new RenderedTextCache(index,8);
Check(cache.TryTranslate("メニュー",ChineseScript.Original,out var value,out var hit) && value == "菜单" && !hit,"first translation");
Check(cache.TryTranslate("メニュー",ChineseScript.Original,out value,out hit) && hit,"positive cache");
Check(!cache.TryTranslate("未知の語",ChineseScript.Original,out _,out hit) && !hit,"first miss");
Check(!cache.TryTranslate("未知の語",ChineseScript.Original,out _,out hit) && hit,"negative cache");
Check(cache.TryTranslate("メニュー",ChineseScript.Traditional,out value,out hit) && value == "菜單" && !hit,"script is part of cache identity");
Check(!cache.TryTranslate("挑戦回数",ChineseScript.Original,out _,out _),"control identifier remains protected");
for (var i = 0; i < 100; i++) cache.TryTranslate("残り"+i+"回",ChineseScript.Original,out _,out _);
Check(cache.Count == 8,"dynamic text cannot grow cache beyond capacity");
var count = cache.Count;
for (var i = 0; i < 100; i++) cache.TryTranslate(i.ToString(),ChineseScript.Original,out _,out _);
Check(cache.Count == count,"damage numbers do not evict shared translations");
cache.TryTranslate("メニュー",ChineseScript.Original,out _,out hit);
Check(!hit,"oldest entries evicted");
var fresh = new RenderedTextCache(Index("新菜单"));
Check(fresh.TryTranslate("メニュー",ChineseScript.Original,out value,out hit) && value == "新菜单" && !hit,"new package does not inherit old results");

var state = new RenderedTextState();
var calls = 0;
Func<string,string> translate = source => { calls++; return source == "メニュー" ? "菜单" : null; };
Check(state.Resolve("メニュー",1,true,ChineseScript.Original,translate,out value) && value == "菜单","component first write");
Check(!state.Resolve("菜单",1,true,ChineseScript.Original,translate,out _) && calls == 1,"unchanged final value bypasses lookup");
Check(state.Resolve("菜单",1,false,ChineseScript.Original,translate,out value) && value == "メニュー","turning off restores original");
Check(state.Resolve(value,1,true,ChineseScript.Original,translate,out value) && value == "菜单","turning on uses retained original");
Check(state.Resolve(value,2,true,ChineseScript.Original,s => s == "メニュー" ? "新菜单" : null,out value) && value == "新菜单","reload translates original, not old Chinese output");
Check(state.Resolve(value,2,true,ChineseScript.Traditional,s => s == "メニュー" ? "新菜單" : null,out value) && value == "新菜單","script change refreshes rendered text");
Check(!state.Resolve("戦闘中",2,true,ChineseScript.Traditional,translate,out value) && value == "戦闘中","external/native field change replaces stored original");
Check(!state.Resolve(value,2,false,ChineseScript.Traditional,translate,out value) && value == "戦闘中","disable cannot restore obsolete component content");
var missed = new RenderedTextState(); var misses = 0;
Func<string,string> noTranslation = _ => { misses++; return null; };
missed.Resolve("未翻訳",1,true,0,noTranslation,out _);
missed.Resolve("未翻訳",1,true,0,noTranslation,out _);
Check(misses == 1,"component caches misses too");
Check(missed.Resolve("未翻訳",2,true,0,_ => "新译文",out value) && value == "新译文","package invalidation retries previous miss");

var queue = new BoundedWorkQueue<int,string>(2);
Check(queue.Enqueue(1,"old") && queue.Enqueue(1,"latest") && queue.Count == 1,"deduplicate pooled component notifications");
Check(queue.Enqueue(2,"second") && !queue.Enqueue(3,"overflow"),"bounded queue backpressure");
Check(queue.TryDequeue(out value) && value == "latest","process latest assignment");
queue.Enqueue(1,"retry");
Check(queue.TryDequeue(out value) && value == "second","pending work retry cannot starve other components");
queue.Clear(); Check(queue.Count == 0 && !queue.TryDequeue(out _),"package invalidation cancels queued objects");

var pixels = new RgbaPixels(2,2,new byte[] {1,2,3,255, 4,5,6,255, 7,8,9,255, 10,11,12,0});
var canonical = new byte[] {7,8,9,255, 0,0,0,0, 1,2,3,255, 4,5,6,255};
Check(pixels.CanonicalHash() == Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant(),"canonical hash is top-down and ignores hidden transparent RGB");
var quad = new[] { new Vector2(0,0),new Vector2(2,0),new Vector2(2,2),new Vector2(0,2) };
var uv = new[] { new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1) };
var triangles = new ushort[] {0,1,2,2,3,0};
Check(pixels.Sprite(2,2,quad,uv,triangles,1,Vector2.Zero).Bytes.SequenceEqual(pixels.Bytes),"sprite mesh extraction preserves all four corner pixels");
Check(pixels.Copy(true).Sprite(2,2,quad,uv,triangles,1,Vector2.Zero,true).Bytes.SequenceEqual(pixels.Bytes),"opposite GPU row origin normalizes by source hash");
var triangle = pixels.Sprite(2,2,quad,uv,new ushort[] {0,1,2},1,Vector2.Zero);
Check(triangle.Bytes[11] == 0 && triangle.Bytes[3] == 255,"tight sprite mesh leaves non-triangle pixels transparent");
var rotated = pixels.Region(new(0,0,2,2,90));
Check(rotated.Bytes.SequenceEqual(new byte[] {4,5,6,255, 10,11,12,0, 1,2,3,255, 7,8,9,255}),"90 degree Spine unpack orientation");
Check(pixels.Copy(true).Region(new(0,0,2,2,90),true).Bytes.SequenceEqual(rotated.Bytes),"flipped GPU readback of rotated region");
foreach (var degrees in new[] {0,90})
{
    var atlas = new RgbaPixels(6,6,Enumerable.Repeat((byte)90,6*6*4).ToArray());
    var region = new PixelRegion(2,2,2,2,degrees);
    atlas.Patch(region,pixels);
    Check(atlas.Region(region).Bytes.SequenceEqual(pixels.Bytes),$"Spine patch/unpack {degrees} retains replacement geometry");
    Check(atlas.Bytes.AsSpan(0,4).SequenceEqual(new byte[] {90,90,90,90}),"non-title atlas art unchanged");
    Check(atlas.Bytes.AsSpan((2*6+1)*4,4).SequenceEqual(atlas.Bytes.AsSpan((2*6+2)*4,4)),"one-pixel edge extrusion refreshed");
}
if (args.Length >= 1)
{
    var package = PreparedImagePackage.Load(args[0]);
    Check(package.Pixels.Count == 72,"all current image assets decode");
    if (args.Length >= 2)
    {
        var oracle = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[1]));
        foreach (var pair in package.Pixels)
            Check(Convert.ToHexString(SHA256.HashData(pair.Value.Bytes)).ToLowerInvariant() == oracle[pair.Key],"independent Pillow pixel parity: " + pair.Key);
    }
}
cache = new RenderedTextCache(index);
cache.TryTranslate("メニュー",0,out _,out _);
for (var i = 0; i < 500; i++) cache.TryTranslate("メニュー",0,out _,out _);
var before = GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < 10000; i++) cache.TryTranslate("メニュー",0,out _,out _);
Check(GC.GetAllocatedBytesForCurrentThread()-before == 0,"warm shared lookups allocate no managed bytes");
Console.WriteLine($"PASS: {checks} performance/runtime regression assertions (no Unity performance claim)");
