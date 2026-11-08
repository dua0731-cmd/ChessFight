using UnityEngine;

namespace ChessFight.ProtectKing
{
    public enum MotionKind { Translate, Rotate, Gate }

    // Generic authored course scenery. The fifteen named hazards keep their own tuning classes.
    public sealed class CourseObstacleMotion : ObstacleMotion
    {
        [SerializeField] public MotionKind kind;
        [SerializeField] public Vector3 axis = Vector3.right;
        [SerializeField] public float distance = 4, period = 5, phase, degreesPerSecond = 60;
        [SerializeField] public bool shove = true, useLocalTranslationAxes;

        protected override Pose Evaluate(double time)
        {
            var unit = TranslationAxis(axis, kind != MotionKind.Rotate && useLocalTranslationAxes);
            if (kind == MotionKind.Rotate)
                return new Pose(Origin, RestRotation * Quaternion.AngleAxis((float)((time * degreesPerSecond + phase * 360) % 360), unit));
            float wave = Wave(time, period, phase);
            float fraction = kind == MotionKind.Gate ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.35f, .35f, wave)) : wave;
            return new Pose(Origin + unit * (distance * fraction), RestRotation);
        }
    }
}
