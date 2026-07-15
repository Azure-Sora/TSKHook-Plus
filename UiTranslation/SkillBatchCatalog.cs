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
    private readonly Dictionary<int, UnitRecord> unitsByUserId = new();

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

                var skillIds = new HashSet<int>();
                if (unit.TryGetProperty("skill_data", out var skillData) &&
                    skillData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var skill in skillData.EnumerateArray())
                    {
                        if (TryGetPositiveInt(skill, "skill_id", out var skillId))
                        {
                            skillIds.Add(skillId);
                        }
                    }
                }

                unitsByUserId[userUnitId] = new UnitRecord(masterUnitId, skillIds);
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
            var matchesMasterId = unitsByUserId.Values.Any(unit => unit.MasterUnitId == requestId);

            if (matchesUserId ^ matchesMasterId)
            {
                IdentifierMode = matchesUserId
                    ? SkillBatchIdentifierMode.UserUnitId
                    : SkillBatchIdentifierMode.MasterUnitId;
            }

            return IdentifierMode;
        }
    }

    internal bool TryGetCalibrationProbe(out int userUnitId, out int masterUnitId)
    {
        lock (sync)
        {
            var candidate = unitsByUserId
                .Where(pair => pair.Key != pair.Value.MasterUnitId && pair.Value.SkillIds.Count > 0)
                .OrderBy(pair => pair.Key)
                .FirstOrDefault();

            if (candidate.Value == null)
            {
                userUnitId = 0;
                masterUnitId = 0;
                return false;
            }

            userUnitId = candidate.Key;
            masterUnitId = candidate.Value.MasterUnitId;
            return true;
        }
    }

    internal bool SetIdentifierMode(SkillBatchIdentifierMode mode)
    {
        if (mode == SkillBatchIdentifierMode.Unknown)
        {
            return false;
        }

        lock (sync)
        {
            if (IdentifierMode != SkillBatchIdentifierMode.Unknown && IdentifierMode != mode)
            {
                return false;
            }

            IdentifierMode = mode;
            return true;
        }
    }

    internal IReadOnlyList<int> BuildRequestIds()
    {
        lock (sync)
        {
            return IdentifierMode switch
            {
                SkillBatchIdentifierMode.UserUnitId => unitsByUserId.Keys.OrderBy(value => value).ToArray(),
                SkillBatchIdentifierMode.MasterUnitId => unitsByUserId.Values
                    .Select(unit => unit.MasterUnitId).Distinct().OrderBy(value => value).ToArray(),
                _ => Array.Empty<int>()
            };
        }
    }

    internal bool ResponseMatchesUnitSkills(string formattedResponse, int userUnitId)
    {
        HashSet<int> expectedSkillIds;
        lock (sync)
        {
            if (!unitsByUserId.TryGetValue(userUnitId, out var unit) || unit.SkillIds.Count == 0)
            {
                return false;
            }

            expectedSkillIds = new HashSet<int>(unit.SkillIds);
        }

        if (string.IsNullOrWhiteSpace(formattedResponse))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(formattedResponse);
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("skill_data_list", out var skillDataList) ||
                skillDataList.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var skill in skillDataList.EnumerateArray())
            {
                if (TryGetPositiveInt(skill, "ex_skill_id", out var skillId) &&
                    expectedSkillIds.Contains(skillId))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
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

    private sealed class UnitRecord
    {
        internal UnitRecord(int masterUnitId, HashSet<int> skillIds)
        {
            MasterUnitId = masterUnitId;
            SkillIds = skillIds;
        }

        internal int MasterUnitId { get; }
        internal HashSet<int> SkillIds { get; }
    }
}
