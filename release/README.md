# Release DLL

`TSKHook.dll` is the locally verified release artifact uploaded by
`.github/workflows/release.yml` when a `v*` tag is pushed.

The plugin project references game-specific IL2CPP interop assemblies through
`GamePath`, so GitHub-hosted runners cannot reproduce the DLL from a clean
checkout. Before creating a release tag, rebuild locally, run the smoke tests,
and replace this DLL with the verified `bin/Release/net6.0/TSKHook.dll`.

The workflow downloads this DLL and the version-controlled
`translations/ui/runtime/ui_translations.jsonl` directly from the tagged
revision through GitHub's API, then publishes them as Release assets. It does
not depend on a checkout action or attempt to rebuild the plugin on GitHub.
