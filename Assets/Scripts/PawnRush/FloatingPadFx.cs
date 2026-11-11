using System.Collections.Generic;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R558 (승규 11-10 "착지는 C로 해주고 너무 탁이고 저기 구간은 기물이 뛰고 착지를 하면 살짝 내려가는 연출을 넣어줘"): a
    // floating stone of the grand stair hall (Course01v4Builder.FloatingPad) answers a piece landing on it.
    //   dip    the pad's model sinks 8 cm at once and comes back up within about half a second, tipping a little
    //          toward where the piece came down - a picture only: the collider (and the bobbing stone's own motion,
    //          ObstacleMotion) stays where it is, so nothing about the run changes.
    //   land   a weighty wooden knock on the board (HallPadLand, hall_pad_sounds.py: R557 mockup C made less sharp),
    //          at the pad, ringing in the stone hall and heard only inside it.
    // Every PC sees each piece arrive on the pad itself (its feet's colliders just over the deck), as JumpPadFx does,
    // so it needs no packet. No sound for the bobbing (승규: "움직이는 발판이 내는 소리는 지금 별로야").
    [DisallowMultipleComponent]
    public sealed class FloatingPadFx : MonoBehaviour
    {
        const string LandClip = "PawnRushHallSfx/HallPadLand";
        const float Dip = .08f, Tilt = 2.5f, DipTime = .07f;   // m, degrees at the pad's edge, s to the lowest point
        const float Rearm = .35f;                               // s off the pad before a new arrival counts
        const float Feet = .25f;                                // m over the deck that counts as on it
        const float LandVolume = .8f;

        static AudioClip landClip;
        static bool clipLooked;

        BoxCollider deck;
        Transform model;
        Vector3 rest;
        Quaternion restTurn;
        AudioSource voice;
        float landedAt = -10f;
        Vector3 tipAxis = Vector3.right;
        float tip;
        readonly Collider[] hits = new Collider[64];
        readonly Dictionary<ImportedObstacleActor, float> lastSeen = new Dictionary<ImportedObstacleActor, float>();
        readonly HashSet<ImportedObstacleActor> seen = new HashSet<ImportedObstacleActor>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            landClip = null;
            clipLooked = false;
        }

        void Start()
        {
            deck = GetComponent<BoxCollider>();
            model = transform.Find("Pad model");
            if (deck == null || model == null)
            {
                enabled = false;
                return;
            }
            rest = model.localPosition;
            restTurn = model.localRotation;
            if (!clipLooked)
            {
                clipLooked = true;
                landClip = Resources.Load<AudioClip>(LandClip);
                if (landClip == null) Debug.LogWarning("[대계단 홀] 소리 파일이 없어 재생하지 않습니다: Resources/" + LandClip);
            }
            var go = new GameObject("Pad land sound");
            go.transform.SetParent(model, false);
            voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 3f;
            voice.maxDistance = 25f;
            voice.dopplerLevel = 0f;
            voice.volume = LandVolume;
            RollingSpawner.Ring(go);
            SectionSoundGate.Gate(voice);   // heard only in the hall
        }

        void Update()
        {
            Watch(Time.time);
            // Down at once, back up softly: (t / T) e^(1 - t / T) is 1 at T and nearly 0 by 6 T.
            float since = Time.time - landedAt;
            float k = since >= 0f && since < 8f * DipTime ? since / DipTime * Mathf.Exp(1f - since / DipTime) : 0f;
            model.localPosition = rest + transform.InverseTransformVector(Vector3.down * (Dip * k));
            model.localRotation = (k > 0f && tip > 0f ? Quaternion.AngleAxis(Tilt * tip * k, transform.InverseTransformDirection(tipAxis)) : Quaternion.identity) * restTurn;
        }

        // A piece arriving on the deck: its colliders just over the top, not seen there for Rearm s.
        void Watch(float t)
        {
            var box = deck.bounds;
            float top = box.max.y;
            int n = Physics.OverlapBoxNonAlloc(new Vector3(box.center.x, top + Feet * .5f, box.center.z),
                new Vector3(box.extents.x, Feet * .5f, box.extents.z), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(hits[i]);
                if (actor == null || !seen.Add(actor) || actor.Position.y < top - .2f) continue;
                if (!lastSeen.TryGetValue(actor, out float last) || t - last > Rearm) Land(t, actor.Position);
                lastSeen[actor] = t;
            }
        }

        void Land(float t, Vector3 at)
        {
            landedAt = t;
            // Tip toward where it came down (about the line across the deck square to it), not at all at the middle.
            var off = at - deck.bounds.center;
            off.y = 0f;
            float half = Mathf.Max(.1f, deck.bounds.extents.x);
            tip = off.sqrMagnitude > .01f ? Mathf.Clamp01(off.magnitude / half) : 0f;
            if (tip > 0f) tipAxis = Vector3.Cross(Vector3.up, off.normalized);
            if (landClip == null || voice == null) return;
            voice.pitch = Random.Range(.94f, 1.05f);
            voice.PlayOneShot(landClip);
        }
    }
}
