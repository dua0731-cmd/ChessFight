using System;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The single-use promotion pad beside a station's mini-game (R125, in place of the four placeholder pads of
    // design doc v0.4 §5's promotion zones). Every piece starts as a pawn; the first pawn of the station's team to
    // stand on the pad for PawnRushPromotion.HoldSeconds becomes the pad's piece (and gets its Pawn Rush skill),
    // and the pad is spent for the round. Stepping off, or being pushed off, starts the count over; the other
    // team cannot use it.
    //
    // R130 (user: "승격하는 플레이어가 원하는 기물로 선택, 로그라이크 게임처럼 기물 목록이 화면에 UI로"): the pad no
    // longer gives a set piece. After the 2 s the pad is spent and held for that pawn, and its player chooses from
    // cards (PromotionChooser: queen, rook, bishop, knight); nobody choosing within ChooseSeconds gets a random one.
    // The host judges: its own player's choice comes from its screen, another player's in the lab input
    // (pieceAsk / pieceSeq), and the course state tells every PC who is choosing on which pad (CFP4).
    //
    // The station makes it when Play starts (MissionStation.Start), so the scene built before R125 has it too.
    // The round's piece comes from MissionPicker (both teams' pads in a plaza give the same piece). Online only
    // the host judges it; the other PCs show what the host sends (ApplyRemote), like the mini-games.
    //
    // R516: every palace mini-game section's two stations have one as well (the section round, the mini-game prefab
    // test), in the same station-local place - the outer side of the entry strip, on the dais every game lays from u 0
    // across the station's full width - so a round has six: one a team in each of its three mini-games. Online the
    // section round sends their spent bits and choosers in the course state (MatchCourse's RoundMatch).
    public sealed class StationPromotion : MonoBehaviour
    {
        // Station-local: u (north) and v (across) of the pad's middle, and half its side. On the entry strip
        // (u 0..2, design doc v0.4 §5) and clear of every game: A's board starts at u 2, B's plank piles stand at
        // v ±8, u 3 (picked up within 1.9 m), E's first towers at v ±5, u 5, C's rope path is v ±1.6.
        public const float U = 1f, VOuter = 4.5f, Half = 1f, MaxHeight = 2.4f;

        // A promotion that happened (offline or on the host): the playtest's log, the online link's note.
        public static event Action<StationPromotion, ICharacterDriver, PieceKind> Promoted;

        static readonly System.Collections.Generic.List<StationPromotion> all = new System.Collections.Generic.List<StationPromotion>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Promoted = null;
            all.Clear();
        }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable()
        {
            all.Remove(this);
            if (charge != null) { ArenaFx.Cut(charge); charge = null; }
        }

        MissionStation station;
        PieceKind piece = PieceKind.Queen;
        bool used;
        ICharacterDriver standing;
        float standFor;
        // Who chooses the piece now (the host's, or the host's word on a client) and until when (the host's).
        ICharacterDriver choosing;
        float chooseUntil;

        Transform marker, fill;
        Renderer slab;
        TextMesh label;
        Material slabOn, slabOff, fillMaterial;
        PieceKind shownPiece = (PieceKind)255;
        bool shownUsed;
        // 11-22: the shared pressure-plate model (palace rooms, ArtSource/PawnRush/MiniGames/Common/promotion_pad_v2.py) in place
        // of the code slab, frame, bar and label: its plate dims when spent, its 24 frame studs light up over the hold.
        Transform codeLook;
        GameObject model;
        Renderer modelPlate;
        Renderer[] ring;
        Material ringOn, ringOff, plateOn, plateOff;
        int ringShown = -1;

        public MissionStation Station => station;
        public PieceKind Piece => piece;
        public bool Used => used;
        // How far the pawn on it is (0..1), for the fill.
        public float Hold01 => used ? 1f : Mathf.Clamp01(standFor / PawnRushPromotion.HoldSeconds);
        public ICharacterDriver Standing => standing;
        public ICharacterDriver Choosing => choosing;

        // The pad on which this pawn is choosing its piece, or null.
        public static StationPromotion ChoosingFor(ICharacterDriver driver)
        {
            if (driver == null) return null;
            foreach (var pad in all) if (pad != null && pad.choosing == driver) return pad;
            return null;
        }

        // The host (or offline): the chooser's answer. False when it is not this pawn's choice to make.
        public static bool Choose(ICharacterDriver driver, PieceKind piece)
        {
            var pad = ChoosingFor(driver);
            return pad != null && !MiniGameBase.Remote && pad.Give(piece);
        }

        bool Give(PieceKind kind)
        {
            if (choosing == null || !PawnRushPromotion.Gives(kind)) return false;
            var who = choosing;
            choosing = null;
            if (!(who is IPromotable promotable) || !PawnRushPromotion.CanPromote(promotable.Piece)) { Refresh(); return false; }
            promotable.Promote(kind);
            Refresh();
            Debug.Log($"[PawnRush] 승격: 광장 {station.Plaza} {Teams.Name(station.Team)}팀 발판 → {ChessPieces.Name(kind)} (선택)");
            Promoted?.Invoke(this, who, kind);
            return true;
        }

        // Black's station is the mirror of white's: the pad on the outer side of both.
        public static Vector3 LocalMiddle(bool mirrored) => new Vector3((mirrored ? 1f : -1f) * VOuter, Arena.DaisHeight, U);

        public static StationPromotion Make(MissionStation station)
        {
            var go = new GameObject("Promotion pad (single use)");
            go.transform.SetParent(station.transform, false);
            go.transform.localPosition = LocalMiddle(station.Mirrored);
            var pad = go.AddComponent<StationPromotion>();
            pad.station = station;
            pad.Build();
            return pad;
        }

        public void SetPiece(PieceKind kind)
        {
            if (!PawnRushPromotion.Gives(kind)) return;
            piece = kind;
            Refresh();
        }

        // A new round (or the same one again): unused, nobody counting.
        public void ResetPad()
        {
            used = false;
            standing = null;
            standFor = 0f;
            choosing = null;
            Refresh();
        }

        // The host's word on this pad (online, not the host): its piece, spent, and who is choosing on it.
        public void ApplyRemote(PieceKind kind, bool spent, ICharacterDriver chooser = null)
        {
            if (PawnRushPromotion.Gives(kind)) piece = kind;
            used = spent;
            standing = null;
            standFor = 0f;
            choosing = chooser;
            if (chooser != null) PromotionChooser.Ensure();
            Refresh();
        }

        // The seat of whoever chooses on this pad, for the course state.
        public byte ChooserSeat => choosing != null && MiniGameBase.SeatOf != null ? MiniGameBase.SeatOf(choosing) : PawnRushNetState.NoChooser;

        // 11-22 (user "승격 발판 차징 사운드 넣어"): the 2 s on the pad as this PC shows and hears it. The host (or offline)
        // counts every pawn (standFor); a client was never told, so its frame lamps never lit - it now counts its own pawn
        // the same way (the pad's look and sound only: the host still decides the promotion).
        float localFor;
        AudioSource charge;

        float Shown01 => used ? 1f : MiniGameBase.Remote ? Mathf.Clamp01(localFor / PawnRushPromotion.HoldSeconds) : Hold01;

        bool OnPad(ICharacterDriver driver)
        {
            if (driver == null || driver.FollowTarget == null) return false;
            if (!(driver is ITeamMember member) || member.Team != station.Team) return false;
            if (!(driver is IPromotable promotable) || !PawnRushPromotion.CanPromote(promotable.Piece)) return false;
            var local = station.transform.InverseTransformPoint(driver.FollowTarget.position) - new Vector3(0f, Arena.DaisHeight, 0f);
            Vector3 middle = transform.localPosition;
            return Mathf.Abs(local.x - middle.x) <= Half && Mathf.Abs(local.z - middle.z) <= Half && local.y >= -.5f && local.y <= MaxHeight;
        }

        // The charge's sound at the pad while the count runs (sfx_promo_charge: 2 s rising, a tick per lamp; baked by
        // ArtSource/PawnRush/Sfx/bake_promotion.py), cut the moment the pawn steps off; the cards' chime follows.
        void ChargeSound(float k)
        {
            bool charging = !used && choosing == null && k > 0f && k < 1f;
            if (!charging)
            {
                if (charge != null) { ArenaFx.Cut(charge); charge = null; }
                return;
            }
            if (charge != null && charge.isPlaying) return;
            if (k > .97f) return;
            charge = ArenaFx.Hold("sfx_promo_charge", transform.position + Vector3.up * .5f, 1f, .7f);
            if (charge == null || charge.clip == null) return;
            charge.pitch = 1f;   // in step with the lamps (Hold varies the pitch by 5 %)
            charge.time = Mathf.Min(k * PawnRushPromotion.HoldSeconds, charge.clip.length - .05f);
        }

        void FixedUpdate()
        {
            if (station != null && MiniGameBase.Remote)
            {
                bool on = !used && choosing == null && OnPad(MiniGameBase.LocalDriver);
                localFor = on ? Mathf.Min(localFor + Time.fixedDeltaTime, PawnRushPromotion.HoldSeconds) : 0f;
                return;
            }
            if (station == null) return;
            if (choosing != null && Time.time >= chooseUntil)
            {
                // Nobody chose in time: a random piece, so the pad is never held for good.
                Give(PawnRushPromotion.Pieces[UnityEngine.Random.Range(0, PawnRushPromotion.Pieces.Length)]);
                return;
            }
            if (used) return;
            ICharacterDriver found = null, first = null;
            var pawns = station.Pawns();
            Vector3 middle = transform.localPosition;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (!p.Own || !(p.Driver is IPromotable promotable) || !PawnRushPromotion.CanPromote(promotable.Piece)) continue;
                if (Mathf.Abs(p.Local.x - middle.x) > Half || Mathf.Abs(p.Local.z - middle.z) > Half || p.Local.y < -0.5f || p.Local.y > MaxHeight) continue;
                if (first == null) first = p.Driver;
                if (p.Driver == standing) found = p.Driver;
            }
            if (found == null)
            {
                // Whoever was counting stepped off (or was pushed): start over with whoever is on it now.
                standing = first;
                standFor = 0f;
                return;
            }
            standFor += Time.fixedDeltaTime;
            if (standFor < PawnRushPromotion.HoldSeconds) return;
            // Spent and held for this pawn: its player picks the piece (PromotionChooser).
            used = true;
            choosing = found;
            chooseUntil = Time.time + PawnRushPromotion.ChooseSeconds;
            standing = null;
            standFor = 0f;
            Refresh();
            PromotionChooser.Ensure();
        }

        // ------------------------------------------------------------------ the look

        void Build()
        {
            var kit = station.Kit;
            var shader = LitMaterials.Lit;
            slabOn = kit != null && kit.rankGold != null ? kit.rankGold : new Material(shader) { color = new Color(.95f, .78f, .25f) };
            slabOff = kit != null && kit.trimBlack != null ? kit.trimBlack : new Material(shader) { color = new Color(.25f, .25f, .27f) };
            fillMaterial = new Material(shader) { color = new Color(1f, .97f, .85f) };

            // A low slab on the floor (no collider: the floor carries the pawns), a frame in the team's colour - the palace
            // sections' blue and orange there (R516), the course's own trim elsewhere.
            codeLook = new GameObject("Code look").transform;
            codeLook.SetParent(transform, false);
            slab = Part(PrimitiveType.Cube, codeLook, new Vector3(0f, .03f, 0f), new Vector3(Half * 2f, .06f, Half * 2f), slabOn).GetComponent<Renderer>();
            var frame = station.PalaceStyle ? Arena.Lit("PR_MG_PromotionFrame", station.Accent(station.Team))
                      : kit != null ? kit.Team(station.Team) : null;
            if (frame != null)
                for (int i = 0; i < 4; i++)
                {
                    bool alongX = i < 2;
                    float side = (i % 2 == 0 ? -1f : 1f) * (Half + .06f);
                    Part(PrimitiveType.Cube, codeLook, alongX ? new Vector3(0f, .04f, side) : new Vector3(side, .04f, 0f),
                         alongX ? new Vector3(Half * 2f + .24f, .08f, .12f) : new Vector3(.12f, .08f, Half * 2f + .24f), frame);
                }
            fill = Part(PrimitiveType.Cube, codeLook, new Vector3(0f, .065f, -Half + .01f), new Vector3(Half * 2f - .2f, .012f, .02f), fillMaterial).transform;

            marker = new GameObject("Piece").transform;
            marker.SetParent(transform, false);
            marker.localPosition = new Vector3(0f, MarkerY, 0f);
            BuildBeam();

            var text = new GameObject("Label");
            text.transform.SetParent(transform, false);
            text.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            label = text.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .09f;
            label.fontSize = 48;
            var font = RuntimePanels.KoreanFont;
            if (font != null)
            {
                label.font = font;
                text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            if (station.PalaceStyle) DressModel(frame);
            Refresh();
        }

        public const string ModelPath = "PawnRushMiniGames/Common/PR_PromotionPad";

        // The pressure-plate model over the code look (11-22: Codex design 2, user "2번으로 해서 정밀하게 재질 똑같이";
        // ArtSource/PawnRush/MiniGames/Common/promotion_pad_v2.py): the code slab, frame, bar and the floating label go
        // (the label showed through walls). Brushed gold plate, dark veined stone frame, cream marble corners with gold
        // rivets, a pale team band under it all, and 24 gold studs on the frame that light one by one over the hold. No
        // collider: the model is 5.6 cm (7.4 at the rivets) and the dais carries the pawns. Missing model: the code look.
        static Material padGold, padSpent, padStone, padMarble, padStud, padLamp, padRecess;

        static Material Textured(string name, string texture, Color tint, float smoothness, float metallic)
        {
            var m = LitMaterials.NewLit(tint, smoothness, metallic);
            m.name = name;
            var tex = Resources.Load<Texture2D>("PawnRushMiniGames/Common/" + texture);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
            }
            return m;
        }

        static void PadMaterials()
        {
            if (padGold != null) return;
            padGold = Textured("PR_MG_PromoPad_Gold", "T_PromoPad_Gold", Color.white, .72f, .9f);
            LitMaterials.SetEmission(padGold, Arena.Gold * .25f);
            padSpent = Textured("PR_MG_PromoPad_Spent", "T_PromoPad_Gold", Arena.Hex("#5B4F3C"), .35f, .6f);
            padStone = Textured("PR_MG_PromoPad_Stone", "T_PromoPad_Stone", Color.white, .42f, 0f);
            padMarble = Textured("PR_MG_PromoPad_Marble", "T_PromoPad_Marble", Color.white, .6f, 0f);
            padStud = LitMaterials.NewLit(Arena.Hex("#D9A537"), .7f, 1f);
            padStud.name = "PR_MG_PromoPad_Stud";
            padLamp = Arena.Lit("PR_MG_PromoPad_Lamp", Arena.Hex("#FFC94A"), 2.2f, .3f);
            padRecess = Arena.Lit("PR_MG_PromoPad_Recess", Arena.Hex("#1D1B1A"), 0f, .6f);
        }

        void DressModel(Material team)
        {
            var source = Resources.Load<GameObject>(ModelPath);
            if (source == null)
            {
                Debug.LogWarning("[PawnRush] 승격 발판 모델을 불러오지 못했습니다: Resources/" + ModelPath + ".fbx — 코드로 만든 발판을 씁니다");
                return;
            }
            // Under a pivot turned 180 degrees about y, as the other mini-game models (Blender x, y, z = this x, z, y).
            var pivot = new GameObject("Model").transform;
            pivot.SetParent(transform, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model = Instantiate(source, pivot, false);
            model.name = source.name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);

            PadMaterials();
            plateOn = padGold;
            plateOff = padSpent;
            ringOn = padLamp;
            ringOff = padStud;
            // the concept's band is the team colour paled towards white
            var band = Arena.Lit("PR_MG_PromoPad_Band", Color.Lerp(station.Accent(station.Team), Color.white, .45f), 0f, .55f);

            var lamps = new System.Collections.Generic.SortedDictionary<string, Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                Material m = n.Contains("_Lamp_") ? padStud : n.EndsWith("_Plate") ? padGold : n.EndsWith("_Frame") ? padStone
                           : n.EndsWith("_Corners") ? padMarble : n.EndsWith("_Rivets") ? padStud : n.EndsWith("_Recess") ? padRecess
                           : n.EndsWith("_Base") ? band : null;
                if (m == null) continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = m;
                r.sharedMaterials = slots;
                if (n.Contains("_Lamp_")) lamps[n] = r;
                else if (n.EndsWith("_Plate")) modelPlate = r;
            }
            ring = new Renderer[lamps.Count];
            lamps.Values.CopyTo(ring, 0);   // PR_PromoPad_Lamp_00 (north-west) onwards, clockwise from above
            codeLook.gameObject.SetActive(false);
            label.gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (slab == null) return;
            slab.sharedMaterial = used ? slabOff : slabOn;
            if (modelPlate != null) modelPlate.sharedMaterial = used ? plateOff : plateOn;
            marker.gameObject.SetActive(!used);
            if (projection != null) projection.gameObject.SetActive(!used);
            shownUsed = used;
            label.text = choosing != null ? Loc.T("승격 · 기물 고르는 중")
                       : used ? Loc.T("승격 발판 · 사용함")
                       : Loc.F("승격 발판 · 원하는 기물 선택\n폰이 {0:0}초 서 있기", PawnRushPromotion.HoldSeconds);
            label.color = used ? new Color(.7f, .7f, .7f) : new Color(1f, .9f, .45f);
        }

        void LateUpdate()
        {
            if (marker == null) return;
            if (used != shownUsed) Refresh();
            // The gold piece over the pad shows each choice in turn (the player picks one).
            var cycle = PawnRushPromotion.Pieces[(int)(Time.time / 1.2f) % PawnRushPromotion.Pieces.Length];
            if (!used && cycle != shownPiece)
            {
                shownPiece = cycle;
                for (int i = marker.childCount - 1; i >= 0; i--) Destroy(marker.GetChild(i).gameObject);
                BuildPiece(marker, cycle, station.Team, slabOn);
            }
            marker.localRotation = Quaternion.Euler(0f, Time.time * 60f, 0f);
            marker.localPosition = new Vector3(0f, MarkerY + Mathf.Sin(Time.time * 2f) * MarkerBob, 0f);
            float k = Shown01;
            ChargeSound(k);
            if (ring != null)
            {
                // The frame studs light one by one while a pawn holds the pad; all out again once it is spent.
                int lit = used ? 0 : Mathf.Clamp(Mathf.CeilToInt(k * ring.Length - .001f), 0, ring.Length);
                if (lit != ringShown)
                {
                    ringShown = lit;
                    for (int i = 0; i < ring.Length; i++) ring[i].sharedMaterial = i < lit ? ringOn : ringOff;
                }
            }
            AnimateProjection();
            fill.gameObject.SetActive(!used && k > 0f);
            fill.localScale = new Vector3(Half * 2f - .2f, .012f, Mathf.Max(.02f, (Half * 2f - .2f) * k));
            fill.localPosition = new Vector3(0f, .065f, -Half + .1f + fill.localScale.z * .5f);
            var cam = Camera.main;
            if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, Vector3.up);
        }

        // The piece over the pad, a little under a metre tall, no colliders. 11-22 (user "기물은 실제 흑팀이면 흑팀 백팀이면
        // 백팀 기물을 해"): the piece a player of the pad's team becomes - the same model and the black set's material for
        // black (RagdollPawn.Piece) - else a gold turned piece, else primitives.
        static void BuildPiece(Transform at, PieceKind kind, int team, Material gold)
        {
            if (TeamPiece(at, kind, team, HoloHeight) != null) return;
            // a real turned piece in polished gold when the map kit is in (10-22)
            var real = MapLook.PieceBody(at, "Gold piece", kind, .95f, MapLook.Gold);
            if (real != null)
            {
                real.transform.localPosition = new Vector3(0f, -.48f, 0f);
                return;
            }
            Part(PrimitiveType.Cylinder, at, new Vector3(0f, -.42f, 0f), new Vector3(.5f, .06f, .5f), gold);
            switch (kind)
            {
                case PieceKind.Rook:
                    Part(PrimitiveType.Cylinder, at, new Vector3(0f, -.1f, 0f), new Vector3(.36f, .3f, .36f), gold);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f;
                        Part(PrimitiveType.Cube, at, new Vector3(Mathf.Cos(a) * .15f, .27f, Mathf.Sin(a) * .15f), Vector3.one * .12f, gold);
                    }
                    break;
                case PieceKind.Bishop:
                    Part(PrimitiveType.Capsule, at, new Vector3(0f, -.02f, 0f), new Vector3(.3f, .34f, .3f), gold);
                    Part(PrimitiveType.Sphere, at, new Vector3(0f, .42f, 0f), Vector3.one * .1f, gold);
                    break;
                case PieceKind.Knight:
                    Part(PrimitiveType.Cylinder, at, new Vector3(0f, -.2f, 0f), new Vector3(.3f, .18f, .3f), gold);
                    Part(PrimitiveType.Cube, at, new Vector3(0f, .1f, -.02f), new Vector3(.2f, .42f, .26f), gold).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                    Part(PrimitiveType.Cube, at, new Vector3(0f, .24f, .14f), new Vector3(.17f, .15f, .3f), gold);
                    break;
                default:   // the queen: a body and a crown of five points
                    Part(PrimitiveType.Capsule, at, new Vector3(0f, -.05f, 0f), new Vector3(.32f, .34f, .32f), gold);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        Part(PrimitiveType.Sphere, at, new Vector3(Mathf.Cos(a) * .15f, .34f, Mathf.Sin(a) * .15f), Vector3.one * .08f, gold);
                    }
                    Part(PrimitiveType.Sphere, at, new Vector3(0f, .44f, 0f), Vector3.one * .1f, gold);
                    break;
            }
        }

        // The piece model a player wears in the game (Resources/PieceModels/PawnRush/<piece>, else the shared set) drawn
        // still: its skinned mesh as it was rigged - the pawn skin's space, Blender's z up and in hundredths (a Queen is
        // 0.016 units tall: the skin's transform scales it up). Turned z-up to y-up, scaled to `height` from its own
        // bounds, base on the marker's foot (-0.48 m, as the gold piece), centred. Null (and a warning) without a model.
        static GameObject TeamPiece(Transform at, PieceKind kind, int team, float height)
        {
            var prefab = Resources.Load<GameObject>("PieceModels/PawnRush/" + kind);
            if (prefab == null) prefab = Resources.Load<GameObject>("PieceModels/" + kind);
            var skin = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (skin == null || skin.sharedMesh == null)
            {
                Debug.LogWarning("[PawnRush] 승격 발판: 기물 모델이 없습니다(Resources/PieceModels/PawnRush/" + kind + ") — 금 기물을 씁니다");
                return null;
            }
            var mesh = skin.sharedMesh;
            Bounds b = mesh.bounds;
            // Rigged z-up (the tall axis is z): stand it up. Already y-up: as it is.
            var turn = b.size.z > b.size.y * 1.2f ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = turn * (b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }
            float tall = max.y - min.y;
            if (tall <= 1e-6f) return null;
            float scale = height / tall;
            var go = new GameObject("Team piece (hologram)", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(at, false);
            go.transform.localRotation = turn;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(-(min.x + max.x) * .5f * scale, -.48f - min.y * scale, -(min.z + max.z) * .5f * scale);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            // The one mesh drawn twice: the see-through team body, then the projection's light over it.
            var holo = Hologram(team);
            renderer.sharedMaterials = holo != null ? new[] { HoloBody(team), holo } : new[] { HoloBody(team) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            // The rising band starts on the gold plate, as the beam's does, and runs on up through the piece.
            block.SetFloat("_SweepFrom", (at.parent != null ? at.parent.position.y : at.position.y - MarkerY) + BeamBottom);
            renderer.SetPropertyBlock(block);
            return go;
        }

        // 11-22 (user "백팀은 백 흑팀은 흑 기물이 보일 수 있게 홀로그램 형식으로 띄우고 기물은 실제 인게임에서 플레이하는
        // 기물로 해"): the piece over the pad is the player's own piece model as a hologram in the pad's team colour.
        // ChessFight/Hologram (the entrance screen's, Resources/Hologram) only adds light, so it cannot show black: a
        // see-through body carries the colour - pale white for white, near black for black - and the hologram's fine
        // lines, edge light and rising band go over it (cool white for white, a dim grey-violet for black).
        static readonly Material[] holoBody = new Material[2], holoLight = new Material[2];

        static Material HoloBody(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            if (holoBody[t] != null) return holoBody[t];
            var shader = Shader.Find("Sprites/Default");
            var m = new Material(shader != null ? shader : LitMaterials.Unlit)
            {
                name = t == 1 ? "PR_MG_PromoHolo_BodyBlack" : "PR_MG_PromoHolo_BodyWhite",
                color = t == 1 ? new Color(.02f, .02f, .03f, .8f) : new Color(.93f, .96f, 1f, .3f),
            };
            return holoBody[t] = m;
        }

        // The projection's light over the piece.
        static Material Hologram(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            if (holoLight[t] != null) return holoLight[t];
            var shader = Shader.Find("ChessFight/Hologram");
            if (shader == null) shader = Resources.Load<Shader>("Hologram/Hologram");
            if (shader == null) return null;
            var m = new Material(shader) { name = "PR_MG_PromoHolo_Light" + (t == 1 ? "Black" : "White") };
            Color light = t == 1 ? new Color(.22f, .2f, .3f) : new Color(.62f, .84f, 1f) * 1.3f;
            m.SetColor("_Color", light);
            m.SetFloat("_Fill", .05f);
            m.SetFloat("_Top", .05f);
            m.SetFloat("_Rim", .6f);
            m.SetFloat("_Base", .3f);
            m.SetFloat("_BaseHeight", .15f);
            m.SetFloat("_Scan", .35f);
            m.SetFloat("_Sweep", 1.2f);
            m.SetFloat("_SweepHeight", SweepHeight);
            return holoLight[t] = m;
        }

        // ------------------------------------------------------------------ the beam

        // 11-22 (user "좀만 더 아래에 배치 해주고 크기를 살짝만 키워줘"): the hologram piece is 1.1 m tall (was 0.95) and
        // floats lower, its foot 0.62 m over the pad (was 0.92), bobbing 5 cm (was 10). The foot is PieceFoot under the
        // marker, as the gold piece's always was.
        const float MarkerY = 1.1f, MarkerBob = .05f, HoloHeight = 1.1f, PieceFoot = .48f;

        // 11-22 (user "승격 발판 금태랑 홀로그램 형식으로 이어질 수 있게 처리해"): the projection that joins the gold plate to
        // the hologram piece - an open skirt of light from the emitter's outer ring on the plate, drawing in to a ring under the
        // piece's foot; gold at the plate turning into the team's hologram light at the piece, dimmer as it rises, in
        // faint upright rays, with the hologram's fine lines. One band rises from the plate through the beam and on up
        // through the piece (same start and height on both). No caps and no edge light: the first column (a capped
        // cylinder with the edge light on) read as a glass jar.
        const float BeamBottom = .06f, BeamBase = .68f, BeamTopRadius = .24f;
        const float SweepHeight = MarkerY - PieceFoot + HoloHeight + MarkerBob - BeamBottom;
        static Mesh beamMesh;
        static readonly Material[] beamLight = new Material[2];

        // 11-22 (user "투영 되는 그래픽을 더 좋게 해"): the projection as a projector's, all under one root that goes out
        // with the piece when the pad is spent -
        //   the emitter: a gold lens of rings and ticks lying on the plate, turning slowly (where the light comes from);
        //   the skirt (above), its rays drifting round;
        //   two scan rings that rise from the plate to the piece's foot, gold turning to the team's light, fading in
        //   and out on the way, drawn to the skirt's width at their height;
        //   motes of light rising inside it and drawing in towards the piece;
        //   the receiver: a ring of the team's light under the piece's foot, riding its bob and turning the other way.
        // All of it is added light (ChessFight/Hologram, or additive particles), no colliders, no shadows.
        Transform projection, emitter, receiver;
        Renderer beam;
        readonly Renderer[] scans = new Renderer[ScanCount];
        MaterialPropertyBlock fxBlock;
        const int ScanCount = 2;   // 11-22 "투영이 좀 과해": was 3
        const float ScanSeconds = 2.4f, EmitterRadius = .74f, ReceiverRadius = .36f;
        static readonly Color PlateGold = new Color(1f, .74f, .28f);
        static Mesh flatQuad;
        static readonly Material[] emitterLight = new Material[2], ringLight = new Material[2];
        static Texture2D emitterTexture, ringTexture, moteTexture;
        static readonly Material[] moteMaterial = new Material[2];

        static Color TeamLight(int team) => team == Teams.Black ? new Color(.34f, .31f, .46f) : new Color(.62f, .84f, 1f);

        void BuildBeam()
        {
            var material = BeamMaterial(station.Team);
            if (material == null) return;
            fxBlock = new MaterialPropertyBlock();
            projection = new GameObject("Hologram projection").transform;
            projection.SetParent(transform, false);
            float sweepFrom = transform.position.y + BeamBottom;

            var go = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(projection, false);
            go.GetComponent<MeshFilter>().sharedMesh = BeamMesh();
            beam = Glow(go.GetComponent<MeshRenderer>(), material, sweepFrom);

            emitter = Flat("Emitter", EmitterMaterial(station.Team), BeamBottom + .004f, EmitterRadius, sweepFrom).transform;
            receiver = Flat("Receiver", RingMaterial(station.Team), MarkerY - PieceFoot, ReceiverRadius, sweepFrom).transform;
            fxBlock.Clear();
            fxBlock.SetFloat("_SweepFrom", sweepFrom);
            fxBlock.SetColor("_Color", TeamLight(station.Team) * 1.1f);
            receiver.GetComponent<Renderer>().SetPropertyBlock(fxBlock);
            for (int i = 0; i < ScanCount; i++) scans[i] = Flat("Scan ring " + (i + 1), RingMaterial(station.Team), BeamBottom, 1f, sweepFrom);
            var strandLight = StrandMaterial();
            if (strandLight != null)
            {
                var lines = new GameObject("Strands", typeof(MeshFilter), typeof(MeshRenderer));
                lines.transform.SetParent(projection, false);
                lines.GetComponent<MeshFilter>().sharedMesh = StrandMesh(station.Team);
                strands = Glow(lines.GetComponent<MeshRenderer>(), strandLight, sweepFrom).transform;
            }
            Motes(station.Team);
        }

        // The skirt alone was too faint to read in daylight (11-22 captures on the project copy), and lit brighter it
        // turns into a solid: eight thin strands of light (sixteen until "투영이 좀 과해") run up its surface instead, from the emitter's outer ring to
        // the receiver, a little twisted, dashes of light flowing up them (ChessFight/Hologram Line, the entrance
        // screen's), gold at the plate turning into the team's light at the piece (the vertex colours).
        Transform strands;
        const int StrandCount = 8, StrandSteps = 8;   // 11-22 "투영이 좀 과해": was 16
        static readonly Mesh[] strandMesh = new Mesh[2];
        static Material strandMaterial;

        static Mesh StrandMesh(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            if (strandMesh[t] != null) return strandMesh[t];
            var holo = new HoloMesh();
            float top = MarkerY - PieceFoot;
            for (int s = 0; s < StrandCount; s++)
            {
                float a0 = s * Mathf.PI * 2f / StrandCount, along = 0f;
                Vector3 prev = Vector3.zero;
                for (int k = 0; k <= StrandSteps; k++)
                {
                    float v = k / (float)StrandSteps;
                    float r = Mathf.Lerp(BeamBase - .02f, BeamTopRadius + .03f, 1f - Mathf.Pow(1f - v, 2.2f));
                    float a = a0 + v * .55f;
                    var p = new Vector3(Mathf.Cos(a) * r, Mathf.Lerp(BeamBottom + .01f, top, v), Mathf.Sin(a) * r);
                    if (k > 0)
                    {
                        float d = (p - prev).magnitude;
                        holo.Line(prev, p, 1f, along, along + d, round: false);
                        along += d;
                    }
                    prev = p;
                }
            }
            var mesh = holo.LineMesh("Promotion pad strands", .1f);
            var verts = mesh.vertices;
            var colours = new Color[verts.Length];
            Color light = TeamLight(team);
            for (int i = 0; i < verts.Length; i++)
            {
                float v = Mathf.InverseLerp(BeamBottom, top, verts[i].y);
                var c = Color.Lerp(PlateGold, light, Mathf.SmoothStep(0f, 1f, v));
                c.a = Mathf.Lerp(1f, .6f, v);
                colours[i] = c;
            }
            mesh.colors = colours;
            return strandMesh[t] = mesh;
        }

        static Material StrandMaterial()
        {
            if (strandMaterial != null) return strandMaterial;
            var shader = Shader.Find("ChessFight/Hologram Line");
            if (shader == null) shader = Resources.Load<Shader>("Hologram/HologramLine");
            if (shader == null) return null;
            var m = new Material(shader) { name = "PR_MG_PromoHolo_Strands" };
            m.SetColor("_Color", Color.white * .7f);
            m.SetColor("_GlowColor", Color.white * .4f);
            m.SetFloat("_Width", .008f);
            m.SetFloat("_CoreWidth", .35f);
            m.SetFloat("_Glow", .6f);
            m.SetFloat("_Dash", .85f);
            m.SetFloat("_DashLength", .22f);
            m.SetFloat("_DashFlow", .35f);
            m.SetFloat("_Scan", .15f);
            return strandMaterial = m;
        }

        Renderer Glow(Renderer r, Material material, float sweepFrom)
        {
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            fxBlock.Clear();
            fxBlock.SetFloat("_SweepFrom", sweepFrom);
            r.SetPropertyBlock(fxBlock);
            return r;
        }

        // A flat disc of light lying at height y, `radius` across from the middle (a quad, the picture round).
        Renderer Flat(string name, Material material, float y, float radius, float sweepFrom)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(projection, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(radius, 1f, radius);
            go.GetComponent<MeshFilter>().sharedMesh = FlatQuad();
            return Glow(go.GetComponent<MeshRenderer>(), material, sweepFrom);
        }

        // Light rising off the plate inside the skirt, drawing in towards the piece: gold at first, the team's light
        // by the top. In the pad's own space, so it stays with the pad.
        void Motes(int team)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(projection, false);
            go.transform.localPosition = new Vector3(0f, BeamBottom + .02f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the circle shape lies flat; local z = up
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .06f);
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .58f;
            shape.radiusThickness = .6f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.42f);   // local -z is up here (turned 90 about x)
            velocity.radial = new ParticleSystem.MinMaxCurve(-.26f);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(PlateGold, 0f), new GradientColorKey(TeamLight(team), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .35f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MoteMaterial(team);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        // Every frame while the projection is on: the lens and rays turn, the receiver rides the piece's bob, the scan
        // rings climb.
        void AnimateProjection()
        {
            if (projection == null || !projection.gameObject.activeSelf) return;
            float t = Time.time;
            emitter.localRotation = Quaternion.Euler(0f, t * 14f, 0f);
            if (strands != null) strands.localRotation = Quaternion.Euler(0f, t * 14f, 0f);
            beam.transform.localRotation = Quaternion.Euler(0f, -t * 9f, 0f);
            receiver.localPosition = new Vector3(0f, MarkerY - PieceFoot + Mathf.Sin(t * 2f) * MarkerBob - .01f, 0f);
            receiver.localRotation = Quaternion.Euler(0f, -t * 40f, 0f);
            float top = MarkerY - PieceFoot, sweepFrom = transform.position.y + BeamBottom;
            Color team = TeamLight(station.Team);
            for (int i = 0; i < ScanCount; i++)
            {
                if (scans[i] == null) continue;
                float v = Mathf.Repeat(t / ScanSeconds + i / (float)ScanCount, 1f);
                float radius = Mathf.Lerp(BeamBase * 1.04f, BeamTopRadius, 1f - Mathf.Pow(1f - v, 2.2f)) + .02f;
                var tr = scans[i].transform;
                tr.localPosition = new Vector3(0f, Mathf.Lerp(BeamBottom + .01f, top, v), 0f);
                tr.localScale = new Vector3(radius, 1f, radius);
                float fade = Mathf.Sin(v * Mathf.PI);
                fxBlock.Clear();
                fxBlock.SetFloat("_SweepFrom", sweepFrom);
                fxBlock.SetColor("_Color", Color.Lerp(PlateGold, team, Mathf.SmoothStep(0f, 1f, v)) * (.8f * fade));
                scans[i].SetPropertyBlock(fxBlock);
            }
        }

        static Mesh FlatQuad()
        {
            if (flatQuad != null) return flatQuad;
            flatQuad = new Mesh
            {
                name = "Promotion pad light disc",
                vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            flatQuad.RecalculateBounds();
            return flatQuad;
        }

        static Material HoloMaterialFor(string name, Texture2D picture, Color light, float fill)
        {
            var shader = Shader.Find("ChessFight/Hologram");
            if (shader == null) shader = Resources.Load<Shader>("Hologram/Hologram");
            if (shader == null) return null;
            var m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", picture);
            m.SetColor("_Color", light);
            m.SetFloat("_Fill", fill);
            m.SetFloat("_Top", 0f);
            m.SetFloat("_Rim", 0f);
            m.SetFloat("_Base", 0f);
            m.SetFloat("_Scan", .2f);
            m.SetFloat("_Sweep", .6f);
            m.SetFloat("_SweepHeight", SweepHeight);
            m.SetFloat("_Grain", .05f);
            return m;
        }

        // The lens on the plate: the plate's gold, a little of the team's light in its outer ring.
        static Material EmitterMaterial(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            return emitterLight[t] != null ? emitterLight[t]
                 : emitterLight[t] = HoloMaterialFor("PR_MG_PromoHolo_Emitter", EmitterTexture(), Color.Lerp(PlateGold, TeamLight(team), .2f) * .75f, 1f);
        }

        // A thin ring of light with a soft glow (the scan rings and the receiver; each sets its own colour).
        static Material RingMaterial(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            return ringLight[t] != null ? ringLight[t]
                 : ringLight[t] = HoloMaterialFor("PR_MG_PromoHolo_Ring", RingTexture(), TeamLight(team), 1f);
        }

        static Material MoteMaterial(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            if (moteMaterial[t] != null) return moteMaterial[t];
            if (moteTexture == null) moteTexture = Round(64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));
            return moteMaterial[t] = LitMaterials.NewAdditiveParticles(moteTexture, new Color(1.6f, 1.6f, 1.6f, 1f));
        }

        // A square picture drawn from the distance to its middle (0 there, 1 at the edge's middle) and the angle round.
        static Texture2D Round(int size, System.Func<float, float> f) => Round(size, (r, a) => f(r));

        static Texture2D Round(int size, System.Func<float, float, float> f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Promotion pad light" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + .5f) / size * 2f - 1f, py = (y + .5f) / size * 2f - 1f;
                    float v = Mathf.Clamp01(f(Mathf.Sqrt(px * px + py * py), Mathf.Atan2(py, px)));
                    pixels[y * size + x] = new Color(v, v, v, v);
                }
            tex.SetPixels(pixels);
            tex.Apply(true, true);
            return tex;
        }

        static float Line(float r, float at, float width) => Mathf.Exp(-Mathf.Pow((r - at) / width, 2f));

        // The lens: an outer ring with 48 ticks inside it, a dashed middle ring, an inner ring and a soft heart.
        static Texture2D EmitterTexture()
        {
            if (emitterTexture != null) return emitterTexture;
            return emitterTexture = Round(512, (r, a) =>
            {
                if (r > 1f) return 0f;
                float turn = (a / (Mathf.PI * 2f) + 1f) % 1f;
                float ticks = r > .8f && r < .88f ? Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(turn * 48f, 1f) - .5f) * 6f) * .8f : 0f;
                float dashes = Mathf.Repeat(turn * 12f, 1f) < .62f ? 1f : .15f;
                float rings = Line(r, .93f, .012f) + .35f * Line(r, .93f, .05f)
                            + .8f * dashes * Line(r, .66f, .01f)
                            + .9f * Line(r, .4f, .008f) + .25f * Line(r, .4f, .04f);
                float heart = .45f * Mathf.Exp(-Mathf.Pow(r / .3f, 2f));
                float haze = .08f * (1f - r);
                return rings + ticks + heart + haze;
            });
        }

        // A ring at the disc's edge: a sharp line and a soft glow either side.
        static Texture2D RingTexture()
        {
            if (ringTexture != null) return ringTexture;
            return ringTexture = Round(256, r => Line(r, .9f, .018f) + .3f * Line(r, .9f, .07f));
        }

        static Mesh BeamMesh()
        {
            if (beamMesh != null) return beamMesh;
            const int around = 64, rings = 10;
            float top = MarkerY - PieceFoot;
            var verts = new Vector3[(around + 1) * (rings + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= rings; j++)
            {
                float v = j / (float)rings;
                // Drawn in quickly over the plate, then rising almost straight under the piece.
                float pull = 1f - Mathf.Pow(1f - v, 2.2f);
                for (int i = 0; i <= around; i++)
                {
                    float a = i / (float)around * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    // Round from the emitter's outer ring (it turns; a square foot turned with it showed as a skewed
                    // sheet over the plate) in to the circle under the piece.
                    float r = Mathf.Lerp(BeamBase, BeamTopRadius, pull);
                    verts[j * (around + 1) + i] = new Vector3(c * r, Mathf.Lerp(BeamBottom, top, v), s * r);
                    uvs[j * (around + 1) + i] = new Vector2(i / (float)around, v);
                }
            }
            var tris = new int[around * rings * 6];
            int n = 0;
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < around; i++)
                {
                    int a0 = j * (around + 1) + i, a1 = a0 + 1, b0 = a0 + around + 1, b1 = b0 + 1;
                    tris[n++] = a0; tris[n++] = b0; tris[n++] = a1;
                    tris[n++] = a1; tris[n++] = b0; tris[n++] = b1;
                }
            beamMesh = new Mesh { name = "Promotion pad beam", vertices = verts, uv = uvs, triangles = tris };
            beamMesh.RecalculateNormals();
            beamMesh.RecalculateBounds();
            return beamMesh;
        }

        static Material BeamMaterial(int team)
        {
            int t = team == Teams.Black ? 1 : 0;
            if (beamLight[t] != null) return beamLight[t];
            var shader = Shader.Find("ChessFight/Hologram");
            if (shader == null) shader = Resources.Load<Shader>("Hologram/Hologram");
            if (shader == null) return null;
            var m = new Material(shader) { name = "PR_MG_PromoHolo_Beam" + (t == 1 ? "Black" : "White") };
            m.SetTexture("_MainTex", BeamTexture(t));   // the colour and fade live in the picture
            m.SetColor("_Color", Color.white);
            m.SetFloat("_Fill", .35f);
            m.SetFloat("_Top", 0f);
            m.SetFloat("_Rim", 0f);
            m.SetFloat("_Base", 0f);
            m.SetFloat("_Scan", .3f);
            m.SetFloat("_Sweep", 1.2f);
            m.SetFloat("_SweepHeight", SweepHeight);
            m.SetFloat("_Grain", .08f);
            return beamLight[t] = m;
        }

        // Up the beam (v): the plate's gold turning into the team's hologram light, from full at the plate to about a
        // quarter at the piece; round it (u): sixteen soft upright rays.
        static Texture2D BeamTexture(int t)
        {
            // (11-22, "투영 되는 그래픽을 더 좋게 해") thin streaks of uneven width over a faint glow instead of even bands,
            // a hot edge where the light leaves the plate, and a soft end at the piece.
            const int w = 512, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "PromotionBeam" + (t == 1 ? "Black" : "White"),
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
            };
            Color gold = PlateGold, light = TeamLight(t == 1 ? Teams.Black : Teams.White);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                Color c = Color.Lerp(gold, light, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v * 1.25f)));
                float fade = Mathf.Lerp(1f, .35f, Mathf.Pow(v, .9f)) * (1f - .55f * Mathf.SmoothStep(.85f, 1f, v));
                float hot = .35f * Mathf.Exp(-Mathf.Pow(v / .05f, 2f));
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w * Mathf.PI * 2f;
                    float fine = Mathf.Pow(.5f + .5f * Mathf.Sin(u * 24f + 1.3f * Mathf.Sin(u * 5f)), 7f);
                    float wide = .6f * Mathf.Pow(.5f + .5f * Mathf.Sin(u * 11f + 2f + .8f * Mathf.Sin(u * 3f)), 12f);
                    float rays = .45f + .55f * Mathf.Max(fine, wide);
                    var p = c * (fade * rays + hot);
                    p.a = 1f;
                    pixels[y * w + x] = p;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true, true);
            return tex;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            MapLook.Smooth(go); // round parts as true circles (10-22)
            var collider = go.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
