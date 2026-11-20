using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A map crown for the ultimate gauge (JY-skill-lab, 2026-11-21; Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 7.4: "맵상의 왕관
    /// 몇 개를 주우면 조금씩 차고"): a small gold crown turning over the floor. A piece that touches it with room in its
    /// gauge takes it (10%, RagdollPawn.TakeCrown), and it is back in its place 30 s later. Where crowns stand is the
    /// course's to say (a section 4 to 6, not all in one place); the skill test scene puts six round its start
    /// (PawnRushSkillBed). The host decides who takes one; every PC places the same crowns in the same order, and is told
    /// a crown's number when it is taken (SkillFxKind.KitCrown). Not seen in Unity by anyone yet.
    /// </summary>
    public class PawnRushGaugeCrown : MonoBehaviour
    {
        public static readonly List<PawnRushGaugeCrown> All = new List<PawnRushGaugeCrown>();
        static Material gold;

        public int Index { get; private set; }
        public bool Waiting => hiddenLeft > 0f;
        public float WaitLeft => Mathf.Max(0f, hiddenLeft);

        Vector3 floor;
        float hiddenLeft, spin;
        Transform look;

        /// <summary>A crown over <paramref name="floorPoint"/>, numbered <paramref name="index"/> (the same on every PC).</summary>
        public static PawnRushGaugeCrown Place(int index, Vector3 floorPoint)
        {
            var go = new GameObject("Gauge crown " + index);
            var c = go.AddComponent<PawnRushGaugeCrown>();
            c.Index = index;
            c.floor = floorPoint;
            go.transform.position = floorPoint + Vector3.up * 0.9f;
            c.Build();
            All.Add(c);
            return c;
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (All[i] != null) Destroy(All[i].gameObject);
            All.Clear();
        }

        public static PawnRushGaugeCrown Find(int index)
        {
            foreach (var c in All)
                if (c != null && c.Index == index) return c;
            return null;
        }

        /// <summary>Taken (a client is told): gone for <paramref name="seconds"/>.</summary>
        public void Take(float seconds)
        {
            hiddenLeft = Mathf.Max(0.1f, seconds);
            if (look != null) look.gameObject.SetActive(false);
        }

        static Material Gold()
        {
            if (gold != null) return gold;
            var lit = LitMaterials.Lit;
            gold = lit != null ? LitMaterials.NewLit(new Color(1f, 0.78f, 0.22f), 0.8f, 0.6f)
                               : new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.78f, 0.22f) };
            gold.name = "Gauge crown gold";
            return gold;
        }

        void Part(PrimitiveType type, Vector3 at, Vector3 scale, Quaternion turn)
        {
            var p = GameObject.CreatePrimitive(type);
            var col = p.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);   // a picture: the host takes it by distance
            p.transform.SetParent(look, false);
            p.transform.localPosition = at;
            p.transform.localRotation = turn;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Gold();
        }

        void Build()
        {
            look = new GameObject("Crown").transform;
            look.SetParent(transform, false);
            // A band and five points round its top.
            Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.42f, 0.06f, 0.42f), Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Vector3 at = new Vector3(Mathf.Cos(a) * 0.17f, 0.12f, Mathf.Sin(a) * 0.17f);
                Part(PrimitiveType.Cube, at, new Vector3(0.08f, 0.16f, 0.08f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 45f));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (hiddenLeft > 0f)
            {
                hiddenLeft -= dt;
                if (hiddenLeft <= 0f && look != null) look.gameObject.SetActive(true);
                return;
            }
            spin += dt * 90f;
            if (look != null)
            {
                look.localRotation = Quaternion.Euler(0f, spin, 0f);
                look.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.4f + Index) * 0.08f);
            }
            // The host's: the first piece that touches it with room in its gauge takes it.
            foreach (var p in RagdollPawn.All)
            {
                if (p == null || p.NetworkPuppet || p.PawnRushSkills == null) continue;
                float reach = p.PawnRushSkills.crownReach;
                if ((p.Hips.position - transform.position).sqrMagnitude > reach * reach) continue;
                if (!p.TakeCrown(this)) continue;
                Take(p.PawnRushSkills.crownRespawn);
                break;
            }
        }

        void OnDestroy() => All.Remove(this);
    }
}
