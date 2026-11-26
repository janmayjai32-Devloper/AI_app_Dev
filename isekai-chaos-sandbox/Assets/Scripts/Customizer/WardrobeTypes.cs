using System;
using UnityEngine;

namespace IsekaiChaos.Customizer
{
    /// <summary>The 5 equip tiers, innermost first. Persisted as int — append only.</summary>
    public enum WardrobeLayer
    {
        BaseSkin = 0,       // body, tattoos, scars, markings (never "unequipped", only covered)
        Undergarments = 1,
        Clothing = 2,
        Armor = 3,
        Accessories = 4,
    }

    public enum GarmentCondition { Intact = 0, Torn = 1, Stripped = 2 }

    /// <summary>
    /// Body regions used for skin-occlusion. The base body mesh is split at import time into
    /// one renderer (or submesh) per region so fully-covered regions can be disabled outright:
    /// fewer triangles, no skin poking through cloth, and no extra draw call.
    /// </summary>
    [Flags]
    public enum BodyRegion
    {
        None      = 0,
        Head      = 1 << 0,
        Neck      = 1 << 1,
        Shoulders = 1 << 2,
        Chest     = 1 << 3,
        Abdomen   = 1 << 4,
        UpperArms = 1 << 5,
        Forearms  = 1 << 6,
        Hands     = 1 << 7,
        Hips      = 1 << 8,
        Thighs    = 1 << 9,
        Shins     = 1 << 10,
        Feet      = 1 << 11,
        Tail      = 1 << 12,
        Wings     = 1 << 13,
    }

}
