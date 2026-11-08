using System.Globalization;
using UnityEngine;
using ChessFight.Game;
namespace ChessFight.RagdollLab
{
    // All four previews use the course's selected replay, including F3 round changes.
    public sealed class WarRoomScoreLabel : MonoBehaviour
    {
        [SerializeField,Range(0,1)] float previewFraction;
        GameObject contents;
        Material white,black,teal;
        void Start()
        {
            var course=GetComponentInParent<GiantChessCourse>();
            if(course!=null && course.CurrentGame!=null)Refresh(course.CurrentGame);
        }
        Material Mat(Color color) => LitMaterials.NewLit(color);
        public void Refresh(GiantChessGame game)
        {
            if(contents!=null){contents.SetActive(false);Destroy(contents);}
            if(white==null){white=Mat(new Color(.95f,.9f,.7f));black=Mat(new Color(.12f,.1f,.2f));teal=Mat(new Color(.1f,.7f,.7f));}
            contents=new GameObject("Selected game preview");contents.transform.SetParent(transform,false);
            int ply=Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(previewFraction)*(game.rows.Length-1)),0,game.rows.Length-1);
            var row=game.rows[ply].Split(',');string board=row[8];
            for(int i=0;i<64;i++) if(board[i]!='.')
            {
                Vector3 pos=Square(i); bool light=char.IsUpper(board[i]);
                Box("Piece marker",pos,new Vector3(.8f,.8f,.12f),light?white:black);
                Caption(char.ToUpperInvariant(board[i]).ToString(),pos+Vector3.back*.12f,.55f,light?Color.black:Color.white);
            }
            int from=int.Parse(row[0],CultureInfo.InvariantCulture),to=int.Parse(row[1],CultureInfo.InvariantCulture);
            var a=Square(from)+Vector3.back*.3f;var b=Square(to)+Vector3.back*.3f;
            if(char.ToUpperInvariant(board[from])=='N')
            {var corner=new Vector3(b.x,a.y,a.z);Line(a,corner);Line(corner,b);}
            else Line(a,b);
            Box("Destination",b,new Vector3(.3f,.3f,.08f),teal);
            Caption(game.id+" · "+(ply/2+1)+(ply%2==0?". ":"... ")+row[5],new Vector3(0,8.65f,-.6f),8,Color.white);
        }
        static Vector3 Square(int i) => new Vector3(i%8-3.5f,4+i/8-3.5f,-.5f);
        void Box(string name,Vector3 pos,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(contents.transform,false);
            go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        }
        void Line(Vector3 a,Vector3 b)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Selected move path";go.transform.SetParent(contents.transform,false);
            go.transform.localPosition=(a+b)*.5f;go.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            go.transform.localScale=new Vector3(Vector3.Distance(a,b),.1f,.05f);go.GetComponent<Renderer>().sharedMaterial=teal;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        }
        void Caption(string value,Vector3 position,float width,Color color)
        {
            var go=new GameObject("Score caption");go.transform.SetParent(contents.transform,false);go.transform.localPosition=position;
            var text=go.AddComponent<TextMesh>();text.text=value;text.font=RuntimePanels.KoreanFont;
            text.fontSize=48;text.characterSize=.15f;text.anchor=TextAnchor.MiddleCenter;text.color=color;
            var renderer=go.GetComponent<MeshRenderer>();if(text.font!=null)renderer.sharedMaterial=text.font.material;
            if(renderer.bounds.size.x>width)go.transform.localScale*=width/renderer.bounds.size.x;
        }
        void OnDestroy(){if(white!=null)Destroy(white);if(black!=null)Destroy(black);if(teal!=null)Destroy(teal);}
    }
}
