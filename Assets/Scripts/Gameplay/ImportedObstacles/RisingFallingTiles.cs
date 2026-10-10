using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class RisingFallingTiles : ObstacleMotion
    {
        [SerializeField] Vector3 axis = Vector3.right;
        [SerializeField, Tooltip("Offset in cycles.")] float phase;
        [SerializeField] float distance = 4f;
        [SerializeField, Min(.5f)] float period = 5f;
        [SerializeField] bool useLocalTranslationAxes;

        protected override Pose Evaluate(double time)
        {
            float wave = Wave(time, period, phase);
            return new Pose(Origin + TranslationAxis(axis, useLocalTranslationAxes) * (distance * wave), RestRotation);
        }
    }
}
