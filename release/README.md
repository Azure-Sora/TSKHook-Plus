# Release DLL

`TSKHook.dll` is the locally verified release artifact uploaded by
`.github/workflows/release.yml` when a `v*` tag is pushed.

The plugin project references game-specific IL2CPP interop assemblies through
`GamePath`, so GitHub-hosted runners cannot reproduce the DLL from a clean
checkout. Before creating a release tag, rebuild locally, run the smoke tests,
and replace this DLL with the verified `bin/Release/net6.0/TSKHook.dll`.

The workflow publishes this DLL together with the version-controlled
`translations/ui/runtime/ui_translations.jsonl` file.
