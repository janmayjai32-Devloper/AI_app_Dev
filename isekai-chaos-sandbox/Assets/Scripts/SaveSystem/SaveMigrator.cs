using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IsekaiChaos.SaveSystem
{
    /// <summary>
    /// Upgrades older save JSON to <see cref="SaveData.CurrentVersion"/> one step at a time,
    /// working on the raw JObject BEFORE typed deserialization, so renamed/removed fields can
    /// be rescued.
    ///
    /// To ship a breaking change:
    ///   1. bump SaveData.CurrentVersion (e.g. 1 → 2)
    ///   2. add  { 1, Migrate_1_to_2 }  to Steps
    ///   3. add a test fixture save at the old version
    /// Additive changes (new fields with defaults, new biomes/NPCs/outfits) need no step:
    /// missing fields deserialize to their C# defaults and unknown ids are skipped on restore.
    /// </summary>
    public static class SaveMigrator
    {
        static readonly Dictionary<int, Action<JObject>> Steps = new Dictionary<int, Action<JObject>>
        {
            // { 1, Migrate_1_to_2 },
        };

        public static JObject Migrate(JObject root)
        {
            int v = root.Value<int?>("saveVersion") ?? 0;
            if (v > SaveData.CurrentVersion)
                throw new InvalidOperationException(
                    $"Save is from a newer build (v{v}); this build supports up to v{SaveData.CurrentVersion}. Update the game.");

            if (v == 0) Migrate_0_to_1(root);        // pre-release saves without a version header
            v = Math.Max(v, 1);

            while (v < SaveData.CurrentVersion)
            {
                if (!Steps.TryGetValue(v, out var step))
                    throw new InvalidOperationException($"No migration step from save v{v}.");
                step(root);
                v++;
                root["saveVersion"] = v;
                Debug.Log($"[SaveMigrator] Migrated save to v{v}");
            }
            return root;
        }

        // Example step: early prototype stored wardrobe under "outfit" and gold at root.
        static void Migrate_0_to_1(JObject root)
        {
            if (root["player"] is JObject p && p["outfit"] != null && p["wardrobe"] == null)
            {
                p["wardrobe"] = p["outfit"];
                p.Remove("outfit");
            }
            if (root["gold"] != null)
            {
                root["economy"] ??= new JObject();
                root["economy"]["gold"] = root["gold"];
                root.Remove("gold");
            }
            root["saveVersion"] = 1;
        }

        // static void Migrate_1_to_2(JObject root)
        // {
        //     // e.g. Lust stat split into "attraction" + "desire":
        //     foreach (JObject r in root["relationships"] ?? new JArray())
        //         r["attraction"] = r["lust"];
        // }
    }
}
