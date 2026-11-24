using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R830 (승규 님 11-24: "칼베기 잔상 효과를 넣어줘 흰팀은 흰색 흑팀은 흑팀 플레이어pc에서 보여야 하고 흰팀이 흑팀을 볼 때
    /// 빨간색, 흑팀이 백팀을 볼 때 빨간색으로 처리 해줘"): the afterimage of a sword cut - a ribbon swept by the blade from a
    /// little above the guard to the tip over the last <see cref="Life"/> seconds, fading with age and toward the hilt.
    /// Its colour is chosen on each screen: my own side's cuts (mine included) in my side's colour - white on a white
    /// player's PC, black on a black player's - and the other side's in red. With nobody to look from (a film, a test
    /// bed without a player, a spectator) every cut is in its own side's colour.
    /// Drawn on every machine from the sword as it is posed there (after RagdollVisualSync, order 100), while
    /// <see cref="SwordFightPawn.Slashing"/>: nothing is sent.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public sealed class SwordSlashTrail : MonoBehaviour
    {
        /// <summary>How long a stretch of the ribbon stays (s): the blade's pass is 0.18 s, so most of it shows. R831 (11-24, "칼날 효과 조금만 더 잘 보이게"): 0.1 → 0.15, wider (from 0.3 → 0.22 m), denser, fading more slowly.</summary>
        public const float Life = .15f;
        /// <summary>Where on the blade the ribbon runs, metres along it from the grip (times the sword model's size).</summary>
        public const float From = .22f, To = .95f;
        /// <summary>Steps drawn between two frames' blades (turned about the grip, so a fast cut is a curve, not chords).</summary>
        const int Between = 3;

        public static readonly Color White = new Color(1f, 1f, 1f, .95f);
        public static readonly Color Black = new Color(.02f, .02f, .03f, .92f);
        /// <summary>The other side's: the kit's foe red (SkillTeamTint, #D9473F).</summary>
        public static readonly Color Foe = new Color(1f, .18f, .13f, .95f);

        struct Sample
        {
            public Vector3 grip, way;
            public float length, time;
        }

        static Material material;
        static bool materialTried;

        readonly List<Sample> samples = new List<Sample>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();
        SwordFightPawn fighter;
        GameObject holder;
        Mesh mesh;
        MeshRenderer view;

        /// <summary>How many cuts' ribbons have been drawn (the checks).</summary>
        public int Slashes { get; private set; }

        static Material Shared()
        {
            if (materialTried) return material;
            materialTried = true;
            var shader = Shader.Find("ChessFight/Sword Slash");
            if (shader == null) shader = Resources.Load<Shader>("SwordFightSkillFx/SwordSlash");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return material;
        }

        void Awake() => fighter = GetComponent<SwordFightPawn>();

        void OnDestroy()
        {
            if (holder != null) Destroy(holder);
            if (mesh != null) Destroy(mesh);
        }

        void OnDisable()
        {
            samples.Clear();
            if (view != null) view.enabled = false;
        }

        /// <summary>The colour <paramref name="owner"/>'s cuts are drawn in on this screen.</summary>
        public static Color ColorFor(RagdollPawn owner)
        {
            var viewer = SwordFightSkillSfx.ViewerPawn;
            if (owner == null) return White;
            if (viewer != null && viewer != owner && viewer.Team != Teams.None && owner.Team != Teams.None && viewer.Team != owner.Team)
                return Foe;
            return owner.Team == Teams.Black ? Black : White;
        }

        // R832 (승규 님 11-24: "칼 베는 띠도 모든 플레이어가 볼 수 있게 해"): on a machine that only sees this fighter (every
        // client for everyone, the host for nobody), the ribbon does not wait on SwordFightPawn.Slashing alone: it also
        // goes by the sword as drawn here - turning against the hips faster than SeenRate degrees a second (a cut; walking
        // and the guard turn it far slower), held on SeenHold seconds so a snapshot's still frame does not break it.
        // Nothing is sent: every screen draws every fighter's cuts from its own picture.
        const float SeenRate = 240f, SeenHold = .08f;
        Quaternion lastSeen = Quaternion.identity;
        bool seenPrimed;
        float seenTurning;

        bool SeenSlashing()
        {
            // Not while it is knocked down (a falling body flings its sword round) or its sword is out of its hand.
            if (fighter.Authority || fighter.SwordAway || fighter.Pawn.Hips == null || fighter.Pawn.State != PawnState.Active) { seenPrimed = false; seenTurning = 0f; return false; }
            Quaternion turn = Quaternion.Inverse(fighter.Pawn.Hips.rotation) * fighter.SwordTransform.rotation;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (seenPrimed && Quaternion.Angle(lastSeen, turn) / dt > SeenRate) seenTurning = SeenHold;
            else seenTurning = Mathf.Max(0f, seenTurning - dt);
            lastSeen = turn; seenPrimed = true;
            return seenTurning > 0f;
        }

        void LateUpdate()
        {
            if (fighter == null || fighter.Pawn == null || fighter.SwordTransform == null) return;
            float now = Time.time;
            for (int i = samples.Count - 1; i >= 0; i--)
                if (now - samples[i].time > Life) samples.RemoveAt(i);
            if (!fighter.Alive) { samples.Clear(); seenTurning = 0f; }
            else if (fighter.Slashing || SeenSlashing())
            {
                var s = fighter.SwordTransform;
                float size = fighter.SwordModel != null ? Mathf.Clamp(fighter.SwordModelScale, .8f, 1.3f) : 1f;
                if (samples.Count == 0 || now - samples[samples.Count - 1].time > Life) Slashes++;
                samples.Add(new Sample { grip = s.position, way = s.forward, length = size, time = now });
            }
            Draw(now);
        }

        void Draw(float now)
        {
            if (samples.Count < 2 || Shared() == null)
            {
                if (view != null) view.enabled = false;
                return;
            }
            if (holder == null)
            {
                // In the world, not under the pawn: the ribbon stays where the blade went as the body moves on.
                holder = new GameObject("Sword slash trail");
                mesh = new Mesh { name = "Sword slash trail" };
                mesh.MarkDynamic();
                holder.AddComponent<MeshFilter>().sharedMesh = mesh;
                view = holder.AddComponent<MeshRenderer>();
                view.sharedMaterial = material;
                view.shadowCastingMode = ShadowCastingMode.Off;
                view.receiveShadows = false;
                view.lightProbeUsage = LightProbeUsage.Off;
                view.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            verts.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
            Color tint = ColorFor(fighter.Pawn);
            for (int i = 0; i < samples.Count; i++)
            {
                var a = samples[i];
                int steps = i == samples.Count - 1 ? 1 : Between;
                var b = i < samples.Count - 1 ? samples[i + 1] : a;
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    Vector3 grip = Vector3.Lerp(a.grip, b.grip, t);
                    Vector3 way = Vector3.Slerp(a.way, b.way, t);
                    float length = Mathf.Lerp(a.length, b.length, t);
                    float age = Mathf.Clamp01((now - Mathf.Lerp(a.time, b.time, t)) / Life);
                    // Fading out with age, faster at its very end; the newest stretch at the blade is solid.
                    float fade = Mathf.Pow(1f - age, 1.4f);
                    var c = new Color(tint.r, tint.g, tint.b, tint.a * fade);
                    verts.Add(grip + way * (From * length));
                    verts.Add(grip + way * (To * length));
                    colors.Add(c); colors.Add(c);
                    uvs.Add(new Vector2(age, 0f)); uvs.Add(new Vector2(age, 1f));
                }
            }
            for (int i = 0; i + 3 < verts.Count; i += 2)
            {
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            view.enabled = true;
        }
    }
}
