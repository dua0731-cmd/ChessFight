using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R448 (승규 11-05: "이 기물 3명이 플레이어들을 공격을 할거야 사이드에 있는 애들은 공격속도가 빠른 대신 데미지는 약해 가운데 있는
    // 애는 차징을 크게 하고 공격속도가 늦은 대신 1번 맞으면 엄청 데미지가 커 vfx를 기물이랑 받침대 사이에서 빛이 들어오고 거기서 차징
    // 하면서 쏘게 해"): the three marble rooks behind the war room's goal (ArtSource/PawnRush/Library/goal_rooks.py) shoot
    // down the hall. R449 ("예고는 없어도 돼 ... 포물선 ... 그게 아니야 1자로 쫘악 깔려야 되고 사거리는 남쪽 가장 끝에 있는 기둥
    // 까지 바닥에 부딪치면 파티클 + vfx ... 팡 터지게 ... 기물에 맞아도 파티클 + vfx"): no warning on the floor, no arc - each shot
    // is a straight streak from the gap under the piece, sloping gently down the hall to the floor by the southernmost
    // inner piers (the hall's far end, about 36 m), and it bursts where it meets the floor (or anything solid on the
    // way) and on every pawn it passes through. Each round on the shared obstacle clock:
    //   charge  the piece rises off its pedestal and turns its window to this round's line; light pours out of the gap
    //           (the socket's glow, a band of light round the gap, a lamp) and motes are drawn into it
    //   fire    a flash at the gap's rim and the shot streaks off along its line, the piece drops back
    //   burst   where it meets the floor: a flash, a light, sparks, embers - no ring (R450) - (the heavy one also
    //           dust); on a pawn it passes through: a flash, a ring and sparks (it goes on through)
    //            side rooks (x +-2.25)   every 2.2 s, 0.55 s charge, a thin fast streak (50 m/s): a stagger
    //                                    (push 3.5 m/s + up .8, knocked down 1.3 s - R473), and 0.9 m round where it lands
    //            middle rook (1.2x)      every 8.5 s, 3 s charge, a thick streak (38 m/s): knocked flat for 2.4 s
    //                                    (push 13 + up 5), and where it lands flat within 1.6 m, staggered out to 3 m
    // Each round's line goes to a point picked by its number (golden-ratio steps) anywhere between the side inner piers,
    // from 3 m in front of the rooks to the southernmost piers' line - the floor, the conveyor (its deck's level over the
    // open well), the stairs - and a line that something solid stops well short (a balustrade, the goal's front) is
    // passed over for the next point (R451; R449 shot only at the far end, down the middle):
    // aiming at the pawns themselves would differ between PCs (they see each pawn at a different
    // moment) and a mover driven by player state is offline only (DECISIONS G2). The floor it meets is found once per
    // shot along its line against the still model only (moving parts, pawns and the rooks left out), so every PC draws
    // the same shot with nothing sent. Hits are the authority's (ObstacleClock.Authority), on the pawns it simulates, as
    // ChandelierRain's; the bursts on pawns are drawn on every PC for the pawns it shows.
    public sealed class LibraryRookTurret : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, .72f, .3f), Hot = new Color(1f, .9f, .62f);
        static Texture2D dot, ring, band, beam;
        // R451: where the shots land - anywhere between the inner piers (x +-9.05), from just in front of the rooks to the
        // southernmost piers' line
        const float Lane = 7.8f, NearDepth = 3f;
        const int Tries = 24;
        const float FirstPiers = 15f;       // R455: the two rows of piers nearest the rooks are never aimed at
        const float EntranceFrom = 29f;     // R455: the hall's entrance end (the start platform) begins this far out

        [SerializeField] bool heavy;
        [SerializeField] Transform piece, gap, pedestal, center, goal;
        [SerializeField] float size = 1f;

        // the round
        // R450 (승규 11-05 "바닥에 맞았을 때 원도 하지마 그리고 공격 텀이 좀 빨라 지금 보다 조금 천천히 공격 해주는 대신 날아오는
        // 속도는 빠르게 해"): rounds slower (were 7 and 1.6 s), streaks faster (were 24 and 32 m/s), no ring on the floor.
        float Period => heavy ? 8.5f : 2.2f;
        float ChargeTime => heavy ? 3f : .55f;
        float Speed => heavy ? 60f : 80f;         // R455: faster again (were 38 and 50)
        float Lift => (heavy ? .32f : .12f) * size;
        float Settle => heavy ? .35f : .2f;
        // the hit
        float DirectRadius => heavy ? .8f : .45f;
        float InnerRadius => heavy ? 1.6f : .9f;
        float OuterRadius => heavy ? 3f : .9f;
        // R473 (승규 11-05 "양 옆 쏠 때 넘어지게 해 날아갈 정도로는 하지 말고"): a side rook's hit knocks a pawn down for
        // 1.3 s (it only staggered), pushed less (4.5 -> 3.5 m/s) and barely lifted (1.5 -> .8) so it falls where it
        // stands instead of being thrown
        float InnerPush => heavy ? 13f : 3.5f;
        float InnerUp => heavy ? 5f : .8f;
        float InnerKnockdown => heavy ? 2.4f : 1.3f;
        float OuterPush => heavy ? 7f : 3.5f;
        float OuterUp => heavy ? 2.5f : .8f;

        float phase, seed, pieceRadius, range = 36f;
        // R454: the inner piers a shot may end on, and the conveyor's deck (where it will be when a shot lands)
        readonly List<Collider> piers = new List<Collider>();
        LibraryShuttleDriver shuttle;
        BoxCollider deck;
        Vector3 deckFromCarriage;
        Vector3 origin, fwd, right, gapRest, pieceRestLocal;
        Quaternion pieceRestRot;
        bool ready;

        struct Shot { public int k; public double fire, impact; public Vector3 from, to, normal, dir; public float yaw; }
        readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        readonly Dictionary<int, HashSet<ImportedObstacleActor>> shown = new Dictionary<int, HashSet<ImportedObstacleActor>>();
        readonly Collider[] overlaps = new Collider[256];
        readonly RaycastHit[] rays = new RaycastHit[64];
        int struckShot = int.MinValue;
        double lastFixed = double.NaN, lastFrame = double.NaN;

        // R463 (승규 11-05 "이제 기물 공격 sfx 다 찾아서 넣어"): the charge from the piece as the round begins, the shot as
        // it leaves, the burst where it lands - NearFarSound, so close up the detailed take, across the hall the dull
        // one, and in the lower floor or on the outside stairs muffled (LibraryMuffle). The side rooks' short and light,
        // the middle one's three-second charge, boom and blast heavy. ElevenLabs takes made into
        // Assets/Resources/PawnRushLibrarySfx/Rook*.wav by ArtSource/PawnRush/Library/rook_sounds.py.
        const string Sfx = "PawnRushLibrarySfx/";
        NearFarSound chargeSound, fireSound, impactSound;
        AudioClip chargeNear, chargeFar, fireNear, fireFar, impactNear, impactFar;
        Transform impactPoint;

        // look
        Renderer[] glowRenderers;
        int[] glowSlots;
        MaterialPropertyBlock block;
        Transform bandT, discT, flashT, laserDotT;
        LineRenderer laser;
        Renderer laserDotR;
        Renderer bandR, discR, flashR;
        Light lamp, burstLamp;
        ParticleSystem motes, crackle, sparks, embers, dust;
        Camera cam;
        float flashAt = -10f, burstLampAt = -10f, burstLampPower;
        sealed class Streak { public Transform t; public Renderer r; public TrailRenderer trail; public int shot = int.MinValue; }
        readonly Streak[] streaks = new Streak[2];
        sealed class Burst { public Transform flash, ring; public Renderer flashR, ringR; public float at = -10f, scale, life; public bool live; }
        readonly Burst[] bursts = new Burst[6];
        int nextBurst;

        public void Configure(bool heavyRook, float rookSize, Transform movingPiece, Transform gapPoint, Transform pedestalPart,
                              Transform middleRook, Transform goalZone)
        {
            heavy = heavyRook;
            size = rookSize;
            piece = movingPiece;
            gap = gapPoint;
            pedestal = pedestalPart;
            center = middleRook;
            goal = goalZone;
        }

        void Start()
        {
            if (piece == null || gap == null || center == null || goal == null) { enabled = false; return; }
            origin = center.position;
            var goalAt = goal.TryGetComponent<Renderer>(out var zone) ? zone.bounds.center : goal.position;
            var toGoal = goalAt - origin;
            toGoal.y = 0f;
            fwd = toGoal.sqrMagnitude > .01f ? toGoal.normalized : -transform.forward;
            right = Vector3.Cross(Vector3.up, fwd);
            // the range: as far down the hall as the southernmost inner piers
            var hall = transform.parent != null ? transform.parent : transform;
            float far = 0f;
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("Inner_gallery_pier") && t.name.IndexOf('|') < 0) far = Mathf.Max(far, Vector3.Dot(t.position - origin, fwd));
            if (far > 10f) range = far;
            foreach (var c in hall.GetComponentsInChildren<Collider>(true))
                if (Pier(c)) piers.Add(c);
            float closest = float.MaxValue;
            foreach (var d in FindObjectsByType<LibraryShuttleDriver>(FindObjectsSortMode.None))
            {
                if (d.Carriage == null) continue;
                float gapTo = (d.Carriage.position - origin).sqrMagnitude;
                if (gapTo < closest && gapTo < 80f * 80f) { closest = gapTo; shuttle = d; }
            }
            if (shuttle != null)
                foreach (var b in shuttle.Carriage.GetComponentsInChildren<BoxCollider>(true))
                    if (b.name == "Conveyor collision") { deck = b; deckFromCarriage = b.transform.TransformPoint(b.center) - shuttle.Carriage.position; }
            float own = Vector3.Dot(transform.position - origin, right);
            phase = heavy ? .37f : own > 0f ? 1.1f : 0f;          // the side rooks half a round apart
            seed = heavy ? .11f : own > 0f ? .29f : .71f;
            pieceRadius = .56f * size;                  // the piece's foot (goal_rooks.py)
            gapRest = gap.position;
            pieceRestLocal = piece.localPosition;
            pieceRestRot = piece.rotation;
            Look();
            Sounds();
            ready = true;
        }

        // ------------------------------------------------------------------ the round, from the clock

        int Round(double t) => (int)System.Math.Floor((t + phase) / Period);
        double RoundStart(int k) => k * (double)Period - phase;

        Shot ShotOf(int k)
        {
            if (shots.TryGetValue(k, out var s)) return s;
            if (shots.Count > 8) shots.Clear();
            // R451 ("... 컨테이너도 쏠 수 있고 대신 오른쪽 왼쪽 기둥은 못싸"): anywhere between the side piers, near or far.
            // R454 ("확률은 하지 말자 ... 중간에 날아와서 터치지 말고 바닥이나 기둥에 닿아야 터치게 하고 난간은 절대 조준 대상이
            // 아니야"; R453's aimed rounds are gone): a shot only ever ends on the floor, a pier or the conveyor's deck, never
            // in the air or on a balustrade. The round's number picks candidates (golden-ratio steps), a third of them on
            // a pier's face, the rest a point on the floor (the deck, if it will be under that point when the shot lands);
            // the first whose line meets nothing on the way - and, for a pier, meets that pier - is the shot.
            for (int a = 0; a < Tries; a++)
            {
                int i = k * Tries + a;
                float u = Frac(i * .6180339f + seed), v = Frac(i * .7548777f + seed * 1.7f), w = Frac(i * .5698403f + seed * 2.9f);
                // R455 ("기둥은 1번 째 2번 째 기둥까지 조준 대상이 아니야 ... 특히 컨베이너 벨트랑 입구 쪽에 포격을 해주고"): about a
                // third of the candidates on the conveyor's belt, a third at the entrance end, the rest on the far piers
                // or anywhere on the floor
                bool ok;
                if (w < .35f) ok = OnBelt(k, u, v, ref s);
                else if (w < .67f) ok = OnPoint(k, origin + fwd * Mathf.Lerp(EntranceFrom, range, u) + right * Mathf.Lerp(-5f, 5f, v), ref s);
                else if (w < .84f && piers.Count > 0) ok = AtPier(k, u, v, ref s);
                else ok = OnFloor(k, u, v, ref s);
                if (ok) return Keep(k, s);
            }
            // nothing found (no pawns' business, the hall's model is fixed): straight down the middle to the far end's floor
            var far = origin + fwd * range;
            if (!FloorAt(far, out var floor)) floor = new Vector3(far.x, origin.y - 1.49f, far.z);
            Line(floor, ref s);
            s.normal = Vector3.up;
            return Keep(k, s);
        }

        Shot Keep(int k, Shot s)
        {
            s.k = k;
            s.dir = (s.to - s.from).normalized;
            s.fire = RoundStart(k) + ChargeTime;
            s.impact = s.fire + Vector3.Distance(s.from, s.to) / Speed;
            shots[k] = s;
            return s;
        }

        // From the gap's rim toward a point (the piece turned to it).
        void Line(Vector3 target, ref Shot s)
        {
            var flat = target - gapRest;
            flat.y = 0f;
            var dir = flat.sqrMagnitude > .001f ? flat.normalized : fwd;
            s.from = gapRest + dir * pieceRadius + Vector3.up * (Lift * .5f);
            s.to = target;
            s.yaw = Vector3.SignedAngle(fwd, dir, Vector3.up);
        }

        // The first solid thing on the way from s.from toward `toward`, up to `length`.
        bool FirstHit(Vector3 from, Vector3 toward, float length, out RaycastHit first)
        {
            first = default;
            var line = toward - from;
            if (line.sqrMagnitude < .01f) return false;
            int n = Physics.RaycastNonAlloc(from, line.normalized, rays, length, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int h = 0; h < n; h++)
                if (rays[h].distance < best && Solid(rays[h].collider)) { best = rays[h].distance; first = rays[h]; }
            return best < float.MaxValue;
        }

        // a point on one pier's face toward the hall, at a pawn's height or a little above
        bool AtPier(int k, float u, float v, ref Shot s)
        {
            var pier = piers[Mathf.Min(piers.Count - 1, Mathf.FloorToInt(u * piers.Count))];
            var b = pier.bounds;
            float floorY = origin.y - 1.49f;
            float height = floorY + Mathf.Lerp(.4f, 2.4f, v), depth = Vector3.Dot(b.center - origin, fwd);
            if (depth < FirstPiers || depth > range + .5f) return false;
            // its face toward the hall's middle line (a ray from the middle at that depth and height to the pier)
            var middle = origin + fwd * depth;
            middle.y = height;
            var toward = new Vector3(b.center.x, height, b.center.z) - middle;
            if (!pier.Raycast(new Ray(middle, toward.normalized), out var face, toward.magnitude + 1f)) return false;
            var aim = face.point;
            Line(aim, ref s);
            float length = Vector3.Distance(s.from, aim) + .6f;
            if (!FirstHit(s.from, aim, length, out var hit) || hit.collider != pier) return false;
            s.to = hit.point;
            s.normal = hit.normal;
            return true;
        }

        // a point on the floor (or on the conveyor's deck when it will be there), reached with nothing in the way
        bool OnFloor(int k, float u, float v, ref Shot s) =>
            OnPoint(k, origin + fwd * Mathf.Lerp(NearDepth, range, u) + right * Mathf.Lerp(-Lane, Lane, v), ref s);

        // a point on the conveyor's belt, along the middle of the hall where it runs
        bool OnBelt(int k, float u, float v, ref Shot s)
        {
            if (shuttle == null || deck == null) return false;
            var p = origin + fwd * Mathf.Lerp(8f, EntranceFrom + 2f, u) + right * Mathf.Lerp(-1.3f, 1.3f, v);
            if (!DeckAt(k, p, out var top)) return false;
            return OnPoint(k, top, ref s);
        }

        bool OnPoint(int k, Vector3 p, ref Shot s)
        {
            bool onDeck = false;
            if (!FloorAt(p, out var target))
            {
                if (!DeckAt(k, p, out target)) return false;
                onDeck = true;
            }
            Line(target, ref s);
            float length = Vector3.Distance(s.from, target);
            if (FirstHit(s.from, target, length + .4f, out var hit))
            {
                // only the floor at the point itself (a balustrade, a fascia or a pier on the way: not this one)
                if (hit.distance < length - .4f) return false;               // something on the way
                if (onDeck) { s.normal = Vector3.up; return true; }          // the deck's rails just under it
                if (hit.normal.y < .7f || Railing(hit.collider)) return false;
                s.to = hit.point;
                s.normal = hit.normal;
                return true;
            }
            if (!onDeck) return false;
            s.normal = Vector3.up;
            return true;
        }

        // The conveyor deck's top over p when this round's shot would land there, if it covers p then.
        bool DeckAt(int k, Vector3 p, out Vector3 top)
        {
            top = default;
            if (shuttle == null || deck == null) return false;
            double land = RoundStart(k) + ChargeTime + Vector3.Distance(gapRest, p) / Speed;
            if (!shuttle.CarriageAt(land, out var carriage)) return false;
            var centre = carriage + deckFromCarriage;
            var size = Vector3.Scale(deck.size, deck.transform.lossyScale);
            var local = Quaternion.Inverse(deck.transform.rotation) * (p - centre);
            if (Mathf.Abs(local.x) > size.x * .5f - .3f || Mathf.Abs(local.z) > size.z * .5f - .3f) return false;
            top = new Vector3(p.x, centre.y + size.y * .5f, p.z);
            return true;
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        static bool Pier(Collider c) => c != null && c.attachedRigidbody == null &&
            (c.name.Contains("Inner_gallery_pier") || c.name.StartsWith("Pier upper shaft"));

        // A balustrade or anything of one (never a target): the railings' sectors, the goal front's posts and fascia.
        static bool Railing(Collider c)
        {
            string n = c.name;
            return n.Contains("Railing") || n.Contains("railing") || n.Contains("alustr") || n.StartsWith("J14_") || n.Contains("fascia") || n.Contains("Fascia");
        }

        // Part of the still model: not a pawn, not a rook, nothing on a body (the conveyor, the cannons, the rook pieces move).
        static bool Solid(Collider c)
        {
            if (c == null || c.attachedRigidbody != null || ImportedObstacleActor.Find(c) != null) return false;
            return !c.name.StartsWith("Goal_rook_");
        }

        // The floor under a point: its first solid, upward-facing surface near the hall's floor level (not a balustrade's
        // top, not the lower floor under the conveyor's well), looked for from a little over the goal's floor so the
        // lanterns hanging over the far end are not taken for it.
        bool FloorAt(Vector3 p, out Vector3 floor)
        {
            floor = default;
            var from = new Vector3(p.x, origin.y + 1.2f, p.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, rays, 12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = rays[i];
                if (h.distance >= best || !Solid(h.collider)) continue;
                best = h.distance;
                found = h.normal.y >= .7f && !Railing(h.collider) && h.point.y >= origin.y - 3f;
                if (found) floor = h.point;
            }
            return found;
        }

        Vector3 At(in Shot s, double t) => Vector3.Lerp(s.from, s.to, Mathf.Clamp01((float)((t - s.fire) / (s.impact - s.fire))));

        // ------------------------------------------------------------------ hits (the authority's)

        void FixedUpdate()
        {
            if (!ready) return;
            double now = ObstacleClock.Now;
            if (ObstacleClock.Authority && !double.IsNaN(lastFixed) && now > lastFixed && now - lastFixed < .5)
            {
                int k = Round(now);
                for (int j = k - 1; j <= k; j++)
                {
                    var s = ShotOf(j);
                    if (s.impact <= lastFixed || s.fire > now) continue;
                    if (struckShot != j) { struck.Clear(); struckShot = j; }
                    // the streak's path this step: whoever it passes through takes the full hit
                    double a = System.Math.Max(lastFixed, s.fire), b = System.Math.Min(now, s.impact);
                    int n = Physics.OverlapCapsuleNonAlloc(At(s, a), At(s, b), DirectRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
                    var push = Vector3.ProjectOnPlane(s.dir, Vector3.up).normalized;
                    for (int i = 0; i < n; i++)
                    {
                        var actor = ImportedObstacleActor.Find(overlaps[i]);
                        if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                        actor.AddObstacleHit(push * InnerPush + Vector3.up * InnerUp, InnerKnockdown);
                    }
                    if (s.impact > lastFixed && s.impact <= now) Blast(s);
                }
            }
            lastFixed = now;
        }

        void Blast(in Shot s)
        {
            int n = Physics.OverlapSphereNonAlloc(s.to, OuterRadius + 1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || struck.Contains(actor)) continue;
                var delta = actor.Position - s.to;
                if (Mathf.Abs(delta.y) > 2.2f) continue;
                var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                float distance = flat.magnitude;
                if (distance > OuterRadius) continue;
                struck.Add(actor);
                var outward = distance > .05f ? flat / distance : Vector3.ProjectOnPlane(s.dir, Vector3.up).normalized;
                bool inner = distance <= InnerRadius;
                actor.AddObstacleHit(outward * (inner ? InnerPush : OuterPush) + Vector3.up * (inner ? InnerUp : OuterUp),
                                     inner ? InnerKnockdown : 0f);
            }
        }

        // ------------------------------------------------------------------ the look, every frame

        void Update()
        {
            if (!ready) return;
            double now = ObstacleClock.Now;
            int k = Round(now);
            var s = ShotOf(k);
            var before = ShotOf(k - 1);
            float tau = (float)(now - RoundStart(k));
            float charge = tau < ChargeTime ? tau / ChargeTime : -1f;

            // the piece: rises through the charge, turns to its line, drops back after the shot
            float lift;
            if (charge >= 0f)
            {
                float rise = heavy ? Mathf.SmoothStep(0f, 1f, charge / .7f) : Mathf.SmoothStep(0f, 1f, charge / .35f);
                float shake = charge > .6f ? Mathf.Sin(Time.time * (heavy ? 70f : 90f)) * .006f * size * (charge - .6f) / .4f : 0f;
                lift = Lift * rise + shake;
            }
            else
            {
                float after = tau - ChargeTime;
                lift = after < Settle ? Lift * (1f - Mathf.SmoothStep(0f, 1f, after / Settle)) : 0f;
                if (after >= Settle && after < Settle + .15f) lift = -.01f * size * Mathf.Sin((after - Settle) / .15f * Mathf.PI);
            }
            float turn = charge >= 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(charge / .6f)) : 1f;
            float yaw = Mathf.LerpAngle(before.yaw, s.yaw, turn);
            piece.localPosition = pieceRestLocal + (piece.parent != null ? piece.parent.InverseTransformVector(Vector3.up * lift) : Vector3.up * lift);
            piece.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * pieceRestRot;

            // the light in the gap
            float glow = charge >= 0f ? Mathf.Pow(charge, heavy ? 1.6f : 1.2f) : 0f;
            if (charge > .7f) glow *= .8f + .2f * Mathf.Sin(Time.time * (heavy ? 45f : 60f));
            float sinceFire = (float)(now - s.fire);
            if (charge < 0f && sinceFire < .3f) glow = Mathf.Max(glow, 1.2f * (1f - sinceFire / .3f));
            Glow(glow, lift);
            if (charge >= 0f) Draw(charge);
            Laser(s, charge);

            // the shots in flight (this round's and, for a long streak, the last one's), their bursts
            bool fresh = !double.IsNaN(lastFrame) && now - lastFrame < .5;
            for (int j = k - 1; j <= k; j++)
            {
                var x = ShotOf(j);
                var streak = streaks[((j % 2) + 2) % 2];
                if (now >= x.fire && now < x.impact)
                {
                    if (streak.shot != j) Fire(streak, x);
                    var p = At(x, now);
                    var prev = !fresh || lastFrame < x.fire ? x.from : At(x, lastFrame);
                    streak.t.position = p;
                    ThroughPawns(x, prev, p);
                }
                else if (streak.shot == j && now >= x.impact && streak.r.enabled)
                {
                    streak.t.position = x.to;
                    streak.r.enabled = false;
                    streak.trail.emitting = false;
                }
                if (fresh && x.impact > lastFrame && x.impact <= now) Land(x);
                // R463: the round's sounds on their edges (a clock that jumped - a resync - plays nothing)
                if (fresh)
                {
                    double start = RoundStart(j);
                    if (start > lastFrame && start <= now) chargeSound.Shot(chargeNear, chargeFar, heavy ? 1f : .8f, Random.Range(.97f, 1.03f));
                    if (x.fire > lastFrame && x.fire <= now) fireSound.Shot(fireNear, fireFar, 1f, Random.Range(.95f, 1.05f));
                    if (x.impact > lastFrame && x.impact <= now)
                    {
                        impactPoint.position = x.to;
                        // R471 ("가운데 사운드 폭발음 더 크게"): the middle's burst played 1.6 times as loud (a one-shot's
                        // volume scale is not held to 1; about +4 dB over its file, already at full scale)
                        impactSound.Shot(impactNear, impactFar, heavy ? 1.6f : 1f, Random.Range(.93f, 1.05f));
                    }
                }
            }
            Animate();
            lastFrame = now;
        }

        void LateUpdate()
        {
            if (!ready) return;
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam == null) return;
            Face(flashT);
            if (laserDotR.enabled) Face(laserDotT);
            foreach (var st in streaks) if (st.r.enabled) Face(st.t);
            foreach (var b in bursts) if (b.flashR.enabled) Face(b.flash);
        }

        void Face(Transform t)
        {
            if (t != null && t.gameObject.activeSelf) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
        }

        // ------------------------------------------------------------------ parts of the look

        void Glow(float g, float lift)
        {
            var c = Gold * (heavy ? 4.5f : 3f) * g;
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                glowRenderers[i].GetPropertyBlock(block, glowSlots[i]);
                block.SetColor("_EmissionColor", Color.Lerp(Gold * .25f, c, Mathf.Clamp01(g * 3f)) + c);
                glowRenderers[i].SetPropertyBlock(block, glowSlots[i]);
                block.Clear();
            }
            bool on = g > .01f;
            bandR.enabled = discR.enabled = on;
            lamp.enabled = on;
            if (!on) return;
            float h = Mathf.Max(lift, .02f) + .12f * size;
            bandT.localScale = new Vector3(pieceRadius * 2.08f, h, pieceRadius * 2.08f);
            Tint(bandR, Gold * (heavy ? 3f : 2.2f) * g);
            discT.localScale = Vector3.one * pieceRadius * (2.6f + .8f * g);
            Tint(discR, Gold * 1.6f * g);
            lamp.intensity = (heavy ? 9f : 4f) * g;
        }

        // motes drawn into the gap; crackling sparks round the rim near the end of a heavy charge
        void Draw(float c)
        {
            var e = new ParticleSystem.EmitParams { startColor = Color.white };
            int n = Mathf.FloorToInt(Time.deltaTime * (heavy ? Mathf.Lerp(20f, 140f, c * c) : Mathf.Lerp(10f, 50f, c)) + Random.value);
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var round = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var from = gapRest + round * pieceRadius * Random.Range(1.6f, heavy ? 3.4f : 2.4f) + Vector3.up * Random.Range(-.25f, .6f) * size;
                float life = Random.Range(.35f, .6f);
                var spin = Vector3.Cross(Vector3.up, round) * Random.Range(.5f, 1.5f) * pieceRadius;
                e.position = from;
                e.velocity = (gapRest + Vector3.up * Lift * .5f - from) / life + spin;
                e.startLifetime = life;
                e.startSize = Random.Range(.04f, .08f) * (heavy ? 1.4f : 1f);
                motes.Emit(e, 1);
            }
            if (!heavy || c < .55f) return;
            int m = Mathf.FloorToInt(Time.deltaTime * 60f * (c - .55f) / .45f + Random.value);
            for (int i = 0; i < m; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var round = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                e.position = gapRest + round * pieceRadius + Vector3.up * Lift * Random.Range(.1f, .9f);
                e.velocity = Vector3.Cross(Vector3.up, round) * Random.Range(2f, 4f) + round * Random.Range(.4f, 1.4f) + Vector3.up * Random.Range(0f, 1f);
                e.startLifetime = Random.Range(.12f, .25f);
                e.startSize = Random.Range(.03f, .05f);
                crackle.Emit(e, 1);
            }
        }

        void Fire(Streak streak, in Shot s)
        {
            flashAt = Time.time;
            flashT.position = s.from;
            flashT.gameObject.SetActive(true);
            streak.shot = s.k;
            streak.trail.emitting = false;
            streak.t.position = s.from;
            streak.trail.Clear();
            streak.trail.emitting = true;
            streak.r.enabled = true;
            streak.t.localScale = Vector3.one * (heavy ? .75f : .32f);
        }

        // On every PC, a burst on each pawn the streak passes through (the hit itself is FixedUpdate's).
        void ThroughPawns(in Shot s, Vector3 a, Vector3 b)
        {
            if (!shown.TryGetValue(s.k, out var seen))
            {
                if (shown.Count > 4) shown.Clear();
                shown[s.k] = seen = new HashSet<ImportedObstacleActor>();
            }
            int n = Physics.OverlapCapsuleNonAlloc(a, b, DirectRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.isActiveAndEnabled || !seen.Add(actor)) continue;
                var at = actor.Position + Vector3.up * .45f;
                Pop(at, -s.dir, heavy ? 1.2f : .7f, false);
                Spray(at, s.dir, -s.dir, heavy ? 36 : 18, heavy ? 1f : .7f);
            }
        }

        // R455 ("레이저로(빨간 색) 예고를 해주고"): a thin red beam from the gap's rim to where the shot will land, through
        // the charge, brightening and thickening toward the shot, and a red spot where it lands
        void Laser(in Shot s, float charge)
        {
            bool on = charge >= 0f;
            laser.enabled = laserDotR.enabled = on;
            if (!on) return;
            float flicker = .85f + .15f * Mathf.Sin(Time.time * 50f);
            float c = Mathf.Lerp(.35f, 1f, charge) * flicker;
            laser.SetPosition(0, s.from);
            laser.SetPosition(1, s.to + s.normal * .03f);
            float width = (heavy ? .05f : .03f) * Mathf.Lerp(.6f, 1.6f, charge * charge);
            laser.startWidth = laser.endWidth = width;
            var red = new Color(3.2f, .25f, .18f) * c;
            laser.startColor = laser.endColor = red;
            laserDotT.position = s.to + s.normal * .06f;
            laserDotT.localScale = Vector3.one * (heavy ? .7f : .45f) * Mathf.Lerp(.7f, 1.3f, charge);
            Tint(laserDotR, red * 1.4f);
        }

        // where the streak meets the floor (or a wall): the big pop
        void Sounds()
        {
            string kind = heavy ? "Heavy" : "Side";
            chargeNear = Clip("RookCharge" + kind);
            chargeFar = Clip("RookCharge" + kind + "Far");
            fireNear = Clip("RookFire" + kind);
            fireFar = Clip("RookFire" + kind + "Far");
            impactNear = Clip("RookImpact" + kind);
            impactFar = Clip("RookImpact" + kind + "Far");
            chargeSound = new NearFarSound(piece, "Rook charge sound", null, null, false)
                { NearFull = 5f, NearEnd = 28f, FarStart = 6f, FarEnd = 60f, FarGain = .8f };
            fireSound = new NearFarSound(piece, "Rook shot sound", null, null, false)
                { NearFull = 8f, NearEnd = 38f, FarStart = 8f, FarEnd = 90f, FarGain = 1f };
            impactPoint = new GameObject("Rook impact sound").transform;
            impactPoint.SetParent(transform, false);
            impactSound = new NearFarSound(impactPoint, "Rook impact sound", null, null, false)
                // R470 ("b로 해서 소리 키워"): full up to 12 / 9 m (was 8 / 5), the close take heard out to 46 / 34 m
                // R471: the middle's full up to 18 m, its close take out to 60 m
                { NearFull = heavy ? 18f : 9f, NearEnd = heavy ? 60f : 34f, FarStart = 8f, FarEnd = heavy ? 110f : 90f, FarGain = 1.2f };
        }

        static AudioClip Clip(string name)
        {
            var clip = Resources.Load<AudioClip>(Sfx + name);
            if (clip == null) Debug.LogWarning("Rook sound missing: Resources/" + Sfx + name);
            return clip;
        }

        void Land(in Shot s)
        {
            // R455 ("터지는 임팩트를 더 시원하게"): a bigger flash and light, twice the sparks thrown harder, more embers,
            // a puff of dust for the side rooks too
            Pop(s.to + s.normal * .05f, s.normal, heavy ? 3.6f : 1.9f, true);
            var bounce = Vector3.Reflect(s.dir, s.normal);
            Spray(s.to + s.normal * .1f, bounce, s.normal, heavy ? 140 : 60, heavy ? 1.8f : 1.35f);
            Spray(s.to + s.normal * .1f, s.normal, s.normal, heavy ? 50 : 20, heavy ? 1.5f : 1.1f);
            var e = new ParticleSystem.EmitParams { startColor = Color.white };
            int glowing = heavy ? 55 : 22;
            for (int i = 0; i < glowing; i++)
            {
                e.position = s.to + s.normal * .15f + Random.insideUnitSphere * .2f;
                e.velocity = (s.normal * Random.Range(.5f, 2f) + Random.insideUnitSphere * 1.2f) * (heavy ? 1.3f : 1f);
                e.startLifetime = Random.Range(.6f, 1.2f);
                e.startSize = Random.Range(.06f, .12f) * (heavy ? 1.5f : 1f);
                embers.Emit(e, 1);
            }
            int puffs = heavy ? 26 : 8;
            for (int i = 0; i < puffs; i++)
            {
                var d = Random.insideUnitCircle.normalized;
                var flat = Vector3.ProjectOnPlane(new Vector3(d.x, 0f, d.y), s.normal).normalized;
                e.position = s.to + s.normal * .1f + flat * Random.Range(.3f, 1f);
                e.velocity = flat * Random.Range(2f, 4.5f) + s.normal * Random.Range(.4f, 1.2f);
                e.startLifetime = Random.Range(.9f, 1.5f);
                e.startSize = Random.Range(.6f, 1.1f) * (heavy ? 1f : .6f);
                e.rotation = Random.Range(0f, 360f);
                dust.Emit(e, 1);
            }
        }

        void Spray(Vector3 at, Vector3 along, Vector3 up, int count, float strength)
        {
            var e = new ParticleSystem.EmitParams { startColor = Color.white };
            for (int i = 0; i < count; i++)
            {
                var d = (along * Random.Range(.2f, 1f) + up * Random.Range(.3f, 1.1f) + Random.insideUnitSphere * .9f).normalized;
                e.position = at + Random.insideUnitSphere * .1f;
                e.velocity = d * Random.Range(3f, 9f) * strength;
                e.startLifetime = Random.Range(.25f, .6f) * strength;
                e.startSize = Random.Range(.03f, .06f) * (heavy ? 1.4f : 1f);
                sparks.Emit(e, 1);
            }
        }

        // a flash and a burst of light; on a pawn also a ring facing back up the streak (R450: none on the floor)
        void Pop(Vector3 at, Vector3 normal, float scale, bool onFloor)
        {
            var b = bursts[nextBurst];
            nextBurst = (nextBurst + 1) % bursts.Length;
            b.at = Time.time;
            b.scale = scale;
            b.life = onFloor ? (heavy ? .55f : .35f) : .3f;
            b.live = true;
            b.flash.position = at + normal * .1f;
            b.ring.position = at;
            b.ring.rotation = Quaternion.LookRotation(normal.sqrMagnitude > .01f ? -normal : Vector3.down);
            b.flashR.enabled = true;
            b.ringR.enabled = !onFloor;
            burstLamp.transform.position = at + normal * .5f;
            burstLampAt = Time.time;
            burstLampPower = onFloor ? (heavy ? 20f : 10f) : (heavy ? 8f : 4f);
        }

        // the muzzle flash, the bursts and their light, by time since they began
        void Animate()
        {
            float f = Time.time - flashAt;
            if (f < .14f)
            {
                flashT.localScale = Vector3.one * (heavy ? 1.4f : .6f) * Mathf.Lerp(.6f, 1.4f, f / .14f);
                Tint(flashR, Hot * (heavy ? 4f : 3f) * (1f - f / .14f));
            }
            else if (flashT.gameObject.activeSelf) flashT.gameObject.SetActive(false);
            foreach (var b in bursts)
            {
                if (!b.live) continue;
                float u = (Time.time - b.at) / b.life;
                if (u >= 1f) { b.live = b.flashR.enabled = b.ringR.enabled = false; continue; }
                float ease = 1f - (1f - u) * (1f - u);
                b.flashR.enabled = u < .4f;
                if (b.flashR.enabled)
                {
                    b.flash.localScale = Vector3.one * b.scale * Mathf.Lerp(.8f, 2f, Mathf.Sqrt(u / .4f));
                    Tint(b.flashR, Hot * 6f * (1f - u / .4f));
                }
                if (!b.ringR.enabled) continue;
                b.ring.localScale = Vector3.one * b.scale * Mathf.Lerp(.3f, 2.4f, ease);
                Tint(b.ringR, Hot * 2.6f * (1f - u));
            }
            float l = Time.time - burstLampAt;
            burstLamp.enabled = l < .35f;
            if (burstLamp.enabled) burstLamp.intensity = burstLampPower * Mathf.Exp(-l * 9f);
        }

        void Tint(Renderer r, Color c)
        {
            r.GetPropertyBlock(block);
            LitMaterials.SetColor(block, c);
            r.SetPropertyBlock(block);
            block.Clear();
        }

        // ------------------------------------------------------------------ building the look

        void Look()
        {
            block = new MaterialPropertyBlock();
            Textures();
            // the socket's glow material slots on the pedestal
            var rs = new List<Renderer>();
            var slots = new List<int>();
            foreach (var r in (pedestal != null ? pedestal : transform).GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith("Library rook glow")) { rs.Add(r); slots.Add(i); }
            }
            glowRenderers = rs.ToArray();
            glowSlots = slots.ToArray();

            // the effects hang off the rook in world units and world axes, whatever scale or turn the FBX gave its nodes
            var holder = new GameObject("Rook turret fx").transform;
            holder.SetPositionAndRotation(gapRest, Quaternion.identity);
            holder.SetParent(transform, true);
            // a band of light round the gap (a short open cylinder, brightest at its foot)
            bandT = new GameObject("Rook gap light").transform;
            bandT.SetParent(holder, false);
            bandT.SetPositionAndRotation(gapRest, Quaternion.identity);
            bandT.gameObject.AddComponent<MeshFilter>().sharedMesh = BandMesh();
            bandR = bandT.gameObject.AddComponent<MeshRenderer>();
            bandR.sharedMaterial = Additive(band, Gold);
            bandR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discT = Flat(holder, "Rook gap spill", dot, out discR);
            discT.position = gapRest + Vector3.up * .005f;
            flashT = Quad(holder, "Rook muzzle flash", dot, out flashR);
            laser = new GameObject("Rook laser warning").AddComponent<LineRenderer>();
            laser.transform.SetParent(holder, false);
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.numCapVertices = 2;
            laser.sharedMaterial = Additive(beam, Color.white);
            laser.textureMode = LineTextureMode.Stretch;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
            laser.enabled = false;
            laserDotT = Quad(holder, "Rook laser spot", dot, out laserDotR);
            laserDotR.enabled = false;
            flashT.gameObject.SetActive(false);
            for (int i = 0; i < streaks.Length; i++)
            {
                var st = new Streak();
                st.t = Quad(holder, "Rook shot " + i, dot, out st.r);
                st.r.enabled = false;
                st.trail = st.t.gameObject.AddComponent<TrailRenderer>();
                st.trail.time = heavy ? .2f : .12f;       // about 12 and 9.6 m of streak
                st.trail.minVertexDistance = .15f;
                st.trail.widthCurve = new AnimationCurve(new Keyframe(0f, heavy ? .62f : .22f), new Keyframe(.25f, heavy ? .5f : .16f), new Keyframe(1f, 0f));
                st.trail.sharedMaterial = Additive(dot, Gold * 2.6f);
                st.trail.colorGradient = Fade(Hot, Gold);
                st.trail.emitting = false;
                st.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                streaks[i] = st;
            }
            for (int i = 0; i < bursts.Length; i++)
            {
                var b = new Burst();
                b.flash = Quad(holder, "Rook burst flash " + i, dot, out b.flashR);
                b.ring = Quad(holder, "Rook burst ring " + i, ring, out b.ringR);
                b.flashR.enabled = b.ringR.enabled = false;
                bursts[i] = b;
            }

            lamp = Lamp(holder, "Rook gap lamp", gapRest + Vector3.up * .15f * size, heavy ? 7f : 4f);
            burstLamp = Lamp(holder, "Rook burst lamp", gapRest, heavy ? 9f : 6f);

            motes = Particles(holder, "Rook charge motes", dot, Gold * 2f, true, 400, 0f);
            crackle = Particles(holder, "Rook crackle", dot, Hot * 3f, true, 300, 0f);
            sparks = Particles(holder, "Rook burst sparks", dot, Hot * 3f, true, 600, 1.3f);
            embers = Particles(holder, "Rook burst embers", dot, Gold * 2.2f, false, 200, -.08f);
            var shrink = embers.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .2f));
            dust = Particles(holder, "Rook burst dust", dot, new Color(.62f, .55f, .46f, .5f), false, 120, -.05f);
            var grow = dust.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2.6f));
        }

        static Light Lamp(Transform parent, string name, Vector3 at, float reach)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Gold;
            l.range = reach;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        static Mesh BandMesh()
        {
            const int seg = 32;
            var m = new Mesh { name = "Rook gap light" };
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                var p = new Vector3(Mathf.Cos(a) * .5f, 0f, Mathf.Sin(a) * .5f);
                v[i * 2] = p;
                v[i * 2 + 1] = p + Vector3.up;
                uv[i * 2] = new Vector2((float)i / seg, 0f);
                uv[i * 2 + 1] = new Vector2((float)i / seg, 1f);
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i * 2, t = i * 6;
                tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2;
                tri[t + 3] = b + 2; tri[t + 4] = b + 1; tri[t + 5] = b + 3;
            }
            m.vertices = v;
            m.uv = uv;
            m.triangles = tri;
            m.RecalculateBounds();
            return m;
        }

        static Gradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Material Additive(Texture2D texture, Color tint)
        {
            var m = LitMaterials.NewAdditiveParticles(texture, tint);
            if (m == null) return null;
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.name = "Rook turret fx";
            return m;
        }

        static Transform Quad(Transform parent, string name, Texture2D texture, out Renderer r)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(parent, false);
            r = q.GetComponent<Renderer>();
            r.sharedMaterial = Additive(texture, Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        static Transform Flat(Transform parent, string name, Texture2D texture, out Renderer r)
        {
            var t = Quad(parent, name, texture, out r);
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            return t;
        }

        static ParticleSystem Particles(Transform parent, string name, Texture2D texture, Color tint, bool additive, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            Material m;
            if (additive) m = Additive(texture, tint);
            else
            {
                var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
                m = shader != null ? new Material(shader) { mainTexture = texture, color = tint, name = name } : null;
                if (m != null) LitMaterials.MakeTransparent(m, false);
            }
            r.sharedMaterial = m;
            r.renderMode = additive ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = .05f;
            r.lengthScale = 1.4f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.8f));
            ring = Draw(128, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y) - .86f;
                return Mathf.Clamp01(Mathf.Exp(-Mathf.Pow(d / .05f, 2f)) + .3f * Mathf.Exp(-Mathf.Pow(d / .14f, 2f)));
            });
            // the laser: a bright core across its width (v), the same all along (u)
            beam = Draw(64, (x, y) => Mathf.Exp(-Mathf.Pow(y / .35f, 2f)) + .25f * Mathf.Exp(-Mathf.Pow(y / .9f, 2f)));
            // the gap's band: bright at its foot (v 0), gone at its top, a little flicker of streaks round it
            band = Draw(64, (x, y) =>
            {
                float v = (y + 1f) * .5f;
                return Mathf.Pow(1f - v, 2.2f) * (.75f + .25f * Mathf.PerlinNoise(x * 6f + 3f, 1.7f));
            });
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Rook turret fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    byte b = (byte)(Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f)) * 255f);
                    pixels[j * n + i] = new Color32(b, b, b, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
