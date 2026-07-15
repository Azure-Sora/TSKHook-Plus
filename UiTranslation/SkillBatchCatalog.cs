using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TSKHook;

internal enum SkillBatchIdentifierMode
{
    Unknown,
    UserUnitId,
    MasterUnitId
}

internal sealed class SkillBatchCatalog
{
    private readonly object sync = new();
    private readonly Dictionary<int, int> unitsByUserId = new();

    internal SkillBatchIdentifierMode IdentifierMode { get; private set; }

    internal int UnitCount
    {
        get
        {
            lock (sync)
            {
                return unitsByUserId.Count;
            }
        }
    }

    internal int ObserveUnitList(string formattedResponse)
    {
        if (string.IsNullOrWhiteSpace(formattedResponse))
        {
            return 0;
        }

        using var document = JsonDocument.Parse(formattedResponse);
        if (!document.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("unit_list", out var unitList) ||
            unitList.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var added = 0;
        lock (sync)
        {
            foreach (var unit in unitList.EnumerateArray())
            {
                if (!TryGetPositiveInt(unit, "u_unit_id", out var userUnitId) ||
                    !TryGetPositiveInt(unit, "unit_id", out var masterUnitId))
                {
                    continue;
                }

                if (!unitsByUserId.ContainsKey(userUnitId))
                {
                    added++;
                }

                unitsByUserId[userUnitId] = masterUnitId;
            }
        }

        return added;
    }

    internal SkillBatchIdentifierMode ObserveKnownRequestId(int requestId)
    {
        if (requestId <= 0)
        {
            return IdentifierMode;
        }

        lock (sync)
        {
            if (IdentifierMode != SkillBatchIdentifierMode.Unknown)
            {
                return IdentifierMode;
            }

            var matchesUserId = unitsByUserId.ContainsKey(requestId);
            var matchesMasterId = unitsByUserId.Values.Contains(requestId);

            if (matchesUserId ^ matchesMasterId)
            {
                IdentifierMode = matchesUserId
                    ? SkillBatchIdentifierMode.UserUnitId
                    : SkillBatchIdentifierMode.MasterUnitId;
            }

            return IdentifierMode;
        }
    }

    internal IReadOnlyList<int> BuildRequestIds()
    {
        lock (sync)
        {
            return IdentifierMode switch
            {
                SkillBatchIdentifierMode.UserUnitId => unitsByUserId.Keys.OrderBy(value => value).ToArray(),
                SkillBatchIdentifierMode.MasterUnitId => unitsByUserId.Values.Distinct().OrderBy(value => value).ToArray(),
                _ => Array.Empty<int>()
            };
        }
    }

    internal static bool HasExSkillDataResponse(string formattedResponse)
    {
        if (string.IsNullOrWhiteSpace(formattedResponse))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(formattedResponse);
            return document.RootElement.TryGetProperty("result", out var result) &&
                   result.TryGetProperty("skill_data_list", out var skillDataList) &&
                   skillDataList.ValueKind == JsonValueKind.Array &&
                   skillDataList.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetPositiveInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return value > 0;
        }

        return property.ValueKind == JsonValueKind.String &&
               int.TryParse(property.GetString(), out value) && value > 0;
    }
}
