using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Which Mixamo clip each Sword Fight skill's body plays (R190), kept at Resources/SwordFightClips.asset. The editor
    /// menu ChessFight/Sword Fight/Skill Clips/Fill From Mixamo Folder makes and fills it from the clips in
    /// Assets/Art/SwordFight/Mixamo (named &lt;Piece&gt;_F or &lt;Piece&gt;_E, e.g. King_F.fbx). A skill with no clip here
    /// keeps its motion in code (SwordFightSkills.Motion), so the asset can be filled one skill at a time.
    /// </summary>
    [CreateAssetMenu(menuName = "ChessFight/Sword Fight/Skill Clip Set", fileName = "SwordFightClips")]
    public class SwordFightClipSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public PieceKind piece = PieceKind.King;
            [Tooltip("Off: the F skill. On: the edge skill (E).")]
            public bool edge;
            public AnimationClip clip;
            [Tooltip("Four times in the clip (s): the windup plays mark 0 → 1, the release (dash, flight, cut) 1 → 2, the recovery 2 → 3. Put mark 1 where the clip's blow starts. Empty: the clip in even thirds.")]
            public float[] marks = Array.Empty<float>();
            [Tooltip("How much of the clip the body takes (0: the code motion only).")]
            [Range(0f, 1f)] public float weight = 1f;
            [Tooltip("Share of the clip in the chest, the head, the free (left) arm, the legs, and the whole-body turn and lean. A share below 1 keeps some of the code motion, which aims the head and the free hand at what the skill is about.")]
            [Range(0f, 1f)] public float chest = 1f, head = 0.5f, freeArm = 0.7f, legs = 1f, turn = 0.6f;
            [Tooltip("Seconds the clip takes to come in at the skill's start.")]
            public float blendIn = 0.08f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        /// <summary>The entry with a clip for <paramref name="piece"/>'s F skill or edge skill, or null.</summary>
        public Entry Find(PieceKind piece, bool edge)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (e != null && e.clip != null && e.piece == piece && e.edge == edge && e.weight > 0f) return e;
            return null;
        }

        static SwordFightClipSet current;
        static bool looked;

        /// <summary>Resources/SwordFightClips, or null while there is none.</summary>
        public static SwordFightClipSet Current
        {
            get
            {
                if (!looked) { current = Resources.Load<SwordFightClipSet>("SwordFightClips"); looked = true; }
                return current;
            }
        }

        /// <summary>Look again (the editor menu calls it after filling the asset).</summary>
        public static void Forget() { looked = false; current = null; }
    }
}
