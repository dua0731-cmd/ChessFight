using ChessFight.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace ChessFight.ProtectKing
{
    // StateDrivenMover: PhysX contact forces rotate a hinged deck. Remote peers follow the host angle.
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
        [SerializeField, Min(1f), Tooltip("Mass of the deck in kilograms. Lower values react more strongly to a rider's weight.")] float deckMass = 40f;
        [SerializeField, Min(0f), Tooltip("Spring torque returning the unloaded deck to level. Zero disables automatic return.")] float returnSpring = 150f;
        [SerializeField, Min(0f), Tooltip("Damping of the return spring; increase it to reduce oscillation.")] float returnDamper = 120f;
        [SerializeField, Min(1f), FormerlySerializedAs("tiltSpeed"), Tooltip("Angular speed cap in degrees per second.")] float maximumAngularSpeed = 30f;
        [SerializeField, HideInInspector] Collider centralSupport;
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
        bool Simulating => body != null && isActiveAndEnabled && (context == null || (context.Running && context.Authority));
        Vector3 AngularVelocity => Simulating ? body.angularVelocity : remoteAngularVelocity;
        public Quaternion DeltaRotation => !isActiveAndEnabled || (context != null && !context.Running) || AngularVelocity.sqrMagnitude < .000001f
            ? Quaternion.identity : Quaternion.AngleAxis(AngularVelocity.magnitude * Time.fixedDeltaTime * Mathf.Rad2Deg, AngularVelocity.normalized);
        public Vector3 PointVelocity(Vector3 point)
        {
            if (!Application.isPlaying || body == null || !isActiveAndEnabled || (context != null && !context.Running)) return Vector3.zero;
            Vector3 normal = body.rotation * Vector3.up, offset = point - body.position;
            offset -= normal * (Vector3.Dot(offset, normal) - DeckSize.y * .5f);
            return Simulating ? body.GetPointVelocity(body.position + offset) : Vector3.Cross(remoteAngularVelocity, offset);
        }
        public float Angle => hinge != null && Simulating ? hinge.angle : remotePoseAngle;
        public uint NetworkId { get; private set; }
        static readonly List<WeightedBridge> instances = new List<WeightedBridge>();
        Rigidbody body;
        Quaternion restRotation;
        HingeJoint hinge;
        bool physicsDirty = true;
        float remoteAngle, remotePoseAngle;
        Vector3 remoteAngularVelocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { instances.Clear(); }
        void Awake()
        {
            if (!Application.isPlaying) return;
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            body = deckBody != null ? deckBody : GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.solverIterations = 24; body.solverVelocityIterations = 12;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            restRotation = Deck.localRotation;
            WeightedBridgeSurface.Bind(body, this);
            hinge = body.GetComponent<HingeJoint>();
            if (hinge == null) hinge = body.gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = null;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector3.zero; hinge.axis = Vector3.forward;
            hinge.connectedAnchor = body.position;
            hinge.useMotor = false;
            body.centerOfMass = Vector3.zero;
            var deckCollider = solidDeck != null ? solidDeck.GetComponent<Collider>() : null;
            // The fixed pivot housing overlaps the deck centre; it is not a load on the board.
            if (centralSupport != null && deckCollider != null) Physics.IgnoreCollision(centralSupport, deckCollider, true);
            ApplyPhysicsSettings();
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
            geometryDirty = true; physicsDirty = true;
        }
        void Update() { if (geometryDirty) RefreshGeometry(); }
        void ApplyPhysicsSettings()
        {
            if (body == null || hinge == null) return;
            body.mass = Mathf.Max(1f, deckMass);
            body.maxAngularVelocity = Mathf.Max(1f, maximumAngularSpeed) * Mathf.Deg2Rad;
            var limits = hinge.limits; limits.min = -maximumTilt; limits.max = maximumTilt;
            limits.bounciness = 0f; limits.contactDistance = .5f;
            hinge.limits = limits; hinge.useLimits = true;
            var spring = hinge.spring; spring.spring = Mathf.Max(0f, returnSpring);
            spring.damper = Mathf.Max(0f, returnDamper); spring.targetPosition = 0f;
            hinge.spring = spring; hinge.useSpring = returnSpring > 0f;
            body.isKinematic = !Simulating;
            body.ResetInertiaTensor();
            if (!body.isKinematic) body.WakeUp();
            physicsDirty = false;
        }
        void OnDestroy()
        {
            if (generatedTiles == null) return;
            if (Application.isPlaying) Destroy(generatedTiles); else DestroyImmediate(generatedTiles);
        }
        void RefreshGeometry()
        {
            geometryDirty = false; physicsDirty = true;
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
        void OnDisable()
        {
            instances.Remove(this); remoteAngularVelocity = Vector3.zero;
            if (body != null) body.isKinematic = true;
            physicsDirty = true;
        }
        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            if (body == null) Awake();
            bool simulate = Simulating;
            if (physicsDirty || body.isKinematic == simulate) ApplyPhysicsSettings();
            if (simulate || (context != null && !context.Running))
            {
                remoteAngularVelocity = Vector3.zero;
                return; // Contact forces and the hinge own the authoritative pose.
            }
            float next = Mathf.MoveTowards(remotePoseAngle, remoteAngle, maximumAngularSpeed * Time.fixedDeltaTime);
            Vector3 axis = Deck.parent != null ? Deck.parent.rotation * restRotation * Vector3.forward : restRotation * Vector3.forward;
            remoteAngularVelocity = axis * ((next - remotePoseAngle) * Mathf.Deg2Rad / Time.fixedDeltaTime);
            remotePoseAngle = next;
            Quaternion local = restRotation * Quaternion.AngleAxis(next, Vector3.forward);
            body.MoveRotation(Deck.parent != null ? Deck.parent.rotation * local : local);
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
