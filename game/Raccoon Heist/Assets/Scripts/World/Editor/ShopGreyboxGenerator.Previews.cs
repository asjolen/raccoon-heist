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
    // Scene preview capture: renders review angles of every route for validation runs.
    public static partial class ShopGreyboxGenerator
    {
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
                // Editor cameras do not advance particle systems on their own. The
                // focused smoke is now deliberately slow, so a short seed shows its
                // direction and density without turning it into a frozen jet.
                foreach (var particles in Object.FindObjectsByType<ParticleSystem>())
                {
                    float previewTime = particles.name.Contains("Haze") ? 6f
                        : particles.name.Contains("Steam") ? 8f
                        : 3f;
                    particles.Simulate(previewTime, true, true, true);
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
                CapturePreview("HaroldBackRoom", new Vector3(1.5f, 1.15f, 15.25f), new Vector3(2.15f, 0.95f, 17.55f));
                CapturePreview("Passage", new Vector3(15.15f, 0.75f, 12.1f), new Vector3(15.65f, 1.05f, 5.2f));
                CapturePreview("EastQuietCrawl", new Vector3(15.35f, 0.36f, 1.38f), new Vector3(14.02f, 0.22f, 1.38f));
                CapturePreview("PassageValve", new Vector3(17.65f, 1.32f, 12.75f), new Vector3(14.35f, 1.30f, 12.75f));
                CapturePreview("Alley", new Vector3(11.7f, 0.82f, 25f), new Vector3(8.2f, 1.2f, 19.7f));
                CapturePreview("RearBreakInCrawl", new Vector3(6.63f, 0.55f, 23.2f), new Vector3(6.63f, 0.22f, StorageZ1 + T));
                CapturePreview("BackWindowExterior", new Vector3(8.6f, 1.72f, 24.2f), new Vector3(8.6f, 1.9f, StorageZ1 + T));
                CapturePreview("BackWindowInterior", new Vector3(8.6f, 1.3f, 18.5f), new Vector3(8.6f, 1.9f, StorageZ1 + T));
                CapturePreview("BackBoundaryDen", new Vector3(6.9f, 0.72f, 23.15f), new Vector3(6.9f, 0.9f, OutD1 + 0.15f));
                CapturePreview("FacadeLampAlignment", new Vector3(10.8f, 2.15f, 23.55f), new Vector3(9.6f, 2.25f, StorageZ1 + T + 0.35f));
                CapturePreview("LStreetLampAlignment", new Vector3(-8.1f, 5.75f, -9.7f), new Vector3(-10f, 5.95f, -13.25f));
                CapturePreview("AlleyStoryWall", new Vector3(7.45f, 1.42f, 24.7f), new Vector3(7.45f, 1.35f, StorageZ1 + T));
                CapturePreview("StorageCrawlBypass", new Vector3(10f, 0.38f, 14.75f), new Vector3(10f, 0.22f, D + T));
                CapturePreview("WestStreet", new Vector3(-1.2f, 8f, -18f), new Vector3(-10.5f, 0.8f, 9f));
                CapturePreview("EastStreet", new Vector3(15.2f, 7.5f, -3.5f), new Vector3(24.5f, 0.8f, 9f));
                CapturePreview("RearStreet", new Vector3(7f, 7.5f, 18f), new Vector3(7f, 0.8f, 30f));
                CapturePreview("WestOppositeWalk", new Vector3(-10.5f, 1.25f, 8f), new Vector3(-17.5f, 1.1f, 8f));
                CapturePreview("EastOppositeWalk", new Vector3(24.5f, 1.25f, 10.5f), new Vector3(31.5f, 1.1f, 10.5f));
                CapturePreview("RearOppositeWalk", new Vector3(7f, 1.25f, 30.5f), new Vector3(7f, 1.1f, 37.5f));
                CapturePreview("FrontWestIntersection", new Vector3(-10.5f, 5.2f, -0.8f), new Vector3(-10.5f, 0.7f, -7f));
                CapturePreview("FrontEastIntersection", new Vector3(24.5f, 5.2f, -0.8f), new Vector3(24.5f, 0.7f, -7f));
                CapturePreview("RearWestIntersection", new Vector3(-10.5f, 5.2f, 23.8f), new Vector3(-10.5f, 0.7f, 30.5f));
                CapturePreview("RearEastIntersection", new Vector3(24.5f, 5.2f, 23.8f), new Vector3(24.5f, 0.7f, 30.5f));
                CapturePreview("FrontSteam", new Vector3(8.6f, 1.35f, -5.6f), new Vector3(5.5f, 0.75f, -3.2f));
                CapturePreview("RoofSmoke", new Vector3(16f, 8f, -1f), new Vector3(11.5f, 4.6f, 4f));
                CapturePreview("Block", new Vector3(32f, 27f, -22f), new Vector3(7f, 0.8f, 10f));
            }
            finally
            {
                if (raccoonWasActive) raccoon.SetActive(true);
            }
            Debug.Log("Raccoon Heist: captured environment previews to /tmp/RaccoonHeist_*.png.");
        }

        [MenuItem("Raccoon Heist/Capture Harold Walking Preview")]
        public static void CaptureHaroldWalkingPreview()
        {
            var harold = GameObject.Find("Harold");
            var animator = harold != null ? harold.GetComponentInChildren<Animator>(true) : null;
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Models/Harold/CorrectedClips/Harold_Walk.anim");
            if (harold == null || animator == null || walk == null)
            {
                Debug.LogError("Raccoon Heist: generate the shop and corrected Harold clips before capturing his walk.");
                return;
            }

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, walk, walk.length * 0.36f);
                AnimationMode.EndSampling();
                CapturePreview("HaroldWalking", new Vector3(1.5f, 1.15f, 15.25f),
                    new Vector3(2.15f, 0.95f, 17.55f));
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            }
            Debug.Log("Raccoon Heist: captured sampled Harold walk to /tmp/RaccoonHeist_HaroldWalking.png.");
        }

        [MenuItem("Raccoon Heist/Capture 32-Angle Perimeter Sweep")]
        public static void CapturePerimeterSweep()
        {
            var sceneRoot = GameObject.Find("ShopGreybox");
            if (sceneRoot == null)
            {
                Debug.LogWarning("Raccoon Heist: generate the shop before capturing the perimeter sweep.");
                return;
            }

            var raccoon = GameObject.Find("Raccoon");
            bool raccoonWasActive = raccoon != null && raccoon.activeSelf;
            if (raccoonWasActive) raccoon.SetActive(false);
            try
            {
                var streetPositions = new List<Vector3>();
                foreach (float x in new[] { -29f, -10f, 7f, 24f, 45f })
                    streetPositions.Add(new Vector3(x, 2.65f, -7f));
                foreach (float z in new[] { -29f, -7f, 10f, 30f, 47f })
                    streetPositions.Add(new Vector3(OutW1 + 5.5f, 2.65f, z));
                foreach (float x in new[] { 45f, 24f, 7f, -10f, -29f })
                    streetPositions.Add(new Vector3(x, 2.65f, OutD1 + 5.5f));
                foreach (float z in new[] { 47f, 30f, 10f, -7f, -29f })
                    streetPositions.Add(new Vector3(OutW0 - 5.5f, 2.65f, z));

                var blockCentre = new Vector3(W * 0.5f, 1.15f, 8f);
                for (int i = 0; i < streetPositions.Count; i++)
                    CapturePreview($"SweepStreet_{i:00}", streetPositions[i], blockCentre);

                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 30f * Mathf.Deg2Rad;
                    var position = blockCentre + new Vector3(Mathf.Sin(angle) * 60f, 42f, Mathf.Cos(angle) * 60f);
                    CapturePreview($"SweepHigh_{i:00}", position, new Vector3(W * 0.5f, 1f, 8f));
                }
            }
            finally
            {
                if (raccoonWasActive) raccoon.SetActive(true);
            }

            Debug.Log("Raccoon Heist: captured 32-angle perimeter sweep to /tmp/RaccoonHeist_Sweep*.png.");
        }

        [MenuItem("Raccoon Heist/Capture Perimeter Sightline Audit")]
        public static void CapturePerimeterSightlineAudit()
        {
            var sceneRoot = GameObject.Find("ShopGreybox");
            if (sceneRoot == null)
            {
                Debug.LogWarning("Raccoon Heist: generate the shop before capturing the sightline audit.");
                return;
            }

            var raccoon = GameObject.Find("Raccoon");
            bool raccoonWasActive = raccoon != null && raccoon.activeSelf;
            if (raccoonWasActive) raccoon.SetActive(false);
            try
            {
                CapturePreview("Sightline_MainWest", new Vector3(7f, 1.35f, -7f), new Vector3(CityWestCapFacade, 2f, -7f));
                CapturePreview("Sightline_MainEast", new Vector3(7f, 1.35f, -7f), new Vector3(CityEastCapFacade, 2f, -7f));
                CapturePreview("Sightline_WestNorth", new Vector3(OutW0 - 5.5f, 1.35f, 8f), new Vector3(OutW0 - 5.5f, 2f, CityNorthCapFacade));
                CapturePreview("Sightline_WestSouth", new Vector3(OutW0 - 5.5f, 1.35f, 8f), new Vector3(OutW0 - 5.5f, 2f, CitySouthCapFacade));
                CapturePreview("Sightline_EastNorth", new Vector3(OutW1 + 5.5f, 1.35f, 10f), new Vector3(OutW1 + 5.5f, 2f, CityNorthCapFacade));
                CapturePreview("Sightline_EastSouth", new Vector3(OutW1 + 5.5f, 1.35f, 10f), new Vector3(OutW1 + 5.5f, 2f, CitySouthCapFacade));
                CapturePreview("Sightline_RearWest", new Vector3(7f, 1.35f, OutD1 + 5.5f), new Vector3(CityWestCapFacade, 2f, OutD1 + 5.5f));
                CapturePreview("Sightline_RearEast", new Vector3(7f, 1.35f, OutD1 + 5.5f), new Vector3(CityEastCapFacade, 2f, OutD1 + 5.5f));

                var roof = new Vector3(7f, H + 1.35f, 10f);
                CapturePreview("Sightline_RoofNorth", roof, new Vector3(7f, 7f, CityNorthCapFacade));
                CapturePreview("Sightline_RoofSouth", roof, new Vector3(7f, 7f, CitySouthCapFacade));
                CapturePreview("Sightline_RoofWest", roof, new Vector3(CityWestCapFacade, 7f, 10f));
                CapturePreview("Sightline_RoofEast", roof, new Vector3(CityEastCapFacade, 7f, 10f));
            }
            finally
            {
                if (raccoonWasActive) raccoon.SetActive(true);
            }

            Debug.Log("Raccoon Heist: captured 12-direction sightline audit to /tmp/RaccoonHeist_Sightline_*.png.");
        }

        [MenuItem("Raccoon Heist/Capture Synty City Reference Views")]
        public static void CaptureSyntyCityReferenceViews()
        {
            string returnScene = SceneManager.GetActiveScene().path;
            try
            {
                CaptureSyntyReferenceScene(
                    "SyntyCityDemo",
                    "Assets/Synty/PolygonCity/Scenes/Demo.unity");
                CaptureSyntyReferenceScene(
                    "SyntyCityOverview",
                    "Assets/Synty/PolygonCity/Scenes/Overview.unity");
            }
            finally
            {
                if (!string.IsNullOrEmpty(returnScene))
                    EditorSceneManager.OpenScene(returnScene, OpenSceneMode.Single);
            }

            Debug.Log("Raccoon Heist: captured Synty reference views to /tmp/RaccoonHeist_SyntyCity*.png.");
        }

        [MenuItem("Raccoon Heist/Capture Synty Building Catalog")]
        public static void CaptureSyntyBuildingCatalog()
        {
            string returnScene = SceneManager.GetActiveScene().path;
            var candidates = new[]
            {
                "SM_Bld_Shop_01",
                "SM_Bld_Shop_03",
                "SM_Bld_Shop_Corner_01",
                "SM_Bld_Apartment_Stack_01",
                "SM_Bld_Apartment_Corner_01",
                "SM_Bld_Apartment_Door_Corner_01",
                "SM_Bld_OfficeOld_Small_01",
                "SM_Bld_OfficeOld_Large_01",
                "SM_Bld_OfficeSquare_01",
                "SM_Bld_OfficeRound_01",
                "SM_Bld_OfficeOctagon_01",
                "SM_Bld_CityHall_01",
                "SM_Bld_Station_01",
                "SM_Bld_Station_02",
                "SM_Bld_Station_03",
                "SM_Env_SubwayEntrance_01",
                "SM_Env_SubwayEntrance_02"
            };

            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);

                var key = new GameObject("CatalogKey").AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.35f;
                key.color = new Color(1f, 0.91f, 0.78f);
                key.transform.rotation = Quaternion.Euler(38f, -42f, 0f);

                foreach (string prefabName in candidates)
                {
                    var prefab = SyntyPrefab(prefabName);
                    if (prefab == null) continue;
                    bool inspectAllSides = prefabName.Contains("Apartment_")
                        || prefabName.Contains("Shop_");
                    int viewCount = inspectAllSides ? 4 : 1;
                    for (int view = 0; view < viewCount; view++)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        instance.transform.SetPositionAndRotation(
                            Vector3.zero,
                            Quaternion.Euler(0f, view * 90f, 0f));

                        var bounds = GeometryBounds(instance);
                        var holder = new GameObject($"CatalogCamera_{prefabName}");
                        var camera = holder.AddComponent<Camera>();
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(0.055f, 0.065f, 0.09f);
                        camera.fieldOfView = 38f;
                        camera.nearClipPlane = 0.03f;
                        camera.farClipPlane = 250f;
                        float radius = Mathf.Max(3f, bounds.extents.magnitude);
                        Vector3 viewDirection = new Vector3(1f, 0.58f, -1f).normalized;
                        Vector3 cameraPosition = bounds.center + viewDirection * radius * 2.9f;
                        holder.transform.SetPositionAndRotation(
                            cameraPosition,
                            Quaternion.LookRotation(bounds.center - cameraPosition));

                        CaptureCamera($"Catalog_{prefabName}_R{view * 90:000}", camera);
                        Object.DestroyImmediate(holder);
                        Object.DestroyImmediate(instance);
                    }
                }

                Object.DestroyImmediate(key.gameObject);
            }
            finally
            {
                if (!string.IsNullOrEmpty(returnScene))
                    EditorSceneManager.OpenScene(returnScene, OpenSceneMode.Single);
            }

            Debug.Log("Raccoon Heist: captured Synty building catalog to /tmp/RaccoonHeist_Catalog_*.png.");
        }

        static void CaptureSyntyReferenceScene(string captureName, string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (camera == null)
            {
                Debug.LogWarning($"Raccoon Heist: no camera found in Synty reference scene '{scenePath}'.");
                return;
            }

            CaptureCamera(captureName, camera);
        }

        static void CapturePreview(string name, Vector3 position, Vector3 target)
        {
            var holder = new GameObject($"PreviewCamera_{name}");
            var camera = holder.AddComponent<Camera>();
            holder.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = name == "Block" || name.Contains("High") ? 54f : 68f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 180f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            CaptureCamera(name, camera);
            Object.DestroyImmediate(holder);
        }

        static void CaptureCamera(string name, Camera camera)
        {
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
        }

        static void ValidateVehicleBuildingClearance()
        {
            var vehicles = new List<GameObject>();
            var buildings = new List<GameObject>();
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == root) continue;
                bool directEnvironmentChild = candidate.parent == root || candidate.parent.name == "Backdrop";

                string name = candidate.name;
                if (directEnvironmentChild && name.StartsWith("SM_Veh_"))
                    vehicles.Add(candidate.gameObject);
                else if (IsEnvironmentBuildingRoot(candidate))
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
            foreach (var candidate in backdrop.GetComponentsInChildren<Transform>(true))
            {
                bool directBackdropChild = candidate.parent == backdrop;
                if (directBackdropChild
                    && (candidate.name.StartsWith("SM_Prop_") || candidate.name.Contains("FarLamp_")))
                    props.Add(candidate.gameObject);
                else if (IsEnvironmentBuildingRoot(candidate))
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
                    Debug.LogWarning(
                        $"Environment overlap: prop '{prop.name}' at {prop.transform.position} intersects building '{building.name}' "
                        + $"(prop xz {propBounds.min.x:0.00}..{propBounds.max.x:0.00}, "
                        + $"{propBounds.min.z:0.00}..{propBounds.max.z:0.00}; building xz "
                        + $"{buildingBounds.min.x:0.00}..{buildingBounds.max.x:0.00}, "
                        + $"{buildingBounds.min.z:0.00}..{buildingBounds.max.z:0.00}).");
                }
            }

            if (overlaps == 0)
                Debug.Log($"Raccoon Heist: backdrop prop/building clearance check passed ({props.Count} props, {buildings.Count} buildings).");
        }

        static void ValidateRoadBuildingClearance()
        {
            var roads = new List<GameObject>();
            var buildings = new List<GameObject>();
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == root) continue;
                bool directEnvironmentChild = candidate.parent == root || candidate.parent.name == "Backdrop";

                if (directEnvironmentChild
                    && (candidate.name == "Road" || candidate.name.StartsWith("CityRoad_")))
                    roads.Add(candidate.gameObject);
                else if (IsEnvironmentBuildingRoot(candidate))
                    buildings.Add(candidate.gameObject);
            }

            int overlaps = 0;
            foreach (var roadObject in roads)
            {
                var roadBounds = GeometryBounds(roadObject);
                foreach (var building in buildings)
                {
                    var buildingBounds = GeometryBounds(building);
                    bool overlapsX = roadBounds.min.x < buildingBounds.max.x
                        && roadBounds.max.x > buildingBounds.min.x;
                    bool overlapsZ = roadBounds.min.z < buildingBounds.max.z
                        && roadBounds.max.z > buildingBounds.min.z;
                    if (!overlapsX || !overlapsZ) continue;

                    overlaps++;
                    Debug.LogWarning(
                        $"Environment overlap: road '{roadObject.name}' enters building '{building.name}' "
                        + $"(road xz {roadBounds.min.x:0.0}..{roadBounds.max.x:0.0}, "
                        + $"{roadBounds.min.z:0.0}..{roadBounds.max.z:0.0}; building xz "
                        + $"{buildingBounds.min.x:0.0}..{buildingBounds.max.x:0.0}, "
                        + $"{buildingBounds.min.z:0.0}..{buildingBounds.max.z:0.0}).");
                }
            }

            if (overlaps == 0)
                Debug.Log($"Raccoon Heist: road/building clearance check passed ({roads.Count} roads, {buildings.Count} buildings).");
        }

        static bool IsEnvironmentBuildingRoot(Transform candidate)
        {
            string name = candidate.name;
            if (name.StartsWith("CompleteLandmark_") || name.StartsWith("CityBlockBuilding_"))
                return true;

            bool directEnvironmentChild = candidate.parent == root
                || candidate.parent != null && candidate.parent.name == "Backdrop";
            return directEnvironmentChild
                && (name == "NeighbourBuilding"
                    || name.StartsWith("BackdropBld_")
                    || name.StartsWith("SM_Bld_Shop_")
                    || name.StartsWith("SM_Bld_Apartment_")
                    || name.StartsWith("SM_Bld_Station_"));
        }

        static void ValidateStreetCanyonCaps()
        {
            var backdrop = root.Find("Backdrop");
            if (backdrop == null) return;

            string[] expectedCaps =
            {
                "StreetCap_MainWest",
                "StreetCap_MainEast",
                "StreetCap_RearWest",
                "StreetCap_RearEast",
                "StreetCap_WestNorth",
                "StreetCap_WestSouth",
                "StreetCap_EastNorth",
                "StreetCap_EastSouth"
            };

            int missing = 0;
            foreach (string cap in expectedCaps)
            {
                if (backdrop.Find(cap) != null) continue;
                missing++;
                Debug.LogWarning($"Environment sightline: required street-canyon cap '{cap}' is missing.");
            }

            if (backdrop.Find("SyntyAuthoredSkyline") == null)
            {
                missing++;
                Debug.LogWarning("Environment sightline: the authored Synty skyline is missing.");
            }

            if (missing == 0)
                Debug.Log("Raccoon Heist: all 8 street-canyon caps and the authored skyline are present.");
        }

        static void ValidateStreetFurniturePlacement()
        {
            var roads = new List<GameObject>();
            var furniture = new List<GameObject>();
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == root) continue;
                bool directEnvironmentChild = candidate.parent == root || candidate.parent.name == "Backdrop";
                if (!directEnvironmentChild) continue;

                if (candidate.name == "Road" || candidate.name.StartsWith("CityRoad_"))
                    roads.Add(candidate.gameObject);
                else if (candidate.name.StartsWith("SM_Prop_")
                         || candidate.name.StartsWith("SM_Env_Tree_")
                         || candidate.name.StartsWith("CityTree_"))
                    furniture.Add(candidate.gameObject);
            }

            var clearWalkingLanes = new List<Bounds>();
            var horizontalRuns = new[]
            {
                new Vector2(CityWestEnd, OutW0 - 9f),
                new Vector2(OutW0 - 2f, OutW1 + 2f),
                new Vector2(OutW1 + 9f, CityEastEnd)
            };
            foreach (var run in horizontalRuns)
            {
                float centre = (run.x + run.y) * 0.5f;
                float width = run.y - run.x;
                clearWalkingLanes.Add(new Bounds(new Vector3(centre, 1.5f, -13.3f), new Vector3(width, 3f, 0.8f)));
                clearWalkingLanes.Add(new Bounds(new Vector3(centre, 1.5f, OutD1 + 11.25f), new Vector3(width, 3f, 1.2f)));
            }

            var verticalRuns = new[]
            {
                new Vector2(CityNorthEnd, -12f),
                new Vector2(-2f, OutD1 + 2f),
                new Vector2(OutD1 + 9f, CitySouthEnd)
            };
            foreach (var run in verticalRuns)
            {
                float centre = (run.x + run.y) * 0.5f;
                float depth = run.y - run.x;
                clearWalkingLanes.Add(new Bounds(new Vector3(OutW1 + 11.25f, 1.5f, centre), new Vector3(1.2f, 3f, depth)));
                clearWalkingLanes.Add(new Bounds(new Vector3(OutW0 - 11.25f, 1.5f, centre), new Vector3(1.2f, 3f, depth)));
            }

            int problems = 0;
            foreach (var item in furniture)
            {
                // Flat road furniture is intentionally on asphalt.
                if (item.name.StartsWith("SM_Prop_Manhole_")
                    || item.name.StartsWith("SM_Prop_Barrier_")
                    || item.name.StartsWith("SM_Prop_Cone_")
                    || item.name.StartsWith("SM_Prop_Water_Puddle_")
                    || item.name.StartsWith("SM_Prop_Billboard_"))
                    continue;
                if (item.name.Contains("LightPole") || item.name.Contains("Streetlight")) continue;

                var itemBounds = GeometryBounds(item);
                Vector3 footprint = new Vector3(itemBounds.center.x, 0f, itemBounds.center.z);
                foreach (var roadObject in roads)
                {
                    var roadBounds = GeometryBounds(roadObject);
                    if (footprint.x <= roadBounds.min.x || footprint.x >= roadBounds.max.x
                        || footprint.z <= roadBounds.min.z || footprint.z >= roadBounds.max.z)
                        continue;
                    problems++;
                    Debug.LogWarning($"Street placement: '{item.name}' at {footprint} is centred in road '{roadObject.name}'.");
                }

                foreach (var lane in clearWalkingLanes)
                {
                    if (!lane.Contains(new Vector3(footprint.x, lane.center.y, footprint.z))) continue;
                    problems++;
                    Debug.LogWarning($"Street placement: '{item.name}' blocks a designated sidewalk walking lane at {footprint}.");
                }
            }

            if (problems == 0)
                Debug.Log($"Raccoon Heist: street furniture road/walking-lane check passed ({furniture.Count} props).");
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

        // Regeneration markers let an already-open editor own the scene safely;
        // agents cannot launch a second Unity process while the project is
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
                    CapturePerimeterSweep();
                    CapturePerimeterSightlineAudit();
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

    }
}
