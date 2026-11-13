using UnityEngine;
namespace ChessFight.RagdollLab
{
    // Local presentation only: reveal a large room without resizing any physical objects.
    public sealed class RoyalLibraryPortal : MonoBehaviour
    {
        [SerializeField] Transform threshold;
        [SerializeField] GameObject veil;
        [SerializeField, Min(.2f)] float revealSeconds = 1.6f;
        [SerializeField, Range(0,25)] float fovExpansion = 16;
        [SerializeField, Min(0)] float cameraPullback = 7;
        LabCamera rig; RagdollPawn observed;
        float previousZ, elapsed, savedFov, savedDistance;
        bool revealing;
        void Awake() { rig=FindFirstObjectByType<LabCamera>(); }
        public void ResetPresentation()
        {
            Restore(); observed=null;
            if(veil!=null) veil.SetActive(true);
        }
        void Restore()
        {
            if(revealing && rig!=null) { rig.baseFov=savedFov;rig.distance=savedDistance; }
            revealing=false;
        }
        void Update()
        {
            if(rig==null || threshold==null || rig.soloTarget==null) return;
            var pawn=rig.soloTarget;
            Vector3 p=threshold.InverseTransformPoint(pawn.Hips.position);
            if(pawn!=observed) { Restore();observed=pawn;previousZ=p.z; }
            if(previousZ<0 && p.z>=0 && Mathf.Abs(p.x)<5 && p.y>-1 && p.y<8 && !revealing)
            {
                savedFov=rig.baseFov;savedDistance=rig.distance;elapsed=0;revealing=true;
                if(veil!=null) veil.SetActive(false);
            }
            previousZ=p.z;
            if(!revealing) return;
            elapsed+=Time.deltaTime;
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.2f,revealSeconds));
            float pulse=Mathf.Sin(t*Mathf.PI);
            rig.baseFov=savedFov+fovExpansion*pulse;
            rig.distance=savedDistance+cameraPullback*pulse;
            if(t>=1) Restore();
        }
        void OnGUI()
        {
            if(!revealing || elapsed>.3f) return;
            Color before=GUI.color;
            GUI.color=new Color(.6f,.9f,1,Mathf.Clamp01(1-elapsed/.3f)*.5f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
            GUI.color=before;
        }
        void OnDisable() { Restore(); }
    }
}
