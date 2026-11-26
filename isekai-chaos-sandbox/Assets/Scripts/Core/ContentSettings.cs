using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsekaiChaos.Core
{
    /// <summary>
    /// Player-facing content gate. Mature content is OFF by default and can only be enabled
    /// after the age confirmation screen. Every mature-only code path (intimacy scenes,
    /// stripping below the undergarment layer, etc.) must check <see cref="MatureContentEnabled"/>.
    ///
    /// Stored in PlayerPrefs (device-level preference), NOT in save files, so a shared save
    /// can never switch mature content on for another device/user.
    /// </summary>
    public static class ContentSettings
    {
        const string KeyAgeConfirmed = "content.ageConfirmed";
        const string KeyMature = "content.matureEnabled";
        const string KeyThemePrefix = "content.theme.";

        public static event Action Changed;

        public static bool AgeConfirmed => PlayerPrefs.GetInt(KeyAgeConfirmed, 0) == 1;

        public static bool MatureContentEnabled =>
            AgeConfirmed && PlayerPrefs.GetInt(KeyMature, 0) == 1;

        /// <summary>Called by the age-gate UI after the player confirms they are 18+.</summary>
        public static void ConfirmAdult()
        {
            PlayerPrefs.SetInt(KeyAgeConfirmed, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetMatureContent(bool enabled)
        {
            if (enabled && !AgeConfirmed)
            {
                Debug.LogWarning("[ContentSettings] Age not confirmed; mature content stays disabled.");
                return;
            }
            PlayerPrefs.SetInt(KeyMature, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Per-theme opt-in filters shown in the Settings > Content menu. Scenes tagged with a
        /// theme the player has switched off are skipped or swapped for a fade-to-black variant.
        /// </summary>
        public static bool IsThemeEnabled(string themeTag) =>
            PlayerPrefs.GetInt(KeyThemePrefix + themeTag, 1) == 1;

        public static void SetThemeEnabled(string themeTag, bool enabled)
        {
            PlayerPrefs.SetInt(KeyThemePrefix + themeTag, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool AllThemesEnabled(IEnumerable<string> themeTags)
        {
            foreach (var t in themeTags)
                if (!IsThemeEnabled(t)) return false;
            return true;
        }
    }
}
