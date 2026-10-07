using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A wooden barricade only the rook's charge breaks (DESIGN §6). Broken, it opens the way for a while,
    /// blinks for the last two seconds and stands again - but not on top of a piece standing in the gap.
    /// Test-bed art: a stack of planks made of boxes.
    /// </summary>
    public class SkillBarricade : MonoBehaviour
    {
        public Vector3 size = new Vector3(3f, 1.6f, 0.35f);

        BoxCollider box;
        Renderer[] planks;
        float regrowLeft;
        public bool Standing => box != null && box.enabled;

        public static SkillBarricade Build(Vector3 groundCenter, Vector3 facing, string label)
        {
            var go = new GameObject(label);
            go.transform.position = groundCenter;
            facing.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.forward, Vector3.up);
            var b = go.AddComponent<SkillBarricade>();
            b.Make();
            return b;
        }

        void Make()
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            var wood = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.36f, 0.2f) };
            int rows = 4;
            planks = new Renderer[rows + 2];
            for (int i = 0; i < rows; i++)
            {
                var plank = Plank(new Vector3(0f, (i + 0.5f) * size.y / rows, 0f), new Vector3(size.x, size.y / rows * 0.8f, size.z * 0.6f), wood);
                plank.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 2f : -2f));
                planks[i] = plank.GetComponent<Renderer>();
            }
            var post = new Material(Shader.Find("Standard")) { color = new Color(0.4f, 0.25f, 0.13f) };
            planks[rows] = Plank(new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(0.18f, size.y + 0.2f, size.z), post).GetComponent<Renderer>();
            planks[rows + 1] = Plank(new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(0.18f, size.y + 0.2f, size.z), post).GetComponent<Renderer>();
        }

        Transform Plank(Vector3 at, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        public void Break(float regrow)
        {
            if (!Standing) return;
            box.enabled = false;
            regrowLeft = regrow;
            Show(false);
        }

        void Update()
        {
            if (Standing) return;
            regrowLeft -= Time.deltaTime;
            // The outline blinks for the last two seconds before it is back.
            Show(regrowLeft < 2f && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f);
            if (regrowLeft > 0f) return;
            var center = transform.TransformPoint(box.center);
            // A little smaller than the wall, so the floor it stands on does not count.
            var half = new Vector3(box.size.x * 0.5f, box.size.y * 0.5f - 0.12f, box.size.z * 0.5f);
            if (Physics.CheckBox(center + Vector3.up * 0.06f, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                regrowLeft = 0.2f;   // someone is in the gap: wait for them
                return;
            }
            box.enabled = true;
            Show(true);
            PawnRushSkillBed.Report("바리케이드가 다시 생김");
        }

        void Show(bool on)
        {
            if (planks == null) return;
            foreach (var r in planks)
                if (r != null) r.enabled = on;
        }
    }
}
