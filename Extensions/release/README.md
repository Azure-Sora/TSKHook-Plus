# Plugin release staging

The plugin release contains only `TSKHook.dll`. Shared core source is compiled directly into it; the standalone core project remains available to the translation workbench.
Run `Extensions/Stage-Release.ps1 -GamePath <game directory>` after validation, commit the staged DLL and tag that commit. The release workflow publishes this file from that exact tag.

When upgrading from a two-DLL plugin release, close the game, replace `TSKHook.dll` and remove the old `TSKHook.Translation.Core.dll` from the plugin installation. Keep configuration, fonts, captures and translation packages.

Translation data and workbench releases live in Azure-Sora/TSKHook-UITranslations. ConfigurationManager and its keyboard-shortcut companion are installed from anosu/BepInEx.ConfigurationManager IL2CPP releases.


## v1.7.2

UI runtime optimizations and image compatibility fixes, independent image toggle, F1 configuration reload fallback when ConfigurationManager is absent, and startup-only automatic resource checks. Assembly version 1.7.2.0; SHA-256 2cd817fd48bcc94b75da59915625ff57762289f6d85e9d16fd0510bf13e805a3. See [release notes](../docs/release-1.7.2.md) for validation scope.

## v1.6.1

Urgent challenge-count control-identifier fix. Both BattleEnd retry_data.cost_name and next_data.cost_name remain original. The bare 挑戦回数 identifier is protected against exact/hash/rendered fallback. Assembly version 1.6.1.0; 155,136 bytes. Staged DLL passed 24 package/update/rollback/protection assertions; legacy/static UI regression passed 27+15 assertions. Real-game button retest is pending.

Upgrade after closing the game by replacing TSKHook.dll; text package 2026.10.15.1 removes the nine remaining next-stage translations and is independently auto-updatable.

## v1.6.0

Staged from runtime source 648e6a1. Assembly version 1.6.0.0, 154,624 bytes; SHA-256 95a40b03d1077a7259850448d3f834ee4bb94b3af9d4b7e02f9b7de2eafd472d.
The release rebuild differs from the game-tested DLL 48a56b395676f33213c764ebd8c8479f502652e1a1e0c6eb682045ad6b215706 in build metadata. All 567 method signatures/bodies, exception regions and embedded resources compared equal. No external Translation.Core, UiInspector or OpenCC runtime dependency.
Text 2026.10.14.2 and images 2026.10.14.3 are independent releases in the translation repository. Image packages require plugin 1.6.0; text packages require at least 1.4.6. See Extensions/docs/verification.md for the scoped real-game acceptance.
