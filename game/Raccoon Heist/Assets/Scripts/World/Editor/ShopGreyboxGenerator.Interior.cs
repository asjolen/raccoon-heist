using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RaccoonHeist.Player;
using RaccoonHeist.World;

namespace RaccoonHeist.World.Editor
{
    // Shop structure and fixtures: floors, walls, back room, storage room, shelves, stock.
    public static partial class ShopGreyboxGenerator
    {
        // ---------- structure ----------

        // Outdoor compound bounds (perimeter walls sit just outside these)
        const float OutW0 = -5f, OutW1 = W + 5f;   // x range
        const float OutD0 = -14f, OutD1 = 25f;      // z range: two-lane street | shop block | alley

        static void BuildGroundAndCeilings()
        {
            BoxMinMax("Ground", new Vector3(OutW0 - 0.3f, -0.1f, OutD0 - 0.3f), new Vector3(OutW1 + 0.3f, 0f, OutD1 + 0.3f), null,
                TiledMat("Rough", new Color(0.40f, 0.40f, 0.42f), TexRough, 18f, 22f));
            // Zone overlays: read where you are by the ground underfoot. The street
            // continues beyond the playable block but ends at authored facade caps;
            // an actually endless plane creates empty sightlines rather than scale.
            // Road has painted markings; pavements are raised 0.12 m with curb faces
            // (hop-able: raccoon step offset handles it).
            bool cityRoads = SyntyPrefab("SM_Env_Road_Lines_01") != null;
            BoxMinMax("Road", new Vector3(CityWestEnd, 0f, -12f), new Vector3(CityEastEnd, 0.02f, -2f), null,
                cityRoads ? TiledMat("AsphaltWet", new Color(0.30f, 0.32f, 0.38f), TexAsphalt, 42f, 2f, 0.42f)
                          : TiledMat("RoadMarkedWet", new Color(0.38f, 0.40f, 0.46f), TexRoad, 14f, 1f, 0.42f));
            if (cityRoads)
                for (float x0 = CityWestEnd; x0 < CityEastEnd; x0 += 5f)
                {
                    bool crossing = Mathf.Approximately(x0, -10f) || Mathf.Approximately(x0, 15f);
                    foreach (float laneZ in new[] { -4.5f, -9.5f })
                        PlaceSynty(crossing ? "SM_Env_Road_Crossing_01" : "SM_Env_Road_01",
                            new Vector3(x0 + 2.5f, 0.028f, laneZ), crossing ? 0f : 90f);
                }

            // A broken centre stripe makes the ten-metre surface unmistakably read
            // as two opposing lanes even when the prefab markings are in shadow.
            var roadStripe = Mat("RoadCentreStripe", new Color(0.64f, 0.60f, 0.43f), 0.18f);
            int stripeIndex = 0;
            for (float x0 = CityWestEnd + 1f; x0 < CityEastEnd; x0 += 6f)
                BoxMinMax($"RoadCentreStripe_{stripeIndex++}", new Vector3(x0, 0.031f, -7.055f),
                    new Vector3(x0 + 3.1f, 0.037f, -6.945f), null, roadStripe);

            // The opposite pavement has a wall-side furniture verge beyond the
            // playable limit, a clear walking strip, then curb-side lamp hardware.
            // Split both front pavements at the side-road crossings. The former
            // full-width slabs formed raised bars across the east and west streets.
            BuildFrontSidewalkSegments("Pavement_Far", OutD0 - 1.5f, -12f);
            BuildFrontSidewalkSegments("Pavement_Shop", -2f, 0f);
            // The service route is a narrow asphalt lane rather than one broad,
            // stretched concrete plane. Matte outdoor concrete aprons hug the walls and
            // a dark gutter separates them from the roadway, making every edge intentional.
            var serviceAsphalt = TiledMat("ServiceAsphaltOutdoor", new Color(0.24f, 0.26f, 0.31f), TexAsphalt, 5f, 12f, 0.14f);
            var perimeterSlabs = TiledMat("PerimeterConcreteOutdoor", new Color(0.34f, 0.35f, 0.38f), TexRough, 3f, 12f, 0.05f);
            var serviceGutter = Mat("ServiceGutter", new Color(0.15f, 0.17f, 0.20f), 0.10f);
            BoxMinMax("PassageAsphalt", new Vector3(W + T, 0.001f, 0f), new Vector3(OutW1, 0.017f, StorageZ1 + T), null, serviceAsphalt);
            BoxMinMax("AlleyAsphalt", new Vector3(OutW0, 0.001f, StorageZ1 + T), new Vector3(OutW1, 0.017f, OutD1), null, serviceAsphalt);
            BoxMinMax("EastWallApron", new Vector3(W + T, 0.018f, 0f), new Vector3(W + T + 1.15f, 0.075f, D + T), null, perimeterSlabs);
            BoxMinMax("EastWallGutter", new Vector3(W + T + 1.15f, 0.018f, 0f), new Vector3(W + T + 1.33f, 0.045f, D + T), null, serviceGutter);
            BoxMinMax("RearWallApron", new Vector3(OutW0, 0.018f, StorageZ1 + T), new Vector3(StorageX1, 0.075f, StorageZ1 + T + 1.05f), null, perimeterSlabs);
            BoxMinMax("RearWallGutter", new Vector3(OutW0, 0.018f, StorageZ1 + T + 1.05f), new Vector3(StorageX1, 0.045f, StorageZ1 + T + 1.23f), null, serviceGutter);
            BoxMinMax("ShopFloor", new Vector3(0f, 0f, 0f), new Vector3(W, 0.02f, D), null,
                TiledMat("Planks", new Color(0.62f, 0.50f, 0.38f), TexPlanks, 7f, 8f));
            BoxMinMax("BackRoomFloor", new Vector3(0f, 0f, D + T), new Vector3(3f, 0.02f, D + T + 4f), null,
                TiledMat("Planks", new Color(0.62f, 0.50f, 0.38f), TexPlanks, 2f, 2f));
            BoxMinMax("StorageFloor", new Vector3(StorageX0 + T, 0f, D + T), new Vector3(StorageX1 - T, 0.02f, StorageZ1), null,
                TiledMat("Planks", new Color(0.62f, 0.50f, 0.38f), TexPlanks, 3f, 3f));

            // Main shop ceiling has a 1 x 1 m skylight hole at (6.5-7.5, 10-11):
            // roof route drop-in, one-way. Curb frame marks it on the roof.
            var ceil = Mat("Ceiling", new Color(0.30f, 0.28f, 0.25f));
            BoxMinMax("Ceiling_West", new Vector3(-T, H, -T), new Vector3(6.5f, H + 0.1f, D + T), null, ceil);
            BoxMinMax("Ceiling_East", new Vector3(7.5f, H, -T), new Vector3(W + T, H + 0.1f, D + T), null, ceil);
            BoxMinMax("Ceiling_SkyS", new Vector3(6.5f, H, -T), new Vector3(7.5f, H + 0.1f, 10f), null, ceil);
            BoxMinMax("Ceiling_SkyN", new Vector3(6.5f, H, 11f), new Vector3(7.5f, H + 0.1f, D + T), null, ceil);
            var curb = Mat("SkylightCurb", new Color(0.5f, 0.5f, 0.52f));
            BoxMinMax("SkylightCurb_S", new Vector3(6.4f, H + 0.1f, 9.9f), new Vector3(7.6f, H + 0.35f, 10f), null, curb);
            BoxMinMax("SkylightCurb_N", new Vector3(6.4f, H + 0.1f, 11f), new Vector3(7.6f, H + 0.35f, 11.1f), null, curb);
            BoxMinMax("SkylightCurb_W", new Vector3(6.4f, H + 0.1f, 10f), new Vector3(6.5f, H + 0.35f, 11f), null, curb);
            BoxMinMax("SkylightCurb_E", new Vector3(7.5f, H + 0.1f, 10f), new Vector3(7.6f, H + 0.35f, 11f), null, curb);

            BoxMinMax("Ceiling_BackRoom", new Vector3(-T, H, D + T), new Vector3(3f + T, H + 0.1f, D + T + 4f + T), null, ceil);
            BoxMinMax("Ceiling_Storage", new Vector3(StorageX0, H, D), new Vector3(StorageX1, H + 0.1f, StorageZ1 + T), null, ceil);
        }

        static void BuildFrontSidewalkSegments(string name, float zMin, float zMax)
        {
            var runs = new[]
            {
                new Vector2(CityWestEnd, OutW0 - 9f),
                new Vector2(OutW0 - 2f, OutW1 + 2f),
                new Vector2(OutW1 + 9f, CityEastEnd)
            };
            for (int i = 0; i < runs.Length; i++)
            {
                float width = runs[i].y - runs[i].x;
                BoxMinMax($"{name}_{i}", new Vector3(runs[i].x, 0f, zMin),
                    new Vector3(runs[i].y, 0.12f, zMax), null, SidewalkMat(width, zMax - zMin));
            }
        }

        static void BuildFrontWall()
        {
            float doorMin = EntranceX - EntranceWidth * 0.5f;
            float doorMax = EntranceX + EntranceWidth * 0.5f;
            const float windowEdge = 0.5f;
            const float jamb = 0.22f;

            // A centered entrance gives the fourteen-metre facade a clear hierarchy:
            // display window, door, display window. The real wall openings match the
            // decorative facade rather than hiding a sealed wall behind it.
            BoxMinMax("FrontWall_LeftEdge", new Vector3(-T, 0f, -T), new Vector3(windowEdge, H, 0f), null, Wall);
            BoxMinMax("FrontWall_LeftWindowSill", new Vector3(windowEdge, 0f, -T), new Vector3(doorMin - jamb, 0.9f, 0f), null, Wall);
            BoxMinMax("FrontWall_LeftWindowHeader", new Vector3(windowEdge, 2.5f, -T), new Vector3(doorMin - jamb, H, 0f), null, Wall);
            BoxMinMax("FrontWall_LeftJamb", new Vector3(doorMin - jamb, 0f, -T), new Vector3(doorMin, H, 0f), null, Wall);
            BoxMinMax("FrontWall_DoorHeader", new Vector3(doorMin, ShopConstants.DoorwayHeight, -T), new Vector3(doorMax, H, 0f), null, Wall);
            BoxMinMax("FrontWall_RightJamb", new Vector3(doorMax, 0f, -T), new Vector3(doorMax + jamb, H, 0f), null, Wall);
            BoxMinMax("FrontWall_RightWindowSill", new Vector3(doorMax + jamb, 0f, -T), new Vector3(W - windowEdge, 0.9f, 0f), null, Wall);
            BoxMinMax("FrontWall_RightWindowHeader", new Vector3(doorMax + jamb, 2.5f, -T), new Vector3(W - windowEdge, H, 0f), null, Wall);
            BoxMinMax("FrontWall_RightEdge", new Vector3(W - windowEdge, 0f, -T), new Vector3(W + T, H, 0f), null, Wall);

            // The panel and pet flap share a real hinge. The door opens from both sides;
            // only a street-side opening is treated as a break-in by the shop alarm.
            var pivot = new GameObject("EntranceDoorPivot").transform;
            pivot.SetParent(root, false);
            pivot.position = new Vector3(doorMin, 0f, -0.10f);
            Box("FrontDoor", new Vector3(EntranceWidth * 0.5f, ShopConstants.DoorwayHeight * 0.5f, 0f),
                new Vector3(EntranceWidth, ShopConstants.DoorwayHeight, 0.10f), pivot,
                Mat("Door", new Color(0.22f, 0.25f, 0.29f), 0.18f));
            Box("PetFlap", new Vector3(EntranceWidth * 0.5f, 0.185f, -0.07f), new Vector3(0.4f, 0.33f, 0.05f), pivot,
                Mat("PetFlap", new Color(0.34f, 0.29f, 0.24f), 0.12f));
            pivot.gameObject.AddComponent<HingedDoor>();
        }

        static void BuildSideAndRearWalls()
        {
            // West wall runs the full length of shop + back room
            BoxMinMax("WestWall", new Vector3(-T, 0f, -T), new Vector3(0f, H, D + T + 4f + T), null, Wall);

            // East wall with the crouch-only floor duct near the front
            // (quiet entry/extraction).
            BoxMinMax("EastWall_Front", new Vector3(W, 0f, -T), new Vector3(W + T, H, 1f), null, Wall);
            BoxMinMax("EastWall_AboveVent", new Vector3(W, CrawlVentHeight, EastVentZ0),
                new Vector3(W + T, H, EastVentZ1), null, Wall);
            BoxMinMax("EastWall_Rear", new Vector3(W, 0f, EastVentZ1), new Vector3(W + T, H, D + T), null, Wall);

            // Rear wall of the main shop: Harold's doorway (west) + open storage doorway (east)
            BoxMinMax("RearWall_West", new Vector3(0f, 0f, D), new Vector3(1.05f, H, D + T), null, Wall);
            BoxMinMax("RearWall_HaroldDoorHeader", new Vector3(1.05f, ShopConstants.DoorwayHeight, D), new Vector3(1.95f, H, D + T), null, Wall);
            BoxMinMax("RearWall_Mid", new Vector3(1.95f, 0f, D), new Vector3(7.3f, H, D + T), null, Wall);
            BoxMinMax("RearWall_StorageDoorHeader", new Vector3(7.3f, ShopConstants.DoorwayHeight, D), new Vector3(8.5f, H, D + T), null, Wall);
            BoxMinMax("RearWall_East_A", new Vector3(8.5f, 0f, D),
                new Vector3(StorageBypassX0, H, D + T), null, Wall);
            BoxMinMax("RearWall_StorageCrawlHeader", new Vector3(StorageBypassX0, CrawlVentHeight, D),
                new Vector3(StorageBypassX1, H, D + T), null, Wall);
            BoxMinMax("RearWall_East_B", new Vector3(StorageBypassX1, 0f, D),
                new Vector3(W + T, H, D + T), null, Wall);
        }

        static void BuildBackRoom()
        {
            float z0 = D + T;                 // Harold's room interior: x 0–3, one doorway. Sacred.
            float z1 = z0 + ShopConstants.BackRoomDepth;
            BoxMinMax("BackRoom_EastWall", new Vector3(3f, 0f, z0), new Vector3(3f + T, H, z1 + T), null, Wall);
            BoxMinMax("BackRoom_NorthWall", new Vector3(-T, 0f, z1), new Vector3(3f + T, H, z1 + T), null, Wall);

            // Cot on legs — the gap underneath is a crouch-only hiding spot
            var cotMat = Mat("CotFrame", new Color(0.35f, 0.30f, 0.25f));
            BoxMinMax("CotLeg_A", new Vector3(0.30f, 0f, z1 - 2.10f), new Vector3(0.38f, 0.38f, z1 - 2.02f), null, cotMat);
            BoxMinMax("CotLeg_B", new Vector3(1.12f, 0f, z1 - 2.10f), new Vector3(1.20f, 0.38f, z1 - 2.02f), null, cotMat);
            BoxMinMax("CotLeg_C", new Vector3(0.30f, 0f, z1 - 0.18f), new Vector3(0.38f, 0.38f, z1 - 0.10f), null, cotMat);
            BoxMinMax("CotLeg_D", new Vector3(1.12f, 0f, z1 - 0.18f), new Vector3(1.20f, 0.38f, z1 - 0.10f), null, cotMat);
            BoxMinMax("CotPlatform", new Vector3(0.30f, 0.38f, z1 - 2.10f), new Vector3(1.20f, 0.46f, z1 - 0.10f), null, cotMat);
            BoxMinMax("CotBlanket", new Vector3(0.32f, 0.46f, z1 - 1.70f), new Vector3(1.18f, 0.54f, z1 - 0.12f), null,
                Mat("Blanket", new Color(0.45f, 0.18f, 0.16f)));
            BoxMinMax("CotPillow", new Vector3(0.42f, 0.46f, z1 - 2.05f), new Vector3(1.08f, 0.58f, z1 - 1.78f), null,
                Mat("Pillow", new Color(0.80f, 0.78f, 0.70f)));

            // The scoring hole in the floorboards — THE drop-off point, right by the cot.
            // Visual marker for now; deposit/dive mechanics come with the loot system.
            BoxMinMax("FloorHole", new Vector3(1.6f, 0.021f, z1 - 0.9f), new Vector3(2.3f, 0.03f, z1 - 0.2f), null,
                Mat("HoleDark", new Color(0.05f, 0.04f, 0.03f)));

            SpawnHarold(new Vector3(2.2f, 0f, z1 - 2.7f));
        }

        static void BuildStorageRoom()
        {
            BoxMinMax("Storage_WestWall", new Vector3(StorageX0, 0f, D + T), new Vector3(StorageX0 + T, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_EastWall", new Vector3(StorageX1 - T, 0f, D + T), new Vector3(StorageX1, H, StorageZ1 + T), null, Wall);

            // North wall has the creaky back window (x 8.2-9.0, y 1.5-2.3),
            // reachable from the alley dumpster. Its real collider prevents ghosting;
            // smashing it is a loud entry until the future quiet-unlock mechanic lands.
            BoxMinMax("Storage_NorthWall_W_A", new Vector3(StorageX0, 0f, StorageZ1),
                new Vector3(RearBreakVentX0, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_BreakVentHeader", new Vector3(RearBreakVentX0, CrawlVentHeight, StorageZ1),
                new Vector3(RearBreakVentX1, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_W_B", new Vector3(RearBreakVentX1, 0f, StorageZ1),
                new Vector3(8.2f, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_E", new Vector3(9f, 0f, StorageZ1), new Vector3(StorageX1, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_Sill", new Vector3(8.2f, 0f, StorageZ1), new Vector3(9f, 1.5f, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_Header", new Vector3(8.2f, 2.3f, StorageZ1), new Vector3(9f, H, StorageZ1 + T), null, Wall);
            BoxMinMax("BackWindowSill", new Vector3(8.15f, 1.45f, StorageZ1 - 0.15f), new Vector3(9.05f, 1.5f, StorageZ1 + T + 0.15f), null, Wood);
            BuildBreakableWindowPane("BackStorage",
                new Vector3(8.24f, 1.54f, StorageZ1 + T - 0.01f),
                new Vector3(8.96f, 2.26f, StorageZ1 + T + 0.01f),
                TransparentMat("BackWindowGlass", new Color(0.10f, 0.25f, 0.38f, 0.34f)), 3);

            // Tall stock racks (2.4 m) hugging the side walls
            BuildShelfUnit(root, new Vector3(StorageX1 - T - 0.3f, 0f, D + 1.6f), 90f, 2.4f);
            BuildShelfUnit(root, new Vector3(StorageX1 - T - 0.3f, 0f, D + 3.8f), 90f, 2.4f);
            BuildShelfUnit(root, new Vector3(StorageX0 + T + 0.3f, 0f, D + 2.7f), 90f, 2.4f);

            // Crate maze in the middle — gaps are raccoon-sized hiding slots
            CrateStack("StorageCrate_1", 6.8f, D + 1.2f, 0.6f);
            CrateStack("StorageCrate_2", 7.5f, D + 1.1f, 1.2f);
            CrateStack("StorageCrate_3", 8.7f, D + 1.3f, 0.6f);
            CrateStack("StorageCrate_4", 6.9f, D + 2.4f, 1.2f);
            CrateStack("StorageCrate_5", 8.0f, D + 2.6f, 0.6f);
            CrateStack("StorageCrate_6", 9.1f, D + 2.3f, 1.2f);
            CrateStack("StorageCrate_7", 7.4f, D + 3.7f, 0.6f);
            CrateStack("StorageCrate_8", 8.6f, D + 3.9f, 1.2f);
        }

        static void CrateStack(string name, float x, float z, float height)
        {
            if (SyntyPrefab("SM_Gen_Prop_Crate_01") != null)
            {
                var baseCrate = PlaceSynty("SM_Gen_Prop_Crate_01", new Vector3(x + 0.3f, 0f, z + 0.3f), 90f * Random.Range(0, 4));
                baseCrate.name = name;
                if (height > 0.9f)
                {
                    float h = GeometryBounds(baseCrate).size.y;
                    PlaceSynty("SM_Gen_Prop_Cardboard_Box_02", new Vector3(x + 0.3f, h, z + 0.3f), 90f * Random.Range(0, 4));
                }
                return;
            }
            BoxMinMax(name, new Vector3(x, 0f, z), new Vector3(x + 0.6f, height, z + 0.6f), null, Crate);
        }

        // ---------- fixtures & stock ----------

        static void BuildFixtures()
        {
            if (SyntyAvailable)
            {
                Debug.Log("Raccoon Heist: POLYGON Shops pack detected — dressing the shop with Synty assets.");
                BuildSyntyAisles();
                BuildSyntyFridgeRun();
                PlaceSynty("SM_Prop_Market_Checkout_Large_01", new Vector3(W - 4.25f, 0f, 1.6f));
                return;
            }

            var shelves = new GameObject("Shelves").transform;
            shelves.SetParent(root, false);

            // Shelf grid scales with the shop: rows every (depth + aisle), units every 2.5 m
            // with 0.5 m squeeze-gaps between neighbouring units
            var rowZ = new List<float>();
            for (float z = 5f; z <= D - 5.5f; z += ShopConstants.ShelfDepth + ShopConstants.AisleWidth)
                rowZ.Add(z);
            var unitX = new List<float>();
            for (float x = 3.5f; x <= W - 2.5f; x += 2.5f)
                unitX.Add(x);
            foreach (float z in rowZ)
                foreach (float x in unitX)
                    BuildShelfUnit(shelves, new Vector3(x, 0f, z));

            // Fridge case pulled off the west wall — the 0.5 m gap behind it is a hiding slot
            BoxMinMax("FridgeCase", new Vector3(0.5f, 0f, 3f), new Vector3(1.3f, 2f, D - 3f), null,
                Mat("Fridge", new Color(0.72f, 0.75f, 0.78f), 0.35f));

            // Counter: solid from the front, hollow underneath from behind — a hiding cave
            float cx0 = W - 6.5f, cx1 = W - 2f;
            var counterMat = Mat("Counter", new Color(0.36f, 0.26f, 0.17f));
            BoxMinMax("Counter_Front", new Vector3(cx0, 0f, 1.2f), new Vector3(cx1, 0.9f, 1.32f), null, counterMat);
            BoxMinMax("Counter_SideW", new Vector3(cx0, 0f, 1.32f), new Vector3(cx0 + 0.12f, 0.9f, 2f), null, counterMat);
            BoxMinMax("Counter_SideE", new Vector3(cx1 - 0.12f, 0f, 1.32f), new Vector3(cx1, 0.9f, 2f), null, counterMat);
            BoxMinMax("Counter_Top", new Vector3(cx0, 0.9f, 1.2f), new Vector3(cx1, 1f, 2f), null, counterMat);
            BoxMinMax("CashRegister", new Vector3(cx0 + 0.5f, 1f, 1.4f), new Vector3(cx0 + 1f, 1.35f, 1.8f), null,
                Mat("Register", new Color(0.22f, 0.22f, 0.26f)));

            for (int i = 0; i < 3; i++)
                CreateProp(root, new Vector3(Random.Range(cx0 + 1.2f, cx1 - 0.3f), 1f, Random.Range(1.35f, 1.85f)));
        }

        static void BuildShelfUnit(Transform parent, Vector3 floorCenter, float yaw = 0f, float height = ShopConstants.ShelfHeight)
        {
            float L = ShopConstants.ShelfLength;
            float Dp = ShopConstants.ShelfDepth;

            var unit = new GameObject("ShelfUnit").transform;
            unit.SetParent(parent, false);
            unit.localPosition = floorCenter;
            unit.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box("Side_A", new Vector3(-(L - 0.04f) / 2f, height / 2f, 0f), new Vector3(0.04f, height, Dp), unit, Wood);
            Box("Side_B", new Vector3((L - 0.04f) / 2f, height / 2f, 0f), new Vector3(0.04f, height, Dp), unit, Wood);

            // Board surfaces are jump-height steps: raccoon (1 m jump) can hop board to board
            var boardTops = new List<float>();
            for (float t = 0.1f; t <= height - 0.35f; t += 0.45f) boardTops.Add(t);
            boardTops.Add(height);
            foreach (float top in boardTops)
            {
                Box("Board", new Vector3(0f, top - 0.02f, 0f), new Vector3(L - 0.08f, 0.04f, Dp), unit, Wood);
                int count = Random.Range(2, 4);
                for (int i = 0; i < count; i++)
                    CreateProp(unit, new Vector3(Random.Range(-0.85f, 0.85f), top, Random.Range(-0.16f, 0.16f)));
            }
        }

        static void CreateProp(Transform parent, Vector3 localBase)
        {
            if (TryCreateSyntyProduct(parent, localBase)) return;

            GameObject prop;
            float halfHeight, mass;
            switch (Random.Range(0, 5))
            {
                case 0:
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    prop.name = "Prop_Can";
                    prop.transform.localScale = new Vector3(0.08f, 0.055f, 0.08f);
                    halfHeight = 0.055f; mass = 0.3f;
                    ApplyMat(prop, Mat("CanRed", new Color(0.72f, 0.24f, 0.20f)));
                    break;
                case 1:
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    prop.name = "Prop_Can";
                    prop.transform.localScale = new Vector3(0.08f, 0.055f, 0.08f);
                    halfHeight = 0.055f; mass = 0.3f;
                    ApplyMat(prop, Mat("CanBlue", new Color(0.24f, 0.40f, 0.66f)));
                    break;
                case 2:
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.name = "Prop_CerealBox";
                    prop.transform.localScale = new Vector3(0.16f, 0.26f, 0.09f);
                    halfHeight = 0.13f; mass = 0.25f;
                    ApplyMat(prop, Mat("Cereal", new Color(0.82f, 0.64f, 0.24f)));
                    break;
                case 3:
                    prop = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    prop.name = "Prop_Bottle";
                    prop.transform.localScale = new Vector3(0.07f, 0.14f, 0.07f);
                    halfHeight = 0.14f; mass = 0.4f;
                    ApplyMat(prop, Mat("Bottle", new Color(0.28f, 0.50f, 0.30f)));
                    break;
                default:
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.name = "Prop_MilkCarton";
                    prop.transform.localScale = new Vector3(0.10f, 0.24f, 0.10f);
                    halfHeight = 0.12f; mass = 0.3f;
                    ApplyMat(prop, Mat("Milk", new Color(0.85f, 0.84f, 0.78f)));
                    break;
            }
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localBase + Vector3.up * (halfHeight + 0.01f);
            prop.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var rb = prop.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        static void ApplyMat(GameObject go, Material mat) => go.GetComponent<Renderer>().sharedMaterial = mat;

    }
}
