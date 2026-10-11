using ChessFight.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.ProtectKing
{
    // StateDrivenMover: tilted by the weight on its deck, not by ObstacleClock. Offline only
    // until the host sends its angle (Docs/KingRush/MAP_IMPORT.md).
    [ExecuteAlways]
    public sealed class WeightedBridge : Obstacle
    {
        [SerializeField] ObstacleContext context;
        [SerializeField, Range(0f, 80f), Tooltip("Maximum downward tilt in degrees on either side. Zero keeps the deck level.")]
        float maximumTilt = 24f;
        [SerializeField, Range(1, 64), Tooltip("Number of tiles across the tilting direction.")] int tilesX = 4;
        [SerializeField, Range(1, 64), Tooltip("Number of tiles along the bridge.")] int tilesZ = 8;
        [SerializeField, Min(.1f), Tooltip("Side length of one square tile in metres, before root scale.")] float tileSize = 1.5f;
        [SerializeField, Min(.05f)] float deckThickness = .4f;
        [SerializeField, Min(0f)] float degreesPerPlayer = 12f;
        [SerializeField, Min(1f)] float tiltSpeed = 30f;
        [SerializeField, Min(0f)] float slideSpeed = 3f;
        [SerializeField, HideInInspector] Rigidbody deckBody;
        [SerializeField, HideInInspector] Transform solidDeck;
        [SerializeField, HideInInspector] Transform leftStripe, rightStripe, entryRamp, exitRamp;
        [SerializeField, HideInInspector] MeshFilter tileMesh;
        [SerializeField, HideInInspector] GameObject[] oldTiles;
        Mesh generatedTiles;
        bool geometryDirty;
        Transform Deck => deckBody != null ? deckBody.transform : transform;
        Vector3 DeckSize => new Vector3(tilesX * tileSize, deckThickness, tilesZ * tileSize);
        public float Angle { get; private set; }
        public uint NetworkId { get; private set; }
        public int RiderCount { get; private set; }
        public Vector3 SlideVelocity => Vector3.ProjectOnPlane(Vector3.down, Deck.up) * slideSpeed;
        static readonly List<WeightedBridge> instances = new List<WeightedBridge>();
        readonly Collider[] overlaps = new Collider[512];
        readonly HashSet<ImportedObstacleActor> riders = new HashSet<ImportedObstacleActor>();
        Rigidbody body;
        Quaternion restRotation;
        float remoteAngle, previousTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { instances.Clear(); }
        void Awake()
        {
            if (!Application.isPlaying) return;
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            body = deckBody != null ? deckBody : GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            restRotation = Deck.localRotation;
            // Identical saved hierarchy on host and clients yields an identical ID for each instance.
            uint hash = 2166136261;
            for (var t = Deck; t != null; t = t.parent)
                foreach (char c in t.name + ":" + t.GetSiblingIndex()) hash = (hash ^ c) * 16777619;
            NetworkId = hash;
        }
        void OnEnable()
        {
            RefreshGeometry();
            if (Application.isPlaying && !instances.Contains(this)) instances.Add(this);
        }
        void OnValidate()
        {
            tilesX = Mathf.Clamp(tilesX, 1, 64); tilesZ = Mathf.Clamp(tilesZ, 1, 64);
            tileSize = Mathf.Max(.1f, tileSize); deckThickness = Mathf.Max(.05f, deckThickness);
            maximumTilt = Mathf.Clamp(maximumTilt, 0f, 80f);
            geometryDirty = true;
        }
        void Update() { if (geometryDirty) RefreshGeometry(); }
        void OnDestroy()
        {
            if (generatedTiles == null) return;
            if (Application.isPlaying) Destroy(generatedTiles); else DestroyImmediate(generatedTiles);
        }
        void RefreshGeometry()
        {
            geometryDirty = false;
            if (solidDeck == null || tileMesh == null) return;
            Vector3 size = DeckSize;
            solidDeck.localScale = size; // The authored unit BoxCollider scales with the mesh.
            ResizeStripe(leftStripe, -1f, size); ResizeStripe(rightStripe, 1f, size);
            ResizeRamp(entryRamp, -1f, size); ResizeRamp(exitRamp, 1f, size);
            if (oldTiles != null) foreach (var old in oldTiles) if (old != null) old.SetActive(false);
            tileMesh.transform.localPosition = Vector3.zero;
            tileMesh.transform.localRotation = Quaternion.identity;
            tileMesh.transform.localScale = Vector3.one;
            if (generatedTiles == null)
                generatedTiles = new Mesh { name = "Weighted bridge checkerboard", hideFlags = HideFlags.DontSave };
            generatedTiles.Clear();
            var vertices = new Vector3[tilesX * tilesZ * 4];
            var light = new List<int>(); var dark = new List<int>();
            float gap = Mathf.Min(.025f, tileSize * .05f), y = size.y * .5f + .0285f;
            for (int x = 0; x < tilesX; x++) for (int z = 0; z < tilesZ; z++)
            {
                int i = (x * tilesZ + z) * 4;
                float loX = -size.x * .5f + x * tileSize + gap * .5f, hiX = loX + tileSize - gap;
                float loZ = -size.z * .5f + z * tileSize + gap * .5f, hiZ = loZ + tileSize - gap;
                vertices[i] = new Vector3(loX, y, loZ); vertices[i+1] = new Vector3(loX, y, hiZ);
                vertices[i+2] = new Vector3(hiX, y, hiZ); vertices[i+3] = new Vector3(hiX, y, loZ);
                var triangles = (x + z) % 2 == 0 ? light : dark;
                triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2);
                triangles.Add(i); triangles.Add(i+2); triangles.Add(i+3);
            }
            generatedTiles.vertices = vertices; generatedTiles.subMeshCount = 2;
            generatedTiles.SetTriangles(light, 0); generatedTiles.SetTriangles(dark, 1);
            generatedTiles.RecalculateNormals(); generatedTiles.RecalculateBounds();
            tileMesh.sharedMesh = generatedTiles;
        }
        static void ResizeStripe(Transform stripe, float side, Vector3 size)
        {
            if (stripe == null) return;
            stripe.localPosition = new Vector3(side * (size.x * .5f - .04f), size.y * .5f + .03f, 0f);
            stripe.localScale = new Vector3(.09f, .03f, size.z);
        }
        static void ResizeRamp(Transform ramp, float side, Vector3 size)
        {
            if (ramp == null) return;
            var position = ramp.localPosition; position.z = side * (size.z * .5f + 1.2f); ramp.localPosition = position;
            var scale = ramp.localScale; scale.x = size.x; ramp.localScale = scale;
        }
        void OnDisable() { instances.Remove(this); riders.Clear(); }
        public float CalculateTarget()
        {
            riders.Clear();
            Vector3 deckSize = DeckSize;
            var scale = Deck.lossyScale;
            var half = Vector3.Scale(new Vector3(deckSize.x * .5f, 1.15f, deckSize.z * .5f), new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(Deck.TransformPoint(new Vector3(0, deckSize.y * .5f + 1, 0)), half,
                overlaps, Deck.rotation, ~0, QueryTriggerInteraction.Ignore);
            float weight = 0;
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(overlaps[i]);
                if (motor == null || !motor.CanReceiveObstacle || !motor.Grounded || !riders.Add(motor)) continue;
                var feet = Deck.InverseTransformPoint(motor.Position);
                if (feet.y < deckSize.y * .5f - .2f || feet.y > deckSize.y * .5f + .6f ||
                    Mathf.Abs(feet.x) > deckSize.x * .5f || Mathf.Abs(feet.z) > deckSize.z * .5f)
                { riders.Remove(motor); continue; }
                weight += Mathf.Clamp(feet.x / Mathf.Max(.1f, deckSize.x * .5f), -1, 1);
            }
            RiderCount = riders.Count;
            return Mathf.Clamp(-weight * degreesPerPlayer, -maximumTilt, maximumTilt);
        }
        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            if (body == null) Awake();
            float time = context != null ? context.Elapsed : Time.time;
            if (time < previousTime || (context != null && !context.Running)) { Angle = 0; remoteAngle = 0; }
            else Angle = Mathf.MoveTowards(Angle, context == null || context.Authority ? CalculateTarget() : remoteAngle, tiltSpeed * Time.fixedDeltaTime);
            previousTime = time;
            Angle = Mathf.Clamp(Angle, -maximumTilt, maximumTilt);
            var localRotation = restRotation * Quaternion.AngleAxis(Angle, Vector3.forward);
            body.MoveRotation(Deck.parent != null ? Deck.parent.rotation * localRotation : localRotation);
        }
        public static void Capture(Scene scene, out uint[] ids, out float[] angles)
        {
            int count = 0;
            foreach (var bridge in instances) if (bridge != null && bridge.gameObject.scene == scene) count++;
            // A separate bridge-state message would be needed for levels exceeding this budget.
            if (count > 64) throw new System.InvalidOperationException("At most 64 weighted bridges per online scene are supported.");
            ids = new uint[count]; angles = new float[count]; int i = 0;
            foreach (var bridge in instances)
                if (bridge != null && bridge.gameObject.scene == scene) { ids[i] = bridge.NetworkId; angles[i++] = bridge.Angle; }
        }
        public static void ApplyRemote(Scene scene, uint[] ids, float[] angles)
        {
            foreach (var bridge in instances)
            {
                if (bridge == null || bridge.gameObject.scene != scene || bridge.context == null || bridge.context.Authority) continue;
                for (int i = 0; i < ids.Length; i++) if (ids[i] == bridge.NetworkId)
                { bridge.remoteAngle = Mathf.Clamp(angles[i], -bridge.maximumTilt, bridge.maximumTilt); break; }
            }
        }
    }
}
