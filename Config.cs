using System.Text.Json;
using Il2CppSystem.IO;
using Il2CppSystem.Text;

namespace TSKHook;

public class TSKConfig
{
    public static double Speed;
    public static int FPS;
    public static bool TranslationEnabled;
    public static int width;
    public static int height;
    public static float zoom;
    public static bool UiTranslationEnabled;
    public static bool UiCaptureEnabled;
    public static int UiCaptureFlushSeconds;
    public static bool UiSpriteDumpEnabled;

    public static void Read()
    {
        if (File.Exists("./BepInEx/plugins/config.json"))
        {
            var content = File.InternalReadAllText("./BepInEx/plugins/config.json", Encoding.UTF8);
            var doc = JsonDocument.Parse(content);
            var config = doc.RootElement;

            var needWrite = false;

            if (config.TryGetProperty("speed", out var sValue))
            {
                Speed = sValue.GetDouble();
            }
            else
            {
                Speed = 0.5;
                needWrite = true;
            }

            if (config.TryGetProperty("fps", out var fValue))
            {
                FPS = fValue.GetInt32();
            }
            else
            {
                FPS = 60;
                needWrite = true;
            }

            if (config.TryGetProperty("translation", out var tValue))
            {
                TranslationEnabled = tValue.GetBoolean();
            }
            else
            {
                TranslationEnabled = true;
                needWrite = true;
            }

            if (config.TryGetProperty("width", out var wValue))
            {
                width = wValue.GetInt32();
            }
            else
            {
                width = 1280;
                needWrite = true;
            }

            if (config.TryGetProperty("height", out var hValue))
            {
                height = hValue.GetInt32();
            }
            else
            {
                height = 720;
                needWrite = true;
            }

            if (config.TryGetProperty("zoom", out var zValue))
            {
                zoom = (float)zValue.GetDouble();
            }
            else
            {
                zoom = 1.0f;
                needWrite = true;
            }

            if (config.TryGetProperty("uiCapture", out var uiCaptureValue))
            {
                UiCaptureEnabled = uiCaptureValue.GetBoolean();
            }
            else
            {
                UiCaptureEnabled = true;
                needWrite = true;
            }

            if (config.TryGetProperty("uiTranslation", out var uiTranslationValue))
            {
                UiTranslationEnabled = uiTranslationValue.GetBoolean();
            }
            else
            {
                UiTranslationEnabled = true;
                needWrite = true;
            }

            if (config.TryGetProperty("uiCaptureFlushSeconds", out var uiCaptureFlushSecondsValue))
            {
                UiCaptureFlushSeconds = System.Math.Max(1, uiCaptureFlushSecondsValue.GetInt32());
            }
            else
            {
                UiCaptureFlushSeconds = 5;
                needWrite = true;
            }

            if (config.TryGetProperty("uiSpriteDump", out var uiSpriteDumpValue))
            {
                UiSpriteDumpEnabled = uiSpriteDumpValue.GetBoolean();
            }
            else
            {
                UiSpriteDumpEnabled = false;
                needWrite = true;
            }

            if (needWrite) WriteJsonFile(Speed, FPS, TranslationEnabled, width, height, zoom,
                UiTranslationEnabled, UiCaptureEnabled, UiCaptureFlushSeconds, UiSpriteDumpEnabled);

            Plugin.Global.Log.LogInfo("Current setting:");
            Plugin.Global.Log.LogInfo("Game speed(each step): " + Speed);
            Plugin.Global.Log.LogInfo("FPS: " + FPS);
            Plugin.Global.Log.LogInfo("Translation: " + (TranslationEnabled ? "Enabled" : "Disabled"));
            Plugin.Global.Log.LogInfo("Zoom ratio: " + zoom);
            Plugin.Global.Log.LogInfo("UI translation: " + (UiTranslationEnabled ? "Enabled" : "Disabled"));
            Plugin.Global.Log.LogInfo("UI capture: " + (UiCaptureEnabled ? "Enabled" : "Disabled"));
            Plugin.Global.Log.LogInfo("UI Sprite dump: " + (UiSpriteDumpEnabled ? "Enabled" : "Disabled"));
        }
        else
        {
            Plugin.Global.Log.LogWarning("config.json not found!!!");
            Plugin.Global.Log.LogWarning("Using default config.");
            Speed = 0.5;
            FPS = 60;
            TranslationEnabled = true;
            width = 1280;
            height = 720;
            zoom = 1.0f;
            UiTranslationEnabled = true;
            UiCaptureEnabled = true;
            UiCaptureFlushSeconds = 5;
            UiSpriteDumpEnabled = false;

            // Create default JSON file
            WriteJsonFile(0.5, 60, true, width, height, zoom, true, true, 5, false);
        }
    }

    public static void WriteJsonFile(double speed, int fps, bool enabled, int w, int h, float z,
        bool uiTranslation, bool uiCapture, int uiCaptureFlushSeconds, bool uiSpriteDump)
    {
        var config = new config
        {
            speed = speed,
            fps = fps,
            translation = enabled,
            width = w,
            height = h,
            zoom = z,
            uiTranslation = uiTranslation,
            uiCapture = uiCapture,
            uiCaptureFlushSeconds = uiCaptureFlushSeconds,
            uiSpriteDump = uiSpriteDump
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("./BepInEx/plugins/config.json", json);
    }

    public class config
    {
        public double speed { get; set; }
        public int fps { get; set; }
        public bool translation { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public float zoom { get; set; }
        public bool uiTranslation { get; set; }
        public bool uiCapture { get; set; }
        public int uiCaptureFlushSeconds { get; set; }
        public bool uiSpriteDump { get; set; }
    }
}
