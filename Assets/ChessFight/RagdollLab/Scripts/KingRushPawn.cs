using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // All mode state lives beside the shared pawn. No mass/drive/tuning writes.
    [RequireComponent(typeof(RagdollPawn), typeof(RagdollDriver))]
    public sealed class KingRushPawn : MonoBehaviour, IKingRushCharacter
    {
        public RagdollPawn Pawn { get; private set; }
        public ulong Id { get; private set; }
        public int Team => Pawn.Team;
        public KingRushPiece Piece { get; private set; }
        public bool FixedKing { get; private set; }
        public KingRushSection Section { get; set; }
        public Vector3 BodyPosition => Pawn.Hips.position;
        public bool CountsAsBody => !Pawn.Floating && !Pawn.Launched && !Pawn.BeingHeld;
        public bool StandingOnPad => CountsAsBody && Pawn.Grounded && Pawn.State == PawnState.Active && !Pawn.Climbing;
        public Collider[] BodyColliders { get; private set; }
        public IHitReceiver HitReceiver { get; private set; }
        public bool AbilityActive => windup > 0;
        public float Cooldown01 => Mathf.Clamp01(cooldown / 12f);
        public int Checks { get; private set; }
        public bool AbilityHeld { get; private set; }
        public bool AbilitiesEnabled
        {
            get => abilitiesEnabled;
            set { abilitiesEnabled = value; if (!value) CancelAbility(); }
        }
        KingRushMatch match;
        bool abilitiesEnabled, abilityPressed;
        float windup, cooldown;
        int hitsAtStart;
        TextMesh label;
        LineRenderer warning, recharge;
        Material lineMaterial;

        public void Initialize(KingRushMatch owner, ulong id, bool king)
        {
            Pawn = GetComponent<RagdollPawn>(); match = owner; Id = id; FixedKing = king;
            Piece = king ? KingRushPiece.King : KingRushPiece.Pawn;
            BodyColliders = GetComponentsInChildren<Collider>(); HitReceiver = GetComponent<RagdollDriver>();
            match.Register(this);
            label = KingRushPrototype.Label(transform, "Piece", Vector3.zero, "", .025f);
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            warning = Ring("Check warning", .035f, new Color(1, .3f, .15f));
            recharge = Ring("Ability cooldown", .025f, new Color(1, .82f, .12f));
        }
        LineRenderer Ring(string title, float width, Color color)
        {
            var line = new GameObject(title).AddComponent<LineRenderer>(); line.transform.SetParent(transform);
            line.sharedMaterial = lineMaterial; line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color; line.useWorldSpace = true; line.enabled = false;
            return line;
        }
        public bool Promote(KingRushPiece piece)
        {
            if (FixedKing || !KingRushPieces.CanPromote(Piece, piece)) return false;
            Piece = piece; CancelAbility(); return true;
        }
        public void LeaveBlue()
        { AbilitiesEnabled = false; if (!FixedKing) Piece = KingRushPiece.Pawn; }
        public void ResetForRound()
        { LeaveBlue(); Section = KingRushSection.Red1; cooldown = 0; Checks = 0; }
        public void SetInput(PawnInput input, bool held = false)
        {
            abilityPressed |= input.ability; AbilityHeld = held;
            // Never send King Rush E/Q to the Queen Hill hook on the shared pawn.
            input.ability = input.ability2 = false; Pawn.SetInput(input);
        }
        public void CancelAbility() { windup = 0; abilityPressed = false; AbilityHeld = false; }
        public void Step(float dt)
        {
            if (!match.Authority) { CancelAbility(); return; }
            cooldown = Mathf.Max(0, cooldown - dt);
            bool allowed = AbilitiesEnabled && !Pawn.Floating && Pawn.State == PawnState.Active;
            if (!allowed || (AbilityActive && Pawn.Hits != hitsAtStart)) CancelAbility();
            if (AbilityActive)
            {
                windup -= dt;
                if (windup <= 0)
                {
                    Checks++; cooldown = 12;
                    foreach (var target in match.Characters)
                    {
                        if (target.Id == Id || (target.BodyPosition - BodyPosition).sqrMagnitude > 16f) continue;
                        Vector3 away = target.BodyPosition - BodyPosition; away.y = 0;
                        if (away.sqrMagnitude < .0001f) away = Pawn.Facing;
                        target.CancelAbility();
                        foreach (var holder in match.Characters) holder.ReleaseHoldOn(target);
                        target.HitReceiver.ApplyHit(away.normalized * 6 + Vector3.up, 1, 0, true);
                    }
                }
            }
            // Other five abilities intentionally remain unavailable in this milestone.
            if (abilityPressed && allowed && Piece == KingRushPiece.King && cooldown <= 0)
            { windup = .5f; cooldown = 12; hitsAtStart = Pawn.Hits; }
            abilityPressed = false;
        }
        public void Respawn(Vector3 ground)
        {
            CancelAbility(); AbilitiesEnabled = false;
            ((ICharacterDriver)HitReceiver).Teleport(ground, Quaternion.identity);
            Pawn.SetInput(default); // Clear held movement/grab along with latched actions.
        }
        public void ReleaseHoldOn(IKingRushCharacter target)
        {
            foreach (var body in target.BodyColliders)
            {
                if (Pawn.handL.HeldCollider == body) Pawn.handL.Release(.6f);
                if (Pawn.handR.HeldCollider == body) Pawn.handR.Release(.6f);
            }
        }
        void LateUpdate()
        {
            if (Pawn == null) return;
            label.transform.position = BodyPosition + Vector3.up * 1.05f;
            if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
            label.text = (Team == 0 ? "백 " : "흑 ") + KingRushPieces.Name(Piece) + (FixedKing ? " [고정]" : "");
            label.color = Team == 0 ? new Color(1, .94f, .65f) : new Color(.7f, .85f, 1);
            DrawRing(warning, new Vector3(BodyPosition.x, .04f, BodyPosition.z), 4, AbilityActive ? 1 : 0);
            DrawRing(recharge, BodyPosition + Vector3.up * 1.25f, .26f, Cooldown01);
        }
        static void DrawRing(LineRenderer line, Vector3 center, float radius, float fraction)
        {
            line.enabled = fraction > 0;
            if (!line.enabled) return;
            line.positionCount = 65;
            for (int i = 0; i <= 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2 * fraction;
                line.SetPosition(i, center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius);
            }
        }
        void OnDestroy()
        { if (match != null) match.Unregister(this); if (lineMaterial != null) Destroy(lineMaterial); }
    }
}
