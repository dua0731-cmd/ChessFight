using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The king's 왕의 부름 (R146, RagdollPawn.KingCall.cs), drawn from its moments on every PC:
    /// - KingCall: over the king's head a gold crown (his piece's crown, the Queen of the Hill sceptre's) rises with a shaft
    ///   of light standing up from it, for as long as he calls; the square each one called will come to shows faintly.
    /// - KingCallGo: the one who answered glows in the king's colour as it rises and comes in, like Overwatch's nano boost
    ///   (승규 님 10-15: "팀원이 올 0.6초 동안 올 때 오버워치에서 나노강화제 받는 이펙트 처럼 몸이 킹이 쓰는 색으로 변하면서
    ///   왔으면 좋겠어"): a flash as it takes the boost, then its body turned his colour with a bright edge and bands running up
    ///   it, gold flecks coming off it, a gold line along the floor behind it, and through any wall it passes its glowing
    ///   outline (Skill Nano's pass behind what stands in front). Its square lights up.
    /// - KingCallLand: the square flares and fades, a little dust where it sets down; the glow drains off it.
    /// - KingCallEnd: its square goes out.
    /// No rings or discs (승규 님: nothing round at hits or landings).
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Shader nanoShader, beamShader;
        Material matBeam;
        readonly Dictionary<Material, Material> nanoOf = new Dictionary<Material, Material>();
        readonly Dictionary<RagdollPawn, NanoGlow> nanoGlows = new Dictionary<RagdollPawn, NanoGlow>();
        readonly Dictionary<RagdollPawn, KingSquare> kingSquares = new Dictionary<RagdollPawn, KingSquare>();
        readonly HashSet<RagdollPawn> kingCrowns = new HashSet<RagdollPawn>();

        /// <summary>The king's colour (his palette in the look in use: polished antique gold on the board).</summary>
        Kit.Palette KingColors => kit != null ? kit.king : Kit.King;

        void EnsureKingCall()
        {
            if (nanoShader == null) nanoShader = Shader.Find("ChessFight/Skill Nano") ?? Resources.Load<Shader>("PawnRushSkillFx/SkillNano");
            if (beamShader == null) beamShader = Shader.Find("ChessFight/Skill Beam") ?? Resources.Load<Shader>("PawnRushSkillFx/SkillBeam");
            if (matBeam == null && beamShader != null) matBeam = new Material(beamShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void DestroyKingCall()
        {
            foreach (var g in nanoGlows.Values) g.Restore();
            nanoGlows.Clear();
            foreach (var m in nanoOf.Values)
                if (m != null) Destroy(m);
            nanoOf.Clear();
            if (matBeam != null) Destroy(matBeam);
            matBeam = null;
            kingSquares.Clear();
            kingCrowns.Clear();
        }

        // ---------------------------------------------------------------- the moments

        void KingCallFx(SkillFxEvent e)
        {
            if (kit == null) return;
            EnsureKingCall();
            var king = e.by;
            if (king != null && kingCrowns.Add(king)) KingCrown(king);
            if (e.target != null) KingSquareAt(e.target, e.at, e.dir, false);
        }

        void KingCallGoFx(SkillFxEvent e)
        {
            if (kit == null || e.target == null) return;
            EnsureKingCall();
            var mate = e.target;
            float seconds = e.size > 0f ? e.size : 0.75f;
            var p = KingColors;
            // Taking the boost: a quick white flash in its glow (Skill Nano's _Flash), a puff of gold flecks off it.
            MoteBurst(mate.Hips.position + Vector3.up * 0.15f, 14, p.light, 0.35f, 0.6f, 0.9f, 0.6f, 0.07f);
            NanoOn(mate, seconds);
            // A gold line along the floor behind it as it comes.
            float left = seconds + 0.05f;
            float floorY = Kit.FloorUnder(mate.Hips.position, 0.8f).y;
            InkTrail(() =>
            {
                if ((left -= Time.deltaTime) <= 0f || mate == null) return null;
                Vector3 h = mate.Hips.position;
                return new Vector3(h.x, floorY + 0.03f, h.z);
            }, p, 0.45f, 0.16f, p.light);
            KingSquareAt(mate, e.at, e.dir, true);
        }

        void KingCallLandFx(SkillFxEvent e)
        {
            if (kit == null || e.target == null) return;
            var mate = e.target;
            Vector3 at = Kit.FloorUnder(e.at, 0.8f);
            if (kingSquares.TryGetValue(mate, out var sq)) sq.Land(at, e.dir);
            else KingSquareAt(mate, at, e.dir, true).Land(at, e.dir);
            kit.PuffBurst(at + Vector3.up * 0.05f, kit.dust, 5, 0.32f, 0.2f, 0.4f, 0.12f);
            MoteBurst(at + Vector3.up * 0.25f, 8, KingColors.light, 0.4f, 0.8f, 0.5f, 0.55f, 0.06f);
            if (nanoGlows.TryGetValue(mate, out var g)) g.Drain();
        }

        void KingCallEndFx(SkillFxEvent e)
        {
            if (e.target != null && kingSquares.TryGetValue(e.target, out var sq)) sq.Out();
        }

        // ---------------------------------------------------------------- the crown and its shaft

        /// <summary>The crown over the calling king's head and the shaft of light standing up from it, while he calls (his
        /// stage Active: the host's own, or his skill state on a client).</summary>
        void KingCrown(RagdollPawn king)
        {
            var mesh = SkillMeshes.SceptreCrown();
            var crown = new Kit.Prop(kit, mesh, KingColors, "King call crown") { inkWidth = 0.008f };
            Vector3 size = mesh.bounds.size;
            float scale = 0.34f / Mathf.Max(0.01f, Mathf.Max(size.x, size.z));
            Transform beam = null;
            MeshRenderer beamR = null;
            var block = new MaterialPropertyBlock();
            if (matBeam != null)
            {
                var go = new GameObject("King call shaft");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = kit.meshQuad;
                beamR = go.AddComponent<MeshRenderer>();
                beamR.sharedMaterial = matBeam;
                beamR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamR.receiveShadows = false;
                beam = go.transform;
            }
            float gone = -1f, spin = Random.Range(0f, 360f);
            var f = kit.Run(20f, (fx, d) =>
            {
                if (king == null) return false;
                bool calling = king.Piece == PieceKind.King && king.SkillStage == SkillStage.Active;
                if (!calling && gone < 0f) gone = fx.age;
                float out01 = gone < 0f ? 0f : Mathf.Clamp01((fx.age - gone) / 0.25f);
                if (out01 >= 1f) return false;
                float pop = Kit.Pop(fx.age, 1.18f, 0.2f) * (1f - out01);
                Vector3 head = king.bodies[(int)BodyId.Head].position;
                float rise = Mathf.Lerp(0.25f, 0.62f, Kit.EaseOut(Mathf.Clamp01(fx.age / 0.3f)));
                Vector3 at = head + Vector3.up * (rise + 0.04f * Mathf.Sin(fx.age * 3.2f));
                spin += 70f * d;
                crown.t.SetPositionAndRotation(at, Quaternion.Euler(0f, spin, 0f));
                crown.t.localScale = Vector3.one * scale * Mathf.Max(0.001f, pop);
                crown.rim = Hdr(KingColors.light, 0.9f + 0.3f * Mathf.Sin(fx.age * 6f));
                crown.fade = 1f - out01;
                crown.Apply();
                if (beam != null)
                {
                    // Standing up from the crown, turned to the camera about its own upright.
                    var cam = ViewCamera;
                    float tall = Mathf.Lerp(0.4f, 3.4f, Kit.EaseOut(Mathf.Clamp01(fx.age / 0.35f)));
                    float wide = 0.42f * (1f - 0.5f * out01);
                    Vector3 foot = at + Vector3.up * 0.08f;
                    Vector3 toCam = cam != null ? cam.transform.position - foot : Vector3.back;
                    toCam.y = 0f;
                    Quaternion face = toCam.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(-toCam.normalized, Vector3.up) : Quaternion.identity;
                    beam.SetPositionAndRotation(foot + Vector3.up * (tall * 0.5f), face);
                    beam.localScale = new Vector3(wide, tall, 1f);
                    block.SetColor("_Color", Hdr(KingColors.light, 1.6f));
                    block.SetFloat("_Fade", (1f - out01) * Mathf.Clamp01(fx.age / 0.12f));
                    beamR.SetPropertyBlock(block);
                    beamR.enabled = true;
                }
                return true;
            });
            f.end = () =>
            {
                crown.Destroy();
                if (beam != null) Object.Destroy(beam.gameObject);
                kingCrowns.Remove(king);
            };
        }

        // ---------------------------------------------------------------- the square each one comes to

        KingSquare KingSquareAt(RagdollPawn mate, Vector3 at, Vector3 dir, bool lit)
        {
            if (!kingSquares.TryGetValue(mate, out var sq))
            {
                sq = new KingSquare(this, mate, at, dir);
                kingSquares[mate] = sq;
            }
            else sq.Move(at, dir);
            if (lit) sq.Light();
            return sq;
        }

        /// <summary>A gold square on the floor behind the king: faint while it waits for its piece, lit as the piece sets out,
        /// flaring as it lands, then fading.</summary>
        class KingSquare
        {
            readonly PawnRushSkillFx fx;
            readonly RagdollPawn mate;
            readonly Kit.Tile tile;
            Vector3 at, dir;
            float lit, landedAt = -1f, outAt = -1f, age;

            public KingSquare(PawnRushSkillFx fx, RagdollPawn mate, Vector3 at, Vector3 dir)
            {
                this.fx = fx;
                this.mate = mate;
                tile = new Kit.Tile(fx.kit, "King call square");
                Move(at, dir);
                var run = fx.kit.Run(12f, (r, d) => Step(d));
                run.end = () =>
                {
                    tile.t.gameObject.SetActive(false);
                    Object.Destroy(tile.t.gameObject);
                    if (fx.kingSquares.TryGetValue(mate, out var mine) && mine == this) fx.kingSquares.Remove(mate);
                };
            }

            public void Move(Vector3 where, Vector3 way)
            {
                at = Kit.FloorUnder(where, 0.8f) + Vector3.up * 0.016f;
                dir = way.sqrMagnitude > 1e-4f ? way : Vector3.forward;
            }

            public void Light() => lit = Mathf.Max(lit, 0.01f);
            public void Land(Vector3 where, Vector3 way)
            {
                Move(where, way);
                landedAt = age;
            }

            public void Out()
            {
                if (outAt < 0f && landedAt < 0f) outAt = age;
            }

            bool Step(float d)
            {
                age += d;
                var p = fx.KingColors;
                float glow;
                if (outAt >= 0f)
                {
                    glow = 0.3f * (1f - Mathf.Clamp01((age - outAt) / 0.3f));
                    if (age - outAt > 0.3f) return false;
                }
                else if (landedAt >= 0f)
                {
                    float k = age - landedAt;
                    if (k > 0.9f) return false;
                    glow = k < 0.12f ? Mathf.Lerp(0.85f, 1.25f, k / 0.12f) : Mathf.Lerp(1.25f, 0f, (k - 0.12f) / 0.78f);
                }
                else if (lit > 0f)
                {
                    lit = Mathf.Min(1f, lit + d / 0.25f);
                    glow = Mathf.Lerp(0.3f, 0.85f, lit) + 0.08f * Mathf.Sin(age * 14f);
                }
                else glow = 0.3f + 0.05f * Mathf.Sin(age * 6f);
                if (age > 11f) return false;
                glow = Mathf.Max(0f, glow);
                tile.Floor(at, dir, Vector2.one * (Square * 0.92f));
                tile.shape = 0f;
                tile.round = 0.06f;
                tile.fill = Kit.A(p.main, Mathf.Clamp01(0.18f + 0.6f * glow));
                tile.core = Kit.A(p.light, Mathf.Clamp01(glow - 0.2f));
                tile.coreWidth = 0.08f;
                tile.ink = Kit.A(p.ink, Mathf.Clamp01(0.4f + 0.6f * glow));
                tile.inkWidth = 0.04f;
                tile.rim = Kit.A(p.light, Mathf.Clamp01(glow * 0.8f));
                tile.stripe = Color.clear;
                tile.fade = Mathf.Clamp01(glow * 1.4f);
                if (tile.board)
                {
                    tile.metal = 1f;
                    tile.bevel = Kit.A(p.deep, 0.85f);
                    tile.glow = Kit.A(p.light, Mathf.Clamp01(glow));
                }
                tile.flash = Mathf.Clamp01(glow - 0.9f) * 2f;
                tile.Apply();
                return true;
            }
        }

        // ---------------------------------------------------------------- the boost's glow on the body

        /// <summary>The one coming in glows in the king's colour for <paramref name="seconds"/> (Skill Nano in place of its
        /// own materials, reading their textures), then drains back over a quarter second.</summary>
        void NanoOn(RagdollPawn mate, float seconds)
        {
            if (nanoShader == null || mate == null) return;
            if (nanoGlows.TryGetValue(mate, out var old)) old.Restore();
            var g = new NanoGlow(this, mate);
            nanoGlows[mate] = g;
            float fleck = 0f;
            var f = kit.Run(seconds + 1.5f, (fx, d) =>
            {
                if (mate == null || g.done) return false;
                float k = fx.age;
                // In over a tenth of a second, held, out over a quarter once it has landed (or its time is up).
                float on = Mathf.Clamp01(k / 0.1f);
                if (g.drainAt < 0f && k >= seconds + 0.05f) g.drainAt = k;
                if (g.drainAt >= 0f) on *= 1f - Mathf.Clamp01((k - g.drainAt) / 0.25f);
                if (on <= 0f && g.drainAt >= 0f) return false;
                if (g.drainAt < 0f && g.drainNow) g.drainAt = k;
                g.Paint(on, k);
                // Gold flecks coming off it as it comes.
                fleck -= d;
                if (g.drainAt < 0f && fleck <= 0f)
                {
                    fleck = 0.03f;
                    Vector3 h = mate.Hips.position + Random.insideUnitSphere * 0.25f + Vector3.up * Random.Range(0f, 0.5f);
                    motes.Emit(h, Vector3.up * Random.Range(0.3f, 0.8f) + Random.insideUnitSphere * 0.3f, Random.Range(0.035f, 0.06f),
                               Random.Range(0.3f, 0.5f), Tint(KingColors.light, Random.value * 0.4f));
                }
                return true;
            });
            f.end = () =>
            {
                g.Restore();
                if (nanoGlows.TryGetValue(mate, out var mine) && mine == g) nanoGlows.Remove(mate);
            };
        }

        Material NanoFor(Material original)
        {
            if (original == null) return null;
            if (nanoOf.TryGetValue(original, out var m) && m != null) return m;
            m = new Material(nanoShader) { hideFlags = HideFlags.HideAndDontSave, name = original.name + " (Nano)" };
            if (original.HasProperty("_MainTex")) m.SetTexture("_MainTex", original.GetTexture("_MainTex"));
            if (original.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", original.GetTextureScale("_MainTex"));
            if (original.HasProperty("_MainTex")) m.SetTextureOffset("_MainTex", original.GetTextureOffset("_MainTex"));
            m.SetColor("_Color", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
            nanoOf[original] = m;
            return m;
        }

        class NanoGlow
        {
            readonly PawnRushSkillFx fx;
            readonly List<Renderer> renderers = new List<Renderer>();
            readonly List<Material[]> originals = new List<Material[]>();
            readonly List<Material[]> nanos = new List<Material[]>();
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public float drainAt = -1f;
            public bool drainNow, done;

            public NanoGlow(PawnRushSkillFx fx, RagdollPawn mate)
            {
                this.fx = fx;
                foreach (var r in mate.GetComponentsInChildren<Renderer>())
                {
                    if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || !r.enabled) continue;
                    var mats = r.sharedMaterials;
                    var swap = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++) swap[i] = fx.NanoFor(mats[i]) ?? mats[i];
                    renderers.Add(r);
                    originals.Add(mats);
                    nanos.Add(swap);
                    r.sharedMaterials = swap;
                }
            }

            public void Drain() => drainNow = true;

            public void Paint(float on, float age)
            {
                var p = fx.KingColors;
                block.Clear();
                block.SetColor("_Tint", p.main);
                block.SetColor("_RimColor", Hdr(p.light, 2.2f));
                block.SetFloat("_Glow", on);
                block.SetFloat("_Flow", age);
                block.SetFloat("_Flash", 1f - Mathf.Clamp01(age / 0.09f));
                foreach (var r in renderers)
                    if (r != null) r.SetPropertyBlock(block);
            }

            /// <summary>Its own materials back (not over another change made meanwhile, a new piece's model).</summary>
            public void Restore()
            {
                if (done) return;
                done = true;
                for (int i = 0; i < renderers.Count; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    var now = r.sharedMaterials;
                    bool ours = now.Length == nanos[i].Length;
                    for (int j = 0; ours && j < now.Length; j++) ours = now[j] == nanos[i][j];
                    if (ours) r.sharedMaterials = originals[i];
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
