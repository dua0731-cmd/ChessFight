from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest
import test_war_room_complete as room
from test_obstacle_assets import fields
ROOT=Path(__file__).resolve().parents[2]

class TypewriterTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which('mcs') and shutil.which('mono'),'Mono unavailable')
    def test_actual_rules_and_footing(self):
        with tempfile.TemporaryDirectory() as temp:
            exe=str(Path(temp)/'Keys.exe')
            subprocess.run(['mcs','-langversion:experimental','-out:'+exe,str(ROOT/'Assets/Scripts/Gameplay/ImportedObstacles/TypewriterStairs.cs'),str(ROOT/'Tests/Gameplay/Fixtures/TypewriterHarness.cs')],check=True)
            subprocess.run(['mono',exe],check=True)

    def test_active_prefab_has_printed_document_and_reachable_safe_keys(self):
        room.CompleteWarRoom.setUpClass();r=room.CompleteWarRoom()
        _,f,block=r.scripts_of('TypewriterStairs')[0]
        self.assertEqual(len(r.scripts_of('TypewriterStairs')),1)
        self.assertEqual(len(r.scripts_of('TroopFormationSlide')),0)
        word=f['printedWord'].strip('"');letters=f['keyLetters'].strip('"')
        self.assertEqual(word,'CHECK');self.assertEqual(len(letters),20)
        last=None
        for row in range(5):
            choices=[i for i,c in enumerate(letters[row*4:row*4+4]) if c in word]
            self.assertEqual(len(choices),1)
            if last is not None:self.assertLessEqual(abs(choices[0]-last),1)
            last=choices[0]
        self.assertNotIn('fileID: 0',f['documentFace'])
        for name in ('keyAreas','keyFaces','keyVisuals'):
            section=re.search(r'^  '+name+r':\n((?:  - .*\n)+)',block,re.M)[1]
            self.assertEqual(len(re.findall('fileID:',section)),20)
