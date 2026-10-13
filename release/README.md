# Plugin release staging

`TSKHook.dll` and `TSKHook.Translation.Core.dll` must be released together.
Run `scripts/Stage-Release.ps1 -GamePath <game directory>` after validation, commit the resulting assemblies and tag that commit. The release workflow publishes both files from that exact tag.

Translation data and workbench releases live in Azure-Sora/TSKHook-UITranslations. ConfigurationManager and its keyboard-shortcut companion are installed from anosu/BepInEx.ConfigurationManager IL2CPP releases.
