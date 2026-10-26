using TSKHook;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

var checks = 0;
void Check(bool condition,string message) { checks++; if (!condition) throw new Exception(message); }
var sourcePixels = new RgbaPixels(2,2,new byte[] {1,2,3,255, 4,5,6,255, 7,8,9,255, 10,11,12,255});
var translated = new RgbaPixels(2,2,Enumerable.Repeat((byte)120,16).ToArray());
var translatedB = new RgbaPixels(2,2,Enumerable.Repeat((byte)230,16).ToArray());
PreparedImagePackage Package(string version,RgbaPixels replacement) => new("fixture",new UiImageManifest
{
    Version=version,SchemaVersion=1,
    Sprites = new() { new() { Sprite="button",File="button.png",Width=2,Height=2,SourceRgbaSha256=sourcePixels.CanonicalHash(),Sha256=version } }
},new Dictionary<string,RgbaPixels> { ["button.png"] = replacement });
Sprite Source()
{
    var texture = new Texture2D(2,2,TextureFormat.RGBA32,false,false) { Pixels=sourcePixels };
    return new Sprite { name="button",texture=texture,rect=new(0,0,2,2),pivot=new(0,0),
        vertices=new[] {new Vector2(0,0),new Vector2(2,0),new Vector2(2,2),new Vector2(0,2)},
        uv=new[] {new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)},triangles=new ushort[] {0,1,2,2,3,0} };
}
async Task Pump(Func<bool> finished)
{
    for (var i=0;i<1000;i++) { UiImageTranslationService.Update(); if (finished()) return; await Task.Delay(1); }
    throw new Exception("runtime work did not complete");
}
UiImageTranslationService.Install(Package("A",translated));
var source = Source(); var image = new Image { sprite=source };
UiRuntimeActivity.InBattle=true;
UiImageTranslationService.Notify(image); UiImageTranslationService.Update();
Check(!image.HasOverride && UiTextureReadback.Reads==0,"battle does not start uncached image jobs");
UiRuntimeActivity.InBattle=false;
await Pump(() => image.HasOverride);
Check(image.overrideSprite.texture.Pixels.Bytes.SequenceEqual(translated.Bytes),"source matched and prepared pixels uploaded");
var reads = UiTextureReadback.Reads;
var second = new Image { sprite=source };
UiRuntimeActivity.InBattle=true;
UiImageTranslationService.Notify(second); await Pump(() => second.HasOverride);
Check(second.overrideSprite==image.overrideSprite && UiTextureReadback.Reads==reads,"warm replacement can bind during battle without readback");
UiRuntimeActivity.InBattle=false;
var changed = Source(); changed.name="untranslated";
image.sprite=changed; await Pump(() => !image.HasOverride);
Check(image.overrideSprite==changed,"watcher detects pooled image base changes without setter notifications");
image.sprite=source; await Pump(() => image.HasOverride);
Check(image.overrideSprite==second.overrideSprite,"watcher restores cached cover after selected/unselected state changes");
var assignedLater=new Image();
UiImageTranslationService.Notify(assignedLater); UiImageTranslationService.Update();
assignedLater.sprite=source;
await Pump(() => assignedLater.HasOverride);
Check(assignedLater.overrideSprite==second.overrideSprite,"sprite assigned after OnEnable is discovered without setter hook");
TSKConfig.Settings.ImageTranslation.Value=false; UiImageTranslationService.Update();
Check(!second.HasOverride && second.sprite==source,"turning off images restores game sprite");
TSKConfig.Settings.ImageTranslation.Value=true; UiImageTranslationService.Notify(second); await Pump(() => second.HasOverride);
var external=Source(); external.name="other-mod";
second.overrideSprite=external;
TSKConfig.Settings.ImageTranslation.Value=false; UiImageTranslationService.Update();
Check(second.overrideSprite==external,"disable preserves an override now owned by another mod");
TSKConfig.Settings.ImageTranslation.Value=true;

UiImageTranslationService.Install(Package("A",translated));
var delayed = new TaskCompletionSource<RgbaPixels>(TaskCreationOptions.RunContinuationsAsynchronously);
UiTextureReadback.Next=delayed.Task;
var stale = new Image { sprite=Source() };
UiImageTranslationService.Notify(stale); UiImageTranslationService.Update();
UiImageTranslationService.Install(Package("B",translatedB));
delayed.SetResult(sourcePixels);
UiImageTranslationService.Notify(stale); await Pump(() => stale.HasOverride);
Check(stale.overrideSprite.texture.Pixels.Bytes.SequenceEqual(translatedB.Bytes),"old completion cannot apply previous package after reload");
var retained=stale.overrideSprite;
UiImageTranslationService.Install(Package("B",translatedB));
Check(stale.overrideSprite==retained,"equivalent-package install retains live covers");

var root=new GameObject();
var bootstrap=new Image {gameObject=root,sprite=Source()};
root.Components[typeof(Image)]=bootstrap;
UnityEngine.SceneManagement.SceneManager.Roots=new[] {root};
var beforeDiscoveryWarnings=Plugin.Global.Log.Warnings.Count;
UiImageTranslationService.RequestDiscovery();
await Pump(() => bootstrap.HasOverride);
Check(UnityEngine.SceneManagement.SceneManager.RootEnumerations>0,"bootstrap uses working list overload even when parameterless wrapper throws");
Check(Plugin.Global.Log.Warnings.Count==beforeDiscoveryWarnings,"scene discovery completes without compatibility warning");
UnityEngine.SceneManagement.SceneManager.Roots=Array.Empty<GameObject>();

var bad=Source(); bad.texture.Pixels=new(2,2,new byte[16]);
var rejected = new Image { sprite=bad };
var warnings=Plugin.Global.Log.Warnings.Count;
UiImageTranslationService.Notify(rejected); await Pump(() => Plugin.Global.Log.Warnings.Count>warnings);
Check(!rejected.HasOverride,"pixel mismatch never overrides game artwork");
reads=UiTextureReadback.Reads;
for(var i=0;i<10;i++) { UiImageTranslationService.Notify(rejected); UiImageTranslationService.Update(); }
Check(UiTextureReadback.Reads==reads,"rejected source does not restart expensive work");

foreach(var multiple in new[] {false,true})
{
    var texture = new Texture2D(2,2,TextureFormat.RGBA32,false,false) {name="atlas",Pixels=sourcePixels};
    var material = new Material {mainTexture=texture};
    var atlas = new SpineAtlasAsset {materials=new[] {material}};
    atlas.Atlas.Regions["title"]=new() {page=new() {rendererObject=material},width=2,height=2,originalWidth=2,originalHeight=2};
    var asset = new SkeletonDataAsset {name="card",atlasAssets=new object[] {atlas}};
    var graphic=new SkeletonGraphic {SkeletonDataAsset=asset,baseTexture=texture,allowMultipleCanvasRenderers=multiple};
    var manifest=new UiImageManifest {Version="spine",Spine=new() {new() {Asset="card",Texture="atlas",Regions=new() {
        new() {Name="title",File="title.png",Width=2,Height=2,OriginalWidth=2,OriginalHeight=2,SourceRgbaSha256=sourcePixels.CanonicalHash()}
    }}}};
    UiImageTranslationService.Install(new("fixture",manifest,new Dictionary<string,RgbaPixels> { ["title.png"]=translated }));
    UiImageTranslationService.Notify(graphic);
    await Pump(() => multiple ? graphic.CustomTextureOverride.ContainsKey(texture) : graphic.OverrideTexture!=null);
    var replacement=multiple ? graphic.CustomTextureOverride[texture] : graphic.OverrideTexture;
    Check(replacement.Pixels.Bytes.SequenceEqual(translated.Bytes),$"Spine pixels applied, multiple={multiple}");
    TSKConfig.Settings.ImageTranslation.Value=false; UiImageTranslationService.Update();
    Check(multiple ? !graphic.CustomTextureOverride.ContainsKey(texture) : graphic.OverrideTexture==null,"Spine disable restores only our texture mapping");
    TSKConfig.Settings.ImageTranslation.Value=true;
}
UiImageTranslationService.Shutdown();
Console.WriteLine($"PASS: {checks} image-service lifecycle assertions (Unity substitutes; game integration remains manual)");
