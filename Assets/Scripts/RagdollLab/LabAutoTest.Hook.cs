using System;
using System.Collections;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for the pawn's grappling hook (Queen of the Hill M5) on the [7f] hook range:
    /// the gauge (a longer hold throws further), sticking in walls, tops and ceilings, being reeled up
    /// 20 m onto the tower, hanging at a wall until the right button takes hold, the retry after a miss,
    /// en passant, and the hook's state reaching a network puppet.
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator HookChecks()
        {
            yield return HookGauge();
            yield return HookSurfaces();
            yield return HookTower();
            yield return HookWall();
            yield return HookMiss();
            yield return HookEnPassant();
            yield return HookNetwork();
        }

        static void HookInput(RagdollPawn pawn, Vector3 aim, bool ability = false, bool press = false, bool held = false,
                              bool interact = false, bool jump = false, bool grab = false) =>
            pawn.SetInput(new PawnInput
            {
                aim = aim, ability = ability, shove = press, shoveHeld = held, interact = interact, jump = jump, grab = grab,
            });

        /// <summary>E if the hook is away, then the left button down for <paramref name="hold"/> seconds and up.</summary>
        IEnumerator ThrowHook(RagdollPawn pawn, Vector3 aim, float hold, Action perStep = null)
        {
            if (pawn.Hook == HookPhase.None)
            {
                HookInput(pawn, aim, ability: true);
                yield return Sim(Dt);
            }
            HookInput(pawn, aim, press: true, held: true);
            yield return Sim(Dt, perStep);
            yield return Sim(hold, () =>
            {
                HookInput(pawn, aim, held: true);
                perStep?.Invoke();
            });
            HookInput(pawn, aim);
            yield return Sim(Dt, perStep);
        }

        /// <summary>Keep still until the thrown hook has landed (or missed), at most <paramref name="limit"/> s.</summary>
        IEnumerator WaitForHook(RagdollPawn pawn, Vector3 aim, float limit = 3f)
        {
            for (float t = 0f; t < limit && pawn.Hook == HookPhase.Flying; t += Dt)
            {
                HookInput(pawn, aim);
                yield return Sim(Dt);
            }
        }

        /// <summary>Put the hook away (letting go of any rope) and stand still a moment.</summary>
        IEnumerator StowHook(RagdollPawn pawn)
        {
            if (pawn.Hook != HookPhase.None)
            {
                HookInput(pawn, Vector3.zero, ability: true);
                yield return Sim(Dt);
            }
            yield return Sim(0.3f, () => HookInput(pawn, Vector3.zero));
        }

        /// <summary>
        /// The throw a player would make to land best by <paramref name="score"/> (lower is better): the aim
        /// and how many steps to swing (the gauge), searched with the pawn's own flight steps. The gauge is
        /// the distance control, so a spot close behind an edge takes a lighter throw, not a lob.
        /// </summary>
        (Vector3 aim, int steps) SolveHookThrow(RagdollPawn pawn, Vector3 toward, Func<RaycastHit, float> score)
        {
            var p = game.tuning.values;
            Vector3 flat = Flat(toward).normalized;
            Vector3 head = pawn.bodies[(int)BodyId.Head].position;
            int full = Mathf.CeilToInt(p.hookChargeTime / Dt);
            (Vector3, int) best = (flat, full);
            float bestScore = float.MaxValue;
            for (int steps = full / 4; steps <= full; steps += 3)
            {
                float charge = Mathf.Min(1f, steps * Dt / p.hookChargeTime);
                for (float pitch = -20f; pitch <= 80f; pitch += 0.5f)
                {
                    float r = pitch * Mathf.Deg2Rad;
                    Vector3 aim = flat * Mathf.Cos(r) + Vector3.up * Mathf.Sin(r);
                    RagdollPawn.HookLaunch(p, head, aim, flat, charge, out Vector3 pos, out Vector3 vel);
                    for (float t = 0f; t < p.hookMaxFlight; t += Dt)
                    {
                        if (!pawn.HookFlightStep(p, ref pos, ref vel, Dt, out var hit)) continue;
                        // Of two arcs onto the same spot, the flat one (the way a player throws).
                        float s = score(hit) + t * 0.3f;
                        if (s < bestScore)
                        {
                            bestScore = s;
                            best = (aim, steps);
                        }
                        break;
                    }
                }
            }
            return best;
        }

        /// <summary>Swing for exactly <paramref name="steps"/> steps (the gauge) and throw.</summary>
        IEnumerator ThrowHookSteps(RagdollPawn pawn, Vector3 aim, int steps, Action perStep = null) =>
            ThrowHook(pawn, aim, Mathf.Max(0f, (steps - 1.5f) * Dt), perStep);

        (Vector3 aim, int steps) TowerTopThrow(RagdollPawn pawn, float inland) =>
            SolveHookThrow(pawn, Vector3.right, hit =>
                (hit.normal.y > 0.6f && hit.point.y > QueenHillTestBed.HookTowerTop - 0.1f ? 0f : 100f)
                + Mathf.Abs(hit.point.x - (QueenHillTestBed.HookTowerFaceX + inland)));

        // ------------------------------------------------------------------ gauge

        IEnumerator HookGauge()
        {
            // Open floor to the south: a short tap and a full swing, both thrown level.
            var pawn = Spawn(new Vector3(45f, 0f, -17f), Vector3.back, "hook-gauge");
            yield return Sim(0.6f);
            Vector3 aim = Vector3.back;
            bool dove = false, rose = true;
            float last = 0f, peak = 0f;
            void Watch()
            {
                dove |= pawn.Diving;
                if (pawn.Hook == HookPhase.Charging)
                {
                    rose &= pawn.HookCharge >= last - 1e-4f;
                    last = pawn.HookCharge;
                    peak = Mathf.Max(peak, pawn.HookCharge);
                }
            }
            float Distance(Vector3 from) => pawn.Hook == HookPhase.Pulling || pawn.Hook == HookPhase.Stuck
                ? Flat(pawn.HookPoint - from).magnitude : -1f;

            Vector3 start = pawn.Hips.position;
            yield return ThrowHook(pawn, aim, 0.12f, Watch);
            float tapCharge = pawn.HookCharge;
            yield return Sim(1.5f, () => { HookInput(pawn, aim); Watch(); });
            float tap = Distance(start);
            yield return StowHook(pawn);

            pawn.Teleport(new Vector3(45f, pawn.standHeight + 0.02f, -17f), Vector3.back);
            yield return Sim(0.6f, () => HookInput(pawn, Vector3.zero));
            start = pawn.Hips.position;
            last = 0f;
            yield return ThrowHook(pawn, aim, game.tuning.values.hookChargeTime + 0.2f, Watch);
            float fullCharge = pawn.HookCharge;
            yield return Sim(1.5f, () => { HookInput(pawn, aim); Watch(); });
            float full = Distance(start);
            Report("M5 갈고리: E로 꺼내 좌클릭을 누르고 있으면 게이지가 차고, 오래 누를수록 멀리 날아감",
                tap > 0f && full > 2f * tap && full > 15f && fullCharge > 0.99f && tapCharge < 0.2f && rose && !dove,
                $"짧게 누름(게이지 {tapCharge * 100f:F0}%) {tap:F1} m / 가득(게이지 {fullCharge * 100f:F0}%) {full:F1} m (15 m 이상, 짧게의 2배 이상), "
                + $"게이지가 줄지 않고 참 {rose}, 좌클릭에 다이빙 안 함 {!dove}");
            yield return StowHook(pawn);
            yield return Clear();
        }

        // ------------------------------------------------------------------ any face

        IEnumerator HookSurfaces()
        {
            var pawn = Spawn(QueenHillTestBed.HookStart, Vector3.right, "hook-faces");
            yield return Sim(0.6f);
            // The tower's face at about 5 m.
            float wallY = 5f;
            var (aim, steps) = SolveHookThrow(pawn, Vector3.right, hit =>
                (Mathf.Abs(hit.normal.x + 1f) < 0.1f ? 0f : 100f) + Mathf.Abs(hit.point.y - wallY));
            yield return ThrowHookSteps(pawn, aim, steps);
            yield return WaitForHook(pawn, aim);
            bool wall = pawn.Hook == HookPhase.Pulling && pawn.HookNormal.x < -0.9f;
            Vector3 wallAt = pawn.HookPoint;
            yield return StowHook(pawn);

            // The tower's top.
            pawn.Teleport(QueenHillTestBed.HookStart + Vector3.up * (pawn.standHeight + 0.02f), Vector3.right);
            yield return Sim(0.6f, () => HookInput(pawn, Vector3.zero));
            (aim, steps) = TowerTopThrow(pawn, 1.5f);
            yield return ThrowHookSteps(pawn, aim, steps);
            yield return WaitForHook(pawn, aim);
            bool top = (pawn.Hook == HookPhase.Pulling || pawn.Hook == HookPhase.Stuck) && pawn.HookNormal.y > 0.9f;
            Vector3 topAt = pawn.HookPoint;
            yield return StowHook(pawn);

            // The arch's underside, straight up from beneath it.
            Vector3 under = QueenHillTestBed.HookArchCenter;
            pawn.Teleport(new Vector3(under.x, pawn.standHeight + 0.02f, under.z), Vector3.right);
            yield return Sim(0.6f, () => HookInput(pawn, Vector3.zero));
            yield return ThrowHook(pawn, Vector3.up, 0.6f);
            yield return WaitForHook(pawn, Vector3.up);
            yield return Sim(1.5f, () => HookInput(pawn, Vector3.up));
            bool ceiling = pawn.Hook == HookPhase.Pulling && pawn.HookNormal.y < -0.9f;
            Vector3 ceilingAt = pawn.HookPoint;
            float hangHead = pawn.bodies[(int)BodyId.Head].position.y;
            yield return StowHook(pawn);

            Report("M5 갈고리: 어느 면에나 박힘 (탑 벽면 · 탑 윗면 · 아치 천장)", wall && top && ceiling,
                $"벽면 {wall} (높이 {wallAt.y:F1} m), 윗면 {top} (높이 {topAt.y:F1} m, 벽에서 {topAt.x - QueenHillTestBed.HookTowerFaceX:F1} m 안쪽), "
                + $"천장 {ceiling} (높이 {ceilingAt.y:F2} m, 매달린 머리 높이 {hangHead:F2} m)");
            yield return Clear();
        }

        // ------------------------------------------------------------------ onto the tower

        IEnumerator HookTower()
        {
            var pawn = Spawn(QueenHillTestBed.HookStart, Vector3.right, "hook-tower");
            yield return Sim(0.6f);
            var (aim, steps) = TowerTopThrow(pawn, 1.5f);
            float t = 0f, stuckAt = -1f, arrivedAt = -1f, fastest = 0f;
            Vector3 from = Vector3.zero, arrivedPos = Vector3.zero;
            void Track()
            {
                t += Dt;
                if (stuckAt < 0f && pawn.Hook == HookPhase.Pulling)
                {
                    stuckAt = t;
                    from = pawn.Hips.position;
                }
                if (pawn.Hook == HookPhase.Pulling) fastest = Mathf.Max(fastest, pawn.bodies[0].linearVelocity.magnitude);
                if (arrivedAt < 0f && stuckAt >= 0f && pawn.Hook == HookPhase.Stuck)
                {
                    arrivedAt = t;
                    arrivedPos = pawn.Hips.position;
                }
            }
            yield return ThrowHookSteps(pawn, aim, steps, Track);
            yield return Sim(8f, () =>
            {
                HookInput(pawn, aim);
                Track();
            });
            float pull = arrivedAt >= 0f ? arrivedAt - stuckAt : -1f;
            float distance = arrivedAt >= 0f ? Vector3.Distance(from, arrivedPos) : 0f;
            bool standing = pawn.State == PawnState.Active && pawn.Grounded && pawn.Hips.position.y > QueenHillTestBed.HookTowerTop + 0.1f
                            && pawn.Hips.position.x > QueenHillTestBed.HookTowerFaceX + 0.2f;
            Report("M5 갈고리: 박히면 약 6 m/s로 끌려가 10 m 탑 위에 올라섬 (약 20 m를 약 4초)",
                pull > 2.5f && pull < 6f && distance > 18f && standing && pawn.Knockdowns == 0,
                $"끌려간 거리 {distance:F1} m, 걸린 시간 {Fmt(pull)} (평균 {(pull > 0f ? distance / pull : 0f):F1} m/s, 최고 {fastest:F1} m/s), "
                + $"도착 뒤 탑 위에 서 있음 {standing} (골반 높이 {pawn.Hips.position.y:F2} m, 탑 {QueenHillTestBed.HookTowerTop:F0} m), 넘어짐 {pawn.Knockdowns}");
            yield return StowHook(pawn);
            yield return Clear();
        }

        // ------------------------------------------------------------------ a hook in a wall

        IEnumerator HookWall()
        {
            var pawn = Spawn(QueenHillTestBed.HookStart, Vector3.right, "hook-wall");
            yield return Sim(0.6f);
            var (aim, steps) = SolveHookThrow(pawn, Vector3.right, hit =>
                (Mathf.Abs(hit.normal.x + 1f) < 0.1f ? 0f : 100f) + Mathf.Abs(hit.point.y - 6f));
            yield return ThrowHookSteps(pawn, aim, steps);
            yield return Sim(6f, () => HookInput(pawn, aim));
            // Hanging on the rope against the face, off the ground, not climbing yet.
            float gap = QueenHillTestBed.HookTowerFaceX - pawn.Hips.position.x;
            bool hanging = pawn.Hook == HookPhase.Pulling && !pawn.Climbing && pawn.Hips.position.y > 3f && gap < 0.6f;
            float hangY = pawn.Hips.position.y;
            // The right button takes hold of the wall: from here it is an ordinary climb.
            bool climbing = false;
            yield return Sim(0.5f, () =>
            {
                HookInput(pawn, aim, grab: true);
                climbing |= pawn.Climbing;
            });
            float heldY = pawn.Hips.position.y;
            Report("M5 갈고리: 벽면에 박으면 벽 앞에 매달리고, 우클릭으로 벽을 잡아 등반으로 이어짐",
                hanging && climbing && pawn.Climbing && heldY > hangY - 0.3f,
                $"밧줄에 매달림 {hanging} (골반 높이 {hangY:F2} m, 벽까지 {gap:F2} m), 우클릭 뒤 등반 {climbing}, "
                + $"잡은 뒤 높이 {heldY:F2} m (떨어지지 않음)");
            yield return Sim(0.3f, () => HookInput(pawn, Vector3.zero));
            yield return Clear();
        }

        // ------------------------------------------------------------------ a miss

        IEnumerator HookMiss()
        {
            var pawn = Spawn(new Vector3(45f, 0f, -22f), Vector3.back, "hook-miss");
            yield return Sim(0.6f);
            // Straight up at full gauge: still rising or falling back when hookMaxFlight runs out.
            var p = game.tuning.values;
            yield return ThrowHook(pawn, Vector3.up, p.hookChargeTime + 0.1f);
            float t = 0f, missAt = -1f;
            int misses = pawn.HookMisses;
            yield return Sim(p.hookMaxFlight + 0.3f, () =>
            {
                t += Dt;
                HookInput(pawn, Vector3.up);
                if (missAt < 0f && pawn.HookMisses > misses) missAt = t;
            });
            // Too soon: the button does nothing. After the delay: it swings again.
            yield return Sim(p.hookRetryDelay * 0.3f, () => HookInput(pawn, Vector3.up));
            HookInput(pawn, Vector3.up, press: true, held: true);
            yield return Sim(Dt);
            bool tooSoon = pawn.Hook == HookPhase.Charging;
            yield return Sim(0.1f, () => HookInput(pawn, Vector3.up));
            yield return Sim(p.hookRetryDelay * 0.8f, () => HookInput(pawn, Vector3.up));
            HookInput(pawn, Vector3.up, press: true, held: true);
            yield return Sim(Dt);
            bool again = pawn.Hook == HookPhase.Charging;
            Report("M5 갈고리: 아무 데도 안 박히면 빗나감, 1초 뒤에 다시 던질 수 있음",
                missAt > 0f && !tooSoon && again,
                $"빗나감 처리 {Fmt(missAt)} (설정 {p.hookMaxFlight:F1}초), 빗나간 직후 다시 던지기 {(tooSoon ? "됨(틀림)" : "안 됨")}, "
                + $"{p.hookRetryDelay:F1}초 뒤 다시 돌리기 {again}");
            yield return StowHook(pawn);
            yield return Clear();
        }

        // ------------------------------------------------------------------ en passant

        IEnumerator HookEnPassant()
        {
            var climber = Spawn(QueenHillTestBed.HookStart, Vector3.right, "hook-climber");
            var cutter = Spawn(new Vector3(QueenHillTestBed.HookTowerFaceX + 3f, QueenHillTestBed.HookTowerTop, QueenHillTestBed.HookTowerCenter.z + 3f),
                Vector3.left, "hook-cutter");
            yield return Sim(0.6f);
            var (aim, steps) = TowerTopThrow(climber, 1.5f);
            yield return ThrowHookSteps(climber, aim, steps);
            // Wait for the hook to stick, then stand the cutter right beside it and hold F.
            yield return WaitForHook(climber, aim);
            bool stuck = climber.Hook == HookPhase.Pulling;
            Vector3 hook = climber.HookPoint;
            cutter.Teleport(new Vector3(hook.x + 0.3f, QueenHillTestBed.HookTowerTop + cutter.standHeight + 0.02f, hook.z + 1.2f), Vector3.back);
            yield return Sim(0.3f, () =>
            {
                HookInput(climber, aim);
                HookInput(cutter, Vector3.zero);
            });
            // A teammate holding F does nothing (M10): only an enemy's hook can be cut.
            climber.Team = cutter.Team = Gameplay.Teams.White;
            yield return Sim(0.8f, () =>
            {
                HookInput(climber, aim);
                HookInput(cutter, Vector3.zero, interact: true);
            });
            bool teammateLeft = climber.HookCutOff == 0 && climber.Hook == HookPhase.Pulling;
            cutter.Team = Gameplay.Teams.Black;
            HookInput(cutter, Vector3.zero);
            yield return Sim(Dt, () => HookInput(climber, aim));
            float t = 0f, cutAt = -1f, climberTop = climber.Hips.position.y;
            yield return Sim(3f, () =>
            {
                t += Dt;
                HookInput(climber, aim);
                HookInput(cutter, Vector3.zero, interact: true);
                if (cutAt < 0f && climber.HookCutOff > 0) cutAt = t;
                climberTop = Mathf.Max(climberTop, climber.Hips.position.y);
            });
            bool fell = climber.Hips.position.y < QueenHillTestBed.HookTowerTop - 1f && climber.Hook != HookPhase.Pulling;
            var p = game.tuning.values;
            Report("M5·M10 앙파상: 끌려가는 적의 갈고리 옆에서 F를 0.4초 누르면 갈고리가 빠지고 적이 떨어짐 (같은 팀은 안 됨)",
                stuck && teammateLeft && cutAt > 0f && Mathf.Abs(cutAt - p.enPassantHold) < 0.1f && fell && cutter.EnPassantCuts == 1,
                $"갈고리 박힘 {stuck}, 같은 팀이 0.8초 눌러도 그대로 {teammateLeft}, 적이 F 누른 뒤 {Fmt(cutAt)}에 빠짐 (설정 {p.enPassantHold:F1}초), 끊은 쪽 앙파상 {cutter.EnPassantCuts}회, "
                + $"상대 떨어짐 {fell} (최고 {climberTop:F1} m → 지금 {climber.Hips.position.y:F1} m)");
            yield return Clear();
        }

        // ------------------------------------------------------------------ network

        IEnumerator HookNetwork()
        {
            var host = Spawn(new Vector3(45f, 0f, -20f), Vector3.back, "hook-net-host");
            var remote = Spawn(new Vector3(45f, 0f, -26f), Vector3.back, "hook-net-remote");
            remote.SetNetworkPuppet(true);
            yield return Sim(0.6f);
            var pose = new RagdollPose { id = 1 };
            var list = new System.Collections.Generic.List<RagdollPose> { pose };
            var snapshot = new RagdollSnapshot();
            bool parsed = true;
            void Copy()
            {
                host.CaptureNetworkPose(pose);
                pose.id = 1;
                byte[] bytes = RagdollNetProtocol.Snapshot(77, 1, 1, list);
                parsed &= RagdollNetProtocol.ReadSnapshot(bytes, 77, snapshot);
                remote.ApplyNetworkPose(snapshot.At(0));
            }
            Vector3 aim = Vector3.back;
            HookInput(host, aim, ability: true);
            yield return Sim(Dt);
            HookInput(host, aim, press: true, held: true);
            yield return Sim(0.5f, () => HookInput(host, aim, held: true));
            Copy();
            bool swing = remote.Hook == HookPhase.Charging && Mathf.Abs(remote.HookCharge - host.HookCharge) < 0.01f;
            HookInput(host, aim);
            yield return Sim(0.2f, () => HookInput(host, aim));
            Copy();
            bool flying = remote.Hook == HookPhase.Flying;
            float pointError = Vector3.Distance(remote.HookPoint, host.HookPoint);
            // The input packet carries the held button.
            var sent = new RagdollNetInput { shoveHeld = true, aim = Vector3.up };
            bool heldOk = RagdollNetProtocol.ReadInput(RagdollNetProtocol.Input(77, 3, 5, sent), 77, out _, out _, out var got) && got.shoveHeld;
            Report("M5 갈고리: 원격 화면에 갈고리 상태·게이지·위치가 전달되고, 좌클릭 누르고 있기가 입력 패킷에 실림",
                parsed && swing && flying && pointError < 0.01f && heldOk,
                $"해독 {parsed}, 돌리는 중·게이지 {swing} (원격 {remote.HookCharge:F2} / 방장 {host.HookCharge:F2}), 날아가는 중 {flying}, "
                + $"갈고리 위치 오차 {pointError * 100f:F2} cm, 좌클릭 누름 비트 {heldOk}, 폰당 {RagdollNetProtocol.PoseBytes}바이트");
            yield return Clear();
        }
    }
}
