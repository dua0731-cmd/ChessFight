from pathlib import Path
import unittest,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[2]
class LibraryPortal(unittest.TestCase):
    @unittest.skipUnless(shutil.which('mcs') and shutil.which('mono'),'Mono unavailable')
    def test_actual_camera_reveal_and_restore(self):
        with tempfile.TemporaryDirectory() as temp:
            exe=str(Path(temp)/'Portal.exe')
            subprocess.run(['mcs','-langversion:experimental','-out:'+exe,str(ROOT/'Assets/Scripts/RagdollLab/RoyalLibraryPortal.cs'),str(ROOT/'Tests/Gameplay/Fixtures/LibraryPortalHarness.cs')],check=True)
            subprocess.run(['mono',exe],check=True)
