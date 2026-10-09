using System;
using UnityEngine;

namespace ChessFight.Gameplay.PawnRush
{
    public enum SectionKind { [InspectorName("장애물")] Obstacle, [InspectorName("미션협동")] Mission }

    // One authored, reusable course section. Connectors are ground-level poses.
    public sealed class PawnRushSection : MonoBehaviour
    {
        public string sectionId;
        public SectionKind kind;
        public Transform entrance;
        public Transform exit;
        public Bounds authoringBounds = new Bounds(new Vector3(0, 3, 20), new Vector3(20, 6, 40));

        public void ValidateDefinition()
        {
            if (string.IsNullOrWhiteSpace(sectionId)) throw new InvalidOperationException(name + ": Section ID가 필요합니다.");
            if (entrance == null || exit == null || entrance == exit || entrance.parent != transform || exit.parent != transform)
                throw new InvalidOperationException(name + ": Entrance/Exit를 루트의 직계 자식으로 지정하세요.");
            if ((transform.localScale - Vector3.one).sqrMagnitude > .00001f)
                throw new InvalidOperationException(name + ": 구역 루트 Scale은 (1,1,1)이어야 합니다.");
            Vector3 span = exit.localPosition - entrance.localPosition;
            if (span.z < 4 || Mathf.Abs(span.x) > .001f || Mathf.Abs(span.y) > .001f ||
                Quaternion.Angle(entrance.localRotation, Quaternion.identity) > .01f || Quaternion.Angle(exit.localRotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException(name + ": 입구와 출구는 같은 X/Y, +Z 방향이어야 합니다. 길이는 4m 이상입니다.");
            var mission = GetComponent<TeamMission>();
            if (kind == SectionKind.Mission && mission == null)
                throw new InvalidOperationException(name + ": 미션 구역에는 TeamMission이 필요합니다.");
            if (kind == SectionKind.Obstacle && mission != null)
                throw new InvalidOperationException(name + ": 장애물 구역에는 TeamMission을 두지 않습니다.");
            if (mission != null) mission.ValidateDefinition();
        }

        public void AlignTo(Pose target)
        {
            transform.SetPositionAndRotation(target.position - target.rotation * entrance.localPosition, target.rotation);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = kind == SectionKind.Obstacle ? new Color(.2f, .75f, 1) : new Color(1, .7f, .15f);
            Gizmos.DrawWireCube(authoringBounds.center, authoringBounds.size);
            Gizmos.matrix = Matrix4x4.identity;
            if (entrance != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(entrance.position, .7f); }
            if (exit != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(exit.position, .7f); }
        }
    }
}
