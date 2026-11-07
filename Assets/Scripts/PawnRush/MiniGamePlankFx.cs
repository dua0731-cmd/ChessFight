using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R503: B 보수 중인 동관's moments, played by whoever sees them (pictures and sounds only, no game state).
    // One per station, it watches MgPlanks' own objects - the laid slots, the pile boards and the carried boards - so a
    // client plays the same moments from the state it gets (ArenaFx's rule), with no change to MgPlanks:
    //   pick    a board leaves a pile: a dust puff off the stack, sfx_plank_pick
    //   lay     a slot turns on: the plank thuds onto the joists (dust along its front edge, sawdust flakes) and two
    //           hammer strikes on the nails (sparks + puffs at 0.18 s and 0.5 s), sfx_plank_lay
    //   drop    a carried board vanishes without a lay (knocked over, fell): a clatter of dust and flakes where it was,
    //           sfx_plank_drop
    //   return  a pile grows back (the dropped plank is restacked after 5 s): a puff, sfx_plank_return
    //   quarter 25 / 50 / 75 % (IGateFx): warm motes rise out of the chasm along the laid bridge (no sound)
    //   ghost   while a pawn carrying a plank walks up to the bridge's front edge, a see-through glowing plank
    //           hovers over the slot the game would fill (MgPlanks.Lay's own column rule), brighter once the pawn
    //           is inside the laying zone
    //   opened  the gate's dust sheet goes up (IGateFx, instead of the shared beam): a dust cloud off the hem and the
    //           floor, sawdust shaken off the batten, sfx_dustsheet_open
    // Textures are the library's (T_Door_Dust / T_Door_Mote), flakes use the joist-wood material. Sounds go through
    // ArenaFx.Sfx (Resources/PawnRushMiniGameSfx/<name>; a missing file is warned about once and skipped).
    public sealed class MiniGamePlankFx : MonoBehaviour, IGateFx
    {
        const string Fx = "PawnRushMiniGames/Fx/";
        static readonly Color Dust = new Color(.8f, .72f, .6f), Warm = new Color(2.4f, 1.55f, .55f, 1f);
        static Material additive, blended, wood;
        static Texture2D dustTex, moteTex;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { additive = blended = wood = ghostMat = null; dustTex = moteTex = null; loaded = false; }

        sealed class Bit
        {
            public Transform T;
            public Renderer R;
            public int Kind;                 // 0 dust puff, 1 warm mote / spark, 2 wood flake
            public Vector3 From, Velocity, Spin;
            public float Start, Life, Size, Phase, Floor;
        }

        sealed class Carried { public Transform T; public Vector3 Last; }

        MissionStation station;
        MgPlanks game;
        Transform root;
        readonly List<Bit> bits = new List<Bit>();
        readonly Stack<Bit>[] spare = { new Stack<Bit>(), new Stack<Bit>(), new Stack<Bit>() };   // pooled per kind
        MaterialPropertyBlock block;
        readonly List<Transform> slots = new List<Transform>(), piles = new List<Transform>();
        readonly List<bool> slotOn = new List<bool>();
        readonly int[] pileCount = new int[2];
        readonly Dictionary<int, Carried> carried = new Dictionary<int, Carried>();
        readonly List<int> gone = new List<int>();
        readonly List<(float at, Vector3 where)> hammer = new List<(float, Vector3)>();
        bool primed;
        float clock;
        readonly Dictionary<int, Renderer[]> ghosts = new Dictionary<int, Renderer[]>();
        readonly Dictionary<int, float> ghostNear = new Dictionary<int, float>();
        static Material ghostMat;

        public void Configure(MissionStation owner, MgPlanks planks)
        {
            station = owner;
            game = planks;
            slots.Clear(); piles.Clear(); slotOn.Clear(); carried.Clear(); primed = false;
            DropGhosts();
            if (game == null) return;
            foreach (var t in game.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Plank (laid)") slots.Add(t);
                else if (t.name == "Pile plank") piles.Add(t);
            }
            foreach (var s in slots) slotOn.Add(s.gameObject.activeSelf);
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var a = Resources.Load<Material>("URPVariants/Particles Additive");
            var b = Resources.Load<Material>("URPVariants/Lit Transparent");
            dustTex = Resources.Load<Texture2D>(Fx + "T_Door_Dust");
            moteTex = Resources.Load<Texture2D>(Fx + "T_Door_Mote");
            wood = Resources.Load<Material>("PawnRushMiniGames/Materials/PR_Mid_JoistWood");
            if (a != null && b != null && dustTex != null && moteTex != null)
            {
                additive = new Material(a) { name = "Plank FX add" };
                blended = new Material(b) { name = "Plank FX dust" };
            }
            else Debug.LogWarning("[미니게임 모델] 판자 효과 재질·텍스처가 없어 그리지 않습니다: Resources/" + Fx);
        }

        // ---------------------------------------------------------------- watching the game
        void LateUpdate()
        {
            if (game == null || !Application.isPlaying) return;
            clock += Time.deltaTime;
            int laidNow = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                bool on = slots[i].gameObject.activeSelf;
                if (primed && on && !slotOn[i]) { Lay(slots[i]); laidNow = i; }
                slotOn[i] = on;
            }
            for (int p = 0; p < 2; p++)
            {
                int n = 0;
                Transform top = null;
                for (int i = p * MgPlanks.PerPile; i < (p + 1) * MgPlanks.PerPile && i < piles.Count; i++)
                    if (piles[i] != null && piles[i].gameObject.activeSelf) { n++; top = piles[i]; }
                var stack = piles.Count > p * MgPlanks.PerPile ? piles[p * MgPlanks.PerPile] : null;
                if (primed && stack != null)
                {
                    var at = (top != null ? top.position : stack.position) + Vector3.up * .15f;
                    if (n < pileCount[p]) Pick(at);
                    else if (n > pileCount[p]) Return(at);
                }
                pileCount[p] = n;
            }
            // carried boards: children MgPlanks makes and destroys; one that vanishes without a lay was dropped
            gone.Clear();
            foreach (var kv in carried)
                if (kv.Value.T == null) gone.Add(kv.Key);
                else kv.Value.Last = kv.Value.T.position;
            foreach (var id in gone)
            {
                if (primed && laidNow < 0) Drop(carried[id].Last);
                carried.Remove(id);
            }
            foreach (Transform c in game.transform)
                if (c.name == "Plank (carried)" && !carried.ContainsKey(c.GetInstanceID()))
                    carried[c.GetInstanceID()] = new Carried { T = c, Last = c.position };
            primed = true;
            Ghosts();
            for (int i = hammer.Count - 1; i >= 0; i--)
                if (clock >= hammer[i].at) { Strike(hammer[i].where); hammer.RemoveAt(i); }
            Animate();
        }

        // ---------------------------------------------------------------- placement preview
        // The pawn stands about 1.1 m under its board (MgPlanks.LateUpdate), so the board tells where the carrier is.
        void Ghosts()
        {
            ghostNear.Clear();
            int front = -1;
            for (int r = 0; r < MgPlanks.Rows && front < 0; r++)
                if (!Laid(r, 0) || !Laid(r, 1)) front = r;
            if (front >= 0)
            {
                float edge = MgPlanks.Chasm0 + front * MgPlanks.Row;
                foreach (var kv in carried)
                {
                    if (kv.Value.T == null) continue;
                    var at = game.transform.InverseTransformPoint(kv.Value.T.position) - Vector3.up * 1.1f;
                    // shown a little before the laying zone (edge - 1.6 .. edge + .4, |x| <= 3.2) so the pawn sees where to go
                    if (at.z < edge - 3.2f || at.z > edge + .6f || Mathf.Abs(at.x) > MgPlanks.PlankWidth + 1.2f || at.y < -1f || at.y > 2.5f) continue;
                    int col = Laid(front, 0) ? 1 : Laid(front, 1) ? 0 : at.x < 0f ? 0 : 1;
                    int slot = front * MgPlanks.Columns + col;
                    if (slot >= slots.Count || slots[slot] == null) continue;
                    bool inside = at.z > edge - 1.6f && at.z < edge + .4f && Mathf.Abs(at.x) <= MgPlanks.PlankWidth + .7f;
                    ghostNear[slot] = Mathf.Max(ghostNear.TryGetValue(slot, out var n) ? n : 0f, inside ? 1f : .45f);
                }
            }
            foreach (var kv in ghosts)
                if (!ghostNear.ContainsKey(kv.Key)) foreach (var r in kv.Value) if (r != null) r.enabled = false;
            if (ghostNear.Count == 0) return;
            if (block == null) block = new MaterialPropertyBlock();
            float pulse = .5f + .5f * Mathf.Sin(clock * 5f);
            foreach (var kv in ghostNear)
            {
                if (!ghosts.TryGetValue(kv.Key, out var rs)) ghosts[kv.Key] = rs = MakeGhost(slots[kv.Key]);
                if (rs.Length == 0 || rs[0] == null) continue;
                var c = new Color(1f, .82f, .48f, kv.Value * (.22f + .2f * pulse));
                var slot = slots[kv.Key];
                GhostRoot(rs[0]).transform.position = slot.position + slot.up * (.12f + .05f * pulse);
                foreach (var r in rs)
                {
                    if (r == null) continue;
                    r.enabled = true;
                    block.Clear(); block.SetColor("_BaseColor", c); r.SetPropertyBlock(block);
                }
            }
        }

        bool Laid(int row, int col)
        {
            int i = row * MgPlanks.Columns + col;
            return i < slots.Count && slots[i] != null && slots[i].gameObject.activeSelf;
        }

        static GameObject GhostRoot(Renderer r)
        {
            var t = r.transform;
            while (t.parent != null && t.name != "Plank (ghost)") t = t.parent;
            return t.gameObject;
        }

        void DropGhosts()
        {
            foreach (var g in ghosts.Values) if (g.Length > 0 && g[0] != null) Destroy(GhostRoot(g[0]));
            ghosts.Clear();
        }

        // A copy of the slot's plank (the walnut model when B's art is installed) with every behaviour and collider
        // stripped and one see-through warm material, so it cannot be stood on or mistaken for a laid plank.
        Renderer[] MakeGhost(Transform slot)
        {
            if (ghostMat == null)
            {
                var b = Resources.Load<Material>("URPVariants/Lit Transparent");
                if (b == null) return new Renderer[0];
                ghostMat = new Material(b) { name = "Plank ghost" };
                if (ghostMat.HasProperty("_BaseMap")) ghostMat.SetTexture("_BaseMap", null);
                if (ghostMat.HasProperty("_EmissionColor"))
                {
                    ghostMat.EnableKeyword("_EMISSION");
                    ghostMat.SetColor("_EmissionColor", new Color(.55f, .38f, .16f));
                }
            }
            bool was = slot.gameObject.activeSelf;
            slot.gameObject.SetActive(false);           // the copy is born inactive, so nothing on it wakes up
            var copy = Instantiate(slot.gameObject, slot.parent);
            slot.gameObject.SetActive(was);
            copy.name = "Plank (ghost)";
            foreach (var m in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(m);
            foreach (var col in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
            var keep = new List<Renderer>();
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;   // the bare box hidden under the dressed model stays hidden
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                keep.Add(r);
            }
            copy.SetActive(true);
            if (keep.Count == 0) { Destroy(copy); return new Renderer[0]; }
            return keep.ToArray();
        }

        // ---------------------------------------------------------------- moments
        void Lay(Transform slot)
        {
            var c = slot.position + slot.up * (slot.lossyScale.y * .5f);
            var right = slot.right * (slot.lossyScale.x * .5f);
            var fwd = slot.forward * (slot.lossyScale.z * .5f);
            ArenaFx.Sfx("sfx_plank_lay", c);
            for (int i = 0; i < 10; i++)
                Puff(c - fwd + right * Random.Range(-1f, 1f), new Vector3(0f, .35f, 0f) - slot.forward * Random.Range(.2f, .6f), 0f, Random.Range(.9f, 1.4f), Random.Range(.5f, .9f));
            for (int i = 0; i < 16; i++)
                Flake(c + right * Random.Range(-.9f, .9f) + fwd * Random.Range(-.8f, .8f),
                      new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.6f, 3f), Random.Range(-1.2f, 1.2f)), c.y);
            // two hammer strikes on two nails (the sound's two knocks)
            hammer.Add((clock + .18f, c + right * .8f - fwd * .7f));
            hammer.Add((clock + .5f, c - right * .8f - fwd * .7f));
        }

        void Strike(Vector3 at)
        {
            for (int i = 0; i < 6; i++)
                Mote(at, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1f, 2.4f), Random.Range(-1.5f, 1.5f)), 0f, Random.Range(.25f, .45f), Random.Range(.05f, .09f));
            Puff(at, Vector3.up * .25f, 0f, .7f, .35f);
        }

        void Pick(Vector3 at)
        {
            ArenaFx.Sfx("sfx_plank_pick", at, volume: .8f);
            for (int i = 0; i < 5; i++)
                Puff(at + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-.8f, .8f)), new Vector3(0f, .3f, 0f), 0f, Random.Range(.8f, 1.2f), Random.Range(.4f, .7f));
        }

        void Return(Vector3 at)
        {
            ArenaFx.Sfx("sfx_plank_return", at, volume: .7f);
            for (int i = 0; i < 4; i++)
                Puff(at + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-.8f, .8f)), new Vector3(0f, .25f, 0f), 0f, Random.Range(.8f, 1.1f), Random.Range(.4f, .6f));
        }

        void Drop(Vector3 at)
        {
            ArenaFx.Sfx("sfx_plank_drop", at);
            for (int i = 0; i < 7; i++)
                Puff(at + Random.insideUnitSphere * .6f, new Vector3(0f, .3f, 0f), 0f, Random.Range(.9f, 1.3f), Random.Range(.5f, .8f));
            for (int i = 0; i < 10; i++)
                Flake(at, new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 2.5f), Random.Range(-2f, 2f)), at.y - 1.2f);
        }

        public void Quarter(int quarter)
        {
            if (station == null || game == null) return;
            // warm motes rise out of the chasm along the bridge laid so far (u 6 .. 6 + 2 * rows)
            int rows = Mathf.Clamp(game.Laid / MgPlanks.Columns, 1, MgPlanks.Rows);
            var g = game.transform;
            // no sound at the quarters in any mini-game (user 11-07, "25 50 75 효과음은 하지마")
            for (int i = 0; i < 18 + 6 * quarter; i++)
            {
                var at = g.TransformPoint(new Vector3(Random.Range(-3.5f, 3.5f), -1.5f, MgPlanks.Chasm0 + Random.Range(0f, rows * MgPlanks.Row)));
                Mote(at, new Vector3(Random.Range(-.2f, .2f), Random.Range(1f, 1.8f), Random.Range(-.2f, .2f)), Random.Range(0f, .8f), Random.Range(1.6f, 2.4f), Random.Range(.08f, .15f));
            }
        }

        public void Opened()
        {
            if (station == null || station.Gate == null) return;
            var gate = station.Gate.transform;
            var right = gate.right * 2f;
            var front = -gate.forward;
            var bottom = gate.position - gate.up * (gate.lossyScale.y * .5f) + front * (gate.lossyScale.z * .5f + .1f);
            ArenaFx.Sfx("sfx_dustsheet_open", gate.position);
            for (int i = 0; i < 22; i++)
                Puff(bottom + right * Random.Range(-1.1f, 1.1f), front * Random.Range(.3f, .9f) + Vector3.up * Random.Range(.2f, .6f),
                     Random.Range(0f, .6f), Random.Range(1.6f, 2.6f), Random.Range(.9f, 1.6f));
            // sawdust shaken off the batten as the sheet rises, falling over the doorway
            for (int i = 0; i < 18; i++)
                Flake(bottom + right * Random.Range(-1f, 1f) + Vector3.up * Random.Range(3.6f, 4.4f),
                      new Vector3(Random.Range(-.6f, .6f), Random.Range(0f, .8f), 0f) + front * Random.Range(.2f, .8f), bottom.y, Random.Range(.2f, 1.2f));
            for (int i = 0; i < 14; i++)
                Mote(bottom + right * Random.Range(-1f, 1f) + Vector3.up * Random.Range(.5f, 3f), front * Random.Range(.3f, .7f) + Vector3.up * Random.Range(.1f, .4f),
                     Random.Range(.3f, 1.5f), Random.Range(1.4f, 2.2f), Random.Range(.1f, .18f));
        }

        // ---------------------------------------------------------------- particles (pooled quads and cubes)
        void Puff(Vector3 at, Vector3 v, float delay, float life, float size) => Add(0, at, v, delay, life, size, 0f);
        void Mote(Vector3 at, Vector3 v, float delay, float life, float size) => Add(1, at, v, delay, life, size, 0f);
        void Flake(Vector3 at, Vector3 v, float floor, float delay = 0f) => Add(2, at, v, delay, Random.Range(1.6f, 2.4f), Random.Range(.03f, .06f), floor);

        void Add(int kind, Vector3 at, Vector3 v, float delay, float life, float size, float floor)
        {
            Load();
            if (kind != 2 && (additive == null || blended == null)) return;
            if (root == null) root = new GameObject("Plank FX").transform;
            if (block == null) block = new MaterialPropertyBlock();
            Bit b = null;
            while (spare[kind].Count > 0 && b == null) { var s = spare[kind].Pop(); if (s.T != null) b = s; }
            if (b == null)
            {
                var go = GameObject.CreatePrimitive(kind == 2 ? PrimitiveType.Cube : PrimitiveType.Quad);
                Destroy(go.GetComponent<Collider>());
                go.name = kind == 0 ? "Dust" : kind == 1 ? "Mote" : "Sawdust";
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sharedMaterial = kind == 0 ? blended : kind == 1 ? additive : wood;
                if (kind != 2)
                {
                    block.Clear(); block.SetTexture("_BaseMap", kind == 0 ? dustTex : moteTex); block.SetColor("_BaseColor", Color.clear);
                    r.SetPropertyBlock(block);
                }
                b = new Bit { T = go.transform, R = r, Kind = kind };
            }
            b.From = at; b.Velocity = v; b.Start = clock + delay; b.Life = life; b.Size = size; b.Floor = floor;
            b.Phase = Random.value * 6.3f;
            b.Spin = new Vector3(Random.Range(-400f, 400f), Random.Range(-400f, 400f), Random.Range(-400f, 400f));
            b.R.enabled = false;
            bits.Add(b);
        }

        void Animate()
        {
            if (bits.Count == 0) return;
            var cam = Camera.main;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                if (b.T == null) { bits.RemoveAt(i); continue; }
                float t = clock - b.Start, k = t / b.Life;
                if (k < 0f) { b.R.enabled = false; continue; }
                if (k > 1f) { b.R.enabled = false; bits.RemoveAt(i); spare[b.Kind].Push(b); continue; }
                b.R.enabled = true;
                if (b.Kind == 2)
                {
                    // a sawdust flake: thrown, falls with gravity, rests on the floor and shrinks away
                    var p = b.From + b.Velocity * t + .5f * Physics.gravity * t * t;
                    bool rest = p.y <= b.Floor;
                    if (rest) p.y = b.Floor;
                    b.T.position = p;
                    if (!rest) b.T.rotation = Quaternion.Euler(b.Spin * t);
                    b.T.localScale = new Vector3(b.Size, b.Size * .35f, b.Size * .8f) * (k > .75f ? (1f - k) / .25f : 1f);
                    continue;
                }
                float ease = 1f - (1f - k) * (1f - k);
                b.T.position = b.From + b.Velocity * t + (b.Kind == 1 ? .5f * Physics.gravity * .25f * t * t * (b.Velocity.y > 1f ? 1f : 0f) : Vector3.zero);
                if (cam != null) b.T.rotation = Quaternion.LookRotation(b.T.position - cam.transform.position, cam.transform.up);
                Color c;
                if (b.Kind == 0)
                {
                    b.T.localScale = Vector3.one * b.Size * Mathf.Lerp(.5f, 1.9f, ease);
                    float a = (k < .2f ? k / .2f : (1f - k) / .8f) * .55f;
                    c = new Color(Dust.r, Dust.g, Dust.b, a);
                }
                else
                {
                    b.T.localScale = Vector3.one * b.Size;
                    float a = (k < .15f ? k / .15f : (1f - k) / .85f) * (.7f + .3f * Mathf.Sin(b.Phase + t * 9f));
                    c = new Color(Warm.r * a, Warm.g * a, Warm.b * a, a);    // premultiplied for the additive material
                }
                block.Clear(); b.R.GetPropertyBlock(block); block.SetColor("_BaseColor", c); b.R.SetPropertyBlock(block);
            }
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            DropGhosts();
        }
    }
}
