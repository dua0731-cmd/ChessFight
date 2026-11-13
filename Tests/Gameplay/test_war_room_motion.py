"""Execute production movement curves with engine stand-ins, not a PhysX simulation."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
ROOT = Path(__file__).resolve().parents[2]

class WarRoomMotion(unittest.TestCase):
    @unittest.skipUnless(shutil.which('mcs') and shutil.which('mono'), 'Mono unavailable')
    def test_motion_and_floor_activation(self):
        with tempfile.TemporaryDirectory() as temp:
            exe = str(Path(temp) / 'Motion.exe')
            sources = [ROOT / 'Tests/Gameplay/Fixtures/WarRoomMotionHarness.cs']
            sources += [ROOT / ('Assets/Scripts/Gameplay/ImportedObstacles/' + name + '.cs')
                        for name in ('CommandStampPress', 'RollingMapBridge')]
            subprocess.run(['mcs', '-langversion:experimental', '-out:' + exe, *map(str, sources)], check=True)
            subprocess.run(['mono', exe], check=True)
