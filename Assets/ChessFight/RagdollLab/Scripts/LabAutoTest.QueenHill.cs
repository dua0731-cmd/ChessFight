using System.Collections;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for the Queen of the Hill mechanics (Docs/GameModes/QueenOfTheHill/MECHANICS_TODO.md),
    /// one per completion criterion, on the [7] test bed (QueenHillTestBed):
    /// M2 the new keys reach the pawn, M1 riding platforms and a moving wall, M3 hits, M4 water and respawn.
    /// </summary>
    public partial class LabAutoTest
    {
        QueenHillTestBed Bed => game.GetComponent<QueenHillTestBed>();

        IEnumerator QueenHill()
        {
            if (Bed == null)
            {
                Report("퀸 오브 더 힐 시험대", false, "시험대가 만들어지지 않음");
                yield break;
            }
            yield return QhInputs();
            yield return QhRide(Bed.Shuttle, QueenHillTestBed.PlatformThickness * 0.5f, Vector3.zero, 10f,
                "탈것: 왕복 발판(2 m/s) 위에 10초 서 있기");
            yield return QhWaitForLiftBottom();
            yield return QhRide(Bed.Lift, QueenHillTestBed.PlatformThickness * 0.5f, Vector3.zero, Bed.Lift.CycleSeconds + 0.5f,
                "탈것: 승강기(3 m/s, 12 m) 한 바퀴 서 있기");
            yield return QhRide(Bed.Disc, QueenHillTestBed.DiscHeight * 0.5f, new Vector3(2.5f, 0f, 0f), 10f,
                "탈것: 회전 원판(20°/초) 가장자리 쪽에 10초 서 있기");
            yield return QhWaitForLiftBottom();
            yield return QhRideJump(Bed.Lift, QueenHillTestBed.PlatformThickness * 0.5f, true, "탈것: 올라가는 승강기에서 점프해도 다시 발판에 내림");
            yield return QhRideJump(Bed.Shuttle, QueenHillTestBed.PlatformThickness * 0.5f, false, "탈것: 달리는 왕복 발판에서 점프해도 제자리에 내림");
            yield return QhSlideWall();
            yield return QhHitStanding();
            yield return QhHitClimbing();
            yield return QhWaterClimbing();
            yield return QhWaterHolding();
        }

        // ------------------------------------------------------------------ M2

        IEnumerator QhInputs()
        {
            var pawn = Spawn(new Vector3(-6f, 0f, 6f), Vector3.forward, "qh-input");
            yield return Sim(0.3f);
            var driver = pawn.GetComponent<RagdollDriver>();
            Vector3 aim = new Vector3(0.2f, -0.3f, 0.93f).normalized;
            // Through the game's own contract where the prefab has it, as the host and bots will.
            void Send(bool ability, bool ability2, bool interact)
            {
                if (driver != null)
                    driver.SetCommand(new CharacterCommand { Ability = ability, Ability2 = ability2, Interact = interact, Sprint = true, Aim = aim });
                else
                    pawn.SetInput(new PawnInput { ability = ability, ability2 = ability2, interact = interact, sprint = true, aim = aim });
            }
            Send(true, false, false);
            yield return Sim(Dt * 2f, () => Send(false, false, false));
            Send(false, true, false);
            yield return Sim(Dt * 2f, () => Send(false, false, false));
            bool heldSeen = false;
            yield return Sim(0.5f, () =>
            {
                Send(false, false, true);
                heldSeen |= pawn.InteractHeld;
            });
            yield return Sim(0.1f, () => Send(false, false, false));
            Send(false, false, true);
            yield return Sim(Dt * 2f);
            float aimError = Vector3.Angle(pawn.Aim, aim);
            bool ok = pawn.AbilityPresses == 1 && pawn.Ability2Presses == 1 && pawn.InteractPresses == 2 && heldSeen
                      && pawn.SprintHeld && aimError < 0.5f;
            Report("M2 입력: 능력·능력2·상호작용·전력질주·조준이 래그돌에 들어감", ok,
                $"경로 {(driver != null ? "ICharacterDriver.SetCommand" : "SetInput(프리팹에 RagdollDriver 없음)")}, "
                + $"능력 {pawn.AbilityPresses}회(기대 1), 능력2 {pawn.Ability2Presses}회(1), 상호작용 누른 횟수 {pawn.InteractPresses}회(2), "
                + $"누르는 중 표시 {heldSeen}, 전력질주 {pawn.SprintHeld}, 조준 오차 {aimError:F2}°");
            pawn.SetInput(default);
            yield return Clear();
        }

        // ------------------------------------------------------------------ M1

        IEnumerator QhWaitForLiftBottom()
        {
            // The lift idles at the bottom for LiftPause; start a ride there so it covers a whole cycle.
            float waited = 0f, limit = Bed.Lift.CycleSeconds + 1f;
            float bottom = QueenHillTestBed.LiftStart.y;
            while (waited < limit && (Bed.Lift.transform.position.y > bottom + 0.01f || Bed.Lift.PointVelocity(Bed.Lift.transform.position).y < -0.01f))
            {
                yield return new WaitForFixedUpdate();
                waited += Dt;
            }
            // Just arrived at the bottom: the pause is still ahead.
            yield return new WaitForFixedUpdate();
        }

        /// <summary>A pawn put on a moving platform must stay where it was put, in the platform's own frame.</summary>
        IEnumerator QhRide(MovingPlatform platform, float halfHeight, Vector3 offset, float seconds, string name)
        {
            Transform pt = platform.transform;
            Vector3 ground = pt.position + pt.rotation * offset + Vector3.up * halfHeight;
            var pawn = Spawn(ground, Vector3.forward, "qh-ride");
            yield return Sim(0.6f);
            Vector3 local0 = Quaternion.Inverse(pt.rotation) * (pawn.Hips.position - pt.position);
            float yaw0 = Yaw(pawn.Facing) - pt.eulerAngles.y;
            float drift = 0f, turnDrift = 0f, peakSpeed = 0f, minAbove = float.MaxValue, maxAbove = float.MinValue, rise = 0f;
            float startY = pt.position.y;
            int falls = pawn.Knockdowns, ridingSteps = 0, steps = 0;
            yield return Sim(seconds, () =>
            {
                pawn.SetInput(default);
                Vector3 local = Quaternion.Inverse(pt.rotation) * (pawn.Hips.position - pt.position);
                drift = Mathf.Max(drift, Flat(local - local0).magnitude);
                float above = pawn.Hips.position.y - (pt.position.y + halfHeight);
                minAbove = Mathf.Min(minAbove, above);
                maxAbove = Mathf.Max(maxAbove, above);
                turnDrift = Mathf.Max(turnDrift, Mathf.Abs(Mathf.DeltaAngle(Yaw(pawn.Facing) - pt.eulerAngles.y, yaw0)));
                peakSpeed = Mathf.Max(peakSpeed, platform.PointVelocity(pawn.Hips.position).magnitude);
                rise = Mathf.Max(rise, pt.position.y - startY);
                if (pawn.Riding) ridingSteps++;
                steps++;
            });
            bool stayed = pawn.Knockdowns == falls && minAbove > 0.1f && maxAbove < 0.6f;
            bool ok = stayed && drift < 0.5f && turnDrift < 25f && ridingSteps > steps * 0.9f;
            Report(name, ok,
                $"발판 기준 최대 밀림 {drift:F2} m (기준 0.5), 발판 위 골반 높이 {minAbove:F2}~{maxAbove:F2} m, 방향 어긋남 최대 {turnDrift:F0}°, "
                + $"발판 최고 속도 {peakSpeed:F1} m/s, 최고 상승 {rise:F1} m, 탈것 판정 {ridingSteps * 100 / Mathf.Max(1, steps)}%, 넘어짐 {pawn.Knockdowns - falls}");
            yield return Clear();
        }

        /// <summary>Jump off a platform in full motion; the pawn must come down on the same platform, near the
        /// spot it left (in the platform's frame), with a real jump's height above it.</summary>
        IEnumerator QhRideJump(MovingPlatform platform, float halfHeight, bool rising, string name)
        {
            Transform pt = platform.transform;
            var pawn = Spawn(pt.position + Vector3.up * halfHeight, Vector3.forward, "qh-jump");
            yield return Sim(0.6f);
            // Jump just as the platform reaches cruising speed, so the whole flight happens before it
            // slows down for the end of its leg: first wait for a stop, then for full speed.
            float waited = 0f, limit = 2f * platform.CycleSeconds + 1f;
            bool stopped = false;
            while (waited < limit)
            {
                Vector3 v = platform.PointVelocity(pt.position);
                if (v.magnitude < 0.5f) stopped = true;
                else if (stopped && v.magnitude >= platform.Speed * 0.95f && (!rising || v.y > 0f)) break;
                pawn.SetInput(default);
                yield return new WaitForFixedUpdate();
                waited += Dt;
            }
            float speedAtJump = platform.PointVelocity(pt.position).magnitude;
            Vector3 local0 = pawn.Hips.position - pt.position;
            pawn.SetInput(new PawnInput { jump = true });
            float apex = 0f, t = 0f, landed = -1f;
            bool left = false;
            yield return Sim(1.6f, () =>
            {
                t += Dt;
                pawn.SetInput(default);
                float above = pawn.Hips.position.y - (pt.position.y + halfHeight) - pawn.standHeight;
                apex = Mathf.Max(apex, above);
                if (!pawn.Grounded) left = true;
                else if (left && landed < 0f) landed = t;
            });
            Vector3 local = pawn.Hips.position - pt.position;
            float miss = Flat(local - local0).magnitude;
            float above1 = pawn.Hips.position.y - (pt.position.y + halfHeight);
            bool ok = left && landed > 0f && miss < 0.8f && above1 > 0.1f && above1 < 0.6f && apex > 0.3f;
            Report(name, ok,
                $"점프 때 발판 속도 {speedAtJump:F1} m/s, 발판 기준 최고 {apex:F2} m, 착지 {Fmt(landed)}, 발판 기준 착지 오차 {miss:F2} m (기준 0.8), "
                + $"착지 후 발판 위 골반 {above1:F2} m");
            yield return Clear();
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;


        /// <summary>Hang on the sliding wall with nothing pressed but grab: it must carry the pawn.</summary>
        IEnumerator QhSlideWall()
        {
            Transform wt = Bed.SlideWall.transform;
            float face = wt.position.z + QueenHillTestBed.SlideWallSize.z * 0.5f;
            var pawn = Spawn(new Vector3(wt.position.x, 0f, face + 0.5f), Vector3.back, "qh-wall");
            yield return Sim(0.4f);
            float t = 0f, started = -1f;
            yield return Sim(1.5f, () =>
            {
                t += Dt;
                pawn.SetInput(new PawnInput { move = started < 0f ? Vector3.back : Vector3.zero, grab = true });
                if (started < 0f && pawn.Climbing) started = t;
            });
            float along0 = pawn.Hips.position.x - wt.position.x;
            float drift = 0f, peak = 0f, wallMoved = 0f, startX = wt.position.x;
            int lost = 0;
            yield return Sim(6f, () =>
            {
                pawn.SetInput(new PawnInput { grab = true });
                if (!pawn.Climbing) lost++;
                drift = Mathf.Max(drift, Mathf.Abs(pawn.Hips.position.x - wt.position.x - along0));
                peak = Mathf.Max(peak, Mathf.Abs(Bed.SlideWall.PointVelocity(wt.position).x));
                wallMoved = Mathf.Max(wallMoved, Mathf.Abs(wt.position.x - startX));
            });
            bool ok = started >= 0f && lost == 0 && drift < 0.4f && wallMoved > 1f;
            Report("탈것: 움직이는 벽에 매달려 있어도 떨어지지 않음", ok,
                $"매달리기 시작 {Fmt(started)}, 6초 중 떨어진 스텝 {lost}, 벽 기준 밀림 최대 {drift:F2} m (기준 0.4), "
                + $"벽 이동 {wallMoved:F1} m · 최고 {peak:F1} m/s, 스테미나 {pawn.Stamina:P0}");
            pawn.SetInput(default);
            yield return Clear();
        }

        // ------------------------------------------------------------------ M3

        IEnumerator QhHitStanding()
        {
            // Knocked backwards (-Z) with room to land: it slides about 6 m on the main floor.
            var pawn = Spawn(new Vector3(-6f, 0f, 6f), Vector3.forward, "qh-hit");
            yield return Sim(0.8f);
            var receiver = pawn.GetComponent<IHitReceiver>();
            Vector3 push = (Vector3.back * 0.87f + Vector3.up * 0.5f).normalized * 6f;
            Vector3 start = pawn.Hips.position;
            if (receiver != null) receiver.ApplyHit(push, 1f, 0f, false);
            else pawn.TakeHit(push, 1f, 0f, false);
            bool downAtOnce = pawn.State == PawnState.Ragdoll;
            float t = 0f, rise = -1f, stand = -1f, flown = 0f;
            yield return Sim(3f, () =>
            {
                t += Dt;
                pawn.SetInput(default);
                flown = Mathf.Max(flown, Flat(pawn.Hips.position - start).magnitude);
                if (rise < 0f && pawn.State == PawnState.GettingUp) rise = t;
                if (stand < 0f && rise >= 0f && pawn.State == PawnState.Active) stand = t;
            });
            bool ok = downAtOnce && rise >= 0.95f && rise <= 1.2f && stand > 0f && flown > 1f && pawn.Grounded;
            Report("M3 피격: 서 있는 폰에 6 m/s·1초 → 날아가 1초 넘어졌다 일어남", ok,
                $"경로 {(receiver != null ? "IHitReceiver" : "TakeHit(프리팹에 RagdollDriver 없음)")}, 즉시 넘어짐 {downAtOnce}, "
                + $"일어나기 시작 {Fmt(rise)} (기준 1.0), 다 일어남 {Fmt(stand)}, 날아간 거리 {flown:F2} m, 마지막 접지 {pawn.Grounded}");
            yield return Clear();
        }

        IEnumerator QhHitClimbing()
        {
            var pawn = Spawn(new Vector3(LabLayout.WallFrontX - 1.2f, 0f, LabLayout.WallZ[2]), Vector3.right, "qh-hit-wall");
            yield return Sim(0.6f);
            float t = 0f, started = -1f;
            yield return Sim(2f, () =>
            {
                t += Dt;
                pawn.SetInput(new PawnInput { move = started < 0f || t - started < 0.8f ? Vector3.right : Vector3.zero, grab = true });
                if (started < 0f && pawn.Climbing) started = t;
            });
            var receiver = pawn.GetComponent<IHitReceiver>();
            void Hit(Vector3 push, float stamina, bool drop)
            {
                if (receiver != null) receiver.ApplyHit(push, 0f, stamina, drop);
                else pawn.TakeHit(push, 0f, stamina, drop);
            }
            float before = pawn.Stamina;
            Hit(Vector3.zero, 2.5f, false);
            float after = pawn.Stamina;
            yield return Sim(0.3f, () => pawn.SetInput(new PawnInput { grab = true }));
            bool stillOn = pawn.Climbing;
            float y0 = pawn.Hips.position.y;
            Hit(Vector3.left * 2f + Vector3.up, 0f, true);
            bool offAtOnce = !pawn.Climbing;
            yield return Sim(0.5f, () => pawn.SetInput(default));
            float fell = y0 - pawn.Hips.position.y;
            float expected = 2.5f / Mathf.Max(0.01f, game.tuning.values.climbStaminaMax);
            bool ok = started >= 0f && Mathf.Abs(before - after - expected) < 0.02f && stillOn && offAtOnce && !pawn.Climbing && fell > 0.2f;
            Report("M3 피격: 벽에서 스테미나 -2.5는 게이지만 줄고, drop이면 벽에서 떨어짐", ok,
                $"매달림 {Fmt(started)}, 게이지 {before:P0} → {after:P0} (기대 -{expected:P0}), 그 뒤에도 매달림 {stillOn}, "
                + $"drop 즉시 놓음 {offAtOnce}, 0.5초에 {fell:F2} m 떨어짐");
            yield return Clear();
        }

        // ------------------------------------------------------------------ M4

        /// <summary>Climb the pool's pillar from the pier, go sideways over the water and down into it, holding
        /// grab all the way through the respawn: the pawn must come back standing on the checkpoint, off the
        /// wall and out of the kinematic climb pose.</summary>
        IEnumerator QhWaterClimbing()
        {
            var bed = Bed;
            int respawns = bed.WaterRespawns;
            var pawn = Spawn(QueenHillTestBed.PierEnd, Vector3.back, "qh-water-wall");
            yield return Sim(0.4f);
            float t = 0f, started = -1f, wet = -1f;
            bool climbingWhenWet = false;
            // Climb on, then sideways off the end of the pier (right of a pawn facing -Z is -X), then down.
            yield return Sim(8f, () =>
            {
                t += Dt;
                Vector3 move = Vector3.back;
                if (started >= 0f) move = t - started < 2.4f ? Vector3.left : Vector3.forward;
                if (wet >= 0f) move = Vector3.zero;   // hands off the keys; grab stays held through the respawn
                pawn.SetInput(new PawnInput { move = move, grab = true });
                if (started < 0f && pawn.Climbing) started = t;
                if (wet < 0f && bed.IsDrowning(pawn))
                {
                    wet = t;
                    climbingWhenWet = pawn.Climbing;
                }
            });
            yield return Sim(0.8f, () => pawn.SetInput(new PawnInput { grab = true }));
            float fromCheckpoint = Flat(pawn.Hips.position - QueenHillTestBed.Checkpoint).magnitude;
            bool clean = !pawn.Climbing && !pawn.Grabbing && !pawn.Hips.isKinematic && pawn.State == PawnState.Active;
            bool ok = started >= 0f && wet >= 0f && climbingWhenWet && bed.WaterRespawns == respawns + 1
                      && fromCheckpoint < 1f && clean && pawn.Grounded;
            Report("M4 물: 벽에 매달린 채 물에 닿아도 2초 뒤 체크포인트에 멀쩡히 섬", ok,
                $"매달림 {Fmt(started)}, 물에 닿음 {Fmt(wet)} (그때 매달린 상태 {climbingWhenWet}), 부활 {bed.WaterRespawns - respawns}회, "
                + $"체크포인트까지 {fromCheckpoint:F2} m, 매달림 {pawn.Climbing} · 잡기 {pawn.Grabbing} · 운동학 {pawn.Hips.isKinematic} · 상태 {pawn.State} · 접지 {pawn.Grounded}");
            pawn.SetInput(default);
            yield return Clear();
        }

        /// <summary>Grab the crate by the pool and walk into the water holding it.</summary>
        IEnumerator QhWaterHolding()
        {
            var bed = Bed;
            int respawns = bed.WaterRespawns;
            int logFrom = bed.WaterLog.Count;
            var crate = bed.Crate;
            crate.position = QueenHillTestBed.CrateSpot;
            crate.rotation = Quaternion.identity;
            crate.linearVelocity = crate.angularVelocity = Vector3.zero;
            var pawn = Spawn(QueenHillTestBed.CrateSpot + new Vector3(0f, -0.25f, 0.75f), Vector3.back, "qh-water-grab");
            yield return Sim(0.5f);
            float t = 0f, grabbed = -1f, wet = -1f, kick = 0f;
            int mostGrips = 0;
            bool holdingWhenWet = false, back = false;
            yield return Sim(6f, () =>
            {
                t += Dt;
                Vector3 move = grabbed < 0f ? Vector3.back * 0.3f : Vector3.back;
                pawn.SetInput(new PawnInput { move = wet < 0f ? move : Vector3.zero, grab = true });
                if (grabbed < 0f && pawn.Grabbing) grabbed = t;
                if (wet < 0f && bed.IsDrowning(pawn))
                {
                    wet = t;
                    holdingWhenWet = pawn.Grabbing;
                }
                // One grip joint per hand at most: a grip left behind by a knockdown yanked the pawn
                // back into the water right after its respawn.
                mostGrips = Mathf.Max(mostGrips, pawn.GetComponentsInChildren<FixedJoint>(true).Length);
                if (!back && bed.WaterRespawns > respawns)
                {
                    back = true;
                    kick = pawn.Hips.linearVelocity.magnitude;   // one step after the respawn
                }
            });
            yield return Sim(0.8f, () => pawn.SetInput(new PawnInput { grab = true }));
            float fromCheckpoint = Flat(pawn.Hips.position - QueenHillTestBed.Checkpoint).magnitude;
            bool heldCrate = pawn.handL.HeldBody == crate || pawn.handR.HeldBody == crate;
            bool ok = grabbed >= 0f && wet >= 0f && holdingWhenWet && bed.WaterRespawns == respawns + 1 && mostGrips <= 2
                      && kick < 3f && fromCheckpoint < 1f && !heldCrate && pawn.State == PawnState.Active && pawn.Grounded;
            Report("M4 물: 무언가를 잡은 채 물에 빠져도 2초 뒤 체크포인트에 멀쩡히 섬", ok,
                $"상자 잡음 {Fmt(grabbed)}, 물에 닿음 {Fmt(wet)} (그때 잡고 있음 {holdingWhenWet}), 부활 {bed.WaterRespawns - respawns}회, "
                + $"손 관절 최대 {mostGrips}개(2 이하), 부활 직후 속도 {kick:F2} m/s, "
                + $"체크포인트까지 {fromCheckpoint:F2} m, 부활 뒤 상자를 쥠 {heldCrate}, 상태 {pawn.State}, 접지 {pawn.Grounded}");
            Info("물 기록", string.Join(" / ", bed.WaterLog.GetRange(logFrom, bed.WaterLog.Count - logFrom)));
            pawn.SetInput(default);
            yield return Clear();
        }
    }
}
