using UnityEngine;
using ChessFight.Game;
namespace ChessFight.RagdollLab
{
    public sealed class WarRoomScoreLabel : MonoBehaviour
    {
        [SerializeField] string label;
        void Start()
        {
            Caption(label,new Vector3(0,9.4f,-.6f),8);
            // Labels identify the actual pieces reconstructed from the replay, not generic dots.
            foreach(Transform child in transform)
                if(child.name.StartsWith("Piece "))
                    Caption(child.name.Substring(6).ToUpperInvariant(),child.localPosition+Vector3.back*.4f,.55f);
        }
        void Caption(string value,Vector3 position,float width)
        {
            var go=new GameObject("Score caption");go.transform.SetParent(transform,false);go.transform.localPosition=position;
            var text=go.AddComponent<TextMesh>();text.text=value;text.font=RuntimePanels.KoreanFont;
            text.fontSize=48;text.characterSize=.15f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=text.font.material;
            if(renderer.bounds.size.x>width)go.transform.localScale*=width/renderer.bounds.size.x;
        }
    }
}
