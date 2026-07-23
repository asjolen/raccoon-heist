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
    // Builds the entire greybox shop from ShopConstants. Re-running it wipes and
    // rebuilds, so the scene file never holds hand-placed layout.
    public static class ShopGreyboxGenerator
    {
        const float W = ShopConstants.ShopWidth;      // x, west wall at x = 0
        const float D = ShopConstants.ShopDepth;      // z, front wall at z = 0
        const float H = ShopConstants.CeilingHeight;
        const float T = ShopConstants.WallThickness;
        const float EntranceX = W * 0.5f;
        const float EntranceWidth = 1.2f;

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
            syntyCache.Clear();
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
            BuildShopAlarm();
            BuildEnvironmentAudio();
            BuildRaccoon();
            ValidateVehicleBuildingClearance();
            ValidateBackdropPropBuildingClearance();
            ValidateNeighbourFrontageClearance();

            foreach (Transform child in root)
                if (!child.name.StartsWith("Harold") && child.GetComponent<Rigidbody>() == null
                    && child.GetComponent<HingedDoor>() == null
                    && child.GetComponent<ShopAlarmController>() == null)
                    child.gameObject.isStatic = true;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Raccoon Heist: shop greybox generated. Press Play and sneak around.");
        }

        // Command-line/editor automation entry point. The normal menu command leaves
        // saving in the designer's hands; this explicit method is used by validation
        // runs that need the generated world persisted to the project scene.
        public static void GenerateAndSave()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            Generate();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Raccoon Heist: generated and saved Assets/Scenes/SampleScene.unity.");
        }

        // Single entry point for unattended validation: build the saved scene and
        // render every route plus dedicated street-ring review angles.
        public static void GenerateSaveAndCapture()
        {
            GenerateAndSave();
            CaptureEnvironmentPreviews();
        }

        [MenuItem("Raccoon Heist/Capture Environment Previews")]
        public static void CaptureEnvironmentPreviews()
        {
            var sceneRoot = GameObject.Find("ShopGreybox");
            if (sceneRoot == null)
            {
                Debug.LogWarning("Raccoon Heist: generate the shop before capturing environment previews.");
                return;
            }

            var raccoon = GameObject.Find("Raccoon");
            bool raccoonWasActive = raccoon != null && raccoon.activeSelf;
            if (raccoonWasActive) raccoon.SetActive(false);
            try
            {
                // Editor cameras do not advance particle systems on their own. Seed
                // only the broad ground haze: fast-forwarding the focused vent steam
                // turns its short plume into an oversized column in static previews.
                foreach (var particles in Object.FindObjectsByType<ParticleSystem>())
                {
                    if (!particles.name.Contains("GroundHaze")) continue;
                    particles.Simulate(6f, true, true, true);
                    particles.Play(true);
                }
                CapturePreview("Street", new Vector3(7f, 0.72f, -10.85f), new Vector3(7f, 1.15f, 2.5f));
                CapturePreview("Storefront", new Vector3(10.8f, 0.82f, -6.2f), new Vector3(6.8f, 1.15f, -0.05f));
                CapturePreview("Entrance", new Vector3(EntranceX, 0.68f, -2.8f), new Vector3(EntranceX, 1.02f, -0.05f));
                CapturePreview("NeighbourFrontage", new Vector3(-3f, 3.15f, -4.2f), new Vector3(-3f, 1.35f, -0.05f));
                CapturePreview("MainStreet", new Vector3(-10f, 6.5f, -7f), new Vector3(18f, 0.7f, -7f));
                var alarm = sceneRoot.GetComponentInChildren<ShopAlarmController>(true);
                if (alarm != null)
                {
                    alarm.TriggerAlarm();
                    CapturePreview("AlarmInterior", new Vector3(1.2f, 2.05f, 1.3f), new Vector3(7.4f, 2.42f, 9.5f));
                    alarm.ResetAlarm();
                }
                CapturePreview("Passage", new Vector3(15.15f, 0.75f, 12.1f), new Vector3(15.65f, 1.05f, 5.2f));
                CapturePreview("Alley", new Vector3(11.7f, 0.82f, 25f), new Vector3(8.2f, 1.2f, 19.7f));
                CapturePreview("WestStreet", new Vector3(-1.2f, 8f, -18f), new Vector3(-10.5f, 0.8f, 9f));
                CapturePreview("EastStreet", new Vector3(15.2f, 7.5f, -3.5f), new Vector3(24.5f, 0.8f, 9f));
                CapturePreview("RearStreet", new Vector3(7f, 7.5f, 18f), new Vector3(7f, 0.8f, 30f));
                CapturePreview("WestOppositeWalk", new Vector3(-10.5f, 1.25f, 8f), new Vector3(-17.5f, 1.1f, 8f));
                CapturePreview("EastOppositeWalk", new Vector3(24.5f, 1.25f, 10.5f), new Vector3(31.5f, 1.1f, 10.5f));
                CapturePreview("RearOppositeWalk", new Vector3(7f, 1.25f, 30.5f), new Vector3(7f, 1.1f, 37.5f));
                CapturePreview("Block", new Vector3(32f, 27f, -22f), new Vector3(7f, 0.8f, 10f));
            }
            finally
            {
                if (raccoonWasActive) raccoon.SetActive(true);
            }
            Debug.Log("Raccoon Heist: captured environment previews to /tmp/RaccoonHeist_*.png.");
        }

        static void CapturePreview(string name, Vector3 position, Vector3 target)
        {
            var holder = new GameObject($"PreviewCamera_{name}");
            var camera = holder.AddComponent<Camera>();
            holder.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = name == "Block" ? 54f : 68f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 180f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var targetTexture = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = targetTexture;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = targetTexture;
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes($"/tmp/RaccoonHeist_{name}.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(targetTexture);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(holder);
        }

        static void ValidateVehicleBuildingClearance()
        {
            var vehicles = new List<GameObject>();
            var buildings = new List<GameObject>();
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == root) continue;
                bool directEnvironmentChild = candidate.parent == root || candidate.parent.name == "Backdrop";
                if (!directEnvironmentChild) continue;

                string name = candidate.name;
                if (name.StartsWith("SM_Veh_"))
                    vehicles.Add(candidate.gameObject);
                else if (name == "NeighbourBuilding" || name.StartsWith("BackdropBld_")
                         || name.StartsWith("SM_Bld_Shop_") || name.StartsWith("SM_Bld_Apartment_")
                         || name.StartsWith("SM_Bld_Station_"))
                    buildings.Add(candidate.gameObject);
            }

            int overlaps = 0;
            foreach (var vehicle in vehicles)
            {
                var vehicleBounds = GeometryBounds(vehicle);
                if (vehicleBounds.size == Vector3.zero) continue;
                foreach (var building in buildings)
                {
                    var buildingBounds = GeometryBounds(building);
                    if (buildingBounds.size == Vector3.zero || !vehicleBounds.Intersects(buildingBounds)) continue;
                    overlaps++;
                    Debug.LogWarning($"Environment overlap: vehicle '{vehicle.name}' intersects building '{building.name}'.");
                }
            }

            if (overlaps == 0)
                Debug.Log($"Raccoon Heist: vehicle/building clearance check passed ({vehicles.Count} vehicles, {buildings.Count} buildings).");
        }

        static void ValidateBackdropPropBuildingClearance()
        {
            var backdrop = root.Find("Backdrop");
            if (backdrop == null) return;
            var props = new List<GameObject>();
            var buildings = new List<GameObject>();
            foreach (Transform candidate in backdrop)
            {
                if (candidate.name.StartsWith("SM_Prop_") || candidate.name.Contains("FarLamp_"))
                    props.Add(candidate.gameObject);
                else if (candidate.name.StartsWith("SM_Bld_") || candidate.name.StartsWith("BackdropBld_"))
                    buildings.Add(candidate.gameObject);
            }

            int overlaps = 0;
            foreach (var prop in props)
            {
                var propBounds = GeometryBounds(prop);
                if (propBounds.size == Vector3.zero) continue;
                foreach (var building in buildings)
                {
                    var buildingBounds = GeometryBounds(building);
                    if (buildingBounds.size == Vector3.zero || !propBounds.Intersects(buildingBounds)) continue;
                    overlaps++;
                    Debug.LogWarning($"Environment overlap: prop '{prop.name}' at {prop.transform.position} intersects building '{building.name}'.");
                }
            }

            if (overlaps == 0)
                Debug.Log($"Raccoon Heist: backdrop prop/building clearance check passed ({props.Count} props, {buildings.Count} buildings).");
        }

        static void ValidateNeighbourFrontageClearance()
        {
            var shutterObject = root.Find("NeighbourClosedShutter")?.gameObject;
            if (shutterObject == null) return;

            int problems = 0;
            foreach (Transform candidate in root)
            {
                if (!candidate.name.StartsWith("NeighbourShutter") && !candidate.name.StartsWith("NeighbourShopSign")) continue;
                var bounds = GeometryBounds(candidate.gameObject);
                if (bounds.min.x >= OutW0 && bounds.max.x <= -0.2f) continue;
                problems++;
                Debug.LogWarning($"Neighbour frontage overflow: '{candidate.name}' spans x {bounds.min.x:0.00}..{bounds.max.x:0.00} outside wall {OutW0:0.00}..-0.20.");
            }

            var shutterBounds = GeometryBounds(shutterObject);
            var clearApron = new Bounds(
                new Vector3(shutterBounds.center.x, 1.35f, -0.48f),
                new Vector3(shutterBounds.size.x, 2.7f, 0.90f));
            foreach (Transform candidate in root)
            {
                if (!candidate.name.StartsWith("SM_Prop_")) continue;
                var bounds = GeometryBounds(candidate.gameObject);
                if (bounds.size == Vector3.zero || !clearApron.Intersects(bounds)) continue;
                problems++;
                Debug.LogWarning($"Neighbour frontage obstruction: prop '{candidate.name}' at {candidate.position} enters the shutter apron.");
            }

            if (problems == 0)
                Debug.Log("Raccoon Heist: neighbour frontage bounds/apron clearance check passed.");
        }

        // Agents cannot safely launch a second Unity process while the project is
        // already open. Dropping the project-root marker lets the live editor perform
        // one requested generation after its next domain reload, then removes it.
        [InitializeOnLoadMethod]
        static void RunRequestedGeneration()
        {
            string marker = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath)!, "RegenerateEnvironment.once");
            if (!System.IO.File.Exists(marker)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning("Raccoon Heist: environment regeneration is waiting for Edit mode.");
                    EditorApplication.playModeStateChanged -= RunWhenEditMode;
                    EditorApplication.playModeStateChanged += RunWhenEditMode;
                    return;
                }

                try
                {
                    GenerateAndSave();
                    CaptureEnvironmentPreviews();
                    System.IO.File.Delete(marker);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        static void RunWhenEditMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= RunWhenEditMode;
            RunRequestedGeneration();
        }

        [InitializeOnLoadMethod]
        static void RunRequestedCapture()
        {
            string marker = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath)!, "CaptureEnvironment.once");
            if (!System.IO.File.Exists(marker)) return;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    CaptureEnvironmentPreviews();
                    System.IO.File.Delete(marker);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
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

        static Material EmissiveMat(string name, Color baseColor, Color emission)
        {
            var mat = Mat(name, baseColor, 0.22f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        static Material TransparentMat(string name, Color color, float smoothness = 0.72f)
        {
            var mat = Mat(name, color, smoothness);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        static Material Wall => Mat("Wall", new Color(0.42f, 0.40f, 0.36f));
        static Material Wood => Mat("ShelfWood", new Color(0.42f, 0.31f, 0.21f));
        static Material Crate => Mat("Crate", new Color(0.55f, 0.42f, 0.28f));

        // ---------- procedural textures (generated once as PNG assets) ----------

        static Material TiledMat(string name, Color tint, Texture2D tex, float tileX, float tileY, float smoothness = 0.08f)
        {
            string key = $"{name}_{Mathf.RoundToInt(tileX)}x{Mathf.RoundToInt(tileY)}";
            if (matCache.TryGetValue(key, out var cached)) return cached;
            var mat = Mat(key, tint, smoothness);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(tileX, tileY));
            mat.SetFloat("_Smoothness", smoothness);
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
        const float OutD0 = -14f, OutD1 = 25f;      // z range: two-lane street | shop block | alley

        static void BuildGroundAndCeilings()
        {
            BoxMinMax("Ground", new Vector3(OutW0 - 0.3f, -0.1f, OutD0 - 0.3f), new Vector3(OutW1 + 0.3f, 0f, OutD1 + 0.3f), null,
                TiledMat("Rough", new Color(0.40f, 0.40f, 0.42f), TexRough, 18f, 22f));
            // Zone overlays: read where you are by the ground underfoot. The street
            // strips run far past the invisible walls so the road reads as endless.
            // Road has painted markings; pavements are raised 0.12 m with curb faces
            // (hop-able: raccoon step offset handles it).
            bool cityRoads = SyntyPrefab("SM_Env_Road_Lines_01") != null;
            BoxMinMax("Road", new Vector3(-40f, 0f, -12f), new Vector3(W + 40f, 0.02f, -2f), null,
                cityRoads ? TiledMat("AsphaltWet", new Color(0.30f, 0.32f, 0.38f), TexAsphalt, 42f, 2f, 0.42f)
                          : TiledMat("RoadMarkedWet", new Color(0.38f, 0.40f, 0.46f), TexRoad, 14f, 1f, 0.42f));
            if (cityRoads)
                for (float x0 = -40f; x0 < W + 40f; x0 += 5f)
                {
                    bool crossing = Mathf.Approximately(x0, -10f) || Mathf.Approximately(x0, 15f);
                    foreach (float laneZ in new[] { -4.5f, -9.5f })
                        PlaceSynty(crossing ? "SM_Env_Road_Crossing_01" : "SM_Env_Road_01",
                            new Vector3(x0 + 2.5f, 0.028f, laneZ), 90f);
                }

            // A broken centre stripe makes the ten-metre surface unmistakably read
            // as two opposing lanes even when the prefab markings are in shadow.
            var roadStripe = Mat("RoadCentreStripe", new Color(0.64f, 0.60f, 0.43f), 0.18f);
            int stripeIndex = 0;
            for (float x0 = -39f; x0 < W + 40f; x0 += 6f)
                BoxMinMax($"RoadCentreStripe_{stripeIndex++}", new Vector3(x0, 0.031f, -7.055f),
                    new Vector3(x0 + 3.1f, 0.037f, -6.945f), null, roadStripe);

            // The opposite pavement has a wall-side furniture verge beyond the
            // playable limit, a clear walking strip, then curb-side lamp hardware.
            BoxMinMax("Pavement_Far", new Vector3(-40f, 0f, OutD0 - 1.5f), new Vector3(W + 40f, 0.12f, -12f), null,
                TiledMat("SlabsDamp", new Color(0.44f, 0.45f, 0.49f), TexSlabs, 40f, 1f, 0.22f));
            BoxMinMax("Pavement_Shop", new Vector3(-40f, 0f, -2f), new Vector3(W + 40f, 0.12f, 0f), null,
                TiledMat("SlabsDamp", new Color(0.44f, 0.45f, 0.49f), TexSlabs, 40f, 1f, 0.22f));
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

        // Facade skins, puddles, posters, and other visual-only dressing must not
        // accidentally seal a gameplay opening with PlaceSynty's fitted collider.
        static GameObject PlaceSyntyDecorative(string name, Vector3 floorPos, float yaw = 0f)
        {
            var go = PlaceSynty(name, floorPos, yaw);
            if (go == null) return null;
            foreach (var collider in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
            return go;
        }

        // Particle-only prefabs have no mesh bounds to ground against. Instantiate
        // them directly so steam/fog FX do not receive a zero-sized box collider.
        static GameObject PlaceSyntyFx(string name, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var prefab = SyntyPrefab(name);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
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

            // Crate steps sit beside the centered door, leaving its swing and the
            // direct entry sightline clear while preserving the front parkour route.
            BoxMinMax("Crates_Door_A", new Vector3(5.0f, 0f, 0.5f), new Vector3(5.6f, 0.6f, 1.1f), null, Crate);
            BoxMinMax("Crates_Door_B", new Vector3(5.0f, 0f, 1.15f), new Vector3(5.6f, 1.2f, 1.75f), null, Crate);

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
            BuildNeighbourFrontage();
            BuildExteriorShell();

            // Street dressing: a parked food trailer (cover + climbable) and ground
            // decals. The entrance canopy is part of the unified facade, not a deep
            // prefab awning intersecting its windows and door trim.
            PlaceSynty("SM_Veh_Food_Trailer_01", new Vector3(15.2f, 0f, -4.2f), 90f);
            PlaceSynty("SM_Env_Ground_Manhole_01", new Vector3(5.5f, 0.03f, -3.2f));
            PlaceSynty("SM_Env_Ground_ParkingLines_01", new Vector3(-2.5f, 0.03f, -4.2f));
            PlaceSynty("SM_Env_Ground_Panel_01", new Vector3(7.2f, 0.03f, 22.5f));
            PlaceSynty("SM_Prop_Rubbish_Bin_02", new Vector3(5.4f, 0f, 24.1f), 25f);
            PlaceSynty("SM_Prop_Rubbish_Bin_03", new Vector3(12.5f, 0f, 22f), 190f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(10.9f, 0f, 24.3f), 120f);

            // Bins across both lanes — the yeet landing zone on the far pavement.
            for (int i = 0; i < 3; i++)
            {
                if (PlaceSynty($"SM_Prop_Rubbish_Bin_0{i + 1}", new Vector3(2.6f + i * 1.1f, 0.12f, OutD0 + 0.28f), 180f) == null)
                    BoxMinMax($"Bin_{i}", new Vector3(2.3f + i * 1.1f, 0.12f, OutD0 + 0.05f), new Vector3(3.1f + i * 1.1f, 1f, OutD0 + 0.6f), null,
                        Mat("BinGreen", new Color(0.24f, 0.34f, 0.24f)));
            }
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(5.6f, 0.12f, OutD0 + 0.25f), 40f);

            // Side passage clutter (east, where the vent is)
            PlaceSynty("SM_Prop_Warehouse_Pallet_01", new Vector3(W + 3.5f, 0f, 6f), 15f);
            PlaceSynty("SM_Prop_Warehouse_Pallet_Stacked_01", new Vector3(W + 3.8f, 0f, 12f), 75f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(W + 1f, 0f, 17f), 200f);

            // Alley roof route: crate (0.6) -> dumpster (1.3) -> AC unit (2.2) -> storage roof (3.1)
            CrateStack("AlleyCrate", 11.2f, StorageZ1 + 0.4f, 0.6f);
            var dumpster = PlaceSynty("SM_Prop_Dumptser_02", new Vector3(10f, 0f, StorageZ1 + 0.95f), 90f);
            if (dumpster != null)
                dumpster.name = "Dumpster";
            else
                BoxMinMax("Dumpster", new Vector3(9.2f, 0f, StorageZ1 + 0.3f), new Vector3(10.8f, 1.3f, StorageZ1 + 1.3f), null,
                    Mat("DumpsterGreen", new Color(0.22f, 0.32f, 0.26f), 0.3f));
            var acUnit = PlaceSynty("SM_Prop_Aircon_01", new Vector3(8.45f, 1.58f, StorageZ1 + 0.55f), 90f);
            if (acUnit != null)
                acUnit.name = "AcUnit";
            else
                BoxMinMax("AcUnit", new Vector3(7.9f, 1.6f, StorageZ1 + T), new Vector3(8.9f, 2.2f, StorageZ1 + T + 0.7f), null,
                    Mat("AcMetal", new Color(0.55f, 0.56f, 0.58f), 0.35f));

            // More alley cover
            CrateStack("AlleyCrates_2", 2.5f, 22.5f, 1.2f);
            PlaceSynty("SM_Prop_Warehouse_Pallet_01", new Vector3(4.5f, 0f, 23.5f), 100f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(1.2f, 0f, 24.2f), 310f);

            // POLYGON City extras — silent no-ops until that pack is imported
            // Keep the actual entrance sightline clear; this parked car used to hide
            // the door and pet flap from both players and the storefront camera.
            PlaceSynty("SM_Veh_Car_Sedan_01", new Vector3(-8.5f, 0f, -4.3f), 92f);
            PlaceSynty("SM_Veh_Car_Taxi_01", new Vector3(-2.2f, 0f, -4.1f), 268f);
            RoofBillboard(new Vector3(-2.6f, 4f, 12f), 90f, 3); // neighbour's roof, facing the passage

            BuildPlayableBoundaryDressing();
            DecorateStreetPassageAndAlley();
        }

        static void BuildNeighbourFrontage()
        {
            // Screenshot regression guard: the old fire-escape prefab landed at pavement level here and intersected
            // a bench, reading as an unsupported clothes rack. A closed roller shutter
            // gives this blank neighbour wall a grounded, street-facing purpose instead.
            var shutter = Mat("NeighbourShutter", new Color(0.15f, 0.19f, 0.24f), 0.10f);
            var trim = Mat("NeighbourShutterTrim", new Color(0.055f, 0.075f, 0.10f), 0.14f);
            var sign = Mat("NeighbourSign", new Color(0.13f, 0.10f, 0.16f), 0.08f);
            var lamp = EmissiveMat("NeighbourFrontageLamp", new Color(0.72f, 0.48f, 0.22f), new Color(2.4f, 1.15f, 0.42f));

            // The neighbour wall is exactly OutW0..-0.2. These inset limits leave
            // visible brick reveals on both sides, including the frame thickness.
            const float left = -4.65f;
            const float right = -0.55f;
            BoxMinMax("NeighbourClosedShutter", new Vector3(left, 0.16f, -0.075f), new Vector3(right, 2.48f, -0.025f), null, shutter);
            BoxMinMax("NeighbourShutterFrame_L", new Vector3(left - 0.10f, 0.10f, -0.12f), new Vector3(left + 0.05f, 2.60f, -0.015f), null, trim);
            BoxMinMax("NeighbourShutterFrame_R", new Vector3(right - 0.05f, 0.10f, -0.12f), new Vector3(right + 0.10f, 2.60f, -0.015f), null, trim);
            BoxMinMax("NeighbourShutterFrame_T", new Vector3(left - 0.10f, 2.48f, -0.12f), new Vector3(right + 0.10f, 2.62f, -0.015f), null, trim);
            for (float y = 0.32f; y < 2.42f; y += 0.22f)
                BoxMinMax($"NeighbourShutterSlat_{y:0.00}", new Vector3(left + 0.04f, y, -0.10f), new Vector3(right - 0.04f, y + 0.025f, -0.065f), null, trim);

            BoxMinMax("NeighbourShopSign", new Vector3(-4.20f, 2.78f, -0.10f), new Vector3(-1.00f, 3.28f, -0.025f), null, sign);
            BoxMinMax("NeighbourShopSignLight", new Vector3(-2.82f, 3.05f, -0.17f), new Vector3(-2.38f, 3.18f, -0.105f), null, lamp);
            PointLight("NeighbourFrontageGlow", new Vector3(-2.6f, 2.78f, -0.72f), new Color(1f, 0.54f, 0.22f), 0.55f, 4.2f);
        }

        // The interior walls stay simple and readable, while thin exterior skins make
        // the building belong to the surrounding city.
        static void BuildExteriorShell()
        {
            var exteriorBrick = BrickMat(D);
            BoxMinMax("ExteriorSkin_EastShop", new Vector3(W + T, 0f, 1.5f), new Vector3(W + T + 0.045f, H, D + T), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_BackRoomEast", new Vector3(3f + T, 0f, D + T), new Vector3(3f + T + 0.045f, H, D + T + 4f + T), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_BackRoomNorth", new Vector3(0f, 0f, D + T + 4f + T), new Vector3(3f + T, H, D + T + 4f + T + 0.045f), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_StorageNorth_W", new Vector3(StorageX0, 0f, StorageZ1 + T), new Vector3(8.2f, H, StorageZ1 + T + 0.045f), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_StorageNorth_E", new Vector3(9f, 0f, StorageZ1 + T), new Vector3(StorageX1, H, StorageZ1 + T + 0.045f), null, exteriorBrick);

            // Three overlapping modular shopfront prefabs made this read as three
            // unrelated buildings and hid signs/posters behind their structural bays.
            // One measured frame now follows the actual wall openings exactly.
            BuildUnifiedStorefront(exteriorBrick);
            BuildStorefrontBranding();
            var openSign = PlaceSyntyDecorative("SM_Sign_Neon_Open_03", new Vector3(2.05f, 1.25f, 0.08f), 180f);
            MakeEmissive(openSign);

            float doorMin = EntranceX - EntranceWidth * 0.5f;
            float doorMax = EntranceX + EntranceWidth * 0.5f;

            // The generated panel is the only door mesh. Its glass, hardware, and pet
            // flap share the real hinge; no second prefab sits half a metre in front.
            BoxMinMax("EntranceDoormat", new Vector3(doorMin - 0.05f, 0.121f, -0.96f), new Vector3(doorMax + 0.05f, 0.145f, -0.53f), null,
                Mat("EntranceMat", new Color(0.13f, 0.15f, 0.17f), 0.05f));
            var brass = Mat("EntranceBrass", new Color(0.62f, 0.45f, 0.17f), 0.52f);
            ParentToEntranceDoor(BoxMinMax("EntranceHandle", new Vector3(doorMax - 0.26f, 1.0f, -0.22f),
                new Vector3(doorMax - 0.20f, 1.31f, -0.16f), null, brass));
            var entranceTrim = Mat("EntranceTrim", new Color(0.18f, 0.39f, 0.48f), 0.36f);
            BoxMinMax("EntranceTrim_L", new Vector3(doorMin - 0.08f, 0.1f, -0.29f), new Vector3(doorMin, 2.18f, -0.19f), null, entranceTrim);
            BoxMinMax("EntranceTrim_R", new Vector3(doorMax, 0.1f, -0.29f), new Vector3(doorMax + 0.08f, 2.18f, -0.19f), null, entranceTrim);
            BoxMinMax("EntranceTrim_T", new Vector3(doorMin - 0.08f, 2.10f, -0.29f), new Vector3(doorMax + 0.08f, 2.18f, -0.19f), null, entranceTrim);

            var doorGlass = BoxMinMax("EntranceDoorGlass", new Vector3(doorMin + 0.17f, 0.82f, -0.172f),
                new Vector3(doorMax - 0.17f, 1.91f, -0.154f), null,
                TransparentMat("StorefrontGlass", new Color(0.12f, 0.30f, 0.48f, 0.38f)));
            Object.DestroyImmediate(doorGlass.GetComponent<Collider>());
            ParentToEntranceDoor(doorGlass);
            ParentToEntranceDoor(BoxMinMax("EntranceDoorWindow_L", new Vector3(doorMin + 0.12f, 0.77f, -0.20f),
                new Vector3(doorMin + 0.18f, 1.96f, -0.16f), null, entranceTrim));
            ParentToEntranceDoor(BoxMinMax("EntranceDoorWindow_R", new Vector3(doorMax - 0.18f, 0.77f, -0.20f),
                new Vector3(doorMax - 0.12f, 1.96f, -0.16f), null, entranceTrim));
            ParentToEntranceDoor(BoxMinMax("EntranceDoorWindow_B", new Vector3(doorMin + 0.12f, 0.77f, -0.20f),
                new Vector3(doorMax - 0.12f, 0.83f, -0.16f), null, entranceTrim));
            ParentToEntranceDoor(BoxMinMax("EntranceDoorWindow_T", new Vector3(doorMin + 0.12f, 1.90f, -0.20f),
                new Vector3(doorMax - 0.12f, 1.96f, -0.16f), null, entranceTrim));
            var flapFrame = Mat("PetFlapFrame", new Color(0.16f, 0.18f, 0.20f), 0.24f);
            ParentToEntranceDoor(BoxMinMax("PetFlapFrame_L", new Vector3(EntranceX - 0.24f, 0.12f, -0.22f),
                new Vector3(EntranceX - 0.18f, 0.5f, -0.16f), null, flapFrame));
            ParentToEntranceDoor(BoxMinMax("PetFlapFrame_R", new Vector3(EntranceX + 0.18f, 0.12f, -0.22f),
                new Vector3(EntranceX + 0.24f, 0.5f, -0.16f), null, flapFrame));
            ParentToEntranceDoor(BoxMinMax("PetFlapFrame_T", new Vector3(EntranceX - 0.18f, 0.44f, -0.22f),
                new Vector3(EntranceX + 0.18f, 0.5f, -0.16f), null, flapFrame));
            ParentToEntranceDoor(BoxMinMax("PetFlapPanel", new Vector3(EntranceX - 0.18f, 0.14f, -0.215f),
                new Vector3(EntranceX + 0.18f, 0.44f, -0.155f), null,
                Mat("PetFlapPanel", new Color(0.34f, 0.29f, 0.24f), 0.12f)));

            // A compact, procedural canopy is aligned to the same central bay. It
            // replaces the deep prefab awning that intersected the trim and glazing.
            var canopy = BoxMinMax("EntranceCanopy", new Vector3(doorMin - 0.18f, 2.12f, -0.72f),
                new Vector3(doorMax + 0.18f, 2.22f, -0.20f), null,
                Mat("StorefrontCanopy", new Color(0.32f, 0.075f, 0.085f), 0.18f));
            Object.DestroyImmediate(canopy.GetComponent<Collider>());
            var canopyLight = BoxMinMax("EntranceCanopyLight", new Vector3(EntranceX - 0.30f, 2.105f, -0.64f),
                new Vector3(EntranceX + 0.30f, 2.125f, -0.38f), null,
                EmissiveMat("EntranceCanopyLight", new Color(0.68f, 0.43f, 0.18f), new Color(3.2f, 1.55f, 0.52f)));
            Object.DestroyImmediate(canopyLight.GetComponent<Collider>());

            // Side/rear details stay asset-rich; the front is deliberately uncluttered
            // so nothing hides behind a frame or competes with the open sign.
            MakeEmissive(PlaceSyntyDecorative("SM_Prop_Wall_Light_01", new Vector3(W + 0.44f, 2.35f, 7.2f), 90f));
            MakeEmissive(PlaceSyntyDecorative("SM_Prop_Wall_Light_01", new Vector3(9.6f, 2.35f, StorageZ1 + T + 0.44f)));
            PlaceSyntyDecorative("SM_Bld_Awning_01_Small", new Vector3(8.6f, 2.35f, StorageZ1 + T + 0.12f));
            PlaceSyntyDecorative("SM_Prop_Poster_02", new Vector3(6.4f, 0.85f, StorageZ1 + T + 0.08f));
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_02", new Vector3(10.25f, 0.15f, StorageZ1 + T + 0.08f));
            PlaceSynty("SM_Prop_PowerBox_01", new Vector3(W + 0.65f, 0f, 10.5f), 90f);
            // The old seven-metre pipe preset projected through the passage and roof.
            // A compact, wall-flush utility run gives the same grime without tangles.
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_01", new Vector3(W + 0.18f, 0.35f, 12.35f), 90f);
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_02", new Vector3(W + 0.18f, 0.35f, 12.75f), 90f);
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_01", new Vector3(W + 0.18f, 0.35f, 13.15f), 90f);
            MakeEmissive(PlaceSyntyDecorative("SM_Prop_Wall_Light_01", new Vector3(W + 0.44f, 2.25f, 16.2f), 90f));
            MakeEmissive(PlaceSyntyDecorative("SM_Prop_Wall_Light_01", new Vector3(2.2f, 2.35f, D + T + 4f + T + 0.44f)));
        }

        static void BuildUnifiedStorefront(Material exteriorBrick)
        {
            float doorMin = EntranceX - EntranceWidth * 0.5f;
            float doorMax = EntranceX + EntranceWidth * 0.5f;
            const float windowMinX = 0.5f;
            const float windowMaxX = W - 0.5f;
            const float windowBottom = 0.9f;
            const float windowTop = 2.5f;
            var frame = Mat("StorefrontFrame", new Color(0.105f, 0.24f, 0.31f), 0.42f);
            var glass = TransparentMat("StorefrontGlass", new Color(0.12f, 0.30f, 0.48f, 0.38f));

            // Continuous masonry and fascia make the full fourteen metres read as one
            // address. All depth is within 5.5 cm of the actual front wall.
            BoxMinMax("StorefrontPlinth_L", new Vector3(0.2f, 0.1f, -0.225f),
                new Vector3(doorMin, windowBottom, -0.20f), null, exteriorBrick);
            BoxMinMax("StorefrontPlinth_R", new Vector3(doorMax, 0.1f, -0.225f),
                new Vector3(W - 0.2f, windowBottom, -0.20f), null, exteriorBrick);

            var leftGlass = BoxMinMax("StorefrontGlass_Left", new Vector3(windowMinX, windowBottom, -0.215f),
                new Vector3(doorMin - 0.22f, windowTop, -0.202f), null, glass);
            var rightGlass = BoxMinMax("StorefrontGlass_Right", new Vector3(doorMax + 0.22f, windowBottom, -0.215f),
                new Vector3(windowMaxX, windowTop, -0.202f), null, glass);
            Object.DestroyImmediate(leftGlass.GetComponent<Collider>());
            Object.DestroyImmediate(rightGlass.GetComponent<Collider>());

            // Outer frame, door-adjacent frame, two mullions, and shared top/bottom
            // rails. Nothing crosses a sign or sits in front of a poster.
            foreach (float x in new[] { windowMinX, doorMin - 0.22f, doorMax + 0.22f, windowMaxX })
                BoxMinMax($"StorefrontFrame_V_{x}", new Vector3(x - 0.055f, windowBottom - 0.06f, -0.255f),
                    new Vector3(x + 0.055f, windowTop + 0.06f, -0.19f), null, frame);
            foreach (float x in new[] { (windowMinX + doorMin - 0.22f) * 0.5f, (doorMax + 0.22f + windowMaxX) * 0.5f })
                BoxMinMax($"StorefrontMullion_{x}", new Vector3(x - 0.035f, windowBottom, -0.247f),
                    new Vector3(x + 0.035f, windowTop, -0.19f), null, frame);
            foreach (float y in new[] { windowBottom, windowTop })
            {
                BoxMinMax($"StorefrontRail_L_{y}", new Vector3(windowMinX - 0.055f, y - 0.055f, -0.255f),
                    new Vector3(doorMin - 0.165f, y + 0.055f, -0.19f), null, frame);
                BoxMinMax($"StorefrontRail_R_{y}", new Vector3(doorMax + 0.165f, y - 0.055f, -0.255f),
                    new Vector3(windowMaxX + 0.055f, y + 0.055f, -0.19f), null, frame);
            }
        }

        // Builds the title from the POLYGON pack's measured letter prefabs. This
        // keeps the shop identity fully 3D and in-family with the supplied low-poly art.
        static void BuildStorefrontBranding()
        {
            var backing = BoxMinMax("RaccoonHeistSignBacking", new Vector3(0.28f, 2.22f, -0.31f),
                new Vector3(W - 0.28f, 2.98f, -0.22f), null,
                Mat("RaccoonHeistSignBacking", new Color(0.025f, 0.045f, 0.075f), 0.25f));
            Object.DestroyImmediate(backing.GetComponent<Collider>());

            const string title = "RACCOON HEIST";
            const float scale = 0.82f;
            const float spacing = 0.08f;
            const float wordSpace = 0.34f;
            float total = 0f;
            foreach (char character in title)
            {
                if (character == ' ') { total += wordSpace; continue; }
                var prefab = SyntyPrefab($"SM_Sign_3dText_Letter_{character}");
                if (prefab != null) total += GeometryBounds(prefab).size.x * scale + spacing;
            }
            total = Mathf.Max(0f, total - spacing);

            var holder = new GameObject("RaccoonHeistTitle").transform;
            holder.SetParent(root, false);
            var letterMat = EmissiveMat("RaccoonHeistLetters", new Color(0.08f, 0.48f, 0.62f), new Color(0.2f, 2.4f, 3.2f));
            float cursor = 7f - total * 0.5f;
            foreach (char character in title)
            {
                if (character == ' ') { cursor += wordSpace; continue; }
                string prefabName = $"SM_Sign_3dText_Letter_{character}";
                var prefab = SyntyPrefab(prefabName);
                if (prefab == null) continue;
                float width = GeometryBounds(prefab).size.x * scale;
                var letter = PlaceSyntyDecorative(prefabName, new Vector3(cursor + width * 0.5f, 2.28f, -0.35f), 180f);
                if (letter != null)
                {
                    letter.name = $"Title_{character}_{Mathf.RoundToInt(cursor * 100f)}";
                    letter.transform.localScale *= scale;
                    ApplyMatRecursive(letter, letterMat);
                    letter.transform.SetParent(holder, true);
                }
                cursor += width + spacing;
            }

        }

        static void ApplyMatRecursive(GameObject go, Material mat)
        {
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = mat;
                renderer.sharedMaterials = materials;
            }
        }

        static void ParentToEntranceDoor(GameObject go)
        {
            if (go == null) return;
            var pivot = root.Find("EntranceDoorPivot");
            if (pivot != null) go.transform.SetParent(pivot, true);
        }

        // Invisible colliders remain the authoritative limit, but every edge also has
        // a physical explanation: storefronts, chain-link fencing, or road works.
        static void BuildPlayableBoundaryDressing()
        {
            for (float z = 2.5f; z < OutD1; z += 5f)
                PlaceSynty("SM_Bld_Metal_Fence_02", new Vector3(OutW1 + 0.08f, 0f, z), 90f);
            PlaceSynty("SM_Bld_Metal_Fence_02", new Vector3(OutW0 - 0.08f, 0f, 22.5f), 90f);

            foreach (float z in new[] { -12.3f, -10.7f, -9.1f, -6.3f, -4.7f, -3.1f })
            {
                PlaceSynty("SM_Prop_Barrier_01", new Vector3(OutW0 + 0.15f, 0.02f, z), 90f);
                PlaceSynty("SM_Prop_Barrier_01", new Vector3(OutW1 - 0.15f, 0.02f, z), 90f);
            }
            PlaceSynty("SM_Prop_Cone_01", new Vector3(OutW0 + 0.45f, 0.02f, -2.15f));
            PlaceSynty("SM_Prop_Cone_02", new Vector3(OutW1 - 0.45f, 0.02f, -7.45f), 25f);
            // Kept clear of the roadwork barriers instead of intersecting their ends.
            PlaceSynty("SM_Veh_Car_Police_01", new Vector3(OutW1 + 3.3f, 0.02f, -4.3f), 90f);
        }

        static void DecorateStreetPassageAndAlley()
        {
            // Front street furniture establishes scale and creates strong silhouettes.
            PlaceSynty("SM_Prop_Hydrant_01", new Vector3(-1.1f, 0.12f, -1.72f), 20f);
            PlaceSynty("SM_Prop_Mailbox_01", new Vector3(12.8f, 0.12f, -1.68f), 180f);
            PlaceSynty("SM_Prop_ParkingMeter_01", new Vector3(0.2f, 0.12f, -1.78f));
            PlaceSynty("SM_Prop_ParkingMeter_01", new Vector3(13.8f, 0.12f, -1.78f));
            PlaceSynty("SM_Prop_ParkBench_01", new Vector3(10.8f, 0.12f, OutD0 + 0.25f), 180f);
            PlaceSyntyDecorative("SM_Prop_Newspaper_01", new Vector3(6.5f, 0.125f, -0.38f), 15f);

            // Keep the hero facade and centered entrance readable, but let the shop
            // pavement join the broader street composition at both outer corners.
            PlaceSynty("SM_Prop_Planter_02", new Vector3(W + 2.15f, 0.12f, -0.45f));
            PlaceSynty("SM_Prop_Newspaper_02", new Vector3(W + 3.45f, 0.12f, -0.42f), 180f);
            PlaceSynty("SM_Prop_Rubbish_Bin_01", new Vector3(W + 4.45f, 0.12f, -0.42f), 345f);

            foreach (var puddle in new[]
                     {
                         new Vector3(0.5f, 0.032f, -3.2f), new Vector3(11.2f, 0.032f, -5.6f),
                         new Vector3(15.6f, 0.012f, 5.5f), new Vector3(4.2f, 0.012f, 23.2f)
                     })
                PlaceSyntyDecorative("SM_Prop_Water_Puddle_01", puddle, Random.Range(0f, 360f));

            // Small access covers interrupt the new perimeter slab aprons without
            // turning the route into an obstacle course at raccoon height.
            PlaceSyntyDecorative("SM_Prop_Sidewalk_Panel_02", new Vector3(14.78f, 0.076f, 5.2f), 18f);
            PlaceSyntyDecorative("SM_Prop_Sidewalk_Panel_04", new Vector3(14.78f, 0.076f, 14.4f), 75f);
            PlaceSyntyDecorative("SM_Prop_Sidewalk_Panel_03", new Vector3(4.9f, 0.076f, 20.75f), 110f);

            // East service passage: utility hardware, a skip, and delivery clutter.
            PlaceSynty("SM_Prop_Skip_02", new Vector3(17.75f, 0f, 15.5f), 90f);
            PlaceSynty("SM_Prop_Warehouse_Boxes_Stacked_03", new Vector3(17.5f, 0f, 8.2f), 15f);
            PlaceSynty("SM_Prop_Warehouse_Pallet_Jack_01", new Vector3(15.7f, 0f, 10.1f), 200f);
            PlaceSynty("SM_Prop_Trashbin_01", new Vector3(18.15f, 0f, 4.2f), 25f);

            // Rear alley: mixed refuse, paper, and localized steam. Avoid unsupported
            // hanging props here; the open service yard has nowhere credible to anchor them.
            PlaceSynty("SM_Prop_TrashBag_01", new Vector3(7.8f, 0f, 24.05f), 25f);
            PlaceSynty("SM_Prop_TrashBag_03", new Vector3(8.45f, 0f, 23.85f), 300f);
            PlaceSyntyDecorative("SM_Props_Papers_03", new Vector3(12.2f, 0.015f, 23.7f), 80f);

            // Every plume has an obvious source. This avoids disconnected smoke that
            // looks like a misplaced effect rather than city infrastructure.
            PlaceSyntyDecorative("SM_Env_Ground_Manhole_01", new Vector3(17.8f, 0.025f, 10f), 20f);
            PlaceSyntyDecorative("SM_Env_Ground_Manhole_01", new Vector3(3.8f, 0.025f, 24.1f), 105f);
            PlaceSyntyFx("FX_Steam", new Vector3(5.5f, 0.06f, -3.2f), 0f, 0.7f);
            PlaceSyntyFx("FX_Steam", new Vector3(W + 0.45f, 0.16f, 1.25f), 90f, 0.55f);
            PlaceSyntyFx("FX_Steam", new Vector3(17.8f, 0.075f, 10f), 180f, 0.55f);
            PlaceSyntyFx("FX_Steam", new Vector3(3.8f, 0.075f, 24.1f), 25f, 0.5f);

            // Thin animated ground haze makes the fog visible in nearby light pools.
            // It is deliberately low-alpha and broad: atmosphere, not smoke plumes.
            PlaceGroundHaze("StreetGroundHaze", new Vector3(7f, 0.28f, -7f), 0.56f);
            PlaceGroundHaze("PassageGroundHaze", new Vector3(17f, 0.24f, 10.5f), 0.34f);
            PlaceGroundHaze("AlleyGroundHaze", new Vector3(7.5f, 0.24f, 23f), 0.38f);
        }

        static void PlaceGroundHaze(string name, Vector3 position, float scale)
        {
            var haze = PlaceSyntyFx("FX_Fog_01", position, 0f, scale);
            if (haze == null) return;
            haze.name = name;
            foreach (var particles in haze.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.16f, 0.22f, 0.38f, 0.08f),
                    new Color(0.30f, 0.40f, 0.62f, 0.22f));
                main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 28f);
                main.startSize = new ParticleSystem.MinMaxCurve(8f, 18f);
                var emission = particles.emission;
                emission.rateOverTime = 3f;
                var shape = particles.shape;
                shape.radius = 14f;
            }
            foreach (var renderer in haze.GetComponentsInChildren<ParticleSystemRenderer>())
                renderer.sharedMaterial = GroundHazeMaterial(renderer.sharedMaterial);
        }

        static Material GroundHazeMaterial(Material source)
        {
            const string path = "Assets/Materials/Greybox/GroundHaze.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            // The supplied generic FX material fades particles out eight metres from
            // the camera; that makes a block-scale fog system effectively invisible.
            if (material.HasProperty("_Enable_Camera_Fade")) material.SetFloat("_Enable_Camera_Fade", 0f);
            if (material.HasProperty("_CameraFadingEnabled")) material.SetFloat("_CameraFadingEnabled", 0f);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.34f, 0.46f, 0.78f, 0.72f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- backdrop: the city beyond the walls ----------

        static void BuildBackdrop()
        {
            var backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root, false);

            // Dark floor far past the walls so gaps between buildings never show void
            BoxMinMax("VoidFloor", new Vector3(-70f, -0.25f, -70f), new Vector3(W + 70f, -0.15f, D + 70f), backdrop,
                Mat("Void", new Color(0.04f, 0.04f, 0.06f)));

            BuildVisualStreetRing(backdrop);

            // Street level: shopfronts shoulder-to-shoulder with the train station as
            // the landmark directly across from our shop; block continues east/west;
            // apartments crowd the alley fence
            BackdropRow(backdrop, ShopFronts, true, OutD0 - 3.5f, OutW0 - 30f, 1.5f, 0f, 6f, 10f, 0.1f, 0.5f, true);
            var station = PlaceSynty("SM_Bld_Station_01", new Vector3(7f, 0f, OutD0 - 5.3f));
            if (station != null) { MakeEmissive(station); station.transform.SetParent(backdrop, true); }
            BackdropRow(backdrop, ShopFronts, true, OutD0 - 3.5f, 12.5f, OutW1 + 30f, 0f, 6f, 10f, 0.1f, 0.5f, true);
            BackdropRow(backdrop, Apartments, true, OutD1 + 14.3f, OutW0 - 28f, OutW1 + 28f, 180f,
                8f, 14f, 0.2f, 1f, false, true, OutD1 + 14.1f);

            // Flanks sit across the side roads, so the shop reads as one complete
            // city block instead of a facade with empty ground behind it. Do not add
            // along-X shop rows at z=4 here: they cross both side streets and swallow
            // vehicles placed at the west/east curbs.
            BackdropRow(backdrop, Apartments, false, OutW1 + 14.3f, OutD0 - 15f, OutD1 + 20f, 270f,
                8f, 14f, 0.2f, 1f, false, true, OutW1 + 14.1f);
            BackdropRow(backdrop, Apartments, false, OutW0 - 14.3f, OutD0 - 15f, OutD1 + 20f, 90f,
                8f, 14f, 0.2f, 1f, false, true, OutW0 - 14.1f);

            // Far skyline: procedural towers pushed deep into the fog — silhouettes only
            BackdropRow(backdrop, null, true, OutD0 - 42f, OutW0 - 40f, OutW1 + 40f, 0f, 16f, 30f, 2f, 6f);
            BackdropRow(backdrop, null, true, OutD1 + 38f, OutW0 - 40f, OutW1 + 40f, 180f, 16f, 30f, 2f, 6f);
            BackdropRow(backdrop, null, false, OutW1 + 34f, OutD0 - 20f, OutD1 + 20f, 270f, 14f, 26f, 2f, 6f);
            BackdropRow(backdrop, null, false, OutW0 - 34f, OutD0 - 20f, OutD1 + 20f, 90f, 14f, 26f, 2f, 6f);

            StreetDressing();
        }

        // Roads wrap visually around the entire block. Only the front face, east
        // service passage, and rear alley are playable; the other streets sit beyond
        // chain-link boundaries and sell a larger city without bloating traversal.
        static void BuildVisualStreetRing(Transform backdrop)
        {
            var road = TiledMat("CityAsphaltOutdoor", new Color(0.25f, 0.27f, 0.32f), TexAsphalt, 32f, 4f, 0.18f);
            var walk = TiledMat("CitySidewalkConcrete", new Color(0.35f, 0.36f, 0.39f), TexRough, 18f, 4f, 0.05f);
            var curb = TiledMat("CityCurbStone", new Color(0.54f, 0.55f, 0.58f), TexSlabs, 24f, 1f, 0.05f);

            // Rear cross street, east side street, and west side street.
            BoxMinMax("CityRoad_Back", new Vector3(OutW0 - 35f, 0f, OutD1 + 2f), new Vector3(OutW1 + 35f, 0.018f, OutD1 + 9f), backdrop, road);
            BoxMinMax("CityRoad_East", new Vector3(OutW1 + 2f, 0f, OutD0 - 35f), new Vector3(OutW1 + 9f, 0.018f, OutD1 + 35f), backdrop, road);
            BoxMinMax("CityRoad_West", new Vector3(OutW0 - 9f, 0f, OutD0 - 35f), new Vector3(OutW0 - 2f, 0.018f, OutD1 + 35f), backdrop, road);

            BoxMinMax("CityWalk_BackNear", new Vector3(OutW0 - 35f, 0f, OutD1), new Vector3(OutW1 + 35f, 0.12f, OutD1 + 2f), backdrop, walk);
            BoxMinMax("CityWalk_BackFar", new Vector3(OutW0 - 35f, 0f, OutD1 + 9f), new Vector3(OutW1 + 35f, 0.12f, OutD1 + 13.5f), backdrop, walk);
            BoxMinMax("CityWalk_EastNear", new Vector3(OutW1, 0f, OutD0 - 35f), new Vector3(OutW1 + 2f, 0.12f, OutD1 + 35f), backdrop, walk);
            BoxMinMax("CityWalk_EastFar", new Vector3(OutW1 + 9f, 0f, OutD0 - 35f), new Vector3(OutW1 + 13.5f, 0.12f, OutD1 + 35f), backdrop, walk);
            BoxMinMax("CityWalk_WestNear", new Vector3(OutW0 - 2f, 0f, OutD0 - 35f), new Vector3(OutW0, 0.12f, OutD1 + 35f), backdrop, walk);
            BoxMinMax("CityWalk_WestFar", new Vector3(OutW0 - 13.5f, 0f, OutD0 - 35f), new Vector3(OutW0 - 9f, 0.12f, OutD1 + 35f), backdrop, walk);

            // Raised curb caps make both paired pavements read as separate surfaces in the
            // dark grade. They also expose any accidental road-side prop placement.
            BoxMinMax("CityCurb_BackNear", new Vector3(OutW0 - 35f, 0.12f, OutD1 + 1.84f),
                new Vector3(OutW1 + 35f, 0.18f, OutD1 + 2f), backdrop, curb);
            BoxMinMax("CityCurb_BackFar", new Vector3(OutW0 - 35f, 0.12f, OutD1 + 9f),
                new Vector3(OutW1 + 35f, 0.18f, OutD1 + 9.16f), backdrop, curb);
            BoxMinMax("CityCurb_EastNear", new Vector3(OutW1 + 1.84f, 0.12f, OutD0 - 35f),
                new Vector3(OutW1 + 2f, 0.18f, OutD1 + 35f), backdrop, curb);
            BoxMinMax("CityCurb_EastFar", new Vector3(OutW1 + 9f, 0.12f, OutD0 - 35f),
                new Vector3(OutW1 + 9.16f, 0.18f, OutD1 + 35f), backdrop, curb);
            BoxMinMax("CityCurb_WestNear", new Vector3(OutW0 - 2f, 0.12f, OutD0 - 35f),
                new Vector3(OutW0 - 1.84f, 0.18f, OutD1 + 35f), backdrop, curb);
            BoxMinMax("CityCurb_WestFar", new Vector3(OutW0 - 9.16f, 0.12f, OutD0 - 35f),
                new Vector3(OutW0 - 9f, 0.18f, OutD1 + 35f), backdrop, curb);

            // Measured 5 m POLYGON road tiles provide painted lines on every side.
            for (float x = OutW0 - 35f; x < OutW1 + 35f; x += 5f)
            {
                var tile = PlaceSynty("SM_Env_Road_Lines_01", new Vector3(x + 2.5f, 0.024f, OutD1 + 5.5f), 90f);
                if (tile != null) tile.transform.SetParent(backdrop, true);
            }
            for (float z = OutD0 - 35f; z < OutD1 + 35f; z += 5f)
            {
                var east = PlaceSynty("SM_Env_Road_Lines_01", new Vector3(OutW1 + 5.5f, 0.024f, z + 2.5f));
                var west = PlaceSynty("SM_Env_Road_Lines_01", new Vector3(OutW0 - 5.5f, 0.024f, z + 2.5f));
                if (east != null) east.transform.SetParent(backdrop, true);
                if (west != null) west.transform.SetParent(backdrop, true);
            }

            // Parked traffic makes the perimeter roads legible through the fence and fog.
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Van_01", new Vector3(-10f, 0.02f, OutD1 + 2.9f), 88f);
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Medium_01", new Vector3(23f, 0.02f, OutD1 + 8f), 270f);
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Small_01", new Vector3(OutW1 + 2.9f, 0.02f, 7f), 180f);
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Taxi_01", new Vector3(OutW1 + 8.1f, 0.02f, 20f));
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Medium_01", new Vector3(OutW0 - 2.9f, 0.02f, 5f));
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Small_01", new Vector3(OutW0 - 8.1f, 0.02f, 21f), 180f);
            foreach (float x in new[] { -24f, -8f, 8f, 24f, 40f })
                CreatePerimeterLamp($"BackLamp_{x}", new Vector3(x, 0.12f, OutD1 + 1.2f), 180f, backdrop);
            foreach (float z in new[] { -8f, 5f, 20f, 33f })
                CreatePerimeterLamp($"EastLamp_{z}", new Vector3(OutW1 + 1.2f, 0.12f, z), 270f, backdrop);
            foreach (float z in new[] { -6f, 8f, 22f, 34f })
                CreatePerimeterLamp($"WestLamp_{z}", new Vector3(OutW0 - 1.2f, 0.12f, z), 90f, backdrop);

            // The opposite sidewalks need their own curb-side lighting; relying on
            // lamps across a seven-metre road leaves their furniture as silhouettes.
            foreach (float x in new[] { -16f, 0f, 16f, 32f })
                CreatePerimeterLamp($"BackFarLamp_{x}", new Vector3(x, 0.12f, OutD1 + 9.72f), 0f, backdrop, 6.2f);
            foreach (float z in new[] { -1f, 12f, 27f, 34f })
                CreatePerimeterLamp($"EastFarLamp_{z}", new Vector3(OutW1 + 9.72f, 0.12f, z), 90f, backdrop, 6.2f);
            foreach (float z in new[] { 1f, 15f, 29f })
                CreatePerimeterLamp($"WestFarLamp_{z}", new Vector3(OutW0 - 9.72f, 0.12f, z), 270f, backdrop, 6.2f);

            DressPerimeterBlock(backdrop);
        }

        // The visual streets are close enough to read from the playable loop, so they
        // need ordinary city clutter rather than only buildings and parked cars.
        static void DressPerimeterBlock(Transform backdrop)
        {
            // Four-and-a-half-metre far sidewalks have a curb-fixture lane, a clear
            // central walking lane, and a building-side furniture lane.
            float rearFarCurb = OutD1 + 9.4f;
            float eastFarCurb = OutW1 + 9.4f;
            float westFarCurb = OutW0 - 9.4f;
            float rearFarWall = OutD1 + 13f;
            float eastFarWall = OutW1 + 13f;
            float westFarWall = OutW0 - 13f;

            // Rear near pavement: repeated curb fixtures bridge the longer gaps, while
            // the furniture stays in separated clusters instead of forming a prop wall.
            foreach (float x in new[] { -20f, -9f, 5f, 17f, 36f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(x, 0.12f, OutD1 + 1.64f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(-28f, 0.12f, OutD1 + 1.55f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(-3f, 0.12f, OutD1 + 0.78f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_02", new Vector3(-1.45f, 0.12f, OutD1 + 0.72f), 20f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(7.2f, 0.12f, OutD1 + 0.74f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(14f, 0.12f, OutD1 + 0.7f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(21f, 0.12f, OutD1 + 0.7f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Trashbin_01", new Vector3(24.8f, 0.12f, OutD1 + 0.72f), 335f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_01", new Vector3(31f, 0.12f, OutD1 + 0.72f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_02", new Vector3(32.4f, 0.12f, OutD1 + 0.72f), 35f);

            // Rear far pavement: a small transit/commercial strip against the flats.
            foreach (float x in new[] { -6f, 10f, 26f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(x, 0.12f, rearFarCurb));
            PlaceBackdropProp(backdrop, "SM_Prop_BusStop_01", new Vector3(-15f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_01", new Vector3(-11.7f, 0.12f, rearFarWall), 15f);
            PlaceBackdropProp(backdrop, "SM_Prop_Phones_01", new Vector3(-4f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(0.2f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_ATM_01", new Vector3(8f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(13.2f, 0.12f, rearFarWall));
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(18f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(26f, 0.12f, rearFarWall), 155f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(31f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(-30f, 0.12f, rearFarWall), 330f);

            // East side: delivery/service clutter on the near walk, public furniture
            // against the opposite shop row. Keep the road itself clear.
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(OutW1 + 1.55f, 0.12f, -10f), 25f);
            foreach (float z in new[] { -5f, 6.5f, 19f, 30f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(OutW1 + 1.64f, 0.12f, z), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_PowerBox_01", new Vector3(OutW1 + 0.75f, 0.12f, -1f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_01", new Vector3(OutW1 + 0.68f, 0.12f, 5.2f), 35f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_03", new Vector3(OutW1 + 0.72f, 0.12f, 6.15f), 300f);
            PlaceBackdropProp(backdrop, "SM_Prop_Warehouse_Pallet_Stacked_02", new Vector3(OutW1 + 0.72f, 0.12f, 11f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_03", new Vector3(OutW1 + 0.72f, 0.12f, 16f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(OutW1 + 0.72f, 0.12f, 22f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(OutW1 + 0.75f, 0.12f, 27f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(OutW1 + 0.72f, 0.12f, 34.5f), 270f);

            foreach (float z in new[] { -7f, 6f, 20f, 30.5f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(eastFarCurb, 0.12f, z), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(eastFarWall, 0.12f, -9f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Phones_01", new Vector3(eastFarWall, 0.12f, 1f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Trashbin_02", new Vector3(eastFarWall, 0.12f, 7f), 20f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(eastFarWall, 0.12f, 13f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(eastFarWall, 0.12f, 18f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(eastFarWall, 0.12f, 23.5f), 255f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_01", new Vector3(eastFarWall, 0.12f, 29f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_02", new Vector3(eastFarWall, 0.12f, 31.5f), 15f);

            // West side: this is the most exposed perimeter in the player views, so
            // use curb rhythm along the full run and several small residential clusters.
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(OutW0 - 1.55f, 0.12f, -10f), 25f);
            foreach (float z in new[] { -2f, 4.5f, 17f, 29f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(OutW0 - 1.64f, 0.12f, z), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Trashbin_01", new Vector3(OutW0 - 0.75f, 0.12f, 1f), 20f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(OutW0 - 0.75f, 0.12f, 6f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(OutW0 - 0.78f, 0.12f, 13f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(OutW0 - 0.75f, 0.12f, 19f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_01", new Vector3(OutW0 - 0.72f, 0.12f, 26f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_01", new Vector3(OutW0 - 0.7f, 0.12f, 27.7f), 320f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(OutW0 - 0.72f, 0.12f, 32f), 75f);

            foreach (float z in new[] { -7f, 7f, 22f, 32f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(westFarCurb, 0.12f, z), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(westFarWall, 0.12f, -10f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Phones_01", new Vector3(westFarWall, 0.12f, -2f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(westFarWall, 0.12f, 4f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(westFarWall, 0.12f, 8f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_03", new Vector3(westFarWall, 0.12f, 11f), 15f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(westFarWall, 0.12f, 16f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ATM_01", new Vector3(westFarWall, 0.12f, 21f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(westFarWall, 0.12f, 27f), 105f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(westFarWall, 0.12f, 31f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(westFarWall, 0.12f, 32.5f), 90f);
        }

        static GameObject PlaceBackdropProp(Transform parent, string name, Vector3 position, float yaw = 0f)
        {
            var go = PlaceSynty(name, position, yaw);
            if (go != null) go.transform.SetParent(parent, true);
            return go;
        }

        static void CreatePerimeterLamp(string name, Vector3 position, float yaw, Transform parent, float intensity = 3.9f)
        {
            var pole = PlaceBackdropProp(parent, "SM_Prop_Streetlight_02", position, yaw);
            if (pole == null) return;
            pole.name = name;
            MakeEmissive(pole);
            var bounds = GeometryBounds(pole);
            var lamp = new GameObject($"{name}_Light").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.type = LightType.Spot;
            lamp.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.35f, bounds.center.z);
            lamp.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            lamp.spotAngle = 88f;
            lamp.innerSpotAngle = 38f;
            lamp.color = new Color(1f, 0.55f, 0.22f);
            lamp.intensity = intensity;
            lamp.range = 8.5f;
            lamp.shadows = LightShadows.None;
        }

        static readonly string[] ShopFronts = { "SM_Bld_Shop_01", "SM_Bld_Shop_02", "SM_Bld_Shop_03",
                                                "SM_Bld_Shop_04", "SM_Bld_Shop_05", "SM_Bld_Shop_06" };
        static readonly string[] Apartments = { "SM_Bld_Apartment_Stack_01", "SM_Bld_Apartment_Stack_02" };

        static void StreetDressing()
        {
            // Lamp posts along the far pavement: glowing head + a real light under
            // the arm, with the central facade sightline intentionally left open.
            for (float x = -34f; x <= W + 34f; x += 12f)
            {
                if (x > -6f && x < W + 6f) continue; // preserve the storefront sightline
                // Poles sit on the far pavement and reach back over the two-lane road.
                // Keeping their full arm twelve metres off the facade guarantees that
                // no lamp geometry can pass through the shop roof or sign.
                var pole = PlaceSynty("SM_Prop_LightPole_Base_01", new Vector3(x, 0.12f, -12.5f), 180f);
                if (pole == null) continue;
                MakeEmissive(pole);
                var pb = GeometryBounds(pole);
                // Downward SPOT, not a point: lamps make cones and pools, not a wash
                var lamp = new GameObject($"LampSpot_{x}").AddComponent<Light>();
                lamp.transform.SetParent(root, false);
                lamp.type = LightType.Spot;
                lamp.transform.position = new Vector3(x, pb.max.y - 0.5f, pb.min.z + 0.4f);
                lamp.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                lamp.spotAngle = 100f;
                lamp.innerSpotAngle = 45f;
                lamp.color = new Color(1f, 0.62f, 0.28f); // sodium orange
                lamp.intensity = 5.5f;
                lamp.range = 9f;
            }

            // Parked cars down the road, outside the boundary
            PlaceSynty("SM_Veh_Car_Van_01", new Vector3(-28f, 0.02f, -3.9f), 272f);
            PlaceSynty("SM_Veh_Car_Taxi_01", new Vector3(W + 27f, 0.02f, -4f), 268f);
            PlaceSynty("SM_Veh_Car_Small_01", new Vector3(-7f, 0.02f, -9.5f), 270f);
            PlaceSynty("SM_Veh_Car_Medium_01", new Vector3(W + 10f, 0.02f, -9.4f), 88f);

            PlaceSynty("SM_Prop_BusStop_01", new Vector3(-12f, 0.12f, OutD0 - 0.72f));

            // Opposite pavement: an urban sequence that carries beyond both frame
            // edges. Large objects stay well separated; small curb fixtures fill the
            // visual gaps without spilling into either traffic lane.
            float mainStreetFurnitureZ = OutD0 - 0.68f;
            PlaceSynty("SM_Prop_Planter_01", new Vector3(-38f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Mailbox_01", new Vector3(-31f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Newspaper_02", new Vector3(-25f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Trashbin_01", new Vector3(-19f, 0.12f, mainStreetFurnitureZ), 15f);
            PlaceSynty("SM_Prop_Hydrant_01", new Vector3(-8f, 0.12f, mainStreetFurnitureZ), 25f);
            PlaceSynty("SM_Prop_Phones_01", new Vector3(-3.5f, 0.12f, mainStreetFurnitureZ));
            PlaceSynty("SM_Prop_Planter_02", new Vector3(W + 1f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Newspaper_02", new Vector3(W + 6f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Hydrant_01", new Vector3(W + 9f, 0.12f, mainStreetFurnitureZ), 335f);
            PlaceSynty("SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(W + 19f, 0.12f, mainStreetFurnitureZ), 165f);
            PlaceSynty("SM_Prop_Rubbish_Bin_02", new Vector3(W + 21f, 0.12f, mainStreetFurnitureZ), 15f);
            PlaceSynty("SM_Prop_ParkBench_01", new Vector3(W + 31f, 0.12f, mainStreetFurnitureZ), 180f);

            foreach (float x in new[] { -35f, -26f, -17f, -7f, 0f, W + 5f, W + 16f, W + 27f, W + 38f })
                PlaceSynty("SM_Prop_ParkingMeter_01", new Vector3(x, 0.12f, -12.22f), 180f);

            // Small storefront neons punctuate the street ring. They are kept across
            // the road or beyond the playable fence, so they add depth without noise
            // around the interaction route.
            var westNeon = PlaceSyntyDecorative("SM_Sign_Neon_Open_01", new Vector3(-12f, 1.35f, OutD0 - 1.15f));
            var eastNeon = PlaceSyntyDecorative("SM_Sign_Neon_Open_02", new Vector3(W + 24f, 1.45f, OutD0 - 1.15f));
            var rearNeon = PlaceSyntyDecorative("SM_Sign_Neon_Open_04", new Vector3(7.2f, 1.45f, OutD1 + 11.75f), 180f);
            MakeEmissive(westNeon);
            MakeEmissive(eastNeon);
            MakeEmissive(rearNeon);

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
                PlaceSynty(trees[t++ % 3], new Vector3(x, 0.12f, OutD0 - 0.72f), t * 77f);
            PlaceSynty(trees[0], new Vector3(-22f, 0.12f, -0.42f), 30f);
            PlaceSynty(trees[1], new Vector3(W + 22f, 0.12f, -0.42f), 130f);

            // Rooftop clutter on OUR roof — cover for the roof route (skylight at x 6.5-7.5, z 10-11)
            PlaceSynty("SM_Prop_Vents_Straight_01", new Vector3(3.5f, H + 0.1f, 6f), 20f);
            PlaceSynty("SM_Prop_Vents_Corner_01", new Vector3(10.5f, H + 0.1f, 13f));
            PlaceSynty("SM_Prop_Vents_Exhaust_01", new Vector3(11.5f, H + 0.1f, 4f), 90f);
            if (PlaceSynty("SM_Prop_Aircon_01", new Vector3(4.5f, H + 0.1f, 14.5f), 45f) == null)
                PlaceSynty("SM_Prop_AirCon_01", new Vector3(4.5f, H + 0.1f, 14.5f), 45f);

            // Security cameras stay on the service routes; the front camera used to
            // puncture the sign fascia and made the rebuilt facade look unfinished.
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

        static void BackdropRow(Transform parent, string[] prefabs, bool alongX, float fixedCoord, float from, float to,
            float yaw, float hMin, float hMax, float gapMin = 1.5f, float gapMax = 5f,
            bool dressShopfront = false, bool emissiveWindows = false, float facadeEdge = float.NaN)
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

                    // Prefab pivots are not facade pivots. Align the renderer edge
                    // explicitly so apartment geometry cannot consume the sidewalk.
                    if (!float.IsNaN(facadeEdge))
                    {
                        bb = GeometryBounds(b);
                        if (Mathf.Approximately(yaw, 180f))
                            b.transform.position += Vector3.forward * (facadeEdge - bb.min.z);
                        else if (Mathf.Approximately(yaw, 270f))
                            b.transform.position += Vector3.right * (facadeEdge - bb.min.x);
                        else if (Mathf.Approximately(yaw, 90f))
                            b.transform.position += Vector3.right * (facadeEdge - bb.max.x);
                    }
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
                // Near (prefab) buildings DO cast moon shadows — shadow shapes are what
                // make the light feel real. Only the far procedural towers skip them.
                if (!usePrefabs) DisableShadows(b);
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
            // warm, just over bloom threshold — windows halo softly instead of glowing flat
            if (mat.HasProperty("_Emission_Color")) mat.SetColor("_Emission_Color", new Color(1.35f, 1.1f, 0.72f));
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
            // Trilight gives upward faces cold moon fill while undersides and alley
            // recesses stay dark. Flat ambient was the main source of the cardboard look.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.085f, 0.105f, 0.17f);
            RenderSettings.ambientEquatorColor = new Color(0.042f, 0.052f, 0.09f);
            RenderSettings.ambientGroundColor = new Color(0.016f, 0.019f, 0.032f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.34f;
            RenderSettings.reflectionBounces = 1;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(root, false);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            moon.color = new Color(0.55f, 0.66f, 1f);
            moon.intensity = 0.86f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.82f;

            // Real night sky: stars, painted moon, warm city-glow at the horizon
            string skyPath = "Assets/Materials/Greybox/NightSkyPano.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetTexture("_MainTex", TexNightSky);
            sky.SetFloat("_Exposure", 0.8f);
            RenderSettings.skybox = sky;
            RenderSettings.sun = moon;

            // Blue urban haze separates foreground, block, and skyline into readable
            // depth layers without putting translucent cone geometry in the scene.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.095f, 0.115f, 0.18f);
            RenderSettings.fogDensity = 0.038f;

            BuildAtmosphere();

            // Harold's lamp glow spilling out of the back room — his "location beacon"
            PointLight("BackRoomLamp", new Vector3(2.5f, 2.1f, D + T + 4f - 0.6f), new Color(1f, 0.62f, 0.32f), 1.6f, 6f);
            PointLight("FridgeGlow", new Vector3(1.8f, 1.9f, (D + 0f) / 2f), new Color(0.65f, 0.85f, 1f), 1.4f, 5f);
            PointLight("VentGlow", new Vector3(W - 0.25f, 0.45f, 1.25f), new Color(0.4f, 1f, 0.5f), 0.8f, 2f);
            // Overnight safety lights — the diegetic excuse for a readable interior
            PointLight("ShopNightLight_A", new Vector3(4.5f, 2.7f, 5.5f), new Color(0.55f, 0.68f, 0.72f), 0.5f, 8f);
            PointLight("ShopNightLight_B", new Vector3(10f, 2.7f, 11f), new Color(0.55f, 0.68f, 0.72f), 0.5f, 8f);
            // Bare bulb in the storage room — dim, warm, horror-pantry mood, slight waver
            var bulb = PointLight("StorageBulb", new Vector3((StorageX0 + StorageX1) / 2f, 2.5f, D + 2.5f), new Color(1f, 0.72f, 0.42f), 1.3f, 6f);
            var bulbFlicker = bulb.gameObject.AddComponent<FlickeringLight>();
            bulbFlicker.flickerAmount = 0.18f;
            bulbFlicker.dropoutChance = 0.03f;
            // Outside: dying fluorescent over the dumpster, warm glow from the den hole.
            // A shadowed spot makes the alley alternate between safe pools and darkness.
            SpotLight("AlleyLight", new Vector3(9.6f, 2.45f, StorageZ1 + T + 0.95f),
                new Vector3(0f, -0.78f, 1f), new Color(1f, 0.62f, 0.30f), 5.6f, 7.5f, 72f, true);
            PointLight("DenGlow", new Vector3(6.9f, 0.35f, OutD1 - 0.4f), new Color(1f, 0.62f, 0.3f), 1f, 3f);
            PointLight("AlleyAmbientFill", new Vector3(7f, 1.1f, 23f), new Color(0.32f, 0.43f, 0.78f), 2.35f, 11.5f);
            PointLight("AlleyWarmBounce", new Vector3(11.1f, 0.9f, 21.8f), new Color(0.86f, 0.38f, 0.17f), 0.72f, 6.5f);

            SpotLight("PassageSecurityLight", new Vector3(W + 0.72f, 2.65f, 7.2f),
                new Vector3(1f, -0.9f, 0.08f), new Color(0.48f, 0.70f, 1f), 6.4f, 9f, 74f, true);
            SpotLight("PassageUtilityLight_N", new Vector3(W + 0.72f, 2.35f, 16.2f),
                new Vector3(1f, -0.92f, -0.12f), new Color(0.40f, 0.62f, 1f), 5.2f, 8f, 70f, false);
            PointLight("PassageAmbientFill", new Vector3(16.5f, 1.1f, 11f), new Color(0.30f, 0.44f, 0.80f), 2.4f, 11.5f);
            PointLight("PassageWarmBounce", new Vector3(15.3f, 0.9f, 16.5f), new Color(0.78f, 0.36f, 0.18f), 0.66f, 6.5f);

            SpotLight("AlleySecondaryLight", new Vector3(2.2f, 2.42f, D + T + 4f + T + 0.95f),
                new Vector3(0f, -0.82f, 1f), new Color(1f, 0.54f, 0.24f), 5f, 7f, 68f, false);

            // The entrance pool now originates at the visible wall fixture instead
            // of floating above the street and blasting through the whole facade.
            SpotLight("EntranceCanopyPool", new Vector3(EntranceX, 2.10f, -0.53f),
                new Vector3(0f, -0.90f, -0.28f), new Color(1f, 0.62f, 0.28f), 4.2f, 6.5f, 68f, true);
            PointLight("StreetAmbientFill", new Vector3(7f, 1.25f, -4.5f), new Color(0.25f, 0.34f, 0.62f), 0.42f, 10f);
            PointLight("EntranceVestibuleGlow", new Vector3(EntranceX, 1.45f, 0.35f), new Color(1f, 0.55f, 0.24f), 1.25f, 4.2f);
            PointLight("WindowDisplayWarm", new Vector3(3.55f, 1.55f, 0.55f), new Color(1f, 0.52f, 0.25f), 1.1f, 4.2f);
            PointLight("WindowDisplayCool", new Vector3(10.45f, 1.55f, 0.55f), new Color(0.25f, 0.55f, 1f), 0.85f, 4.2f);

            // Local glow around the signs lets their colour touch nearby masonry and
            // wet pavement; small ranges keep the palette intentional.
            PointLight("OpenSignGlow", new Vector3(2.05f, 1.35f, -0.18f), new Color(0.15f, 0.75f, 1f), 0.8f, 3.3f);
            PointLight("RaccoonHeistSignGlow", new Vector3(7f, 2.55f, -0.42f), new Color(0.12f, 0.72f, 1f), 0.9f, 5f);
            PointLight("WestNeonGlow", new Vector3(-12f, 1.5f, OutD0 - 0.7f), new Color(0.95f, 0.12f, 0.72f), 0.65f, 3.4f);
            PointLight("EastNeonGlow", new Vector3(W + 24f, 1.5f, OutD0 - 0.7f), new Color(0.12f, 0.72f, 1f), 0.6f, 3.4f);
            PointLight("RearNeonGlow", new Vector3(7.2f, 1.5f, OutD1 + 11.3f), new Color(0.8f, 0.14f, 1f), 0.55f, 3.8f);

            // Soft, stable reflected city glow connects the perimeter lamp pools. These fills are
            // intentionally outside the storefront zone and never flicker, keeping
            // the night exposure stable while the surrounding block remains legible.
            PointLight("PerimeterAmbient_Rear", new Vector3(7f, 1.2f, OutD1 + 3.5f), new Color(0.20f, 0.31f, 0.60f), 0.66f, 10f);
            PointLight("PerimeterAmbient_East", new Vector3(OutW1 + 3.5f, 1.2f, 11f), new Color(0.18f, 0.30f, 0.62f), 0.64f, 10f);
            PointLight("PerimeterAmbient_West", new Vector3(OutW0 - 3.5f, 1.2f, 12f), new Color(0.24f, 0.28f, 0.54f), 0.58f, 9f);
            PointLight("PerimeterAmbient_FarWalk", new Vector3(7f, 1.2f, OutD0 + 0.7f), new Color(0.22f, 0.30f, 0.56f), 0.56f, 9f);

            // Low, stable bounce on the opposite furniture lanes prevents props from
            // vanishing between the stronger pools cast by the streetlights.
            PointLight("RearFarWalkFill_W", new Vector3(-8f, 1.35f, OutD1 + 12f), new Color(0.22f, 0.34f, 0.68f), 0.9f, 9f);
            PointLight("RearFarWalkFill_E", new Vector3(20f, 1.35f, OutD1 + 12f), new Color(0.25f, 0.36f, 0.68f), 0.9f, 9f);
            PointLight("EastFarWalkFill_S", new Vector3(OutW1 + 12f, 1.35f, 4f), new Color(0.20f, 0.34f, 0.70f), 0.9f, 9f);
            PointLight("EastFarWalkFill_N", new Vector3(OutW1 + 12f, 1.35f, 25f), new Color(0.24f, 0.34f, 0.66f), 0.9f, 9f);
            PointLight("WestFarWalkFill_S", new Vector3(OutW0 - 12f, 1.35f, 4f), new Color(0.27f, 0.31f, 0.62f), 0.85f, 9f);
            PointLight("WestFarWalkFill_N", new Vector3(OutW0 - 12f, 1.35f, 25f), new Color(0.23f, 0.34f, 0.67f), 0.85f, 9f);
        }

        static void BuildEnvironmentAudio()
        {
            const string ambiencePath = "Assets/Audio/Environment/city_ambience.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ambiencePath);
            if (clip == null)
            {
                Debug.LogWarning($"Raccoon Heist: exterior ambience clip is missing at {ambiencePath}.");
                return;
            }

            var ambience = new GameObject("ExteriorCityAmbience");
            ambience.transform.SetParent(root, false);
            var source = ambience.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0.55f;
            source.spatialBlend = 0f; // A city bed surrounds the listener; it is not one point in the street.
            source.dopplerLevel = 0f;
            source.priority = 96;

            // These are the walkable interior volumes, inset slightly from their walls.
            // The roof and every exterior route remain outside the zones and keep the city audible.
            var interiorZones = new[]
            {
                new Bounds(new Vector3(W * 0.5f, 1.38f, D * 0.5f), new Vector3(W - 0.1f, 3.55f, D - 0.1f)),
                new Bounds(new Vector3(1.5f, 1.38f, D + T + ShopConstants.BackRoomDepth * 0.5f),
                    new Vector3(2.9f, 3.55f, ShopConstants.BackRoomDepth - 0.1f)),
                new Bounds(new Vector3((StorageX0 + StorageX1) * 0.5f, 1.38f, D + T + ShopConstants.StorageDepth * 0.5f),
                    new Vector3(ShopConstants.StorageWidth - 0.4f, 3.55f, ShopConstants.StorageDepth - 0.1f))
            };
            var controller = ambience.AddComponent<ExteriorAmbienceController>();
            controller.Configure(0.55f, 0.8f, interiorZones);

            bool outsideAudible = !controller.IsInteriorPosition(new Vector3(EntranceX, 0.35f, -2f));
            bool shopMuted = controller.IsInteriorPosition(new Vector3(EntranceX, 0.35f, 2f));
            bool backRoomMuted = controller.IsInteriorPosition(new Vector3(1.5f, 0.35f, D + T + 2f));
            bool storageMuted = controller.IsInteriorPosition(new Vector3(8f, 0.35f, D + T + 2.5f));
            if (outsideAudible && shopMuted && backRoomMuted && storageMuted)
                Debug.Log("Raccoon Heist: exterior ambience threshold check passed (outside audible; all interiors muted).");
            else
                Debug.LogWarning("Raccoon Heist: exterior ambience threshold check failed; inspect configured interior bounds.");
        }

        static void BuildShopAlarm()
        {
            const string alarmPath = "Assets/Audio/Environment/security_alarm.mp3";
            var alarmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(alarmPath);
            if (alarmClip == null)
                Debug.LogWarning($"Raccoon Heist: storefront alarm clip is missing at {alarmPath}.");

            var alarmSystem = new GameObject("ShopAlarmSystem");
            alarmSystem.transform.SetParent(root, false);

            var alarmSpeaker = new GameObject("AlarmSpeaker");
            alarmSpeaker.transform.SetParent(alarmSystem.transform, false);
            alarmSpeaker.transform.localPosition = new Vector3(EntranceX, 2.65f, D * 0.5f);
            var alarmSource = alarmSpeaker.AddComponent<AudioSource>();
            alarmSource.clip = alarmClip;
            alarmSource.loop = true;
            alarmSource.playOnAwake = false;
            alarmSource.volume = 0.85f;
            alarmSource.spatialBlend = 0.6f;
            alarmSource.minDistance = 2f;
            alarmSource.maxDistance = 32f;
            alarmSource.rolloffMode = AudioRolloffMode.Linear;
            alarmSource.dopplerLevel = 0f;
            alarmSource.priority = 72;

            var beaconRotors = new[]
            {
                CreateAlarmBeacon(alarmSystem.transform, "AlarmBeacon_Front", new Vector3(3.1f, 2.70f, 4f), 20f),
                CreateAlarmBeacon(alarmSystem.transform, "AlarmBeacon_Rear", new Vector3(10.9f, 2.70f, 12f), 200f)
            };

            var door = root.Find("EntranceDoorPivot")?.GetComponent<HingedDoor>();
            if (door == null)
                Debug.LogWarning("Raccoon Heist: storefront alarm could not find EntranceDoorPivot/HingedDoor.");
            alarmSystem.AddComponent<ShopAlarmController>().Configure(door, alarmSource, beaconRotors);
        }

        static Transform CreateAlarmBeacon(Transform parent, string name, Vector3 localPosition, float initialYaw)
        {
            var beacon = new GameObject(name).transform;
            beacon.SetParent(parent, false);
            beacon.localPosition = localPosition;

            var housingMaterial = Mat("AlarmBeaconHousing", new Color(0.09f, 0.10f, 0.12f), 0.28f);
            var reflectorMaterial = Mat("AlarmBeaconReflector", new Color(0.56f, 0.58f, 0.62f), 0.88f);
            var redMaterial = TransparentMat("AlarmBeaconRed", new Color(0.46f, 0.008f, 0.004f, 0.46f), 0.74f);
            redMaterial.EnableKeyword("_EMISSION");
            redMaterial.SetColor("_EmissionColor", new Color(1.15f, 0.012f, 0.006f));
            redMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var hotLensMaterial = EmissiveMat("AlarmBeaconHotLens", new Color(1f, 0.055f, 0.025f),
                new Color(18f, 0.12f, 0.035f));

            var ceilingMount = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ceilingMount.name = "CeilingMount";
            ceilingMount.transform.SetParent(beacon, false);
            ceilingMount.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ceilingMount.transform.localScale = new Vector3(0.24f, 0.06f, 0.24f);
            ApplyMat(ceilingMount, housingMaterial);
            Object.DestroyImmediate(ceilingMount.GetComponent<Collider>());

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "BeaconRim";
            rim.transform.SetParent(beacon, false);
            rim.transform.localScale = new Vector3(0.22f, 0.025f, 0.22f);
            ApplyMat(rim, housingMaterial);
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(beacon, false);
            rotor.localRotation = Quaternion.Euler(0f, initialYaw, 0f);

            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "RedBeaconDome";
            dome.transform.SetParent(rotor, false);
            dome.transform.localPosition = new Vector3(0f, -0.09f, 0f);
            dome.transform.localScale = new Vector3(0.23f, 0.14f, 0.23f);
            ApplyMat(dome, redMaterial);
            Object.DestroyImmediate(dome.GetComponent<Collider>());

            // The dome is rotationally symmetrical, so rotating it alone reads as a
            // static red glow. These reflector faces and bright lens panels are aligned
            // with the two spotlights and turn with the rotor, making the beacon itself
            // visibly sweep in sync, even when the light pools are outside the camera view.
            var spindle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spindle.name = "ReflectorSpindle";
            spindle.transform.SetParent(rotor, false);
            spindle.transform.localPosition = new Vector3(0f, -0.09f, 0f);
            spindle.transform.localScale = new Vector3(0.025f, 0.05f, 0.025f);
            ApplyMat(spindle, housingMaterial);
            Object.DestroyImmediate(spindle.GetComponent<Collider>());

            var frontReflector = Box("Reflector_Front", new Vector3(0f, -0.09f, 0.055f),
                new Vector3(0.13f, 0.09f, 0.018f), rotor, reflectorMaterial);
            Object.DestroyImmediate(frontReflector.GetComponent<Collider>());
            var rearReflector = Box("Reflector_Rear", new Vector3(0f, -0.09f, -0.055f),
                new Vector3(0.13f, 0.09f, 0.018f), rotor, reflectorMaterial);
            Object.DestroyImmediate(rearReflector.GetComponent<Collider>());

            var frontLens = Box("HotLens_Front", new Vector3(0f, -0.09f, 0.116f),
                new Vector3(0.105f, 0.078f, 0.018f), rotor, hotLensMaterial);
            Object.DestroyImmediate(frontLens.GetComponent<Collider>());
            var rearLens = Box("HotLens_Rear", new Vector3(0f, -0.09f, -0.116f),
                new Vector3(0.105f, 0.078f, 0.018f), rotor, hotLensMaterial);
            Object.DestroyImmediate(rearLens.GetComponent<Collider>());

            var beam = new GameObject("RotatingRedBeam").AddComponent<Light>();
            beam.transform.SetParent(rotor, false);
            beam.transform.localPosition = new Vector3(0f, -0.08f, 0.08f);
            beam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.18f, 1f));
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.04f, 0.02f);
            beam.intensity = 18f;
            beam.range = 12f;
            beam.spotAngle = 50f;
            beam.innerSpotAngle = 18f;
            beam.shadows = LightShadows.None;

            var reverseBeam = new GameObject("RotatingRedBeam_Reverse").AddComponent<Light>();
            reverseBeam.transform.SetParent(rotor, false);
            reverseBeam.transform.localPosition = new Vector3(0f, -0.08f, -0.08f);
            reverseBeam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.18f, -1f));
            reverseBeam.type = LightType.Spot;
            reverseBeam.color = new Color(1f, 0.025f, 0.012f);
            reverseBeam.intensity = 14f;
            reverseBeam.range = 10f;
            reverseBeam.spotAngle = 44f;
            reverseBeam.innerSpotAngle = 16f;
            reverseBeam.shadows = LightShadows.None;

            var localGlow = new GameObject("RedBeaconGlow").AddComponent<Light>();
            localGlow.transform.SetParent(rotor, false);
            localGlow.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            localGlow.type = LightType.Point;
            localGlow.color = new Color(1f, 0.025f, 0.012f);
            localGlow.intensity = 4.8f;
            localGlow.range = 5.5f;
            localGlow.shadows = LightShadows.None;

            rotor.gameObject.SetActive(false);
            return rotor;
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

        static Light SpotLight(string name, Vector3 pos, Vector3 direction, Color color, float intensity, float range, float angle, bool shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Spot;
            light.transform.position = pos;
            light.transform.rotation = Quaternion.LookRotation(direction.normalized);
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.48f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return light;
        }

        // Procedural night sky: gradient with city light-pollution at the horizon,
        // stars that thin out low, drifting cloud noise, and a painted moon
        static Texture2D TexNightSky => EnsureTex("tex_nightsky_v3", (x, y) =>
        {
            float v = y / 511f;
            float alt = Mathf.Clamp01((v - 0.5f) * 2f);
            var horizon = new Color(0.21f, 0.15f, 0.12f);
            var zenith = new Color(0.012f, 0.022f, 0.062f);
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
            bloom.intensity.Override(0.82f);
            bloom.threshold.Override(0.82f);
            bloom.scatter.Override(0.72f);
            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>();
            color.postExposure.Override(0.55f);
            color.contrast.Override(14f);
            color.saturation.Override(-9f);
            var wb = Get<WhiteBalance>();
            wb.temperature.Override(-14f);
            var vig = Get<Vignette>();
            vig.intensity.Override(0.19f);
            vig.smoothness.Override(0.52f);
            var grain = Get<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Own exactly one active global volume. Reusing the template scene's first
            // global volume made edit/play transitions dependent on discovery order and
            // could briefly show the scene with a completely different exposure.
            foreach (var existing in Object.FindObjectsByType<Volume>())
                if (existing.isGlobal) existing.enabled = false;
            var go = new GameObject("NightVolume");
            go.transform.SetParent(root, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            vol.weight = 1f;
            vol.blendDistance = 0f;
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
