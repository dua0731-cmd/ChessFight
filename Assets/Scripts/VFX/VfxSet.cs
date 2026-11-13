using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A piece's effects by name for game code (R159, 10-27): Resources/VfxSets/&lt;Piece&gt;.asset lists the prefabs of
    /// Assets/VFX/Prefabs/&lt;Piece&gt;/ under the names a skill asks for ("F_Guard", "E_Land" ...). A prefab moved or
    /// renamed in the folder stays listed (the reference is by GUID); a new one is added here to be used by a skill.
    /// </summary>
    [CreateAssetMenu(menuName = "ChessFight/VFX Set", fileName = "VfxSet")]
    public sealed class VfxSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();

        static readonly Dictionary<string, VfxSet> loaded = new Dictionary<string, VfxSet>();

        /// <summary>Resources/VfxSets/&lt;name&gt;, loaded once (null if there is none).</summary>
        public static VfxSet Load(string name)
        {
            if (!loaded.TryGetValue(name, out var set) || set == null)
            {
                set = Resources.Load<VfxSet>("VfxSets/" + name);
                loaded[name] = set;
            }
            return set;
        }

        public GameObject Find(string key)
        {
            foreach (var e in entries)
                if (e.key == key) return e.prefab;
            return null;
        }

        /// <summary>Plays the effect named <paramref name="key"/> at a place (see <see cref="VfxCue.Spawn"/>); null if
        /// the set has no such effect.</summary>
        public GameObject Play(string key, Vector3 at, Quaternion facing, Transform parent = null, Vector3? target = null)
        {
            var prefab = Find(key);
            return prefab != null ? VfxCue.Spawn(prefab, at, facing, parent, target) : null;
        }
    }
}
