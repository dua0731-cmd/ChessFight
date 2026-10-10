using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class ReciprocatingPusher : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField] float distance = 4f;
        [SerializeField, Min(.5f)] float period = 5f;
        [SerializeField] bool useLocalTranslationAxes;
        [SerializeField, Min(0)] float strength = 9f;
        [SerializeField, Min(0)] float lift = 2f;
        [SerializeField, Min(.05f)] float hitCooldown = .35f;
        public override float KnockbackForce => strength;
        public override float UpwardForce => lift;
        public override float HitCooldown => hitCooldown;

        protected override Pose Evaluate(double time)
        {
            float wave = Wave(time, period, phase);
            return new Pose(Origin + TranslationAxis(axis, useLocalTranslationAxes) * (distance * wave), RestRotation);
        }
    }
}
