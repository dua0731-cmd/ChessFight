using UnityEngine;

namespace ChessFight.PawnRush
{
    // R643, G · 왕의 행차 회랑 (KingEscort): the procession's door opens under two banners. A brass rod with ivory
    // finials slams up beside each side of the doorway and its banner drops open in 0.16 s ("촥"): ivory cloth, an
    // ebony band down the middle, a brass hem at the top and a brass band near the foot. It sways a little, hangs,
    // and rolls back up into the rod. 3.4 s. See MiniGameGateMoment.
    public sealed class MiniGameProcessionGateFx : MiniGameGateMoment
    {
        const float Width = .92f, Drop = 3.1f, Top = OpeningTop + .35f, Front = .3f;

        Part leftRod, rightRod, leftCloth, rightCloth;

        protected override float Length => 3.4f;

        protected override void Build()
        {
            float x = HalfOpening + .62f;
            (leftRod, leftCloth) = Banner(-x);
            (rightRod, rightCloth) = Banner(x);
        }

        (Part rod, Part cloth) Banner(float x)
        {
            var rod = Pivot("G banner rod", new Vector3(x, Top, Front));
            var bar = Shape(PrimitiveType.Cylinder, "G rod", Vector3.zero, new Vector3(.07f, (Width + .24f) * .5f, .07f), Brass, rod.T);
            bar.T.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Shape(PrimitiveType.Sphere, "G finial", new Vector3(-(Width * .5f + .14f), 0f, 0f), Vector3.one * .13f, Ivory, rod.T);
            Shape(PrimitiveType.Sphere, "G finial", new Vector3(Width * .5f + .14f, 0f, 0f), Vector3.one * .13f, Ivory, rod.T);
            // the cloth hangs from the rod: its pivot at the top, the cloth below it
            var cloth = Pivot("G banner cloth", new Vector3(0f, -.04f, 0f), rod.T);
            Box("G cloth", new Vector3(0f, -Drop * .5f, 0f), new Vector3(Width, Drop, .025f), Ivory, cloth.T);
            Box("G band", new Vector3(0f, -Drop * .5f, .018f), new Vector3(Width * .28f, Drop, .012f), Ebony, cloth.T);
            Box("G hem", new Vector3(0f, -.06f, .02f), new Vector3(Width, .1f, .014f), Brass, cloth.T);
            Box("G foot band", new Vector3(0f, -Drop + .32f, .02f), new Vector3(Width, .08f, .014f), Brass, cloth.T);
            // a pointed foot: two ivory halves turned in, an ebony tip
            var tipL = Box("G point", new Vector3(-Width * .25f, -Drop - .1f, 0f), new Vector3(Width * .56f, .1f, .025f), Ivory, cloth.T);
            tipL.T.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var tipR = Box("G point", new Vector3(Width * .25f, -Drop - .1f, 0f), new Vector3(Width * .56f, .1f, .025f), Ivory, cloth.T);
            tipR.T.localRotation = Quaternion.Euler(0f, 0f, 18f);
            return (rod, cloth);
        }

        protected override void Pose(float t)
        {
            Pose(leftRod, leftCloth, t);
            Pose(rightRod, rightCloth, t - .05f);
        }

        static void Pose(Part rod, Part cloth, float t)
        {
            // the rod lands, the cloth drops open (overshooting a little and settling), sways, rolls up, the rod goes
            float rodScale = t < 0f ? 0f : Slam(t) * Fade(t, 3.1f, .25f);
            rod.T.localScale = Vector3.one * Mathf.Max(.0001f, rodScale);
            float drop;
            if (t < .04f) drop = 0f;
            else if (t < .2f) drop = Mathf.Lerp(0f, 1.06f, Out01((t - .04f) / .16f));
            else if (t < .3f) drop = Mathf.Lerp(1.06f, 1f, (t - .2f) / .1f);
            else drop = 1f;
            drop *= Fade(t, 2.6f, .45f);
            cloth.T.localScale = new Vector3(1f, Mathf.Max(.0001f, drop), 1f);
            // the sway: a few degrees back and forth about the rod, dying away
            float sway = t > .2f ? 5f * Mathf.Exp(-(t - .2f) * 2.2f) * Mathf.Sin((t - .2f) * 9f) : 0f;
            cloth.T.localRotation = Quaternion.Euler(sway, 0f, 0f);
        }

        static float Out01(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u); }
    }
}
