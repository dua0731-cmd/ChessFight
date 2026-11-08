using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // The printed order is the source of truth. Any letter in it is safe; order is not enforced.
    public sealed class TypewriterStairs : Obstacle
    {
        [SerializeField] ObstacleContext context;
        [SerializeField] string printedWord = "CHECK";
        [SerializeField] string keyLetters = "BCFNQSHRXEVPCBNRQKWZ";
        [SerializeField] BoxCollider[] keyAreas;
        [SerializeField] Transform[] keyFaces;
        [SerializeField] Renderer[] keyVisuals;
        [SerializeField] Transform documentFace;
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

        public bool IsSafe(char letter) => char.IsLetter(letter) && !string.IsNullOrEmpty(printedWord)
            && printedWord.IndexOf(char.ToUpperInvariant(letter).ToString(), StringComparison.OrdinalIgnoreCase) >= 0;

        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            if (string.IsNullOrWhiteSpace(printedWord) || keyAreas == null || keyLetters == null || keyLetters.Length != keyAreas.Length ||
                keyFaces == null || keyFaces.Length != keyAreas.Length || keyVisuals == null || keyVisuals.Length != keyAreas.Length)
            { Debug.LogError("Typewriter stairs require matching letters, areas, labels and key visuals.", this); enabled = false; return; }
            for (int i = 0; i < keyAreas.Length; i++)
                if (keyAreas[i] == null || keyFaces[i] == null || keyVisuals[i] == null)
                { Debug.LogError("Typewriter stairs have an unassigned key.", this); enabled = false; return; }
            flashUntil = new float[keyAreas.Length]; tint = new MaterialPropertyBlock();
            for (int i = 0; i < keyFaces.Length; i++)
                Label(keyFaces[i], char.ToUpperInvariant(keyLetters[i]).ToString(), 2.2f, 1.1f, Color.black);
            if (documentFace != null)
                Label(documentFace, "작전 명령서\n" + printedWord.ToUpperInvariant() + "\n이 글자만 밟으시오\n다른 키는 뒤로 튕겨냅니다", 8, 2.6f, Color.black);
        }
        static void Label(Transform parent, string value, float width, float height, Color color)
        {
            var go = new GameObject("Printed lettering"); go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMesh>(); text.text = value; text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.font = RuntimePanels.KoreanFont; text.fontSize = 64; text.characterSize = height; text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            if (text.font != null) renderer.sharedMaterial = text.font.material;
            Vector3 size = renderer.localBounds.size;
            float scale = Mathf.Min(1, Mathf.Min(width / Mathf.Max(.001f, size.x), height / Mathf.Max(.001f, size.y)));
            go.transform.localScale = Vector3.one * scale;
        }
        void FixedUpdate()
        {
            if (keyAreas == null || flashUntil == null || (context != null && (!context.Running || !context.Authority))) return;
            float now = Time.time;
            visited.Clear();
            for (int i = 0; i < keyAreas.Length; i++)
            {
                if (IsSafe(keyLetters[i])) continue;
                var area = keyAreas[i];
                if (!area.gameObject.activeInHierarchy) continue;
                var scale = area.transform.lossyScale;
                int count = Physics.OverlapBoxNonAlloc(area.transform.TransformPoint(area.center),
                    Vector3.Scale(area.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                    overlaps, area.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < count; j++)
                {
                    var actor = ImportedObstacleActor.Find(overlaps[j]);
                    if (actor == null || !actor.CanReceiveObstacle || !actor.Grounded) continue;
                    Vector3 foot = area.transform.InverseTransformPoint(actor.Position) - area.center;
                    Vector3 half = area.size * .5f;
                    // A reaching hand, an airborne crossing or a pawn below the step cannot press a key.
                    if (Mathf.Abs(foot.x) > half.x || Mathf.Abs(foot.z) > half.z || Mathf.Abs(foot.y) > half.y || !visited.Add(actor)) continue;
                    if (lastHit.TryGetValue(actor, out float previous) && now >= previous && now - previous < Mathf.Max(.1f, hitCooldown)) continue;
                    lastHit[actor] = now; flashUntil[i] = now + Mathf.Max(.05f, flashSeconds);
                    actor.AddObstacleHit(transform.TransformDirection(Vector3.back) * Mathf.Max(0, backwardSpeed)
                        + Vector3.up * Mathf.Max(0, upwardSpeed), Mathf.Max(0, knockdownSeconds));
                }
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
