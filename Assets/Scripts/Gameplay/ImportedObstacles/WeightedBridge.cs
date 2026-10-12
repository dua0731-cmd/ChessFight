using ChessFight.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.ProtectKing
{
    // StateDrivenMover: tilted by the weight on its deck, not by ObstacleClock. Offline only
    // until the host sends its angle (Docs/KingRush/MAP_IMPORT.md).
    [ExecuteAlways, DefaultExecutionOrder(-100)]
    public sealed class WeightedBridge : Obstacle
    {
        [SerializeField] ObstacleContext context;
        [SerializeField, Range(0f, 80f), Tooltip("Maximum downward tilt in degrees on either side. Zero keeps the deck level.")]
        float maximumTilt = 24f;
        [SerializeField, Range(1, 64), Tooltip("Number of tiles across the tilting direction.")] int tilesX = 4;
        [SerializeField, Range(1, 64), Tooltip("Number of tiles along the bridge.")] int tilesZ = 8;
        [SerializeField, Min(.1f), Tooltip("Side length of one square tile in metres, before root scale.")] float tileSize = 1.5f;
        [SerializeField, Min(.05f)] float deckThickness = .4f;
        [SerializeField, Range(0f, .3f), Tooltip("Brief contact tolerance in seconds. Jumping or leaving the deck cancels it immediately.")] float supportGrace = .12f;
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
        public Vector3 Size => DeckSize;
        public Quaternion DeltaRotation { get; private set; } = Quaternion.identity;
        float angularSpeed;
        public Vector3 PointVelocity(Vector3 point)
        {
            if (!Application.isPlaying || body == null || !isActiveAndEnabled || (context != null && !context.Running)) return Vector3.zero;
            Vector3 normal = body.rotation * Vector3.up;
            Vector3 offset = point - body.position;
            // Sample the supporting surface, rather than the upright hips above it.
            offset -= normal * (Vector3.Dot(offset, normal) - DeckSize.y * .5f);
            return Vector3.Cross(body.rotation * Vector3.forward * angularSpeed, offset);
        }
        struct SupportSample { public float time; public Vector3 localPoint; }
        readonly Dictionary<ImportedObstacleActor, SupportSample> supportSamples = new Dictionary<ImportedObstacleActor, SupportSample>();
        readonly List<ImportedObstacleActor> expiredSupports = new List<ImportedObstacleActor>();
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
            WeightedBridgeSurface.Bind(body, this);
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
            var position = ramp.localPosition; position.z = side * (size.z * .5f + 1.55f); ramp.localPosition = position;
            var scale = ramp.localScale; scale.x = size.x; ramp.localScale = scale;
        }
        void OnDisable() { instances.Remove(this); riders.Clear(); supportSamples.Clear(); angularSpeed = 0f; DeltaRotation = Quaternion.identity; }
        public float CalculateTarget()
        {
            riders.Clear();
            if (solidDeck == null || body == null) return 0f;
            var support = solidDeck.GetComponent<BoxCollider>();
            if (support == null) return 0f;
            Vector3 size = DeckSize, scale = Deck.lossyScale;
            var half = Vector3.Scale(new Vector3(size.x * .5f + .35f, 2f, size.z * .5f + .35f),
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(Deck.TransformPoint(new Vector3(0f, size.y * .5f + 1.5f, 0f)),
                half, overlaps, body.rotation, ~0, QueryTriggerInteraction.Ignore);
            Vector3 normal = body.rotation * Vector3.up;
            float now = Time.time, weight = 0f;
            for (int i = 0; i < count; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !riders.Add(actor)) continue;
                Vector3 top = Deck.TransformPoint(Vector3.up * (size.y * .5f));
                Vector3 projectedFeet = actor.Position - Vector3.up *
                    (Vector3.Dot(actor.Position - top, normal) / Mathf.Max(.1f, normal.y));
                Vector3 feet = Deck.InverseTransformPoint(projectedFeet);
                bool inBounds = Mathf.Abs(feet.x) <= size.x * .5f + .35f && Mathf.Abs(feet.z) <= size.z * .5f + .35f;
                Vector3 velocity = PointVelocity(actor.Position);
                bool separating = Vector3.Dot(actor.Velocity - velocity, normal) > 2.5f;
                Vector3 localPoint;
                if (!separating && actor.TrySupport(support, normal, velocity, out var contact))
                {
                    localPoint = Deck.InverseTransformPoint(contact);
                    supportSamples[actor] = new SupportSample { time = now, localPoint = localPoint };
                }
                else if (!separating && inBounds && supportSamples.TryGetValue(actor, out var sample) && now - sample.time <= supportGrace)
                    localPoint = sample.localPoint;
                else
                {
                    riders.Remove(actor); supportSamples.Remove(actor); continue;
                }
                // One player at either side reaches Maximum Tilt; it is not capped at the old 12 degrees.
                weight += Mathf.Clamp(localPoint.x / Mathf.Max(.1f, size.x * .5f * .85f), -1f, 1f);
            }
            expiredSupports.Clear();
            foreach (var pair in supportSamples) if (!riders.Contains(pair.Key)) expiredSupports.Add(pair.Key);
            foreach (var actor in expiredSupports) supportSamples.Remove(actor);
            RiderCount = riders.Count;
            return -maximumTilt * Mathf.Clamp(weight, -1f, 1f);
        }
        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            if (body == null) Awake();
            float oldAngle = Angle;
            float time = context != null ? context.Elapsed : Time.time;
            if (time < previousTime || (context != null && !context.Running)) { Angle = 0; remoteAngle = 0; }
            else Angle = Mathf.MoveTowards(Angle, context == null || context.Authority ? CalculateTarget() : remoteAngle, tiltSpeed * Time.fixedDeltaTime);
            previousTime = time;
            angularSpeed = (Angle - oldAngle) * Mathf.Deg2Rad / Time.fixedDeltaTime;
            DeltaRotation = Quaternion.AngleAxis(Angle - oldAngle, Deck.parent != null
                ? Deck.parent.rotation * restRotation * Vector3.forward : restRotation * Vector3.forward);
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
