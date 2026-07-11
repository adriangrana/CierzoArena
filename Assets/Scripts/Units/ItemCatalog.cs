using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    public sealed class ItemCatalog : MonoBehaviour
    {
        [SerializeField] private ItemDefinition[] items;
        private readonly Dictionary<string, ItemDefinition> byId = new();
        private readonly HashSet<string> duplicateIds = new();
        private static ItemCatalog active;
        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyCollection<string> DuplicateIds => duplicateIds;
        public bool HasDuplicateIds => duplicateIds.Count > 0;
        public static ItemCatalog Active => active;

        private void OnEnable()
        {
            if (active == null)
            {
                active = this;
            }
        }

        private void OnDisable()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private void Awake() => Rebuild();
        public bool TryGet(string id, out ItemDefinition item)
        {
            item = null;
            if (byId.Count == 0) Rebuild();
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out item);
        }
        public bool TryGetByStableHash(int hash, out ItemDefinition item)
        {
            item = null;
            if (byId.Count == 0) Rebuild();
            foreach (KeyValuePair<string, ItemDefinition> pair in byId)
            {
                if (StableHash(pair.Key) == hash) { item = pair.Value; return true; }
            }
            return false;
        }
        public static int StableHash(string value)
        {
            unchecked { int hash = 23; for (int i=0; i<(value?.Length ?? 0); i++) hash = hash * 31 + value[i]; return hash; }
        }
        public void Rebuild()
        {
            byId.Clear();
            duplicateIds.Clear();
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                ItemDefinition item = items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                {
                    continue;
                }

                if (byId.ContainsKey(item.ItemId))
                {
                    duplicateIds.Add(item.ItemId);
                    continue;
                }

                byId.Add(item.ItemId, item);
            }
        }
    }
}
