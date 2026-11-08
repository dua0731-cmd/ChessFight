using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R379 (승규 11-04 "바닥 충돌 등 맵 충돌 입혀"): the library (작전 회의실) collides as it is drawn. Until now only the
        // walked levels were boxes (start / goal floors, balconies, basement, the return stairs), so walls, piers, the
        // balustrades, bookcases, desks, crates, the cannons and the war map let pawns through. Every model piece within
        // reach gets a mesh collider of its own shape (the FBX is readable for it, as the garden's), except:
        //   - what starts more than ReachAbove over the model's floor (vaults, ribs, banners, the upper lanterns),
        //   - what is smaller than SmallPiece every way (books, rivets, finials),
        //   - flat floor details thinner than FlatDetail (tile joints, inlays, the compass rose): a pawn would trip on
        //     their edges, and the floors under them already collide,
        //   - the parts that turn (the cannons' fans, the armillary's rings and globe).
        // The walked boxes stay; where a model floor lies on one they agree. The conveyor keeps its moving deck box;
        // only its fixed guide rails, piers and take-off ramp (Fixed_*) collide as meshes.
        const float ReachAbove = 3.5f, SmallPiece = .3f, FlatDetail = .08f, PropSize = 5f;
        const long DenseProp = 20000;   // triangles: a smaller prop with more collides as its box

        static void LibraryCollision(GameObject hall, GameObject shuttle)
        {
            float floor = hall.transform.position.y;
            int meshes = 0, boxes = 0, skipped = 0;
            long triangles = 0;
            foreach (var filter in hall.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !filter.TryGetComponent<Renderer>(out var r)) continue;
                string n = filter.name;
                bool turns = (n.StartsWith("BLOWER_") && n.EndsWith("_Fan")) || n.StartsWith("Start_armillary_Ring")
                    || n.StartsWith("Start_armillary_Sphere")
                    || n.Contains("_zone")    // START_spawn_zone: the start disc pawns appear on stays walk-through, as before
                    || n.StartsWith("Pier_banner_");   // R402: the pier banners are cloth (their foot 3.1 m up)
                var size = r.bounds.size;
                bool high = r.bounds.min.y - floor > ReachAbove;
                bool small = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < SmallPiece;
                bool flat = size.y < FlatDetail;
                if (turns || high || small || flat || filter.GetComponent<Collider>() != null) { skipped++; continue; }
                long count = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
                if (count > DenseProp && Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < PropSize)
                {
                    // a small, dense prop (the cannons, the war map frame): its box is enough and far lighter
                    var box = filter.gameObject.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                    boxes++;
                    continue;
                }
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                meshes++;
                triangles += count;
            }
            // The cannons recoil by moving their root: a kinematic body keeps their colliders from being re-baked as
            // moved statics every frame.
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("BLOWER_") || t.name.Contains("|") || t.name.EndsWith("_Fan")) continue;
                if (t.GetComponent<Rigidbody>() != null) continue;
                var body = t.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
            int fixedParts = 0;
            if (shuttle != null)
            {
                foreach (var filter in shuttle.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.name.StartsWith("Fixed_") || filter.sharedMesh == null) continue;
                    if (filter.GetComponent<Collider>() != null) continue;
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    fixedParts++;
                }
            }
            Debug.Log("[작전 회의실] 맵 충돌: 모델 " + meshes + "개 메시 충돌(삼각형 " + triangles + "), 촘촘한 소품 " + boxes + "개 상자, 뺀 것 " + skipped
                      + "개(높은 곳 · 작은 것 · 얇은 바닥 장식 · 도는 부품), 컨베이어 고정 부품 " + fixedParts + "개");
        }
    }
}
