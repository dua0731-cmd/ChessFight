using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R534 (사용자 11-10, 소드 파이트 멀티플레이): each screen draws the other side's skill effects red (white's player sees
    /// black's skills red, black's player sees white's red); its own side's, its own included, keep their colours. Online
    /// only (a real match or the loopback test): offline and in the test bed nothing changes. The effects say whose they are
    /// by being made inside <see cref="Owned"/> (the skill events, the per-frame marks of each fighter), the kit keeps that
    /// owner on the effects they start, and <see cref="SkillTeamTint"/> turns a foe's red after they have stepped.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>Off: every side's effects in their own colours, as before R534.</summary>
        public static bool TeamTintOn = true;

        SkillTeamTint teamTint;

        void SetUpTeamTint()
        {
            teamTint = new SkillTeamTint();
            kit.Claimed = (owner, t) => { if (!Shared(t)) teamTint.Add(owner, t); };
            kit.Foe = IsFoe;
        }

        /// <summary>The holders every piece's effects share, made the first time one needs them (the air, the dust, the
        /// arcs): never one piece's.</summary>
        static bool Shared(Transform t) =>
            t.name == "Skill air" || t.name == "Skill dust" || t.name.StartsWith("Skill arcs");

        /// <summary>A colour this skill's own piece paints its screen wash or a part with: red if it is a foe's.</summary>
        Color SideTint(Color c) => IsFoe(kit.Owner) ? SkillTeamTint.Red(c) : c;

        /// <summary>This screen's player is on one side and <paramref name="pawn"/> on the other (online only).</summary>
        bool IsFoe(RagdollPawn pawn) => TeamTintOn && OtherSide(pawn);

        bool OtherSide(RagdollPawn pawn)
        {
            if (pawn == null || !Online || ViewOverride != null) return false;
            var v = Viewer;
            if (v == null || pawn == v) return false;
            return v.Team != Teams.None && pawn.Team != Teams.None && pawn.Team != v.Team;
        }

        /// <summary>
        /// R535 (사용자 11-10: "내가 미리보기로 상대를 조준 하거나 그런거 상대가 볼 수 없게 해"): online, a skill's aim and windup
        /// previews (the queen's and the rook's lanes of light, the bishop's ring and X, the knight's landing and fork
        /// squares, the edge skills' line, circle and landing, design A's warning squares) are not drawn on the other side's
        /// screens; mine and my side's still see them. The skill itself (the cast, the dash, the blow, the rook's crack in
        /// the board) everyone sees as before. Off: everyone sees every preview, as before R535.
        /// </summary>
        public static bool HidePreviewsFromFoes = true;

        /// <summary>True while the marks of a fighter this screen must not see the previews of are being drawn.</summary>
        bool previewHidden;

        bool PreviewHidden(SwordFightSkills s) => HidePreviewsFromFoes && s != null && OtherSide(s.Pawn);

        /// <summary>Whose effects are being made from here on (<see cref="EndOwned"/> hands what was made to the tint).</summary>
        int Owned(RagdollPawn owner)
        {
            kit.Owner = owner;
            return kit.root.childCount;
        }

        void EndOwned(int from)
        {
            kit.ClaimFrom(from);
            kit.Owner = null;
        }

        /// <summary>A prefab effect just played (it may be hung on a body next, out of the kit's root).</summary>
        GameObject Claimed(GameObject go)
        {
            kit.Claim(go);
            return go;
        }

        void ApplyTeamTint() => teamTint?.Apply(IsFoe);
    }
}
