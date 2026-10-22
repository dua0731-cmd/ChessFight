using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // The hall's two square piers, stone by stone (10-17, 승규 님: "좀더 폴리곤 수 많아 보이게 해줘 그래픽 더
    // 좋게"): every course is four real blocks laid as a pinwheel round a mortar core, turned the other way
    // on the next course so the joints alternate like the wall's. Each block has rounded arrises, a face
    // that is not quite flat, a little tilt and its own cream shade, with a fine stone grain and its relief
    // (normal map) on top, so the light catches every edge. The courses are the wall's height (.375 m).
    public sealed partial class IntroStage
    {
        const float PierWidth = 1.15f, Course = .375f, PierBase = .75f, PierTop = 6.4f;
        Material pierStone, pierCore, pierPlinth;
        readonly List<Mesh> pierMeshes = new List<Mesh>();

        void Pillars()
        {
            if (pierStone == null) pierStone = StoneBlocks.Grained("Intro pier stone", Color.white, .18f);
            pierPlinth = StoneBlocks.Grained("Intro pier plinth", MenuArt.Hex(0xD2BE98), .2f);
            pierCore = LastSceneArt.Lit(0x8A7658, .05f);
            var random = new System.Random(1710);
            foreach (float right in new[] { -4.5f, 4.5f })
                Pier(new Vector3(Sx(right), 0, -.6f), random);
        }

        void Pier(Vector3 at, System.Random random)
        {
            var root = new GameObject("Pier").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            float a = PierWidth / 2f, longSide = PierWidth * .6f, shortSide = PierWidth - longSide;
            const float gap = .014f;

            // The base: a plinth block, a moulded step and the brass band, all with rounded edges.
            Block(root, "Pier Plinth", new Vector3(0, .3f, 0), new Vector3(PierWidth + .3f, .6f, PierWidth + .3f), .045f, pierPlinth, Color.white, random, .004f);
            Block(root, "Pier Step", new Vector3(0, .65f, 0), new Vector3(PierWidth + .16f, .1f, PierWidth + .16f), .03f, pierPlinth, MenuArt.Hex(0xF2E6CC), random, .002f);
            MenuArt.Slab(root, "Pier Band", new Vector3(0, .725f, 0), new Vector3(PierWidth + .05f, .05f, PierWidth + .05f), MenuArt.BrassMetal());
            MenuArt.Slab(root, "Pier Core", new Vector3(0, (PierBase + PierTop) / 2f, 0), new Vector3(PierWidth - .03f, PierTop - PierBase, PierWidth - .03f), pierCore);

            var shades = StoneBlocks.Shades;
            int courses = Mathf.CeilToInt((PierTop - PierBase) / Course);
            for (int k = 0; k < courses; k++)
            {
                float y = PierBase + (k + .5f) * Course;
                float flip = k % 2 == 0 ? 1f : -1f;
                // A pinwheel of four blocks: (centre x, centre z, size x, size z) before the flip.
                var stones = new[]
                {
                    new Vector4(-a + longSide / 2f, a - shortSide / 2f, longSide, shortSide),
                    new Vector4(a - shortSide / 2f, a - longSide / 2f, shortSide, longSide),
                    new Vector4(a - longSide / 2f, -a + shortSide / 2f, longSide, shortSide),
                    new Vector4(-a + shortSide / 2f, -a + longSide / 2f, shortSide, longSide),
                };
                foreach (var s in stones)
                {
                    float out_ = ((float)random.NextDouble() - .5f) * .014f;
                    var centre = new Vector3(s.x * flip, y + ((float)random.NextDouble() - .5f) * .006f, s.y);
                    // Pushed out or in a few millimetres along the faces it shows.
                    centre += new Vector3(Mathf.Sign(centre.x), 0, Mathf.Sign(centre.z)) * out_;
                    var size = new Vector3(s.z - gap, Course - gap, s.w - gap);
                    var go = Block(root, "Pier Stone", centre, size, .028f, pierStone, shades[random.Next(shades.Length)], random, .006f);
                    go.transform.localRotation = Quaternion.Euler(((float)random.NextDouble() - .5f) * .8f, ((float)random.NextDouble() - .5f) * 1.2f,
                                                                  ((float)random.NextDouble() - .5f) * .8f);
                }
            }
        }

        // One block: a rounded box whose faces are gently uneven, mapped with the grain texture by
        // its own size so the grain is not stretched, tinted through a property block.
        GameObject Block(Transform parent, string name, Vector3 centre, Vector3 size, float radius, Material material, Color tint,
                         System.Random random, float bump)
        {
            var mesh = StoneBlocks.Block(size, radius, 8, bump, (float)random.NextDouble() * 50f);
            pierMeshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (tint != Color.white)
            {
                var block = new MaterialPropertyBlock();
                LitMaterials.SetColor(block, tint);
                renderer.SetPropertyBlock(block);
            }
            return go;
        }

        // The hall wall in the same blocks as the piers (10-17, 승규 님: wall and pillars the same polygon
        // count): courses of .625 m x .375 m blocks, every other course offset by half a block, between
        // the plinth rail and the battlements and as wide as the camera sees, over a mortar-coloured slab.
        // The great window's opening (and its jambs) stays clear, half blocks closing up to it. Sixteen
        // block shapes are made once and reused, turned over at random.
        void WallBlocks(float wallZ)
        {
            const float bw = .625f, bh = .375f, depth = .14f, gap = .014f, bottom = .66f, half = 8.2f;
            if (pierStone == null) pierStone = StoneBlocks.Grained("Intro pier stone", Color.white, .18f);
            var random = new System.Random(1717);
            var shapes = new Mesh[16];
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = StoneBlocks.Block(new Vector3(bw - gap, bh - gap, depth), .026f, 7, .006f, (float)random.NextDouble() * 50f);
                pierMeshes.Add(shapes[i]);
            }
            var halfShapes = new Mesh[4];
            for (int i = 0; i < halfShapes.Length; i++)
            {
                halfShapes[i] = StoneBlocks.Block(new Vector3(bw / 2f - gap, bh - gap, depth), .026f, 6, .005f, (float)random.NextDouble() * 50f);
                pierMeshes.Add(halfShapes[i]);
            }
            var shades = StoneBlocks.Shades;
            var root = new GameObject("Wall Blocks").transform;
            root.SetParent(transform, false);
            var block = new MaterialPropertyBlock();
            for (int row = 0; row < 10; row++)
            {
                float y = bottom + (row + .5f) * bh;
                float offset = row % 2 == 0 ? 0f : bw / 2f;
                for (float x0 = -half - offset; x0 < half; x0 += bw)
                {
                    float x = x0 + bw / 2f;
                    bool high = y + bh / 2f > GreatBottom - .18f;
                    float clear = GreatHalfWidth + .3f;
                    bool cut = high && Mathf.Abs(x - GreatCentre) < clear + bw / 2f;
                    if (cut)
                    {
                        // Half blocks either side of a window opening where they fit.
                        foreach (float side in new[] { -bw / 4f, bw / 4f })
                        {
                            float hx = x + side;
                            bool hit = Mathf.Abs(hx - GreatCentre) < clear + bw / 4f;
                            if (!hit) Lay(root, halfShapes[random.Next(halfShapes.Length)], new Vector3(hx, y, wallZ + depth / 2f - .03f), random, shades, block);
                        }
                        continue;
                    }
                    Lay(root, shapes[random.Next(shapes.Length)], new Vector3(x, y, wallZ + depth / 2f - .03f), random, shades, block);
                }
            }
        }

        void Lay(Transform root, Mesh mesh, Vector3 at, System.Random random, Color[] shades, MaterialPropertyBlock block)
        {
            var go = new GameObject("Wall Block");
            go.transform.SetParent(root, false);
            go.transform.localPosition = at + new Vector3(0, 0, ((float)random.NextDouble() - .5f) * .012f);
            go.transform.localRotation = Quaternion.Euler(((float)random.NextDouble() - .5f) * .6f, ((float)random.NextDouble() - .5f) * .8f,
                                                          random.Next(2) * 180f + ((float)random.NextDouble() - .5f) * .5f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = pierStone;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            LitMaterials.SetColor(block, shades[random.Next(shades.Length)]);
            renderer.SetPropertyBlock(block);
        }

        void DisposePiers()
        {
            foreach (var m in pierMeshes) if (m != null) Destroy(m);
            if (pierStone != null) Destroy(pierStone);
            if (pierPlinth != null) Destroy(pierPlinth);
        }
    }
}
