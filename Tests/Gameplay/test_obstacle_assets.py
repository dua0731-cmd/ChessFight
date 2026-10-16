"""Serialization/inheritance checks; these do not simulate Unity or PhysX.

Run: python3 -m unittest discover -s Tests/Gameplay -p 'test_*.py' -v
"""
from pathlib import Path
import re
import math
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
            2: {"context", "jumpHeight", "airMoveSpeed", "airAcceleration", "launchCooldown"},
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

    def test_pusher_and_hammer_speed_force_migration_preserves_default_motion(self):
        for number, speed, force in ((3, "moveSpeed", 9), (4, "rotationSpeed", 12)):
            prefab, = PREFABS.glob(f"{number:02}_*.prefab")
            component, = [b for b in blocks(prefab.read_text()).values()
                          if "::ChessFight.ProtectKing." + TYPES[number - 1] + "\n" in b]
            values = fields(component)
            self.assertTrue({speed, "pushForce", "lift", "hitCooldown"} <= values.keys())
            self.assertTrue({"period", "strength", "degreesPerSecond"}.isdisjoint(values))
            self.assertEqual(float(values["pushForce"]), force)
            if number == 3:
                # The original amplitude 4.5 m and period 4 s must survive conversion to peak speed.
                self.assertAlmostEqual(float(values[speed]), 2 * math.pi * 4.5 / 4, places=6)
            else:
                self.assertEqual(float(values[speed]), 70)
        scene = (ASSETS / "Scenes/ImportedChessFightMap.unity").read_text()
        reversed_hammers = [b for b in blocks(scene).values()
                            if "::ChessFight.ProtectKing.RotatingHammer\n" in b and
                            float(fields(b)["rotationSpeed"]) < 0]
        self.assertTrue(reversed_hammers, "Existing reverse rotation must survive migration")

    def test_pusher_hammer_controls_live_on_root_and_move_only_child_body(self):
        for number in (3, 4):
            prefab, = PREFABS.glob(f"{number:02}_*.prefab")
            records = blocks(prefab.read_text())
            component_id, component = next((k, b) for k, b in records.items()
                if "::ChessFight.ProtectKing." + TYPES[number - 1] + "\n" in b)
            root_id, root = next((k, b) for k, b in records.items()
                if "  m_Name: " + prefab.stem + "\n" in b)
            self.assertEqual(fields(component)["m_GameObject"], "{fileID: " + root_id + "}")
            self.assertIn("component: {fileID: " + component_id + "}", root)
            body_id = re.search(r"fileID: (\d+)", fields(component)["movingBody"])[1]
            child_id = re.search(r"fileID: (\d+)", fields(records[body_id])["m_GameObject"])[1]
            self.assertNotEqual(child_id, root_id)
            self.assertIn("component: {fileID: " + body_id + "}", records[child_id])
            self.assertNotIn("component: {fileID: " + component_id + "}", records[child_id])
            for b in records.values():
                if b.startswith("--- !u!54"):
                    self.assertNotEqual(fields(b)["m_GameObject"], "{fileID: " + root_id + "}")

    def test_weighted_bridge_root_controls_and_tile_geometry_references(self):
        prefab, = PREFABS.glob("07_*.prefab")
        records = blocks(prefab.read_text())
        component, = [b for b in records.values() if "::ChessFight.ProtectKing.WeightedBridge\n" in b]
        values = fields(component)
        root_id, root = next((k, b) for k, b in records.items() if "  m_Name: 07_WeightedBridge\n" in b)
        self.assertEqual(values["m_GameObject"], "{fileID: " + root_id + "}")
        self.assertEqual((int(values["tilesX"]), int(values["tilesZ"])), (4, 8))
        self.assertEqual(float(values["tileSize"]), 1.5)
        self.assertNotIn("deckSize", values)
        self.assertEqual(float(values["maximumTilt"]), 24)
        for reference in ("deckBody", "solidDeck", "tileMesh", "leftStripe", "rightStripe", "entryRamp", "exitRamp"):
            target = re.search(r"fileID: (\d+)", values[reference])[1]
            self.assertIn(target, records)
        deck_id = re.search(r"fileID: (\d+)", values["deckBody"])[1]
        self.assertNotEqual(fields(records[deck_id])["m_GameObject"], values["m_GameObject"])
        tile_id = re.search(r"fileID: (\d+)", values["tileMesh"])[1]
        tile_go = fields(records[tile_id])["m_GameObject"]
        renderer, = [b for b in records.values() if b.startswith("--- !u!23") and fields(b)["m_GameObject"] == tile_go]
        self.assertEqual(len(re.findall(r"^  - \{fileID: 2100000, guid:", renderer, re.M)), 2)
        source = (CODE / "WeightedBridge.cs").read_text()
        for field in ("tilesX", "tilesZ", "maximumTilt"):
            self.assertRegex(source, r"\[SerializeField,[^\]]+\]\s+(?:int|float) " + field)

    def test_bridge_uses_contact_driven_hinge_and_only_deck_transports_riders(self):
        source = (CODE / "WeightedBridge.cs").read_text()
        self.assertNotIn("CalculateTarget", source)
        self.assertNotIn("TrySupport", source)
        self.assertNotIn("OverlapBox", source)
        self.assertIn("AddComponent<HingeJoint>()", source)
        self.assertIn("hinge.useLimits = true", source)
        self.assertIn("body.GetPointVelocity", source)
        self.assertIn("Physics.IgnoreCollision(centralSupport, deckCollider, true)", source)
        prefab, = PREFABS.glob("07_*.prefab")
        records = blocks(prefab.read_text())
        component, = [b for b in records.values() if "::ChessFight.ProtectKing.WeightedBridge\n" in b]
        values = fields(component)
        self.assertTrue({"supportGrace", "degreesPerPlayer", "tiltSpeed"}.isdisjoint(values))
        self.assertEqual(float(values["deckMass"]), 40)
        for field in ("returnSpring", "returnDamper", "maximumAngularSpeed"):
            self.assertGreater(float(values[field]), 0)
        support_id = re.search(r"fileID: (\d+)", values["centralSupport"])[1]
        self.assertTrue(records[support_id].startswith("--- !u!65"))
        surface, = [b for b in records.values() if "::ChessFight.ProtectKing.ImportedMovingSurface\n" in b]
        self.assertNotEqual(fields(surface)["m_GameObject"], values["m_GameObject"])
        guid = re.search(r"m_Script: .*guid: (\w+)", surface)[1]
        self.assertEqual(self.scripts[guid], CODE / "ImportedMovingSurface.cs")
        self.assertNotIn("WeightedBridgeSurface", source)
        self.assertIn("ImportedMovingSurface.BindDynamic(body, PointVelocity", source)

    def test_bridge_practice_scene_has_clearance_and_resizing_fixture(self):
        records = blocks((ASSETS / "Scenes/ObstacleRagdollTest.unity").read_text())
        fixture, = [b for b in records.values() if "::ChessFight.RagdollLab.WeightedBridgePracticeArea\n" in b]
        self.assertEqual(fields(fixture)["m_GameObject"], "{fileID: 800201}")
        for field, value in fields(fixture).items():
            if field.startswith("m_"): continue
            target = re.search(r"fileID: (\d+)", value)[1]
            self.assertIn(target, records)
        # The old side floors began at 3.1 m; keep a full metre clear of the default half width.
        for target in ("800216", "800221"):
            transform = fields(records[target])
            x = abs(float(re.search(r"x: ([\d.-]+)", transform["m_LocalPosition"])[1]))
            width = float(re.search(r"x: ([\d.-]+)", transform["m_LocalScale"])[1])
            self.assertGreaterEqual(x - width / 2, 4)
        for target in ("800206", "800211"):
            transform = fields(records[target])
            z = abs(float(re.search(r"z: ([\d.-]+)", transform["m_LocalPosition"])[1]))
            length = float(re.search(r"z: ([\d.-]+)", transform["m_LocalScale"])[1])
            self.assertGreaterEqual(z - length / 2, 6.5)

    def test_all_obstacle_prefab_roots_expose_a_settings_inspector(self):
        def is_obstacle(name):
            visited = set()
            while name not in visited:
                if name == "Obstacle": return True
                visited.add(name); name = self.parents.get(name, "")
            return False
        checked = 0
        for prefab in ASSETS.rglob("*.prefab"):
            records = blocks(prefab.read_text())
            obstacle_owners, settings_owners = set(), set()
            for block in records.values():
                match = re.search(r"m_Script: .*guid: (\w+)", block)
                if match is None or match[1] not in self.scripts: continue
                name = self.scripts[match[1]].stem
                owner = fields(block)["m_GameObject"]
                if is_obstacle(name): obstacle_owners.add(owner)
                if name == "ObstacleContext": settings_owners.add(owner)
            if not obstacle_owners: continue
            roots = [b for b in records.values() if b.startswith("--- !u!4") and fields(b).get("m_Father") == "{fileID: 0}"]
            if not roots: continue
            root = fields(roots[0])["m_GameObject"]
            self.assertIn(root, obstacle_owners | settings_owners, str(prefab))
            checked += 1
        self.assertGreaterEqual(checked, 32)

    def test_platform_wall_gate_speed_migration_preserves_original_periods(self):
        for number, name, original, factor in ((5, "RisingFallingTiles", 6, 1),
            (6, "SlidingWall", 4.5, 1.5/.7), (9, "ChessClockGate", 5, 1.5/.7)):
            prefab, = PREFABS.glob(f"{number:02}_*.prefab")
            values = [fields(b) for b in blocks(prefab.read_text()).values()
                      if "::ChessFight.ProtectKing." + name + "\n" in b]
            self.assertTrue(values)
            for data in values:
                self.assertNotIn("period", data)
                period = 2 * math.pi * float(data["distance"]) * factor / float(data["moveSpeed"])
                self.assertAlmostEqual(period, original, places=6)
        # Unity permits SerializeField on fields, not on expression-bodied properties.
        for path in self.scripts.values():
            self.assertNotRegex(path.read_text(), r"\[SerializeField\] public \w+ \w+\s*=>", str(path))

    def test_folding_panels_use_downward_gravity_hinges_with_mount_clearance(self):
        prefab = PREFABS / "12_FoldingBridge.prefab"
        records = blocks(prefab.read_text())
        component, = [b for b in records.values() if "::ChessFight.ProtectKing.FoldingBridge\n" in b]
        data = fields(component)
        self.assertTrue({"OpenDuration", "FoldDuration", "Simultaneous"}.isdisjoint(data))
        for field in ("LeftPivot", "RightPivot"):
            body_id = re.search(r"fileID: (\d+)", data[field])[1]
            body = fields(records[body_id])
            self.assertEqual(body["m_IsKinematic"], "0")
            self.assertEqual(body["m_UseGravity"], "1")
            self.assertGreater(float(body["m_Mass"]), 0)
        for mount in ("6861347175342721897", "728252298897654261"):
            value = fields(records[mount])["m_LocalPosition"]
            self.assertGreaterEqual(abs(float(re.search(r"x: ([\d.-]+)", value)[1])) - 1.5 - 4, .35)
        source = (CODE / "FoldingBridge.cs").read_text()
        self.assertNotIn("MoveRotation", source)
        self.assertNotIn("Evaluate(float elapsed)", source)
        self.assertIn("body.ResetCenterOfMass()", source)
        self.assertIn("limits.min = left ? -Mathf.Clamp", source)
        self.assertIn("limits.max = left ? 0f", source)

    def test_floor_piston_is_a_bounded_acceleration_lift_without_launch(self):
        source = (CODE / "FloorPiston.cs").read_text()
        for old in ("LaunchFromObstacle", "LaunchAbove", "LaunchForce", "HorizontalForce"):
            self.assertNotIn(old, source)
        self.assertIn("Mathf.Abs(Physics.gravity.y) * .5f", source)
        self.assertIn("Mathf.Sqrt(6f * Mathf.Max(0f, RiseHeight) / SafeAcceleration)", source)
        records = blocks((PREFABS / "13_FloorPiston.prefab").read_text())
        component, = [b for b in records.values() if "::ChessFight.ProtectKing.FloorPiston\n" in b]
        data = fields(component)
        self.assertTrue({"LaunchForce", "HorizontalForce", "HitArea"}.isdisjoint(data))
        height, acceleration = float(data["RiseHeight"]), min(float(data["maximumAcceleration"]), 9.81 * .5)
        for duration in ("RiseDuration", "FallDuration"):
            actual = max(float(data[duration]), math.sqrt(6 * height / acceleration))
            self.assertLessEqual(6 * height / actual ** 2, 9.81 * .5)

    def test_box_and_round_vent_variants_share_logic_and_match_wind_shapes(self):
        values = []
        for filename, shape, mesh in (("15_AirVent.prefab", "0", "10202"), ("AirVent_Round.prefab", "1", "10206")):
            records = blocks((PREFABS / filename).read_text())
            component, = [b for b in records.values() if "::ChessFight.ProtectKing.AirVent\n" in b]
            data = fields(component); values.append(data)
            self.assertEqual(data["shape"], shape)
            volume_mesh = records["9008100000000000002"]
            self.assertIn("m_Mesh: {fileID: " + mesh, volume_mesh)
            trigger = fields(records[re.search(r"fileID: (\d+)", data["WindTrigger"])[1]])
            self.assertEqual(trigger["m_Size"], "{x: 3, y: 3, z: 6}")
        for setting in ("WindStrength", "WindDirection", "MaxPushSpeed", "VerticalForce", "IdleDuration", "BlowDuration", "WarningDuration"):
            self.assertEqual(values[0][setting], values[1][setting])
        source = (CODE / "AirVent.cs").read_text()
        self.assertIn("shape == AirVentShape.Cylinder && !OverlapsCylinder(buffer[i])", source)
        self.assertIn("other.ClosestPoint", source)
        self.assertIn("x * x + y * y <=", source)
        scene = (ASSETS / "Scenes/ObstacleRagdollTest.unity").read_text()
        for filename in ("15_AirVent.prefab", "AirVent_Round.prefab"):
            guid = re.search(r"guid: (\w+)", (PREFABS / (filename + ".meta")).read_text())[1]
            self.assertIn("m_SourcePrefab: {fileID: 100100000, guid: " + guid, scene)
        self.assertEqual(len(re.findall(r"^--- !u!\d+ &(\d+)", scene, re.M)), len(blocks(scene)))

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
                self.assertTrue({"strength", "pushForce", "lift", "hitCooldown"}.isdisjoint(relay))
                pendulum = relay.get("pendulum", "{fileID: 0}") != "{fileID: 0}"
                target = re.search(r"fileID: (\d+)", relay["pendulum" if pendulum else "motion"])[1]
                owner = fields(records[target])
                required = {"hitCooldown", "KnockbackForce", "UpwardForce"} if pendulum else {"hitCooldown", "strength", "lift"}
                if "::ChessFight.ProtectKing.ReciprocatingPusher\n" in records[target] or "::ChessFight.ProtectKing.RotatingHammer\n" in records[target]:
                    required = {"hitCooldown", "pushForce", "lift"}
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
