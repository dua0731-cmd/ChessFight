using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The turning part of a SlewCrane (R662): the crane's house on its drum, the jib with its counterweight, the slings
    // and the slung load - all of it turned about the drum's middle to the crane's heading, a pure function of
    // ObstacleClock (SlewCrane.HeadingAt). Only the load is solid: a bundle of timber along the jib from the crane's inner
    // to its outer radius, never climbed, its leading side knocking whoever it comes at (YardImpact: the side moving at
    // the pawn, along the sweep, at a speed that grows with the load's speed there). Riders are carried as on any
    // obstacle (IMovingSurface: a pawn that lands on the load's top rides it).
    public sealed class SlewJib : Obstacle
    {
        [SerializeField] SlewCrane crane;
        bool built;

        public void Configure(SlewCrane crane) => this.crane = crane;

        protected override void Awake()
        {
            if (crane == null) crane = GetComponentInParent<SlewCrane>();
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built || crane == null) return;
            built = true;
            float r0 = crane.InnerRadius, r1 = crane.OuterRadius, w = crane.LoadSection.x, h = crane.LoadSection.y;
            float bottom = crane.LoadBottom, jib = crane.JibHeight;
            float top = crane.DrumHeight;

            // The crane's house on the drum, the treadwheel in it, the jib out over the load, the counter-jib and its weight.
            YardKit.Box(transform, "Crane house", new Vector3(-.2f, top + .9f, 0f), new Vector3(2f, 1.8f, 1.8f), YardKit.WoodDark, Solid.None);
            YardKit.Box(transform, "House roof", new Vector3(-.2f, top + 1.9f, 0f), new Vector3(2.3f, .2f, 2.1f), YardKit.Roof, Solid.None);
            YardKit.Ring(transform, "Treadwheel", new Vector3(-.2f, top + 1.05f, 1.0f), Vector3.forward, .85f, .12f, .1f, 16, YardKit.WoodLight, Solid.None);
            YardKit.Box(transform, "Jib", new Vector3((r1 + .4f - 2.6f) * .5f, jib, 0f), new Vector3(r1 + .4f + 2.6f, .4f, .4f), YardKit.WoodDark, Solid.None);
            YardKit.Box(transform, "Jib trim", new Vector3((r1 + .4f - 2.6f) * .5f, jib + .22f, 0f), new Vector3(r1 + .4f + 2.6f, .05f, .42f), YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Counterweight", new Vector3(-2.4f, jib - .6f, 0f), new Vector3(1f, 1f, 1f), YardKit.Iron, Solid.None);
            // A brace from the house up to the jib, at 45 degrees.
            float braceRun = Mathf.Min(r1 * .45f, jib - top - 2f);
            var a = new Vector3(.8f, top + 1.9f, 0f);
            var b = new Vector3(.8f + braceRun, top + 1.9f + braceRun, 0f);
            var mid = (a + b) * .5f;
            YardKit.Box(transform, "Jib brace", mid, new Vector3(.22f, (b - a).magnitude, .22f), YardKit.WoodDark, Solid.None,
                        Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
            // The lamp at the jib's end: the crane colours it (green resting, red lifting and slewing).
            var lampMaterial = new Material(Arena.Glow(new Color(.35f, 1f, .45f, .9f))) { name = "M_YardSlewLamp" };
            var lamp = YardKit.Box(transform, "Jib lamp", new Vector3(r1 + .35f, jib - .35f, 0f), new Vector3(.36f, .36f, .36f), lampMaterial, Solid.None);
            lamp.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            crane.SetLamp(lamp.transform, lampMaterial);

            // Two slings from the jib down to the load.
            float loadTop = bottom + h;
            foreach (float r in new[] { r0 + (r1 - r0) * .2f, r0 + (r1 - r0) * .8f })
                YardKit.Box(transform, "Sling", new Vector3(r, (jib + loadTop) * .5f, 0f), new Vector3(.06f, jib - loadTop, .06f), YardKit.WoodDark, Solid.None);

            // The load: squared timber bundled two under one (a ridge on top), iron bands round it, its ends darker
            // (pictures). Solid: a lower box under the two bottom beams, and over it a roof whose faces slope 50 degrees
            // up to the ridge - steeper than anyone stands on (Pawn Rush's step and ledge help take nothing under 0.7 of
            // upright), so a pawn landing on it slides off rather than riding it across the field.
            float length = r1 - r0, centre = (r0 + r1) * .5f, lower = Mathf.Min(.17f, h * .45f);
            var load = new GameObject("Timber load");
            load.transform.SetParent(transform, false);
            load.transform.localPosition = new Vector3(centre, bottom, 0f);
            foreach (float side in new[] { -1f, 1f })
                YardKit.Box(load.transform, "Timber", new Vector3(0f, lower * .5f, side * w * .25f), new Vector3(length, lower - .01f, w * .5f - .01f),
                            side < 0f ? YardKit.WoodLight : YardKit.WoodDark, Solid.None);
            YardKit.Box(load.transform, "Timber", new Vector3(0f, lower + (h - lower) * .5f, 0f), new Vector3(length - .3f, h - lower, w * .5f), YardKit.WoodLight, Solid.None);
            for (float x = -length * .5f + .5f; x < length * .5f - .3f; x += 1.5f)
                YardKit.Box(load.transform, "Timber band", new Vector3(x, lower * .5f, 0f), new Vector3(.08f, lower + .03f, w + .03f), YardKit.Iron, Solid.None);
            foreach (float end in new[] { -1f, 1f })
                YardKit.Box(load.transform, "Timber end", new Vector3(end * (length * .5f + .01f), lower * .5f, 0f), new Vector3(.03f, lower - .04f, w - .04f), YardKit.WoodDark, Solid.None);

            var body = new GameObject("Timber load (collider)");
            body.transform.SetParent(load.transform, false);
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, lower * .5f, 0f);
            box.size = new Vector3(length, lower, w);
            body.AddComponent<NoClimbSurface>();
            var ridge = new GameObject("Timber ridge (collider)");
            ridge.transform.SetParent(load.transform, false);
            var roof = ridge.AddComponent<MeshCollider>();
            roof.sharedMesh = Ridge(length, lower, h, w);
            roof.convex = true;
            ridge.AddComponent<NoClimbSurface>();

            var impact = YardKit.Shell(load.transform, "Timber load (hit)", new Vector3(0f, h * .5f, 0f), new Vector3(length + .1f, h + .1f, w + .3f), crane.KnockbackForce);
            // Judged by the lower box (a pawn standing has its hips in it; one over the load is above it and not hit),
            // and the roof let through with it (alsoSolid) whenever it knocks someone down.
            impact.solid = box;
            impact.alsoSolid = new Collider[] { roof };
            // A low load's underside never faces anyone (onFloor); a load hung higher judges the part of the pawn it
            // touches, so whoever it passes over is not hit by its underside's edge.
            impact.onFloor = bottom < .3f;
            impact.judgePart = bottom >= .3f;
            impact.speedScale = crane.SpeedScale;
            impact.maxPush = crane.MaxPush;
            impact.alongShare = crane.AlongShare;
            impact.lift = 1.6f;
            impact.replaceSpeed = true;
            impact.minSpeed = .4f;
            impact.knockdownSpeed = crane.KnockdownSpeed;
            // A knockdown tips the pawn over the load (a push under the load's speed) and lets it through until the load
            // has passed on, so it is not driven ahead of the sweep and hit again; a stagger near the drum still sends
            // it on ahead of the slow load.
            impact.downPush = crane.DownPush;
            impact.knockdownSeconds = 1f;
            impact.cooldown = .7f;
            // Long enough to cover a load that comes to rest over whoever it has just knocked down (the last second of a
            // slew, a 1.4 s rest, the slow start back): let through until the load has gone on, never pressed under it.
            impact.ghostSeconds = 4f;
            impact.course = crane.Course;
            impact.forwardShare = .6f;
        }

        static Mesh ridgeMesh;
        static Vector4 ridgeKey;

        // A triangular prism along x: a base `width` wide at `low`, the ridge at `high` over its middle (local to the load).
        static Mesh Ridge(float length, float low, float high, float width)
        {
            var key = new Vector4(length, low, high, width);
            if (ridgeMesh != null && ridgeKey == key) return ridgeMesh;
            float x = length * .5f, z = width * .5f;
            var v = new[]
            {
                new Vector3(-x, low, -z), new Vector3(-x, low, z), new Vector3(-x, high, 0f),
                new Vector3(x, low, -z), new Vector3(x, low, z), new Vector3(x, high, 0f),
            };
            var t = new[] { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4, 0, 1, 4, 0, 4, 3 };
            ridgeMesh = new Mesh { name = "TimberRidge", vertices = v, triangles = t };
            ridgeMesh.RecalculateNormals();
            ridgeMesh.RecalculateBounds();
            ridgeKey = key;
            return ridgeMesh;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            rotation = crane != null ? StartRotation * Quaternion.Euler(0f, crane.HeadingAt(time), 0f) : StartRotation;
        }
    }
}
