using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RaccoonHeist.Player;

namespace RaccoonHeist.World.Editor
{
    // Builds the entire greybox shop from ShopConstants. Re-running it wipes and
    // rebuilds, so the scene file never holds hand-placed layout.
    public static class ShopGreyboxGenerator
    {
        const float W = ShopConstants.ShopWidth;      // x, west wall at x = 0
        const float D = ShopConstants.ShopDepth;      // z, front wall at z = 0
        const float H = ShopConstants.CeilingHeight;
        const float T = ShopConstants.WallThickness;

        // Storage room interior sits behind the rear wall on the east side
        const float StorageX0 = 5f;
        const float StorageX1 = StorageX0 + ShopConstants.StorageWidth;
        const float StorageZ1 = 0f + D + T + ShopConstants.StorageDepth;

        static Transform root;
        static readonly Dictionary<string, Material> matCache = new();

        [MenuItem("Raccoon Heist/Generate Shop Greybox")]
        public static void Generate()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Raccoon Heist: exit Play mode first — anything generated during Play is discarded when you stop.");
                return;
            }

            matCache.Clear();
            DeleteIfExists("ShopGreybox");
            DeleteIfExists("Raccoon");
            DisableTemplateObjects();

            root = new GameObject("ShopGreybox").transform;
            Random.InitState(1287); // fixed seed: same prop scatter every rebuild

            BuildGroundAndCeilings();
            BuildFrontWall();
            BuildSideAndRearWalls();
            BuildBackRoom();
            BuildStorageRoom();
            BuildFixtures();
            BuildParkour();
            BuildOutside();
            BuildBackdrop();
            BuildLighting();
            BuildRaccoon();

            foreach (Transform child in root)
                if (!child.name.StartsWith("Harold") && child.GetComponent<Rigidbody>() == null)
                    child.gameObject.isStatic = true;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Raccoon Heist: shop greybox generated. Press Play and sneak around.");
        }

        // ---------- materials (created once as assets, palette lives here) ----------

        static Material Mat(string name, Color color, float smoothness = 0.08f)
        {
            if (matCache.TryGetValue(name, out var cached)) return cached;
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox")) AssetDatabase.CreateFolder("Assets/Materials", "Greybox");
            string path = $"Assets/Materials/Greybox/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color; // palette tweaks here propagate to the saved asset on rebuild
            mat.SetFloat("_Smoothness", smoothness);
            matCache[name] = mat;
            return mat;
        }

        static Material Wall => Mat("Wall", new Color(0.42f, 0.40f, 0.36f));
        static Material Wood => Mat("ShelfWood", new Color(0.42f, 0.31f, 0.21f));
        static Material Crate => Mat("Crate", new Color(0.55f, 0.42f, 0.28f));

        // ---------- procedural textures (generated once as PNG assets) ----------

        static Material TiledMat(string name, Color tint, Texture2D tex, float tileX, float tileY)
        {
            string key = $"{name}_{Mathf.RoundToInt(tileX)}x{Mathf.RoundToInt(tileY)}";
            if (matCache.TryGetValue(key, out var cached)) return cached;
            var mat = Mat(key, tint);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(tileX, tileY));
            return mat;
        }

        static Texture2D EnsureTex(string name, System.Func<int, int, Color> pixel, int size = 256, int height = 0)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox")) AssetDatabase.CreateFolder("Assets/Materials", "Greybox");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox/Textures")) AssetDatabase.CreateFolder("Assets/Materials/Greybox", "Textures");
            string path = $"Assets/Materials/Greybox/Textures/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            if (height == 0) height = size;
            var tex = new Texture2D(size, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Hash(int x, int y) => Mathf.Abs(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f) % 1f;

        static Texture2D TexAsphalt => EnsureTex("tex_asphalt", (x, y) =>
        {
            float n = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
            if (Hash(x, y) > 0.985f) n += 0.18f; // speckle
            return new Color(n, n, n);
        });

        static Texture2D TexSlabs => EnsureTex("tex_slabs", (x, y) =>
        {
            // 2 paving slabs per tile with grout lines
            float n = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            if (x % 128 < 3 || y % 128 < 3) n *= 0.62f;
            return new Color(n, n, n);
        });

        static Texture2D TexRough => EnsureTex("tex_rough", (x, y) =>
        {
            float n = 0.72f + 0.18f * Mathf.PerlinNoise(x * 0.03f, y * 0.03f)
                             + 0.10f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
            return new Color(n, n, n);
        });

        static Texture2D TexRoad => EnsureTex("tex_road", (x, y) =>
        {
            // Asphalt with a dashed centre line and solid edge lines (v spans road width)
            float n = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
            if (Hash(x, y) > 0.985f) n += 0.15f;
            bool dash = y >= 124 && y < 132 && x < 128;
            bool edge = y < 6 || y > 249;
            if (dash || edge) return new Color(Mathf.Min(1f, 1.7f * n), Mathf.Min(1f, 1.7f * n), Mathf.Min(1f, 1.6f * n));
            return new Color(n, n, n);
        });

        static Texture2D TexBrick => EnsureTex("tex_brick", (x, y) =>
        {
            // Running-bond bricks: 8 rows per tile, alternate rows offset half a brick
            int row = y / 32;
            int xo = (row % 2 == 0) ? 0 : 32;
            bool mortar = (y % 32) < 3 || ((x + xo) % 64) < 3;
            if (mortar) return new Color(0.78f, 0.76f, 0.73f);
            float v = 0.72f + 0.28f * Hash((x + xo) / 64 * 7 + 1, row * 13 + 1);
            float n = 0.92f + 0.08f * Hash(x, y);
            return new Color(v * n, v * n * 0.96f, v * n * 0.92f);
        });

        static Texture2D TexPlanks => EnsureTex("tex_planks", (x, y) =>
        {
            // 8 floor planks per tile with grain
            float n = 0.8f + 0.2f * Hash(x / 32 * 31 + 7, 3);
            n *= 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.02f, y * 0.3f);
            if (x % 32 < 2) n *= 0.55f;
            if (Hash(0, y / 96 + x / 32 * 17) > 0.8f && y % 96 < 2) n *= 0.6f; // plank ends
            return new Color(n, n * 0.94f, n * 0.86f);
        });

        static Material BrickMat(float lengthMeters) =>
            TiledMat("Brick", new Color(0.62f, 0.45f, 0.37f), TexBrick, Mathf.Max(1f, lengthMeters / 2f), 2f);

        // ---------- structure ----------

        // Outdoor compound bounds (perimeter walls sit just outside these)
        const float OutW0 = -5f, OutW1 = W + 5f;   // x range
        const float OutD0 = -8f, OutD1 = 25f;       // z range: street | shop block | alley

        static void BuildGroundAndCeilings()
        {
            BoxMinMax("Ground", new Vector3(OutW0 - 0.3f, -0.1f, OutD0 - 0.3f), new Vector3(OutW1 + 0.3f, 0f, OutD1 + 0.3f), null,
                TiledMat("Rough", new Color(0.40f, 0.40f, 0.42f), TexRough, 18f, 22f));
            // Zone overlays: read where you are by the ground underfoot. The street
            // strips run far past the invisible walls so the road reads as endless.
            // Road has painted markings; pavements are raised 0.12 m with curb faces
            // (hop-able: raccoon step offset handles it).
            bool cityRoads = SyntyPrefab("SM_Env_Road_Lines_01") != null;
            BoxMinMax("Road", new Vector3(-40f, 0f, -7f), new Vector3(W + 40f, 0.02f, -2f), null,
                cityRoads ? TiledMat("Asphalt", new Color(0.42f, 0.42f, 0.47f), TexAsphalt, 42f, 2f)
                          : TiledMat("RoadMarked", new Color(0.5f, 0.5f, 0.55f), TexRoad, 14f, 1f));
            if (cityRoads)
                for (float x0 = -40f; x0 < W + 40f; x0 += 5f)
                {
                    bool crossing = Mathf.Approximately(x0, -10f) || Mathf.Approximately(x0, 15f);
                    PlaceSynty(crossing ? "SM_Env_Road_Crossing_01" : "SM_Env_Road_Lines_01",
                        new Vector3(x0 + 2.5f, 0.028f, -4.5f), 90f);
                }
            BoxMinMax("Pavement_Far", new Vector3(-40f, 0f, OutD0), new Vector3(W + 40f, 0.12f, -7f), null,
                TiledMat("Slabs", new Color(0.55f, 0.55f, 0.56f), TexSlabs, 40f, 1f));
            BoxMinMax("Pavement_Shop", new Vector3(-40f, 0f, -2f), new Vector3(W + 40f, 0.12f, 0f), null,
                TiledMat("Slabs", new Color(0.55f, 0.55f, 0.56f), TexSlabs, 40f, 1f));
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

        static void BuildFrontWall()
        {
            // Front wall (z = 0 plane): door gap at x 1.5–2.5, big window from x 3.5 almost to the corner
            BoxMinMax("FrontWall_Left", new Vector3(-T, 0f, -T), new Vector3(1.5f, H, 0f), null, Wall);
            BoxMinMax("FrontWall_DoorHeader", new Vector3(1.5f, ShopConstants.DoorwayHeight, -T), new Vector3(2.5f, H, 0f), null, Wall);
            BoxMinMax("FrontWall_Pillar", new Vector3(2.5f, 0f, -T), new Vector3(3.5f, H, 0f), null, Wall);
            BoxMinMax("FrontWall_WindowSill", new Vector3(3.5f, 0f, -T), new Vector3(W - 0.5f, 0.9f, 0f), null, Wall);
            BoxMinMax("FrontWall_WindowHeader", new Vector3(3.5f, 2.5f, -T), new Vector3(W - 0.5f, H, 0f), null, Wall);
            BoxMinMax("FrontWall_Right", new Vector3(W - 0.5f, 0f, -T), new Vector3(W + T, H, 0f), null, Wall);

            // Closed front door with the pet flap raccoons get let back in through
            BoxMinMax("FrontDoor", new Vector3(1.55f, 0f, -0.15f), new Vector3(2.45f, ShopConstants.DoorwayHeight, -0.05f), null,
                Mat("Door", new Color(0.33f, 0.22f, 0.13f)));
            BoxMinMax("PetFlap", new Vector3(1.8f, 0.02f, -0.18f), new Vector3(2.2f, 0.35f, -0.02f), null,
                Mat("PetFlap", new Color(0.62f, 0.56f, 0.45f)));
        }

        static void BuildSideAndRearWalls()
        {
            // West wall runs the full length of shop + back room
            BoxMinMax("WestWall", new Vector3(-T, 0f, -T), new Vector3(0f, H, D + T + 4f + T), null, Wall);

            // East wall with the vent hole at floor level near the front (entry/extraction)
            BoxMinMax("EastWall_Front", new Vector3(W, 0f, -T), new Vector3(W + T, H, 1f), null, Wall);
            BoxMinMax("EastWall_AboveVent", new Vector3(W, 0.5f, 1f), new Vector3(W + T, H, 1.5f), null, Wall);
            BoxMinMax("EastWall_Rear", new Vector3(W, 0f, 1.5f), new Vector3(W + T, H, D + T), null, Wall);
            BoxMinMax("VentExitPad", new Vector3(W + T, -0.02f, 0.8f), new Vector3(W + 1.4f, 0.02f, 1.7f), null,
                Mat("VentMetal", new Color(0.55f, 0.58f, 0.62f), 0.4f));
            PlaceSynty("SM_Prop_AirDuct_01", new Vector3(W + 0.9f, 0f, 1.25f), 90f); // dressing on the vent exit

            // Rear wall of the main shop: Harold's doorway (west) + open storage doorway (east)
            BoxMinMax("RearWall_West", new Vector3(0f, 0f, D), new Vector3(1.05f, H, D + T), null, Wall);
            BoxMinMax("RearWall_HaroldDoorHeader", new Vector3(1.05f, ShopConstants.DoorwayHeight, D), new Vector3(1.95f, H, D + T), null, Wall);
            BoxMinMax("RearWall_Mid", new Vector3(1.95f, 0f, D), new Vector3(7.3f, H, D + T), null, Wall);
            BoxMinMax("RearWall_StorageDoorHeader", new Vector3(7.3f, ShopConstants.DoorwayHeight, D), new Vector3(8.5f, H, D + T), null, Wall);
            BoxMinMax("RearWall_East", new Vector3(8.5f, 0f, D), new Vector3(W + T, H, D + T), null, Wall);
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

            // North wall has the creaky back-window opening (x 8.2-9.0, y 1.5-2.3):
            // reachable from the alley dumpster. Always open in greybox; lock mechanic later.
            BoxMinMax("Storage_NorthWall_W", new Vector3(StorageX0, 0f, StorageZ1), new Vector3(8.2f, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_E", new Vector3(9f, 0f, StorageZ1), new Vector3(StorageX1, H, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_Sill", new Vector3(8.2f, 0f, StorageZ1), new Vector3(9f, 1.5f, StorageZ1 + T), null, Wall);
            BoxMinMax("Storage_NorthWall_Header", new Vector3(8.2f, 2.3f, StorageZ1), new Vector3(9f, H, StorageZ1 + T), null, Wall);
            BoxMinMax("BackWindowSill", new Vector3(8.15f, 1.45f, StorageZ1 - 0.15f), new Vector3(9.05f, 1.5f, StorageZ1 + T + 0.15f), null, Wood);

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

        // ---------- Synty POLYGON Shops set dressing (optional — greybox fallback) ----------

        static readonly Dictionary<string, GameObject> syntyCache = new();

        static bool SyntyAvailable => SyntyPrefab("SM_Prop_Market_Aisle_Preset_01") != null;

        static GameObject SyntyPrefab(string name)
        {
            if (syntyCache.TryGetValue(name, out var cached)) return cached;
            // Both packs ship prefabs with identical names (roads, trees) — prefer City
            string bestPath = null;
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
                if (bestPath == null || path.Contains("/PolygonCity/")) bestPath = path;
                if (path.Contains("/PolygonCity/")) break;
            }
            var prefab = bestPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
            syntyCache[name] = prefab;
            return prefab;
        }

        // Instantiates a Synty prefab grounded at floorPos (feet on floor, footprint
        // centered) regardless of where its pivot is. Adds a fitted box collider if
        // the prefab ships without one.
        static GameObject PlaceSynty(string name, Vector3 floorPos, float yaw = 0f)
        {
            var prefab = SyntyPrefab(name);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = floorPos;
            var b = GeometryBounds(go);
            go.transform.position += new Vector3(floorPos.x - b.center.x, floorPos.y - b.min.y, floorPos.z - b.center.z);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                b = GeometryBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                var localSize = go.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            }
            return go;
        }

        // Full pre-stocked market aisles, sized on the fly so any Synty footprint works
        static void BuildSyntyAisles()
        {
            string[] presets = { "SM_Prop_Market_Aisle_Preset_01", "SM_Prop_Market_Aisle_Preset_02",
                                 "SM_Prop_Market_Aisle_Preset_03", "SM_Prop_Market_Aisle_Preset_04" };
            var probe = PlaceSynty(presets[0], new Vector3(0f, -50f, 0f));
            var size = GeometryBounds(probe).size;
            Object.DestroyImmediate(probe);
            float len = Mathf.Max(size.x, size.z);
            float depth = Mathf.Min(size.x, size.z);
            float yaw = size.x >= size.z ? 0f : 90f; // long axis along x
            int i = 0;
            for (float z = 5f; z + depth / 2f <= D - 4.5f; z += depth + ShopConstants.AisleWidth)
                for (float x = 2.8f; x + len <= W - 2f; x += len + 0.6f) // 0.6 m squeeze-gaps
                    PlaceSynty(presets[i++ % presets.Length], new Vector3(x + len / 2f, 0f, z), yaw);
        }

        static void BuildSyntyFridgeRun()
        {
            var probe = PlaceSynty("SM_Prop_Market_Wall_Fridge_01", new Vector3(0f, -50f, 0f));
            if (probe == null) return;
            var size = GeometryBounds(probe).size;
            Object.DestroyImmediate(probe);
            float len = Mathf.Max(size.x, size.z);
            float yaw = size.x >= size.z ? 90f : 0f; // long axis along z, off the west wall
            for (float z = 3.2f; z + len <= D - 3f; z += len + 0.05f)
                PlaceSynty("SM_Prop_Market_Wall_Fridge_01", new Vector3(0.9f, 0f, z + len / 2f), yaw);
        }

        static bool TryCreateSyntyProduct(Transform parent, Vector3 localBase)
        {
            var prefab = SyntyPrefab($"SM_Prop_Product_{Random.Range(1, 21):00}") ?? SyntyPrefab("SM_Prop_Product_01");
            if (prefab == null) return false;

            // Instantiate at origin first so bounds/collider math is clean, then parent
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var b = GeometryBounds(go);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            float lift = -b.min.y;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localBase + Vector3.up * (lift + 0.01f);
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return true;
        }

        // The vertical climbing layer. Every surface is reachable in steps of <= 1 m
        // (the raccoon's jump), forming floor-free routes around the whole shop:
        // crates -> ledges -> ceiling beams -> fridge top -> shelf tops.
        static void BuildParkour()
        {
            var beamMat = Mat("Beam", new Color(0.30f, 0.24f, 0.19f));

            // Interior window sill — walkable ledge with a view of the street
            BoxMinMax("WindowSill", new Vector3(3.5f, 0.85f, 0f), new Vector3(W - 0.5f, 0.9f, 0.22f), null, Wood);

            // High ledges along the east and rear walls
            BoxMinMax("EastLedge", new Vector3(W - 0.25f, 1.9f, 3f), new Vector3(W, 1.95f, D - 3f), null, Wood);
            BoxMinMax("RearLedge", new Vector3(3f, 2.15f, D - 0.22f), new Vector3(W - 3f, 2.2f, D), null, Wood);

            // Ceiling beams — the raccoon highway. Harold (1.8 m) walks under them.
            for (float z = 4f; z < D - 3f; z += 3.6f)
                BoxMinMax($"Beam_{z}", new Vector3(0f, 2.25f, z - 0.12f), new Vector3(W, 2.4f, z + 0.12f), null, beamMat);

            // Crate steps by the front door (hide clutter + hop to the counter zone)
            BoxMinMax("Crates_Door_A", new Vector3(0.6f, 0f, 0.5f), new Vector3(1.2f, 0.6f, 1.1f), null, Crate);
            BoxMinMax("Crates_Door_B", new Vector3(0.6f, 0f, 1.15f), new Vector3(1.2f, 1.2f, 1.75f), null, Crate);

            // Crate steps up to the east ledge
            BoxMinMax("Crates_Vent_A", new Vector3(W - 0.65f, 0f, D - 4.2f), new Vector3(W - 0.05f, 0.6f, D - 3.6f), null, Crate);
            BoxMinMax("Crates_Vent_B", new Vector3(W - 0.65f, 0f, D - 3.55f), new Vector3(W - 0.05f, 1.2f, D - 2.95f), null, Crate);

            // Crate steps up to the fridge-top high route
            BoxMinMax("Crates_Fridge_A", new Vector3(1.4f, 0f, 2.3f), new Vector3(2f, 0.6f, 2.9f), null, Crate);
            BoxMinMax("Crates_Fridge_B", new Vector3(2.05f, 0f, 2.3f), new Vector3(2.65f, 1.2f, 2.9f), null, Crate);
        }

        // ---------- outside: street, side passage, back alley, den, roof route ----------

        static void BuildOutside()
        {
            // Invisible boundaries — the city continues visually, the raccoons don't.
            // 4 m tall so nobody hops out from the roof (3.1 m).
            InvisibleWall("Bound_Front", new Vector3(OutW0 - 0.3f, 0f, OutD0 - 0.3f), new Vector3(OutW1 + 0.3f, 4f, OutD0));
            InvisibleWall("Bound_West", new Vector3(OutW0 - 0.3f, 0f, OutD0), new Vector3(OutW0, 4f, OutD1 + 0.3f));
            InvisibleWall("Bound_East", new Vector3(OutW1, 0f, OutD0), new Vector3(OutW1 + 0.3f, 4f, OutD1 + 0.3f));
            InvisibleWall("Bound_Back_Upper", new Vector3(OutW0, 0.6f, OutD1), new Vector3(OutW1, 4f, OutD1 + 0.3f));
            InvisibleWall("Bound_Back_W", new Vector3(OutW0, 0f, OutD1), new Vector3(6.5f, 0.6f, OutD1 + 0.3f));
            InvisibleWall("Bound_Back_E", new Vector3(7.3f, 0f, OutD1), new Vector3(OutW1, 0.6f, OutD1 + 0.3f));

            // See-through metal fence marks the alley boundary; the den is dug under it
            var fenceProbe = PlaceSynty("SM_Bld_Metal_Fence_01", new Vector3(0f, -50f, 0f));
            if (fenceProbe != null)
            {
                float flen = Mathf.Max(GeometryBounds(fenceProbe).size.x, GeometryBounds(fenceProbe).size.z);
                Object.DestroyImmediate(fenceProbe);
                for (float x = OutW0; x < OutW1; x += flen)
                {
                    if (x < 7.3f && x + flen > 6.5f) continue; // dug-under gap at the den
                    PlaceSynty("SM_Bld_Metal_Fence_01", new Vector3(x + flen / 2f, 0f, OutD1 + 0.15f));
                }
            }
            else
            {
                BoxMinMax("BackFence_W", new Vector3(OutW0, 0f, OutD1), new Vector3(6.5f, 1.8f, OutD1 + 0.1f), null, BrickMat(11.5f));
                BoxMinMax("BackFence_E", new Vector3(7.3f, 0f, OutD1), new Vector3(OutW1, 1.8f, OutD1 + 0.1f), null, BrickMat(11.5f));
            }
            BoxMinMax("DenPad", new Vector3(6.4f, 0f, OutD1 - 0.8f), new Vector3(7.4f, 0.025f, OutD1), null,
                Mat("DenDirt", new Color(0.24f, 0.18f, 0.13f)));

            // Neighbour building fills the west side — makes the outdoors a C-shape:
            // street -> east passage -> back alley
            BoxMinMax("NeighbourBuilding", new Vector3(OutW0, 0f, 0f), new Vector3(-0.2f, 4f, D + T + 4f), null, BrickMat(20.8f));

            // Street dressing: awnings over the shopfront, a parked food trailer
            // (cover + climbable), ground decals so the flat zones read as real street
            PlaceSynty("SM_Bld_Awning_Large_01", new Vector3(2f, 2.1f, -0.5f));
            PlaceSynty("SM_Bld_Awning_Large_02", new Vector3(6.5f, 2.1f, -0.5f));
            PlaceSynty("SM_Bld_Awning_Large_01", new Vector3(11f, 2.1f, -0.5f));
            PlaceSynty("SM_Bld_Awning_03", new Vector3(-2.6f, 2.1f, -0.45f));
            PlaceSynty("SM_Veh_Food_Trailer_01", new Vector3(9f, 0f, -4.2f), 90f);
            PlaceSynty("SM_Env_Ground_Manhole_01", new Vector3(5.5f, 0.03f, -3.2f));
            PlaceSynty("SM_Env_Ground_ParkingLines_01", new Vector3(-2.5f, 0.03f, -4.2f));
            PlaceSynty("SM_Env_Ground_Panel_01", new Vector3(7.2f, 0.03f, 22.5f));
            PlaceSynty("SM_Prop_Rubbish_Bin_02", new Vector3(5.4f, 0f, 24.1f), 25f);
            PlaceSynty("SM_Prop_Rubbish_Bin_03", new Vector3(12.5f, 0f, 22f), 190f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(10.9f, 0f, 24.3f), 120f);

            // Bins across the street — the yeet landing zone (on the raised pavement)
            for (int i = 0; i < 3; i++)
            {
                if (PlaceSynty($"SM_Prop_Rubbish_Bin_0{i + 1}", new Vector3(2.6f + i * 1.1f, 0.12f, -7.3f), 180f) == null)
                    BoxMinMax($"Bin_{i}", new Vector3(2.3f + i * 1.1f, 0.12f, -7.6f), new Vector3(3.1f + i * 1.1f, 1f, -6.9f), null,
                        Mat("BinGreen", new Color(0.24f, 0.34f, 0.24f)));
            }
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(5.6f, 0.12f, -7.2f), 40f);
            PlaceSynty("SM_Prop_Bollard_Light_01", new Vector3(4f, 0.12f, -1.2f));
            PlaceSynty("SM_Prop_Bollard_Light_01", new Vector3(10f, 0.12f, -1.2f));

            // Side passage clutter (east, where the vent is)
            PlaceSynty("SM_Prop_Warehouse_Pallet_01", new Vector3(W + 3.5f, 0f, 6f), 15f);
            PlaceSynty("SM_Prop_Warehouse_Pallet_Stacked_01", new Vector3(W + 3.8f, 0f, 12f), 75f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(W + 1f, 0f, 17f), 200f);

            // Alley roof route: crate (0.6) -> dumpster (1.3) -> AC unit (2.2) -> storage roof (3.1)
            CrateStack("AlleyCrate", 11.2f, StorageZ1 + 0.4f, 0.6f);
            BoxMinMax("Dumpster", new Vector3(9.2f, 0f, StorageZ1 + 0.3f), new Vector3(10.8f, 1.3f, StorageZ1 + 1.3f), null,
                Mat("DumpsterGreen", new Color(0.22f, 0.32f, 0.26f), 0.3f));
            BoxMinMax("AcUnit", new Vector3(7.9f, 1.6f, StorageZ1 + T), new Vector3(8.9f, 2.2f, StorageZ1 + T + 0.7f), null,
                Mat("AcMetal", new Color(0.55f, 0.56f, 0.58f), 0.35f));

            // More alley cover
            CrateStack("AlleyCrates_2", 2.5f, 22.5f, 1.2f);
            PlaceSynty("SM_Prop_Warehouse_Pallet_01", new Vector3(4.5f, 0f, 23.5f), 100f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(1.2f, 0f, 24.2f), 310f);

            // POLYGON City extras — silent no-ops until that pack is imported
            PlaceSynty("SM_Veh_Car_Sedan_01", new Vector3(3.2f, 0f, -4.3f), 92f);
            PlaceSynty("SM_Veh_Car_Taxi_01", new Vector3(-2.2f, 0f, -4.1f), 268f);
            PlaceSynty("SM_Bld_FireEscape_01", new Vector3(-2.6f, 0.12f, 0.35f), 180f);
            RoofBillboard(new Vector3(-2.6f, 4f, 12f), 90f, 3); // neighbour's roof, facing the passage
        }

        // ---------- backdrop: the city beyond the walls ----------

        static void BuildBackdrop()
        {
            var backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root, false);

            // Dark floor far past the walls so gaps between buildings never show void
            BoxMinMax("VoidFloor", new Vector3(-70f, -0.25f, -70f), new Vector3(W + 70f, -0.15f, D + 70f), backdrop,
                Mat("Void", new Color(0.04f, 0.04f, 0.06f)));

            // Street level: shopfronts shoulder-to-shoulder with the train station as
            // the landmark directly across from our shop; block continues east/west;
            // apartments crowd the alley fence
            BackdropRow(backdrop, ShopFronts, true, OutD0 - 3.5f, OutW0 - 30f, 1.5f, 0f, 6f, 10f, 0.1f, 0.5f, true);
            var station = PlaceSynty("SM_Bld_Station_01", new Vector3(7f, 0f, -13.3f));
            if (station != null) { MakeEmissive(station); DisableShadows(station); station.transform.SetParent(backdrop, true); }
            BackdropRow(backdrop, ShopFronts, true, OutD0 - 3.5f, 12.5f, OutW1 + 30f, 0f, 6f, 10f, 0.1f, 0.5f, true);
            BackdropRow(backdrop, ShopFronts, true, 4f, OutW1 + 0.5f, OutW1 + 30f, 180f, 6f, 10f, 0.1f, 0.5f, true);
            BackdropRow(backdrop, ShopFronts, true, 4f, OutW0 - 30f, OutW0 - 0.5f, 180f, 6f, 10f, 0.1f, 0.5f, true);
            BackdropRow(backdrop, Apartments, true, OutD1 + 6f, OutW0 - 20f, OutW1 + 20f, 180f, 8f, 14f, 0.2f, 1f, false, true);

            // Flanks: the block continues along both sides so no viewpoint faces bare
            // ground — east past the passage, west along the alley end
            BackdropRow(backdrop, Apartments, false, OutW1 + 3f, -2f, OutD1 + 18f, 270f, 8f, 14f, 0.2f, 1f, false, true);
            BackdropRow(backdrop, Apartments, false, OutW0 - 3f, D + 4f, OutD1 + 18f, 90f, 8f, 14f, 0.2f, 1f, false, true);

            // Far skyline: procedural towers pushed deep into the fog — silhouettes only
            BackdropRow(backdrop, null, true, OutD0 - 42f, OutW0 - 40f, OutW1 + 40f, 0f, 16f, 30f, 2f, 6f);
            BackdropRow(backdrop, null, true, OutD1 + 38f, OutW0 - 40f, OutW1 + 40f, 180f, 16f, 30f, 2f, 6f);
            BackdropRow(backdrop, null, false, OutW1 + 34f, OutD0 - 20f, OutD1 + 20f, 270f, 14f, 26f, 2f, 6f);
            BackdropRow(backdrop, null, false, OutW0 - 34f, OutD0 - 20f, OutD1 + 20f, 90f, 14f, 26f, 2f, 6f);

            StreetDressing();
        }

        static readonly string[] ShopFronts = { "SM_Bld_Shop_01", "SM_Bld_Shop_02", "SM_Bld_Shop_03",
                                                "SM_Bld_Shop_04", "SM_Bld_Shop_05", "SM_Bld_Shop_06" };
        static readonly string[] Apartments = { "SM_Bld_Apartment_Stack_01", "SM_Bld_Apartment_Stack_02" };

        static void StreetDressing()
        {
            // Lamp posts along the shop-side pavement: glowing head + a real light
            // under the arm, every pole (URP Forward+ handles the count)
            for (float x = -34f; x <= W + 34f; x += 12f)
            {
                var pole = PlaceSynty("SM_Prop_LightPole_Base_01", new Vector3(x, 0.12f, -1.3f));
                if (pole == null) continue;
                MakeEmissive(pole);
                var pb = GeometryBounds(pole);
                PointLight($"LampGlow_{x}", new Vector3(x, pb.max.y - 0.6f, pb.max.z - 0.4f),
                    new Color(1f, 0.72f, 0.4f), 1.5f, 9f);
            }

            // Parked cars down the road, outside the boundary
            PlaceSynty("SM_Veh_Car_Muscle_01", new Vector3(-16f, 0.02f, -4.4f), 88f);
            PlaceSynty("SM_Veh_Car_Van_01", new Vector3(-28f, 0.02f, -3.9f), 272f);
            PlaceSynty("SM_Veh_Car_Sedan_01", new Vector3(W + 16f, 0.02f, -4.2f), 90f);
            PlaceSynty("SM_Veh_Car_Taxi_01", new Vector3(W + 27f, 0.02f, -4f), 268f);

            PlaceSynty("SM_Prop_BusStop_01", new Vector3(-12f, 0.12f, -7.4f));

            // Painted crossings only if the Synty road tiles (with real crossings) are absent
            if (SyntyPrefab("SM_Env_Road_Lines_01") == null)
            {
                Crosswalk(-9f);
                Crosswalk(W + 9f);
            }

            // Street trees on the pavements, outside the play area
            string[] trees = { "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03" };
            int t = 0;
            foreach (float x in new[] { -28f, -16f, W + 16f, W + 28f })
                PlaceSynty(trees[t++ % 3], new Vector3(x, 0.12f, -7.4f), t * 77f);
            PlaceSynty(trees[0], new Vector3(-22f, 0.12f, -1f), 30f);
            PlaceSynty(trees[1], new Vector3(W + 22f, 0.12f, -1f), 130f);

            // Traffic lights at the two crossings: the head hangs from the pole's
            // measured arm tip (head pivot is at its top), not from thin air
            foreach (float x in new[] { -7.5f, 17.5f })
            {
                var pole = PlaceSynty("SM_Prop_LightPole_Base_01", new Vector3(x, 0.12f, -1.3f), 180f);
                if (pole == null) continue;
                MakeEmissive(pole);
                var pb = GeometryBounds(pole);
                var head = PlaceSynty("SM_Prop_TrafficLight_02", new Vector3(x, pb.max.y - 1.45f, pb.min.z + 0.55f), 180f);
                MakeEmissive(head);
            }

            // Rooftop clutter on OUR roof — cover for the roof route (skylight at x 6.5-7.5, z 10-11)
            PlaceSynty("SM_Prop_Vents_Straight_01", new Vector3(3.5f, H + 0.1f, 6f), 20f);
            PlaceSynty("SM_Prop_Vents_Corner_01", new Vector3(10.5f, H + 0.1f, 13f));
            PlaceSynty("SM_Prop_Vents_Exhaust_01", new Vector3(11.5f, H + 0.1f, 4f), 90f);
            if (PlaceSynty("SM_Prop_Aircon_01", new Vector3(4.5f, H + 0.1f, 14.5f), 45f) == null)
                PlaceSynty("SM_Prop_AirCon_01", new Vector3(4.5f, H + 0.1f, 14.5f), 45f);

            // Security cameras (props for now — maybe a mechanic later) + alley drainpipe
            PlaceSynty("SM_Prop_SecurityCamera_01", new Vector3(3f, 2.45f, -0.28f), 180f);
            PlaceSynty("SM_Prop_SecurityCamera_01", new Vector3(9.6f, 2.4f, StorageZ1 + T + 0.05f));
            PlaceSynty("SM_Prop_Pipe_Small_01", new Vector3(5.4f, 0f, StorageZ1 + T + 0.1f));
        }

        static void Crosswalk(float x)
        {
            var paint = Mat("RoadPaint", new Color(0.85f, 0.85f, 0.82f));
            for (int i = 0; i < 5; i++)
                BoxMinMax($"Crosswalk_{x}_{i}", new Vector3(x + i * 0.85f, 0.022f, -6.2f), new Vector3(x + i * 0.85f + 0.45f, 0.028f, -2.3f), null, paint);
        }

        static void InvisibleWall(string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = (min + max) / 2f;
            box.size = max - min;
        }

        static void BackdropRow(Transform parent, string[] prefabs, bool alongX, float fixedCoord, float from, float to, float yaw, float hMin, float hMax, float gapMin = 1.5f, float gapMax = 5f, bool dressShopfront = false, bool emissiveWindows = false)
        {
            bool usePrefabs = prefabs != null && SyntyPrefab(prefabs[0]) != null;
            float cursor = from;
            int i = 0;
            while (cursor < to)
            {
                float w = Random.Range(6f, 10f);
                Vector3 pos = alongX ? new Vector3(cursor + w / 2f, 0f, fixedCoord) : new Vector3(fixedCoord, 0f, cursor + w / 2f);
                GameObject b = usePrefabs ? PlaceSynty(prefabs[i % prefabs.Length], pos, yaw) : null;
                if (b != null)
                {
                    // slide so the building's near edge starts at the cursor, then advance by its real width
                    var bb = GeometryBounds(b);
                    float shift = cursor - (alongX ? bb.min.x : bb.min.z);
                    b.transform.position += (alongX ? Vector3.right : Vector3.forward) * shift;
                    w = alongX ? bb.size.x : bb.size.z;
                }
                else
                {
                    // procedural night building: dark block, window grid, some windows lit
                    float h = Random.Range(hMin, hMax), d = Random.Range(5f, 8f);
                    var mat = BuildingMat(i % 2, w / 3f, h / 3f);
                    b = alongX
                        ? BoxMinMax($"BackdropBld_{fixedCoord}_{i}", new Vector3(cursor, 0f, fixedCoord - d / 2f), new Vector3(cursor + w, h, fixedCoord + d / 2f), parent, mat)
                        : BoxMinMax($"BackdropBld_{fixedCoord}_{i}", new Vector3(fixedCoord - d / 2f, 0f, cursor), new Vector3(fixedCoord + d / 2f, h, cursor + w), parent, mat);
                }
                // Closed-for-the-night dressing: shutters/canopies on most shopfronts,
                // complete billboards (frame + ad face) on every third
                if (b != null && usePrefabs && dressShopfront)
                {
                    var fb = GeometryBounds(b);
                    if (i % 3 == 0)
                    {
                        RoofBillboard(new Vector3(fb.center.x, fb.max.y, fb.center.z), yaw, i / 3);
                    }
                    else
                    {
                        float faceZ = yaw < 90f ? fb.max.z + 0.15f : fb.min.z - 0.15f;
                        var cover = PlaceSynty("SM_Bld_Shop_Cover_03", new Vector3(fb.center.x, 2.2f, faceZ), yaw);
                        if (cover != null) { DisableShadows(cover); cover.transform.SetParent(parent, true); }
                    }
                }

                if (usePrefabs && emissiveWindows) MakeEmissive(b);
                DisableShadows(b); // backdrop must never shadow the play area's moonlight
                b.transform.SetParent(parent, true);
                cursor += w + Random.Range(gapMin, gapMax);
                i++;
            }
        }

        // Frame + ad face: Billboard_Roof_01 is only the scaffold, the sign mounts on it
        static void RoofBillboard(Vector3 roofPos, float yaw, int variant)
        {
            var holder = PlaceSynty("SM_Prop_Billboard_Roof_01", roofPos, yaw);
            if (holder == null) return;
            DisableShadows(holder);
            var hb = GeometryBounds(holder);
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float extent = Mathf.Abs(dir.x) > 0.5f ? hb.extents.x : hb.extents.z;
            var face = new Vector3(hb.center.x, 0f, hb.center.z) + dir * (extent - 0.25f);
            var sign = PlaceSynty($"SM_Prop_Billboard_Sign_0{1 + variant % 7}",
                new Vector3(face.x, hb.min.y + 0.85f, face.z), yaw);
            if (sign != null) { MakeEmissive(sign); DisableShadows(sign); }
        }

        // Swaps an instance's materials for emissive variants (created once as assets)
        // so ITS windows/lamps glow without lighting up every object sharing the atlas
        static void MakeEmissive(GameObject go)
        {
            if (go == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox/Emissive"))
                AssetDatabase.CreateFolder("Assets/Materials/Greybox", "Emissive");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && mats[m].HasProperty("_Emission_Map") && mats[m].GetTexture("_Emission_Map") != null)
                        mats[m] = EmissiveVariant(mats[m]);
                r.sharedMaterials = mats;
            }
        }

        static Material EmissiveVariant(Material src)
        {
            string key = $"Emissive_{src.name}";
            if (matCache.TryGetValue(key, out var cached)) return cached;
            string path = $"Assets/Materials/Greybox/Emissive/{src.name}_Emissive.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(src);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_Enable_Emission")) mat.SetFloat("_Enable_Emission", 1f);
            // warm and dimmed — window glow, not neon
            if (mat.HasProperty("_Emission_Color")) mat.SetColor("_Emission_Color", new Color(0.75f, 0.62f, 0.42f));
            matCache[key] = mat;
            return mat;
        }

        static void DisableShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Texture2D TexWindows(int variant) => EnsureTex($"tex_windows_{variant}", (x, y) =>
        {
            bool frame = (x % 64) < 14 || (x % 64) > 50 || (y % 64) < 16 || (y % 64) > 52;
            if (frame) return new Color(0.07f, 0.07f, 0.09f);
            bool lit = Hash(x / 64 * 7 + variant * 131, y / 64 * 13 + variant * 57) > 0.7f;
            return lit ? new Color(0.95f, 0.78f, 0.45f) : new Color(0.10f, 0.11f, 0.14f);
        });

        static Material BuildingMat(int variant, float tx, float ty)
        {
            string key = $"NightBld{variant}_{Mathf.RoundToInt(tx)}x{Mathf.RoundToInt(ty)}";
            if (matCache.TryGetValue(key, out var cached)) return cached;
            var mat = Mat(key, Color.white);
            var tex = TexWindows(variant);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(tx, ty));
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", new Color(1.4f, 1.2f, 0.9f)); // lit windows glow at night
            return mat;
        }

        // ---------- lighting ----------

        static void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.11f); // darker base; lamps and windows carry the scene

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(root, false);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(40f, 15f, 0f);
            moon.color = new Color(0.62f, 0.7f, 1f);
            moon.intensity = 0.6f;
            moon.shadows = LightShadows.Soft;

            // Real night sky: stars, painted moon, warm city-glow at the horizon
            string skyPath = "Assets/Materials/Greybox/NightSkyPano.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetTexture("_MainTex", TexNightSky);
            sky.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = sky;
            RenderSettings.sun = moon;

            // Night haze: thicker fog matched to the horizon glow
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.055f, 0.06f, 0.095f);
            RenderSettings.fogDensity = 0.016f;

            BuildAtmosphere();

            // Harold's lamp glow spilling out of the back room — his "location beacon"
            PointLight("BackRoomLamp", new Vector3(2.5f, 2.1f, D + T + 4f - 0.6f), new Color(1f, 0.62f, 0.32f), 1.6f, 6f);
            PointLight("FridgeGlow", new Vector3(1.8f, 1.9f, (D + 0f) / 2f), new Color(0.65f, 0.85f, 1f), 1.1f, 4f);
            PointLight("VentGlow", new Vector3(W - 0.25f, 0.45f, 1.25f), new Color(0.4f, 1f, 0.5f), 0.8f, 2f);
            // Bare bulb in the storage room — dim, warm, horror-pantry mood
            PointLight("StorageBulb", new Vector3((StorageX0 + StorageX1) / 2f, 2.5f, D + 2.5f), new Color(1f, 0.72f, 0.42f), 1.3f, 6f);
            // Outside: dim alley bulb over the dumpster, warm glow from the den hole
            PointLight("AlleyLight", new Vector3(9.5f, 3.2f, StorageZ1 + 1.5f), new Color(1f, 0.68f, 0.38f), 1.2f, 7f);
            PointLight("DenGlow", new Vector3(6.9f, 0.35f, OutD1 - 0.4f), new Color(1f, 0.62f, 0.3f), 1f, 3f);

            // Streetlamp outside, shining in through the front window
            var street = new GameObject("StreetLampSpot").AddComponent<Light>();
            street.transform.SetParent(root, false);
            street.type = LightType.Spot;
            street.transform.position = new Vector3(W / 2f + 1f, 2.6f, -2.6f);
            street.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.45f, 1f));
            street.spotAngle = 95f;
            street.range = 16f;
            street.color = new Color(1f, 0.62f, 0.28f);
            street.intensity = 5f;
            street.shadows = LightShadows.Soft;
        }

        static Light PointLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.transform.position = pos;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        // Procedural night sky: gradient with city light-pollution at the horizon,
        // stars that thin out low, drifting cloud noise, and a painted moon
        static Texture2D TexNightSky => EnsureTex("tex_nightsky", (x, y) =>
        {
            float v = y / 511f;
            float alt = Mathf.Clamp01((v - 0.5f) * 2f);
            var horizon = new Color(0.14f, 0.105f, 0.075f);
            var zenith = new Color(0.012f, 0.018f, 0.045f);
            Color c = v >= 0.5f
                ? Color.Lerp(horizon, zenith, Mathf.Sqrt(alt))
                : Color.Lerp(horizon, new Color(0.01f, 0.01f, 0.015f), (0.5f - v) * 3f);
            float cloud = Mathf.PerlinNoise(x * 0.008f, y * 0.02f);
            c *= 0.92f + 0.16f * cloud;
            if (alt > 0.05f && Hash(x, y) > 0.9988f - alt * 0.0012f)
            {
                float b = 0.35f + 0.65f * Hash(x + 7, y + 3);
                c += new Color(b, b, b * 0.95f);
            }
            float dx = x - 720f, dy = y - 400f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < 14f) c += new Color(0.95f, 0.96f, 1f);
            else if (dist < 60f) c += new Color(0.5f, 0.55f, 0.7f) * Mathf.Pow(1f - (dist - 14f) / 46f, 2.5f);
            return c;
        }, 1024, 512);

        // Post-processing: the difference between "3D scene" and "night movie still".
        // Bloom makes every emissive window/lamp halo; grain+vignette kill the flatness.
        static void BuildAtmosphere()
        {
            string path = "Assets/Materials/Greybox/NightPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c)) c = profile.Add<T>();
                return c;
            }
            var bloom = Get<Bloom>();
            bloom.intensity.Override(0.5f);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(0.7f);
            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>();
            color.postExposure.Override(0.2f);
            color.contrast.Override(15f);
            color.saturation.Override(-8f);
            var wb = Get<WhiteBalance>();
            wb.temperature.Override(-10f);
            var vig = Get<Vignette>();
            vig.intensity.Override(0.25f);
            vig.smoothness.Override(0.45f);
            var grain = Get<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.2f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Volume vol = null;
            foreach (var v in Object.FindObjectsByType<Volume>())
                if (v.isGlobal) { vol = v; break; }
            if (vol == null)
            {
                var go = new GameObject("NightVolume");
                go.transform.SetParent(root, false);
                vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
            }
            vol.sharedProfile = profile;
        }

        // ---------- characters ----------

        const string HaroldFbxPath = "Assets/Models/Harold/Meshy_AI_Harold_biped_Character_output.fbx";
        const string HaroldTexturePath = "Assets/Models/Harold/Meshy_AI_Harold_biped_texture_0.png";
        const string HaroldMaterialPath = "Assets/Models/Harold/Harold.mat";

        static void SpawnHarold(Vector3 floorPosition)
        {
            EnsureHaroldMaterial();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HaroldFbxPath);
            if (model == null)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Harold_Placeholder";
                capsule.transform.SetParent(root, false);
                capsule.transform.localScale = new Vector3(0.6f, ShopConstants.HaroldHeight / 2f, 0.6f);
                capsule.transform.localPosition = floorPosition + Vector3.up * (ShopConstants.HaroldHeight / 2f);
                Debug.LogWarning($"Harold model not found at {HaroldFbxPath} — using capsule placeholder.");
                return;
            }

            var harold = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            harold.name = "Harold";
            harold.transform.position = floorPosition;
            harold.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the doorway

            // Meshy exports at arbitrary scale — normalize to design height, feet on floor.
            // Uses mesh geometry, not renderer.bounds: skinned renderer bounds are
            // unreliable before any animation has run and made him float.
            var bounds = GeometryBounds(harold);
            if (bounds.size.y > 0.001f)
            {
                harold.transform.localScale *= ShopConstants.HaroldHeight / bounds.size.y;
                bounds = GeometryBounds(harold);
                harold.transform.position += Vector3.up * (floorPosition.y - bounds.min.y);
            }
        }

        // Meshy FBX materials lose their texture link on import — build a URP material
        // with the texture and remap the model's embedded materials onto it once.
        static void EnsureHaroldMaterial()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HaroldTexturePath);
            var importer = AssetImporter.GetAtPath(HaroldFbxPath) as ModelImporter;
            if (texture == null || importer == null) return;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(HaroldMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", texture);
                mat.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(mat, HaroldMaterialPath);
            }

            var existing = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(HaroldFbxPath))
            {
                if (sub is not Material srcMat) continue;
                var id = new AssetImporter.SourceAssetIdentifier(srcMat);
                if (existing.TryGetValue(id, out var mapped) && mapped == mat) continue;
                importer.AddRemap(id, mat);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        static Bounds GeometryBounds(GameObject go)
        {
            var bounds = new Bounds();
            bool has = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                EncapsulateMesh(mf.sharedMesh, mf.transform, ref bounds, ref has);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                EncapsulateMesh(smr.sharedMesh, smr.transform, ref bounds, ref has);
            return has ? bounds : new Bounds(go.transform.position, Vector3.zero);
        }

        static void EncapsulateMesh(Mesh mesh, Transform t, ref Bounds bounds, ref bool has)
        {
            if (mesh == null) return;
            var mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var world = t.TransformPoint(corner);
                if (!has) { bounds = new Bounds(world, Vector3.zero); has = true; }
                else bounds.Encapsulate(world);
            }
        }

        static void BuildRaccoon()
        {
            var raccoon = new GameObject("Raccoon");
            raccoon.transform.position = new Vector3(6.9f, 0.05f, OutD1 - 1.2f); // at the den in the alley
            raccoon.transform.rotation = Quaternion.Euler(0f, 180f, 0f);         // facing the shop

            var cc = raccoon.AddComponent<CharacterController>();
            cc.height = ShopConstants.RaccoonHeight;
            cc.radius = 0.2f;
            cc.center = new Vector3(0f, ShopConstants.RaccoonHeight / 2f, 0f);
            cc.stepOffset = 0.15f;
            cc.slopeLimit = 50f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(raccoon.transform, false);
            body.transform.localScale = new Vector3(0.35f, ShopConstants.RaccoonHeight / 2f, 0.35f);
            body.transform.localPosition = new Vector3(0f, ShopConstants.RaccoonHeight / 2f, 0f);
            ApplyMat(body, Mat("RaccoonFur", new Color(0.35f, 0.35f, 0.38f)));

            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(raccoon.transform, false);
            eyes.transform.localPosition = new Vector3(0f, ShopConstants.RaccoonEyeHeight, 0.08f);
            var cam = eyes.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            eyes.tag = "MainCamera";
            eyes.AddComponent<AudioListener>();

            raccoon.AddComponent<RaccoonController>();
        }

        // ---------- helpers ----------

        static void DisableTemplateObjects()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>())
                if (cam.transform.root.name != "Raccoon")
                    cam.transform.root.gameObject.SetActive(false);
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional && light.transform.root.name != "ShopGreybox")
                    light.gameObject.SetActive(false);
        }

        static void DeleteIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, Transform parent = null, Material mat = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            if (mat != null) ApplyMat(go, mat);
            return go;
        }

        static GameObject BoxMinMax(string name, Vector3 min, Vector3 max, Transform parent = null, Material mat = null)
            => Box(name, (min + max) * 0.5f, max - min, parent, mat);
    }
}
