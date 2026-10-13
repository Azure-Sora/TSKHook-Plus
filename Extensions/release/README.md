# Plugin release staging

The plugin release contains only `TSKHook.dll`. Shared core source is compiled directly into it; the standalone core project remains available to the translation workbench.
Run `Extensions/Stage-Release.ps1 -GamePath <game directory>` after validation, commit the staged DLL and tag that commit. The release workflow publishes this file from that exact tag.

When upgrading from a two-DLL plugin release, close the game, replace `TSKHook.dll` and remove the old `TSKHook.Translation.Core.dll` from the plugin installation. Keep configuration, fonts, captures and translation packages.

Translation data and workbench releases live in Azure-Sora/TSKHook-UITranslations. ConfigurationManager and its keyboard-shortcut companion are installed from anosu/BepInEx.ConfigurationManager IL2CPP releases.
