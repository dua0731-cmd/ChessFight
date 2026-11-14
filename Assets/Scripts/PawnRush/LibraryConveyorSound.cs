using UnityEngine;

namespace ChessFight.PawnRush
{
    // The library conveyor's sound (R289): a belt-and-motor loop and a rail-slide loop on the moving carriage whose
    // volume and pitch follow its speed, and a dull clunk at each end when the carriage turns back. Reads the speed
    // from LibraryShuttleDriver, so every PC hears the same moment of the shared 16 s round trip.
    // R290: each sound is a NearFarSound — the detailed take up close, a muffled roomy take across the hall.
    // R316 (승규 님: "컨베이어 소리 좀 더 울리게 해줘"): every layer rings in the stone hall (a 2.8 s tail, the
    // clunk's a little longer) and the far layers carry farther and louder.
    // R443 (승규 11-05 "덜컹 하면서 멈출 수 있게 ... 그 사운드 찾아주고 ... 파티클"): the carriage now stops with a jolt
    // (LibraryShuttleDriver) and bounces, so the clunk plays on the driver's impact count instead of on the speed
    // dropping (the bounce would set it off again): ConveyorJolt(+Far), a heavy iron thunk with a rattle (ElevenLabs,
    // ArtSource/PawnRush/Library/conveyor_jolt_sound.py), and the impact's dust, chips and sparks (LibraryConveyorJoltFx,
    // made here at Play). A driver that found no stops keeps the old clunk on the speed.
    public sealed class LibraryConveyorSound : MonoBehaviour
    {
        const string Folder = "PawnRushLibrarySfx/";
        const float TopSpeed = 2f;      // m/s: the round trip's fastest stretch (about 1.9 m/s)
        const float TurnSpeed = .15f;   // below this the carriage counts as stopped at an end

        [SerializeField] LibraryShuttleDriver driver;
        [SerializeField] Transform carriage;

        NearFarSound belt, slide, clunk;
        AudioClip clunkNear, clunkFar, joltNear, joltFar;
        bool armed;
        int heardJolts;

        public void Configure(LibraryShuttleDriver shuttle, Transform movingCarriage)
        {
            driver = shuttle;
            carriage = movingCarriage;
        }

        void Start()
        {
            if (driver == null || carriage == null) { enabled = false; return; }
            // The motor carries across the whole hall; the shoes' grind and the clunk's crack are close-up detail.
            belt = new NearFarSound(carriage, "Conveyor belt sound", Load("ConveyorBeltLoop"), Load("ConveyorBeltLoopFar"), true)
                { NearFull = 4f, NearEnd = 28f, FarStart = 6f, FarEnd = 85f, FarGain = 1.1f }.Hall(2.8f, 0f);
            slide = new NearFarSound(carriage, "Conveyor rail slide sound", Load("ConveyorSlideLoop"), Load("ConveyorSlideLoopFar"), true)
                { NearFull = 3f, NearEnd = 22f, FarStart = 6f, FarEnd = 55f, FarGain = .8f }.Hall(2.8f, -100f);
            clunk = new NearFarSound(carriage, "Conveyor turn sound", null, null, false)
                { NearFull = 4f, NearEnd = 30f, FarStart = 6f, FarEnd = 95f, FarGain = 1.2f }.Hall(3.6f, 200f);
            clunkNear = Load("ConveyorTurnClunk");
            clunkFar = Load("ConveyorTurnClunkFar");
            joltNear = Load("ConveyorJolt");
            joltFar = Load("ConveyorJoltFar");
            if (joltNear == null) { joltNear = clunkNear; joltFar = clunkFar; }
            heardJolts = driver.Jolts;
            if (GetComponent<LibraryConveyorJoltFx>() == null) gameObject.AddComponent<LibraryConveyorJoltFx>().Configure(driver, carriage);
        }

        AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("Library conveyor sound missing: Resources/" + Folder + name);
            return clip;
        }

        void Update()
        {
            Vector3 v = driver.CarriageVelocity;
            float speed = v.magnitude;
            float t = Mathf.Clamp01(speed / TopSpeed);
            // The motor never stops (it hums at the ends), the shoes only sound while they slide.
            belt.Mix(Mathf.Lerp(.25f, .55f, t), Mathf.Lerp(.85f, 1.08f, t));
            slide.Mix(.45f * t * t, Mathf.Lerp(.8f, 1.05f, t));

            // R443: the jolting stop's impact, once per stop on the shared clock.
            if (driver.Jolting)
            {
                if (driver.Jolts != heardJolts)
                {
                    heardJolts = driver.Jolts;
                    clunk.Shot(joltNear, joltFar, .62f, Random.Range(.95f, 1.04f));   // R654: -21 dB (was -17)
                }
                return;
            }
            // A turn = the carriage slows below TurnSpeed after moving; clunk once, then wait until it moves again.
            if (speed > TurnSpeed * 3f) armed = true;
            if (armed && speed < TurnSpeed)
            {
                clunk.Shot(clunkNear, clunkFar, .8f, Random.Range(.92f, 1.05f));
                armed = false;
            }
        }
    }
}
