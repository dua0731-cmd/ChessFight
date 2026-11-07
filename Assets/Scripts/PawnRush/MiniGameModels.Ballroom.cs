using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // F · 대무도회장 (R508, ArtSource/PawnRush/MiniGames/Mid/f_*.py, Docs/PawnRush/ConceptArt/R508_ClockworkBallroom).
    // Pictures only: every statue collider, its clock-driven step, the capture rule, the tray, the team gate and its
    // barrier stay MgEnPassant's and the section builder's.
    //   room      F_HallEnv = E's room without the library's wall bookcases (e_hall_v1.py --ballroom), E's chandeliers
    //             and sconces (E_LightsEnv), E's floors, E's team door arches, code pennants
    //   dancers   F_DancerGown / F_DancerTailcoat (f_dancers_v1.py, made in Blender: "인형 느낌") on every statue's
    //             Look, costume tinted to the other team's colour, a turning wind-up key in the back socket
    //   bench     F_Bench (f_bench_v1.py) under the game's tray
    //   lanes     the game's lane squares in the parquet swatch (one square = one tile)
    //   mirrors   F_MirrorDoor on each team gate, F_WallMirror in the gallery; their MIRROR_Surface is a flat mirror
    //             material lit by one box-projected reflection probe rendered with the dancers hidden (user, 11-08:
    //             "방만 비침": the room shows in them, the moving pieces do not)
    //   props     spotlights, harps, music stands, coat stands, drinks tables, topiaries (Tripo, f_props_v1.py)
    public static partial class MiniGameModels
    {
        const string BallroomFolder = "Mid/F/";

        // f_dancers_v1.py: the back socket (Blender 0, 0.178, 0.515 = Look 0, 0.515, 0.178)
        static readonly Vector3 DancerSocket = new Vector3(0f, .515f, .178f);

        // Placed props: model, section position (x, y, z), facing direction in the section (x, z).
        struct Placed
        {
            public string Model;
            public Vector3 At;
            public Vector2 Face;
            public Placed(string model, Vector3 at, Vector2 face) { Model = model; At = at; Face = face; }
        }

        static List<Placed> BallroomProps()
        {
            var list = new List<Placed>();
            foreach (float s in new[] { -1f, 1f })
            {
                var room = new Vector2(-s, 0f);
                // gallery wall mirrors between the upper windows (window centres at z 2.5 .. 22.5, so the piers at
                // z 5, 10, 15, 20), bottom 7.3 m (windows 6.9..10.5 m), backs on the upper wall face (|x| 22)
                foreach (float z in new[] { 5f, 10f, 15f, 20f })
                    list.Add(new Placed("F_WallMirror", new Vector3(s * 21.97f, 7.3f, z), room));
                // ceiling spotlights over each team's dancers, aimed at the floor's middle (x +-12, z 14)
                foreach (float x in new[] { 7.5f, 16.5f })
                    foreach (float z in new[] { 9f, 18f })
                        list.Add(new Placed("F_Spotlight", new Vector3(s * x, 12.98f, z), new Vector2(s * 12f - s * x, 14f - z)));
                // the gallery (deck 5.3 m, |x| 20.7..22): a harp and a music stand
                list.Add(new Placed("F_Harp", new Vector3(s * 21.25f, 5.3f, 12.5f), room));
                list.Add(new Placed("F_MusicStand", new Vector3(s * 21.15f, 5.3f, 14.4f), new Vector2(0f, -1f)));
                // the entry band's outer corners and the rear corridor's ends, off the pawns' ways (entry x +-6 to the
                // station ramps x +-9.8..14.2, z 2.4..4; the tunnels x +-12 at z 30 to the exit x +-6 at z 36)
                list.Add(new Placed("F_CoatStand", new Vector3(s * 21.2f, 0f, 1.0f), room));
                list.Add(new Placed("F_DrinksTable", new Vector3(s * 18.3f, 0f, 1.1f), new Vector2(0f, -1f)));
                list.Add(new Placed("F_Topiary", new Vector3(s * 15.7f, 0f, 1.0f), new Vector2(0f, -1f)));
                list.Add(new Placed("F_Topiary", new Vector3(s * 21.0f, 0f, 3.3f), room));
                list.Add(new Placed("F_Topiary", new Vector3(s * 16.6f, 0f, 34.9f), new Vector2(0f, -1f)));
                list.Add(new Placed("F_DrinksTable", new Vector3(s * 19.6f, 0f, 33.4f), room));
            }
            return list;
        }

        static void ClockworkBallroom(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            BallroomHall(owner, content, cache);
            var gown = Load(BallroomFolder + "F_DancerGown");
            var coat = Load(BallroomFolder + "F_DancerTailcoat");
            var key = Load(BallroomFolder + "F_WindKey");
            var bench = Load(BallroomFolder + "F_Bench");
            var door = Load(BallroomFolder + "F_MirrorDoor");
            var parquet = Resources.Load<Material>(OwnMaterials + "PR_Mid_Parquet");
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Mid_MatteBrass");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                BallroomDoor(station, door, cache);
                if (station.Game == null) continue;
                var game = station.Game.transform;
                int them = station.Team == Teams.White ? Teams.Black : Teams.White;
                var costume = Color.Lerp(MiniGamePalaceArt.TeamColor(them), Color.white, .12f);
                foreach (var t in game.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Statue ") && int.TryParse(t.name.Substring(7), out int number))
                        Dancer(t, number - 1, gown, coat, key, costume, cache);
                    else if (t.name.StartsWith("Lane ") && t.name.Contains(" square ") && parquet != null)
                    {
                        var r = t.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = parquet;
                    }
                    else if (t.name == "Tray rim W" || t.name == "Tray rim E") Hide(t);
                    else if (t.name == "Tray" && bench != null)
                    {
                        Hide(t);
                        if (game.Find("MODEL_Bench") == null)
                        {
                            var at = new GameObject("MODEL_Bench").transform;
                            at.SetParent(game, false);
                            at.localPosition = new Vector3(t.localPosition.x, 0f, t.localPosition.z);
                            Hang(at, RideName, bench, cache);
                        }
                    }
                    else if (t.name == "DEC_Bench") Hide(t);      // R312's small velvet bench by the tray
                    // the dance floor's border: matte brass as the concept's thin strip (bright gold is for the game's
                    // own signals: the rings under the stepped dancers)
                    else if (t.name.StartsWith("Rim ") && brass != null)
                    {
                        var r = t.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = brass;
                    }
                }
                // the concept's spotlight on the dancer that has just stepped (lights are code, user rule 11-07)
                if (station.GetComponent<MiniGameBallroomSpots>() == null) station.gameObject.AddComponent<MiniGameBallroomSpots>();
            }
            // the game sign over each team floor: walnut board in a matte brass frame, as B's (same palace stage)
            DressRepairWingSigns(owner, null);
        }

        static void BallroomHall(MiniGameSection owner, Transform content, Dictionary<string, Material> cache)
        {
            var hall = Load(BallroomFolder + "F_HallEnv");
            if (hall == null) return;
            if (content.Find("MODEL_Hall") == null) Hang(content, "MODEL_Hall", hall, cache);
            Hide(content.Find(MiniGamePalaceArt.ShellName));
            var lights = Load("Mid/E/E_LightsEnv");
            if (lights != null && content.Find("MODEL_Lights") == null)
            {
                Hang(content, "MODEL_Lights", lights, cache);
                MiniGameFixtures.LightSockets(content.Find("MODEL_Lights"));
            }
            LibraryFloors(owner, content);
            var arch = Load("Mid/E/E_DoorArch");
            if (arch != null)
                foreach (float x in new[] { -12f, 12f })
                {
                    string name = x < 0f ? "MODEL_TeamArch_W" : "MODEL_TeamArch_E";
                    if (content.Find(name) != null) continue;
                    var at = new GameObject(name).transform;
                    at.SetParent(content, false);
                    at.localPosition = new Vector3(x, 0f, 23.8f);
                    Hang(at, RideName, arch, cache);
                }
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                float x = station.transform.localPosition.x;
                string name = x < 0f ? "MODEL_Pennant_W" : "MODEL_Pennant_E";
                if (content.Find(name) == null)
                    MiniGameFixtures.Pennant(content, name, new Vector3(x, 7.0f, 23.25f), 2.0f, 1.6f, MiniGamePalaceArt.TeamColor(station.Team));
            }
            if (content.Find("MODEL_Props") == null)
            {
                var props = new GameObject("MODEL_Props").transform;
                props.SetParent(content, false);
                var models = new Dictionary<string, GameObject>();
                int i = 0;
                foreach (var p in BallroomProps())
                {
                    if (!models.TryGetValue(p.Model, out var model)) models[p.Model] = model = Load(BallroomFolder + p.Model);
                    if (model == null) continue;
                    var at = new GameObject(p.Model + " " + i++).transform;
                    at.SetParent(props, false);
                    at.localPosition = p.At;
                    // the models face -z after Hang; turn them to face p.Face
                    at.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-p.Face.x, -p.Face.y) * Mathf.Rad2Deg, 0f);
                    Hang(at, RideName, model, cache);
                }
            }
            BallroomDrapes(content);
            if (content.Find("MODEL_MirrorProbe") == null)
            {
                var probe = new GameObject("MODEL_MirrorProbe");
                probe.transform.SetParent(content, false);
                probe.transform.localPosition = new Vector3(0f, 6.5f, 18f);
                probe.AddComponent<MiniGameMirrorProbe>();
            }
        }

        // The concept's tied-back drapes either side of the tall ground windows (F_01), in code as every hanging cloth
        // (user rule 11-07): cream canvas (PR_Mid_Canvas, two-sided) on a matte brass rod over each window, pleated and
        // drawn in at a brass tie-back, swaying gently (SwayingCloth). The ground windows' centres are at z 2.5 .. 22.5
        // (e_hall_v1.py SIDE_Z), about 1 m either side of the centre open (the window's half width), top 4.0 m; the
        // drapes hang 0.12 m off the wall face (|x| 22), their hems over the station floor (0.4 m) where it reaches.
        static void BallroomDrapes(Transform content)
        {
            if (content.Find("MODEL_Drapes") != null) return;
            var canvas = Resources.Load<Material>(OwnMaterials + "PR_Mid_Canvas");
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Mid_MatteBrass");
            if (canvas == null || brass == null) return;
            var root = new GameObject("MODEL_Drapes").transform;
            root.SetParent(content, false);
            const float rod = 4.5f, width = .75f, height = 3.95f;
            foreach (float s in new[] { -1f, 1f })
                foreach (float z in new[] { 2.5f, 7.5f, 12.5f, 17.5f, 22.5f })
                {
                    var window = new GameObject("Window drapes").transform;
                    window.SetParent(root, false);
                    window.localPosition = new Vector3(s * 21.88f, rod, z);
                    // local -z toward the room, local x along the wall
                    window.localRotation = Quaternion.Euler(0f, s * 90f, 0f);
                    var bar = DrapePart(PrimitiveType.Cylinder, window, "Drape rod", new Vector3(0f, .05f, .02f), new Vector3(.06f, 1.75f, .06f), brass);
                    bar.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    foreach (float x in new[] { -1.78f, 1.78f })
                        DrapePart(PrimitiveType.Sphere, window, "Rod finial", new Vector3(x, .05f, .02f), Vector3.one * .14f, brass);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var cloth = new GameObject("Drape");
                        cloth.transform.SetParent(window, false);
                        cloth.transform.localPosition = new Vector3(side * (1f + width * .5f), -height, 0f);
                        cloth.AddComponent<MeshFilter>();
                        cloth.AddComponent<MeshRenderer>().sharedMaterial = canvas;
                        cloth.AddComponent<SwayingCloth>().Configure(width, height, 0f, false, .035f, .45f);
                        // the brass tie-back round the drawn-in waist
                        DrapePart(PrimitiveType.Cylinder, cloth.transform, "Drape tie-back", new Vector3(0f, height * .35f, -.02f),
                                  new Vector3(width * .62f, .045f, .09f), brass).localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                }
        }

        static Transform DrapePart(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        // The mirror door rides the gate's own leaf group (DEC_PalaceGate, unscaled, at the gate box's centre 2.3 m
        // up), so it moves with the game's gate; the code-built leaf, its brass frame and the R312 mirror insert stop
        // drawing.
        static void BallroomDoor(MissionStation station, GameObject door, Dictionary<string, Material> cache)
        {
            if (door == null || station.Gate == null) return;
            var art = station.Gate.transform.Find("DEC_PalaceGate");
            if (art == null) return;
            foreach (Transform part in art)
                if (part.name != RideName) Hide(part);
            if (art.Find(RideName) != null) return;
            var at = new GameObject(RideName).transform;
            at.SetParent(art, false);
            at.localPosition = new Vector3(0f, -2.275f, 0f);    // the leaf's foot (4.55 m leaf, centred)
            Hang(at, "MODEL_MirrorDoor", door, cache);
            // user, 11-08 (the probe showed the sky: realtime probes are off in the quality settings): "여기는 실제로
            // 반사 되게 유리 넣고" - the door's glass is a live reflection
            foreach (var r in at.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("MIRROR_Surface") && r.GetComponent<MiniGameMirrorGlass>() == null)
                    r.gameObject.AddComponent<MiniGameMirrorGlass>();
        }

        // One statue: the game's turned pawn body and R312's costume stop drawing (not the team band "Ring" or the
        // capture halo line), the doll rides the Look (so the warning bob, the landing hop and a bounce's shake move it),
        // and MiniGameDancerLook follows the game's own glow and gives the key its turn.
        static void Dancer(Transform statue, int index, GameObject gown, GameObject coat, GameObject key, Color costume,
                           Dictionary<string, Material> cache)
        {
            var look = statue.Find("Look");
            if (look == null) return;
            // a chequer of gowns and tailcoats over the 4 x 4 grid (index row-first, 4 per row)
            var model = ((index % 4) + (index / 4)) % 2 == 0 ? gown : coat;
            if (model == null) return;
            Renderer skin = null;
            foreach (var r in look.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Ring" || r is LineRenderer || r.transform.IsChildOf(look.Find(RideName) ?? statue)) continue;
                if (skin == null && r.transform.parent == look) skin = r;
                r.enabled = false;
            }
            if (look.Find(RideName) != null) return;
            Hang(look, RideName, model, cache);
            Transform spin = null;
            if (key != null)
            {
                spin = new GameObject("MODEL_Key").transform;
                spin.SetParent(look, false);
                spin.localPosition = DancerSocket;
                Hang(spin, RideName, key, cache);
            }
            statue.gameObject.AddComponent<MiniGameDancerLook>().Configure(look.Find(RideName), skin, spin, costume, index);
        }
    }

    // A dancer doll following its statue (picture only): the costume slot (F_DancerCostume) takes the other team's
    // colour, warmed toward gold while the game glows the statue (its capture window: MgEnPassant.Paint swaps the hidden
    // body's material to "M_PieceWindow"), and the wind-up key turns, faster in the window. No extra network state:
    // the body material is set from the shared clock on every PC.
    public sealed class MiniGameDancerLook : MonoBehaviour
    {
        Renderer skin;
        Transform key;
        readonly List<(Renderer r, int slot)> cloth = new List<(Renderer, int)>();
        Color costume, glow;
        MaterialPropertyBlock block;
        bool lit, first = true;
        float angle;

        // the game's capture window is open on this statue (MiniGameBallroomSpots lights it)
        public bool Lit => lit;

        public void Configure(Transform doll, Renderer gameSkin, Transform windKey, Color tint, int index)
        {
            skin = gameSkin;
            key = windKey;
            costume = tint;
            glow = Color.Lerp(tint, new Color(1f, .82f, .4f), .45f) * 1.25f;
            angle = index * 37f;
            if (doll != null)
                foreach (var r in doll.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.StartsWith("F_DancerCostume")) cloth.Add((r, i));
                }
            Paint(false);
        }

        void Paint(bool on)
        {
            lit = on;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var (r, slot) in cloth)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block, slot);
                block.SetColor("_BaseColor", on ? glow : costume);
                r.SetPropertyBlock(block, slot);
            }
        }

        void LateUpdate()
        {
            bool on = skin != null && skin.sharedMaterial != null && skin.sharedMaterial.name.StartsWith("M_PieceWindow");
            if (on != lit || first) { first = false; Paint(on); }
            if (key == null) return;
            angle = Mathf.Repeat(angle + Time.deltaTime * (on ? 540f : 90f), 360f);
            key.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // One box-projected reflection probe over the ballroom for the gallery's wall mirrors (user, 11-08: "방만 비침").
    // Realtime probes are off in the project's quality settings (both levels), so a realtime probe was never used and
    // the mirrors showed the default sky (user, 11-08 screenshot). Instead the room is rendered once into a cubemap at
    // the start of play, with the dancers and every pawn hidden, and given to a Custom probe (honoured whatever that
    // setting): the mirrors show the walls, chandeliers and windows but no piece twice. It is not refreshed.
    public sealed class MiniGameMirrorProbe : MonoBehaviour
    {
        ReflectionProbe probe;
        RenderTexture cube;

        void Awake()
        {
            probe = gameObject.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.boxProjection = true;
            probe.size = new Vector3(44f, 13f, 36f);
            probe.center = new Vector3(0f, 0f, 0f);
            probe.importance = 2;
        }

        IEnumerator Start()
        {
            if (!Application.isPlaying) yield break;
            yield return null;      // the games and the models are in by the first frame
            var hidden = new List<Renderer>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !Moving(r.transform)) continue;
                r.enabled = false;
                hidden.Add(r);
            }
            cube = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "Ballroom mirror probe", dimension = UnityEngine.Rendering.TextureDimension.Cube,
                useMipMap = true, autoGenerateMips = true
            };
            var go = new GameObject("Ballroom probe camera");
            go.transform.position = transform.position;
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 80f;
            bool ok = cam.RenderToCubemap(cube);
            Destroy(go);
            foreach (var r in hidden) if (r != null) r.enabled = true;
            if (ok) probe.customBakedTexture = cube;
            else Debug.LogWarning("[미니게임 모델] 무도회장 거울 반사(큐브맵)를 만들지 못했습니다");
        }

        void OnDestroy()
        {
            if (cube != null) { cube.Release(); Destroy(cube); }
        }

        // the statues (with their dolls and keys) and anything carrying a character driver (the players' pawns)
        static bool Moving(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name.StartsWith("Statue ")) return true;
                if (p.GetComponent<ICharacterDriver>() != null) return true;
            }
            return false;
        }
    }

    // The mirror door's live glass (user, 11-08: "여기는 실제로 반사 되게 유리 넣고"), as the title screen's polished floor
    // (IntroStage.Gloss): a camera stands mirrored behind the glass, copying the view's lens, and renders into a texture
    // at half the screen; Resources/PawnRushMiniGames/Fx/PR_MirrorGlass.shader lays it on the glass at the screen
    // position (left-right turned). Its near plane is the glass itself (an oblique projection), so the tunnel and the
    // rear corridor behind the door never show. Everything in the room is reflected, pawns and dancers too. It renders
    // only while the glass is on screen and the view is within 60 m; it follows the door as the gate moves.
    public sealed class MiniGameMirrorGlass : MonoBehaviour
    {
        const string ShaderPath = "PawnRushMiniGames/Fx/PR_MirrorGlass";
        static Shader shader;

        Renderer surface;
        Material glass;
        Camera mirror;
        RenderTexture picture;
        Vector3 localNormal = Vector3.back;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { shader = null; }

        void Start()
        {
            surface = GetComponent<Renderer>();
            if (!Application.isPlaying || surface == null) { enabled = false; return; }
            if (shader == null) shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning("[미니게임 모델] 거울 유리 셰이더가 없어 반사를 그리지 않습니다: Resources/" + ShaderPath);
                enabled = false;
                return;
            }
            glass = new Material(shader) { name = "Mirror door glass" };
            surface.sharedMaterial = glass;
            var mesh = GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh : null;
            if (mesh != null && mesh.normals.Length > 0) localNormal = mesh.normals[0];
            var go = new GameObject("Mirror door camera");
            mirror = go.AddComponent<Camera>();
            mirror.enabled = false;
            mirror.allowHDR = false;
            mirror.allowMSAA = false;
            mirror.clearFlags = CameraClearFlags.Skybox;
        }

        void LateUpdate()
        {
            var view = Camera.main;
            if (view == null || mirror == null) return;
            var centre = surface.bounds.center;
            bool on = surface.isVisible && (view.transform.position - centre).sqrMagnitude < 60f * 60f;
            mirror.enabled = on;
            if (!on) return;
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(64, Screen.height / 2);
            if (picture == null || picture.width != w || picture.height != h)
            {
                if (picture != null) { mirror.targetTexture = null; picture.Release(); Destroy(picture); }
                picture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "Mirror door picture" };
                mirror.targetTexture = picture;
                glass.SetTexture("_ReflTex", picture);
            }
            var n = transform.TransformDirection(localNormal).normalized;
            if (Vector3.Dot(view.transform.position - centre, n) < 0f) { mirror.enabled = false; return; }   // seen from behind
            Vector3 Reflect(Vector3 v) => v - 2f * Vector3.Dot(v, n) * n;
            var eye = view.transform.position;
            var at = eye - 2f * Vector3.Dot(eye - centre, n) * n;
            mirror.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Reflect(view.transform.forward), Reflect(view.transform.up)));
            mirror.depth = view.depth - 1;
            mirror.cullingMask = view.cullingMask;
            mirror.ResetProjectionMatrix();
            mirror.fieldOfView = view.fieldOfView;
            mirror.aspect = view.aspect;
            mirror.nearClipPlane = view.nearClipPlane;
            mirror.farClipPlane = view.farClipPlane;
            // the glass as the near plane (2 cm into the room, so the glass itself is not drawn)
            var m = mirror.worldToCameraMatrix;
            var cp = m.MultiplyPoint(centre + n * .02f);
            var cn = m.MultiplyVector(n).normalized;
            mirror.projectionMatrix = mirror.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
        }

        void OnDestroy()
        {
            if (mirror != null) { mirror.targetTexture = null; Destroy(mirror.gameObject); }
            if (picture != null) { picture.Release(); Destroy(picture); }
            if (glass != null) Destroy(glass);
        }
    }

    // The concept's spotlight (F_01: a soft warm beam on the dancer that has just stepped; all dancers are never lit at
    // once, R311 modelling notes): up to three warm spot lights per team floor follow the statues whose capture window is
    // open (MiniGameDancerLook.Lit, from the shared clock on every PC), each from 8 m above and a little to the south,
    // fading in and out. Lights are code (user rule 11-07); the ceiling spotlight models are their dressing.
    public sealed class MiniGameBallroomSpots : MonoBehaviour
    {
        const int Count = 3;
        const float Peak = 7f;
        readonly Light[] lights = new Light[Count];
        readonly MiniGameDancerLook[] target = new MiniGameDancerLook[Count];
        readonly float[] level = new float[Count];
        MiniGameDancerLook[] dancers;
        MissionStation station;

        void Start()
        {
            if (!Application.isPlaying) { enabled = false; return; }
            station = GetComponent<MissionStation>();
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Ballroom spot " + (i + 1));
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, .9f, .72f);
                l.range = 14f;
                l.spotAngle = 24f;
                l.innerSpotAngle = 12f;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                lights[i] = l;
            }
        }

        void LateUpdate()
        {
            if (station == null) return;
            if (dancers == null || dancers.Length == 0)
                dancers = station.Game != null ? station.Game.GetComponentsInChildren<MiniGameDancerLook>(true) : new MiniGameDancerLook[0];
            // keep the lit ones, free the rest, take new lit ones into free lights
            for (int i = 0; i < Count; i++)
                if (target[i] != null && !target[i].Lit && level[i] <= 0f) target[i] = null;
            foreach (var d in dancers)
            {
                if (d == null || !d.Lit || System.Array.IndexOf(target, d) >= 0) continue;
                for (int i = 0; i < Count; i++)
                    if (target[i] == null) { target[i] = d; break; }
            }
            for (int i = 0; i < Count; i++)
            {
                var l = lights[i];
                bool want = target[i] != null && target[i].Lit;
                level[i] = Mathf.MoveTowards(level[i], want ? 1f : 0f, Time.deltaTime * (want ? 6f : 2.5f));
                l.enabled = level[i] > 0f;
                if (!l.enabled || target[i] == null) continue;
                var at = target[i].transform.position + Vector3.up * .5f;
                l.transform.position = at + Vector3.up * 8f - transform.forward * 2.5f;
                l.transform.rotation = Quaternion.LookRotation(at - l.transform.position);
                l.intensity = Peak * level[i] * level[i];
            }
        }
    }
}
