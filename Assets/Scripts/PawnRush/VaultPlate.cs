using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // One of the royal vault's flipping plates (V09, R725, build spec §2.1 and §2.11). This object sits on the plate's hinge
    // line, its outer edge: the deck runs `wide` inward along local +x and `longSide` along local +z, and the inward (+x) edge
    // lifts as it turns about local +z, as TiltingWoodenBridge's LongSide hinge does. The angle is RoyalVaultRules.PlateAngle
    // of its row, a pure function of the obstacle clock (HANDOFF absolute rule 7); the rows run half a cycle apart, so one
    // always lies flat.
    //
    // Riders slide off by the deck's real turning and a slippery deck while it is steeper than PlateSlipAngle, plus the
    // capped pull down the slope TiltingWoodenBridge gives (its slideAssist through ImportedObstacleActor.Accelerate) on the
    // PC that moves them (ObstacleClock.Authority); nobody is moved by script otherwise. They go out over the hinge into the
    // pit and come back at CP4.
    //
    // Configure builds the deck at once, not in Awake: the validators build the vault in Edit mode, where Awake never runs, and
    // must still find the deck, its collider and its CourseFloor (the rim cue is the cue builder's child of this root).
    public sealed class VaultPlate : Obstacle
    {
        public const string DeckName = "RV plate deck";

        // TiltingWoodenBridge's slideAssist (m/s^2) and its speed cap down the slope (m/s); the see-through threshold (deg).
        const float SlideAssist = 6f, SlideLimit = 4f, SeeAngle = .5f;

        [SerializeField] int row;
        [SerializeField] float wide = (float)RoyalVaultRules.PlateWide;
        [SerializeField] float longSide = (float)RoyalVaultRules.PlateLong;

        static PhysicsMaterial wet;

        GameObject deck;
        Collider deckCollider;
        CameraSeeThrough see;
        bool built;
        float angle;
        readonly HashSet<Collider> riders = new HashSet<Collider>();
        readonly HashSet<ImportedObstacleActor> pulled = new HashSet<ImportedObstacleActor>();

        // 0 the west row, 1 the east row.
        public int Row => row;
        // The deck's collider (its transform's up shows which way the plate tilts).
        public Collider Deck => deckCollider;
        // Degrees the inner edge was lifted at the last physics step.
        public float Angle => angle;
        // Steeper than PlateSlipAngle at the last physics step: the deck is wet and pulls its riders down.
        public bool Slipping => angle > (float)RoyalVaultRules.PlateSlipAngle;

        // On an inactive object, before Awake. Builds the deck now, once: a later call changes the row, not the deck.
        public void Configure(int row, float wide = (float)RoyalVaultRules.PlateWide, float longSide = (float)RoyalVaultRules.PlateLong)
        {
            this.row = row;
            this.wide = wide;
            this.longSide = longSide;
            Build();
        }

        protected override void Awake()
        {
            Build();   // a no-op after Configure
            base.Awake();
        }

        void Build()
        {
            if (built) return;
            built = true;
            // A deck already under this root (a copy of a built plate) is kept, not doubled.
            var old = transform.Find(DeckName);
            deck = old != null
                ? old.gameObject
                : YardKit.Box(transform, DeckName, new Vector3(wide * .5f, -.15f, longSide * .5f), new Vector3(wide, .3f, longSide),
                              RoyalVaultLook.Greybox, YardKit.Solid.NoClimb);
            if (!deck.TryGetComponent<CourseFloor>(out _)) deck.AddComponent<CourseFloor>();
            if (!deck.TryGetComponent(out see))
            {
                see = deck.AddComponent<CameraSeeThrough>();
                see.enabled = false;
            }
            deckCollider = deck.GetComponent<Collider>();
        }

        // TiltingWoodenBridge's wet planks: one material shared by every plate, made the first time a deck turns steep (in
        // Play only, so the Edit-mode builds leave none behind).
        static PhysicsMaterial Wet
        {
            get
            {
                if (wet == null)
                    wet = new PhysicsMaterial("RV wet plate")
                    {
                        dynamicFriction = .04f,
                        staticFriction = .04f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounciness = 0f,
                        hideFlags = HideFlags.DontSave
                    };
                return wet;
            }
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            // Turning about local +z lifts the local +x (inner) edge.
            rotation = StartRotation * Quaternion.AngleAxis((float)RoyalVaultRules.PlateAngle(row, time), Vector3.forward);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            angle = (float)RoyalVaultRules.PlateAngle(row, ObstacleClock.Now + phase);
            bool tilted = angle > SeeAngle;
            if (see != null && see.enabled != tilted) see.enabled = tilted;
            bool slip = Slipping;
            if (deckCollider != null)
            {
                var surface = slip ? Wet : null;
                if (deckCollider.sharedMaterial != surface) deckCollider.sharedMaterial = surface;
            }
            if (!slip || !ObstacleClock.Authority || deck == null)
            {
                riders.Clear();
                return;
            }
            // Down the deck's slope, along its surface: out over the hinge.
            var downhill = Vector3.ProjectOnPlane(Vector3.down, deck.transform.up).normalized;
            pulled.Clear();
            foreach (var c in riders)
            {
                var actor = ImportedObstacleActor.Find(c);
                if (actor == null || !pulled.Add(actor)) continue;
                actor.Accelerate(downhill * SlideAssist, SlideLimit, Time.fixedDeltaTime);
            }
            riders.Clear();
        }

        // The deck's collisions arrive here (it is this body's collider): whoever touches it this step. Only collected; the
        // pull is given in the next FixedUpdate (HANDOFF absolute rule 14).
        void OnCollisionStay(Collision c)
        {
            if (c.collider != null && c.collider.GetComponentInParent<ICharacterDriver>() != null) riders.Add(c.collider);
        }
    }
}
