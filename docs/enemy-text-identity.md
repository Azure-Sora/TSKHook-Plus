# Enemy text identity repair (1.4.2)

Enemy `character_name`, `unit_name`, and `unit_detail` under `enemy_data` can share quest/shop identifiers despite containing different enemies. When no `unit_id` is available, their keys now include the full source SHA-256 in addition to the existing API and context identity. Array position is not part of the key. Numbered unit identities are unchanged.

Runtime lookup and capture import use the same shared builder. Workbench 0.4.1 provides a backed-up migration of recognizable legacy keys, including conflict suffixes. Unknown or mixed origins and competing translations remain for review. Install both updated runtime DLLs when publishing migrated translations.

Validation: `dotnet run --project tools/UiRuntimeSmokeTest -c Release` includes two different enemies in the same quest, exact runtime matches with no fallback, location-independent identity, unchanged numbered units, and legacy conflict conversion. This is fixture coverage, not a live game acceptance result.

## Other captured variants (1.4.3 / Workbench 0.5.0)

Numbered unit names also have legitimate text variants: event-effect sister entries may share a unit ID with ordinary titles while displaying `シスター`, and alternate name typography can coexist. Their keys now retain the semantic unit identity and append the complete source hash. Existing `enemy_list` keys retain their prior rule.

The same distinction applies to sister skill descriptions (colored/plain text and different effect versions), BossRushEventTop `stage_data` tab names, and MainStageDetail stage names reused across missions. Old base rows and conflict-suffixed rows can migrate to the shared runtime key; identical duplicates merge only when translations and exclusion state are compatible. Text, source hashes, human decisions, and provenance are preserved, with file backups and a key mapping.

Workbench 0.5.0 migrates ordinary records in these families as well as conflicts, so existing exact matches follow the new rule. Newly published translation packages require plugin 1.4.3. Runtime smoke coverage now contains 27 assertions. Earlier notes about unchanged numbered unit keys apply to 1.4.2 only.
