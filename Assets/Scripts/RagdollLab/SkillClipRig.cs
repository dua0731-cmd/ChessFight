using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A Mixamo animation clip read onto the pawn's eleven bodies (R190, the Sword Fight skills from Mixamo clips).
    /// <list type="bullet">
    /// <item>A hidden copy of the pawn's own Mixamo skeleton in its bind pose (the T-pose, from the skin's bind poses) is
    /// made a humanoid (<see cref="AvatarBuilder"/>), so any humanoid clip retargets onto it whatever character it was
    /// made on. Nothing is drawn: it has no renderer.</item>
    /// <item><see cref="Sample"/> puts it at a time of the clip (a playable graph evaluated by hand, no Animator
    /// update of its own); <see cref="PoseBodies"/> turns that pose into what the joints are asked: each body's
    /// rotation in its parent body, identity at rest, as RagdollPawn.PoseOverride takes it.</item>
    /// <item>The bodies are stubs on a rigid trunk (no elbow, no knee): an arm points from its shoulder to where the
    /// clip's hand is and a thigh from its hip to where the clip's ankle is, so a bent elbow or knee still puts the
    /// mitten and the foot where the clip has them. Spine and neck fold into the chest and head.</item>
    /// </list>
    /// Bodies of the rig are identity at rest (RagdollLabBuilder), so the rest frame is found from the hips' entry of
    /// <see cref="RagdollVisualSync"/>: the hips bone's turn against its body.
    /// </summary>
    public sealed class SkillClipRig : IDisposable
    {
        // Mixamo name (without its prefix) → Unity humanoid bone.
        static readonly string[] HumanMap =
        {
            "Hips", "Hips", "Spine", "Spine", "Spine1", "Chest", "Spine2", "UpperChest", "Neck", "Neck", "Head", "Head",
            "LeftShoulder", "LeftShoulder", "LeftArm", "LeftUpperArm", "LeftForeArm", "LeftLowerArm", "LeftHand", "LeftHand",
            "RightShoulder", "RightShoulder", "RightArm", "RightUpperArm", "RightForeArm", "RightLowerArm", "RightHand", "RightHand",
            "LeftUpLeg", "LeftUpperLeg", "LeftLeg", "LeftLowerLeg", "LeftFoot", "LeftFoot", "LeftToeBase", "LeftToes",
            "RightUpLeg", "RightUpperLeg", "RightLeg", "RightLowerLeg", "RightFoot", "RightFoot", "RightToeBase", "RightToes",
        };
        static readonly string[] Required =
        {
            "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot",
        };
        // RagdollVisualSync takes these out from under their Mixamo parents (the arm stubs hang off stretch helpers, the
        // hands off the pawn): their parents by name.
        static readonly Dictionary<string, string> MovedParent = new Dictionary<string, string>
        {
            { "LeftArm", "LeftShoulder" }, { "RightArm", "RightShoulder" }, { "LeftHand", "LeftForeArm" }, { "RightHand", "RightForeArm" },
        };

        readonly GameObject root;
        readonly Animator animator;
        readonly Avatar avatar;
        readonly Dictionary<string, Transform> bone = new Dictionary<string, Transform>();
        readonly Dictionary<string, Quaternion> bindRot = new Dictionary<string, Quaternion>();
        readonly Dictionary<string, Vector3> bindPos = new Dictionary<string, Vector3>();
        readonly Quaternion rest, restInv;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClip playing;

        /// <summary>Why it could not be made (null: it was).</summary>
        public string Problem { get; }
        public bool Ready => Problem == null;

        public SkillClipRig(RagdollPawn pawn)
        {
            var skin = pawn != null ? pawn.skin : null;
            var sync = pawn != null ? pawn.GetComponent<RagdollVisualSync>() : null;
            if (skin == null || skin.sharedMesh == null) { Problem = "no skin"; return; }
            Transform[] bones = skin.bones;
            Matrix4x4[] bind = skin.sharedMesh.bindposes;
            if (bones.Length != bind.Length) { Problem = "bones and bind poses differ"; return; }

            // The prefix ("mixamorig:", or "mixamorig1:" from some downloads) is whatever comes before Hips.
            string prefix = null;
            foreach (var b in bones)
                if (b != null && b.name.EndsWith("Hips", StringComparison.Ordinal)) { prefix = b.name.Substring(0, b.name.Length - 4); break; }
            if (prefix == null) { Problem = "no Hips bone"; return; }

            Quaternion? hipsOffset = null;
            if (sync != null)
                foreach (var e in sync.entries)
                    if (e.bone != null && e.driverA == (int)BodyId.Hips && e.driverB < 0 && e.bone.name == prefix + "Hips") hipsOffset = e.rotationOffset;
            if (hipsOffset == null) { Problem = "no hips entry in RagdollVisualSync"; return; }

            root = new GameObject("Skill clip rig (" + pawn.name + ")") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            var world = new Dictionary<string, Matrix4x4>();
            var made = new List<string>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || bone.ContainsKey(bones[i].name)) continue;
                string n = bones[i].name;
                bone[n] = new GameObject(n).transform;
                world[n] = bind[i].inverse;
                made.Add(n);
            }
            var parentOf = new Dictionary<string, string>();
            foreach (string n in made)
            {
                string bare = n.Substring(Math.Min(n.Length, prefix.Length));
                string p = MovedParent.TryGetValue(bare, out string moved) && bone.ContainsKey(prefix + moved) ? prefix + moved : null;
                if (p == null)
                {
                    Transform up = Array.Find(bones, b => b != null && b.name == n)?.parent;
                    while (up != null && !bone.ContainsKey(up.name)) up = up.parent;
                    p = up != null ? up.name : null;
                }
                parentOf[n] = p;
                bone[n].SetParent(p != null ? bone[p] : root.transform, false);
            }
            foreach (string n in made)
            {
                Matrix4x4 local = parentOf[n] != null ? world[parentOf[n]].inverse * world[n] : world[n];
                bone[n].localPosition = local.GetColumn(3);
                bone[n].localRotation = local.rotation;
                bone[n].localScale = local.lossyScale;
                bindRot[n] = world[n].rotation;
                bindPos[n] = world[n].GetColumn(3);
            }

            var human = new List<HumanBone>();
            var have = new HashSet<string>();
            for (int i = 0; i < HumanMap.Length; i += 2)
            {
                if (!bone.ContainsKey(prefix + HumanMap[i])) continue;
                human.Add(new HumanBone { boneName = prefix + HumanMap[i], humanName = HumanMap[i + 1], limit = new HumanLimit { useDefaultValues = true } });
                have.Add(HumanMap[i + 1]);
            }
            foreach (string r in Required)
                if (!have.Contains(r)) { Problem = "the skeleton has no " + r; return; }
            var skeleton = new List<SkeletonBone>
            {
                new SkeletonBone { name = root.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one },
            };
            foreach (string n in made)
                skeleton.Add(new SkeletonBone { name = n, position = bone[n].localPosition, rotation = bone[n].localRotation, scale = bone[n].localScale });
            var description = new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { Problem = "the humanoid avatar is not valid"; return; }
            avatar.name = root.name;
            animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root.SetActive(true);

            // The bodies' rest frame in the rig: hips bone = hips body * offset, and the hips body is identity at rest.
            rest = bindRot[prefix + "Hips"] * Quaternion.Inverse(hipsOffset.Value);
            restInv = Quaternion.Inverse(rest);
            Prefix = prefix;
        }

        string Prefix { get; } = "";

        /// <summary>Puts the rig at <paramref name="time"/> seconds into <paramref name="clip"/>.</summary>
        public bool Sample(AnimationClip clip, float time)
        {
            if (!Ready || clip == null) return false;
            if (clip != playing || !graph.IsValid())
            {
                if (graph.IsValid()) graph.Destroy();
                graph = PlayableGraph.Create("Skill clip");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "pose", animator);
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                output.SetSourcePlayable(playable);
                graph.Play();
                playing = clip;
            }
            playable.SetTime(Mathf.Clamp(time, 0f, clip.length));
            graph.Evaluate(0f);
            return true;
        }

        /// <summary>A bone's turn from its bind pose now, in the bodies' rest frame (x right, y up, z ahead).</summary>
        Quaternion Delta(string name)
        {
            string n = Prefix + name;
            return restInv * bone[n].rotation * Quaternion.Inverse(bindRot[n]) * rest;
        }

        /// <summary>The turn that takes the line from bone <paramref name="a"/> to bone <paramref name="b"/> at rest to
        /// where it is now, in the rest frame (no twist about it).</summary>
        Quaternion Swing(string a, string b)
        {
            Vector3 was = restInv * (bindPos[Prefix + b] - bindPos[Prefix + a]);
            Vector3 now = restInv * (bone[Prefix + b].position - bone[Prefix + a].position);
            return Quaternion.FromToRotation(was, now);
        }

        /// <summary>
        /// The pose sampled last, as each body's rotation in its parent body (identity at rest, RagdollPawn.ParentOf),
        /// into <paramref name="local"/> (RagdollPawn.Count long; the hips' place is left alone). Also the hips' turn
        /// against the rest frame: <paramref name="turn"/> + toward the right (the sword hand), <paramref name="lean"/>
        /// + forward, in degrees.
        /// </summary>
        public void PoseBodies(Quaternion[] local, out float turn, out float lean)
        {
            turn = lean = 0f;
            if (!Ready || local == null || local.Length < RagdollPawn.Count) return;
            Quaternion hips = Delta("Hips"), chest = Delta("Spine2"), head = Delta("Head");
            Quaternion armL = Swing("LeftArm", "LeftHand"), armR = Swing("RightArm", "RightHand");
            Quaternion thighL = Swing("LeftUpLeg", "LeftFoot"), thighR = Swing("RightUpLeg", "RightFoot");
            local[(int)BodyId.Chest] = Quaternion.Inverse(hips) * chest;
            local[(int)BodyId.Head] = Quaternion.Inverse(chest) * head;
            local[(int)BodyId.ArmL] = Quaternion.Inverse(chest) * armL;
            local[(int)BodyId.HandL] = Quaternion.Inverse(armL) * Delta("LeftHand");
            local[(int)BodyId.ArmR] = Quaternion.Inverse(chest) * armR;
            local[(int)BodyId.HandR] = Quaternion.Inverse(armR) * Delta("RightHand");
            local[(int)BodyId.ThighL] = Quaternion.Inverse(hips) * thighL;
            local[(int)BodyId.FootL] = Quaternion.Inverse(thighL) * Delta("LeftFoot");
            local[(int)BodyId.ThighR] = Quaternion.Inverse(hips) * thighR;
            local[(int)BodyId.FootR] = Quaternion.Inverse(thighR) * Delta("RightFoot");
            Vector3 ahead = hips * Vector3.forward;
            Vector3 flat = new Vector3(ahead.x, 0f, ahead.z);
            if (flat.sqrMagnitude > 1e-6f) turn = Vector3.SignedAngle(Vector3.forward, flat, Vector3.up);
            lean = -Mathf.Asin(Mathf.Clamp(ahead.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (avatar != null) UnityEngine.Object.Destroy(avatar);
        }
    }
}
