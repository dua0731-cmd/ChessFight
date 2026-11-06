using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // R459 (승규 11-05 "남쪽 계단 오르는게 3초 중후반 대라 북쪽 계단도 그에 맞게 스피드 업 장치를 설치 해"): the north
    // return route (north_stairs.py) is about three times as long as the south one - the north door's landing, the same
    // 32 steps, the top landing, the gallery and the old upper landing to the start floor's door, some 36 m against 12.
    // R461 (승규 11-05 "날아가 버려 그리고 조작감이 힘들고"): R459 carried the pawn along the route as a moving walkway
    // (IMovingSurface, 7 m/s): over the stair's top and at the corners that extra speed threw it off, and steering
    // fought the carry. Now the route's walking colliders are a speed lane (ISpeedSurface): on them a pawn simply runs
    // faster - at least TopSpeed, its own sprint is 9.6 - and steers, turns and stops as it always does. Up the steps
    // 10 (a slope takes about two thirds of it, near a sprint up the south stair), on the flat 12: about 1.8 s up the
    // 12 m flight and 2 s along the 24 m of landing and gallery, 3.8 s door to door. The lane is drawn on the floor by
    // LibraryStairBoostFx.
    public sealed class LibraryStairBoost : MonoBehaviour, ISpeedSurface
    {
        public const float StairSpeed = 10f, FlatSpeed = 12f;

        [SerializeField] float topSpeed = FlatSpeed;

        public float TopSpeed => topSpeed;

        public void Configure(float speed) => topSpeed = speed;
    }
}
