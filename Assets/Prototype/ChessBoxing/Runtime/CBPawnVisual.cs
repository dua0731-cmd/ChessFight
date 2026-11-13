using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.ChessBoxingPrototype
{
    /// <summary>Runtime-owned pawn skin and floating gloves; no shared asset is modified.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CBPawnVisual : MonoBehaviour, ISerializationCallbackReceiver
    {
        public float CharacterHeight { get; private set; }
        public float BodyHeight { get; private set; }
        public float BodyRadius { get; private set; }
        public float HeadRadius { get; private set; }
        public float HandRadius { get; private set; }
        public Vector3 HeadOffset { get; private set; }
        public Vector3 LeftHandOffset { get; private set; }
        public Vector3 RightHandOffset { get; private set; }
        public float BodyOpacity { get; private set; } = 1f;
        public int RemovedArmTriangles { get; private set; }

        [Serializable]
        sealed class BonePose
        {
            public string name;
            public Transform bone;
            public Vector3 position;
            public Quaternion rotation;
        }

        readonly Dictionary<string, BonePose> pose = new Dictionary<string, BonePose>();
        // Unity restores the live transforms on script reload but cannot serialize a Dictionary.
        // Cache the original bind-relative data so reloading never captures an animated pose as rest.
        [SerializeField, HideInInspector] List<BonePose> poseCache = new List<BonePose>();
        [SerializeField, HideInInspector] List<Material> ownedMaterials = new List<Material>();
        [SerializeField, HideInInspector] List<Material> bodyMaterials = new List<Material>();
        [SerializeField, HideInInspector] List<Mesh> ownedMeshes = new List<Mesh>();
        SkinnedMeshRenderer[] skins;
        Transform frame, body, head, handL, handR, gloveL, gloveR;
        Vector3 hipsAt, previousPosition;
        float gait, wantedOpacity = 1f;
        bool bound, transparent;

        public static CBPawnVisual Prepare(GameObject model, Transform parent, Color accent)
        {
            if (model == null) throw new InvalidOperationException("Pawn visual is missing. Run ChessFight > Chess Boxing Prototype > Create or Open Test Scene.");
            if (model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Use the generated visual-only PawnVisual prefab, not a gameplay or ragdoll prefab.");
            var instance = Instantiate(model, parent, false);
            instance.name = "Pawn visual (floating gloves, independent copy)";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (var a in instance.GetComponentsInChildren<Animator>(true)) { a.enabled = false; Destroy(a); }
            foreach (var c in instance.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Destroy(c); }
            foreach (var j in instance.GetComponentsInChildren<Joint>(true)) Destroy(j);
            foreach (var r in instance.GetComponentsInChildren<Rigidbody>(true)) { r.isKinematic = true; r.detectCollisions = false; Destroy(r); }
            var visual = instance.AddComponent<CBPawnVisual>();
            visual.frame = parent;
            try { visual.PrepareSkin(accent); }
            catch { Destroy(instance); throw; }
            return visual;
        }

        void PrepareSkin(Color accent)
        {
            var skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.sharedMesh == null) throw new InvalidOperationException("The pawn visual needs its existing skinned mesh and Mixamo bones.");
            var map = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string key = BoneName(t);
                if (!map.ContainsKey(key)) map.Add(key, t);
            }
            foreach (string name in new[] { "Hips", "Head", "LeftHand", "RightHand", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftFoot", "RightFoot" })
                if (!map.ContainsKey(name)) throw new InvalidOperationException("Pawn rig bone missing: " + name);

            RestoreBindPose(skin);
            if (map.TryGetValue("LeftToeBase", out var toe) && frame.InverseTransformDirection(toe.position - map["LeftFoot"].position).z < 0f)
                transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);

            Vector3[] vertices = skin.sharedMesh.vertices;
            if (vertices.Length == 0) throw new InvalidOperationException("Pawn skin contains no readable vertices.");
            Bounds bounds = MeshBounds(skin, vertices);
            if (bounds.size.y < 0.01f) throw new InvalidOperationException("Pawn skin has invalid bounds.");
            transform.localScale *= 1.05f / bounds.size.y;
            bounds = MeshBounds(skin, vertices);
            transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = MeshBounds(skin, vertices);
            CharacterHeight = bounds.size.y;
            hipsAt = frame.InverseTransformPoint(map["Hips"].position);
            BodyHeight = hipsAt.y;

            poseCache.Clear();
            foreach (var pair in map)
            {
                var saved = new BonePose { name = pair.Key, bone = pair.Value,
                    position = frame.InverseTransformPoint(pair.Value.position) - hipsAt,
                    rotation = Quaternion.Inverse(frame.rotation) * pair.Value.rotation };
                pose[pair.Key] = saved;
                poseCache.Add(saved);
            }

            Bounds headBounds = WeightedBounds(skin, vertices, map["Head"], map["Head"].position, .18f);
            HeadOffset = headBounds.center - hipsAt;
            HeadRadius = Mathf.Clamp(Mathf.Max(headBounds.size.x, headBounds.size.z) * .46f, .12f, .27f);
            HandRadius = .135f;
            BodyRadius = Mathf.Clamp(HeadRadius * .9f, .14f, .24f);
            float restHeight = HeadOffset.y - HeadRadius * .65f;
            LeftHandOffset = new Vector3(-.25f, restHeight, .23f);
            RightHandOffset = new Vector3(.25f, restHeight, .23f);

            // Triangle edits are made on private runtime copies, never the Generated mesh assets.
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in skins)
            {
                RemoveArmGeometry(renderer, map["LeftArm"], map["RightArm"]);
                renderer.updateWhenOffscreen = true;
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var copies = new Material[originals.Length];
                for (int i = 0; i < copies.Length; i++)
                {
                    if (originals[i] == null) continue;
                    var material = new Material(originals[i]) { name = "CB owned pawn body" };
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", accent);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", accent);
                    copies[i] = material;
                    ownedMaterials.Add(material);
                    bodyMaterials.Add(material);
                }
                renderer.sharedMaterials = copies;
            }

            // Build renderers directly so glove decoration never introduces primitive colliders.
            Mesh sphere = MakeSphere();
            ownedMeshes.Add(sphere);
            Material leather = MakeGloveMaterial("CB glove leather", accent, .46f);
            Material cuff = MakeGloveMaterial("CB glove cuff", Color.Lerp(accent, Color.black, .78f), .24f);
            Material trim = MakeGloveMaterial("CB glove trim", new Color(.9f, .91f, .86f), .3f);
            gloveL = MakeGlove(true, sphere, leather, cuff, trim);
            gloveR = MakeGlove(false, sphere, leather, cuff, trim);
        }

        static string BoneName(Transform bone)
        {
            string value = bone.name;
            int colon = value.LastIndexOf(':');
            return colon >= 0 ? value.Substring(colon + 1) : value;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Serialization callbacks can run off the main thread: only rebuild managed data here.
            // Do not access Transform members, renderers or materials in this callback.
            pose.Clear();
            if (poseCache == null) return;
            foreach (var saved in poseCache)
                if (saved != null && !string.IsNullOrEmpty(saved.name)) pose[saved.name] = saved;
        }

        void RemoveArmGeometry(SkinnedMeshRenderer skin, Transform leftArm, Transform rightArm)
        {
            if (skin.sharedMesh == null) return;
            Mesh source = skin.sharedMesh;
            BoneWeight[] weights = source.boneWeights;
            if (weights.Length != source.vertexCount) throw new InvalidOperationException("Pawn skin has no readable bone weights: " + skin.name);
            var armBone = new bool[skin.bones.Length];
            for (int i = 0; i < armBone.Length; i++)
            {
                Transform bone = skin.bones[i];
                armBone[i] = bone != null && (bone == leftArm || bone == rightArm || bone.IsChildOf(leftArm) || bone.IsChildOf(rightArm));
            }
            var remove = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float influence = ArmWeight(armBone, w.boneIndex0, w.weight0) + ArmWeight(armBone, w.boneIndex1, w.weight1)
                    + ArmWeight(armBone, w.boneIndex2, w.weight2) + ArmWeight(armBone, w.boneIndex3, w.weight3);
                remove[i] = influence >= .35f;
            }
            Mesh copy = Instantiate(source);
            copy.name = source.name + " (CB armless runtime copy)";
            ownedMeshes.Add(copy);
            for (int sub = 0; sub < copy.subMeshCount; sub++)
            {
                int[] indices = source.GetTriangles(sub);
                var kept = new List<int>(indices.Length);
                for (int t = 0; t < indices.Length; t += 3)
                {
                    int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                    if (remove[a] || remove[b] || remove[c]) { RemovedArmTriangles++; continue; }
                    kept.Add(a); kept.Add(b); kept.Add(c);
                }
                copy.SetTriangles(kept, sub, false);
            }
            // Mesh replacement while a renderer is active can retain incompatible GPU skin buffers.
            bool wasEnabled = skin.enabled;
            skin.enabled = false;
            try { skin.sharedMesh = copy; }
            finally { skin.enabled = wasEnabled; }
        }

        static float ArmWeight(bool[] bones, int index, float weight)
            => index >= 0 && index < bones.Length && bones[index] ? weight : 0f;

        Material MakeGloveMaterial(string label, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("A lit shader is required for the boxing gloves.");
            var material = new Material(shader) { name = label, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            ownedMaterials.Add(material);
            return material;
        }

        Transform MakeGlove(bool left, Mesh sphere, Material leather, Material cuff, Material trim)
        {
            var root = new GameObject(left ? "CB left floating glove" : "CB right floating glove").transform;
            root.SetParent(frame, false);
            float inward = left ? 1f : -1f;
            GlovePart(root, sphere, leather, "Knuckle pad", new Vector3(0f, .015f, .015f), new Vector3(.24f, .25f, .26f));
            GlovePart(root, sphere, leather, "Thumb", new Vector3(inward * .105f, -.055f, .01f), new Vector3(.095f, .13f, .15f));
            GlovePart(root, sphere, cuff, "Wrist cuff", new Vector3(0f, -.015f, -.096f), new Vector3(.18f, .17f, .075f));
            GlovePart(root, sphere, trim, "Cuff seam", new Vector3(0f, -.015f, -.125f), new Vector3(.18f, .165f, .025f));
            return root;
        }

        static void GlovePart(Transform parent, Mesh mesh, Material material, string label, Vector3 at, Vector3 scale)
        {
            var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = scale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Mesh MakeSphere()
        {
            const int longitude = 24, latitude = 16;
            var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new List<int>(longitude * latitude * 6);
            for (int y = 0; y <= latitude; y++)
                for (int x = 0; x <= longitude; x++)
                {
                    float v = y / (float)latitude, u = x / (float)longitude;
                    float pitch = v * Mathf.PI, yaw = u * Mathf.PI * 2f;
                    int i = y * (longitude + 1) + x;
                    normals[i] = new Vector3(Mathf.Sin(pitch) * Mathf.Cos(yaw), Mathf.Cos(pitch), Mathf.Sin(pitch) * Mathf.Sin(yaw));
                    vertices[i] = normals[i] * .5f;
                    uv[i] = new Vector2(u, v);
                    if (x < longitude && y < latitude)
                    {
                        int below = i + longitude + 1;
                        indices.Add(i); indices.Add(i + 1); indices.Add(below);
                        indices.Add(i + 1); indices.Add(below + 1); indices.Add(below);
                    }
                }
            var mesh = new Mesh { name = "CB glove rounded surface", vertices = vertices, normals = normals, uv = uv };
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void SetBodyOpacity(float alpha)
        {
            wantedOpacity = Mathf.Clamp(alpha, .06f, 1f);
        }

        void UpdateOpacity(float dt)
        {
            BodyOpacity = Mathf.MoveTowards(BodyOpacity, wantedOpacity, dt * 7f);
            bool fade = BodyOpacity < .995f;
            foreach (var material in bodyMaterials)
            {
                if (fade != transparent) ConfigureTransparency(material, fade);
                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor"); color.a = BodyOpacity;
                    material.SetColor("_BaseColor", color);
                }
                if (material.HasProperty("_Color"))
                {
                    Color color = material.GetColor("_Color"); color.a = BodyOpacity;
                    material.SetColor("_Color", color);
                }
            }
            transparent = fade;
        }

        static void ConfigureTransparency(Material material, bool fade)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", fade ? 1f : 0f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", fade ? 2f : 0f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)(fade ? BlendMode.SrcAlpha : BlendMode.One));
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)(fade ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)(fade ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", fade ? 0f : 1f);
            material.SetOverrideTag("RenderType", fade ? "Transparent" : "Opaque");
            material.renderQueue = fade ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (fade) { material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHABLEND_ON"); }
            else { material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHABLEND_ON"); }
            material.SetShaderPassEnabled("ShadowCaster", !fade);
            material.SetShaderPassEnabled("DepthOnly", !fade);
            material.SetShaderPassEnabled("DepthNormals", !fade);
            material.SetShaderPassEnabled("DepthNormalsOnly", !fade);
        }

        static void RestoreBindPose(SkinnedMeshRenderer skin)
        {
            var bones = skin.bones;
            var binds = skin.sharedMesh.bindposes;
            var order = new List<int>();
            for (int i = 0; i < bones.Length && i < binds.Length; i++) if (bones[i] != null) order.Add(i);
            order.Sort((a, b) => Depth(bones[a]).CompareTo(Depth(bones[b])));
            Matrix4x4 root = skin.transform.localToWorldMatrix;
            foreach (int i in order)
            {
                Matrix4x4 world = root * binds[i].inverse;
                bones[i].SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
        }

        static int Depth(Transform t)
        {
            int depth = 0;
            for (; t.parent != null; t = t.parent) depth++;
            return depth;
        }

        Bounds MeshBounds(SkinnedMeshRenderer skin, Vector3[] vertices)
        {
            var bounds = new Bounds(frame.InverseTransformPoint(skin.transform.TransformPoint(vertices[0])), Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(frame.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])));
            return bounds;
        }

        Bounds WeightedBounds(SkinnedMeshRenderer skin, Vector3[] vertices, Transform bone, Vector3 fallback, float radius)
        {
            int index = Array.IndexOf(skin.bones, bone);
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            bool found = false;
            var result = new Bounds(frame.InverseTransformPoint(fallback), Vector3.one * radius * 2f);
            for (int i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                var w = weights[i];
                float weight = (w.boneIndex0 == index ? w.weight0 : 0f) + (w.boneIndex1 == index ? w.weight1 : 0f)
                    + (w.boneIndex2 == index ? w.weight2 : 0f) + (w.boneIndex3 == index ? w.weight3 : 0f);
                if (weight < .65f) continue;
                Vector3 point = frame.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                else result.Encapsulate(point);
            }
            return result;
        }

        public void Bind(Transform physicsBody, Transform headBody, Transform leftHandBody, Transform rightHandBody)
        {
            body = physicsBody; head = headBody; handL = leftHandBody; handR = rightHandBody;
            previousPosition = body.position;
            bound = true;
            LateUpdate();
        }

        /// <summary>Floating gloves keep a fixed body-relative reach envelope, independent of mouse distance.</summary>
        public Vector3 ConstrainHandTarget(bool left, Vector3 wanted)
        {
            wanted.x = Mathf.Clamp(wanted.x, -.48f, .48f);
            wanted.y = Mathf.Clamp(wanted.y, -.10f, HeadOffset.y + .12f);
            wanted.z = Mathf.Clamp(wanted.z, -.12f, .85f);
            Vector3 anchor = new Vector3(left ? -.19f : .19f, HeadOffset.y - .17f, .04f);
            return anchor + Vector3.ClampMagnitude(wanted - anchor, .90f);
        }

        public Quaternion HandLocalRotation(bool left, Vector3 wanted)
        {
            Vector3 anchor = new Vector3(left ? -.19f : .19f, HeadOffset.y - .17f, -.2f);
            Vector3 direction = wanted - anchor;
            return direction.sqrMagnitude > .00001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        }

        void LateUpdate()
        {
            if (!bound || body == null || head == null || handL == null || handR == null) return;
            float dt = Mathf.Max(.001f, Time.deltaTime);
            Vector3 velocity = (body.position - previousPosition) / dt;
            previousPosition = body.position;
            float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            float upright = Mathf.Clamp01((Vector3.Dot(body.up, Vector3.up) - .45f) / .55f);
            gait += Mathf.Min(speed, 4f) * Time.deltaTime * 10f;
            Place("Hips", body.position, body.rotation);
            foreach (string spine in new[] { "Spine", "Spine1", "Spine2", "Neck" })
                if (pose.TryGetValue(spine, out var p)) p.bone.SetPositionAndRotation(body.TransformPoint(p.position), body.rotation * p.rotation);
            var headPose = pose["Head"];
            headPose.bone.SetPositionAndRotation(head.TransformPoint(headPose.position - HeadOffset), head.rotation * headPose.rotation);
            // The source arm bones stay at rest; their triangles have been removed from the private skin.
            gloveL.SetPositionAndRotation(handL.position, handL.rotation);
            gloveR.SetPositionAndRotation(handR.position, handR.rotation);
            Foot("Left", 1f, speed, upright);
            Foot("Right", -1f, speed, upright);
            UpdateBounds();
            UpdateOpacity(Time.deltaTime);
        }

        void UpdateBounds()
        {
            Bounds world = new Bounds(body.position, Vector3.one * CharacterHeight * 1.4f);
            world.Encapsulate(new Bounds(head.position, Vector3.one * (HeadRadius * 2.5f)));
            foreach (var renderer in skins)
            {
                Bounds local = new Bounds(renderer.transform.InverseTransformPoint(world.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            local.Encapsulate(renderer.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents, new Vector3(x, y, z))));
                renderer.localBounds = local;
            }
        }

        void Place(string name, Vector3 point, Quaternion rotation)
        {
            BonePose p = pose[name];
            p.bone.SetPositionAndRotation(point, rotation * p.rotation);
        }

        void Foot(string side, float phase, float speed, float upright)
        {
            var foot = pose[side + "Foot"];
            float amount = Mathf.Clamp01(speed / 1.8f) * upright;
            float wave = Mathf.Sin(gait) * phase;
            Vector3 step = new Vector3(0f, Mathf.Max(0f, wave) * .055f, wave * .085f) * amount;
            Vector3 footPosition = body.TransformPoint(foot.position + step);
            float floor = frame.position.y + BodyHeight + foot.position.y;
            footPosition.y = Mathf.Max(footPosition.y, floor + step.y);
            foot.bone.SetPositionAndRotation(footPosition,
                body.rotation * Quaternion.Euler(wave * -13f * amount, 0f, 0f) * foot.rotation);
        }

        void OnDestroy()
        {
            // Glove roots are siblings of the skin because their dimensions use world metres.
            if (gloveL != null) Destroy(gloveL.gameObject);
            if (gloveR != null) Destroy(gloveR.gameObject);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
