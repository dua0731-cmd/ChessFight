using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The first values of the course obstacles' particle layers (R341, 11-04), shared by the builder (HazardVfxBuilder,
    /// before a rebuilt prefab takes the tuned values back) and by the game when the menu has not made the prefab yet
    /// (PawnRush/FallingPieceFx). I1, as a falling knight lands on the hall's cream stone stair (warm white and orange
    /// like its warning, stone like the stair, on the HDR ladder of Docs/VFX §6):
    ///   Flash   one soft hot flash at its base, warm white to orange, 0.14 s (x8: the ladder's flash)
    ///   Dust    a ring of stone dust puffs swept out along the floor about 2 m, swelling and thinning over a second
    ///   Chips   stone chips knocked up to about a metre, spinning, falling back and sliding on the floor
    ///   Sparks  a few quick gold streaks out and up, 0.25 .. 0.45 s
    /// and I3, after it:
    ///   Embers  orange embers rising slowly out of the cracks over the next two seconds
    ///   Vanish  a soft ivory puff up its body as it goes (the obstacle simply turns it off), so it does not pop out
    /// and V1, the royal fans' wind (R351), in pale air blue:
    ///   Streaks  long thin streaks of air wound round the fan's axis, straightening as they run out across the way
    ///   Motes    dust motes riding the flow, slower, wound tighter and spread wider
    /// </summary>
    public static class VfxHazardLooks
    {
        /// <summary>The I1 layers by name, in the order they are made.</summary>
        public static readonly string[] LandLayers = { "I1 Flash", "I1 Dust", "I1 Chips", "I1 Sparks" };
        /// <summary>The I3 particle layers by name (beside its cracks), in the order they are made.</summary>
        public static readonly string[] AfterLayers = { "I3 Embers", "I3 Vanish" };
        /// <summary>The V1 flow layers by name (beside its swirl), in the order they are made.</summary>
        public static readonly string[] WindLayers = { "V1 Streaks", "V1 Motes" };

        /// <summary>The first values of the flow layer named <paramref name="layer"/>.</summary>
        public static void Apply(string layer, VfxWindFlow f)
        {
            if (layer == "V1 Streaks")
            {
                f.count = 26;
                f.seed = 21;
                f.segments = 8;
                f.laneRadius = new Vector2(.2f, .85f);
                f.speedScale = new Vector2(.8f, 1.2f);
                f.lengthScale = new Vector2(.75f, 1.35f);
                f.widthScale = new Vector2(.7f, 1.4f);
                f.lightScale = new Vector2(.5f, 1f);
                f.speed = 9f;
                f.gap = .15f;
                f.length = 1.6f;
                f.width = .05f;
                f.twist = 1.6f;
                f.twistFade = 2.2f;
                f.spread = .1f;
                f.wobble = .06f;
                f.color = new Color(.86f, .93f, 1f);
                f.brightness = 1.5f;
                f.alpha = .35f;
                f.additive = .5f;
                f.round = false;
            }
            else if (layer == "V1 Motes")
            {
                f.count = 30;
                f.seed = 23;
                f.segments = 1;
                f.laneRadius = new Vector2(.1f, .95f);
                f.speedScale = new Vector2(.7f, 1.3f);
                f.lengthScale = new Vector2(1f, 1f);
                f.widthScale = new Vector2(.6f, 1.3f);
                f.lightScale = new Vector2(.6f, 1f);
                f.speed = 6f;
                f.gap = .3f;
                f.length = .05f;
                f.width = .055f;
                f.twist = 2.2f;
                f.twistFade = 1.5f;
                f.spread = .25f;
                f.wobble = .15f;
                f.color = new Color(1f, .95f, .85f);
                f.brightness = 1.2f;
                f.alpha = .6f;
                f.additive = .3f;
                f.round = true;
            }
        }

        /// <summary>The first values of the particle layer named <paramref name="layer"/>.</summary>
        public static void Apply(string layer, VfxParticleBurst b)
        {
            switch (layer)
            {
                case "I1 Flash": LandFlash(b); break;
                case "I1 Dust": LandDust(b); break;
                case "I1 Chips": LandChips(b); break;
                case "I1 Sparks": LandSparks(b); break;
                case "I3 Embers": AfterEmbers(b); break;
                case "I3 Vanish": AfterVanish(b); break;
            }
        }

        static void AfterEmbers(VfxParticleBurst b)
        {
            b.moment = VfxParticleBurst.Moment.Landing;
            b.count = 22;
            b.seed = 13;
            b.ringRadius = new Vector2(.3f, 2.2f);
            b.startHeight = new Vector2(.02f, .08f);
            b.outSpeed = new Vector2(.05f, .3f);
            b.upSpeed = new Vector2(.25f, .7f);
            b.life = new Vector2(.9f, 1.8f);
            b.sizeScale = new Vector2(.6f, 1.3f);
            b.delay = .5f;
            b.spin = 0f;
            // A little lift: they drift up.
            b.gravity = -.25f;
            b.drag = 1.2f;
            b.floor = true;
            b.colorStart = new Color(1f, .6f, .25f);
            b.colorEnd = new Color(1f, .25f, .04f);
            b.brightness = 3f;
            b.alphaStart = 1f;
            b.alphaEnd = 0f;
            b.fadeIn = .1f;
            b.sizeStart = .045f;
            b.sizeEnd = .02f;
            b.stretch = 0f;
            b.soft = .9f;
            b.hard = 0f;
            b.additive = 1f;
            b.depthFade = 0f;
        }

        static void AfterVanish(VfxParticleBurst b)
        {
            b.moment = VfxParticleBurst.Moment.Gone;
            b.count = 18;
            b.seed = 17;
            b.ringRadius = new Vector2(.15f, .7f);
            b.startHeight = new Vector2(.2f, 2.2f);
            b.outSpeed = new Vector2(.3f, 1.2f);
            b.upSpeed = new Vector2(.3f, 1f);
            b.life = new Vector2(.45f, .8f);
            b.sizeScale = new Vector2(.8f, 1.3f);
            b.delay = .05f;
            b.spin = 30f;
            b.gravity = -.6f;
            b.drag = 2f;
            b.floor = true;
            b.colorStart = new Color(.95f, .9f, .8f);
            b.colorEnd = new Color(.9f, .86f, .78f);
            b.brightness = 1f;
            b.alphaStart = .6f;
            b.alphaEnd = 0f;
            b.fadeIn = .1f;
            b.sizeStart = .5f;
            b.sizeEnd = 1.2f;
            b.stretch = 0f;
            b.soft = 1f;
            b.hard = 0f;
            b.additive = 0f;
            b.depthFade = .2f;
        }

        static void LandFlash(VfxParticleBurst b)
        {
            b.count = 1;
            b.seed = 3;
            b.ringRadius = Vector2.zero;
            b.startHeight = new Vector2(.5f, .5f);
            b.outSpeed = b.upSpeed = Vector2.zero;
            b.life = new Vector2(.14f, .14f);
            b.sizeScale = Vector2.one;
            b.delay = b.spin = 0f;
            b.gravity = 0f;
            b.drag = 0f;
            b.floor = false;
            b.colorStart = new Color(1f, .84f, .62f);
            b.colorEnd = new Color(1f, .42f, .1f);
            b.brightness = 8f;
            b.alphaStart = 1f;
            b.alphaEnd = 0f;
            b.fadeIn = .02f;
            b.sizeStart = 2.8f;
            b.sizeEnd = 1.8f;
            b.stretch = 0f;
            b.soft = 1f;
            b.hard = 0f;
            b.additive = 1f;
            b.depthFade = .3f;
        }

        static void LandDust(VfxParticleBurst b)
        {
            b.count = 26;
            b.seed = 5;
            b.ringRadius = new Vector2(.85f, 1f);
            b.startHeight = new Vector2(.05f, .25f);
            b.outSpeed = new Vector2(3.5f, 6.5f);
            b.upSpeed = new Vector2(.2f, 1f);
            b.life = new Vector2(.8f, 1.3f);
            b.sizeScale = new Vector2(.8f, 1.3f);
            b.delay = .04f;
            b.spin = 40f;
            b.gravity = .6f;
            b.drag = 3.2f;
            b.floor = true;
            b.colorStart = new Color(.82f, .76f, .66f);
            b.colorEnd = new Color(.7f, .66f, .6f);
            b.brightness = 1f;
            b.alphaStart = .55f;
            b.alphaEnd = 0f;
            b.fadeIn = .08f;
            b.sizeStart = .5f;
            b.sizeEnd = 1.7f;
            b.stretch = 0f;
            b.soft = 1f;
            b.hard = 0f;
            b.additive = 0f;
            b.depthFade = .25f;
        }

        static void LandChips(VfxParticleBurst b)
        {
            b.count = 18;
            b.seed = 7;
            b.ringRadius = new Vector2(.5f, .9f);
            b.startHeight = new Vector2(.1f, .35f);
            b.outSpeed = new Vector2(2f, 4.5f);
            b.upSpeed = new Vector2(3f, 6.5f);
            b.life = new Vector2(.6f, .95f);
            b.sizeScale = new Vector2(.6f, 1.4f);
            b.delay = 0f;
            b.spin = 720f;
            b.gravity = 20f;
            b.drag = .3f;
            b.floor = true;
            b.colorStart = b.colorEnd = new Color(.5f, .46f, .4f);
            b.brightness = 1f;
            b.alphaStart = 1f;
            b.alphaEnd = 0f;
            b.fadeIn = .01f;
            b.sizeStart = .09f;
            b.sizeEnd = .07f;
            b.stretch = 0f;
            b.soft = .25f;
            b.hard = 1f;
            b.additive = 0f;
            b.depthFade = 0f;
        }

        static void LandSparks(VfxParticleBurst b)
        {
            b.count = 16;
            b.seed = 9;
            b.ringRadius = new Vector2(.4f, .8f);
            b.startHeight = new Vector2(.1f, .3f);
            b.outSpeed = new Vector2(4f, 8f);
            b.upSpeed = new Vector2(1.5f, 4f);
            b.life = new Vector2(.25f, .45f);
            b.sizeScale = new Vector2(.7f, 1.2f);
            b.delay = 0f;
            b.spin = 0f;
            b.gravity = 9f;
            b.drag = 2f;
            b.floor = true;
            b.colorStart = new Color(1f, .75f, .4f);
            b.colorEnd = new Color(1f, .35f, .08f);
            b.brightness = 4f;
            b.alphaStart = 1f;
            b.alphaEnd = 0f;
            b.fadeIn = .01f;
            b.sizeStart = .05f;
            b.sizeEnd = .03f;
            b.stretch = .045f;
            b.soft = .8f;
            b.hard = 0f;
            b.additive = 1f;
            b.depthFade = 0f;
        }
    }
}
