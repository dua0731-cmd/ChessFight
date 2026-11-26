using UnityEngine;

namespace ChessFight.PawnRush
{
    // R834 (승규 11-25, the tutorial's moat wall: "여기에 폭포수를 넣고 그래픽 좋은 물이 나왔으면 좋겠어", then "폭포수 입체감을
    // 더 키워"): the first-run tutorial's moat (ArtSource/Tutorial, RagdollLabBuilder.TutorialDress) filled with the Royal
    // Carriage Yard's worked-out water, reused as it is:
    //  - the surface: YardWater's moving grid in PR_YardWater (Gerstner swell, Beer-Lambert colour, Fresnel sky, white foam
    //    wherever it meets a wall or a pier), one sheet over the whole moat at surfaceY;
    //  - the falls, two tiers each, in arches of the courtyard rim's moat wall (the stonework is tu_map_v1.fall_works, at
    //    the same numbers as here): a narrow, thick stream out of a stone spout drops into a basin on corbels, its own
    //    small surface churned where the stream lands, and the basin spills over its lower front sill as a wide sheet
    //    with round jets breaking out of it, down to the moat. Both are YardWaterfall nappes
    //    (P = lip + n u tau + g tau^2 / 2); every landing line churns the surface it lands on (16 lines: 8 falls);
    //  - a pawn that falls in splashes (GardenSplashPool's spray, YardWaterSplash's rings) on its way down to the kill
    //    box under it (top -8 m): the surface sits 1 m above that, so nobody stands on water.
    // A picture only: no collider but the splash trigger, nothing sent, nothing changes the lessons. Built at Start.
    public sealed partial class TutorialMoatWater : MonoBehaviour
    {
        [Tooltip("The water's surface (m, world y). The kill box's top is -8")]
        [SerializeField] float surfaceY = -7f;
        [Tooltip("The moat's floor (m, world y): the depth the colour and the swell are worked out for")]
        [SerializeField] float bedY = -14f;
        [Tooltip("The courtyard rim's inner edge, the moat's outer wall: x min, z min, x max, z max (m)")]
        [SerializeField] Vector4 moat = new Vector4(-52f, -47f, 47f, 42f);
        [Tooltip("Pilasters stand every side length / (floor(side / 6) + 1): a fall pours in the arch of the bay nearest "
                 + "each fraction along each side (south, east, north, west). Must match tu_map_v1.FALL_AT")]
        [SerializeField] float[] fallAt = { .3f, .7f };
        [Header("Tier 1: the spout into the basin (tu_map_v1.fall_works)")]
        [SerializeField] float spoutLipOut = .97f, spoutLipY = -1.80f, spoutHalfWidth = .42f, spoutSpeed = .9f;
        [Header("Tier 2: the basin's sill down to the moat")]
        [SerializeField] float basinY = -4.33f, basinHalfWidth = 1.3f, basinBack = -.1f, basinFront = 1.77f;
        [SerializeField] float sillLipOut = 1.98f, sillLipY = -4.37f, sillSpeed = 1.3f;
        [SerializeField] int sillJets = 4;
        [SerializeField] float waveHeight = .05f, waveLength = 3.2f;
        [SerializeField] Vector3 flow = new Vector3(.12f, 0f, .05f);
        [Header("Waves only from the falls (11-26)")]
        [Tooltip("Still water everywhere (no swell, no ripple maps): the only waves are the rings out of each fall's landing")]
        [SerializeField] bool fallWavesOnly = true;
        [Tooltip("Then a faint fine ripple everywhere (the yard's small-ripple layer; the yard's own strength is 0.22)")]
        [SerializeField, Range(0, .5f)] float fineRipples = .06f;
        [Tooltip("Fine ripples per metre (the yard's 0.9)")]
        [SerializeField, Range(.5f, 6)] float fineRippleScale = 2.2f;
        [Tooltip("How strong the falls' rings are (the yard's 1)")]
        [SerializeField, Range(0, 6)] float ringStrength = 1f;
        [Tooltip("How far they carry before fading (x the yard's: e-folding 2.2 m and 5.5 m for the short and the long rings)")]
        [SerializeField, Range(.5f, 8)] float ringReach = 1.5f;
        [Tooltip("The rings are gone past this distance from a fall's landing (m), faded out from half of it")]
        [SerializeField, Range(1, 30)] float ringRadius = 5f;
        [Header("Laps against the island's walls (11-26)")]
        [Tooltip("The falls' waves reaching the courtyard's inner walls (the island's moat walls), reflecting and running up "
                 + "them - only on the faces turned to a fall (PR_YardWater.LapHeight)")]
        [SerializeField] bool wallLaps = true;
        [Tooltip("Their height (m per unit of a fall's wave at the wall: about 0.2 - 0.5 for a face near a fall)")]
        [SerializeField, Range(0, .3f)] float lapHeight = .06f;
        [Tooltip("How far a fall's waves carry across the moat to a wall (m, e-folding)")]
        [SerializeField, Range(2, 80)] float lapFallReach = 25f;
        [Tooltip("Their wavelength (m)")]
        [SerializeField, Range(.8f, 6)] float lapLength = 2.4f;
        [Tooltip("How far out from the wall they are seen, fading (m, e-folding)")]
        [SerializeField, Range(.5f, 8)] float lapReach = 2.5f;
        [Tooltip("The white water on each crest at the wall")]
        [SerializeField, Range(0, 1.5f)] float lapFoam = .8f;
        [Tooltip("How high the water runs up the wall as each crest arrives (m per unit of a fall's wave there)")]
        [SerializeField, Range(0, 1)] float lapSurge = .3f;
        [Tooltip("How far out from the wall that rise reaches (m, e-folding)")]
        [SerializeField, Range(.2f, 3)] float lapSurgeReach = .8f;
        [Header("Breeze patches on the open water (11-26)")]
        [Tooltip("Gusts roughening the still water in patches that drift with the wind (PR_YardWater _Paw*)")]
        [SerializeField] bool breeze = true;
        [Tooltip("The ripples' strength inside a patch")]
        [SerializeField, Range(0, 2)] float breezeStrength = .35f;
        [Tooltip("Share of the water under a patch at any time")]
        [SerializeField, Range(0, 1)] float breezeCover = .45f;
        [Tooltip("Patch size (m)")]
        [SerializeField, Range(2, 60)] float breezeSize = 14f;
        [Tooltip("The wind carrying them (world x, z, m/s)")]
        [SerializeField] Vector2 breezeWind = new Vector2(.5f, .2f);
        [Tooltip("Ripples per metre inside a patch")]
        [SerializeField, Range(.3f, 4)] float breezeRipples = 1.3f;
        [SerializeField, Range(0, 2)] int maxTier = 2;
        [SerializeField] bool splashes = true;
        [Tooltip("Kept for older scenes (the gold sill under the earlier single falls): no longer drawn")]
        [SerializeField] Material sill;

        public void Configure(Material sillMaterial) => sill = sillMaterial;

        void Start()
        {
            var surface = Resources.Load<Material>("PR_YardWater");
            var sheet = Resources.Load<Material>("PR_GardenFallSheet");
            if (surface == null || sheet == null)
            {
                Debug.LogWarning("[튜토리얼 물] Resources의 PR_YardWater / PR_GardenFallSheet 재질이 없어 해자를 비워 둡니다");
                return;
            }
            // 11-26 (승규: "이중무늬 말고 한 무늬만" → "웨이브 넣지 말고 폭포수 구간만 좀 상호작용 식으로 웨이브를 넣어주고
            // 거기서부터 웨이브가 시작 돼서 멀리 갈 수록 약하게"): the yard's swell and ripple maps crossed into a double
            // pattern, so the water is still and the waves come only from the falls - PR_YardWater's rings out of every
            // landing line (h ~ e^(-r/R) sin(k r - omega t)), stronger and carried further than in the yard.
            float swell = waveHeight;
            if (fallWavesOnly)
            {
                surface = new Material(surface) { name = "Tutorial moat water" };
                surface.SetFloat("_WaveStrength", 0f);
                // then "미세하게 잔물결 섞어줘": the yard's one finest ripple layer back everywhere, faint and finer than the
                // breeze patches' ripples, so it reads as a texture rather than a second pattern
                surface.SetFloat("_DetailStrength", fineRipples);
                surface.SetFloat("_DetailScale", fineRippleScale);
                surface.SetFloat("_RingStrength", ringStrength);
                surface.SetFloat("_RingReach", ringReach);
                surface.SetFloat("_RingRadius", ringRadius);
                swell = 0f;
            }
            // Then "물이 살짝살짝 씩 안쪽 벽을 치게": waves lapping at the island's walls (PR_YardWater.LapWave).
            if (wallLaps)
            {
                if (!fallWavesOnly) surface = new Material(surface) { name = "Tutorial moat water" };
                var map = LapMap(out var rect);
                if (map != null)
                {
                    surface.SetTexture("_LapMap", map);
                    surface.SetVector("_LapRect", rect);
                    surface.SetFloat("_LapOn", 1f);
                    surface.SetFloat("_LapHeight", lapHeight);
                    surface.SetFloat("_LapLength", lapLength);
                    surface.SetFloat("_LapReach", lapReach);
                    surface.SetFloat("_LapFoam", lapFoam);
                    surface.SetFloat("_LapSurge", lapSurge);
                    surface.SetFloat("_LapSurgeReach", lapSurgeReach);
                    surface.SetFloat("_LapFallReach", lapFallReach);
                }
            }
            // Then "여기 밋밋한 부분만 좀 살려줘" (the open water between falls and walls): breeze patches.
            if (breeze)
            {
                if (!fallWavesOnly && !wallLaps) surface = new Material(surface) { name = "Tutorial moat water" };
                surface.SetFloat("_PawStrength", breezeStrength);
                surface.SetFloat("_PawCover", breezeCover);
                surface.SetFloat("_PawSize", breezeSize);
                surface.SetVector("_PawWind", new Vector4(breezeWind.x, breezeWind.y, 0f, 0f));
                surface.SetFloat("_PawRippleScale", breezeRipples);
            }
            var go = new GameObject("Water look");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var water = go.AddComponent<YardWater>();
            water.Configure(maxTier);

            // YardWater replaces a water box: an undrawn one the size of the moat, its top at the surface.
            float depth = Mathf.Max(.5f, surfaceY - bedY);
            water.AddSurface("moat", HiddenBox("Moat water box", new Vector3((moat.x + moat.z) * .5f, surfaceY - depth * .5f, (moat.y + moat.w) * .5f),
                                               new Vector3(moat.z - moat.x, depth, moat.w - moat.y), Quaternion.identity),
                             surface, flow, swell, waveLength, depth, .32f, null);

            int falls = 0;
            var corners = new[] { new Vector3(moat.x, 0f, moat.y), new Vector3(moat.z, 0f, moat.y), new Vector3(moat.z, 0f, moat.w), new Vector3(moat.x, 0f, moat.w) };
            var centre = new Vector3((moat.x + moat.z) * .5f, 0f, (moat.y + moat.w) * .5f);
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side];
                var b = corners[(side + 1) % 4];
                float length = Vector3.Distance(a, b);
                var along = (b - a) / length;
                var inward = new Vector3(-along.z, 0f, along.x);
                if (Vector3.Dot(inward, centre - a) < 0f) inward = -inward;
                // The Blender rim (tu_map_v1.arcade_wall): k = floor(L / 6) pilasters, one every L / (k + 1).
                int k = Mathf.FloorToInt(length / 6f);
                float bay = length / (k + 1);
                foreach (float f in fallAt)
                {
                    if (falls * 2 + 2 > YardWater.MaxLines) break;
                    int j = Mathf.Clamp(Mathf.RoundToInt(f * (k + 1) - .5f), 0, k);
                    var mid = a + along * (bay * (j + .5f));
                    var turn = Quaternion.LookRotation(inward, Vector3.up);

                    // the basin's water: its own small surface, running to the sill
                    float bd = basinFront - basinBack;
                    var basinCentre = mid + inward * ((basinBack + basinFront) * .5f) + Vector3.up * (basinY - .2f);
                    water.AddSurface("basin " + falls, HiddenBox("Basin water box " + falls, basinCentre, new Vector3(basinHalfWidth * 2f, .4f, bd), turn),
                                     surface, inward * .45f, fallWavesOnly ? 0f : .012f, .9f, .4f, .1f, null);

                    var fall = YardWaterfall.Create(go.transform, "Water falling (moat wall " + falls + ")", water, sheet, surfaceY);
                    var spout = mid + inward * spoutLipOut + Vector3.up * spoutLipY;
                    fall.AddSheet(spout - along * spoutHalfWidth, spout + along * spoutHalfWidth, inward, spoutSpeed, basinY, true, 1,
                                  .13f + .29f * falls, spoutLipOut + .46f);
                    var lip = mid + inward * sillLipOut + Vector3.up * sillLipY;
                    fall.AddSheet(lip - along * basinHalfWidth, lip + along * basinHalfWidth, inward, sillSpeed, surfaceY, true, sillJets,
                                  .21f + .31f * falls, .45f);
                    fall.Finish();
                    AddPour(mid + inward * (sillLipOut + .5f) + Vector3.up * ((sillLipY + surfaceY) * .5f));   // its sound (TutorialMoatWater.Sound)
                    falls++;
                }
            }

            if (splashes)
            {
                const float under = .6f, over = .05f;
                var pool = new CourseBuilder(null, go.transform).Trigger<GardenSplashPool>("Splash (moat)",
                    new Vector3(moat.x, surfaceY - under, moat.y), new Vector3(moat.z, surfaceY + over, moat.w));
                pool.Configure((under - over) * .5f);
                pool.gameObject.AddComponent<YardWaterSplash>().Configure(water, (under - over) * .5f);
                water.Own(pool.gameObject);
            }
            go.SetActive(true);
            AddLap();
            Debug.Log("[튜토리얼 물] 해자 물 표면 1개(높이 " + surfaceY + " m), 2단 폭포 " + falls + "곳(물받이 수면 " + falls + "개)"
                      + (splashes ? ", 빠지면 물보라" : ""));
        }

        // The island's footprint, seen from above, and the distance from every point of the moat to its nearest wall:
        //  - a grid of LapCell over the moat; a cell is island where a ray down from above meets a collider (not a trigger)
        //    whose top is no lower than LapTopMin (the floors are at 0; the falls' basins and the kill box are far lower),
        //    leaving out a band along the rim so only the inner walls lap;
        //  - opened by LapOpen cells (eroded, then grown back) so a plank or a beam over the water makes no wall;
        //  - the distance by two chamfer passes (3-4 weights: within 8 % of Euclidean), in metres, and the way away
        //    from the wall by central differences.
        // RGBAHalf, bilinear: r signed distance (m, - under the island), g b the way away from the wall (world x, z).
        const float LapCell = .5f, LapTopMin = -1.5f, LapRimBand = 1.5f, LapMaxDistance = 15f;
        const int LapOpen = 2;

        Texture2D LapMap(out Vector4 rect)
        {
            int nx = Mathf.CeilToInt((moat.z - moat.x) / LapCell), nz = Mathf.CeilToInt((moat.w - moat.y) / LapCell);
            rect = new Vector4(moat.x, moat.y, 1f / (nx * LapCell), 1f / (nz * LapCell));
            Physics.SyncTransforms();
            var solid = new bool[nx * nz];
            int count = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = moat.x + (i + .5f) * LapCell, z = moat.y + (j + .5f) * LapCell;
                    if (x - moat.x < LapRimBand || moat.z - x < LapRimBand || z - moat.y < LapRimBand || moat.w - z < LapRimBand) continue;
                    var from = new Vector3(x, 60f, z);
                    if (Physics.Raycast(from, Vector3.down, 60f - LapTopMin, ~0, QueryTriggerInteraction.Ignore))
                    {
                        solid[j * nx + i] = true;
                        count++;
                    }
                }
            if (count == 0)
            {
                Debug.LogWarning("[튜토리얼 물] 해자 안쪽 땅을 찾지 못해 벽 물결을 넣지 않습니다");
                return null;
            }
            solid = Grow(Grow(solid, nx, nz, LapOpen, false), nx, nz, LapOpen, true);

            // Signed distance to the wall (the cell edge between island and water), metres: + over the water, - under the
            // island, so its gradient (the way away from the wall) holds on both sides of the wall's line.
            var toIsland = Chamfer(solid, nx, nz, true);
            var toWater = Chamfer(solid, nx, nz, false);
            float Metres(int i, int j)
            {
                int n = Mathf.Clamp(j, 0, nz - 1) * nx + Mathf.Clamp(i, 0, nx - 1);
                float d = solid[n] ? -(toWater[n] / 3f - .5f) : toIsland[n] / 3f - .5f;
                return Mathf.Clamp(d * LapCell, -LapMaxDistance, LapMaxDistance);
            }

            var pixels = new Color[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float d = Metres(i, j);
                    float gx = Metres(i + 1, j) - Metres(i - 1, j), gz = Metres(i, j + 1) - Metres(i, j - 1);
                    pixels[j * nx + i] = new Color(d, gx, gz, 0f);
                }
            var tex = new Texture2D(nx, nz, TextureFormat.RGBAHalf, false, true)
            {
                name = "Tutorial moat lap map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            Debug.Log("[튜토리얼 물] 벽 물결: 안쪽 땅 " + count + "칸(" + LapCell + " m 격자 " + nx + "×" + nz + ")");
            return tex;
        }

        // Two-pass chamfer distance (3-4 weights, in cells x 3) from every cell to the nearest cell whose solid == target
        // (0 on those cells).
        static int[] Chamfer(bool[] solid, int nx, int nz, bool target)
        {
            const int Far = 1 << 20;
            var dist = new int[nx * nz];
            for (int n = 0; n < dist.Length; n++) dist[n] = solid[n] == target ? 0 : Far;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int n = j * nx + i, v = dist[n];
                    if (i > 0) v = Mathf.Min(v, dist[n - 1] + 3);
                    if (j > 0)
                    {
                        v = Mathf.Min(v, dist[n - nx] + 3);
                        if (i > 0) v = Mathf.Min(v, dist[n - nx - 1] + 4);
                        if (i < nx - 1) v = Mathf.Min(v, dist[n - nx + 1] + 4);
                    }
                    dist[n] = v;
                }
            for (int j = nz - 1; j >= 0; j--)
                for (int i = nx - 1; i >= 0; i--)
                {
                    int n = j * nx + i, v = dist[n];
                    if (i < nx - 1) v = Mathf.Min(v, dist[n + 1] + 3);
                    if (j < nz - 1)
                    {
                        v = Mathf.Min(v, dist[n + nx] + 3);
                        if (i < nx - 1) v = Mathf.Min(v, dist[n + nx + 1] + 4);
                        if (i > 0) v = Mathf.Min(v, dist[n + nx - 1] + 4);
                    }
                    dist[n] = v;
                }
            return dist;
        }

        // Grows the island (grow) or the water (!grow) by r cells: a cell takes the value if any cell within r has it.
        static bool[] Grow(bool[] src, int nx, int nz, int r, bool grow)
        {
            var dst = new bool[src.Length];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    bool v = !grow;
                    for (int dj = -r; dj <= r && v != grow; dj++)
                        for (int di = -r; di <= r; di++)
                        {
                            int a = i + di, b = j + dj;
                            if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
                            if (src[b * nx + a] == grow) { v = grow; break; }
                        }
                    dst[j * nx + i] = v;
                }
            return dst;
        }

        // YardWater takes the place of a water box: an undrawn cube, its top where the water is.
        Renderer HiddenBox(string name, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(transform, false);
            box.transform.SetPositionAndRotation(centre, rotation);
            box.transform.localScale = size;
            var view = box.GetComponent<Renderer>();
            view.enabled = false;
            return view;
        }
    }
}
