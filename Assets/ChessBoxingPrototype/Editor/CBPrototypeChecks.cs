using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ChessFight.ChessBoxingPrototype.Editor
{
    // Optional in-Unity checks; no extra packages or NUnit dependency required.
    // Exercises the actual damage/state code. It does NOT validate physical feel or hand reach.
    public static class CBPrototypeChecks
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("ChessFight/Chess Boxing Prototype/Run Combat Rule Checks (Play Mode)")]
        public static void Run()
        {
            var match = UnityEngine.Object.FindFirstObjectByType<CBMatch>();
            if (!EditorApplication.isPlaying || match == null || match.pawnModel == null)
            {
                Debug.LogWarning("Open the prototype scene and press Play before running these checks.");
                return;
            }
            var root = new GameObject("CB temporary rule checks");
            int passed = 0;
            try
            {
                var settings = new CBSettings();
                CBFighter Make(string label, Vector3 at, Vector3 facing)
                {
                    var go = new GameObject(label);
                    go.transform.SetParent(root.transform);
                    var fighter = go.AddComponent<CBFighter>();
                    fighter.Initialize(match.pawnModel, settings, at, facing, Color.white);
                    return fighter;
                }
                var defender = Make("Defender", new Vector3(50f, 0f, 0f), Vector3.forward);
                var attacker = Make("Attacker", new Vector3(50f, 0f, 0.5f), Vector3.back);
                defender.Opponent = attacker; attacker.Opponent = defender;
                void Check(bool condition, string name)
                {
                    if (!condition) throw new InvalidOperationException("FAILED: " + name);
                    passed++;
                }
                void Fresh()
                {
                    defender.ResetFighter();
                    typeof(CBFighter).GetField("protection", Hidden).SetValue(defender, 0f);
                }
                bool Hit(bool heavy, bool glove = false) => (bool)typeof(CBFighter).GetMethod("Receive", Hidden).Invoke(defender, new object[] { attacker, heavy, glove });
                void Step() => typeof(CBFighter).GetMethod("FixedUpdate", Hidden).Invoke(defender, null);

                Check(Mathf.Approximately(defender.Health, 100f) && Mathf.Approximately(defender.Stamina, 100f), "initial vitals");
                Check(!Hit(false), "spawn protection rejects damage");
                Check(attacker.Punch(false) && !attacker.Punch(false), "one attack at a time");
                Check(Mathf.Approximately(attacker.Stamina, 100f - settings.jabCost), "single stamina cost");
                Fresh();
                Check(Hit(false) && Mathf.Approximately(defender.Health, 100f - settings.jabDamage), "jab damage");
                Fresh(); defender.Command(Vector3.zero, true); Step();
                Check(defender.Guarding && Hit(false, true) && Mathf.Approximately(defender.Health, 99f), "front guard chips one health");
                Check(defender.Blocks == 1 && defender.Stamina < 100f - settings.guardJabCost + 0.01f, "guard spends stamina");
                Fresh(); defender.Command(Vector3.zero, true); Step();
                defender.Body.rotation = Quaternion.Euler(0f, 180f, 0f);
                Check(Hit(false) && Mathf.Approximately(defender.Health, 100f - settings.jabDamage), "back hit bypasses guard");
                Fresh(); defender.Command(Vector3.zero, true); Step();
                typeof(CBFighter).GetMethod("Spend", Hidden).Invoke(defender, new object[] { 95f });
                Check(Hit(true, true) && defender.State == CBBodyState.Down, "low stamina guard breaks into a fall");
                float fallenHealth = defender.Health;
                Check(!Hit(false) && Mathf.Approximately(defender.Health, fallenHealth), "grounded fighter cannot be farmed for damage");
                Fresh(); defender.SetPaused(true);
                Check(defender.Body.isKinematic && !Hit(false), "pause freezes own body and rejects damage");
                defender.SetPaused(false);
                Check(!defender.Body.isKinematic && defender.Body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "resume restores body mode");
                Fresh(); settings.heavyDamage = 150f;
                int defeats = 0;
                defender.Defeated += _ => defeats++;
                Check(Hit(true) && defender.Health == 0f && defender.State == CBBodyState.KnockedOut && defeats == 1, "KO transition");
                Check(!Hit(true) && defeats == 1, "KO is terminal until restart");
                defender.ResetFighter();
                Check(defender.State == CBBodyState.Standing && defender.Health == settings.maxHealth && defender.Stamina == 100f && !defender.Attacking, "restart clears match state");
                Debug.Log("CB combat checks PASSED: " + passed + ". Hand contact, balance and camera still need a Play test.");
            }
            catch (Exception error) { Debug.LogError("CB combat checks stopped after " + passed + " passes. " + error); }
            finally
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
