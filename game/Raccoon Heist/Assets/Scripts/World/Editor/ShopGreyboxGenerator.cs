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
    // Split across partial files by concern: Materials, Interior, SetDressing,
    // Exterior, Backdrop, Ambience, Characters, Previews. This file owns the
    // layout constants, the menu entry points, and the shared box helpers.
    public static partial class ShopGreyboxGenerator
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
            BuildCrouchVentRoutes();
            BuildFixtures();
            BuildParkour();
            BuildOutside();
            BuildBackdrop();
            BuildLighting();
            BuildNeonAudio();
            BuildExteriorSteamAudio();
            BuildShopAlarm();
            BuildEnvironmentAudio();
            BuildNavigation();
            BuildRaccoon();
            ValidateVehicleBuildingClearance();
            ValidateBackdropPropBuildingClearance();
            ValidateRoadBuildingClearance();
            ValidateStreetCanyonCaps();
            ValidateStreetFurniturePlacement();
            ValidateNeighbourFrontageClearance();
            ValidateExteriorLightFixtures();
            ValidateCrouchVentRoutes();
            ValidateBreakableWindows();
            ValidatePlayableBoundaryContinuity();

            MarkStaticRecursive(root);

            // Persist immediately. Leaving the save to the designer let the on-disk
            // scene go stale, so the next editor launch loaded an old lighting state
            // (the recurring "scene turned light until regenerated" bug). Nothing in
            // the scene is hand-placed, so there is no manual work to protect.
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Raccoon Heist: shop greybox generated and saved. Press Play and sneak around.");
        }

        // Command-line/editor automation entry point: build into the saved project
        // scene regardless of which scene is currently open.
        public static void GenerateAndSave()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            Generate();
        }

        // Single entry point for unattended validation: build the saved scene and
        // render every route plus dedicated street-ring review angles.
        public static void GenerateSaveAndCapture()
        {
            GenerateAndSave();
            CaptureEnvironmentPreviews();
            CapturePerimeterSweep();
            CapturePerimeterSightlineAudit();
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

        // Static flags must reach nested content (backdrop buildings, shelf boards);
        // marking only the root's direct children left the whole city un-batched.
        static void MarkStaticRecursive(Transform node)
        {
            if (node.name.StartsWith("Harold") || node.GetComponent<Rigidbody>() != null
                || node.name.StartsWith("BreakableVentCover_")
                || node.name.StartsWith("BreakableWindow_")
                || node.GetComponent<HingedDoor>() != null
                || node.GetComponent<ShopAlarmController>() != null
                || node.GetComponent<NeonSignFlicker>() != null)
                return; // dynamic subtree: doors swing, beacons spin, loot moves
            if (node != root) node.gameObject.isStatic = true;
            foreach (Transform child in node) MarkStaticRecursive(child);
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
