"""Run production barrage code against engine stand-ins; not Unity/PhysX tests."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]


class TacticalBarrageLogic(unittest.TestCase):
    @unittest.skipUnless(shutil.which("mcs") and shutil.which("mono"), "Mono C# tools unavailable")
    def test_production_pattern_telegraph_and_per_character_hits(self):
        with tempfile.TemporaryDirectory(prefix="chessfight-barrage-") as directory:
            executable = str(Path(directory) / "BarrageTests.exe")
            sources = (
                ROOT / "Assets/Scripts/Gameplay/ImportedObstacles/TacticalMapBarrage.cs",
                ROOT / "Tests/Gameplay/Fixtures/TacticalMapBarrageHarness.cs",
            )
            compiled = subprocess.run(
                ["mcs", "-langversion:experimental", "-out:" + executable, *map(str, sources)],
                text=True, capture_output=True, timeout=30,
            )
            self.assertEqual(compiled.returncode, 0, compiled.stdout + compiled.stderr)
            result = subprocess.run(["mono", executable], text=True, capture_output=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("checks passed (stubbed Unity, no PhysX)", result.stdout)


if __name__ == "__main__":
    unittest.main()
