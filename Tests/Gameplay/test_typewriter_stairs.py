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
            subprocess.run(['mcs','-langversion:experimental','-out:'+exe,str(ROOT/'Assets/Scripts/Gameplay/ImportedObstacles/TypewriterStairs.cs'),str(ROOT/'Tests/Gameplay/Fixtures/TypewriterHarness.cs'),str(ROOT/'Assets/Scripts/Core/TypewriterMemory.cs'),str(ROOT/'Assets/Scripts/Gameplay/ImportedObstacles/TypewriterKey.cs'),str(ROOT/'Assets/Scripts/Gameplay/Obstacles/IJumpSpringSurface.cs')],check=True)
            subprocess.run(['mono',exe],check=True)

    def test_active_prefab_has_printed_document_and_reachable_safe_keys(self):
        room.CompleteWarRoom.setUpClass();r=room.CompleteWarRoom()
        _,f,block=r.scripts_of('TypewriterStairs')[0]
        self.assertEqual(len(r.scripts_of('TypewriterStairs')),1)
        self.assertEqual(len(r.scripts_of('TroopFormationSlide')),0)
        self.assertNotIn('printedWord',f)
        self.assertNotIn('keyLetters',f)
        clue_section=re.search(r'^  clueFaces:\n((?:  - .*\n)+)',block,re.M)[1]
        self.assertEqual(len(re.findall('fileID:',clue_section)),5)
        self.assertEqual(len(r.scripts_of('TypewriterKey')),20)
        self.assertNotIn('fileID: 0',f['documentFace'])
        for name in ('keyAreas','keyFaces','keyVisuals','keys'):
            section=re.search(r'^  '+name+r':\n((?:  - .*\n)+)',block,re.M)[1]
            self.assertEqual(len(re.findall('fileID:',section)),20)

    def test_labels_and_supports_share_the_same_physical_row_order(self):
        room.CompleteWarRoom.setUpClass();r=room.CompleteWarRoom()
        _,_,block=r.scripts_of('TypewriterStairs')[0]
        def refs(name):
            section=re.search(r'^  '+name+r':\n((?:  - .*\n)+)',block,re.M)[1]
            return re.findall(r'fileID: (\d+)',section)
        def owner(ref):return room.number(fields(r.data[ref])['m_GameObject'])
        for index,(key,area,face,visual) in enumerate(zip(refs('keys'),refs('keyAreas'),refs('keyFaces'),refs('keyVisuals'))):
            go=owner(key)
            self.assertEqual(r.names[go],f'Key {index//4}-{index%4}')
            self.assertEqual(owner(area),go)
            key_transform=r.owners[go]
            for child in (face,str(r.owners[owner(visual)])):
                self.assertEqual(room.number(fields(r.data[child])['m_Father']),key_transform)
        for index,face in enumerate(refs('clueFaces')):
            parent=room.number(fields(r.data[face])['m_Father'])
            self.assertEqual(r.names[owner(str(parent))],f'Memory clue {index+1}')

    def test_entrance_floor_seam_and_interior_clearance(self):
        room.CompleteWarRoom.setUpClass();r=room.CompleteWarRoom()
        for z in (-26,-19,-12,-9):
            floors=[name for name,lo,hi,_ in r.boxes if lo[0]<0<hi[0] and lo[2]<z<hi[2] and abs(hi[1])<.001]
            self.assertEqual(floors,['Approach floor -27'])
        floors=[name for name,lo,hi,_ in r.boxes if lo[0]<0<hi[0] and lo[2]<-4<hi[2] and abs(hi[1])<.001]
        self.assertEqual(floors,[])  # Play supplies exactly the HQ entry platform from z=-8 to 0.
        blocked=[name for name,lo,hi,_ in r.boxes if lo[0]<0<hi[0] and lo[2]<-35<hi[2] and lo[1]<3<hi[1]]
        self.assertEqual(blocked,[])
        names=set(r.names.values())
        self.assertTrue({'Black platen roller','Round brass key rim','Paper backing','Ink ribbon spool -1'}<=names)
        for i in range(1,6):self.assertIn('Memory clue '+str(i),names)
