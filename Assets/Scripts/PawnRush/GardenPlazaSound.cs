using UnityEngine;

namespace ChessFight.PawnRush
{
    // R530 (승규 11-09): the fountain plaza's water sounds (garden beat 1); the spin bars have their own
    // (GardenSpinBarSound).
    //  fountains  the six jets the concept dressing draws from the kerb's planters out into the moat
    //             (plaza_sample_v1.py "Plaza fountain jets": angles 0, +-44, 136, 180, 224 about the plaza's middle,
    //             2.4 m out from the kerb): a stream pattering into the pool where each lands, looped
    //  moat       the moat's water lapping at the plaza's stone rim: one loop that slides round the rim to the point
    //             nearest the listener, so the whole ring sounds without twenty sources
    // Both are NearFarSound loops, quiet: they sit under the obstacles.
    public sealed class GardenPlazaSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";

        [SerializeField] Vector2 middle = new Vector2(0f, -35f);   // the plaza's middle (x, z) in this object's frame
        [SerializeField] float rimRadius = 17.6f, waterY = -1.9f;
        [SerializeField] Vector3[] jets = new Vector3[0];

        NearFarSound[] fountains;
        NearFarSound moat;
        Transform moatAt;
        static AudioListener listener;

        // For the builder: the plaza's middle, its rim, the moat's water height and where each jet lands.
        public void Configure(Vector2 plazaMiddle, float rim, float water, Vector3[] landings)
        {
            middle = plazaMiddle;
            rimRadius = rim;
            waterY = water;
            jets = landings;
        }

        void Start()
        {
            // R536: the next beat's bobbing drums and lotus pads (GardenFloatSound), added here so the built scene needs
            // nothing new.
            if (GetComponent<GardenFloatSound>() == null) gameObject.AddComponent<GardenFloatSound>();
            AudioClip jetNear = Load("FountainJet"), jetFar = Load("FountainJetFar");
            fountains = new NearFarSound[jets.Length];
            for (int i = 0; i < jets.Length; i++)
            {
                var at = new GameObject("Fountain jet sound point " + (i + 1)).transform;
                at.SetParent(transform, false);
                at.localPosition = jets[i];
                fountains[i] = new NearFarSound(at, "Fountain jet sound", jetNear, jetFar, true)
                    { NearFull = 2f, NearEnd = 13f, FarStart = 5f, FarEnd = 28f, FarGain = .5f };
            }
            moatAt = new GameObject("Moat sound point").transform;
            moatAt.SetParent(transform, false);
            moatAt.localPosition = new Vector3(middle.x + rimRadius, waterY, middle.y);
            moat = new NearFarSound(moatAt, "Moat lapping sound", Load("MoatLap"), Load("MoatLapFar"), true)
                { NearFull = 3f, NearEnd = 16f, FarStart = 6f, FarEnd = 35f, FarGain = .6f };
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + name);
            return clip;
        }

        void Update()
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
            {
                Vector3 l = transform.InverseTransformPoint(listener.transform.position);
                Vector2 d = new Vector2(l.x, l.z) - middle;
                if (d.sqrMagnitude < .01f) d = Vector2.right;
                d = d.normalized * rimRadius;
                moatAt.localPosition = new Vector3(middle.x + d.x, waterY, middle.y + d.y);
            }
            // R535 (승규 11-10, "물 소리 들리는 사운드를 40퍼 낮춰"): both water sounds at 60 % of R530's level.
            // R537 (승규 11-10, "더 줄여 주고 아주 조금만 들리게"): down to 20 % of R530's level.
            moat.Mix(.35f * .2f, 1f);
            foreach (var f in fountains) f.Mix(.3f * .2f, 1f);
        }
    }
}
