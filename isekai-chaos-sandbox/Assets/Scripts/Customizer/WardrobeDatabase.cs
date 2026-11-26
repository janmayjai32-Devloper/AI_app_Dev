using System.Collections.Generic;
using UnityEngine;

namespace IsekaiChaos.Customizer
{
    /// <summary>Registry of all wardrobe items, used to resolve item ids from save files.</summary>
    [CreateAssetMenu(menuName = "IsekaiChaos/Wardrobe Database", fileName = "WardrobeDatabase")]
    public class WardrobeDatabase : ScriptableObject
    {
        [SerializeField] List<WardrobeItem> items = new List<WardrobeItem>();
        Dictionary<string, WardrobeItem> _byId;

        public WardrobeItem Get(string itemId)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, WardrobeItem>();
                foreach (var i in items)
                    if (i && !string.IsNullOrEmpty(i.itemId)) _byId[i.itemId] = i;
            }
            return itemId != null && _byId.TryGetValue(itemId, out var item) ? item : null;
        }

        public IReadOnlyList<WardrobeItem> All => items;

#if UNITY_EDITOR
        void OnValidate() => _byId = null;
#endif
    }
}
