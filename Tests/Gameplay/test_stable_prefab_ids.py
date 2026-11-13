"""Prevent generated prefab IDs changing Unity type between level revisions."""
import importlib.util
from pathlib import Path
import re
import sys
import unittest

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/RoyalPalace'))
import build_war_room as builder
from stable_prefab_ids import stabilize

def types(text):return {int(fid):typ for typ,fid in re.findall(r'^--- !u!(\d+) &(\d+)',text,re.M)}

class StablePrefabIds(unittest.TestCase):
    def test_inserted_geometry_preserves_every_existing_object_id_and_type(self):
        original=types(builder.Builder().save())
        changed=builder.Builder()
        g=changed.group('Regression test extra furniture')
        changed.box(g,'Regression test extra cabinet',(0,0,0),(1,1,1))
        updated=types(changed.save())
        self.assertGreater(len(updated),len(original))
        for fid,typ in original.items():self.assertEqual(updated.get(fid),typ,(fid,typ))
    def test_stabilization_is_idempotent(self):
        path=ROOT/builder.PREFAB;text=path.read_text()
        actual,mapping=stabilize(text)
        self.assertEqual(actual,text)
        self.assertTrue(all(old==new for old,new in mapping.items()))
    def test_scene_corresponding_source_classes_match_all_prefabs(self):
        sources={}
        for p in (ROOT/'Assets').rglob('*.prefab'):
            guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
            sources[guid]=types(p.read_text())
        count=0
        for p in list((ROOT/'Assets').rglob('*.unity'))+list((ROOT/'Assets').rglob('*.prefab')):
            for m in re.finditer(r'^--- !u!(\d+) &\d+[^\n]*\n(.*?)(?=^--- !u!|\Z)',p.read_text(),re.M|re.S):
                ref=re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+), guid: (\w+)',m[2])
                if ref:
                    self.assertIn(ref[2],sources,str(p));count+=1
                    self.assertEqual(sources[ref[2]].get(int(ref[1])),m[1],(str(p),ref[1]))
        self.assertGreater(count,100)
