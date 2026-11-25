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
    public sealed class TutorialMoatWater : MonoBehaviour
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
            var go = new GameObject("Water look");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var water = go.AddComponent<YardWater>();
            water.Configure(maxTier);

            // YardWater replaces a water box: an undrawn one the size of the moat, its top at the surface.
            float depth = Mathf.Max(.5f, surfaceY - bedY);
            water.AddSurface("moat", HiddenBox("Moat water box", new Vector3((moat.x + moat.z) * .5f, surfaceY - depth * .5f, (moat.y + moat.w) * .5f),
                                               new Vector3(moat.z - moat.x, depth, moat.w - moat.y), Quaternion.identity),
                             surface, flow, waveHeight, waveLength, depth, .32f, null);

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
                                     surface, inward * .45f, .012f, .9f, .4f, .1f, null);

                    var fall = YardWaterfall.Create(go.transform, "Water falling (moat wall " + falls + ")", water, sheet, surfaceY);
                    var spout = mid + inward * spoutLipOut + Vector3.up * spoutLipY;
                    fall.AddSheet(spout - along * spoutHalfWidth, spout + along * spoutHalfWidth, inward, spoutSpeed, basinY, true, 1,
                                  .13f + .29f * falls, spoutLipOut + .46f);
                    var lip = mid + inward * sillLipOut + Vector3.up * sillLipY;
                    fall.AddSheet(lip - along * basinHalfWidth, lip + along * basinHalfWidth, inward, sillSpeed, surfaceY, true, sillJets,
                                  .21f + .31f * falls, .45f);
                    fall.Finish();
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
            Debug.Log("[튜토리얼 물] 해자 물 표면 1개(높이 " + surfaceY + " m), 2단 폭포 " + falls + "곳(물받이 수면 " + falls + "개)"
                      + (splashes ? ", 빠지면 물보라" : ""));
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
