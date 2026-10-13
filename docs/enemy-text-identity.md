# Enemy text identity repair (1.4.2)

Enemy `character_name`, `unit_name`, and `unit_detail` under `enemy_data` can share quest/shop identifiers despite containing different enemies. When no `unit_id` is available, their keys now include the full source SHA-256 in addition to the existing API and context identity. Array position is not part of the key. Numbered unit identities are unchanged.

Runtime lookup and capture import use the same shared builder. Workbench 0.4.1 provides a backed-up migration of recognizable legacy keys, including conflict suffixes. Unknown or mixed origins and competing translations remain for review. Install both updated runtime DLLs when publishing migrated translations.

Validation: `dotnet run --project tools/UiRuntimeSmokeTest -c Release` includes two different enemies in the same quest, exact runtime matches with no fallback, location-independent identity, unchanged numbered units, and legacy conflict conversion. This is fixture coverage, not a live game acceptance result.
