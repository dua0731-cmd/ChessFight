using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Draws a pawn's grappling hook (M5) from what the pawn reports, so a network puppet looks the same
    /// as the pawn its host simulates: the hook hanging from the right hand, whirling overhead while the
    /// gauge fills, flying, and stuck in the wall with the rope to the hands (taut while reeling in, slack
    /// after arriving). For the local player's own pawn, while it swings, a faint arc and a marker show
    /// where the throw would go with the gauge as it is - the same flight steps as the real throw.
    /// Added to every pawn by RagdollPawn.Awake. No colliders: purely a picture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PawnHookView : MonoBehaviour
    {
        /// <summary>The local player's aim for a pawn it controls, zero for any other pawn (LabGame sets it).</summary>
        public static Func<RagdollPawn, Vector3> LocalAim;

        const int ArcPoints = 60;
        const float ArcStep = 1f / 30f;
        static Material ropeMaterial, metalMaterial, arcMaterial, markerMaterial;

        RagdollPawn pawn;
        Transform hook, marker;
        LineRenderer rope, arc;
        float spin;
        readonly Vector3[] ropePoints = new Vector3[12];
        readonly Vector3[] arcPoints = new Vector3[ArcPoints];

        void Awake()
        {
            pawn = GetComponent<RagdollPawn>();
            MakeMaterials();
            hook = BuildHook().transform;
            rope = Line("Hook Rope", ropeMaterial, 0.025f);
            arc = Line("Hook Aim", arcMaterial, 0.035f);
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.name = "Hook Aim Marker";
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * 0.22f;
            dot.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
            marker = dot.transform;
            Hide();
        }

        void OnDestroy()
        {
            if (hook != null) Destroy(hook.gameObject);
        }

        void Hide()
        {
            hook.gameObject.SetActive(false);
            rope.enabled = arc.enabled = false;
            marker.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (pawn == null || pawn.bodies == null || pawn.bodies[0] == null) return;
            HookPhase phase = pawn.Hook;
            if (phase == HookPhase.None)
            {
                Hide();
                return;
            }
            Vector3 hand = pawn.handR != null ? pawn.handR.Center : pawn.bodies[(int)BodyId.HandR].position;
            Vector3 at;
            Vector3 point;   // which way the hook's shaft points
            int count = 2;
            switch (phase)
            {
                case HookPhase.Charging:
                    // A puppet has no swing angle on the wire: turn it here at the same rate.
                    spin = pawn.NetworkPuppet
                        ? Mathf.Repeat(spin + RagdollPawn.HookSpinRate(pawn.HookCharge) * Mathf.PI * 2f * Time.deltaTime, Mathf.PI * 2f)
                        : pawn.HookSpin;
                    at = pawn.HookSwingPoint(spin, pawn.HookCharge);
                    point = at - hand;
                    break;
                case HookPhase.Flying:
                case HookPhase.Pulling:
                    at = pawn.HookPoint;
                    point = at - hand;
                    break;
                case HookPhase.Stuck:
                    at = pawn.HookPoint;
                    point = at - hand;
                    count = Slack(hand, at);
                    break;
                default:   // held: hanging from the right hand
                    at = hand + Vector3.down * 0.32f;
                    point = Vector3.down;
                    break;
            }
            hook.gameObject.SetActive(true);
            hook.position = at;
            if (point.sqrMagnitude > 1e-6f) hook.rotation = Quaternion.FromToRotation(Vector3.up, point.normalized);
            rope.enabled = true;
            if (count == 2)
            {
                ropePoints[0] = hand;
                ropePoints[1] = at;
            }
            rope.positionCount = count;
            rope.SetPositions(ropePoints);
            DrawArc(phase);
        }

        /// <summary>A loose rope from the hand to a hook it is no longer pulling on: a shallow sag.</summary>
        int Slack(Vector3 from, Vector3 to)
        {
            int n = ropePoints.Length;
            float sag = Mathf.Min(1.2f, Vector3.Distance(from, to) * 0.12f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                ropePoints[i] = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            return n;
        }

        void DrawArc(HookPhase phase)
        {
            Vector3 aim = phase == HookPhase.Charging && LocalAim != null ? LocalAim(pawn) : Vector3.zero;
            if (aim.sqrMagnitude < 0.25f)
            {
                arc.enabled = false;
                marker.gameObject.SetActive(false);
                return;
            }
            var p = pawn.P;
            Transform hips = pawn.bodies[0].transform;
            RagdollPawn.HookLaunch(p, pawn.bodies[(int)BodyId.Head].position, aim, hips.forward, pawn.HookCharge,
                out Vector3 position, out Vector3 velocity);
            int n = 0;
            arcPoints[n++] = position;
            bool hit = false;
            for (float t = 0f; t < p.hookMaxFlight && n < ArcPoints; t += ArcStep)
            {
                hit = pawn.HookFlightStep(p, ref position, ref velocity, ArcStep, out _);
                arcPoints[n++] = position;
                if (hit) break;
            }
            arc.enabled = true;
            arc.positionCount = n;
            arc.SetPositions(arcPoints);
            marker.gameObject.SetActive(hit);
            if (hit) marker.position = position;
        }

        GameObject BuildHook()
        {
            // A shaft with two prongs bent back at the far end: reads as a grappling hook at any size.
            var root = new GameObject("Grappling Hook");
            Part(root.transform, new Vector3(0f, 0.09f, 0f), Quaternion.identity, new Vector3(0.035f, 0.09f, 0.035f));
            Part(root.transform, new Vector3(0.05f, 0.15f, 0f), Quaternion.Euler(0f, 0f, 50f), new Vector3(0.028f, 0.06f, 0.028f));
            Part(root.transform, new Vector3(-0.05f, 0.15f, 0f), Quaternion.Euler(0f, 0f, -50f), new Vector3(0.028f, 0.06f, 0.028f));
            Part(root.transform, new Vector3(0f, 0.15f, 0.05f), Quaternion.Euler(-50f, 0f, 0f), new Vector3(0.028f, 0.06f, 0.028f));
            return root;
        }

        static void Part(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = metalMaterial;
        }

        LineRenderer Line(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        static void MakeMaterials()
        {
            if (ropeMaterial != null) return;
            Shader unlit = Shader.Find("Sprites/Default");
            Shader lit = Shader.Find("Standard");
            ropeMaterial = new Material(unlit) { color = new Color(0.46f, 0.33f, 0.19f) };
            arcMaterial = new Material(unlit) { color = new Color(1f, 1f, 1f, 0.55f) };
            markerMaterial = new Material(lit != null ? lit : unlit) { color = new Color(1f, 0.85f, 0.3f) };
            metalMaterial = new Material(lit != null ? lit : unlit) { color = new Color(0.32f, 0.33f, 0.36f) };
            if (lit != null)
            {
                metalMaterial.SetFloat("_Metallic", 0.7f);
                metalMaterial.SetFloat("_Glossiness", 0.55f);
            }
        }
    }
}
