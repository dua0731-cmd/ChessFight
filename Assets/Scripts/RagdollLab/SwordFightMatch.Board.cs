using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The prototype match's board (spec §5, §9): the platform sized to 8 × 8 squares and dressed as a chessboard
    /// (ChessFight/Skill Ground, laid out in the world, so the floor round a hole keeps its squares), and the ghost pieces:
    /// from 2:00, every 30 s, a see-through piece glides over its squares while they blink for 1.5 s, then they fall
    /// for good (SwordFightEdgeFloor.Erase). Every PC works the squares out from the match's seed and its clock.
    /// Also the respawn rings, the steal's light pillar and the respawn protection's flicker.
    /// </summary>
    public sealed partial class SwordFightMatch
    {
        List<SwordGhost> plan;
        int planSeed;
        bool[] warned = new bool[4], dropped = new bool[4];
        readonly List<GameObject> warnings = new List<GameObject>();
        GameObject ghostPiece;
        int ghostWave = -1;
        Material boardMat, glowMat;
        readonly bool[] erased = new bool[SwordBoard.Count];

        bool[] ErasedNow => erased;
        bool QueenDone => dropped[3];

        void SizeBoard(float cell)
        {
            Cell = cell;
            var platform = floor.Platform;
            if (platform == null) { Debug.LogWarning("[SwordFightMatch] no platform found under the scene's centre"); return; }
            float size = SwordBoard.Size * cell;
            var scale = platform.localScale;
            platform.localScale = new Vector3(size, scale.y, size);
            platform.position = new Vector3(0f, platform.position.y, 0f);
            var renderer = platform.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (boardMat == null)
                {
                    var shader = Shader.Find("ChessFight/Skill Ground");
                    if (shader == null) shader = Resources.Load<Shader>("QueenHillSkillFx/SkillGround");
                    if (shader != null) boardMat = new Material(shader) { name = "Sword Fight board" };
                }
                if (boardMat != null)
                {
                    boardMat.SetFloat("_Board", 1f);
                    boardMat.SetFloat("_Square", cell);
                    boardMat.SetVector("_Origin", new Vector4(-size * 0.5f, -size * 0.5f, 0f, 0f));
                    renderer.sharedMaterial = boardMat;
                }
            }
            Physics.SyncTransforms();
            floor.Refind();
        }

        void ResetBoard()
        {
            plan = null;
            planSeed = 0;
            for (int i = 0; i < 4; i++) warned[i] = dropped[i] = false;
            for (int c = 0; c < erased.Length; c++) erased[c] = false;
            ClearWarnings();
            if (floor != null) floor.ClearAll();
        }

        void StepBoard(double clock, int seed)
        {
            if (seed == 0) return;
            if (plan == null || planSeed != seed)
            {
                ResetBoard();
                plan = SwordGhosts.Plan(seed);
                planSeed = seed;
            }
            var phase = view.Phase;
            if (phase == SwordPhase.Pick || phase == SwordPhase.Countdown) return;
            for (int i = 0; i < plan.Count; i++)
            {
                var g = plan[i];
                if (!warned[i] && clock >= g.At)
                {
                    warned[i] = true;
                    if (clock < g.DropAt && g.Cells.Length > 0) BeginWarning(i, g);
                }
                if (!dropped[i] && clock >= g.DropAt)
                {
                    dropped[i] = true;
                    if (ghostWave == i) ClearWarnings();
                    foreach (int c in g.Cells)
                    {
                        erased[c] = true;
                        floor.Erase(CellRect(c), game.Authority);
                    }
                }
            }
            if (ghostWave >= 0 && ghostWave < plan.Count) StepWarning(plan[ghostWave], (float)(clock - plan[ghostWave].At));
        }

        void BeginWarning(int wave, SwordGhost g)
        {
            ClearWarnings();
            ghostWave = wave;
            foreach (int c in g.Cells)
            {
                var q = Flat("Ghost warning", CellCentre(c) + Vector3.up * 0.02f, Cell * 0.94f, new Color(1f, 0.25f, 0.2f, 0.5f));
                warnings.Add(q);
            }
            // The see-through ghost piece (its model where there is one, else a plain column).
            var prefab = Resources.Load<GameObject>("PieceModels/" + g.Piece);
            ghostPiece = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghostPiece.name = "Ghost " + g.Piece;
            foreach (var col in ghostPiece.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in ghostPiece.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (var r in ghostPiece.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = GlowMat();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var bounds = new Bounds(ghostPiece.transform.position, Vector3.zero);
            foreach (var r in ghostPiece.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float h = Mathf.Max(0.1f, bounds.size.y);
            ghostPiece.transform.localScale *= Cell * 1.1f / h;
            SetColor(ghostPiece, new Color(0.75f, 0.85f, 1f, 0.45f));
        }

        void StepWarning(SwordGhost g, float age)
        {
            float k = Mathf.Clamp01(age / (float)SwordGhost.Warning);
            float blink = 0.25f + 0.45f * (0.5f + 0.5f * Mathf.Sin(age * Mathf.PI * 2f * 4f));
            foreach (var w in warnings) if (w != null) SetColor(w, new Color(1f, 0.3f, 0.15f, blink));
            if (ghostPiece == null || g.Cells.Length == 0) return;
            // Glide along the squares in their order.
            float along = k * (g.Cells.Length - 1);
            int a = Mathf.Clamp(Mathf.FloorToInt(along), 0, g.Cells.Length - 1), b = Mathf.Min(a + 1, g.Cells.Length - 1);
            Vector3 p = Vector3.Lerp(CellCentre(g.Cells[a]), CellCentre(g.Cells[b]), along - a);
            ghostPiece.transform.position = p + Vector3.up * (0.1f + 0.08f * Mathf.Sin(age * 6f));
            Vector3 d = CellCentre(g.Cells[b]) - CellCentre(g.Cells[a]);
            if (d.sqrMagnitude > 1e-4f) ghostPiece.transform.rotation = Quaternion.LookRotation(d.normalized);
        }

        void ClearWarnings()
        {
            foreach (var w in warnings) if (w != null) Destroy(w);
            warnings.Clear();
            if (ghostPiece != null) Destroy(ghostPiece);
            ghostPiece = null;
            ghostWave = -1;
        }

        Material GlowMat()
        {
            if (glowMat == null) glowMat = new Material(Shader.Find("Sprites/Default")) { name = "Sword Fight glow", renderQueue = 3100 };
            return glowMat;
        }

        static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        static void SetColor(GameObject go, Color c)
        {
            block.SetColor("_Color", c);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }

        /// <summary>A flat see-through square (or a ring) lying on the floor.</summary>
        GameObject Flat(string name, Vector3 at, float size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = GlowMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(go, color);
            return go;
        }

        void DestroyBoardPictures()
        {
            ClearWarnings();
            foreach (var r in rings.Values) if (r != null) Destroy(r);
            rings.Clear();
            foreach (var p in pillars) if (p.go != null) Destroy(p.go);
            pillars.Clear();
            if (boardMat != null) Destroy(boardMat);
            if (glowMat != null) Destroy(glowMat);
        }

        // ---------------------------------------------------------------- respawn rings, steal pillars, protection

        readonly Dictionary<ulong, GameObject> rings = new Dictionary<ulong, GameObject>();
        struct Pillar { public GameObject go; public float until; public Transform follow; }
        readonly List<Pillar> pillars = new List<Pillar>();
        readonly Dictionary<SwordFightPawn, List<Renderer>> flicker = new Dictionary<SwordFightPawn, List<Renderer>>();

        void StepPictures()
        {
            // A light ring on the square a piece is about to come back on (spec §11: 0.8 s, then the pawn).
            var seen = new HashSet<ulong>();
            foreach (var f in view.Fighters)
            {
                if (f.Alive || f.SpawnCell < 0 || f.SpawnCell >= SwordBoard.Count) continue;
                seen.Add(f.Id);
                if (!rings.TryGetValue(f.Id, out var ring) || ring == null)
                    rings[f.Id] = ring = Flat("Respawn ring", CellCentre(f.SpawnCell) + Vector3.up * 0.03f, Cell * 0.8f, new Color(1f, 0.9f, 0.5f, 0.6f));
                ring.transform.position = CellCentre(f.SpawnCell) + Vector3.up * 0.03f;
                float pulse = 0.4f + 0.4f * Mathf.Sin(Time.time * 14f);
                SetColor(ring, new Color(1f, 0.9f, 0.5f, pulse));
            }
            var gone = new List<ulong>();
            foreach (var pair in rings) if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (var id in gone) { if (rings[id] != null) Destroy(rings[id]); rings.Remove(id); }

            for (int i = pillars.Count - 1; i >= 0; i--)
            {
                var p = pillars[i];
                float left = p.until - Time.time;
                if (left <= 0f || p.go == null) { if (p.go != null) Destroy(p.go); pillars.RemoveAt(i); continue; }
                if (p.follow != null) p.go.transform.position = new Vector3(p.follow.position.x, (floor.Found ? floor.Top : 0f) + 2f, p.follow.position.z);
                SetColor(p.go, new Color(1f, 0.82f, 0.25f, 0.55f * Mathf.Clamp01(left / 0.3f)));
            }
        }

        void StealPillar(SwordFightPawn at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Steal pillar";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.9f, 2f, 0.9f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = GlowMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pillars.Add(new Pillar { go = go, until = Time.time + 0.8f, follow = at != null ? at.Pawn.Hips.transform : null });
        }

        /// <summary>The respawn protection (1.25 s, spec §8.4): the piece flickers (stands in for see-through).</summary>
        void StepProtection()
        {
            foreach (var f in game.Fighters.Values)
            {
                if (f == null) continue;
                bool on = f.Alive && f.Protection > 0f;
                flicker.TryGetValue(f, out var list);
                if (on && list == null)
                {
                    list = new List<Renderer>();
                    foreach (var r in f.Pawn.GetComponentsInChildren<Renderer>()) if (r.enabled) list.Add(r);
                    flicker[f] = list;
                }
                if (list == null) continue;
                bool show = !on || Mathf.Repeat(Time.time * 12f, 2f) < 1f;
                foreach (var r in list) if (r != null) r.enabled = show && f.Alive;
                if (!on)
                {
                    if (f.Alive) foreach (var r in list) if (r != null) r.enabled = true;
                    flicker.Remove(f);
                }
            }
        }
    }
}
