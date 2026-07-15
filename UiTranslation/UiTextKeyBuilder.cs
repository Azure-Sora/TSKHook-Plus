using System;
using System.Collections.Generic;
using System.Text;

namespace TSKHook;

internal static class UiTextKeyBuilder
{
    private static readonly string[] GenericIdentityFields =
    {
        "campaign_id", "category_id", "character_id", "event_id", "group_id", "id", "quest_id",
        "reward_id", "reward_type", "shop_id", "sister_unit_id", "stage_id", "status_effect_id", "unit_id"
    };

    internal static UiTextKeyDescriptor Build(string apiName, string path, string source, string sourceHash,
        IReadOnlyDictionary<string, string> context)
    {
        context ??= new Dictionary<string, string>(StringComparer.Ordinal);
        var field = path[(path.LastIndexOf('.') + 1)..].Replace("[]", "", StringComparison.Ordinal);

        if (field.StartsWith("exclusive_unit_", StringComparison.OrdinalIgnoreCase) &&
            context.ContainsKey("exclusive_unit_id"))
        {
            return Create("unit", field, context, ("exclusive_unit_id", true));
        }

        if (field is "unit_name" or "character_name" or "character_name_kana" or "full_name" &&
            context.ContainsKey("unit_id"))
        {
            return Create("unit", field, context, ("unit_id", true), ("character_id", false));
        }

        if (context.TryGetValue("item_id", out var itemId) &&
            (field.Contains("item", StringComparison.OrdinalIgnoreCase) || field == "detail"))
        {
            return itemId == "0"
                ? Create("item", field, context, ("item_type", false), ("item_id", true), ("cost_type", true))
                : Create("item", field, context, ("item_type", false), ("item_id", true));
        }

        if (context.ContainsKey("reward_id") &&
            (field.Contains("reward", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("reward_list", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("product_detail_list", StringComparison.OrdinalIgnoreCase)))
        {
            return CreateReward(field, context, source);
        }

        if (path.Contains("sister_unit_list", StringComparison.OrdinalIgnoreCase) &&
            path.Contains("skill", StringComparison.OrdinalIgnoreCase))
        {
            return CreateSisterSkill(field, path, context);
        }

        if (context.ContainsKey("equip_id") &&
            (field.Contains("equip", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("equip", StringComparison.OrdinalIgnoreCase)))
        {
            var includeSkill = context.ContainsKey("skill_id") &&
                               (path.Contains("skill", StringComparison.OrdinalIgnoreCase) ||
                                path.Contains("enchant", StringComparison.OrdinalIgnoreCase));
            return includeSkill
                ? Create("equip", field, context, ("equip_id", true), ("skill_id", true),
                    ("frame_no", false), ("effect_type", false), ("lv", false))
                : Create("equip", field, context, ("equip_id", true));
        }

        if (context.ContainsKey("ex_skill_id") &&
            path.Contains("skill_data_list", StringComparison.OrdinalIgnoreCase))
        {
            return CreateExSkill(field, context);
        }

        if (context.ContainsKey("skill_id") && path.Contains("skill", StringComparison.OrdinalIgnoreCase))
        {
            return Create("skill", field, context,
                ("skill_data_type", false), ("skill_id", true), ("lv", false), ("unit_id", false));
        }

        if (context.ContainsKey("mission_id") || path.Contains("mission", StringComparison.OrdinalIgnoreCase))
        {
            return Create("mission", field, context, ("mission_type", false), ("mission_group_id", false),
                ("mission_id", false), ("id", false));
        }

        if (context.ContainsKey("product_id") || path.Contains("product", StringComparison.OrdinalIgnoreCase))
        {
            return Create("shop", field, context, ("shop_id", false), ("product_id", false), ("id", false));
        }

        if (context.ContainsKey("unit_id") &&
            (path.Contains("unit", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("character", StringComparison.OrdinalIgnoreCase)))
        {
            return Create("unit", field, context, ("unit_id", true), ("character_id", false));
        }

        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in GenericIdentityFields)
        {
            if (context.TryGetValue(name, out var value))
            {
                identity[name] = value;
            }
        }

        if (identity.Count == 0)
        {
            identity["source_hash"] = sourceHash[..Math.Min(12, sourceHash.Length)];
        }

        return FromIdentity("misc", field, identity, apiName);
    }

    private static UiTextKeyDescriptor CreateExSkill(string field,
        IReadOnlyDictionary<string, string> context)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["skill_id"] = context["ex_skill_id"]
        };
        if (context.TryGetValue("ex_skill_lv", out var level))
        {
            identity["lv"] = level;
        }

        var normalizedField = field switch
        {
            "ex_skill_name" => "skill_name",
            "detail" => "skill_detail",
            _ => field
        };
        return FromIdentity("skill", normalizedField, identity, null);
    }

    private static UiTextKeyDescriptor CreateSisterSkill(string field, string path,
        IReadOnlyDictionary<string, string> context)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[]
                 {
                     "sister_unit_id", "active_skill_id", "support_skill_id", "status_effect_id", "skill_type", "lv"
                 })
        {
            if (context.TryGetValue(name, out var value))
            {
                identity[name] = value;
            }
        }

        identity["skill_kind"] = path.Contains("extra_support_skill_data", StringComparison.OrdinalIgnoreCase)
            ? "extra_support"
            : path.Contains("active_skill_data", StringComparison.OrdinalIgnoreCase)
                ? "active"
                : "support";
        return FromIdentity("skill", field, identity, null);
    }

    private static UiTextKeyDescriptor CreateReward(string field, IReadOnlyDictionary<string, string> context,
        string source)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "reward_type", "reward_id" })
        {
            if (context.TryGetValue(name, out var value))
            {
                identity[name] = value;
            }
        }

        if (!identity.ContainsKey("reward_id"))
        {
            identity["reward_id"] = "missing";
        }

        if (field == "reward_name")
        {
            identity["layout"] = source.Contains('\n') ? "multiline" : "singleline";
        }

        return FromIdentity("reward", field, identity, null);
    }

    private static UiTextKeyDescriptor Create(string category, string field,
        IReadOnlyDictionary<string, string> context, params (string Name, bool Required)[] fields)
    {
        var identity = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var fieldSpec in fields)
        {
            if (context.TryGetValue(fieldSpec.Name, out var value))
            {
                identity[fieldSpec.Name] = value;
            }
            else if (fieldSpec.Required)
            {
                identity[fieldSpec.Name] = "missing";
            }
        }

        return FromIdentity(category, field, identity, null);
    }

    private static UiTextKeyDescriptor FromIdentity(string category, string field,
        SortedDictionary<string, string> identity, string scope)
    {
        var key = new StringBuilder(category).Append(':');
        if (!string.IsNullOrEmpty(scope))
        {
            key.Append(Escape(scope)).Append(':');
        }
        foreach (var pair in identity)
        {
            key.Append(pair.Key).Append('=').Append(Escape(pair.Value)).Append(':');
        }
        key.Append(field);
        return new UiTextKeyDescriptor(key.ToString(), category, field, identity);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value ?? string.Empty);
}

internal sealed record UiTextKeyDescriptor(string Key, string Category, string Field,
    SortedDictionary<string, string> Identity);
