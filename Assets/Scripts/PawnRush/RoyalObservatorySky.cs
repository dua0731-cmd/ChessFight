using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 (the user, before merging into main, on the night shell seen from the balcony against the scene's blue sky:
    // "천문대 안에서만 밤하늘"): the night outside the Royal Observatory - through the dome's slit and from the balcony -
    // is the scene's skybox, swapped for RO_NightSkybox while the camera is in the observatory and given back as soon as
    // it leaves. It replaces the 260 m night shell (RoHallOutside), whose edge showed and which reached toward the next
    // section in a round. Only the skybox changes: the ambient light, fog and sun stay the scene's (the hall's own lamps
    // light it), so nothing else in the round changes with it. Every PC does this for its own camera; nothing is sent.
    public sealed class RoyalObservatorySky : MonoBehaviour
    {
        // The observatory in its own frame: the round hall (r 53 round (0, 64)) from the tunnel's mouth to past the
        // balcony, the abyss below to over the dome. A camera this far outside it still counts (Margin), and it must be
        // clearly out before the scene's sky comes back (Leave), so standing at the edge does not flick it.
        static readonly Bounds Area = new Bounds(new Vector3(0f, 18f, 66f), new Vector3(116f, 110f, 150f));
        const float Margin = 4f, Leave = 10f;
        const string MaterialPath = "RoyalObservatory/Materials/RO_NightSky_Skybox";

        static Material night;
        Material sceneSky;
        bool applied;
        Camera view;
        float lookForView;

        void LateUpdate()
        {
            if (!FindView()) { if (applied) Restore(); return; }
            var p = transform.InverseTransformPoint(view.transform.position);
            float outside = Vector3.Distance(Area.ClosestPoint(p), p);
            if (!applied && outside <= Margin) Apply();
            else if (applied && outside > Leave) Restore();
        }

        bool FindView()
        {
            if (view != null && view.isActiveAndEnabled) return true;
            if (Time.unscaledTime < lookForView) return false;
            lookForView = Time.unscaledTime + 1f;
            view = Camera.main;
            if (view == null)
                foreach (var c in Camera.allCameras)
                    if (c.cameraType == CameraType.Game && c.targetTexture == null) { view = c; break; }
            return view != null;
        }

        void Apply()
        {
            if (night == null) night = Resources.Load<Material>(MaterialPath);
            if (night == null) { Debug.LogWarning("[천문대] 밤하늘 재질이 없습니다: Resources/" + MaterialPath); enabled = false; return; }
            sceneSky = RenderSettings.skybox;
            RenderSettings.skybox = night;
            applied = true;
        }

        void Restore()
        {
            RenderSettings.skybox = sceneSky;
            applied = false;
        }

        void OnDisable()
        {
            if (applied) Restore();
        }
    }
}
