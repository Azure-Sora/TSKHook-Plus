# Plugin release staging

The plugin release contains only `TSKHook.dll`. Shared core source is compiled directly into it; the standalone core project remains available to the translation workbench.
Run `Extensions/Stage-Release.ps1 -GamePath <game directory>` after validation, commit the staged DLL and tag that commit. The release workflow publishes this file from that exact tag.

When upgrading from a two-DLL plugin release, close the game, replace `TSKHook.dll` and remove the old `TSKHook.Translation.Core.dll` from the plugin installation. Keep configuration, fonts, captures and translation packages.

Translation data and workbench releases live in Azure-Sora/TSKHook-UITranslations. ConfigurationManager and its keyboard-shortcut companion are installed from anosu/BepInEx.ConfigurationManager IL2CPP releases.


## v1.6.0

Staged from runtime source 648e6a1. Assembly version 1.6.0.0, 154,624 bytes; SHA-256 95a40b03d1077a7259850448d3f834ee4bb94b3af9d4b7e02f9b7de2eafd472d.
The release rebuild differs from the game-tested DLL 48a56b395676f33213c764ebd8c8479f502652e1a1e0c6eb682045ad6b215706 in build metadata. All 567 method signatures/bodies, exception regions and embedded resources compared equal. No external Translation.Core, UiInspector or OpenCC runtime dependency.
Text 2026.10.14.2 and images 2026.10.14.3 are independent releases in the translation repository. Image packages require plugin 1.6.0; text packages require at least 1.4.6. See Extensions/docs/verification.md for the scoped real-game acceptance.
