using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The crown press's shock band (V05, R725, build spec §2.1 and §2.6): as the die lands, a wave runs out from its two long
    // faces across the full width of the neck, from the die's edge to the band's (PressBandOut) in PressWaveSeconds. Whoever
    // it reaches with a foot on the neck's floor is knocked down and pushed straight out along the way (-z or +z, never into
    // the walls; the part towards the way on cut to 60 % as every yard hit), the push setting the speed across the floor
    // whatever they were doing; whoever it reaches in the air - a jump timed to the landing - or standing up on something is
    // spared. One landing is one hit (spec D4): under the die's own footprint, and wherever they have got to since, whoever
    // the die's crush shell (YardImpact) hit as it came down is the crush's and is left alone.
    //
    // A copy of SlamChest's shockwave with band geometry instead of a circle: keep the two alike. The original is
    // SlamChest.FixedUpdate, Shock and Lowest (SlamChest.cs:280-360 at R702; :278-346 when the spec was written, R691). Two
    // differences, both R725: the crush's hits are left out by who was hit, not only by where they stand (a pawn the crush
    // pushed out slower than the wave runs - +z, cut to 60 %, or a queen - was caught again past the footprint); and the
    // band's ends are judged once - SlamChest goes on judging its whole circle through the 0.1 s of slack, which here knocked
    // down whoever stepped into the band then, while the band's cue already said go.
    //
    // Timing comes from RoyalVaultRules on the obstacle clock (the cycle starts at a landing). Judged once per character per
    // landing on the PC that moves the characters (ObstacleClock.Authority, YardKit.Movable), in FixedUpdate, not a physics
    // callback (a hit lets go of what the hands hold, HANDOFF absolute rule 14). Knocked down by another hazard within
    // YardKit.ChainGrace, a character is only pushed. This object stands still at the die's centre on the neck's floor.
    public sealed class VaultPressWave : MonoBehaviour
    {
        // The die's crush shell's name (the builder names it so; Awake finds it by it if none was configured).
        public const string CrushName = "RV press crush";

        // Who may be reached (m, round this object), and how high a lowest part may be over the floor and still count as a
        // foot on it (SlamChest's CargoSlam.groundTolerance). The band reaches PressDieAcross / 2 + Beside across. UnderDie:
        // hips this far past the die's faces are the crush's too - the shell reports a hit one physics step after it found it,
        // and this covers the wave's first steps, when its front is still within it.
        const float NearRadius = 7f, GroundTolerance = .4f, Beside = .3f, UnderDie = .35f, AheadCut = .4f;

        [Tooltip("The section's root: its frame is the vault's (x across, +z the way on)")]
        [SerializeField] Transform section;
        [Tooltip("The neck's floor, in the section's frame (m)")]
        [SerializeField] float floorLocalY = 4f;
        [Tooltip("The die's crush shell: whoever it hits as the die comes down, the wave leaves alone that landing")]
        [SerializeField] YardImpact crush;

        double judgedTrip = double.NaN, rimTrip = double.NaN, crushTrip = double.NaN;
        readonly HashSet<ICharacterDriver> judged = new HashSet<ICharacterDriver>();
        readonly HashSet<ICharacterDriver> crushed = new HashSet<ICharacterDriver>();
        readonly List<Collider> parts = new List<Collider>();

        // Characters the wave knocked (or pushed, if just knocked down by another hazard) and spared, since it woke.
        public int Knocked { get; private set; }
        public int Spared { get; private set; }

        public void Configure(Transform section, float floorLocalY = 4f, YardImpact crush = null)
        {
            this.section = section;
            this.floorLocalY = floorLocalY;
            this.crush = crush;
        }

        void Awake()
        {
            // Content sits on the section's root with no offset, so its frame is the section's.
            if (section == null) section = transform.parent;
            if (crush == null && transform.parent != null)
                foreach (var impact in transform.parent.GetComponentsInChildren<YardImpact>(true))
                    if (impact.name == CrushName)
                    {
                        crush = impact;
                        break;
                    }
        }

        void OnEnable() => YardKit.HitGiven += OnHitGiven;

        void OnDisable() => YardKit.HitGiven -= OnHitGiven;

        // The crush shell gives its hits as the die comes down and up to a physics step after it lands (YardImpact hands a
        // hit out in the FixedUpdate after the trigger found it), on the same PC as the wave: so the hit belongs to the
        // landing nearest the clock.
        void OnHitGiven(ICharacterDriver driver, Component source, Vector3 push, float knockdown)
        {
            if (driver == null || crush == null || source != crush) return;
            double landing = Math.Floor(ObstacleClock.Now / RoyalVaultRules.PressPeriod + .5);
            if (landing != crushTrip)
            {
                crushTrip = landing;
                crushed.Clear();
            }
            crushed.Add(driver);
        }

        void FixedUpdate()
        {
            if (!ObstacleClock.Authority || section == null) return;
            double now = ObstacleClock.Now;
            double since = RoyalVaultRules.PressSinceLanding(now);
            // A frame or two of slack after the wave's end, so a long step does not skip its rim.
            if (since > RoyalVaultRules.PressWaveSeconds + .1) return;
            // This landing's number, as OnHitGiven counts them (rounded, so it cannot flip at the landing itself).
            double trip = Math.Round((now - since) / RoyalVaultRules.PressPeriod);
            if (trip != judgedTrip)
            {
                judgedTrip = trip;
                judged.Clear();
            }
            // Once the wave has reached the band's ends it is over: whoever steps into the band after that is not reached.
            if (rimTrip == trip) return;
            double front = Math.Min(1.0, since / RoyalVaultRules.PressWaveSeconds);
            Shock(front, trip);
            if (front >= 1.0) rimTrip = trip;
        }

        // Everyone the wave has reached by now (`front` 0 at the die's long faces .. 1 at the band's ends) and not judged this
        // landing (`trip`).
        void Shock(double front, double trip)
        {
            float x0 = (float)RoyalVaultRules.PressX, z0 = (float)RoyalVaultRules.PressZ;
            float across = (float)(RoyalVaultRules.PressDieAcross / 2) + Beside;
            float reach = (float)(RoyalVaultRules.PressDieAlong / 2 + front * RoyalVaultRules.PressBandOut);
            float under = (float)(RoyalVaultRules.PressDieAlong / 2) + UnderDie;
            float floor = section.TransformPoint(new Vector3(x0, floorLocalY, z0)).y;
            var way = new Vector3(section.forward.x, 0f, section.forward.z).normalized;
            foreach (var driver in YardKit.CharactersNear(transform.position, NearRadius))
            {
                if (driver == null || driver.FollowTarget == null || judged.Contains(driver)) continue;
                var local = section.InverseTransformPoint(driver.FollowTarget.position);
                float dz = local.z - z0;
                if (Mathf.Abs(local.x - x0) > across || Mathf.Abs(dz) > reach) continue;
                judged.Add(driver);
                // Under the die's own footprint, or hit by its crush shell this landing wherever they are now: the crush's.
                if (Mathf.Abs(dz) < under || (crushTrip == trip && crushed.Contains(driver))) continue;
                if (!(driver is Component owner)) continue;
                if (!YardKit.Movable(owner.GetComponentInChildren<Collider>(), out _, out var hits)) continue;
                if (Lowest(owner) > floor + GroundTolerance)
                {
                    Spared++;
                    continue;
                }
                // Straight out of the band along the way, to the side the character is on; the way on keeps 60 %.
                var push = way * (Mathf.Sign(dz) * (float)RoyalVaultRules.PressWavePush);
                float ahead = Vector3.Dot(push, way);
                if (ahead > 0f) push -= way * (ahead * AheadCut);
                // The push sets the speed across the floor, whatever the character was doing (YardImpact.replaceSpeed).
                var body = driver.FollowTarget.GetComponent<Rigidbody>();
                var piece = owner.GetComponentInParent<IPromotable>();
                float weight = ChessPieces.Stats(piece != null ? piece.Piece : PieceKind.Pawn).Weight;
                if (body != null) push -= new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z) * weight;
                // Knocked down a moment ago by another hazard: pushed, not knocked down again.
                bool fresh = !YardKit.RecentlyDown(driver);
                float down = fresh ? (float)RoyalVaultRules.PressKnockdown : 0f;
                hits.ApplyHit(push + Vector3.up * (float)RoyalVaultRules.PressWaveLift, down, 0f, true);
                YardKit.ReportHit(driver, this, push, down);
                if (fresh) YardKit.MarkDown(driver);
                Knocked++;
            }
        }

        // The lowest point of a character's solid parts (its feet standing, its back lying): SlamChest.Lowest.
        float Lowest(Component owner)
        {
            owner.GetComponentsInChildren(parts);
            float low = float.MaxValue;
            foreach (var part in parts)
                if (part != null && !part.isTrigger && part.enabled) low = Mathf.Min(low, part.bounds.min.y);
            parts.Clear();
            return low == float.MaxValue ? owner.transform.position.y : low;
        }
    }
}
