using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // seoungtest R689 (승규 11-18 "자이언트 체스 코스 온라인 동기화 등등 나머지", "코스 3 구간", "장애물 3, 임시 바닥 대신",
    // "모든 것을 온라인 기준으로 맞춰"): the Royal Library course from pr/2 (WarRoomComplete - the library's stamps, closing
    // books, memory typewriter and door, then the giant chess room) as a late obstacle section (slot 5) - on seoungtest in
    // the temporary floor's place, on main (R706, 승규 "작전 회의실 후보로 넣어줘") drawn beside the war room. The prefab
    // Late_3-1_RoyalLibrary holds this root and the course prefab as it is (tile 10 m, as its own scene); what a round needs
    // is added here when it wakes:
    //   entry / exit   the outer wall's doors: the 12 m door in the front wall (z -151) and the 14 m one in the back wall
    //                  (z 171), each with a threshold floor to the course's own tables (the course started its players on
    //                  the first table and ended at the back wall's middle)
    //   checkpoints    CP0 on the first table, CP1..3 the course's own, CP4 just inside the chess room, CP5 past the board
    //   kill volume    under the tables (the offline course put a pawn back under 8 m down)
    //   progress path  for the round's HUD
    // and online (ISectionPart / ISectionNetPart), the same on every PC of a match:
    //   the chess      the series of games from the section's seed, the replay from the host's chess start (2 bytes:
    //                  RoyalLibraryMatch + GiantChessReplay, the giant chess controller reads them)
    //   the books      the covers fold on the shared clock from the round's start; the host crushes, and every PC shows a
    //                  crushed pawn as the offline course did - flattened on the page, then back at the book's safe point
    //                  (6 bytes a crush: the seat, the book, where on the page, the seconds left; four at a time)
    //   the typewriter its letters from the section's seed; the host's pressed and red keys (3 + 3 bytes)
    //   the door       how far the host has it open (1 byte)
    // The stamps already run on the shared clock with the host's hits.
    [DisallowMultipleComponent, RequireComponent(typeof(PawnRushSection), typeof(RoyalLibraryMatch))]
    public sealed class RoyalLibrarySection : MonoBehaviour, ISectionPart, ISectionNetPart
    {
        public const string Id = "Late_3-1_RoyalLibrary";
        public const string DisplayName = "왕의 서재 · 거인 체스";
        public const string GroupName = "Section";
        public static readonly Vector3 Entry = new Vector3(0f, 0f, -151f), Exit = new Vector3(0f, 0f, 171f);
        const float FirstTable = -146f, BackWall = 169.9f, ThresholdHalfWidth = 6f;
        const int MaxCrushes = 4, CrushBytes = 6;

        RoyalLibraryMatch match;
        ClosingBook[] books = new ClosingBook[0];
        TypewriterStairs typewriter;
        HeadquartersPrivacyDoor door;

        sealed class Crush
        {
            public ICharacterDriver Driver;
            public byte Seat;
            public int Book, Spot;
            public Vector3 At;
            public double Until;
            public GameObject Flat;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }
        readonly List<Crush> crushes = new List<Crush>();
        readonly List<Crush> gone = new List<Crush>();
        readonly List<byte> seen = new List<byte>(MaxCrushes);

        public int NetBytes => 2 + 1 + 3 + 3 + MaxCrushes * CrushBytes;

        void Awake()
        {
            match = GetComponent<RoyalLibraryMatch>();
            books = GetComponentsInChildren<ClosingBook>(true);
            typewriter = GetComponentInChildren<TypewriterStairs>(true);
            door = GetComponentInChildren<HeadquartersPrivacyDoor>(true);
            foreach (var book in books) book.Crushed += OnCrushed;
            // What only this PC's player sees (the portal's veil, the giants' eyes): the round's local pawn.
            match.LocalCharacter = () => MiniGameBase.LocalDriver != null ? MiniGameBase.LocalDriver.FollowTarget : null;
            Describe();
            if (transform.Find(GroupName) == null) Build();
            // R709 (승규 11-18 "왕실 서재 조명이나 분위기가 달라졌어"): the library's light and the F2 fly camera add themselves
            // to the dressing only as a scene loads, so a library built later - the course prefab test's pick, the section
            // test's F8 - had neither and showed the round's plain light. The section adds them itself.
            foreach (var dressing in GetComponentsInChildren<RoyalLibraryDressing>(true))
            {
                if (dressing.GetComponent<RoyalLibraryLighting>() == null) dressing.gameObject.AddComponent<RoyalLibraryLighting>();
                if (dressing.GetComponent<RoyalLibraryFlyCam>() == null) dressing.gameObject.AddComponent<RoyalLibraryFlyCam>();
            }
        }

        // R710 (승규 11-18 "여기 문을 지나면 타자기 구간까지 모든 참가자 방장 물리담당이 시야가 넓어지게"): from the portal's
        // threshold (z -136.4) to the typewriter (z -60) the camera that follows a body there sees wider - each PC its own
        // (OrbitCamera.Widen), so every player, the host and the physics PC alike. Ramps in over 3 m past the portal and
        // out over the 8 m before the typewriter's floor.
        const float WidenFrom = -136.4f, WidenTo = -60f, WidenFov = 18f, WidenBack = 4f;
        System.Func<Vector3, Vector2> widen;

        void OnEnable()
        {
            widen = Widen;
            ChessFight.Game.OrbitCamera.Widen = widen;
        }

        void OnDisable()
        {
            if (ChessFight.Game.OrbitCamera.Widen == widen) ChessFight.Game.OrbitCamera.Widen = null;
        }

        Vector2 Widen(Vector3 world)
        {
            if (this == null) return Vector2.zero;
            var p = transform.InverseTransformPoint(world);
            if (Mathf.Abs(p.x) > 45f || p.y < -30f || p.y > 40f || p.z < WidenFrom || p.z > WidenTo) return Vector2.zero;
            float k = Mathf.Min(Mathf.Clamp01((p.z - WidenFrom) / 3f), Mathf.Clamp01((WidenTo - p.z) / 8f));
            k = k * k * (3f - 2f * k);
            return new Vector2(WidenFov, WidenBack) * k;
        }

        void OnDestroy()
        {
            foreach (var book in books) if (book != null) book.Crushed -= OnCrushed;
            foreach (var c in crushes) Unshow(c);
            crushes.Clear();
        }

        public void OnSection(PawnRushSection section)
        {
            match.Configure(section.Seed, section.StartClock);
            foreach (var book in books) book.UseSharedClock(section.StartClock);
            if (typewriter != null) typewriter.UseSeed(section.Seed);
        }

        void Describe()
        {
            var section = GetComponent<PawnRushSection>();
            if (section == null) return;
            section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Obstacle, Entry, Exit, 0f, new[]
            {
                new SectionBeat("1 차원문 · 출발 탁자", new Vector3(-9f, -2f, -152f), new Vector3(9f, 10f, -127f)),
                new SectionBeat("2 도장 탁자", new Vector3(-9f, -2f, -127f), new Vector3(9f, 10f, -100f)),
                new SectionBeat("3 덮이는 책", new Vector3(-9f, -2f, -100f), new Vector3(9f, 10f, -60f)),
                new SectionBeat("4 기억의 타자기", new Vector3(-13f, -4f, -60f), new Vector3(13f, 10f, -24f)),
                new SectionBeat("5 지휘부 문", new Vector3(-41f, -2f, -24f), new Vector3(41f, 10f, 0f)),
                new SectionBeat("6 거인 체스판", new Vector3(-41f, -2f, 0f), new Vector3(41f, 30f, 100f)),
                new SectionBeat("7 출구 길", new Vector3(-7f, -2f, 100f), new Vector3(7f, 10f, 172f)),
            });
        }

        void Build()
        {
            var course = GetComponentInChildren<CourseModule>(true);
            var root = new GameObject(GroupName);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var b = new CourseBuilder(null, root.transform);

            // The outer wall's doors to the course's own floors: the corridors of the round join here.
            Threshold(b, "Entry threshold", Entry.z, FirstTable + .1f, "Approach floor -146");
            Threshold(b, "Exit threshold", BackWall, Exit.z, "Final exit walkway");

            int number = 0;
            b.Checkpoint(number++, new Vector3(0f, 0f, -144f), 0f, 12f);
            if (course != null && course.Checkpoints != null)
                foreach (var cp in course.Checkpoints)
                    if (cp != null) b.Checkpoint(number++, Flat(Local(cp.position)), 0f, 12f);
            var chessEntry = Deep(course != null ? course.transform : transform, "Approach exit");
            Vector3 chess = chessEntry != null ? Flat(Local(chessEntry.position)) : new Vector3(0f, 0f, -4f);
            b.Checkpoint(number++, chess, 0f, 14f);
            b.Checkpoint(number, new Vector3(0f, 0f, 103f), 0f, 10f);
            b.Trigger<KillVolume>("Kill volume (under the tables)", new Vector3(-165f, -20f, -156f), new Vector3(165f, -8f, 176f));

            var route = new List<Vector3> { Entry, new Vector3(0f, 0f, -144f) };
            if (course != null && course.Route != null)
                foreach (var point in course.Route)
                    if (point != null) { var p = Flat(Local(point.position)); if (p.z > route[route.Count - 1].z + .5f) route.Add(p); }
            foreach (var p in new[] { chess, new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 100f), Exit })
                if (p.z > route[route.Count - 1].z + .5f) route.Add(p);
            var beats = new string[route.Count];
            var section = GetComponent<PawnRushSection>();
            for (int i = 0; i < route.Count; i++)
            {
                string beat = section != null ? section.BeatAt(transform.TransformPoint(route[i] + Vector3.up)) : null;
                beats[i] = string.IsNullOrEmpty(beat) ? "1" : beat.Substring(0, 1);
            }
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(root.transform, false);
            path.AddComponent<ProgressPath>().Configure(route.ToArray(), route.ToArray(), beats);
            root.SetActive(true);
        }

        // A 12 m floor from z0 to z1, its top on the course's tables (y 0), drawn like the table it leads to.
        void Threshold(CourseBuilder b, string name, float z0, float z1, string lookOf)
        {
            var like = Deep(transform, lookOf);
            var material = like != null && like.TryGetComponent<Renderer>(out var r) ? r.sharedMaterial : null;
            var go = b.Box(name, new Vector3(-ThresholdHalfWidth, -1.2f, z0), new Vector3(ThresholdHalfWidth, 0f, z1), material);
            go.AddComponent<CourseFloor>();
        }

        Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);
        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

        static Transform Deep(Transform under, string name)
        {
            if (under == null) return null;
            if (under.name == name) return under;
            for (int i = 0; i < under.childCount; i++)
            {
                var found = Deep(under.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ---- the books: a crushed pawn lies flat on the page, then comes back at the book's safe point -------------------

        int BookOf(Transform recovery)
        {
            for (int i = 0; i < books.Length; i++) if (books[i] != null && books[i].RecoveryPoint == recovery) return i;
            return -1;
        }

        Crush Find(ICharacterDriver driver)
        {
            foreach (var c in crushes) if (c.Driver == driver) return c;
            return null;
        }

        Crush FindSeat(byte seat)
        {
            foreach (var c in crushes) if (c.Seat == seat) return c;
            return null;
        }

        static byte SeatOf(ICharacterDriver driver) => MiniGameBase.SeatOf != null && driver != null ? MiniGameBase.SeatOf(driver) : MiniGameBase.NoSeat;

        // The authority (offline, or the match's host): a book closed on a pawn.
        void OnCrushed(GameObject victim, Transform recovery, float delay)
        {
            if (!ObstacleClock.Authority || victim == null) return;
            var driver = victim.GetComponent<ICharacterDriver>();
            int book = BookOf(recovery);
            if (driver == null || book < 0 || Find(driver) != null) return;
            var c = new Crush { Driver = driver, Seat = SeatOf(driver), Book = book, Until = ObstacleClock.Now + delay };
            c.Spot = c.Seat != MiniGameBase.NoSeat ? c.Seat : crushes.Count;
            c.At = Feet(driver);
            if (crushes.Count >= MaxCrushes)
            {
                // More crushed at once than the state carries: straight back to the safe point, without the flat body.
                Hold(c);
                return;
            }
            Show(c);
            crushes.Add(c);
            Hold(c);
        }

        static Vector3 Feet(ICharacterDriver driver)
        {
            var follow = driver.FollowTarget;
            var owner = driver as Component;
            float feet = float.PositiveInfinity;
            if (owner != null)
                foreach (var r in owner.GetComponentsInChildren<MeshRenderer>())
                    if (r.enabled) feet = Mathf.Min(feet, r.bounds.min.y);
            Vector3 p = follow != null ? follow.position : owner != null ? owner.transform.position : Vector3.zero;
            return new Vector3(p.x, float.IsInfinity(feet) ? p.y - 1f : feet, p.z);
        }

        // The flattened copy of the body where it was crushed (GiantChessCourse.BookCrush's look) and the body hidden.
        void Show(Crush c)
        {
            var owner = c.Driver as Component;
            if (owner == null) return;
            Vector3 origin = Feet(c.Driver);
            var flat = new GameObject("Flattened book victim");
            flat.transform.position = c.At;
            foreach (var mesh in owner.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (mesh.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                var part = new GameObject("Flattened body part");
                part.transform.SetParent(flat.transform, false);
                part.transform.position = c.At + (mesh.transform.position - origin);
                part.transform.rotation = mesh.transform.rotation;
                part.transform.localScale = mesh.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            flat.transform.localScale = new Vector3(1.4f, .1f, 1.4f);
            flat.transform.position += Vector3.up * .65f;
            c.Flat = flat;
            foreach (var r in owner.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; c.Hidden.Add(r); }
        }

        static void Unshow(Crush c)
        {
            foreach (var r in c.Hidden) if (r != null) r.enabled = true;
            c.Hidden.Clear();
            if (c.Flat != null) Destroy(c.Flat);
            c.Flat = null;
        }

        // The authority keeps a crushed pawn at the book's safe point until it comes back (twelve places, by seat).
        void Hold(Crush c)
        {
            if (c.Book < 0 || c.Book >= books.Length || books[c.Book] == null || books[c.Book].RecoveryPoint == null) return;
            var point = books[c.Book].RecoveryPoint;
            int k = ((c.Spot % 12) + 12) % 12;
            c.Driver.Teleport(point.TransformPoint(new Vector3((k % 4 - 1.5f) * 1.5f, .05f, (k / 4 - 1) * 1.2f)), point.rotation);
        }

        void FixedUpdate()
        {
            if (!ObstacleClock.Authority || crushes.Count == 0) return;
            double now = ObstacleClock.Now;
            gone.Clear();
            foreach (var c in crushes)
            {
                if (c.Driver == null || c.Driver as Component == null) { gone.Add(c); continue; }
                Hold(c);
                if (now >= c.Until) gone.Add(c);
            }
            foreach (var c in gone) { Unshow(c); crushes.Remove(c); }
        }

        // ---- the course state ---------------------------------------------------------------------------------------------

        public void WriteNet(ref NetBlock block)
        {
            block.U16(GiantChessReplay.Encode(match.ChessStart, match.Origin));
            block.U8(door != null ? Mathf.RoundToInt(door.Opening * 255f) : 0);
            uint pressed = typewriter != null ? typewriter.PressedMask : 0, flash = typewriter != null ? typewriter.FlashMask : 0;
            for (int k = 0; k < 3; k++) block.U8((int)(pressed >> (8 * k)) & 0xFF);
            for (int k = 0; k < 3; k++) block.U8((int)(flash >> (8 * k)) & 0xFF);
            double now = ObstacleClock.Now;
            for (int i = 0; i < MaxCrushes; i++)
            {
                var c = i < crushes.Count ? crushes[i] : null;
                if (c == null || c.Seat == MiniGameBase.NoSeat) { block.U8(MiniGameBase.NoSeat); for (int k = 1; k < CrushBytes; k++) block.U8(0); continue; }
                block.U8(c.Seat);
                block.U8(c.Book);
                var p = PageUnit(c.Book, c.At);
                block.U8(p.x); block.U8(p.y); block.U8(p.z);
                block.U8(Mathf.Clamp(Mathf.CeilToInt((float)(c.Until - now) * 10f), 0, 255));
            }
        }

        public void ReadNet(ref NetBlock block)
        {
            match.SetChessStart(GiantChessReplay.Decode(block.ReadU16(), match.Origin));
            float opening = block.ReadU8() / 255f;
            if (door != null) door.SetRemoteOpening(opening);
            uint pressed = 0, flash = 0;
            for (int k = 0; k < 3; k++) pressed |= (uint)block.ReadU8() << (8 * k);
            for (int k = 0; k < 3; k++) flash |= (uint)block.ReadU8() << (8 * k);
            if (typewriter != null) typewriter.ApplyRemote(pressed, flash);
            double now = ObstacleClock.Now;
            seen.Clear();
            for (int i = 0; i < MaxCrushes; i++)
            {
                byte seat = (byte)block.ReadU8();
                int book = block.ReadU8();
                var unit = new Vector3Int(block.ReadU8(), block.ReadU8(), block.ReadU8());
                float left = block.ReadU8() / 10f;
                if (seat == MiniGameBase.NoSeat || book >= books.Length) continue;
                seen.Add(seat);
                var c = FindSeat(seat);
                if (c != null) { c.Until = now + left; continue; }
                var driver = MiniGameBase.DriverOf != null ? MiniGameBase.DriverOf(seat) : null;
                if (driver == null) continue;
                c = new Crush { Driver = driver, Seat = seat, Book = book, Spot = seat, Until = now + left, At = FromPageUnit(book, unit) };
                Show(c);
                crushes.Add(c);
            }
            gone.Clear();
            foreach (var c in crushes) if (!seen.Contains(c.Seat)) gone.Add(c);
            foreach (var c in gone) { Unshow(c); crushes.Remove(c); }
        }

        // Where on the book a crush was, in the book's crush volume (each axis -1..1 of its size, in a byte).
        Vector3Int PageUnit(int book, Vector3 world)
        {
            var volume = book >= 0 && book < books.Length && books[book] != null ? books[book].CrushVolume : null;
            if (volume == null) return new Vector3Int(128, 128, 128);
            Vector3 local = volume.transform.InverseTransformPoint(world) - volume.center;
            Vector3 size = volume.size;
            int Unit(float v, float s) => Mathf.Clamp(Mathf.RoundToInt((Mathf.Clamp(v / Mathf.Max(.01f, s), -1f, 1f) + 1f) * 127.5f), 0, 255);
            return new Vector3Int(Unit(local.x, size.x), Unit(local.y, size.y), Unit(local.z, size.z));
        }

        Vector3 FromPageUnit(int book, Vector3Int unit)
        {
            var volume = books[book] != null ? books[book].CrushVolume : null;
            if (volume == null) return books[book] != null ? books[book].transform.position : transform.position;
            Vector3 size = volume.size;
            var local = new Vector3((unit.x / 127.5f - 1f) * size.x, (unit.y / 127.5f - 1f) * size.y, (unit.z / 127.5f - 1f) * size.z);
            return volume.transform.TransformPoint(local + volume.center);
        }
    }
}
