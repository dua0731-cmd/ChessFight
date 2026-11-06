using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The "next crossing" board of the endgame war room (승규 님's level design: read the connection, then cross; R240).
    // A small map table beside the way: a marker runs along a strip as the ferry runs between its landings, its lamp is
    // green while the ferry waits at this side, amber while it comes back, red otherwise; one lamp per folding bridge
    // ahead is green while it is open, amber while it warns and red while it folds, stays folded or unfolds.
    //
    // It only reads the devices, which move on the shared obstacle clock, so every PC shows the same.
    public sealed class CrossingSignal : MonoBehaviour
    {
        // The ferry's middle within this share of its run from this side: close enough to step or hop on.
        const float Boardable = .1f;

        [SerializeField] Transform ferry, ferryFrom, ferryTo;        // the ferry and where its middle is at either landing
        [SerializeField] Transform marker, stripFrom, stripTo;       // its double on the table
        [SerializeField] Renderer ferryLamp;
        [SerializeField] FoldingBridge[] bridges = new FoldingBridge[0];
        [SerializeField] Renderer[] bridgeLamps = new Renderer[0];

        float lastRun = -1f;

        public void ShowFerry(Transform ferry, Transform from, Transform to, Transform marker, Transform stripFrom, Transform stripTo, Renderer lamp)
        {
            this.ferry = ferry;
            ferryFrom = from;
            ferryTo = to;
            this.marker = marker;
            this.stripFrom = stripFrom;
            this.stripTo = stripTo;
            ferryLamp = lamp;
        }

        public void ShowBridges(FoldingBridge[] bridges, Renderer[] lamps)
        {
            this.bridges = bridges;
            bridgeLamps = lamps;
        }

        static Material go, soon, stop;
        static Material Go => go != null ? go : go = Arena.Emissive(new Color(.3f, .9f, .35f), 1.5f);
        static Material Soon => soon != null ? soon : soon = Arena.Emissive(new Color(1f, .7f, .15f), 1.5f);
        static Material Stop => stop != null ? stop : stop = Arena.Emissive(new Color(.9f, .2f, .15f), 1.5f);

        void Update()
        {
            if (ferry != null && ferryFrom != null && ferryTo != null)
            {
                // How far the ferry is from this side's landing (0) to the far one (1).
                var span = ferryTo.position - ferryFrom.position;
                float run = Mathf.Clamp01(Vector3.Dot(ferry.position - ferryFrom.position, span) / Mathf.Max(.0001f, span.sqrMagnitude));
                if (marker != null && stripFrom != null && stripTo != null) marker.position = Vector3.Lerp(stripFrom.position, stripTo.position, run);
                bool coming = lastRun >= 0f && run < lastRun;
                Light(ferryLamp, run <= Boardable ? Go : coming ? Soon : Stop);
                lastRun = run;
            }
            for (int i = 0; i < bridges.Length && i < bridgeLamps.Length; i++)
            {
                if (bridges[i] == null || bridgeLamps[i] == null) continue;
                var state = bridges[i].State;
                Light(bridgeLamps[i], state == FoldingBridgeState.Open ? Go : state == FoldingBridgeState.Warning ? Soon : Stop);
            }
        }

        static void Light(Renderer lamp, Material colour)
        {
            if (lamp != null && lamp.sharedMaterial != colour) lamp.sharedMaterial = colour;
        }
    }
}
