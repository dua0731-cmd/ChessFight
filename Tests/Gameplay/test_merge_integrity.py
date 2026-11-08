"""Regression checks for the committed upstream merge, independent of Unity."""
from pathlib import Path
import re
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]


class MergeIntegrity(unittest.TestCase):
    def test_tracked_text_has_no_conflict_markers(self):
        result = subprocess.run(
            ['git', 'grep', '-n', '-I', '-E', r'^(<{7} |>{7} |[|]{7} )', '--', '.'],
            cwd=ROOT, text=True, capture_output=True,
        )
        self.assertEqual(result.returncode, 1, result.stdout[:4000] + result.stderr)

    def test_restored_hall_and_test_scene_have_distinct_valid_objects(self):
        assets = ('Assets/Prefabs/PawnRushSections/Late_MG_H_KingsChessHall.prefab',
                  'Assets/Scenes/ImportedChessFightMap.unity')
        guids = []
        for name in assets:
            with self.subTest(asset=name):
                text = (ROOT / name).read_text()
                ids = re.findall(r'^--- !u!\d+ &(\d+)', text, re.M)
                self.assertEqual(len(ids), len(set(ids)))
                refs = set(re.findall(r'\{fileID: (\d+)\}', text)) - {'0'}
                self.assertFalse(refs - set(ids), refs - set(ids))
                guids.append(re.search(r'^guid: (\w+)', (ROOT / (name + '.meta')).read_text(), re.M)[1])
        self.assertNotEqual(*guids)

    def test_serialized_motion_uses_concrete_script(self):
        abstract = []
        for name in ('ObstacleMotion', 'ObstacleSurface'):
            meta = ROOT / ('Assets/Scripts/Gameplay/ImportedObstacles/' + name + '.cs.meta')
            abstract.append(re.search(r'guid: (\w+)', meta.read_text())[1])
        for path in (ROOT / 'Assets').rglob('*'):
            if path.suffix not in ('.unity', '.prefab'):
                continue
            text = path.read_text()
            for guid in abstract:
                self.assertNotIn('guid: ' + guid, text, str(path))
