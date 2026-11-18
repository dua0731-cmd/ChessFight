using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Five route clues map to five keyboard rows. The final keyboard never reveals the answers.
    [DefaultExecutionOrder(-120)]
    public sealed class TypewriterStairs : RoyalLibraryObstacle
    {
        [SerializeField] ObstacleContext context;
        [SerializeField] Transform[] clueFaces;
        [SerializeField] TypewriterKey[] keys;
        [SerializeField] BoxCollider keyboardWell;
        TypewriterMemory layout;
        TextMesh[] keyText, clueText;
        [SerializeField] BoxCollider[] keyAreas;
        [SerializeField] Transform[] keyFaces;
        [SerializeField] Renderer[] keyVisuals;
        [SerializeField] Transform documentFace;
        [SerializeField] Transform nameplateFace;
        [SerializeField, Min(0)] float backwardSpeed = 12;
        [SerializeField, Min(0)] float upwardSpeed = 6;
        [SerializeField, Min(0)] float knockdownSeconds = .7f;
        [SerializeField, Min(.1f)] float hitCooldown = 1;
        [SerializeField, Min(.05f)] float flashSeconds = .3f;
        readonly Collider[] overlaps = new Collider[128];
        readonly HashSet<ImportedObstacleActor> visited = new HashSet<ImportedObstacleActor>();
        readonly Dictionary<ImportedObstacleActor, float> lastHit = new Dictionary<ImportedObstacleActor, float>();
        float[] flashUntil;
        MaterialPropertyBlock tint;
        // seoungtest R689 (the course as a Pawn Rush round section): the letters come from the section's seed, so every PC
        // of a match prints the same clues and keys (offline: a fresh random layout each round, as before). The host judges
        // the keys and sends which are pressed and which flash red; another PC shows what it is handed (ApplyRemote).
        int seed;
        bool seeded;
        uint pressedMask;
        public uint PressedMask => pressedMask;
        public uint FlashMask
        {
            get
            {
                uint mask = 0;
                if (flashUntil != null)
                    for (int i = 0; i < flashUntil.Length && i < 32; i++) if (Time.time < flashUntil[i]) mask |= 1u << i;
                return mask;
            }
        }

        public bool IsSafe(int index) => layout != null && layout.Safe(index);
        public string Sequence => layout != null ? layout.Sequence : "";
        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            if (keyAreas == null || keyAreas.Length != TypewriterMemory.KeyCount || keys == null || keys.Length != TypewriterMemory.KeyCount ||
                keyFaces == null || keyFaces.Length != TypewriterMemory.KeyCount || keyVisuals == null || keyVisuals.Length != TypewriterMemory.KeyCount ||
                clueFaces == null || clueFaces.Length != 5 || keyboardWell == null)
            { Debug.LogError("Typewriter requires 23 staggered keys and five route clues.", this); enabled = false; return; }
            for (int i = 0; i < TypewriterMemory.KeyCount; i++)
                if (keys[i] == null || keyAreas[i] == null || keyFaces[i] == null || keyVisuals[i] == null)
                { Debug.LogError("Typewriter has an unassigned key.", this); enabled = false; return; }
            foreach (var clue in clueFaces) if (clue == null)
            { Debug.LogError("Typewriter has an unassigned clue.", this); enabled = false; return; }
            flashUntil = new float[TypewriterMemory.KeyCount]; tint = new MaterialPropertyBlock();
            keyText = new TextMesh[TypewriterMemory.KeyCount]; clueText = new TextMesh[5];
            for (int i = 0; i < TypewriterMemory.KeyCount; i++) keyText[i] = Label(keyFaces[i], "W", 1.7f, 1.5f, Color.black);
            for (int i = 0; i < 5; i++) clueText[i] = Label(clueFaces[i], "5\nW", 2.3f, 2.3f, Color.black);
            if (documentFace != null)
                Label(documentFace, "1 → 2 → 3 → 4 → 5\n기억한 글자로 올라가 안으로 들어가세요", 8, 1.2f, Color.black);
            if (nameplateFace != null) Label(nameplateFace, "MEMORY TYPEWRITER", 9, .65f, Color.white);
            NewRound();
        }
        // A round section's layout: the same letters on every PC for this seed.
        public void UseSeed(int sectionSeed)
        {
            if (seeded && seed == sectionSeed) return;
            seeded = true; seed = sectionSeed;
            NewRound();
        }
        public void NewRound()
        {
            if (keyText == null) return;
            string previous = layout == null ? null : layout.Sequence;
            if (seeded) layout = TypewriterMemory.Generate(new System.Random(unchecked((int)SeededSchedule.Hash(seed, 0x7E1))));
            else
                do { layout = TypewriterMemory.Generate(new System.Random(UnityEngine.Random.Range(0, int.MaxValue))); }
                while (layout.Sequence == previous);
            pressedMask = 0;
            for (int i = 0; i < TypewriterMemory.KeyCount; i++) { keyText[i].text = layout.Keys[i].ToString(); keys[i].ResetKey(); }
            for (int i = 0; i < 5; i++) clueText[i].text = (i + 1) + "\n" + layout.Sequence[i];
            lastHit.Clear(); visited.Clear(); Array.Clear(flashUntil, 0, flashUntil.Length);
        }
        static TextMesh Label(Transform parent, string value, float width, float height, Color color)
        {
            var go = new GameObject("Printed lettering"); go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMesh>(); text.text = value; text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.font = RuntimePanels.KoreanFont; text.fontSize = 64; text.characterSize = height; text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            if (text.font != null) renderer.sharedMaterial = text.font.material;
            Vector3 size = renderer.localBounds.size;
            float scale = Mathf.Min(1, Mathf.Min(width / Mathf.Max(.001f, size.x), height / Mathf.Max(.001f, size.y)));
            go.transform.localScale = Vector3.one * scale;
            return text;
        }
        void FixedUpdate()
        {
            if (keyAreas == null || flashUntil == null || !ObstacleClock.Authority || (context != null && (!context.Running || !context.Authority))) return;
            float now = Time.time;
            visited.Clear();
            pressedMask = 0;
            foreach (var key in keys) key.SetPressed(false);
            for (int i = 0; i < keyAreas.Length; i++)
            {
                var area = keyAreas[i];
                if (!area.gameObject.activeInHierarchy) continue;
                var scale = area.transform.lossyScale;
                int count = Physics.OverlapBoxNonAlloc(area.transform.TransformPoint(area.center),
                    Vector3.Scale(area.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                    overlaps, area.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < count; j++) CheckSupport(ImportedObstacleActor.Find(overlaps[j]), now);
            }
            var well = keyboardWell.bounds;
            int n = Physics.OverlapBoxNonAlloc(well.center, well.extents, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) CheckSupport(ImportedObstacleActor.Find(overlaps[i]), now);
        }
        void CheckSupport(ImportedObstacleActor actor, float now)
        {
            if (actor == null || !actor.CanReceiveObstacle || !visited.Add(actor) ||
                !actor.TryGetGroundCollider(out var support)) return;
            // The actual supporting collider wins, even when the hips project into a gap.
            var key = support.GetComponentInParent<TypewriterKey>();
            if (key != null)
            {
                int index = Array.IndexOf(keys, key);
                if (index < 0) return;
                key.SetPressed(true);
                if (index < 32) pressedMask |= 1u << index;
                if (!IsSafe(index)) Hit(actor, now, index);
                return;
            }
            // Only a grounded contact on this keyboard's bed can count as a gap failure.
            if (support.transform.IsChildOf(keyboardWell.transform) && keyboardWell.bounds.Contains(actor.Position))
                Hit(actor, now, -1);
        }
        void Hit(ImportedObstacleActor actor, float now, int key)
        {
            if (lastHit.TryGetValue(actor, out float previous) && now >= previous && now - previous < Mathf.Max(.1f, hitCooldown)) return;
            lastHit[actor] = now;
            if (key >= 0) flashUntil[key] = now + Mathf.Max(.05f, flashSeconds);
            actor.AddObstacleHit(transform.TransformDirection(Vector3.back) * Mathf.Max(0, backwardSpeed)
                + Vector3.up * Mathf.Max(0, upwardSpeed), Mathf.Max(0, knockdownSeconds));
        }

        // Not the host: the host's pressed keys sink, its red keys flash (renewed with every state it sends).
        public void ApplyRemote(uint pressed, uint flash)
        {
            if (keys == null || flashUntil == null) return;
            pressedMask = pressed;
            float until = Time.time + .25f;
            for (int i = 0; i < keys.Length && i < 32; i++)
            {
                if (keys[i] != null) keys[i].SetPressed((pressed & (1u << i)) != 0);
                if ((flash & (1u << i)) != 0) flashUntil[i] = Mathf.Max(flashUntil[i], until);
            }
        }

        void Update()
        {
            if (flashUntil == null) return;
            for (int i = 0; i < keyVisuals.Length; i++)
            {
                tint.Clear(); if (Time.time < flashUntil[i]) LitMaterials.SetColor(tint, Color.red);
                keyVisuals[i].SetPropertyBlock(tint);
            }
        }
        void OnDisable()
        {
            lastHit.Clear(); visited.Clear();
            if (flashUntil != null) Array.Clear(flashUntil, 0, flashUntil.Length);
            if (tint == null || keyVisuals == null) return;
            tint.Clear(); foreach (var key in keyVisuals) if (key != null) key.SetPropertyBlock(tint);
        }
    }
}
