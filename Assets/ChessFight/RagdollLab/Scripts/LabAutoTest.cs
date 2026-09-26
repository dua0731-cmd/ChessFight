using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Scripted checks and screenshots for the lab, run only from the command line:
    /// -ragdollAutoTest &lt;report.txt&gt; and/or -ragdollShots &lt;folder&gt;. Never active in normal play.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public partial class LabAutoTest : MonoBehaviour
    {
        public LabGame game;

        public static bool Requested =>
            Arg("-ragdollAutoTest") != null || Arg("-ragdollShots") != null
            || Arg("-ragdollClip") != null || Arg("-ragdollActionClip") != null || FeelRequested;
        static bool PanelShotRequested => Arg("-ragdollPanelShot") != null;

        readonly StringBuilder log = new StringBuilder();
        int passed, failed;
        bool allFinite = true;
        string pendingShot;
        Camera shotCamera;
        string clipFolder;
        int clipFrame;

        public static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name)
                    return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "";
            return null;
        }

        IEnumerator Start()
        {
            if (PanelShotRequested)
            {
                // Normal play with the tuning panel open, captured with IMGUI (needs a windowed player).
                yield return new WaitForSeconds(1.5f);
                if (game.dummies.Count > 0) game.dummies[0].Knockdown("사진");
                game.PanelOpen = true;
                yield return new WaitForSeconds(0.4f);
                ScreenCapture.CaptureScreenshot(Arg("-ragdollPanelShot"));
                yield return new WaitForSeconds(0.6f);
                Application.Quit();
                yield break;
            }
            if (!Requested)
            {
                enabled = false;
                yield break;
            }
            yield return null;
            yield return null;
            // -ragdollTuning <file.json> runs the whole suite on a candidate value set.
            string preset = Arg("-ragdollTuning");
            if (!string.IsNullOrEmpty(preset) && File.Exists(preset))
            {
                game.tuning.LoadJson(File.ReadAllText(preset));
                log.AppendLine($"preset {Path.GetFileName(preset)}");
            }
            string report = Arg("-ragdollAutoTest");
            if (report != null) yield return RunTests(string.IsNullOrEmpty(report) ? "ragdoll_autotest.txt" : report);
            string shots = Arg("-ragdollShots");
            if (shots != null) yield return RunShots(string.IsNullOrEmpty(shots) ? "shots" : shots);
            string clip = Arg("-ragdollClip");
            if (clip != null) yield return RunClip(string.IsNullOrEmpty(clip) ? "clip" : clip);
            string feel = Arg("-ragdollFeel");
            if (feel != null) yield return RunFeel(string.IsNullOrEmpty(feel) ? "feel" : feel);
            string actions = Arg("-ragdollActionClip");
            if (actions != null) yield return RunActionClip(string.IsNullOrEmpty(actions) ? "actions" : actions);
            Application.Quit();
        }

        // ------------------------------------------------------------------ helpers

        RagdollPawn Spawn(Vector3 ground, Vector3 forward, string name) => game.Spawn(ground, forward, null, name);

        static float Dt => Time.fixedDeltaTime;

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static void Drive(RagdollPawn p, Vector3 move, bool grab = false, bool jump = false, bool shove = false, bool sprint = false) =>
            p.SetInput(new PawnInput { move = move, grab = grab, jump = jump, shove = shove, sprint = sprint });

        IEnumerator Sim(float seconds, Action perStep = null)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                perStep?.Invoke();
                yield return new WaitForFixedUpdate();
                foreach (var pawn in RagdollPawn.All)
                    if (!pawn.IsFinite()) allFinite = false;
            }
        }

        IEnumerator Clear()
        {
            foreach (var pawn in RagdollPawn.All.ToArray()) Destroy(pawn.gameObject);
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        void Report(string name, bool ok, string detail)
        {
            if (ok) passed++;
            else failed++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        void Info(string name, string detail) => log.AppendLine($"INFO | {name} | {detail}");

        // ------------------------------------------------------------------ tests

        IEnumerator RunTests(string path)
        {
            Time.timeScale = 4f;
            Time.fixedDeltaTime = 1f / game.physicsRate;
            Time.maximumDeltaTime = 0.5f;
            log.AppendLine($"Ragdoll lab autotest {DateTime.Now:yyyy-MM-dd HH:mm:ss}  fixedDt={Time.fixedDeltaTime:F4} gravity={Physics.gravity.y:F2}");
            log.AppendLine("params " + JsonUtility.ToJson(game.tuning.values));
            if (Arg("-ragdollDiag") != null) yield return Diagnostics();
            // -ragdollQueenHillOnly: just the Queen of the Hill mechanics (quick to iterate on).
            if (Arg("-ragdollQueenHillOnly") != null)
            {
                yield return QueenHill();
                Report("NaN/폭발 없음", allFinite, allFinite ? "모든 부위 좌표 유한" : "NaN 또는 무한대 좌표 발생");
                log.AppendLine($"RESULT passed={passed} failed={failed}");
                File.WriteAllText(path, log.ToString());
                Time.timeScale = 1f;
                yield break;
            }
            // -ragdollAutoTestOnly Climb,ClimbSurfaces: only those tests (by method name), to iterate on
            // a few without the whole eight-minute run.
            var tests = new (string name, Func<IEnumerator> run)[]
            {
                ("JointSign", JointSign), ("Stand", Stand), ("Run", Run), ("Sprint", Sprint),
                ("GaitShape", GaitShape), ("Turn", Turn), ("NoAutoHop", NoAutoHop), ("JumpCheck", JumpCheck),
                ("JumpNoStack", JumpNoStack), ("GetUp", GetUp), ("Fall", Fall), ("Slope", Slope),
                ("DiveSlope", DiveSlope), ("Contact", Contact), ("GrabDrag", GrabDrag),
                ("StruggleEscape", StruggleEscape), ("Climb", Climb), ("ClimbBugs", ClimbBugs),
                ("ClimbSurfaces", ClimbSurfaces), ("DiveTackle", DiveTackle), ("Bar", Bar), ("Beam", Beam),
                ("WallClimb", WallClimb), ("QueenHill", QueenHill), ("Crowd", Crowd), ("NetLoopback", NetLoopback),
            };
            string only = Arg("-ragdollAutoTestOnly");
            foreach (var (name, run) in tests)
                if (string.IsNullOrEmpty(only) || Array.IndexOf(only.Split(','), name) >= 0)
                    yield return run();
            Report("NaN/폭발 없음", allFinite, allFinite ? "모든 부위 좌표 유한" : "NaN 또는 무한대 좌표 발생");
            log.AppendLine($"RESULT passed={passed} failed={failed}");
            File.WriteAllText(path, log.ToString());
            Debug.Log(log.ToString());
            Time.timeScale = 1f;
        }

        string JointErrors(RagdollPawn pawn)
        {
            var sb = new StringBuilder();
            for (int i = 1; i < RagdollPawn.Count; i++)
            {
                Transform child = pawn.bodies[i].transform;
                Transform parent = pawn.bodies[RagdollPawn.ParentOf[i]].transform;
                Quaternion rel = Quaternion.Inverse(parent.rotation) * child.rotation;
                Vector3 e = rel.eulerAngles;
                sb.Append($"{(BodyId)i}({Signed(e.x):F0},{Signed(e.y):F0},{Signed(e.z):F0}) ");
            }
            return sb.ToString();
        }

        static float ChestPitch(RagdollPawn pawn)
        {
            Quaternion rel = Quaternion.Inverse(pawn.bodies[0].rotation) * pawn.bodies[(int)BodyId.Chest].rotation;
            Vector3 up = rel * Vector3.up;
            return Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg;
        }

        IEnumerator DriveProbe()
        {
            var p = game.tuning.values;
            float savedRatio = p.damperRatio;
            p.damperRatio = 0.02f;
            // Zero-g, hips pinned. Kick the chest, record its pitch, fit frequency and decay.
            var pawn = Spawn(new Vector3(-5f, 2f, 5f), Vector3.forward, "drive-probe");
            foreach (var rb in pawn.bodies) rb.useGravity = false;
            pawn.Hips.isKinematic = true;
            foreach (var id in new[] { BodyId.Head, BodyId.ArmL, BodyId.HandL, BodyId.ArmR, BodyId.HandR })
            {
                pawn.bodies[(int)id].isKinematic = false;
            }
            Destroy(pawn.joints[(int)BodyId.Head]);
            Destroy(pawn.joints[(int)BodyId.ArmL]);
            Destroy(pawn.joints[(int)BodyId.ArmR]);
            yield return Sim(0.5f);
            var chest = pawn.bodies[(int)BodyId.Chest];
            chest.angularVelocity = chest.transform.right * 3f;
            var samples = new System.Collections.Generic.List<float>();
            yield return Sim(2f, () => samples.Add(ChestPitch(pawn)));
            // Zero crossings -> damped period; successive peak ratio -> damping.
            var peaks = new System.Collections.Generic.List<(int index, float value)>();
            for (int i = 1; i + 1 < samples.Count; i++)
                if (Mathf.Abs(samples[i]) > Mathf.Abs(samples[i - 1]) && Mathf.Abs(samples[i]) >= Mathf.Abs(samples[i + 1]) && Mathf.Abs(samples[i]) > 0.2f)
                    peaks.Add((i, samples[i]));
            float inertia = Vector3.Dot(chest.transform.right, chest.inertiaTensorRotation * Vector3.Scale(chest.inertiaTensor, Quaternion.Inverse(chest.inertiaTensorRotation) * chest.transform.right));
            Vector3 com = chest.worldCenterOfMass - chest.position;
            float pivotInertia = inertia + chest.mass * (com.y * com.y + com.z * com.z);
            string detail = $"관성(허리 기준) {pivotInertia:F3} kg·m², 피크 {peaks.Count}개";
            if (peaks.Count >= 3)
            {
                float halfPeriod = (peaks[2].index - peaks[1].index) * Dt;
                float omegaD = Mathf.PI / halfPeriod;
                float ratio = Mathf.Abs(peaks[2].value / peaks[1].value);
                float zetaTerm = -Mathf.Log(ratio) / Mathf.PI;
                float zeta = zetaTerm / Mathf.Sqrt(1f + zetaTerm * zetaTerm);
                float omegaN = omegaD / Mathf.Sqrt(1f - zeta * zeta);
                float kEff = omegaN * omegaN * pivotInertia;
                float cEff = 2f * zeta * omegaN * pivotInertia;
                detail += $", 실효 스프링 {kEff:F0} (설정 {p.upperBodySpring:F0}, 비 {kEff / p.upperBodySpring:F2}), 실효 감쇠 {cEff:F1} (설정 {p.upperBodySpring * p.damperRatio:F1}, 비 {cEff / (p.upperBodySpring * p.damperRatio):F2})";
            }
            else detail += $", 진동 없음 (첫 값 {samples[0]:F1}, 끝 값 {samples[samples.Count - 1]:F1})";
            Info("진단: 상체 드라이브 실효값 (감쇠비 0.02)", detail);
            p.damperRatio = savedRatio;
            yield return Clear();

            // Anchor angular drive (XY&Z mode): zero-g, torque the hips about X against the anchor.
            var hipsProbe = Spawn(new Vector3(-5f, 3f, 5f), Vector3.forward, "angular-probe");
            foreach (var rb in hipsProbe.bodies) rb.useGravity = false;
            yield return Sim(0.5f);
            const float hipTorque = 100f;
            float tilt = 0f;
            yield return Sim(2f, () =>
            {
                hipsProbe.Hips.AddTorque(Vector3.right * hipTorque, ForceMode.Force);
                tilt = hipsProbe.HipsTilt;
            });
            Info("진단: 골반 직립 드라이브 실효값", $"{hipTorque:F0} N·m에 {tilt:F2}° → {hipTorque / (tilt * Mathf.Deg2Rad):F0} N·m/rad (설정 {p.balanceStrength:F0})");
            yield return Clear();

            // Anchor linear drive: zero-g, push the hips with a known force against the (fixed) anchor.
            var lin = Spawn(new Vector3(-5f, 3f, 5f), Vector3.forward, "linear-probe");
            foreach (var rb in lin.bodies) rb.useGravity = false;
            yield return Sim(0.5f);
            const float force = 300f;
            float offset = 0f;
            Vector3 anchorStart = lin.AnchorPosition;
            yield return Sim(2f, () =>
            {
                lin.Hips.AddForce(Vector3.right * force, ForceMode.Force);
                offset = lin.Hips.position.x - lin.AnchorPosition.x;
            });
            Info("진단: 앵커 직선 드라이브 실효값", $"{force:F0} N에 {offset:F3} m → {force / Mathf.Max(1e-4f, offset):F0} N/m (설정 {p.hipAnchorStrength:F0}), 앵커 이동 {(lin.AnchorPosition - anchorStart).magnitude:F3} m");
            yield return Clear();
        }

        IEnumerator StiffnessProbe()
        {
            yield return DriveProbe();
            var p = game.tuning.values;
            foreach (var (hz, iterations) in new[] { (60, 16), (60, 40), (120, 24), (240, 16) })
            {
                Time.fixedDeltaTime = 1f / hz;
                var pawn = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "probe");
                foreach (var rb in pawn.bodies)
                {
                    rb.solverIterations = iterations;
                    rb.solverVelocityIterations = Mathf.Max(4, iterations / 4);
                }
                yield return Sim(1.5f);
                float min = 999f, max = -999f, sum = 0f;
                int n = 0;
                yield return Sim(1f, () =>
                {
                    float a = ChestPitch(pawn);
                    min = Mathf.Min(min, a);
                    max = Mathf.Max(max, a);
                    sum += a;
                    n++;
                });
                Info($"진단: 서 있기 상체 앞뒤 각도 {hz}Hz 솔버 {iterations}", $"평균 {sum / n:F1}°, 범위 {min:F1}~{max:F1}°, 골반 y {pawn.Hips.position.y:F3}");
                yield return Clear();

                // Zero-g, hips pinned, push the chest with a known torque and read the settled angle.
                var probe = Spawn(new Vector3(-5f, 2f, 5f), Vector3.forward, "probe-torque");
                foreach (var rb in probe.bodies)
                {
                    rb.useGravity = false;
                    rb.solverIterations = iterations;
                    rb.solverVelocityIterations = Mathf.Max(4, iterations / 4);
                }
                probe.Hips.isKinematic = true;
                yield return Sim(0.5f);
                const float torque = 40f;
                float settled = 0f;
                yield return Sim(2f, () =>
                {
                    probe.bodies[(int)BodyId.Chest].AddTorque(Vector3.right * torque, ForceMode.Force);
                    settled = ChestPitch(probe);
                });
                float expectedDeg = torque / p.upperBodySpring * Mathf.Rad2Deg;
                Info($"진단: 상체에 {torque:F0} N·m 가함 {hz}Hz", $"기울기 {settled:F2}° (스프링 {p.upperBodySpring:F0} 기준 예상 {expectedDeg:F2}°) → 실효 강성 {torque / (Mathf.Abs(settled) * Mathf.Deg2Rad):F0} N·m/rad");
                yield return Clear();
            }
            Time.fixedDeltaTime = 1f / game.physicsRate;
        }

        /// <summary>12 pawns (6v6) piling into each other, timed per physics step at several settings.</summary>
        IEnumerator LoadTest()
        {
            var profiler = game.gameObject.AddComponent<PhysicsStepProfiler>();
            int savedIterations = game.solverIterations;
            foreach (var (hz, iterations, crowd) in new[]
            {
                (120, 24, 12), (120, 24, 6), (90, 24, 12), (60, 40, 12), (60, 16, 12), (120, 40, 12),
            })
            {
                Time.fixedDeltaTime = 1f / hz;
                game.solverIterations = iterations;
                var pawns = new System.Collections.Generic.List<RagdollPawn>();
                for (int i = 0; i < crowd; i++)
                {
                    bool blue = i % 2 == 0;
                    float lane = (i / 2 - (crowd / 4f)) * 1.3f;
                    var pos = new Vector3(lane, 0f, blue ? -3f : 3f);
                    pawns.Add(Spawn(pos, blue ? Vector3.forward : Vector3.back, (blue ? "청" : "홍") + i));
                }
                yield return Sim(1f);
                profiler.ResetCounters();
                profiler.measuring = true;
                float t = 0f;
                yield return Sim(6f, () =>
                {
                    t += Dt;
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        var pawn = pawns[i];
                        bool blue = i % 2 == 0;
                        Vector3 move = (blue ? Vector3.forward : Vector3.back) + Vector3.right * Mathf.Sin(t * 1.3f + i);
                        // Half of them grab and shove, which is the heaviest case (joints + queries).
                        Drive(pawn, move.normalized, grab: i % 4 == 0, shove: i % 4 == 1 && Mathf.Repeat(t, 1.2f) < Dt);
                    }
                });
                profiler.measuring = false;
                double perSecond = profiler.AverageMs * hz;
                int knockdowns = 0;
                foreach (var pawn in pawns) knockdowns += pawn.Knockdowns;
                Info($"부하: {crowd}명 {hz}Hz 솔버 {iterations}회",
                    $"물리 1스텝 평균 {profiler.AverageMs:F2} ms (최대 {profiler.MaxMs:F2}), 게임 1초당 물리 {perSecond:F0} ms = CPU 코어 {perSecond / 10f:F0}%, 넘어짐 {knockdowns}회");
                yield return Clear();
            }
            profiler.measuring = false;
            Destroy(profiler);
            game.solverIterations = savedIterations;
            Time.fixedDeltaTime = 1f / game.physicsRate;
        }

        IEnumerator RunVariants()
        {
            var p = game.tuning.values;
            string saved = JsonUtility.ToJson(p);
            var variants = new (string label, Action apply)[]
            {
                ("기본", () => { }),
                ("애니메이션·기울기·마찰 전부 0", () =>
                {
                    p.legSwing = 0f; p.armSwing = 0f; p.runLean = 0f; p.chestLean = 0f; p.footFrictionMoving = 0f;
                }),
                ("전부 0 + 이동 속도 1", () =>
                {
                    p.legSwing = 0f; p.armSwing = 0f; p.runLean = 0f; p.chestLean = 0f; p.footFrictionMoving = 0f; p.moveSpeed = 1f;
                }),
                ("전부 0 + 앵커 20000", () =>
                {
                    p.legSwing = 0f; p.armSwing = 0f; p.runLean = 0f; p.chestLean = 0f; p.footFrictionMoving = 0f; p.hipAnchorStrength = 20000f;
                }),
                ("직립 토크 4000", () => p.balanceStrength = 4000f),
            };
            foreach (var (label, apply) in variants)
            {
                JsonUtility.FromJsonOverwrite(saved, p);
                apply();
                var pawn = Spawn(new Vector3(0f, 0f, -13.5f), Vector3.forward, "run-variant");
                yield return Sim(0.6f);
                Drive(pawn, Vector3.forward);
                yield return Sim(1f);
                float tiltSum = 0f, chestSum = 0f, speedSum = 0f, footDown = 0f, leadSum = 0f, heightSum = 0f;
                int n = 0;
                yield return Sim(1.2f, () =>
                {
                    Vector3 up = pawn.Hips.transform.up;
                    tiltSum += Mathf.Atan2(Vector3.Dot(up, Vector3.forward), up.y) * Mathf.Rad2Deg;
                    chestSum += ChestPitch(pawn);
                    speedSum += pawn.HorizontalSpeed;
                    footDown += Mathf.Min(pawn.bodies[(int)BodyId.FootL].position.y, pawn.bodies[(int)BodyId.FootR].position.y);
                    leadSum += pawn.AnchorPosition.z - pawn.Hips.position.z;
                    heightSum += pawn.AnchorPosition.y - pawn.Hips.position.y;
                    n++;
                });
                Info($"진단: 달리기 {label}", $"골반 앞기울기 평균 {tiltSum / n:F1}°, 상체 {chestSum / n:F1}°, 속도 {speedSum / n:F2} m/s, 앵커 앞섬 {leadSum / n:F3} m / 위 {heightSum / n:F3} m, 낮은 발목 {footDown / n:F3}, 넘어짐 {pawn.Knockdowns}");
                yield return Clear();
            }
            JsonUtility.FromJsonOverwrite(saved, p);

            var shoves = new (string label, Action apply)[]
            {
                ("기본", () => { }),
                ("내딛기 0.7m", () => p.shoveLunge = 0.7f),
                ("팔 배율 20", () => p.shoveArmMultiplier = 20f),
                ("몸 기울기 40°", () => p.shoveLean = 40f),
            };
            foreach (var (label, apply) in shoves)
            {
                JsonUtility.FromJsonOverwrite(saved, p);
                apply();
                var a = Spawn(new Vector3(-8f, 0f, 8f), Vector3.forward, "shover");
                var b = Spawn(new Vector3(-8f, 0f, 8.65f), Vector3.back, "target");
                yield return Sim(1f);
                Vector3 b0 = b.Hips.position, a0 = a.Hips.position;
                Drive(a, Vector3.zero, shove: true);
                float peakB = 0f, maxHandZ = -99f;
                yield return Sim(1.2f, () =>
                {
                    peakB = Mathf.Max(peakB, b.HorizontalSpeed);
                    maxHandZ = Mathf.Max(maxHandZ, Mathf.Max(a.handL.Center.z, a.handR.Center.z));
                });
                Info($"진단: 밀치기 {label}", $"상대 밀림 {Vector3.Distance(Flat(b.Hips.position), Flat(b0)):F2} m (최고 {peakB:F2} m/s), 미는 쪽 전진 {a.Hips.position.z - a0.z:F2} m, 손 최대 전진 z {maxHandZ - a0.z:F2} m (상대 상체 앞면 ≈ {b0.z - 0.13f - a0.z:F2}), 상대 넘어짐 {b.Knockdowns}");
                yield return Clear();
            }
            JsonUtility.FromJsonOverwrite(saved, p);
        }

        IEnumerator Diagnostics()
        {
            var p = game.tuning.values;
            if (Arg("-ragdollProbe") != null)
            {
                yield return StiffnessProbe();
                yield break;
            }
            if (Arg("-ragdollRunDiag") != null)
            {
                yield return RunVariants();
                yield break;
            }
            if (Arg("-ragdollLoad") != null)
            {
                yield return LoadTest();
                yield break;
            }
            var mass = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "diag-mass");
            {
                Vector3 waist = mass.bodies[(int)BodyId.Chest].position;
                var sb = new StringBuilder();
                Vector3 sum = Vector3.zero;
                float total = 0f;
                foreach (var id in new[] { BodyId.Chest, BodyId.Head, BodyId.ArmL, BodyId.HandL, BodyId.ArmR, BodyId.HandR })
                {
                    var rb = mass.bodies[(int)id];
                    Vector3 c = rb.worldCenterOfMass - waist;
                    sb.Append($"{id} {rb.mass:F1}kg ({c.x:F3},{c.y:F3},{c.z:F3}) ");
                    sum += c * rb.mass;
                    total += rb.mass;
                }
                Vector3 com = sum / total;
                Info("진단: 허리 기준 상체 무게중심", sb + $"→ 합계 ({com.x:F3},{com.y:F3},{com.z:F3})");
                var hips = mass.bodies[0];
                Info("진단: 골반", $"피벗 {hips.position} 무게중심 {hips.worldCenterOfMass} 관성 {hips.inertiaTensor}");
            }
            yield return Clear();
            var air = Spawn(new Vector3(-5f, 3f, 5f), Vector3.forward, "diag-air");
            foreach (var rb in air.bodies) rb.useGravity = false;
            yield return Sim(1f);
            Info("진단: 무중력 공중 관절 각도", JointErrors(air));
            yield return Clear();

            var fresh = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "diag-first");
            yield return new WaitForFixedUpdate();
            Info("진단: 생성 직후 1스텝", $"골반 y {fresh.Hips.position.y:F3} " + JointErrors(fresh));
            yield return Sim(0.25f);
            Info("진단: 생성 0.25초", $"골반 y {fresh.Hips.position.y:F3} " + JointErrors(fresh));
            yield return Clear();

            float savedLower = p.lowerBodySpring, savedAnchor = p.hipAnchorStrength;
            foreach (var (lower, anchorK) in new[] { (10000f, 3000f), (2000f, 20000f), (2000f, 0f) })
            {
                p.lowerBodySpring = lower;
                p.hipAnchorStrength = anchorK;
                var pawn = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "diag");
                yield return Sim(1.5f);
                Info($"진단: 하체 {lower:F0} / 앵커 {anchorK:F0}", $"골반 y {pawn.Hips.position.y:F3}, 기울기 {pawn.HipsTilt:F1}° " + JointErrors(pawn));
                yield return Clear();
            }
            p.lowerBodySpring = savedLower;
            p.hipAnchorStrength = savedAnchor;

            foreach (int variant in new[] { 1, 3, 4, 5, 6 })
            {
                float savedGravity = p.gravityScale;
                float savedStep = Time.fixedDeltaTime;
                var pawn = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "diag-variant");
                foreach (var rb in pawn.bodies)
                {
                    rb.solverIterations = 40;
                    rb.solverVelocityIterations = 10;
                }
                pawn.Hips.isKinematic = true;
                pawn.Hips.MovePosition(pawn.Hips.position + Vector3.up * 0.3f);
                string label = "골반 고정(공중)";
                if (variant == 3)
                {
                    p.gravityScale = savedGravity * 2f;
                    label += " 중력 2배";
                }
                else if (variant == 4)
                {
                    Time.fixedDeltaTime = 1f / 240f;
                    label += " 240Hz";
                }
                else if (variant == 5)
                {
                    var chestJoint = pawn.joints[(int)BodyId.Chest];
                    chestJoint.angularXMotion = chestJoint.angularYMotion = chestJoint.angularZMotion = ConfigurableJointMotion.Free;
                    label += " 상체 제한 없음";
                }
                else if (variant == 6)
                {
                    foreach (var id in new[] { BodyId.Head, BodyId.ArmL, BodyId.HandL, BodyId.ArmR, BodyId.HandR })
                        pawn.bodies[(int)id].isKinematic = false;
                    Destroy(pawn.joints[(int)BodyId.Head]);
                    Destroy(pawn.joints[(int)BodyId.ArmL]);
                    Destroy(pawn.joints[(int)BodyId.ArmR]);
                    label += " 머리·팔 분리";
                }
                yield return Sim(1.5f);
                p.gravityScale = savedGravity;
                Time.fixedDeltaTime = savedStep;
                Transform chest = pawn.bodies[(int)BodyId.Chest].transform;
                Info($"진단: {label} (솔버 40)", $"상체 세계 기울기 {Vector3.Angle(chest.up, Vector3.up):F1}°, 골반 y {pawn.Hips.position.y:F3}, " + JointErrors(pawn));
                yield return Clear();
            }

            float savedUpper = p.upperBodySpring, savedArm = p.armSpring;
            foreach (var (upper, arm) in new[] { (4000f, 80f), (400f, 0f), (400f, 800f) })
            {
                p.upperBodySpring = upper;
                p.armSpring = arm;
                var pawn = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "diag");
                foreach (var rb in pawn.bodies)
                {
                    rb.solverIterations = 40;
                    rb.solverVelocityIterations = 10;
                }
                yield return Sim(1.5f);
                Transform chest = pawn.bodies[(int)BodyId.Chest].transform;
                Info($"진단: 상체 {upper:F0} / 팔 {arm:F0} (솔버 40)", $"상체 세계 기울기 {Vector3.Angle(chest.up, Vector3.up):F1}° (앞뒤 z {chest.up.z:F2}), 퍼펫 상체 {pawn.puppet[(int)BodyId.Chest].localEulerAngles}, " + JointErrors(pawn));
                yield return Clear();
            }
            p.upperBodySpring = savedUpper;
            p.armSpring = savedArm;

            float savedDt = Time.fixedDeltaTime;
            foreach (var (iterations, velocityIterations, hz) in new[] { (40, 10, 60), (16, 4, 120), (40, 10, 120), (100, 20, 60) })
            {
                Time.fixedDeltaTime = 1f / hz;
                var pawn = Spawn(new Vector3(-5f, 0f, 5f), Vector3.forward, "diag");
                foreach (var rb in pawn.bodies)
                {
                    rb.solverIterations = iterations;
                    rb.solverVelocityIterations = velocityIterations;
                }
                yield return Sim(1.5f);
                Info($"진단: 솔버 {iterations}/{velocityIterations}, {hz}Hz", $"골반 y {pawn.Hips.position.y:F3}, 기울기 {pawn.HipsTilt:F1}° " + JointErrors(pawn));
                yield return Clear();
            }
            Time.fixedDeltaTime = savedDt;
        }

        IEnumerator JointSign()
        {
            var pawn = Spawn(new Vector3(0f, 0f, -10f), Vector3.forward, "sign");
            yield return Sim(1f);
            Transform hips = pawn.bodies[0].transform;

            Quaternion chestWant = Quaternion.Euler(25f, 0f, 0f);
            pawn.PoseOverride = i => i == (int)BodyId.Chest ? chestWant : (Quaternion?)null;
            yield return Sim(0.8f);
            Quaternion chestRel = Quaternion.Inverse(hips.rotation) * pawn.bodies[(int)BodyId.Chest].transform.rotation;
            float chestError = Quaternion.Angle(chestRel, chestWant);
            float chestForward = (chestRel * Vector3.up).z;
            Report("관절 목표 방향: 상체 앞으로 25°", chestError < 12f && chestForward > 0.2f, $"각도 오차 {chestError:F1}°, 상체 위쪽 벡터 z {chestForward:F2} (앞 +)");

            // Counter-rotate the foot so the sole stays flat instead of digging its heel into the floor.
            Quaternion thighWant = Quaternion.Euler(-40f, 0f, 0f);
            Quaternion footWant = Quaternion.Euler(40f, 0f, 0f);
            pawn.PoseOverride = i => i == (int)BodyId.ThighL ? thighWant : i == (int)BodyId.FootL ? footWant : (Quaternion?)null;
            yield return Sim(0.8f);
            Quaternion thighRel = Quaternion.Inverse(hips.rotation) * pawn.bodies[(int)BodyId.ThighL].transform.rotation;
            float thighError = Quaternion.Angle(thighRel, thighWant);
            float thighForward = (thighRel * Vector3.down).z;
            Report("관절 목표 방향: 허벅지 앞으로 40°", thighError < 15f && thighForward > 0.3f, $"각도 오차 {thighError:F1}°, 다리 방향 z {thighForward:F2} (앞 +), 골반 기울기 {pawn.HipsTilt:F1}°");
            pawn.PoseOverride = null;
            yield return Clear();
        }

        IEnumerator Stand()
        {
            var pawn = Spawn(new Vector3(0f, 0f, -10f), Vector3.forward, "stand");
            yield return Sim(1f);
            float maxTilt = 0f, minY = 99f, maxY = -99f, maxSpeed = 0f;
            yield return Sim(2f, () =>
            {
                maxTilt = Mathf.Max(maxTilt, pawn.HipsTilt);
                float y = pawn.Hips.position.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                maxSpeed = Mathf.Max(maxSpeed, pawn.Hips.linearVelocity.magnitude);
            });
            float chestSum = 0f;
            int chestN = 0;
            yield return Sim(0.5f, () =>
            {
                chestSum += ChestPitch(pawn);
                chestN++;
            });
            float chestMean = chestSum / chestN;
            Report("가만히 서 있기", maxTilt < 12f && pawn.Knockdowns == 0 && maxSpeed < 0.6f && minY > pawn.standHeight - 0.03f,
                $"최대 기울기 {maxTilt:F1}°, 골반 높이 {minY:F3}~{maxY:F3} (기준 {pawn.standHeight:F3}), 최대 속도 {maxSpeed:F2} m/s, 상체 앞뒤 평균 {chestMean:F1}°");
            Transform hips = pawn.bodies[0].transform;
            string Leg(BodyId thigh, BodyId foot)
            {
                Quaternion rel = Quaternion.Inverse(hips.rotation) * pawn.bodies[(int)thigh].transform.rotation;
                Vector3 e = rel.eulerAngles;
                float footY = pawn.bodies[(int)foot].position.y;
                return $"{thigh} 회전 ({Signed(e.x):F0},{Signed(e.y):F0},{Signed(e.z):F0})° 발목 높이 {footY:F3}";
            }
            Info("서 있기 자세", $"앵커 y {pawn.AnchorPosition.y:F3} / 골반 y {pawn.Hips.position.y:F3}, 접지 {pawn.Grounded}, {Leg(BodyId.ThighL, BodyId.FootL)}, {Leg(BodyId.ThighR, BodyId.FootR)}");
            yield return Clear();
        }

        IEnumerator Run()
        {
            float target = game.tuning.values.moveSpeed;
            var pawn = Spawn(new Vector3(0f, 0f, -13.5f), Vector3.forward, "run");
            yield return Sim(0.8f);
            Drive(pawn, Vector3.forward);
            float t = 0f, reach = -1f;
            yield return Sim(0.8f, () =>
            {
                t += Dt;
                if (reach < 0f && pawn.HorizontalSpeed >= 0.9f * target) reach = t;
            });
            Vector3 start = pawn.Hips.position;
            float maxTilt = 0f, minStiff = 1f, tiltSum = 0f, chestMin = 99f, chestMax = -99f;
            int samples = 0;
            // Foot slip: how fast the lower foot (the one taking weight) slides over the ground.
            // A planted step keeps this near zero; a gliding pawn drags both feet at running speed.
            float slipSum = 0f, slipMax = 0f;
            int plantedSamples = 0;
            float swingMax = 0f, swingSum = 0f;
            // Limp detector: a healthy gait repeats. Collect the height and the spacing of every
            // bounce of the hips; if strong and weak steps alternate, these spread out.
            var hopHeights = new System.Collections.Generic.List<float>();
            var hopTimes = new System.Collections.Generic.List<float>();
            float prevY = pawn.Hips.position.y, prevPrevY = prevY, riseTop = prevY, clock = 0f, lastTop = -1f;
            Vector3 prevL = pawn.bodies[(int)BodyId.FootL].position;
            Vector3 prevR = pawn.bodies[(int)BodyId.FootR].position;
            yield return Sim(1.5f, () =>
            {
                Vector3 fl = pawn.bodies[(int)BodyId.FootL].position;
                Vector3 fr = pawn.bodies[(int)BodyId.FootR].position;
                float slip = Mathf.Min(Flat(fl - prevL).magnitude, Flat(fr - prevR).magnitude) / Dt;
                prevL = fl;
                prevR = fr;
                slipSum += slip;
                slipMax = Mathf.Max(slipMax, slip);
                if (slip < 0.25f * target) plantedSamples++;
                // How far the leg actually swings from straight down - what reads as "running".
                Quaternion inv = Quaternion.Inverse(pawn.Hips.rotation);
                Vector3 legDir = inv * (fl - pawn.bodies[(int)BodyId.ThighL].position);
                float swingAngle = Mathf.Abs(Mathf.Atan2(legDir.z, -legDir.y) * Mathf.Rad2Deg);
                swingMax = Mathf.Max(swingMax, swingAngle);
                swingSum += swingAngle;
                clock += Dt;
                float y = pawn.Hips.position.y;
                if (prevY > prevPrevY && prevY >= y && prevY - riseTop > 0.002f)
                {
                    // prevY was a local maximum of the hips height: one bounce.
                    if (lastTop >= 0f)
                    {
                        hopTimes.Add(clock - lastTop);
                        hopHeights.Add(prevY - riseTop);
                    }
                    lastTop = clock;
                    riseTop = y;
                }
                riseTop = Mathf.Min(riseTop, y);
                prevPrevY = prevY;
                prevY = y;
                maxTilt = Mathf.Max(maxTilt, pawn.HipsTilt);
                tiltSum += pawn.HipsTilt;
                samples++;
                float chest = ChestPitch(pawn);
                chestMin = Mathf.Min(chestMin, chest);
                chestMax = Mathf.Max(chestMax, chest);
                minStiff = Mathf.Min(minStiff, pawn.EffectiveStiffness);
            });
            float average = Vector3.Distance(Flat(start), Flat(pawn.Hips.position)) / 1.5f;
            Report("평지 달리기", average >= 0.8f * target && pawn.Knockdowns == 0,
                $"평균 {average:F2} m/s (목표 {target:F1}), 90% 도달 {reach:F2}s, 골반 기울기 평균 {tiltSum / samples:F1}° 최대 {maxTilt:F1}°, 상체 앞뒤 {chestMin:F0}~{chestMax:F0}°, 최저 강성 {minStiff:F2}, 넘어짐 {pawn.Knockdowns}");
            // Keep running for another 3 s purely to collect enough bounces to judge the rhythm.
            hopHeights.Clear();
            hopTimes.Clear();
            lastTop = -1f;
            clock = 0f;
            yield return Sim(3f, () =>
            {
                clock += Dt;
                float y = pawn.Hips.position.y;
                if (prevY > prevPrevY && prevY >= y && prevY - riseTop > 0.002f)
                {
                    if (lastTop >= 0f)
                    {
                        hopTimes.Add(clock - lastTop);
                        hopHeights.Add(prevY - riseTop);
                    }
                    lastTop = clock;
                    riseTop = y;
                }
                riseTop = Mathf.Min(riseTop, y);
                prevPrevY = prevY;
                prevY = y;
            });
            Info("걸음 리듬 (달리는 중)", GaitRhythm(hopHeights, hopTimes));
            Info("다리 실제 스윙 각도 (달리는 중)",
                $"최대 {swingMax:F0}°, 평균 {swingSum / samples:F0}° (목표 진폭 {game.tuning.values.legSwing:F0}°, 보폭 주기 {game.tuning.values.strideLength:F2} m)");
            Info("디딘 발 미끄러짐 (달리는 중)",
                $"받침발 평균 {slipSum / samples:F2} m/s, 발이 멈춰 있는 시간 {100f * plantedSamples / samples:F0}% (몸 속도 {average:F2} m/s)");
            // Same measurement at walking pace, where a step is slow enough to be seen.
            Drive(pawn, Vector3.forward * 0.4f);
            yield return Sim(0.8f);
            float walkSlip = 0f, walkPlanted = 0f;
            int walkSamples = 0;
            Vector3 walkStart = pawn.Hips.position;
            prevL = pawn.bodies[(int)BodyId.FootL].position;
            prevR = pawn.bodies[(int)BodyId.FootR].position;
            yield return Sim(1.5f, () =>
            {
                Vector3 fl = pawn.bodies[(int)BodyId.FootL].position;
                Vector3 fr = pawn.bodies[(int)BodyId.FootR].position;
                float slip = Mathf.Min(Flat(fl - prevL).magnitude, Flat(fr - prevR).magnitude) / Dt;
                prevL = fl;
                prevR = fr;
                walkSlip += slip;
                walkSamples++;
                if (slip < 0.25f * 0.4f * target) walkPlanted++;
            });
            var trace = new StringBuilder();
            float traceT = 0f, traceNext = 0f;
            yield return Sim(0.5f, () =>
            {
                traceT += Dt;
                if (traceT < traceNext) return;
                traceNext = traceT + 0.04f;
                Quaternion inv = Quaternion.Inverse(pawn.Hips.rotation);
                Vector3 ankle = inv * (pawn.bodies[(int)BodyId.FootL].position - pawn.Hips.position);
                Vector3 want = pawn.puppet[(int)BodyId.ThighL].localRotation * Vector3.down;
                trace.Append($"[{traceT:F2} 발z{ankle.z:F2} 목표z{want.z:F2}]");
            });
            Info("걸음 궤적 (왼발, 걷는 중)", trace.ToString());
            float walkSpeed = Vector3.Distance(Flat(walkStart), Flat(pawn.Hips.position)) / 1.5f;
            Info("디딘 발 미끄러짐 (걷는 중)",
                $"받침발 평균 {walkSlip / walkSamples:F2} m/s, 발이 멈춰 있는 시간 {100f * walkPlanted / walkSamples:F0}% (몸 속도 {walkSpeed:F2} m/s)");
            Drive(pawn, Vector3.zero);
            t = 0f;
            float stop = -1f;
            yield return Sim(2f, () =>
            {
                t += Dt;
                if (stop < 0f && pawn.HorizontalSpeed < 0.5f) stop = t;
            });
            Report("멈추기", stop >= 0f && stop < 1f, $"0.5 m/s 미만까지 {stop:F2}s");
            yield return Clear();
        }

        /// <summary>
        /// Turns a list of bounce heights and intervals into a readable "does it limp" line.
        /// Spread is the coefficient of variation: 0% is a metronome, over ~25% reads as a limp.
        /// </summary>
        static string GaitRhythm(System.Collections.Generic.List<float> heights, System.Collections.Generic.List<float> times)
        {
            if (times.Count < 3) return $"튕김 {times.Count}회 — 걸음이라 부를 게 없음 (골반이 거의 평평하게 이동)";
            float Mean(System.Collections.Generic.List<float> v)
            {
                float t = 0f;
                foreach (float x in v) t += x;
                return t / v.Count;
            }
            float Spread(System.Collections.Generic.List<float> v)
            {
                float m = Mean(v), q = 0f;
                foreach (float x in v) q += (x - m) * (x - m);
                return m <= 1e-5f ? 0f : 100f * Mathf.Sqrt(q / v.Count) / m;
            }
            // Alternating strong/weak steps: compare odd-indexed bounces with even-indexed ones.
            float odd = 0f, even = 0f;
            int no = 0, ne = 0;
            for (int i = 0; i < heights.Count; i++)
            {
                if (i % 2 == 0) { even += heights[i]; ne++; }
                else { odd += heights[i]; no++; }
            }
            float limp = no == 0 || ne == 0 ? 0f
                : 100f * Mathf.Abs(even / ne - odd / no) / Mathf.Max(1e-5f, 0.5f * (even / ne + odd / no));
            return $"튕김 {times.Count}회, {1f / Mean(times):F1}회/초, 높이 {100f * Mean(heights):F1} cm, "
                 + $"간격 편차 {Spread(times):F0}%, 높이 편차 {Spread(heights):F0}%, 절뚝임(한걸음씩 번갈아) {limp:F0}%";
        }

        IEnumerator Turn()
        {
            var pawn = Spawn(new Vector3(-12f, 0f, 5f), Vector3.right, "turn");
            yield return Sim(0.6f);
            Drive(pawn, Vector3.right);
            yield return Sim(1.2f);
            Drive(pawn, Vector3.left);
            float t = 0f, flip = -1f, face = -1f;
            // Slamming the opposite direction must not launch the pawn faster than it can run, and the
            // torso must not whip around. Both were reported as feel bugs; measure them.
            float peakSpeed = 0f, chestWhip = 0f, headWhip = 0f, peakAt = 0f, peakAnchor = 0f, peakCom = 0f, sideTilt = 0f;
            Vector3 peakVel = Vector3.zero;
            float target = game.tuning.values.moveSpeed;
            yield return Sim(1.5f, () =>
            {
                t += Dt;
                if (flip < 0f && pawn.Hips.linearVelocity.x < -1f) flip = t;
                if (face < 0f && Vector3.Angle(Flat(pawn.Hips.transform.forward), Vector3.left) < 30f) face = t;
                if (pawn.HorizontalSpeed > peakSpeed)
                {
                    peakSpeed = pawn.HorizontalSpeed;
                    peakAt = t;
                    peakVel = Flat(pawn.Hips.linearVelocity);
                    peakAnchor = Flat(pawn.AnchorPosition - pawn.Hips.position).magnitude;
                    peakCom = ComVelocity(pawn).magnitude;
                }
                Quaternion inv = Quaternion.Inverse(pawn.Hips.transform.rotation);
                chestWhip = Mathf.Max(chestWhip, Quaternion.Angle(Quaternion.identity,
                    inv * pawn.bodies[(int)BodyId.Chest].transform.rotation));
                headWhip = Mathf.Max(headWhip, Quaternion.Angle(Quaternion.identity,
                    inv * pawn.bodies[(int)BodyId.Head].transform.rotation));
                // Seen from a camera behind the run (travel along X), how far the chest tips to
                // either side - the side-to-side rock that every reversal used to have.
                Vector3 up = pawn.bodies[(int)BodyId.Chest].transform.up;
                sideTilt = Mathf.Max(sideTilt, Mathf.Abs(Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg));
            });
            bool noSlingshot = peakSpeed <= target * 1.25f;
            Report("180° 방향 전환", flip >= 0f && flip < 0.8f && face >= 0f && face < 0.8f && pawn.Knockdowns == 0,
                $"속도 반전 {flip:F2}s, 몸 방향 {face:F2}s, 넘어짐 {pawn.Knockdowns}");
            Report("반대 방향 입력 시 과속 없음", noSlingshot,
                $"전환 중 최고 속도 {peakSpeed:F2} m/s / 달리기 {target:F2} m/s "
                + $"(= {100f * peakSpeed / Mathf.Max(0.1f, target):F0}%, 125% 이하여야 함)");
            Info("과속 상세", $"최고 {peakSpeed:F2} m/s @ {peakAt:F2}s, 방향 ({peakVel.x:F1},{peakVel.z:F1}), "
                + $"전체 무게중심 속도 {peakCom:F2} m/s, 앵커-골반 거리 {peakAnchor:F3} m");
            Info("전환 중 상체 휘청임", $"가슴 최대 {chestWhip:F0}°, 머리 최대 {headWhip:F0}° (골반 기준)");
            Report("반대 방향 전환 시 좌우로 흔들리지 않음", sideTilt < 15f,
                $"진행 방향 옆으로 가슴이 기운 최대 각도 {sideTilt:F0}° (15° 미만)");
            yield return Clear();
        }

        /// <summary>
        /// Run and sprint are two speeds: plain input runs at moveSpeed, holding sprint reaches
        /// sprintSpeed and spends stamina, and running it dry leaves the pawn winded (no sprint)
        /// until enough has come back. Laps a circle so the sprint can last long enough to run out.
        /// </summary>
        IEnumerator Sprint()
        {
            var p = game.tuning.values;
            Vector3 center = new Vector3(-5f, 0f, 0f);
            const float Radius = 8f;
            var pawn = Spawn(center + new Vector3(Radius, 0f, 0f), Vector3.forward, "sprint");
            yield return Sim(0.6f);
            Vector3 Lap()
            {
                Vector3 r = Flat(pawn.Hips.position - center);
                if (r.sqrMagnitude < 1e-4f) return Vector3.forward;
                Vector3 round = Vector3.Cross(Vector3.up, r.normalized) * -1f;
                return (round - r.normalized * ((r.magnitude - Radius) / Radius)).normalized;
            }
            float runSum = 0f, sprintSum = 0f;
            int runN = 0, sprintN = 0;
            yield return Sim(1.2f, () => Drive(pawn, Lap()));
            yield return Sim(1.5f, () =>
            {
                Drive(pawn, Lap());
                runSum += pawn.HorizontalSpeed;
                runN++;
            });
            float before = pawn.Stamina;
            yield return Sim(1.2f, () => Drive(pawn, Lap(), sprint: true));
            yield return Sim(1.5f, () =>
            {
                Drive(pawn, Lap(), sprint: true);
                sprintSum += pawn.HorizontalSpeed;
                sprintN++;
            });
            float run = runSum / runN, sprint = sprintSum / sprintN, spent = before - pawn.Stamina;
            // Round an 8 m circle the lean and the turn rate cost some speed; 75% of the target is
            // the gate for both, so the check is that there ARE two speeds, not the exact figures.
            Report("달리기와 전력질주 (Shift)", run > 0.75f * p.moveSpeed && run < 1.15f * p.moveSpeed
                                                  && sprint > 0.75f * p.sprintSpeed && sprint > run * 1.3f && spent > 0.1f,
                $"달리기 {run:F2} m/s (목표 {p.moveSpeed:F1}), 전력질주 {sprint:F2} m/s (목표 {p.sprintSpeed:F1}), "
                + $"2.7초 질주에 스테미나 {before * 100f:F0}% → {pawn.Stamina * 100f:F0}%, 넘어짐 {pawn.Knockdowns}");

            bool winded = false, sprintWhileWinded = false;
            float emptyAt = -1f, t = 0f;
            yield return Sim(12f, () =>
            {
                t += Dt;
                Drive(pawn, Lap(), sprint: true);
                if (pawn.Exhausted && !winded)
                {
                    winded = true;
                    emptyAt = t;
                }
                if (winded && pawn.Exhausted && pawn.Sprinting) sprintWhileWinded = true;
            });
            float slowed = pawn.HorizontalSpeed;
            // Stop, wait for the breather, and the pawn must be able to sprint again.
            Drive(pawn, Vector3.zero);
            float recovered = -1f;
            t = 0f;
            yield return Sim(6f, () =>
            {
                t += Dt;
                if (recovered < 0f && !pawn.Exhausted) recovered = t;
            });
            Report("스테미나가 떨어지면 전력질주 불가 → 회복하면 다시 가능", winded && !sprintWhileWinded && recovered > 0f,
                $"고갈 {Fmt(emptyAt)} 뒤 지침, 지친 동안 질주 {sprintWhileWinded}, 지친 채 속도 {slowed:F2} m/s, "
                + $"다시 질주 가능까지 {Fmt(recovered)} (회복 {p.climbRecover:F1}/초, 대기 {p.staminaRecoverDelay:F1}초, 기준 {p.sprintResume * 100f:F0}%)");
            yield return Clear();
        }

        /// <summary>
        /// Two things that looked wrong from the chase camera. The first run floated: both feet well
        /// off the floor for much of every stride (the run now has a light bounce, ~3 cm, on purpose).
        /// The old sprint's legs rammed the 60 degree hip stop and spilled up to the 35 degree
        /// sideways limit; the sprint now kicks the back foot out on purpose (sprintSplay), and must
        /// stay clear of that limit.
        /// </summary>
        IEnumerator GaitShape()
        {
            var pawn = Spawn(new Vector3(-12f, 0f, -13f), Vector3.forward, "gait");
            yield return Sim(0.6f);
            Drive(pawn, Vector3.forward);
            yield return Sim(0.8f);
            int frames = 0, floating = 0;
            yield return Sim(1.5f, () =>
            {
                frames++;
                float lowest = Mathf.Min(pawn.footL.bounds.min.y, pawn.footR.bounds.min.y);
                if (lowest > 0.06f) floating++;
            });
            float floatShare = 100f * floating / Mathf.Max(1, frames);
            Report("달리기: 공중에 떠다니지 않음", floatShare < 15f && pawn.Knockdowns == 0,
                $"두 발이 다 6 cm 넘게 뜬 시간 {floatShare:F0}% (15% 미만이어야 함, 가벼운 뜀은 정상)");
            yield return Clear();

            pawn = Spawn(new Vector3(-12f, 0f, -13f), Vector3.forward, "gait-sprint");
            yield return Sim(0.6f);
            Drive(pawn, Vector3.forward, sprint: true);
            yield return Sim(0.8f);
            float sideways = 0f;
            yield return Sim(1.5f, () =>
            {
                Quaternion hipsInv = Quaternion.Inverse(pawn.Hips.rotation);
                foreach (var thigh in new[] { BodyId.ThighL, BodyId.ThighR })
                {
                    Vector3 e = (hipsInv * pawn.bodies[(int)thigh].rotation).eulerAngles;
                    sideways = Mathf.Max(sideways, Mathf.Abs(Signed(e.z)));
                }
            });
            float splay = game.tuning.values.sprintSplay;
            Report("전력질주: 다리가 관절 한계에 박히지 않음", sideways < Mathf.Min(32f, splay + 8f) && pawn.Knockdowns == 0,
                $"허벅지가 옆으로 벌어진 최대 각도 {sideways:F0}° (의도한 발 차기 {splay:F0}° + 8° 이하, 관절 한계 35°)");
            yield return Clear();
        }

        /// <summary>
        /// The reported feel bug: with only the move keys held, slowing down or turning sometimes
        /// threw the pawn into the air. Sprints, slams the brakes, turns hard and zig-zags without
        /// ever pressing jump, and measures how far the hips ever get above running height.
        /// </summary>
        IEnumerator NoAutoHop()
        {
            var p = game.tuning.values;
            var pawn = Spawn(new Vector3(-6f, 0f, -6f), Vector3.forward, "no-hop");
            yield return Sim(0.8f);
            float ride = Mathf.Max(p.runLift + p.stepBob, p.sprintLift + p.sprintBob);
            float maxRise = 0f, air = 0f, longestAir = 0f;
            void Watch()
            {
                // The floor here is y = 0, so this is the hips' height above standing height.
                maxRise = Mathf.Max(maxRise, pawn.Hips.position.y - pawn.standHeight);
                air = pawn.Grounded ? 0f : air + Dt;
                longestAir = Mathf.Max(longestAir, air);
            }
            var legs = new (Vector3 move, bool sprint, float seconds)[]
            {
                (Vector3.forward, true, 1.1f),
                (Vector3.right, true, 0.6f),                  // 90 degree turn at full sprint
                (Vector3.left, true, 0.8f),                   // reverse
                (Vector3.zero, false, 0.7f),                  // hard stop
                (Vector3.back, true, 0.8f),
                (Vector3.zero, false, 0.6f),
                ((Vector3.forward + Vector3.right).normalized, false, 0.35f),
                ((Vector3.forward + Vector3.left).normalized, false, 0.35f),
                ((Vector3.forward + Vector3.right).normalized, false, 0.35f),
                ((Vector3.forward + Vector3.left).normalized, false, 0.35f),
                (Vector3.zero, false, 0.5f),
                (Vector3.left, false, 0.6f),
                (Vector3.right, false, 0.6f),
                (Vector3.zero, false, 0.8f),
            };
            foreach (var (move, sprint, seconds) in legs)
            {
                Drive(pawn, move, sprint: sprint);
                yield return Sim(seconds, Watch);
            }
            Report("의도치 않은 점프 없음 (방향키만: 질주·급정지·급회전·지그재그)",
                maxRise < ride + 0.12f && longestAir < 0.25f && pawn.Knockdowns == 0,
                $"골반 최대 상승 {maxRise:F3} m (달리기 들썩임 {ride:F2} m + 0.12 이하여야 함), 가장 긴 공중 {longestAir:F2}s, "
                + $"튐 방지 개입 {pawn.HopsCaught}회, 넘어짐 {pawn.Knockdowns}");
            yield return Clear();
        }

        static Vector3 ComVelocity(RagdollPawn pawn)
        {
            Vector3 p = Vector3.zero;
            float m = 0f;
            foreach (var rb in pawn.bodies)
            {
                p += rb.linearVelocity * rb.mass;
                m += rb.mass;
            }
            return Flat(p / Mathf.Max(0.001f, m));
        }

        /// <summary>
        /// Held by someone else: doing nothing must not get you out, and tapping fast must. Both
        /// halves matter - a struggle that works while idle is not a struggle.
        /// </summary>
        IEnumerator StruggleEscape()
        {
            var holder = Spawn(new Vector3(6f, 0f, 3f), Vector3.forward, "잡는쪽");
            var victim = Spawn(new Vector3(6f, 0f, 3.7f), Vector3.back, "잡힌쪽");
            yield return Sim(0.8f);
            Drive(holder, Vector3.forward * 0.25f, grab: true);
            float held = 0f;
            yield return Sim(1.4f, () => { if (victim.BeingHeld) held += Dt; });
            bool caught = victim.BeingHeld;

            // Phase 1: hold still for a second. The grip must survive.
            float idle = 0f;
            yield return Sim(1f, () =>
            {
                Drive(holder, Vector3.forward * 0.25f, grab: true);
                Drive(victim, Vector3.zero);
                if (victim.BeingHeld) idle += Dt;
            });
            bool stillHeld = victim.BeingHeld;

            // Phase 2: mash it standing still, one click every 0.12 s. It should take 5-10 clicks.
            int plain = -1;
            float highest = 0f;
            yield return MashOut(holder, victim, Vector3.zero, false, n => plain = n, y => highest = Mathf.Max(highest, y));
            // A slow tap (every 0.6 s) must never get out: the meter leaks between clicks. Twenty
            // seconds, because a meter that leaks too slowly still lets it out in the end.
            yield return Regrab(holder, victim);
            bool regrabbed = victim.BeingHeld;
            int slow = -1;
            yield return MashOut(holder, victim, Vector3.zero, false, n => slow = n, null, 0.6f, 20f);
            bool slowHeld = victim.BeingHeld;
            // Phase 3: mash while pulling away (the victim is north of the captor) and jumping.
            yield return Regrab(holder, victim);
            regrabbed &= victim.BeingHeld;
            int power = -1;
            yield return MashOut(holder, victim, Vector3.forward, true, n => power = n, y => highest = Mathf.Max(highest, y));
            Report("버둥대기: 가만히 5~10번, 당기며 점프하면 더 빨리, 천천히 누르면 못 빠져나감",
                caught && stillHeld && regrabbed && plain >= 5 && plain <= 10 && power >= 3 && power < plain
                && slow < 0 && slowHeld && highest < 1.2f,
                $"잡힘 {caught}, 1초 가만히 둔 뒤에도 잡힘 {stillHeld}, 다시 잡힘 {regrabbed}, 탈출까지 클릭: 가만히 {plain}번 / 당기며 점프 {power}번 / "
                + $"0.6초 간격 20초 {(slow < 0 ? "못 빠져나감" : slow + "번")} (끝까지 잡혀 있음 {slowHeld}), 버둥대는 동안 골반 최고 {highest:F2} m (튀어 오름 없음)");
            yield return Clear();
        }

        /// <summary>Click every <paramref name="every"/> s until the victim is free; reports the clicks it took
        /// (-1 if still held after <paramref name="limit"/> s).</summary>
        IEnumerator MashOut(RagdollPawn holder, RagdollPawn victim, Vector3 pull, bool jump, Action<int> clicks,
                            Action<float> height, float every = 0.12f, float limit = 4f)
        {
            float t = 0f, next = 0f;
            int n = 0, escapesBefore = victim.Escapes;
            bool done = false;
            yield return Sim(limit, () =>
            {
                t += Dt;
                Drive(holder, Vector3.forward * 0.25f, grab: !done);
                if (done) { Drive(victim, Vector3.zero); return; }
                bool tap = t >= next;
                if (tap)
                {
                    next = t + every;
                    n++;
                }
                Drive(victim, pull, shove: tap, jump: tap && jump);
                height?.Invoke(victim.Hips.position.y);
                if (victim.Escapes > escapesBefore)
                {
                    done = true;
                    clicks(n);
                }
            });
            if (!done) clicks(-1);
        }

        /// <summary>Put the two back face to face and let the captor take hold again.</summary>
        IEnumerator Regrab(RagdollPawn holder, RagdollPawn victim)
        {
            Drive(holder, Vector3.zero);
            Drive(victim, Vector3.zero);
            yield return Sim(1.6f);
            holder.Teleport(new Vector3(6f, holder.standHeight + 0.02f, 3f), Vector3.forward);
            victim.Teleport(new Vector3(6f, victim.standHeight + 0.02f, 3.7f), Vector3.back);
            yield return Sim(0.6f);
            yield return Sim(1.4f, () => Drive(holder, Vector3.forward * 0.25f, grab: true));
        }

        /// <summary>
        /// A wall is a route with a length: the pawn climbs while stamina lasts and falls when it is
        /// gone. Checks that it gains height, that stamina actually drains, and that it lets go.
        /// </summary>
        IEnumerator Climb()
        {
            var pawn = Spawn(new Vector3(LabLayout.WallFrontX - 1.2f, 0f, LabLayout.WallZ[2]), Vector3.right, "등반");
            yield return Sim(0.6f);
            float startY = pawn.Hips.position.y, startStamina = pawn.Stamina;
            float topY = startY, climbedFor = 0f, lowestStamina = 1f;
            bool everClimbing = false;
            // Hands, measured against the shoulders: each hand should pat the wall above its shoulder
            // and the two should take turns being the higher one, quickly, without the arms being
            // stretched out past climbArmReach. (With arms this short - the head sticks out further
            // than they reach - "above the head" is not a thing a hand can do without the cartoon
            // stretch the playtest found grotesque.)
            var p = game.tuning.values;
            float overHeadL = float.MinValue, overHeadR = float.MinValue, longestArm = 0f;
            int higher = -1, turns = 0;
            yield return Sim(6f, () =>
            {
                Drive(pawn, Vector3.right, grab: true);
                if (pawn.Climbing)
                {
                    everClimbing = true;
                    climbedFor += Dt;
                    overHeadL = Mathf.Max(overHeadL, pawn.handL.Center.y - pawn.bodies[(int)BodyId.ArmL].position.y);
                    overHeadR = Mathf.Max(overHeadR, pawn.handR.Center.y - pawn.bodies[(int)BodyId.ArmR].position.y);
                    longestArm = Mathf.Max(longestArm,
                        Vector3.Distance(pawn.handL.Center, pawn.bodies[(int)BodyId.ArmL].position),
                        Vector3.Distance(pawn.handR.Center, pawn.bodies[(int)BodyId.ArmR].position));
                    int now = pawn.handL.Center.y >= pawn.handR.Center.y ? 0 : 1;
                    if (higher >= 0 && now != higher) turns++;
                    higher = now;
                }
                topY = Mathf.Max(topY, pawn.Hips.position.y);
                lowestStamina = Mathf.Min(lowestStamina, pawn.Stamina);
            });
            // At this arm length a palm on the face can rise only about 0.09 m above its shoulder, and
            // the lower hand measured 0.050 m: a 0.05 bar flipped on rounding from run to run.
            Report("등반: 두 손이 번갈아 어깨 위를 짚음 (팔 안 늘어남)",
                overHeadL > 0.035f && overHeadR > 0.035f && turns >= 8 && longestArm <= p.climbArmReach + 0.05f,
                $"어깨 대비 최고 손 높이 L {overHeadL:+0.00;-0.00} m / R {overHeadR:+0.00;-0.00} m, "
                + $"위쪽 손 교대 {turns}회, 어깨→손 최대 {longestArm:F2} m (한계 {p.climbArmReach:F2})");
            var tr = new StringBuilder();
            float tt = 0f, tnext = 0f;
            yield return Sim(1.2f, () =>
            {
                Drive(pawn, Vector3.right, grab: true);
                tt += Dt;
                if (tt < tnext) return;
                tnext = tt + 0.15f;
                tr.Append($"[{tt:F2} 골반{pawn.Hips.position.y:F2} 앵커{pawn.AnchorPosition.y:F2} "
                    + $"손L{pawn.handL.Center.y:F2}{(pawn.handL.IsHolding ? "O" : "x")} "
                    + $"손R{pawn.handR.Center.y:F2}{(pawn.handR.IsHolding ? "O" : "x")} "
                    + $"등반{(pawn.Climbing ? 1 : 0)}]");
            });
            Info("등반 추적", tr.ToString());
            var ar = new StringBuilder();
            float at = 0f, anext = 0f;
            yield return Sim(2.2f, () =>
            {
                Drive(pawn, Vector3.right, grab: true);
                at += Dt;
                if (at < anext) return;
                anext = at + 0.15f;
                Transform ch = pawn.bodies[(int)BodyId.Chest].transform;
                Vector3 hl = ch.InverseTransformPoint(pawn.handL.Center);
                Vector3 hr = ch.InverseTransformPoint(pawn.handR.Center);
                Vector3 tl = pawn.puppet[(int)BodyId.ArmL].localRotation * Vector3.left;
                ar.Append($"[{at:F2} 손L({hl.x:F2},{hl.y:F2},{hl.z:F2}) 손R({hr.x:F2},{hr.y:F2},{hr.z:F2}) "
                    + $"목표L({tl.x:F2},{tl.y:F2},{tl.z:F2}) 등반{(pawn.Climbing ? 1 : 0)}]");
            });
            Info("등반 팔 추적 (가슴 기준)", ar.ToString());
            float gain = topY - startY;
            Report("등반: 벽을 타고 올라감", everClimbing && gain > 0.6f,
                $"오른 높이 {gain:F2} m, 매달린 시간 {climbedFor:F1}s, 스테미나 {startStamina:F2} → {lowestStamina:F2}");

            yield return Clear();

            // Same wall, but going up and down so the route never ends: stamina has to run out.
            // The overhang lane, because it has no ledge to rest on - stamina has to be what stops it.
            var hanger = Spawn(new Vector3(LabLayout.ClimbX[1], 0f, LabLayout.ClimbFaceZ - 2.2f), Vector3.forward, "매달림");
            yield return Sim(0.6f);
            // Climb clear of the ground first, then ride up and down so the route never ends and
            // stamina has to be what stops it. "Clear" is a height, not a time: a fixed six seconds
            // of climbing reaches the top of the 5 m lane on a faster climb, and the pawn then rests
            // on the lip instead of running out.
            float phase = 0f, highest = 0f, heightAtEmpty = -1f, climbFor = 6f;
            float hangerStart = hanger.Hips.position.y, hangNext = 0f;
            bool ranOut = false, letGo = false;
            var hangTrace = new StringBuilder();
            yield return Sim(28f, () =>
            {
                phase += Dt;
                if (phase >= hangNext && phase < 4f)
                {
                    hangNext = phase + 0.1f;
                    hangTrace.Append($"[{phase:F1} z{LabLayout.ClimbFaceZ - hanger.Hips.position.z:F2} y{hanger.Hips.position.y:F2} {hanger.State} "
                                     + $"c{(hanger.Climbing ? 1 : 0)} g{(hanger.Grounded ? 1 : 0)} h{(hanger.handL.IsHolding ? 1 : 0)}{(hanger.handR.IsHolding ? 1 : 0)} "
                                     + $"t{hanger.HipsTilt:F0} s{hanger.Stamina:F2}]");
                }
                if (phase < climbFor && hanger.Hips.position.y - hangerStart >= 2.5f) climbFor = phase;
                bool up = phase < climbFor || ((int)((phase - climbFor) / 0.5f) & 1) == 1;
                Drive(hanger, up ? Vector3.forward : Vector3.back, grab: true);
                if (!ranOut) highest = Mathf.Max(highest, hanger.Hips.position.y);
                if (hanger.Stamina > 0.001f) return;
                if (!ranOut) heightAtEmpty = hanger.Hips.position.y;
                ranOut = true;
                if (!hanger.Climbing) letGo = true;
            });
            Info("매달림 추적", hangTrace.ToString());
            Report("등반: 스테미나가 떨어지면 손을 놓음", ranOut && letGo,
                $"스테미나 고갈 {ranOut}, 손 놓음 {letGo}, 최고 {highest:F2} m, 고갈 시점 {heightAtEmpty:F2} m → 최종 {hanger.Hips.position.y:F2} m");
            yield return Clear();
        }

        /// <summary>
        /// The climbing bugs from the playtests, one check each: starting a climb put the body inside
        /// the wall (only the hands showed), moving sideways stretched the arms across the face (and
        /// now has to carry on round the corner), the top was not reliably climbed onto, letting go
        /// shoved the pawn away from the wall, and a pawn that had climbed onto a ledge tipped back
        /// off it.
        /// </summary>
        IEnumerator ClimbBugs()
        {
            var p = game.tuning.values;
            float face = LabLayout.WallFrontX;
            float z = LabLayout.WallZ[2];                 // the 4 m wall, 6 m wide
            // Start 1 m from its +Z end, so one stamina bar covers reaching the end and coming back.
            var pawn = Spawn(new Vector3(face - 1.2f, 0f, z + 2f), Vector3.right, "climb-bugs");
            yield return Sim(0.6f);
            // 1. Run into it and start climbing: the body must stay in front of the face.
            float closest = 9f, longestArm = 0f;
            bool climbed = false;
            float Arm() => Mathf.Max(
                Vector3.Distance(pawn.handL.Center, pawn.bodies[(int)BodyId.ArmL].position),
                Vector3.Distance(pawn.handR.Center, pawn.bodies[(int)BodyId.ArmR].position));
            yield return Sim(1.6f, () =>
            {
                Drive(pawn, Vector3.right, grab: true);
                climbed |= pawn.Climbing;
                closest = Mathf.Min(closest, face - pawn.Hips.position.x);
                if (pawn.Climbing) longestArm = Mathf.Max(longestArm, Arm());
            });
            Report("등반 시작: 몸이 벽 속으로 들어가지 않음", climbed && closest > 0.15f,
                $"등반 {climbed}, 골반과 벽면 최소 거리 {closest:F2} m (치마 반지름 0.14, 0.15 초과여야 함)");

            // 2. Sideways to the end of the wall and on round the corner onto its side face: the arms
            // stay about arm-length all the way. There is no camera here, so the test keeps pressing
            // "left" in the pawn's own terms: +Z on the front face, +X once it faces the side face.
            float half = LabLayout.WallWidth * 0.5f;
            bool stayed = true, rounded = false;
            float sideZ = 0f, alongSide = 0f;
            yield return Sim(2.8f, () =>
            {
                bool onSide = pawn.Climbing && pawn.Facing.z < -0.7f;
                rounded |= onSide;
                Drive(pawn, onSide ? Vector3.right : Vector3.forward, grab: true);
                if (!pawn.Climbing) stayed = false;
                longestArm = Mathf.Max(longestArm, Arm());
                if (onSide)
                {
                    sideZ = pawn.Hips.position.z;
                    alongSide = Mathf.Max(alongSide, pawn.Hips.position.x - face);
                }
            });
            Report("등반: 옆으로 가도 팔이 안 늘어나고 모서리를 돌아감",
                longestArm <= p.climbArmReach + 0.05f && stayed && rounded && sideZ > z + half && alongSide > 0.2f,
                $"어깨→손 최대 {longestArm:F2} m (한계 {p.climbArmReach:F2}), 벽에 붙어 있음 {stayed}, 모서리 돌아감 {rounded}, "
                + $"옆면에서 벽면까지 {sideZ - z - half:F2} m, 옆면을 따라 {alongSide:F2} m");

            // 3. Letting go on the side face: straight down, not shoved away from the wall.
            float z0 = pawn.Hips.position.z, drift = 0f;
            yield return Sim(1.5f, () =>
            {
                Drive(pawn, Vector3.zero);
                drift = Mathf.Max(drift, pawn.Hips.position.z - z0);
            });
            Report("등반 중 손을 놓으면 그대로 떨어짐", !pawn.Climbing && drift < 0.45f,
                $"벽에서 밀려난 거리 {drift:F2} m (0.45 미만)");
            yield return Clear();

            // 4. The 2 m wall to the top: must end up standing on it.
            var top = Spawn(new Vector3(face - 1.2f, 0f, LabLayout.WallZ[0]), Vector3.right, "climb-top");
            yield return Sim(0.6f);
            float wallTop = LabLayout.WallHeights[0], standAt = -1f, t = 0f;
            yield return Sim(6f, () =>
            {
                t += Dt;
                // Hands off once it stands up there: the wall is 5 m deep and the floor behind it ends.
                Drive(top, standAt < 0f ? Vector3.right : Vector3.zero, grab: standAt < 0f);
                if (standAt < 0f && !top.Climbing && top.Grounded && top.Hips.position.y > wallTop + 0.1f
                    && top.Hips.position.x > face + 0.1f) standAt = t;
            });
            Report("등반: 꼭대기에 올라섬", standAt > 0f && top.Knockdowns == 0,
                $"올라선 시각 {Fmt(standAt)}, 최종 골반 높이 {top.Hips.position.y:F2} m (벽 {wallTop:F0} m), 넘어짐 {top.Knockdowns}");
            yield return Clear();

            // 5. The playtest bounce: onto the big wall's first ledge (0.75 m deep), keys held a moment
            // longer, then let go. It has to stay up there, not tip back off the edge.
            var ledge = Spawn(new Vector3(LabLayout.ClimbX[0], 0f, LabLayout.ClimbFaceZ - 1.2f), Vector3.forward, "climb-ledge");
            yield return Sim(0.6f);
            float ledgeTop = LabLayout.LedgeStepHeight, landedAt = -1f, lowest = 99f, lt = 0f, ledgeNext = 0f;
            var ledgeTrace = new StringBuilder();
            yield return Sim(7f, () =>
            {
                lt += Dt;
                if (lt >= ledgeNext && lt > 2f && lt < 5f)
                {
                    ledgeNext = lt + 0.05f;
                    ledgeTrace.Append($"[{lt:F2} z{LabLayout.ClimbFaceZ - ledge.Hips.position.z:F2} y{ledge.Hips.position.y:F2} {ledge.State} "
                                      + $"c{(ledge.Climbing ? 1 : 0)} g{(ledge.Grounded ? 1 : 0)} t{ledge.HipsTilt:F0} "
                                      + $"vz{ledge.Hips.linearVelocity.z:F1} vy{ledge.Hips.linearVelocity.y:F1} a{LabLayout.ClimbFaceZ - ledge.AnchorPosition.z:F2}]");
                }
                bool up = landedAt < 0f || lt < landedAt + 0.3f;
                Drive(ledge, up ? Vector3.forward : Vector3.zero, grab: up);
                if (landedAt < 0f && !ledge.Climbing && ledge.Grounded && ledge.Hips.position.y > ledgeTop + 0.1f) landedAt = lt;
                if (landedAt > 0f && lt > landedAt + 0.3f) lowest = Mathf.Min(lowest, ledge.Hips.position.y);
            });
            Info("턱 추적", ledgeTrace.ToString());
            Report("등반: 턱에 올라선 뒤 뒤로 떨어지지 않음", landedAt > 0f && lowest > ledgeTop && ledge.Knockdowns == 0,
                $"올라선 시각 {Fmt(landedAt)}, 이후 최저 골반 높이 {(lowest > 90f ? 0f : lowest):F2} m (턱 {ledgeTop:F0} m), 넘어짐 {ledge.Knockdowns}");
            yield return Clear();
        }

        /// <summary>
        /// Physical climbing needs something a hand can actually touch. Runs the same attempt at each
        /// of the three climbing faces and reports the height gained and whether a hand ever held on,
        /// so "which surfaces can this body climb" is a measurement rather than an opinion.
        /// </summary>
        IEnumerator ClimbSurfaces()
        {
            string[] names = { "큰 벽 + 바위 (12m)", "역경사 20도", "곡면" };
            float[] seconds = { 34f, 7f, 7f };
            for (int i = 0; i < 3; i++)
            {
                var pawn = Spawn(new Vector3(LabLayout.ClimbX[i], 0f, LabLayout.ClimbFaceZ - 2.2f), Vector3.forward, "등반" + i);
                yield return Sim(0.6f);
                float start = pawn.Hips.position.y, top = start, holdTime = 0f, handTop = 0f;
                float lastStamina = 1f;
                int grips = 0, rests = 0;
                bool wasHolding = false, onTop = false;
                int lane = i;
                var trace = new StringBuilder();
                float traceT = 0f, traceNext = 0f;
                yield return Sim(seconds[i], () =>
                {
                    traceT += Dt;
                    if (traceT >= traceNext && traceT < 3f)
                    {
                        traceNext += 0.1f;
                        trace.Append($"[{traceT:F1} z{LabLayout.ClimbFaceZ - pawn.Hips.position.z:F2} y{pawn.Hips.position.y:F2} "
                                     + $"{pawn.State} c{(pawn.Climbing ? 1 : 0)} g{(pawn.Grounded ? 1 : 0)} "
                                     + $"h{(pawn.handL.IsHolding ? 1 : 0)}{(pawn.handR.IsHolding ? 1 : 0)} t{pawn.HipsTilt:F0} v{pawn.HorizontalSpeed:F1}]");
                    }
                    // The shape-test lanes end in a ~1 m deep top. Once the pawn stands up there, let go
                    // of the keys: holding "forward" ran it off the back, and the final height then
                    // measured the fall behind the lane, not the climb. The big wall keeps going - its
                    // tops are rest ledges and the next block starts behind them.
                    onTop |= lane > 0 && !pawn.Climbing && pawn.Grounded && pawn.Hips.position.y > 1.5f;
                    if (onTop) Drive(pawn, Vector3.zero);
                    else Drive(pawn, Vector3.forward, grab: true);
                    top = Mathf.Max(top, pawn.Hips.position.y);
                    if (pawn.Stamina > lastStamina + 0.002f && pawn.Hips.position.y > 1.5f) rests++;
                    lastStamina = pawn.Stamina;
                    bool holding = pawn.handL.IsHolding || pawn.handR.IsHolding;
                    if (holding)
                    {
                        holdTime += Dt;
                        handTop = Mathf.Max(handTop, Mathf.Max(pawn.handL.Center.y, pawn.handR.Center.y));
                        if (!wasHolding) grips++;
                    }
                    wasHolding = holding;
                });
                if (lane == 1) Info("등반 표면 추적: 역경사", trace.ToString());
                Info($"등반 표면: {names[i]}",
                    $"오른 높이 {top - start:F2} m, 최종 {pawn.Hips.position.y:F2} m, "
                    + $"바위에서 쉰 프레임 {rests}, 남은 스테미나 {pawn.Stamina:F2}");
                // The curved lane is a stack of slabs: the chest rays slip through the seams between
                // them, which once read as the top and climbed the pawn into the slab above.
                if (lane > 0)
                    Report($"등반: {names[i]} 꼭대기에 올라섬", onTop && pawn.Knockdowns == 0,
                        $"올라섬 {onTop}, 최고 골반 높이 {top:F2} m (벽 {LabLayout.ClimbHeight:F0} m), 넘어짐 {pawn.Knockdowns}");
                yield return Clear();
            }
        }

        IEnumerator JumpCheck()
        {
            var pawn = Spawn(new Vector3(5f, 0f, -10f), Vector3.forward, "jump");
            yield return Sim(1f);
            float y0 = pawn.Hips.position.y, maxY = y0;
            Drive(pawn, Vector3.zero, jump: true);
            yield return Sim(2f, () => maxY = Mathf.Max(maxY, pawn.Hips.position.y));
            float g = -Physics.gravity.y;
            float v = game.tuning.values.jumpImpulse;
            float ideal = v * v / (2f * g);
            Report("점프 후 착지", maxY - y0 > 0.5f * ideal && pawn.Knockdowns == 0 && pawn.State == PawnState.Active,
                $"높이 {maxY - y0:F2} m (이론 {ideal:F2} m), 넘어짐 {pawn.Knockdowns}");
            yield return Clear();
        }

        /// <summary>
        /// The second half of the hop bug: jumping while the body was already rising added the jump
        /// on top, so jumping out of a bump went twice as high. The jump now sets the rise instead.
        /// </summary>
        IEnumerator JumpNoStack()
        {
            var pawn = Spawn(new Vector3(5f, 0f, -6f), Vector3.forward, "jump-stack");
            yield return Sim(1f);
            float y0 = pawn.Hips.position.y, maxY = y0;
            pawn.AddVelocity(Vector3.up * 2.5f);          // a bump already under way...
            Drive(pawn, Vector3.zero, jump: true);        // ...and jump pressed on top of it
            yield return Sim(2f, () => maxY = Mathf.Max(maxY, pawn.Hips.position.y));
            float g = -Physics.gravity.y;
            float v = game.tuning.values.jumpImpulse;
            float ideal = v * v / (2f * g);
            float stacked = (v + 2.5f) * (v + 2.5f) / (2f * g);
            Report("점프가 튀는 중에 겹쳐지지 않음", maxY - y0 < ideal * 1.25f,
                $"높이 {maxY - y0:F2} m (정상 점프 {ideal:F2} m, 겹쳤다면 {stacked:F2} m)");
            yield return Clear();
        }

        IEnumerator GetUp()
        {
            var p = game.tuning.values;
            var pawn = Spawn(new Vector3(-5f, 0f, -10f), Vector3.forward, "getup");
            yield return Sim(1f);
            pawn.Knockdown("테스트");
            pawn.AddVelocity(new Vector3(3f, 1f, 0f));
            float t = 0f, rise = -1f, stand = -1f;
            yield return Sim(4f, () =>
            {
                t += Dt;
                if (rise < 0f && pawn.State == PawnState.GettingUp) rise = t;
                if (stand < 0f && rise >= 0f && pawn.State == PawnState.Active) stand = t;
            });
            float tilt = pawn.HipsTilt;
            Report("넉다운 → 자동 기상", rise > 0f && Mathf.Abs(rise - p.getUpDelay) < 0.1f && stand > 0f && tilt < 15f,
                $"기상 시작 {rise:F2}s (설정 {p.getUpDelay:F2}), 서기 완료 {stand:F2}s, 4초 뒤 기울기 {tilt:F1}°");
            yield return Clear();
        }

        IEnumerator Fall()
        {
            var pawn = Spawn(new Vector3(-8f, 6f, -8f), Vector3.forward, "fall");
            float t = 0f, down = -1f;
            yield return Sim(3.5f, () =>
            {
                t += Dt;
                if (down < 0f && pawn.State == PawnState.Ragdoll) down = t;
            });
            Report("6 m 낙하 → 래그돌 → 기상", pawn.Knockdowns >= 1 && pawn.State != PawnState.Ragdoll,
                $"래그돌 시작 {down:F2}s ({pawn.LastKnockdownCause}), 3.5초 뒤 상태 {pawn.State}, 기울기 {pawn.HipsTilt:F0}°");
            yield return Clear();
        }

        IEnumerator Slope()
        {
            foreach (int index in new[] { 0, 1, 2 })
            {
                float angle = LabLayout.SlopeAngles[index];
                float z = LabLayout.SlopeZ[index];
                float edge = LabLayout.PlatformEdgeX;
                float finish = LabLayout.SlopeBottomX(angle) + 2f;
                Vector3 start = new Vector3(LabLayout.PlatformBackX + 0.8f, LabLayout.PlatformHeight, z);

                // Both run up at full speed; the roller throws itself down as it reaches the edge.
                var runner = Spawn(start, Vector3.right, "slope-run");
                yield return Sim(0.6f);
                Drive(runner, Vector3.right);
                float t = 0f, runEdge = -1f, runEnd = -1f, runMax = 0f;
                yield return Sim(7f, () =>
                {
                    t += Dt;
                    float x = runner.Hips.position.x;
                    runMax = Mathf.Max(runMax, runner.HorizontalSpeed);
                    if (runEdge < 0f && x > edge) runEdge = t;
                    if (runEnd < 0f && x > finish) runEnd = t;
                });
                int runFalls = runner.Knockdowns;
                yield return Clear();

                var roller = Spawn(start, Vector3.right, "slope-roll");
                yield return Sim(0.6f);
                Drive(roller, Vector3.right);
                t = 0f;
                float rollEdge = -1f, rollEnd = -1f, rollMax = 0f;
                yield return Sim(7f, () =>
                {
                    t += Dt;
                    float x = roller.Hips.position.x;
                    rollMax = Mathf.Max(rollMax, roller.HorizontalSpeed);
                    if (rollEdge < 0f && x > edge - 0.3f)
                    {
                        rollEdge = t;
                        roller.Knockdown("테스트: 몸 던지기");
                        roller.AddVelocity(new Vector3(1f, 0.5f, 0f));
                    }
                    if (rollEnd < 0f && x > finish) rollEnd = t;
                });
                float runDescent = runEnd >= 0f && runEdge >= 0f ? runEnd - runEdge : -1f;
                float rollDescent = rollEnd >= 0f && rollEdge >= 0f ? rollEnd - rollEdge : -1f;
                bool faster = rollDescent > 0f && (runDescent < 0f || rollDescent < runDescent);
                Report($"경사 {angle:F0}°: 몸 던지기가 더 빠른가 (명세 기준 3)", faster,
                    $"모서리→바닥 달리기 {Fmt(runDescent)} (최고 수평 {runMax:F1} m/s, 넘어짐 {runFalls}) / 구르기 {Fmt(rollDescent)} (최고 수평 {rollMax:F1} m/s, 넘어짐 {roller.Knockdowns})");
                yield return Clear();
            }
        }

        /// <summary>
        /// A dive is meant to be the fast way down a hill. Same run-up as the slope test; one pawn
        /// runs down, the other dives off the edge.
        /// </summary>
        IEnumerator DiveSlope()
        {
            foreach (int index in new[] { 1, 2 })
            {
                float angle = LabLayout.SlopeAngles[index];
                float z = LabLayout.SlopeZ[index];
                float edge = LabLayout.PlatformEdgeX;
                float finish = LabLayout.SlopeBottomX(angle) + 2f;
                Vector3 start = new Vector3(LabLayout.PlatformBackX + 0.8f, LabLayout.PlatformHeight, z);
                float runDescent = -1f, diveDescent = -1f;
                int falls = 0;
                bool dove = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    bool dive = pass == 1;
                    var pawn = Spawn(start, Vector3.right, dive ? "slope-dive" : "slope-run2");
                    yield return Sim(0.6f);
                    float t = 0f, atEdge = -1f, atEnd = -1f;
                    bool pressed = false;
                    yield return Sim(7f, () =>
                    {
                        t += Dt;
                        float x = pawn.Hips.position.x;
                        bool tap = dive && !pressed && x > edge - 0.3f;
                        if (tap) pressed = true;
                        Drive(pawn, Vector3.right, shove: tap);
                        if (atEdge < 0f && x > edge - 0.3f) atEdge = t;
                        if (atEnd < 0f && x > finish) atEnd = t;
                        if (dive) dove |= pawn.Diving;
                    });
                    float descent = atEnd >= 0f && atEdge >= 0f ? atEnd - atEdge : -1f;
                    if (dive)
                    {
                        diveDescent = descent;
                        falls = pawn.Knockdowns;
                    }
                    else runDescent = descent;
                    yield return Clear();
                }
                bool faster = dove && diveDescent > 0f && (runDescent < 0f || diveDescent < runDescent);
                Report($"경사 {angle:F0}°: 슬라이딩이 달려 내려가기보다 빠름", faster,
                    $"모서리→바닥 달리기 {Fmt(runDescent)} / 슬라이딩 {Fmt(diveDescent)} (슬라이딩 {dove}, 넘어짐으로 기록 {falls})");
            }
        }

        static string Fmt(float seconds) => seconds < 0f ? "도착 못 함" : $"{seconds:F2}s";

        static float Signed(float degrees) => degrees > 180f ? degrees - 360f : degrees;

        IEnumerator Contact()
        {
            var p = game.tuning.values;
            var a = Spawn(new Vector3(8f, 0f, 3f), Vector3.forward, "contact-a");
            var b = Spawn(new Vector3(8f, 0f, 4.4f), Vector3.back, "contact-b");
            yield return Sim(0.8f);
            Drive(a, Vector3.forward * 0.5f);
            Drive(b, Vector3.back * 0.5f);
            float minTarget = 1f, minStiff = 1f;
            bool touched = false;
            yield return Sim(1.2f, () =>
            {
                minTarget = Mathf.Min(minTarget, a.TargetStiffness);
                minStiff = Mathf.Min(minStiff, a.EffectiveStiffness);
                touched |= a.Touching;
            });
            Drive(a, Vector3.back * 0.6f);
            Drive(b, Vector3.forward * 0.6f);
            yield return Sim(0.8f);
            Drive(a, Vector3.zero);
            Drive(b, Vector3.zero);
            yield return Sim(1.2f);
            float after = a.EffectiveStiffness;
            Report("몸이 닿으면 흐물 → 떨어지면 복귀", touched && minTarget <= p.contactStiffnessMultiplier + 0.01f && after > 0.9f,
                $"접촉 {touched}, 목표 배율 최저 {minTarget:F2}, 실제 강성 최저 {minStiff:F2}, 떨어진 뒤 {after:F2}, 넘어짐 A{a.Knockdowns}/B{b.Knockdowns}");
            yield return Clear();
        }

        IEnumerator GrabDrag()
        {
            var a = Spawn(new Vector3(-8f, 0f, 3f), Vector3.forward, "grabber");
            var b = Spawn(new Vector3(-8f, 0f, 3.75f), Vector3.back, "held");
            yield return Sim(0.8f);
            Drive(a, Vector3.forward * 0.25f, grab: true);
            float t = 0f, grabbed = -1f;
            yield return Sim(2f, () =>
            {
                t += Dt;
                if (grabbed < 0f && a.Grabbing)
                {
                    grabbed = t;
                    Drive(a, Vector3.zero, grab: true);
                }
            });
            Vector3 a0 = a.Hips.position, b0 = b.Hips.position;
            Drive(a, Vector3.back, grab: true);
            float aStiff = 1f, bStiff = 1f;
            yield return Sim(1.5f, () =>
            {
                aStiff = Mathf.Min(aStiff, a.EffectiveStiffness);
                bStiff = Mathf.Min(bStiff, b.EffectiveStiffness);
            });
            float pulled = Vector3.Dot(b.Hips.position - b0, Vector3.back);
            float walked = Vector3.Dot(a.Hips.position - a0, Vector3.back);
            Report("잡고 끌기", grabbed >= 0f && pulled > 0.6f,
                $"잡기까지 {Fmt(grabbed)}, 끄는 쪽 이동 {walked:F2} m, 끌려온 거리 {pulled:F2} m, 손 L:{a.handL.IsHolding} R:{a.handR.IsHolding}, 최저 강성 A {aStiff:F2} / B {bStiff:F2}, 넘어짐 A{a.Knockdowns}/B{b.Knockdowns}");
            Drive(a, Vector3.zero);
            yield return Clear();
        }

        /// <summary>
        /// Left click on the move: a head-first dive tackle that floors whoever it hits, does not
        /// count as a knockdown for the diver, and gets back up on its own. Also checks it stays
        /// short on the flat (the first version slid about 12 m), lands on its front with the arms
        /// out, stays limp until it gets up, and is not dragged along the floor while it rises.
        /// </summary>
        IEnumerator DiveTackle()
        {
            var a = Spawn(new Vector3(-12f, 0f, -2f), Vector3.right, "diver");
            var b = Spawn(new Vector3(-7f, 0f, -2f), Vector3.left, "target");
            yield return Sim(1f);
            Vector3 a0 = a.Hips.position;
            Drive(a, Vector3.right);
            yield return Sim(0.45f);
            float speedBefore = a.HorizontalSpeed, peak = 0f, t = 0f, upAt = -1f, headAhead = -9f, handsAhead = -9f, prone = -1f, supine = -1f;
            float towed = 0f, bodySpring = 0f, armSpring = float.MaxValue;
            bool dove = false, stiffened = false;
            Vector3 slideStart = a.Hips.position;
            float slid = 0f;
            Drive(a, Vector3.right, shove: true);
            yield return Sim(3f, () =>
            {
                t += Dt;
                Drive(a, Vector3.right);
                // Limp from the click until it gets up: no Active step in between.
                if (dove && upAt < 0f && a.State == PawnState.Active) stiffened = true;
                dove |= a.Diving;
                if (a.Diving)
                {
                    peak = Mathf.Max(peak, a.HorizontalSpeed);
                    slid = Flat(a.Hips.position - slideStart).magnitude;
                    // Head first, arms reaching: head in front of the feet, hands in front of the chest.
                    Vector3 feet = (a.bodies[(int)BodyId.FootL].position + a.bodies[(int)BodyId.FootR].position) * 0.5f;
                    headAhead = Mathf.Max(headAhead, Vector3.Dot(a.bodies[(int)BodyId.Head].position - feet, Vector3.right));
                    Vector3 hands = (a.handL.Center + a.handR.Center) * 0.5f;
                    handsAhead = Mathf.Max(handsAhead, Vector3.Dot(hands - a.bodies[(int)BodyId.Chest].position, Vector3.right));
                    // On its front (the hips' forward pointing at the floor), never flipped onto its back.
                    float down = Vector3.Dot(a.Hips.transform.forward, Vector3.down);
                    prone = Mathf.Max(prone, down);
                    supine = Mathf.Max(supine, -down);
                    // Limp means no drive on the torso at all; the arms are the only thing held.
                    bodySpring = Mathf.Max(bodySpring, a.joints[(int)BodyId.Chest].slerpDrive.positionSpring);
                    armSpring = Mathf.Min(armSpring, a.joints[(int)BodyId.ArmL].slerpDrive.positionSpring,
                                          a.joints[(int)BodyId.ArmR].slerpDrive.positionSpring);
                }
                if (dove && upAt < 0f && !a.Diving) upAt = t;
                // Still holding forward while it gets up: the body must not be dragged along the floor.
                if (upAt >= 0f && a.HipsTilt > 50f) towed = Mathf.Max(towed, a.HorizontalSpeed);
            });
            Drive(a, Vector3.zero);
            bool floored = b.Knockdowns > 0;
            Report("좌클릭 다이빙 태클: 상대가 넘어지고 나는 넉다운으로 안 셈",
                dove && floored && a.Knockdowns == 0 && a.State != PawnState.Ragdoll,
                $"태클 {dove}, 속도 {speedBefore:F1} → 최고 {peak:F1} m/s, 상대 넘어짐 {b.Knockdowns} ({b.LastKnockdownCause}), "
                + $"태클 성공 {a.Tackles}, 태클한 쪽 넉다운 {a.Knockdowns}, 일어나기 시작 {Fmt(upAt)}, 이동 {Flat(a.Hips.position - a0).magnitude:F1} m, 최종 상태 {a.State}");
            Report("다이빙: 머리부터 엎어지며 팔을 앞으로, 등으로 안 뒤집힘, 일어날 때까지 흐물",
                headAhead > 0.3f && handsAhead > 0.1f && prone > 0.5f && supine < 0.5f && !stiffened && slid < 7f
                && bodySpring < 0.01f && armSpring > 1f && towed < 2f,
                $"머리가 발보다 앞선 최대 {headAhead:F2} m, 손이 가슴보다 앞선 최대 {handsAhead:F2} m, "
                + $"엎드림 {prone:F2} / 뒤집힘 {supine:F2} (골반 앞쪽이 아래·위를 향한 정도), 도중에 굳음 {stiffened}, 미끄러진 거리 {slid:F1} m (7 m 미만), "
                + $"다이빙 중 가슴 강성 {bodySpring:F0} (0이어야 흐물) / 팔 강성 {armSpring:F0}, 일어나며 W를 누르고 있을 때 누운 채 끌려간 속도 {towed:F2} m/s (2 미만)");
            yield return Clear();

            // On its own, from standing: a short slip forward, then back up by itself.
            var c = Spawn(new Vector3(-12f, 0f, 3f), Vector3.right, "flop");
            yield return Sim(1f);
            Vector3 c0 = c.Hips.position;
            Drive(c, Vector3.zero, shove: true);
            bool flopped = false;
            yield return Sim(2.5f, () => flopped |= c.Diving);
            float flop = Flat(c.Hips.position - c0).magnitude;
            Report("제자리 다이빙 → 스스로 일어남", flopped && c.State == PawnState.Active && c.Knockdowns == 0 && c.HipsTilt < 25f,
                $"다이빙 {flopped}, 앞으로 {flop:F2} m, 2.5초 뒤 상태 {c.State}, 기울기 {c.HipsTilt:F0}°");
            yield return Clear();
        }

        IEnumerator Bar()
        {
            float saved = game.bar.degreesPerSecond;
            game.bar.degreesPerSecond = 120f;
            var pawn = Spawn(LabLayout.BarCenter + new Vector3(3.5f, 0f, 0f), Vector3.forward, "bar");
            float t = 0f, hit = -1f;
            yield return Sim(5f, () =>
            {
                t += Dt;
                if (hit < 0f && pawn.Knockdowns > 0) hit = t;
            });
            Report("회전 봉에 맞으면 넉다운", hit >= 0f, $"넉다운 {Fmt(hit)} ({pawn.LastKnockdownCause}), 봉 120°/s");
            game.bar.degreesPerSecond = saved;
            yield return Clear();
        }

        IEnumerator Beam()
        {
            var pawn = Spawn(new Vector3(0f, 0f, LabLayout.BeamStartZ - 0.3f), Vector3.back, "beam");
            yield return Sim(0.8f);
            Drive(pawn, Vector3.back * 0.6f);
            float t = 0f, fell = -1f;
            yield return Sim(3f, () =>
            {
                t += Dt;
                if (fell < 0f && pawn.Hips.position.y < -0.5f) fell = t;
            });
            float progress = LabLayout.BeamStartZ - pawn.Hips.position.z;
            Info("외줄 0.6 m 걷기 (속도 60%)", fell < 0f ? $"떨어지지 않음, {progress:F1} m 진행" : $"{fell:F2}s에 떨어짐, {progress:F1} m 진행");
            yield return Clear();
        }

        IEnumerator WallClimb()
        {
            yield return WallClimb(0);
            yield return WallClimb(1);
        }

        IEnumerator WallClimb(int wall)
        {
            // Solo attempt: run in holding grab, jump near the wall, jump again while hanging.
            float wallZ = LabLayout.WallZ[wall];
            float wallTop = LabLayout.WallHeights[wall];
            var pawn = Spawn(new Vector3(LabLayout.WallFrontX - 3f, 0f, wallZ), Vector3.right, "climber");
            yield return Sim(0.6f);
            Drive(pawn, Vector3.right, grab: true);
            bool jumped = false, pulled = false, onTop = false;
            float t = 0f, hangTime = -1f, maxY = 0f, maxHandY = 0f, pullTime = -1f;
            var trace = new StringBuilder();
            yield return Sim(5f, () =>
            {
                t += Dt;
                float x = pawn.Hips.position.x;
                maxY = Mathf.Max(maxY, pawn.Hips.position.y);
                maxHandY = Mathf.Max(maxHandY, Mathf.Max(pawn.handL.Center.y, pawn.handR.Center.y));
                if (!jumped && x > LabLayout.WallFrontX - 1.2f)
                {
                    jumped = true;
                    Drive(pawn, Vector3.right, grab: true, jump: true);
                }
                if (hangTime < 0f && pawn.HoldingEnvironment()) hangTime = t;
                if (!pulled && hangTime >= 0f && t > hangTime + 0.3f)
                {
                    pulled = true;
                    pullTime = t;
                    Drive(pawn, Vector3.right, grab: true, jump: true);
                }
                if (pulled && t > hangTime + 1.2f) Drive(pawn, Vector3.right, grab: false);
                if (pullTime >= 0f && t - pullTime < 0.8f && Mathf.Repeat(t - pullTime, 0.1f) < Dt)
                    trace.Append($"[{t - pullTime:F1}s y{pawn.Hips.position.y:F2} vy{pawn.Hips.linearVelocity.y:F1} x{x - LabLayout.WallFrontX:F2} 잡음{(pawn.Grabbing ? 1 : 0)}] ");
                onTop |= pawn.Grounded && pawn.Hips.position.y > wallTop + 0.1f && x > LabLayout.WallFrontX + 0.3f;
            });
            Info($"벽 {wallTop:F0}m 혼자 오르기 (잡기+점프, 모서리 잡고 점프)", $"매달림 {Fmt(hangTime)}, 손 최고 {maxHandY:F2} m, 골반 최고 {maxY:F2} m, 위에 올라섬 {onTop} " + trace);
            yield return Clear();
        }

        IEnumerator Crowd()
        {
            var pawns = new[]
            {
                Spawn(new Vector3(5f, 0f, 8f), Vector3.forward, "crowd-1"),
                Spawn(new Vector3(5.6f, 0f, 8.3f), Vector3.left, "crowd-2"),
                Spawn(new Vector3(4.5f, 0f, 8.5f), Vector3.right, "crowd-3"),
            };
            yield return Sim(0.5f);
            float maxSpeed = 0f;
            yield return Sim(3f, () =>
            {
                foreach (var pawn in pawns)
                    foreach (var rb in pawn.bodies)
                        maxSpeed = Mathf.Max(maxSpeed, rb.linearVelocity.magnitude);
            });
            int falls = pawns.Sum(p => p.Knockdowns);
            Report("세 명 밀착 안정성", maxSpeed < 6f, $"부위 최대 속도 {maxSpeed:F2} m/s, 넘어짐 {falls}");
            yield return Clear();
        }

        /// <summary>Pose path without Steam: capture on one pawn, pack, unpack, draw on a puppet, compare.</summary>
        IEnumerator NetLoopback()
        {
            var sent = new RagdollNetInput
            {
                move = new Vector2(0.5f, -0.25f), jump = true, grab = true, sprint = true,
                ability2 = true, interact = true, aim = new Vector3(0.3f, -0.4f, 0.866f).normalized,
            };
            byte[] inputBytes = RagdollNetProtocol.Input(99, 7, 1234, sent);
            bool inputOk = RagdollNetProtocol.ReadInput(inputBytes, 99, out uint seq, out uint clientTime, out var got);
            float aimError = Vector3.Angle(sent.aim, got.aim);
            inputOk &= inputBytes.Length == RagdollNetProtocol.InputBytes && seq == 7 && clientTime == 1234
                       && got.jump && !got.shove && got.grab && got.sprint && !got.ability && got.ability2 && got.interact
                       && Mathf.Abs(got.move.x - 0.5f) < 0.01f && Mathf.Abs(got.move.y + 0.25f) < 0.01f && aimError < 1f;
            bool wrongSession = RagdollNetProtocol.ReadInput(inputBytes, 100, out _, out _, out _);
            Report("입력 패킷 왕복", inputOk && !wrongSession,
                $"{inputBytes.Length}바이트, 값 복원 {inputOk} (능력·상호작용·조준 오차 {aimError:F2}°), 다른 방 번호 거부 {!wrongSession}");

            var host = Spawn(new Vector3(0f, 0f, -10f), Vector3.forward, "net-host");
            var remote = Spawn(new Vector3(20f, 0f, -10f), Vector3.forward, "net-remote");
            remote.SetNetworkPuppet(true);
            var pose = new RagdollPose { id = 1 };
            var list = new System.Collections.Generic.List<RagdollPose> { pose };
            var snapshot = new RagdollSnapshot();
            var offset = new Vector3(20f, 0f, 0f);
            float maxAngle = 0f, maxBody = 0f, maxHips = 0f;
            int worstBody = 0;
            int packetBytes = 0, steps = 0, parseFailures = 0, flagMismatch = 0;
            float maxStaminaError = 0f, minStamina = 1f;

            void Replicate()
            {
                steps++;
                if (steps % 4 != 0) return;     // 30 Hz out of 120 Hz physics
                host.CaptureNetworkPose(pose);
                pose.id = 1;
                pose.hips += offset;
                byte[] bytes = RagdollNetProtocol.Snapshot(1234, (uint)steps, (uint)(steps * 8), list);
                packetBytes = bytes.Length;
                if (!RagdollNetProtocol.ReadSnapshot(bytes, 1234, snapshot))
                {
                    parseFailures++;
                    return;
                }
                remote.ApplyNetworkPose(snapshot.At(0));
                // The client draws its own stamina gauge from these, so they have to arrive intact.
                maxStaminaError = Mathf.Max(maxStaminaError, Mathf.Abs(remote.Stamina - host.Stamina));
                minStamina = Mathf.Min(minStamina, remote.Stamina);
                if (remote.Sprinting != host.Sprinting || remote.Exhausted != host.Exhausted || remote.BeingHeld != host.BeingHeld
                    || Mathf.Abs(remote.EscapeProgress - host.EscapeProgress) > 0.01f)
                    flagMismatch++;
                for (int i = 0; i < RagdollPawn.Count; i++)
                {
                    maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(host.bodies[i].transform.rotation, remote.bodies[i].transform.rotation));
                    float bodyError = Vector3.Distance(host.bodies[i].transform.position + offset, remote.bodies[i].transform.position);
                    if (bodyError > maxBody)
                    {
                        maxBody = bodyError;
                        worstBody = i;
                    }
                }
                maxHips = Mathf.Max(maxHips, Vector3.Distance(host.bodies[0].transform.position + offset, remote.bodies[0].transform.position));
            }

            yield return Sim(0.8f);
            Drive(host, Vector3.forward, sprint: true);
            yield return Sim(2.5f, Replicate);
            Drive(host, Vector3.forward, grab: true);
            yield return Sim(0.8f, Replicate);
            host.Knockdown("테스트: 원격 복원");
            host.AddVelocity(new Vector3(2f, 3f, 0f));
            yield return Sim(2.5f, Replicate);

            // Limb positions are rebuilt from the chain, not sent, so the error grows with how hard
            // the limbs swing: 1.7 cm on the spec defaults, 5.0 cm on the lively "weight" preset (a
            // hand, three joints out). 6 cm is the gate; sending hand positions would cost 12 more
            // bytes per pawn per packet, which is not worth it for a remote pawn's hand.
            Report("래그돌 자세 원격 복원", parseFailures == 0 && maxAngle < 2f && maxBody < 0.06f && remote.IsFinite(),
                $"스냅샷 {packetBytes}바이트(폰 1명), 회전 오차 최대 {maxAngle:F2}°, 부위 위치 오차 최대 {maxBody * 100f:F1} cm ({(BodyId)worstBody}), 골반 오차 {maxHips * 100f:F2} cm, 해독 실패 {parseFailures}회");
            Report("스테미나·질주 상태 원격 전달", parseFailures == 0 && maxStaminaError < 0.01f && flagMismatch == 0 && minStamina < 0.95f,
                $"스테미나 오차 최대 {maxStaminaError * 100f:F2}%, 원격에서 본 최저 {minStamina * 100f:F0}%, 상태 불일치 {flagMismatch}회");
            Info("대역폭 환산", $"폰 1명당 {RagdollNetProtocol.PoseBytes}바이트 · 12명 스냅샷 {RagdollNetProtocol.SnapshotBytes(12)}바이트 · 30Hz 기준 호스트 업로드 {RagdollNetProtocol.SnapshotBytes(12) * 30 * 11 * 8 / 1000000f:F2} Mbit/s (11명에게 전송)");
            yield return Clear();
        }

        // ------------------------------------------------------------------ screenshots

        IEnumerator RunShots(string folder)
        {
            Directory.CreateDirectory(folder);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1f / game.physicsRate;
            shotCamera = game.labCamera.GetComponent<Camera>();
            game.labCamera.enabled = false;

            var pawn = Spawn(new Vector3(0f, 0f, -10f), Vector3.back, "shot");
            yield return Sim(1.5f);
            yield return Shot(folder, "01_front", new Vector3(0f, 0.75f, -11.9f), pawn.Hips.position + Vector3.up * 0.3f);
            yield return Shot(folder, "02_three_quarter", new Vector3(1.4f, 0.95f, -11.5f), pawn.Hips.position + Vector3.up * 0.3f);
            yield return Shot(folder, "03_back", new Vector3(-0.6f, 1.1f, -8.2f), pawn.Hips.position + Vector3.up * 0.3f);
            yield return Clear();

            var runner = Spawn(new Vector3(-10f, 0f, -10f), Vector3.right, "runner");
            yield return Sim(0.6f);
            Drive(runner, Vector3.right);
            yield return Sim(1.1f);
            yield return Shot(folder, "04_run_side", runner.Hips.position + new Vector3(0.4f, 0.6f, -2.6f), runner.Hips.position + Vector3.up * 0.25f);
            yield return Sim(0.09f);
            yield return Shot(folder, "05_run_side_b", runner.Hips.position + new Vector3(0.4f, 0.6f, -2.6f), runner.Hips.position + Vector3.up * 0.25f);
            runner.Knockdown("사진");
            runner.AddVelocity(new Vector3(1f, 2.5f, 1f));
            yield return Sim(0.7f);
            yield return Shot(folder, "06_ragdoll", runner.Hips.position + new Vector3(-1.6f, 1.2f, -2.2f), runner.Hips.position);
            yield return Sim(0.65f);
            yield return Shot(folder, "07_getting_up", runner.Hips.position + new Vector3(-1.6f, 1.2f, -2.2f), runner.Hips.position + Vector3.up * 0.2f);
            yield return Clear();

            var a = Spawn(new Vector3(-8f, 0f, 3f), Vector3.forward, "grabber");
            var b = Spawn(new Vector3(-8f, 0f, 3.75f), Vector3.back, "held");
            yield return Sim(0.8f);
            Drive(a, Vector3.forward * 0.25f, grab: true);
            yield return Sim(1.2f);
            Drive(a, Vector3.back * 0.6f, grab: true);
            yield return Sim(0.5f);
            Vector3 mid = (a.Hips.position + b.Hips.position) * 0.5f;
            yield return Shot(folder, "08_grab", mid + new Vector3(2.2f, 1.1f, -0.4f), mid + Vector3.up * 0.25f);
            yield return Clear();

            var s1 = Spawn(new Vector3(-8f, 0f, 6f), Vector3.forward, "diver");
            var s2 = Spawn(new Vector3(-8f, 0f, 8.4f), Vector3.back, "target");
            yield return Sim(1f);
            Drive(s1, Vector3.forward);
            yield return Sim(0.3f);
            Drive(s1, Vector3.forward, shove: true);
            yield return Sim(0.25f);
            Vector3 mid2 = (s1.Hips.position + s2.Hips.position) * 0.5f;
            yield return Shot(folder, "09_dive", mid2 + new Vector3(2.4f, 1.0f, 0f), mid2 + Vector3.up * 0.2f);
            yield return Clear();

            yield return Shot(folder, "10_overview", new Vector3(8f, 34f, -46f), new Vector3(-2f, 0f, 2f));
            yield return Shot(folder, "11_walls", new Vector3(9f, 5f, -14f), new Vector3(21f, 1.5f, 0f));
            yield return Shot(folder, "12_slopes", new Vector3(-10f, 7f, -20f), new Vector3(-30f, 2f, 0f));
            yield return Shot(folder, "13_bar_beam_low", new Vector3(12f, 9f, 6f), new Vector3(2f, 0f, -8f));

            game.labCamera.enabled = true;
            var p1 = Spawn(LabLayout.SpawnP1, Vector3.forward, "P1");
            var p2 = Spawn(LabLayout.SpawnP2, Vector3.forward, "P2");
            if (game.players.Length > 1)
            {
                game.players[0].pawn = p1;
                game.players[1].pawn = p2;
                if (game.players[0].material != null) p1.skin.sharedMaterial = game.players[0].material;
                if (game.players[1].material != null) p2.skin.sharedMaterial = game.players[1].material;
            }
            var dummy = Spawn(LabLayout.DummySpawns[0], Vector3.back, "dummy");
            if (game.dummyMaterial != null) dummy.skin.sharedMaterial = game.dummyMaterial;
            yield return Sim(1.2f);
            game.labCamera.enabled = false;
            yield return ShotCurrent(folder, "14_game_view");
            yield return Clear();
        }

        /// <summary>
        /// Records one scripted performance at a fixed 30 fps: stand, run, hard turn, jump, take a hit,
        /// get up. Same script every time, so two parameter sets can be compared frame for frame.
        /// </summary>
        IEnumerator RunClip(string folder)
        {
            Directory.CreateDirectory(folder);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1f / game.physicsRate;
            Time.captureFramerate = 30;
            shotCamera = game.labCamera.GetComponent<Camera>();
            game.labCamera.enabled = false;

            var pawn = Spawn(new Vector3(-9f, 0f, -6f), Vector3.right, "clip");
            var follow = pawn.Hips.position;
            void Track(float lead)
            {
                Vector3 want = pawn.Hips.position + pawn.Facing * lead;
                follow = Vector3.Lerp(follow, want, 0.12f);
                Vector3 eye = follow + new Vector3(0f, 0.55f, -2.4f);
                shotCamera.transform.SetPositionAndRotation(
                    eye, Quaternion.LookRotation(follow + Vector3.up * 0.18f - eye, Vector3.up));
            }

            for (int i = 0; i < 40; i++) { Track(0f); yield return null; }
            clipFolder = folder;

            yield return ClipSegment(0.6f, () => { Drive(pawn, Vector3.zero); Track(0f); });
            yield return ClipSegment(2.6f, () => { Drive(pawn, Vector3.right); Track(0.9f); });
            yield return ClipSegment(1.6f, () => { Drive(pawn, Vector3.forward); Track(0.9f); });
            yield return ClipSegment(0.15f, () => { Drive(pawn, Vector3.forward, jump: true); Track(0.9f); });
            yield return ClipSegment(1.5f, () => { Drive(pawn, Vector3.forward); Track(0.9f); });
            Drive(pawn, Vector3.zero);
            pawn.Knockdown("클립");
            pawn.AddVelocity(new Vector3(2.5f, 2.2f, 0f));
            yield return ClipSegment(3.2f, () => Track(0f));

            clipFolder = null;
            Time.captureFramerate = 0;
        }

        /// <summary>Records the two new verbs: climbing a wall, and thrashing out of a grab.</summary>
        IEnumerator RunActionClip(string folder)
        {
            Directory.CreateDirectory(folder);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1f / game.physicsRate;
            Time.captureFramerate = 30;
            shotCamera = game.labCamera.GetComponent<Camera>();
            game.labCamera.enabled = false;

            // ---- climb the 4 m wall
            var climber = Spawn(new Vector3(LabLayout.WallFrontX - 2.2f, 0f, LabLayout.WallZ[2]), Vector3.right, "등반");
            Vector3 eye = climber.Hips.position + new Vector3(-1.2f, 1.2f, -3.4f);
            Vector3 camSmooth = Vector3.zero;
            bool camPrimed = false;
            void Watch(RagdollPawn pawn, float height)
            {
                // Follow a smoothed copy of the hips. Chasing them directly picks up every pull
                // and the recording jitters.
                if (!camPrimed) { camSmooth = pawn.Hips.position; camPrimed = true; }
                camSmooth = Vector3.Lerp(camSmooth, pawn.Hips.position, 0.06f);
                Vector3 want = camSmooth + new Vector3(-1.2f, height, -3.4f);
                eye = Vector3.Lerp(eye, want, 0.05f);
                Vector3 look = camSmooth + Vector3.up * 0.2f;
                shotCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            }
            for (int i = 0; i < 30; i++) { Watch(climber, 1.2f); yield return null; }
            clipFolder = folder;
            yield return ClipSegment(6.5f, () => { Drive(climber, Vector3.right, grab: true); Watch(climber, 1.2f); });
            clipFolder = null;
            yield return Clear();

            // ---- get grabbed, then thrash out of it
            var holder = Spawn(new Vector3(0f, 0f, -6f), Vector3.forward, "잡는쪽");
            var victim = Spawn(new Vector3(0f, 0f, -5.3f), Vector3.back, "잡힌쪽");
            eye = victim.Hips.position + new Vector3(2.0f, 0.7f, -1.6f);
            for (int i = 0; i < 20; i++) { Watch(victim, 0.7f); yield return null; }
            clipFolder = folder;
            float t = 0f, nextTap = 1.4f;
            yield return ClipSegment(5.5f, () =>
            {
                t += Time.deltaTime;
                Drive(holder, Vector3.forward * 0.25f, grab: true);
                bool tap = t >= nextTap;
                if (tap) nextTap = t + 0.12f;
                Drive(victim, Vector3.zero, shove: tap);
                Watch(victim, 0.7f);
            });
            clipFolder = null;
            Time.captureFramerate = 0;
        }

        IEnumerator ClipSegment(float seconds, Action perFrame)
        {
            float t = 0f;
            while (t < seconds)
            {
                perFrame();
                yield return null;
                t += Time.deltaTime;
            }
        }

        IEnumerator Shot(string folder, string name, Vector3 position, Vector3 lookAt)
        {
            shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return ShotCurrent(folder, name);
        }

        IEnumerator ShotCurrent(string folder, string name)
        {
            pendingShot = Path.Combine(folder, name + ".png");
            yield return null;
            yield return null;
        }

        void LateUpdate()
        {
            if (clipFolder != null && shotCamera != null && pendingShot == null)
                pendingShot = Path.Combine(clipFolder, $"f_{clipFrame++:D4}.png");
            if (pendingShot == null || shotCamera == null) return;
            int w = shotWidth, h = shotHeight;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            var previous = shotCamera.targetTexture;
            shotCamera.targetTexture = rt;
            shotCamera.Render();
            shotCamera.targetTexture = previous;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(pendingShot, tex.EncodeToPNG());
            if (sideCamera != null && clipFolder != null)
            {
                // The feel recordings' close-up side view of the same frame.
                sideCamera.targetTexture = rt;
                sideCamera.Render();
                sideCamera.targetTexture = null;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string file = Path.GetFileName(pendingShot);
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(pendingShot), "s_" + file.Substring(2)), tex.EncodeToPNG());
            }
            RenderTexture.ReleaseTemporary(rt);
            Destroy(tex);
            pendingShot = null;
        }
    }
}
