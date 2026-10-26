using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class ChessTimerMinigame
    {
        [Header("Prototype cutting helpers")]
        [SerializeField, Range(0, 3)] int cuttingHelpersPerTeam = 3;
        readonly int[] helperTarget = new int[12], helperCutWire = new int[12];
        readonly List<Vector3>[] helperPaths = new List<Vector3>[12];
        readonly int[] helperPathIndex = new int[12];
        bool IsCuttingHelper(int index) => CanDriveBot(index) && index % 6 >= 3 && index % 6 < 3 + cuttingHelpersPerTeam &&
            roles[index] != 1 && roles[index] != 4;
        void ResetHelperRoutes(int team)
        {
            for (int i = team * 6; i < team * 6 + 6; i++)
            { helperTarget[i] = helperCutWire[i] = -1; helperPaths[i] = null; helperPathIndex[i] = 0; }
        }
        void DriveCuttingBots()
        {
            for (int i = 0; i < helperCutWire.Length; i++) helperCutWire[i] = -1;
            if (!started || !Networked && (menu || !Application.isFocused)) return;
            for (int team = 0; team < 2; team++)
            {
                if (rules.Teams[team].Finished) continue;
                var reserved = new HashSet<int>();
                int manual = ManualCutWire(team); if (manual >= 0) reserved.Add(manual);
                if (Networked) for (int i = team * 6; i < team * 6 + 6; i++)
                { int wire = NetworkCutWire(i); if (wire >= 0) reserved.Add(wire); }
                // Keep ongoing assignments before handing free wires to idle helpers.
                for (int i = team * 6 + 3; i < team * 6 + 6; i++)
                {
                    if (!IsCuttingHelper(i) || respawnAt[i] > 0 || players[i].State != PawnState.Active) continue;
                    if (hasCutter[i] && AvailableHelperWire(team, helperTarget[i], reserved)) reserved.Add(helperTarget[i]);
                    else { helperTarget[i] = -1; helperPaths[i] = null; }
                }
                for (int i = team * 6 + 3; i < team * 6 + 6; i++)
                {
                    if (!IsCuttingHelper(i) || respawnAt[i] > 0 || players[i].State != PawnState.Active) continue;
                    roles[i] = 5;
                    var pos = players[i].Hips.position;
                    if (!hasCutter[i])
                    {
                        helperTarget[i] = -1; helperPaths[i] = null;
                        if (TryEquipCutter(i)) continue;
                        // Pass outside the clock and its ramps, then approach the supply bench from the front.
                        float routeX = team == 0 ? -23f : 23f;
                        Vector3 target = pos.z < -22f && Mathf.Abs(pos.x - routeX) > .4f ? new Vector3(routeX, 0, -24f) :
                            pos.z < 9.5f ? new Vector3(routeX, 0, 10.1f) : new Vector3(BoardCentreX(team), 0, 10.1f);
                        WalkHelper(i, target, .25f); continue;
                    }
                    int wire = helperTarget[i];
                    if (wire >= 0) reserved.Remove(wire);
                    if (!AvailableHelperWire(team, wire, reserved))
                    {
                        wire = -1; helperPaths[i] = null;
                        float best = float.PositiveInfinity;
                        for (int candidate = 0; candidate < puzzles[team].Board.WireCount; candidate++)
                        {
                            if (!AvailableHelperWire(team, candidate, reserved)) continue;
                            float distance = (WirePoint(team, candidate) - pos).sqrMagnitude;
                            if (distance < best) { best = distance; wire = candidate; }
                        }
                        helperTarget[i] = wire;
                    }
                    if (wire < 0) continue;
                    reserved.Add(wire);
                    if (Near(pos, WirePoint(team, wire)))
                    { helperCutWire[i] = wire; continue; }
                    if (helperPaths[i] == null || helperPathIndex[i] >= helperPaths[i].Count)
                    { helperPaths[i] = HelperBoardPath(i, wire); helperPathIndex[i] = 0; }
                    var path = helperPaths[i];
                    if (path == null || path.Count == 0) continue;
                    int step = helperPathIndex[i];
                    if (WalkHelper(i, path[step], .3f)) helperPathIndex[i]++;
                    // If pushed away from the route, replan from the current position next frame.
                    if ((new Vector2(pos.x - path[step].x, pos.z - path[step].z)).sqrMagnitude > 36f && pos.z > 14f)
                        helperPaths[i] = null;
                }
            }
        }
        bool AvailableHelperWire(int team, int wire, HashSet<int> reserved)
        {
            var puzzle = puzzles[team];
            return wire >= 0 && wire < puzzle.Board.WireCount && !puzzle.Cut(wire) && !reserved.Contains(wire) &&
                !puzzle.Board.Attacked(puzzle.Board.File(wire), puzzle.Board.Rank(wire));
        }
        bool WalkHelper(int index, Vector3 target, float stop)
        {
            Vector3 delta = target - players[index].Hips.position; delta.y = 0;
            if (delta.magnitude <= stop) return true;
            players[index].SetInput(new PawnInput { move = delta.normalized, aim = delta.normalized });
            return false;
        }
        List<Vector3> HelperBoardPath(int index, int wire)
        {
            int team = index / 6;
            var pos = players[index].Hips.position;
            float xNode = (pos.x - BoardCentreX(team) + 16f) / 2f;
            float yNode = team == 0 ? (46f - pos.z) / 2f : (pos.z - 14f) / 2f;
            int[] nodes = puzzles[team].Board.RouteToWire(xNode, yNode, wire);
            var path = new List<Vector3>();
            foreach (int node in nodes) path.Add(HelperNode(team, node));
            // The supply bench is solid: leave around its side before entering the board.
            if (pos.z < 12.8f && Mathf.Abs(pos.x - BoardCentreX(team)) < 2.3f)
            {
                float side = pos.x < BoardCentreX(team) ? -1f : 1f;
                float x = BoardCentreX(team) + side * 2.6f;
                path.Insert(0, new Vector3(x, 0, 13f));
                path.Insert(0, new Vector3(x, 0, pos.z));
            }
            return path;
        }
        static Vector3 HelperNode(int team, int node) => new Vector3(BoardCentreX(team) - 16 + node % 17 * 2, 0, 14 + (team == 0 ? 16 - node / 17 : node / 17) * 2);
    }
}
