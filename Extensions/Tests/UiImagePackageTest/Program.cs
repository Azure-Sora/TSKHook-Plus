using System.Text.Json;
using TSKHook;

if (args.Length != 1) throw new ArgumentException("Pass translations/images/ui_images from the translation repository.");
var root = Path.GetFullPath(args[0]);
var manifest = UiImageManifest.Read(root);
var checks = 0;
void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
void Reject(Action action,string message) {
 bool rejected=false;
 try { action(); } catch(InvalidDataException) { rejected=true; }
 Check(rejected,message);
}
Check(manifest.Sprites.Count >= 38,"Missing common sprite rules");
Check(manifest.Spine.Count == 16,"Missing large navigation/chapter cards");
Check(manifest.Sprites.Count(x=>x.Sprite.StartsWith("btn_exchange_"))==7,"Expected seven permanent exchange tabs");
Check(manifest.Sprites.Count(x=>x.Sprite.StartsWith("m_gacha_"))==4,"Expected four permanent gacha tabs");
Check(!manifest.Sprites.Any(x=>x.Sprite.StartsWith("m_event_") || x.Sprite.StartsWith("equip_") || x.Sprite.StartsWith("m_gacha_034")),"Limited banners or product art must be excluded");
Reject(()=>UiImageManifest.AssetPath(root,"../outside.png"),"Traversal accepted");
Reject(()=>UiImageManifest.AssetPath(root,Path.GetFullPath("outside.png")),"Absolute path accepted");
Reject(()=>UiImageManifest.AssetPath(root,"image.dll"),"Non-PNG file accepted");
var temporary = Path.Combine(Path.GetTempPath(),"TSKHook-UiImagePackage-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try {
 foreach(var file in Directory.GetFiles(root,"*.png")) File.Copy(file,Path.Combine(temporary,Path.GetFileName(file)));
 var path=Path.Combine(temporary,"manifest.json");
 void Write()=>File.WriteAllText(path,JsonSerializer.Serialize(manifest));
 Write(); Check(UiImageManifest.Read(temporary).Spine.Count==16,"Package roundtrip failed");
 var equivalent=UiImageManifest.Read(temporary);equivalent.Version="another-version";
 Check(manifest.HasSameRules(equivalent),"Version-only changes should retain live covers");
 equivalent.Sprites[0].Sha256=new string('0',64);
 Check(!manifest.HasSameRules(equivalent),"Changed image checksum must invalidate covers");
 equivalent=UiImageManifest.Read(temporary);equivalent.Spine[0].Regions[0].OffsetX++;
 Check(!manifest.HasSameRules(equivalent),"Changed Spine geometry must invalidate covers");
 manifest.Sprites[0].Width++; Write(); Reject(()=>UiImageManifest.Read(temporary),"Dimension mismatch accepted"); manifest.Sprites[0].Width--;
 manifest.Sprites.Add(manifest.Sprites[0]);Write();Reject(()=>UiImageManifest.Read(temporary),"Duplicate rule accepted");manifest.Sprites.RemoveAt(manifest.Sprites.Count-1);
 manifest.Spine[0].Regions[0].Degrees=45;Write();Reject(()=>UiImageManifest.Read(temporary),"Unsupported Spine rotation accepted");manifest.Spine[0].Regions[0].Degrees=0;
 manifest=UiImageManifest.Read(root);Write();
 var imagePath=Path.Combine(temporary,manifest.Sprites[0].File);var bytes=File.ReadAllBytes(imagePath);bytes[^1]^=1;File.WriteAllBytes(imagePath,bytes);
 Reject(()=>UiImageManifest.Read(temporary),"Tampered image accepted");
} finally { Directory.Delete(temporary,true); }
Console.WriteLine($"UI image package passed: {checks} checks, {manifest.Sprites.Count} sprites, {manifest.Spine.Count} Spine cards, {manifest.Spine.Sum(x=>x.Regions.Count)} guarded title regions.");
