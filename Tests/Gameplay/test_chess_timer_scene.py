"""Saved prototype wiring checks. Rule behavior is exercised by NetworkCoreTests."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class ChessTimerSceneTests(unittest.TestCase):
    def test_standalone_scene_uses_timer_driver_existing_pawn_and_camera(self):
        scene = (ROOT / 'Assets/Scenes/ChessTimerMinigame.unity').read_text()
        driver = ROOT / 'Assets/ChessFight/RagdollLab/Scripts/ChessTimerMinigame.cs'
        guid = re.search(r'guid: (\w+)', Path(str(driver) + '.meta').read_text())[1]
        self.assertEqual(scene.count('guid: ' + guid), 1)
        self.assertNotIn('ChessFight.RagdollLab.KingRushPrototype', scene)
        self.assertIn('ChessFight.RagdollLab.LabCamera', scene)
        self.assertIn('ChessFight.Gameplay.PhysicsProfile', scene)
        for field in ('pawnPrefab', 'tuning'):
            reference = re.search(field + r': \{fileID: (\d+), guid: (\w+)', scene)
            self.assertIsNotNone(reference)
            matches = [p for p in (ROOT / 'Assets').rglob('*.meta') if 'guid: ' + reference[2] in p.read_text()]
            self.assertEqual(len(matches), 1, field)
            target = Path(str(matches[0])[:-5]).read_text()
            self.assertIn('&' + reference[1], target, field)

    def test_scene_local_references_and_menu_target_resolve(self):
        path = ROOT / 'Assets/Scenes/ChessTimerMinigame.unity'
        scene = path.read_text()
        ids = re.findall(r'^--- !u!\d+ &(\d+)', scene, re.M)
        self.assertEqual(len(ids), len(set(ids)))
        for local in re.findall(r'\{fileID: (\d+)\}', scene):
            self.assertTrue(local == '0' or local in ids, local)
        menu = (ROOT / 'Assets/ChessFight/RagdollLab/Editor/ChessTimerMinigameTools.cs').read_text()
        self.assertIn('Assets/Scenes/ChessTimerMinigame.unity', menu)
        self.assertTrue(Path(str(path) + '.meta').exists())


if __name__ == '__main__':
    unittest.main()
