using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // E · 왕궁 서고 (R507, ArtSource/PawnRush/MiniGames/Mid/e_library_v1.py): the user's Tripo bookcase towers and props
    // from the GPT asset sheets (Docs/PawnRush/ConceptArt/R507_RoyalLibrary/AssetRefs), fitted to MgBells' numbers.
    // Pictures only: every tower collider, the bell rule, the team gate and its barrier stay the game's.
    public static partial class MiniGameModels
    {
        // e_library_v1.py: the top storey's lid 2 cm over each top, the bell's stand 0.5 cm on it (BELL_LIFT), 0.55 m to
        // the north of the top's centre
        const float BellLift = .025f, BellBack = .4f;      // R543: 0.55 on the 2.4 m tops; the tops are 2 m now
        const float TowerModelHeight = 9f;                 // E_Tower_9
        const string DoorFitName = "MODEL_DoorFit";
        const float DoorTop = 5.79f, DoorWidth = 4.2f;   // e_hall_v1.py TUNNEL: ceiling 5.8, walls 4.44 apart

        static void RoyalLibrary(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            // R543: the towers stand where the team's size puts them (BellTowers), so one tower's model rides each of
            // the game's towers. (Before: E_StationEnv, one mesh of the six towers where they always stood.)
            var tower = Load("Mid/E/E_Tower_9");
            var bell = Load("Mid/E/E_Bell");
            var door = Load("Mid/E/E_ShelfDoor");
            var rung = Resources.Load<Material>(OwnMaterials + "PR_Mid_BellRung");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                // The pivoting bookcase (MiniGamePalaceArt.DressGate, turned by PalaceGateLeaf when the team's gate
                // opens): the leaf model hangs on the same pivot, hinge on it and the leaf along +x, so it swings with
                // it; the code-built shelf stops drawing.
                // Fitted to the rebuilt tunnel with no gap (user, 11-08: "출구 쪽 문 비틈 없이 문 크기 위로 키워"): the
                // 3.8 x 4.6 m leaf grows to the tunnel's floor-to-ceiling 5.79 m and to its far wall (4.2 m from a
                // hinge 8 cm out from the game's pivot, |x| 9.78..14.22 at x +-12); open, it lies along the wall.
                var pivot = station.transform.Find("DEC_ShelfDoorPivot");
                if (pivot != null && door != null)
                {
                    HideExcept(pivot);
                    if (pivot.Find(DoorFitName) == null)
                    {
                        var fit = new GameObject(DoorFitName).transform;
                        fit.SetParent(pivot, false);
                        // the pivot sits at the gate's middle, 2.3 m up: the leaf from the floor (-2.3) to 5.79 m (+3.49)
                        fit.localPosition = new Vector3(-.08f, (DoorTop - 2.3f - 2.3f) * .5f, 0f);
                        fit.localScale = new Vector3(DoorWidth / 3.8f, DoorTop / 4.6f, 1f);
                        Hang(fit, RideName, door, cache);
                    }
                }
                if (station.Game == null) continue;
                var game = station.Game.transform;
                // E_Tower_9 (e_library_v1.py: a 9 m tower of bookcase cabinets, 2 m wide, walnut decks on its ledges and
                // its top; origin on the floor under the top's middle) on each of the game's towers, in the game frame
                // (x = v, z = u). The storey boxes, their code bookshelf faces and the bell posts stop drawing; their
                // colliders stay. A tower of another height keeps the game's own look.
                var towers = station.Game is MgBells bells ? bells.Towers : null;
                if (tower != null && towers != null)
                {
                    bool all = true;
                    for (int i = 0; i < towers.Length; i++)
                    {
                        if (Mathf.Abs(towers[i].y - TowerModelHeight) > .01f) { all = false; continue; }
                        string name = "MODEL_Tower " + (i + 1);
                        if (game.Find(name) != null) continue;
                        var at = new GameObject(name).transform;
                        at.SetParent(game, false);
                        at.localPosition = new Vector3(towers[i].x, 0f, towers[i].z);
                        Hang(at, RideName, tower, cache);
                    }
                    if (all)
                        foreach (var t in game.GetComponentsInChildren<Transform>(true))
                            if ((t.name.StartsWith("Tower ") && t.name.Contains(" storey ")) ||
                                t.name == "Bell post W" || t.name == "Bell post E" || t.name == "Bell beam")
                                Hide(t);
                }
                if (bell != null) LibraryBells(station, game, bell, rung, cache);
                // the bookcase door's own opening pictures and sound instead of the shared ones (ArenaDressing, IGateFx)
                if (station.GetComponent<MiniGameShelfDoorFx>() == null) station.gameObject.AddComponent<MiniGameShelfDoorFx>();
            }

            LibraryFloors(owner, content);
            LibraryHall(owner, content, cache);

            // The stone frame round each team gate's tunnel mouth, where the shell's "Team portal front" arch stood
            // (opening 4.5 m, 5.8 m to the crown, section x +-12, z 23.8; piers 0.9 m).
            var arch = Load("Mid/E/E_DoorArch");
            if (arch == null) return;
            foreach (float x in new[] { -12f, 12f })
            {
                string name = x < 0f ? "MODEL_TeamArch_W" : "MODEL_TeamArch_E";
                if (content.Find(name) != null) continue;
                var at = new GameObject(name).transform;
                at.SetParent(content, false);
                at.localPosition = new Vector3(x, 0f, 23.8f);
                Hang(at, RideName, arch, cache);
            }
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null)
                foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Team portal front") Hide(t);
        }

        // E's room (R507, e_hall_v1.py; E_HallEnv in the section frame): the R312 shell is removed for E as for B (user,
        // 11-07: the old structure may go, walls must run on without gaps). The user's wall and gate sets stand round the
        // room - ground arched windows over walnut wainscot with grey-book bookcases, the gallery balcony, upper arched
        // windows, the cornice, the centre facade with its closed door on the north block, open arches at the entry and
        // the exit, walnut beams - and one cream ashlar fill closes every gap up to the 13 m walnut ceiling, keeping the
        // entry, the exit and the two team-gate tunnels open. Every shell picture stops drawing; colliders are the
        // section builder's and stay. The shell's team banners go with it, so a team-coloured cloth made in code hangs
        // over each team door frame (its front face z 23.3, top 7.2 m) so each team still finds its own door.
        static void LibraryHall(MiniGameSection owner, Transform content, Dictionary<string, Material> cache)
        {
            var hall = Load("Mid/E/E_HallEnv");
            if (hall == null) return;
            if (content.Find("MODEL_Hall") == null) Hang(content, "MODEL_Hall", hall, cache);
            Hide(content.Find(MiniGamePalaceArt.ShellName));
            // The shell's code chandeliers went with it: B's chandelier and sconce bodies (e_lights_v1.py, no flame in
            // the model) - seven chandeliers over the aisle and between the tower columns, ten sconces on the wall
            // pilasters - get their flames and lights from code at the SOCKET_ empties (user rule, 11-07).
            var lights = Load("Mid/E/E_LightsEnv");
            if (lights != null && content.Find("MODEL_Lights") == null)
            {
                Hang(content, "MODEL_Lights", lights, cache);
                MiniGameFixtures.LightSockets(content.Find("MODEL_Lights"));
            }
            // Props modelled in Blender (e_props_v1.py: book trolleys, reading tables with chairs, globes, book stacks,
            // ladders against the wall bookcases, reading desks), by the walls and off the pawns' ways. Pictures only.
            var props = Load("Mid/E/E_PropsEnv");
            if (props != null && content.Find("MODEL_Props") == null) Hang(content, "MODEL_Props", props, cache);
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                float x = station.transform.localPosition.x;
                string name = x < 0f ? "MODEL_Pennant_W" : "MODEL_Pennant_E";
                if (content.Find(name) == null)
                    MiniGameFixtures.Pennant(content, name, new Vector3(x, 7.0f, 23.25f), 2.0f, 1.6f, MiniGamePalaceArt.TeamColor(station.Team));
            }
        }

        // E's floors (R507, as the concept Docs/PawnRush/ConceptArt/R507_RoyalLibrary/E_01_overview.png), B's baked
        // phase-2 floors (r503_material_swatches.py) on the game's own floor boxes: dark slate flagstones on the stations
        // and the gate passages, cream-and-grey marble chequer on the entry band and the central aisle, cream marble on
        // the rear corridor. The shell's code paving over the entry band and the rear corridor stops drawing. Each
        // texture is a 2 m tile: the box's top face (UV 0..1) repeats size / 2 times. Colliders and gold rims stay.
        static void LibraryFloors(MiniGameSection owner, Transform content)
        {
            var slate = Resources.Load<Material>(OwnMaterials + "PR_Mid_SlateFloor");
            var chequer = Resources.Load<Material>(OwnMaterials + "PR_Mid_CheckerMarble");
            var cream = Resources.Load<Material>(OwnMaterials + "PR_Mid_CreamFloor");
            if (slate == null || chequer == null || cream == null) return;
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null) Hide(Find(shell, "ENV_EntryAndRearPaving"));
            var block = new MaterialPropertyBlock();
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
            {
                Material m = null;
                switch (r.name)
                {
                    case "Station floor": case "Gate passage": m = slate; break;
                    case "Entry band": case "Central aisle": m = chequer; break;
                    case "Rear corridor": m = cream; break;
                }
                if (m == null) continue;
                r.sharedMaterial = m;
                var s = r.transform.lossyScale;
                block.Clear();
                block.SetVector("_BaseMap_ST", new Vector4(Mathf.Abs(s.x) * .5f, Mathf.Abs(s.z) * .5f, 0f, 0f));
                r.SetPropertyBlock(block);
            }
        }

        // The user (11-07): no reading lamp on the bookcases. Each top keeps the game's own signal, its bell: the hand
        // bell model stands on the top's north half, and gilds and rocks when the game rings that bell. The game's bell
        // cylinder (and the R312 lamp dressing on it) stops drawing but keeps its renderer, whose material the game
        // switches to rank gold when rung - MiniGameBellLook watches it.
        static void LibraryBells(MissionStation station, Transform game, GameObject bell, Material rung, Dictionary<string, Material> cache)
        {
            var gold = station.Kit != null ? station.Kit.rankGold : null;
            var towers = station.Game is MgBells bells ? bells.Towers : null;
            if (towers == null) return;
            foreach (var t in game.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Bell") continue;
                var local = game.InverseTransformPoint(t.position);
                int index = -1;
                float best = float.MaxValue;
                for (int i = 0; i < towers.Length; i++)
                {
                    var tower = towers[i];
                    float d = new Vector2(local.x - tower.x, local.z - tower.z).sqrMagnitude;
                    if (d < best) { best = d; index = i; }
                }
                if (index < 0) continue;
                string name = "MODEL_Bell " + (index + 1);
                var flat = t.GetComponent<Renderer>();
                Hide(t);
                if (game.Find(name) != null) continue;
                var top = towers[index];
                var at = new GameObject(name).transform;
                at.SetParent(game, false);
                at.localPosition = new Vector3(top.x, top.y + BellLift, top.z + BellBack);
                Hang(at, RideName, bell, cache);
                at.gameObject.AddComponent<MiniGameBellLook>().Watch(flat, gold, rung, index);
            }
        }
    }

    // A bell model following the game's bell state (picture only): gilded and glowing while rung, with a short rock
    // when it turns. The state is read from the game's own (hidden) bell renderer, which MgBells sets to rank gold
    // when rung and back on reset, so remote players and resets follow with no extra network state.
    // When it turns to rung (user, 11-07: "종 울릴 때 vfx랑 sfx 적용 해", then "이런 vfx 말고" -> sound waves and notes):
    // MiniGameBellRingFx, and the bell's ring (ElevenLabs sound effect, Resources/PawnRushMiniGameSfx/sfx_bell_ring)
    // at the tower's own note: the six towers play C D E G A C
    // (u 5, 11, 17 low to high, so the climb rises in pitch). The ring plays from the bell's own voice at the exact
    // note (ArenaFx.Sfx detunes by +-5 %, which would put a scale out of tune).
    public sealed class MiniGameBellLook : MonoBehaviour
    {
        const string RingClip = "PawnRushMiniGameSfx/sfx_bell_ring";
        static readonly int[] Semitones = { 0, 2, 4, 7, 9, 12 };   // MgBells.Towers order: two per height

        Renderer source;
        Material gold, rung, unrung;
        Renderer[] parts;
        Material[][] plain;
        bool shown;
        float rockTime = -1f;
        int note;
        AudioSource voice;
        static AudioClip ringClip;
        static bool ringMissing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ringClip = null; ringMissing = false; }

        public void Watch(Renderer gameBell, Material rankGold, Material rungLook, int tower)
        {
            note = Semitones[Mathf.Clamp(tower, 0, Semitones.Length - 1)];
            source = gameBell;
            gold = rankGold;
            rung = rungLook;
            unrung = gameBell != null ? gameBell.sharedMaterial : null;
            parts = GetComponentsInChildren<Renderer>(true);
            plain = new Material[parts.Length][];
            for (int i = 0; i < parts.Length; i++) plain[i] = parts[i].sharedMaterials;
            Show(IsRung(), false);
        }

        bool IsRung()
        {
            if (source == null) return false;
            var m = source.sharedMaterial;
            return gold != null ? m == gold : m != unrung;
        }

        void Show(bool on, bool rock)
        {
            shown = on;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                if (!on || rung == null) { parts[i].sharedMaterials = plain[i]; continue; }
                var slots = new Material[plain[i].Length];
                for (int k = 0; k < slots.Length; k++) slots[k] = rung;
                parts[i].sharedMaterials = slots;
            }
            if (rock) { rockTime = 0f; Ring(); }
        }

        void Ring()
        {
            var bell = transform.position + transform.up * .75f;
            if (ringClip == null && !ringMissing)
            {
                ringClip = Resources.Load<AudioClip>(RingClip);
                if (ringClip == null)
                {
                    ringMissing = true;
                    Debug.LogWarning("[미니게임 모델] 종소리 파일이 없어 재생하지 않습니다: Resources/" + RingClip);
                }
            }
            if (ringClip != null)
            {
                if (voice == null)
                {
                    voice = gameObject.AddComponent<AudioSource>();
                    voice.playOnAwake = false;
                    voice.spatialBlend = 1f;
                    voice.rolloffMode = AudioRolloffMode.Linear;
                    voice.minDistance = 4f;
                    voice.maxDistance = 45f;
                    SectionSoundGate.Gate(voice);   // R529
                }
                voice.pitch = Mathf.Pow(2f, note / 12f);
                voice.PlayOneShot(ringClip);
            }
            // the picture after the sound: a failing effect must never silence the bell
            MiniGameBellRingFx.Play(bell, parts);
        }

        void LateUpdate()
        {
            if (parts == null) return;
            bool on = IsRung();
            if (on != shown) Show(on, on);
            if (rockTime < 0f) return;
            rockTime += Time.deltaTime;
            const float span = 1.4f;
            float angle = rockTime < span ? 9f * Mathf.Sin(rockTime * 14f) * (1f - rockTime / span) : 0f;
            transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            if (rockTime >= span) rockTime = -1f;
        }
    }

    // The bell's ring as an effect (user, 11-07: sound waves and notes, "음파 + 음표"), picture only, played by whoever
    // sees the bell turn: three sound-wave rings swell from the bell one after another, always facing the camera so
    // they read as circles from anywhere; five gold eighth notes (single and beamed) float up, swaying, and fade; the
    // bell's rim flashes (emission pulse on its own renderers) and a warm point light blinks over the top. Shapes are
    // premultiplied white-on-black textures (e_bell_fx_textures.py, Resources/PawnRushMiniGames/Fx) on the project's additive particle
    // material (Resources/URPVariants/Particles Additive), tinted HDR gold so the bloom takes them.
    public sealed class MiniGameBellRingFx : MonoBehaviour
    {
        const string Fx = "PawnRushMiniGames/Fx/";
        const float Length = 2.1f;
        static readonly Color GoldHdr = new Color(2.6f, 1.75f, .55f, 1f);
        static Material additive;
        static Texture2D wave, note1, note2;
        static bool missing;

        sealed class Piece
        {
            public Transform T;
            public Renderer R;
            public bool Wave;
            public float Delay, Life, Size, Phase, Drift;
        }

        readonly List<Piece> pieces = new List<Piece>();
        // Made in Build: Unity refuses a MaterialPropertyBlock built in a MonoBehaviour's field initialiser (it stayed
        // null, the first quad was left white and the exception skipped the ring's sound; user, 11-07: "똑같아 소리도 안나와")
        MaterialPropertyBlock block;
        Renderer[] bell;
        Light flash;
        float age;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { additive = null; wave = note1 = note2 = null; missing = false; }

        public static void Play(Vector3 at, Renderer[] bellParts)
        {
            if (!Application.isPlaying) return;
            if (additive == null && !missing)
            {
                var source = Resources.Load<Material>("URPVariants/Particles Additive");
                wave = Resources.Load<Texture2D>(Fx + "T_Bell_Wave");
                note1 = Resources.Load<Texture2D>(Fx + "T_Bell_Note1");
                note2 = Resources.Load<Texture2D>(Fx + "T_Bell_Note2");
                if (source == null || wave == null || note1 == null || note2 == null)
                {
                    missing = true;
                    Debug.LogWarning("[미니게임 모델] 종 효과 재질·텍스처가 없어 음파·음표를 그리지 않습니다: Resources/" + Fx);
                }
                else additive = new Material(source) { name = "Bell ring FX" };
            }
            var fx = new GameObject("Bell ring FX").AddComponent<MiniGameBellRingFx>();
            fx.transform.position = at;
            fx.bell = bellParts;
            fx.Build();
        }

        void Build()
        {
            block = new MaterialPropertyBlock();
            if (additive != null)
            {
                for (int i = 0; i < 3; i++)
                    pieces.Add(Make(wave, true, i * .16f, .95f, 1f, 0f, 0f));
                for (int i = 0; i < 5; i++)
                    pieces.Add(Make(i % 2 == 0 ? note1 : note2, false, .05f + i * .13f, 1.5f, Random.Range(.36f, .5f),
                                    Random.Range(0f, 6.3f), Random.Range(-1f, 1f)));
            }
            var go = new GameObject("Bell flash");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * .4f;
            flash = go.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, .78f, .42f);
            flash.range = 7f;
            flash.shadows = LightShadows.None;
            flash.intensity = 0f;
        }

        Piece Make(Texture2D tex, bool isWave, float delay, float life, float size, float phase, float drift)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = isWave ? "Sound wave" : "Note";
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            block.Clear();
            block.SetTexture("_BaseMap", tex);
            block.SetColor("_BaseColor", Color.clear);
            r.SetPropertyBlock(block);
            r.enabled = false;
            return new Piece { T = go.transform, R = r, Wave = isWave, Delay = delay, Life = life, Size = size, Phase = phase, Drift = drift };
        }

        void Update()
        {
            if (block == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            var cam = Camera.main;
            foreach (var p in pieces)
            {
                float k = (age - p.Delay) / p.Life;
                if (k < 0f || k > 1f) { p.R.enabled = false; continue; }
                p.R.enabled = true;
                float ease = 1f - (1f - k) * (1f - k);
                float alpha;
                if (p.Wave)
                {
                    p.T.localPosition = Vector3.zero;
                    p.T.localScale = Vector3.one * Mathf.Lerp(.7f, 4.2f, ease);
                    alpha = Mathf.Pow(1f - k, 1.6f) * .9f;
                }
                else
                {
                    var right = cam != null ? cam.transform.right : Vector3.right;
                    p.T.localPosition = Vector3.up * (.3f + 1.7f * ease) + right * (p.Drift * .7f * ease + .18f * Mathf.Sin(p.Phase + k * 7f));
                    p.T.localScale = Vector3.one * p.Size * (k < .12f ? k / .12f : 1f);
                    alpha = k < .12f ? 1f : 1f - (k - .12f) / .88f;
                }
                if (cam != null)
                {
                    p.T.rotation = Quaternion.LookRotation(p.T.position - cam.transform.position, cam.transform.up);
                    if (!p.Wave) p.T.Rotate(0f, 0f, 14f * Mathf.Sin(p.Phase + k * 5f), Space.Self);
                }
                block.Clear();
                p.R.GetPropertyBlock(block);
                // the additive material adds the colour as it is: fade the colour itself (premultiplied textures)
                block.SetColor("_BaseColor", new Color(GoldHdr.r * alpha, GoldHdr.g * alpha, GoldHdr.b * alpha, alpha));
                p.R.SetPropertyBlock(block);
            }
            // the bell's rim flash and the light: up fast, down over 0.7 s
            float f = age < .06f ? age / .06f : Mathf.Clamp01(1f - (age - .06f) / .7f);
            if (flash != null) flash.intensity = 3.5f * f * f;
            if (bell != null)
                foreach (var r in bell)
                {
                    if (r == null) continue;
                    block.Clear();
                    if (f > 0f) block.SetColor("_EmissionColor", new Color(1.2f, .8f, .25f) * (.5f + 4f * f));
                    r.SetPropertyBlock(block);
                }
            if (age >= Length) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (bell == null) return;
            foreach (var r in bell)
                if (r != null) r.SetPropertyBlock(null);
        }
    }

    // E's bookcase door opening (user, 11-07: "문이 열리는 효과도 교체 해"), on the station in place of the shared
    // gate-open beam, burst and ring (ArenaDressing): as PalaceGateLeaf swings the bookcase back into the tunnel, dust
    // puffs from the floor and the leaf's top, warm gold light spills from the opening with motes drifting out toward
    // the room, loose pages flutter down, and the door's creak plays (ElevenLabs take 4,
    // Resources/PawnRushMiniGameSfx/sfx_shelf_door_open). The quarter moments are left to the bells' own rings.
    // Pictures only, played by whoever sees the game complete.
    public sealed class MiniGameShelfDoorFx : MonoBehaviour, IGateFx
    {
        const string Fx = "PawnRushMiniGames/Fx/", Sound = "PawnRushMiniGameSfx/sfx_shelf_door_open";
        const float Length = 5f;
        static readonly Color Warm = new Color(2.4f, 1.55f, .55f, 1f);
        static Material additive, blended, page;
        static Texture2D dustTex, moteTex, glowTex;
        static AudioClip creak;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { additive = blended = page = null; dustTex = moteTex = glowTex = null; creak = null; loaded = false; }

        sealed class Bit
        {
            public Transform T;
            public Renderer R;
            public int Kind;                        // 0 dust, 1 mote, 2 page
            public Vector3 From, Velocity, Spin;
            public float Delay, Life, Size, Phase;
        }

        readonly List<Bit> bits = new List<Bit>();
        MaterialPropertyBlock block;
        Transform root;
        Renderer glow;
        Light spill;
        float age = -1f;
        Vector3 right, forward, centre;
        float floor;

        public void Quarter(int quarter) { }

        public void Opened()
        {
            var station = GetComponent<MissionStation>();
            if (station == null || station.Gate == null || !Application.isPlaying) return;
            Load();
            var gate = station.Gate.transform;
            right = transform.right; forward = transform.forward;
            centre = gate.position; floor = transform.position.y;
            if (creak != null)
            {
                var v = gate.gameObject.GetComponent<AudioSource>();
                if (v == null)
                {
                    v = gate.gameObject.AddComponent<AudioSource>();
                    v.playOnAwake = false; v.spatialBlend = 1f; v.rolloffMode = AudioRolloffMode.Linear;
                    v.minDistance = 5f; v.maxDistance = 50f;
                    SectionSoundGate.Gate(v);   // R529
                }
                MiniGameEar.Fit(v, station);   // the same anywhere in this mini-game (user 11-08, D session)
                v.PlayOneShot(creak);
            }
            if (additive == null || blended == null) return;
            if (root != null) Destroy(root.gameObject);
            bits.Clear();
            block = new MaterialPropertyBlock();
            root = new GameObject("Shelf door FX").transform;
            age = 0f;
            var rng = new System.Random();
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // dust: along the bottom of the leaf and off its top edge as it swings
            for (int i = 0; i < 16; i++)
            {
                bool top = i >= 11;
                var at = centre + right * R(-1.9f, 1.9f) + Vector3.up * (top ? R(3.2f, 3.5f) : R(-2.1f, -1.8f)) - forward * R(.1f, .5f);
                Add(0, dustTex, blended, at, new Vector3(0f, top ? -.25f : .35f, 0f) - forward * R(.2f, .6f) + right * R(-.3f, .3f),
                    R(0f, .7f), R(1.6f, 2.4f), R(.9f, 1.5f));
            }
            // motes drifting out of the light
            for (int i = 0; i < 30; i++)
                Add(1, moteTex, additive, centre + right * R(-1.8f, 1.8f) + Vector3.up * R(-2f, 1.8f) + forward * .4f,
                    -forward * R(.3f, .7f) + Vector3.up * R(.1f, .35f), R(.5f, 3.4f), R(1.4f, 2.4f), R(.1f, .2f));
            // pages from the top of the bookcase
            for (int i = 0; i < 7; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.name = "Loose page";
                go.transform.SetParent(root, false);
                go.transform.localScale = new Vector3(.2f, .004f, .27f);
                var r = go.GetComponent<Renderer>();
                if (page != null) r.sharedMaterial = page;
                r.enabled = false;
                bits.Add(new Bit { T = go.transform, R = r, Kind = 2, From = centre + right * R(-1.9f, 1.9f) + Vector3.up * 3.55f - forward * .35f,
                                   Delay = R(.2f, .8f), Life = 3.2f, Phase = R(0f, 6.3f), Spin = new Vector3(R(-90f, 90f), R(-160f, 160f), R(-90f, 90f)) });
            }
            // the light spilling from the tunnel
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(g.GetComponent<Collider>());
            g.name = "Doorway glow";
            g.transform.SetParent(root, false);
            g.transform.position = centre + forward * .25f + Vector3.up * .1f;
            g.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            g.transform.localScale = new Vector3(4.2f, 4.9f, 1f);
            glow = g.GetComponent<Renderer>();
            glow.sharedMaterial = additive;
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = new GameObject("Doorway light");
            l.transform.SetParent(root, false);
            l.transform.position = centre + forward * 1.4f;
            spill = l.AddComponent<Light>();
            spill.type = LightType.Point; spill.color = new Color(1f, .76f, .42f); spill.range = 10f; spill.shadows = LightShadows.None; spill.intensity = 0f;
        }

        void Add(int kind, Texture2D tex, Material m, Vector3 from, Vector3 velocity, float delay, float life, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = kind == 0 ? "Dust" : "Mote";
            go.transform.SetParent(root, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            block.Clear(); block.SetTexture("_BaseMap", tex); block.SetColor("_BaseColor", Color.clear); r.SetPropertyBlock(block);
            r.enabled = false;
            bits.Add(new Bit { T = go.transform, R = r, Kind = kind, From = from, Velocity = velocity, Delay = delay, Life = life, Size = size, Phase = Random.value * 6.3f });
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var a = Resources.Load<Material>("URPVariants/Particles Additive");
            var b = Resources.Load<Material>("URPVariants/Lit Transparent");
            dustTex = Resources.Load<Texture2D>(Fx + "T_Door_Dust");
            moteTex = Resources.Load<Texture2D>(Fx + "T_Door_Mote");
            glowTex = Resources.Load<Texture2D>(Fx + "T_Door_Glow");
            page = Resources.Load<Material>("PawnRushMiniGames/Materials/PR_Mid_PropPage");
            creak = Resources.Load<AudioClip>(Sound);
            if (a != null && b != null && dustTex != null && moteTex != null && glowTex != null)
            {
                additive = new Material(a) { name = "Shelf door FX add" };
                blended = new Material(b) { name = "Shelf door FX dust" };
            }
            else Debug.LogWarning("[미니게임 모델] 책장 문 효과 재질·텍스처가 없어 그리지 않습니다: Resources/" + Fx);
            if (creak == null) Debug.LogWarning("[미니게임 모델] 책장 문 소리 파일이 없어 재생하지 않습니다: Resources/" + Sound);
        }

        void Update()
        {
            if (age < 0f || root == null) return;
            age += Time.deltaTime;
            var cam = Camera.main;
            foreach (var b in bits)
            {
                float t = age - b.Delay, k = t / b.Life;
                if (k < 0f || k > 1f) { b.R.enabled = false; continue; }
                b.R.enabled = true;
                if (b.Kind == 2)
                {
                    // fall with a flutter, then lie on the floor and shrink away
                    float y = Mathf.Max(floor + .02f, b.From.y - .85f * t);
                    bool landed = y <= floor + .021f;
                    float sway = landed ? 0f : .35f * Mathf.Sin(b.Phase + t * 3f);
                    b.T.position = new Vector3(b.From.x, y, b.From.z) + right * sway - forward * (.25f * t);
                    if (!landed) b.T.rotation = Quaternion.Euler(b.Spin * t + new Vector3(25f * Mathf.Sin(b.Phase + t * 4f), 0f, 0f));
                    else b.T.rotation = Quaternion.Euler(0f, b.Spin.y * t, 0f);
                    b.T.localScale = new Vector3(.2f, .004f, .27f) * (k > .8f ? (1f - k) / .2f : 1f);
                    continue;
                }
                float ease = 1f - (1f - k) * (1f - k);
                b.T.position = b.From + b.Velocity * t + (b.Kind == 1 ? right * .12f * Mathf.Sin(b.Phase + t * 2.5f) : Vector3.zero);
                if (cam != null) b.T.rotation = Quaternion.LookRotation(b.T.position - cam.transform.position, cam.transform.up);
                Color c;
                if (b.Kind == 0)
                {
                    b.T.localScale = Vector3.one * b.Size * Mathf.Lerp(.5f, 1.8f, ease);
                    float a = (k < .2f ? k / .2f : (1f - k) / .8f) * .5f;
                    c = new Color(.78f, .7f, .6f, a);
                }
                else
                {
                    b.T.localScale = Vector3.one * b.Size;
                    float a = (k < .15f ? k / .15f : (1f - k) / .85f) * (.7f + .3f * Mathf.Sin(b.Phase + t * 9f));
                    c = new Color(Warm.r * a, Warm.g * a, Warm.b * a, a);   // premultiplied for the additive material
                }
                block.Clear(); b.R.GetPropertyBlock(block); block.SetColor("_BaseColor", c); b.R.SetPropertyBlock(block);
            }
            // the doorway light: up as the leaf swings (0.3..1.3 s), held, then down by 4.5 s
            float f = age < .3f ? 0f : age < 1.3f ? (age - .3f) : age < 3f ? 1f : Mathf.Clamp01(1f - (age - 3f) / 1.5f);
            if (glow != null)
            {
                block.Clear(); block.SetTexture("_BaseMap", glowTex);
                block.SetColor("_BaseColor", new Color(Warm.r * f * .8f, Warm.g * f * .8f, Warm.b * f * .8f, f));
                glow.SetPropertyBlock(block);
            }
            if (spill != null) spill.intensity = 4f * f;
            if (age >= Length) { Destroy(root.gameObject); root = null; age = -1f; }
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
