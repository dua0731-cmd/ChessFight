"""Serialization/inheritance checks; these do not simulate Unity or PhysX.

Run: python3 -m unittest discover -s Tests/Gameplay -p 'test_*.py' -v
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
PREFABS = ASSETS / "Maps/ImportedChessFight/Prefabs/Obstacles"
CODE = ASSETS / "Scripts/Gameplay/ImportedObstacles"
TYPES = (
    "SpinningDisc", "JumpPad", "ReciprocatingPusher", "RotatingHammer",
    "RisingFallingTiles", "SlidingWall", "WeightedBridge", "ConveyorChessboard",
    "ChessClockGate", "FallingChessPiece", "PendulumSwing", "FoldingBridge",
    "FloorPiston", "SpringPillar", "AirVent",
)


def blocks(text):
    return {m[1]: part for part in re.split(r"(?=^--- !u!)", text, flags=re.M)
            if (m := re.match(r"--- !u!\d+ &(\d+)", part))}


def fields(block):
    return dict(re.findall(r"^  (\w+):[^\S\n]*(.*)$", block, re.M))


class ObstacleAssets(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scripts = {}
        cls.parents = {}
        for path in (ASSETS / "Scripts/Gameplay").rglob("*.cs"):
            meta = Path(str(path) + ".meta")
            if meta.exists():
                cls.scripts[re.search(r"guid: (\w+)", meta.read_text())[1]] = path
            for name, parent in re.findall(r"class\s+(\w+)\s*:\s*(\w+)", path.read_text()):
                cls.parents[name] = parent

    def test_fifteen_concrete_types_derive_from_obstacle(self):
        for name in TYPES:
            with self.subTest(name=name):
                self.assertIn("sealed class " + name, (CODE / (name + ".cs")).read_text())
                ancestors = []
                parent = name
                while parent in self.parents:
                    parent = self.parents[parent]
                    self.assertNotIn(parent, ancestors)
                    ancestors.append(parent)
                self.assertIn("Obstacle", ancestors)

    def test_each_prefab_uses_its_own_script_and_resolves_guid(self):
        for number, name in enumerate(TYPES, 1):
            with self.subTest(number=number, name=name):
                prefab, = PREFABS.glob(f"{number:02}_*.prefab")
                matches = [b for b in blocks(prefab.read_text()).values()
                           if "::ChessFight.ProtectKing." + name + "\n" in b]
                self.assertTrue(matches)
                for block in matches:
                    guid = re.search(r"m_Script: .*guid: (\w+)", block)[1]
                    self.assertEqual(self.scripts[guid], CODE / (name + ".cs"))

    def test_no_abstract_surface_or_motion_attached_anywhere(self):
        guids = [guid for guid, path in self.scripts.items()
                 if re.search(r"abstract\s+class\s+" + path.stem + r"\b", path.read_text())]
        for pattern in ("*.prefab", "*.unity"):
            for path in ASSETS.rglob(pattern):
                text = path.read_text()
                for guid in guids:
                    self.assertNotIn(guid, text, str(path))

    def test_remaining_hazards_and_kingrush_variants_share_obstacle(self):
        names = ("CrumblingRoyalFloor", "KnightCavalryCharge", "CastlingCrossingBridges",
                 "RopeLine", "PhaseToggle", "ContactLaunchPad", "SpringLaunchPad",
                 "KingRushSeesawBoard", "KingRushFinalBoard", "KingRushOrbitPawn",
                 "KingRushRollingHead", "KingRushClockButton", "KingRushFerry",
                 "KingRushCastleWall", "KingRushDrawbridge", "KingRushPortcullis",
                 "KingRushMace", "KingRushFallingStep", "KingRushSweepingHand")
        for name in names:
            with self.subTest(name=name):
                ancestors = []
                while name in self.parents:
                    name = self.parents[name]
                    self.assertNotIn(name, ancestors)
                    ancestors.append(name)
                self.assertIn("Obstacle", ancestors)

    def test_kingrush_prefabs_use_concrete_variants(self):
        expected = {
            "02_PawnField": {"KingRushOrbitPawn": 4},
            "03_RollingHeads": {"KingRushRollingHead": 3},
            "05_Clock": {"KingRushClockButton": 2},
            "06_Pond": {"ContactLaunchPad": 1},
            "12_CastleUpper": {"KingRushMace": 1},
            "13_CastleLower": {"KingRushFerry": 1, "KingRushCastleWall": 1, "KingRushDrawbridge": 1},
            "14_CrossingGate": {"KingRushPortcullis": 1},
            "19_FallingSteps": {"KingRushFallingStep": 32},
            "20_GiantHandStair": {"KingRushSweepingHand": 1},
        }
        for prefab, types in expected.items():
            records = list(blocks((ASSETS / "Prefabs/KingRushOpening" / (prefab + ".prefab")).read_text()).values())
            for name, count in types.items():
                matching = [b for b in records if "::ChessFight.Gameplay." + name + "\n" in b]
                self.assertEqual(len(matching), count, prefab)
                for block in matching:
                    guid = re.search(r"m_Script: .*guid: (\w+)", block)[1]
                    self.assertEqual(self.scripts[guid].stem, name)
                    self.assertTrue({"kind", "motion", "hand"}.isdisjoint(fields(block)))
                    if name == "ContactLaunchPad":
                        self.assertTrue({"period", "phase", "windKey", "windTurns"}.isdisjoint(fields(block)))

    def test_factories_do_not_instantiate_abstract_obstacles(self):
        abstract = [path.stem for path in self.scripts.values()
                    if re.search(r"abstract\s+class\s+" + path.stem + r"\b", path.read_text())]
        pattern = re.compile(r"AddComponent\s*<\s*(?:" + "|".join(abstract) + r")\s*>")
        for path in ASSETS.rglob("*.cs"):
            self.assertIsNone(pattern.search(path.read_text()), str(path))

    def test_surface_inspectors_contain_only_relevant_serialized_fields(self):
        expected = {
            1: {"context", "rotationSpeed", "diameter", "rotationDirection", "geometryRoot", "rotatingSurface"},
            2: {"context", "jumpHeight", "forwardSpeed", "launchCooldown"},
            8: {"context", "direction", "conveyorSpeed", "beltMarkers", "beltLength"},
        }
        for number, keys in expected.items():
            name = TYPES[number - 1]
            prefab, = PREFABS.glob(f"{number:02}_*.prefab")
            block, = [b for b in blocks(prefab.read_text()).values()
                      if "::ChessFight.ProtectKing." + name + "\n" in b]
            self.assertEqual({k for k in fields(block) if not k.startswith("m_")}, keys)

    def test_disc_has_one_fixed_rotation_pivot_and_shared_size_root(self):
        prefab, = PREFABS.glob("01_*.prefab")
        records = blocks(prefab.read_text())
        component, = [b for b in records.values() if "::ChessFight.ProtectKing.SpinningDisc\n" in b]
        values = fields(component)
        geometry = re.search(r"fileID: (\d+)", values["geometryRoot"])[1]
        rotor = re.search(r"fileID: (\d+)", values["rotatingSurface"])[1]
        self.assertEqual(fields(records[rotor])["m_Father"], "{fileID: " + geometry + "}")
        rings = ("5105911132895000167", "7949982060734359067", "3886350071908459439")
        for ring in rings:
            self.assertEqual(fields(records[ring])["m_Father"], "{fileID: " + rotor + "}")
        self.assertEqual(fields(records["735846764765704968"])["m_Father"], "{fileID: " + geometry + "}")
        self.assertEqual(values["diameter"], "8")
        self.assertEqual(values["rotationDirection"], "0")
        source = (CODE / "SpinningDisc.cs").read_text()
        self.assertNotRegex(source, r"Transform\[\]")
        for reference in ("geometryRoot", "rotatingSurface"):
            self.assertRegex(source, r"\[SerializeField, HideInInspector\] Transform " + reference)

    def test_clock_gates_and_rotating_hands_keep_separate_motion_types(self):
        prefab, = PREFABS.glob("09_*.prefab")
        records = list(blocks(prefab.read_text()).values())
        gates = [b for b in records if "::ChessFight.ProtectKing.ChessClockGate\n" in b]
        hands = [b for b in records if "::ChessFight.ProtectKing.ChessClockHand\n" in b]
        self.assertEqual(len(gates), 2)
        self.assertEqual(len(hands), 2)
        for block in hands:
            self.assertIn("degreesPerSecond", fields(block))
            self.assertNotIn("distance", fields(block))
            guid = re.search(r"m_Script: .*guid: (\w+)", block)[1]
            self.assertEqual(self.scripts[guid], CODE / "ChessClockHand.cs")
        for block in gates:
            self.assertIn("distance", fields(block))
            self.assertNotIn("degreesPerSecond", fields(block))

    def test_impact_relays_reference_an_owner_with_tuning(self):
        paths = list(PREFABS.glob("*.prefab")) + [ASSETS / "Scenes/ImportedChessFightMap.unity"]
        count = 0
        for path in paths:
            records = blocks(path.read_text())
            for block in records.values():
                if "::ChessFight.ProtectKing.ObstacleImpact\n" not in block:
                    continue
                relay = fields(block)
                self.assertTrue({"strength", "lift", "hitCooldown"}.isdisjoint(relay))
                pendulum = relay.get("pendulum", "{fileID: 0}") != "{fileID: 0}"
                target = re.search(r"fileID: (\d+)", relay["pendulum" if pendulum else "motion"])[1]
                owner = fields(records[target])
                required = {"hitCooldown", "KnockbackForce", "UpwardForce"} if pendulum else {"hitCooldown", "strength", "lift"}
                self.assertTrue(required <= owner.keys(), (str(path), target))
                count += 1
        self.assertGreater(count, 0)

    def test_existing_kinematic_obstacles_keep_rigidbody_base(self):
        for name in ("Spinner", "Oscillator", "Pendulum", "OrbitPlatform", "MovingPlatform",
                     "KingRushToyMotion", "KingRushSkyMotion", "KingRushCastleMotion"):
            self.assertEqual(self.parents[name], "KinematicObstacle")
        source = (ASSETS / "Scripts/Gameplay/Obstacles/KinematicObstacle.cs").read_text()
        self.assertIn("RequireComponent(typeof(Rigidbody))", source)
        self.assertEqual(self.parents["KinematicObstacle"], "Obstacle")


if __name__ == "__main__":
    unittest.main()
