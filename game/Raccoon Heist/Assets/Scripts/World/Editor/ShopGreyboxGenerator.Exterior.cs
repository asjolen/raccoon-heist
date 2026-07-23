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
    // Playable outside: street, side passage, back alley, den, storefront, roof route.
    public static partial class ShopGreyboxGenerator
    {
        // ---------- outside: street, side passage, back alley, den, roof route ----------

        static void BuildOutside()
        {
            // Invisible boundaries — the city continues visually, the raccoons don't.
            // The roof deck is 3.1 m high and the raccoon can jump another metre, so
            // the old four-metre top was actually vaultable. A 6.5 m top contains the
            // full roof route while remaining completely invisible.
            const float boundaryTop = 6.5f;
            InvisibleWall("Bound_Front", new Vector3(OutW0 - 0.3f, 0f, OutD0 - 0.3f), new Vector3(OutW1 + 0.3f, boundaryTop, OutD0));
            InvisibleWall("Bound_West", new Vector3(OutW0 - 0.3f, 0f, OutD0), new Vector3(OutW0, boundaryTop, OutD1 + 0.3f));
            InvisibleWall("Bound_East", new Vector3(OutW1, 0f, OutD0), new Vector3(OutW1 + 0.3f, boundaryTop, OutD1 + 0.3f));
            InvisibleWall("Bound_Back", new Vector3(OutW0, 0f, OutD1), new Vector3(OutW1, boundaryTop, OutD1 + 0.3f));

            // A continuous see-through metal fence marks the alley boundary. The den
            // sits against its safe inner edge; no decorative gap may expose the map drop.
            var fenceProbe = PlaceSynty("SM_Bld_Metal_Fence_01", new Vector3(0f, -50f, 0f));
            if (fenceProbe != null)
            {
                float flen = Mathf.Max(GeometryBounds(fenceProbe).size.x, GeometryBounds(fenceProbe).size.z);
                Object.DestroyImmediate(fenceProbe);
                int fenceIndex = 0;
                for (float x = OutW0; x < OutW1; x += flen)
                {
                    var fence = PlaceSynty("SM_Bld_Metal_Fence_01", new Vector3(x + flen / 2f, 0f, OutD1 + 0.15f));
                    if (fence != null) fence.name = $"BackBoundaryFence_{fenceIndex++:00}";
                }
            }
            else
            {
                BoxMinMax("BackBoundaryFence_Fallback", new Vector3(OutW0, 0f, OutD1),
                    new Vector3(OutW1, 1.8f, OutD1 + 0.1f), null, BrickMat(OutW1 - OutW0));
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
                if (PlaceSynty($"SM_Prop_Rubbish_Bin_0{i + 1}", new Vector3(2.6f + i * 1.1f, 0.12f, OutD0 - 1.08f), 180f) == null)
                    BoxMinMax($"Bin_{i}", new Vector3(2.3f + i * 1.1f, 0.12f, OutD0 - 1.35f), new Vector3(3.1f + i * 1.1f, 1f, OutD0 - 0.8f), null,
                        Mat("BinGreen", new Color(0.24f, 0.34f, 0.24f)));
            }
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(5.6f, 0.12f, OutD0 - 1.05f), 40f);

            // Side passage clutter (east, where the vent is)
            PlaceSynty("SM_Prop_Warehouse_Pallet_01", new Vector3(W + 3.5f, 0f, 6f), 15f);
            PlaceSynty("SM_Prop_Warehouse_Pallet_Stacked_01", new Vector3(W + 3.8f, 0f, 12f), 75f);
            PlaceSynty("SM_Prop_Trash_Bags_01", new Vector3(W + 1f, 0f, 17f), 200f);

            // Alley cover and the back-window approach. The old AC step was embedded
            // in the window opening, so the real dumpster remains without that obstruction.
            CrateStack("AlleyCrate", 11.2f, StorageZ1 + 0.4f, 0.6f);
            var dumpster = PlaceSynty("SM_Prop_Dumptser_02", new Vector3(10f, 0f, StorageZ1 + 0.95f), 90f);
            if (dumpster != null)
                dumpster.name = "Dumpster";
            else
                BoxMinMax("Dumpster", new Vector3(9.2f, 0f, StorageZ1 + 0.3f), new Vector3(10.8f, 1.3f, StorageZ1 + 1.3f), null,
                    Mat("DumpsterGreen", new Color(0.22f, 0.32f, 0.26f), 0.3f));

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
            var signLight = BoxMinMax("NeighbourShopSignLight", new Vector3(-2.82f, 3.05f, -0.17f), new Vector3(-2.38f, 3.18f, -0.105f), null, lamp);
            var signGlow = PointLight("NeighbourFrontageGlow", new Vector3(-2.6f, 2.78f, -0.72f),
                new Color(1f, 0.54f, 0.22f), 0.55f, 4.2f);
            ParentLightToFixture(signGlow, signLight);
        }

        // The interior walls stay simple and readable, while thin exterior skins make
        // the building belong to the surrounding city.
        static void BuildExteriorShell()
        {
            var exteriorBrick = BrickMat(D);
            BoxMinMax("ExteriorSkin_EastShop", new Vector3(W + T, 0f, EastVentZ1), new Vector3(W + T + 0.045f, H, D + T), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_BackRoomEast", new Vector3(3f + T, 0f, D + T), new Vector3(3f + T + 0.045f, H, D + T + 4f + T), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_BackRoomNorth", new Vector3(0f, 0f, D + T + 4f + T), new Vector3(3f + T, H, D + T + 4f + T + 0.045f), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_StorageNorth_W_A", new Vector3(StorageX0, 0f, StorageZ1 + T),
                new Vector3(RearBreakVentX0, H, StorageZ1 + T + 0.045f), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_StorageNorth_BreakVentHeader",
                new Vector3(RearBreakVentX0, CrawlVentHeight, StorageZ1 + T),
                new Vector3(RearBreakVentX1, H, StorageZ1 + T + 0.045f), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_StorageNorth_W_B", new Vector3(RearBreakVentX1, 0f, StorageZ1 + T),
                new Vector3(8.2f, H, StorageZ1 + T + 0.045f), null, exteriorBrick);
            BoxMinMax("ExteriorSkin_StorageNorth_E", new Vector3(9f, 0f, StorageZ1 + T), new Vector3(StorageX1, H, StorageZ1 + T + 0.045f), null, exteriorBrick);

            // Three overlapping modular shopfront prefabs made this read as three
            // unrelated buildings and hid signs/posters behind their structural bays.
            // One measured frame now follows the actual wall openings exactly.
            BuildUnifiedStorefront(exteriorBrick);
            BuildStorefrontBranding();
            var openSign = PlaceSyntyDecorative("SM_Sign_Neon_Open_03", new Vector3(2.05f, 1.25f, 0.08f), 180f);
            if (openSign != null) openSign.name = "OpenSignFixture";
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
            PlaceExteriorWallLampUnit("PassageVentLamp",
                new Vector3(W + 0.44f, 2.32f, 1.48f), 90f, Vector3.right);
            PlaceExteriorWallLampUnit("PassageSecurityLamp",
                new Vector3(W + 0.44f, 2.32f, 7.2f), 90f, Vector3.right);
            PlaceExteriorWallLampUnit("AlleyDumpsterLamp",
                new Vector3(9.6f, 2.32f, StorageZ1 + T + 0.44f), 0f, Vector3.forward);
            PlaceExteriorWallLampUnit("AlleyNoticeLamp",
                new Vector3(6.35f, 2.32f, StorageZ1 + T + 0.44f), 0f, Vector3.forward);
            PlaceSyntyDecorative("SM_Bld_Awning_01_Small", new Vector3(8.6f, 2.35f, StorageZ1 + T + 0.12f));
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_02", new Vector3(10.25f, 0.15f, StorageZ1 + T + 0.08f));
            PlaceSynty("SM_Prop_PowerBox_01", new Vector3(W + 0.65f, 0f, 10.5f), 90f);
            // The old seven-metre pipe preset projected through the passage and roof.
            // A compact, wall-flush utility run gives the same grime without tangles.
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_01", new Vector3(W + 0.18f, 0.35f, 12.35f), 90f);
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_02", new Vector3(W + 0.18f, 0.35f, 12.75f), 90f);
            PlaceSyntyDecorative("SM_Prop_Wall_Pipe_01", new Vector3(W + 0.18f, 0.35f, 13.15f), 90f);
            NameExteriorDetail(PlaceSyntyDecorative("SM_Gen_Bld_Pipe_Valve_01",
                new Vector3(W + 0.31f, 1.16f, 12.75f), 90f), "PassageReleaseValve");
            PlaceExteriorWallLampUnit("PassageValveLamp",
                new Vector3(W + 0.44f, 2.32f, 12.75f), 90f, Vector3.right);
            PlaceExteriorWallLampUnit("PassageUtilityLamp",
                new Vector3(W + 0.44f, 2.32f, 16.2f), 90f, Vector3.right);
            PlaceExteriorWallLampUnit("AlleySecondaryLamp",
                new Vector3(2.2f, 2.32f, D + T + 4f + T + 0.44f), 0f, Vector3.forward);

            // A small practical beside the den makes its warm pool diegetic.
            NameAndMakeEmissive(PlaceSyntyDecorative("SM_Prop_Bollard_Light_01",
                new Vector3(6.20f, 0.02f, OutD1 - 0.48f), 180f), "DenFenceLamp");

            BuildExteriorWallStorytelling();
        }

        static void NameAndMakeEmissive(GameObject fixture, string name)
        {
            if (fixture == null) return;
            fixture.name = name;
            MakeEmissive(fixture);
        }

        // One authored exterior wall-lamp unit, reused at every shop-side mount.
        // The fixture, lens and spotlight are positioned as a single assembly so
        // later tuning cannot separate the visible source from its pool of light.
        static GameObject PlaceExteriorWallLampUnit(string name, Vector3 fixtureCenter,
            float yaw, Vector3 outward)
        {
            var fixture = PlaceSyntyDecorative("SM_Prop_Wall_Light_01", fixtureCenter, yaw);
            if (fixture == null) return null;
            fixture.name = name;
            MakeEmissive(fixture);

            outward.Normalize();
            var bounds = GeometryBounds(fixture);
            float outwardExtent = Mathf.Abs(outward.x) * bounds.extents.x
                + Mathf.Abs(outward.z) * bounds.extents.z;
            // The measured prefab projects 0.88 m. Seat the source just inside the
            // real luminous underside near its nose; the prefab emission is the only
            // visible bulb, with no procedural square layered beneath it.
            Vector3 emitterCenter = bounds.center + outward * Mathf.Max(0f, outwardExtent - 0.11f);
            emitterCenter.y = bounds.min.y + 0.028f;

            var light = new GameObject($"{name}_Light").AddComponent<Light>();
            light.transform.SetParent(fixture.transform, true);
            light.type = LightType.Spot;
            light.transform.position = emitterCenter;
            light.transform.rotation = Quaternion.LookRotation(
                (Vector3.down + outward * 0.24f).normalized);
            light.color = ExteriorPracticalColor;
            light.intensity = 5.8f;
            light.range = 9.2f;
            light.spotAngle = 92f;
            light.innerSpotAngle = 46f;
            light.shadows = LightShadows.None;

            // A small fixture-bound point light approximates warm wall/pavement
            // bounce between the broad pools. It is deliberately weak: the visible
            // bulb remains the source and the alley keeps its night-time contrast.
            var bounce = new GameObject($"{name}_Bounce").AddComponent<Light>();
            bounce.transform.SetParent(fixture.transform, true);
            bounce.type = LightType.Point;
            bounce.transform.position = emitterCenter;
            bounce.color = ExteriorPracticalColor;
            bounce.intensity = 0.55f;
            bounce.range = 4.8f;
            bounce.shadows = LightShadows.None;
            return fixture;
        }

        static void BuildExteriorWallStorytelling()
        {
            // East service wall: paper and warning signs sit beside the hardware
            // they describe instead of becoming arbitrary sidewalk clutter.
            NameExteriorDetail(PlaceSyntyDecorative("SM_Prop_Poster_01",
                new Vector3(W + T + 0.075f, 0.78f, 8.45f), 90f), "PassagePoster_Community");
            NameExteriorDetail(PlaceSyntyDecorative("SM_Prop_Sign_Warning_01",
                new Vector3(W + T + 0.078f, 1.38f, 10.55f), 90f), "PassageElectricalWarning");
            NameExteriorDetail(PlaceSyntyDecorative("SM_Prop_Poster_03",
                new Vector3(W + T + 0.075f, 0.70f, 14.75f), 90f), "PassagePoster_Event");

            // Rear wall: one framed notice and two weathered sheets give the service
            // yard a history while leaving pipes, lamp pools, and the den route clear.
            NameExteriorDetail(PlaceSyntyDecorative("SM_Prop_Poster_Frame_01",
                new Vector3(5.45f, 0.70f, StorageZ1 + T + 0.072f)), "AlleyNoticeFrame");
            NameExteriorDetail(PlaceSyntyDecorative("SM_Prop_Poster_02",
                new Vector3(7.55f, 0.82f, StorageZ1 + T + 0.075f)), "AlleyPoster_LayerA");
            NameExteriorDetail(PlaceSyntyDecorative("SM_Prop_Poster_03",
                new Vector3(7.92f, 0.72f, StorageZ1 + T + 0.077f)), "AlleyPoster_LayerB");

        }

        static void NameExteriorDetail(GameObject detail, string name)
        {
            if (detail != null) detail.name = name;
        }

        static void PlaceWallGraphic(string name, string sourceMaterialName, Vector3 position,
            Vector3 outwardNormal, Vector2 size, Color tint, float roll)
        {
            string sourcePath = $"Assets/Synty/PolygonGeneric/Materials/Decals/{sourceMaterialName}.mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            var texture = source != null ? source.GetTexture("_Base_Map") : null;
            if (texture == null)
            {
                Debug.LogWarning($"Raccoon Heist: wall graphic texture missing for {sourceMaterialName}.");
                return;
            }

            var material = TransparentMat($"WallGraphic_{sourceMaterialName}", tint, 0f);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

            var graphic = GameObject.CreatePrimitive(PrimitiveType.Quad);
            graphic.name = name;
            graphic.transform.SetParent(root, false);
            graphic.transform.position = position;
            // The decal texture is authored for the Quad's front face. A negative X
            // scale corrects the back-face mirroring while keeping it wall-flush.
            graphic.transform.rotation = Quaternion.LookRotation(outwardNormal.normalized)
                * Quaternion.Euler(0f, 0f, roll);
            graphic.transform.localScale = new Vector3(-size.x, size.y, 1f);
            Object.DestroyImmediate(graphic.GetComponent<Collider>());
            ApplyMat(graphic, material);
            DisableShadows(graphic);
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

            float leftWindowMax = doorMin - 0.22f;
            float rightWindowMin = doorMax + 0.22f;
            float leftMullion = (windowMinX + leftWindowMax) * 0.5f;
            float rightMullion = (rightWindowMin + windowMaxX) * 0.5f;
            const float paneInset = 0.02f;
            BuildBreakableWindowPane("StorefrontWestOuter",
                new Vector3(windowMinX + paneInset, windowBottom + paneInset, -0.215f),
                new Vector3(leftMullion - paneInset, windowTop - paneInset, -0.202f), glass, 1);
            BuildBreakableWindowPane("StorefrontWestInner",
                new Vector3(leftMullion + paneInset, windowBottom + paneInset, -0.215f),
                new Vector3(leftWindowMax - paneInset, windowTop - paneInset, -0.202f), glass, 2);
            BuildBreakableWindowPane("StorefrontEastInner",
                new Vector3(rightWindowMin + paneInset, windowBottom + paneInset, -0.215f),
                new Vector3(rightMullion - paneInset, windowTop - paneInset, -0.202f), glass, 3);
            BuildBreakableWindowPane("StorefrontEastOuter",
                new Vector3(rightMullion + paneInset, windowBottom + paneInset, -0.215f),
                new Vector3(windowMaxX - paneInset, windowTop - paneInset, -0.202f), glass, 4);

            // Outer frame, door-adjacent frame, two mullions, and shared top/bottom
            // rails. Nothing crosses a sign or sits in front of a poster.
            foreach (float x in new[] { windowMinX, doorMin - 0.22f, doorMax + 0.22f, windowMaxX })
                BoxMinMax($"StorefrontFrame_V_{x}", new Vector3(x - 0.055f, windowBottom - 0.06f, -0.255f),
                    new Vector3(x + 0.055f, windowTop + 0.06f, -0.19f), null, frame);
            foreach (float x in new[] { leftMullion, rightMullion })
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
            var faultyCRenderers = new List<Renderer>();
            var faultyIRenderers = new List<Renderer>();
            var faultyTRenderers = new List<Renderer>();
            float cursor = 7f - total * 0.5f;
            int letterIndex = 0;
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
                    var renderers = letter.GetComponentsInChildren<Renderer>();
                    if (letterIndex == 2)
                        faultyCRenderers.AddRange(renderers);
                    else if (character == 'I')
                        faultyIRenderers.AddRange(renderers);
                    else if (character == 'T')
                        faultyTRenderers.AddRange(renderers);
                }
                cursor += width + spacing;
                letterIndex++;
            }

            // Three separated tubes age differently. The C gives rare short contact
            // chatter, the I is the most unreliable, and the final T has occasional
            // delayed stutters. Separate behaviours prevent synchronized flashing.
            CreateNeonLetterFault(holder, "C", faultyCRenderers, 8f, 18f,
                0.18f, 0.46f, 0.14f, 0.18f, 0.48f, 0.025f);
            CreateNeonLetterFault(holder, "I", faultyIRenderers, 3.5f, 9f,
                0.34f, 0.86f, 0.44f, 0.55f, 1.35f, 0.06f);
            CreateNeonLetterFault(holder, "T", faultyTRenderers, 11f, 24f,
                0.22f, 0.58f, 0.22f, 0.28f, 0.82f, 0.02f);
        }

        static void CreateNeonLetterFault(Transform holder, string letter,
            List<Renderer> targetRenderers, float quietMin, float quietMax,
            float chatterMin, float chatterMax, float blackoutChance,
            float blackoutMin, float blackoutMax, float spillInfluence)
        {
            if (targetRenderers.Count == 0) return;
            var controller = new GameObject($"NeonFault_{letter}");
            controller.transform.SetParent(holder, false);
            var flicker = controller.AddComponent<NeonSignFlicker>();
            flicker.targetRenderers = targetRenderers.ToArray();
            flicker.litBaseColor = new Color(0.08f, 0.48f, 0.62f);
            flicker.litEmission = new Color(0.2f, 2.4f, 3.2f);
            flicker.quietDurationMin = quietMin;
            flicker.quietDurationMax = quietMax;
            flicker.chatterDurationMin = chatterMin;
            flicker.chatterDurationMax = chatterMax;
            flicker.blackoutChance = blackoutChance;
            flicker.blackoutDurationMin = blackoutMin;
            flicker.blackoutDurationMax = blackoutMax;
            flicker.spillLightInfluence = spillInfluence;
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

        static void ValidatePlayableBoundaryContinuity()
        {
            var front = GameObject.Find("Bound_Front")?.GetComponent<BoxCollider>();
            var west = GameObject.Find("Bound_West")?.GetComponent<BoxCollider>();
            var east = GameObject.Find("Bound_East")?.GetComponent<BoxCollider>();
            var back = GameObject.Find("Bound_Back")?.GetComponent<BoxCollider>();
            bool valid = front != null && west != null && east != null && back != null;

            for (int i = 0; i <= 24 && valid; i++)
            {
                float across = Mathf.Lerp(OutW0 + 0.05f, OutW1 - 0.05f, i / 24f);
                float depth = Mathf.Lerp(OutD0 + 0.05f, OutD1 - 0.05f, i / 24f);
                valid &= front.bounds.Contains(new Vector3(across, 0.25f, OutD0 - 0.15f));
                valid &= back.bounds.Contains(new Vector3(across, 0.25f, OutD1 + 0.15f));
                valid &= west.bounds.Contains(new Vector3(OutW0 - 0.15f, 0.25f, depth));
                valid &= east.bounds.Contains(new Vector3(OutW1 + 0.15f, 0.25f, depth));
            }

            int rearFencePieces = 0;
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name.StartsWith("BackBoundaryFence_"))
                    rearFencePieces++;

            if (!valid)
                Debug.LogError("Raccoon Heist: playable perimeter has a collision gap that can drop a raccoon out of the map.");
            else if (rearFencePieces == 0)
                Debug.LogError("Raccoon Heist: rear boundary is physically sealed but has no continuous visible fence.");
            else if (GameObject.Find("AcUnit") != null)
                Debug.LogError("Raccoon Heist: obsolete alley AC still obstructs the back window.");
            else
                Debug.Log($"Raccoon Heist: validated a sealed four-sided perimeter and {rearFencePieces} rear fence pieces.");
        }

        static void DecorateStreetPassageAndAlley()
        {
            // Front street furniture establishes scale and creates strong silhouettes.
            PlaceSynty("SM_Prop_Hydrant_01", new Vector3(-1.1f, 0.12f, -1.72f), 20f);
            PlaceSynty("SM_Prop_Mailbox_01", new Vector3(12.8f, 0.12f, -1.68f), 180f);
            PlaceSynty("SM_Prop_ParkingMeter_01", new Vector3(0.2f, 0.12f, -1.78f));
            PlaceSynty("SM_Prop_ParkingMeter_01", new Vector3(13.8f, 0.12f, -1.78f));
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
            NameExteriorEffect(PlacePreferredSyntyFx("FX_Steam_01", "FX_Steam",
                new Vector3(5.5f, 0.06f, -3.2f), 0f, 0.55f), "Steam_FrontManhole");
            // The crawl duct is an entry route, not an exhaust. Its former additive
            // steam plus green point light made the whole opening glow radioactive.
            NameExteriorEffect(PlacePreferredSyntyFx("FX_Steam_03", "FX_Steam",
                new Vector3(17.8f, 0.075f, 10f), 180f, 0.42f), "Steam_PassageManhole");
            NameExteriorEffect(PlacePreferredSyntyFx("FX_Steam_01", "FX_Steam",
                new Vector3(3.8f, 0.075f, 24.1f), 25f, 0.38f), "Steam_RearManhole");
            var valveSteam = PlacePreferredSyntyFx("FX_Steam_02", "FX_Steam",
                new Vector3(W + 0.53f, 1.39f, 12.75f), 90f, 0.38f);
            ConfigureReleaseValveSteam(valveSteam);
            NameExteriorEffect(valveSteam, "Steam_PassageReleaseValve");

            // Thin animated ground haze is concentrated beneath practical light
            // pools. The gaps remain crisp, preventing a uniform blue blanket.
            PlaceGroundHaze("StreetLampHaze", new Vector3(7f, 0.20f, -4.1f), 0.36f);
            PlaceGroundHaze("PassageLampHaze_S", new Vector3(16.4f, 0.18f, 7.3f), 0.22f);
            PlaceGroundHaze("PassageLampHaze_N", new Vector3(16.4f, 0.18f, 16.1f), 0.20f);
            PlaceGroundHaze("AlleyLampHaze_E", new Vector3(9.5f, 0.18f, 22.7f), 0.22f);
            PlaceGroundHaze("AlleyLampHaze_W", new Vector3(2.3f, 0.18f, 22.8f), 0.20f);
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
                    new Color(0.16f, 0.22f, 0.38f, 0.04f),
                    new Color(0.30f, 0.40f, 0.62f, 0.14f));
                main.startLifetime = new ParticleSystem.MinMaxCurve(22f, 34f);
                main.startSize = new ParticleSystem.MinMaxCurve(6f, 13f);
                var emission = particles.emission;
                emission.rateOverTime = 1.4f;
                var shape = particles.shape;
                shape.radius = 9f;
            }
            foreach (var renderer in haze.GetComponentsInChildren<ParticleSystemRenderer>())
                renderer.sharedMaterial = GroundHazeMaterial(renderer.sharedMaterial);
        }

        static void NameExteriorEffect(GameObject effect, string name)
        {
            if (effect != null) effect.name = name;
        }

        static void ConfigureReleaseValveSteam(GameObject effect)
        {
            if (effect == null) return;
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.simulationSpeed = 0.72f;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.64f, 0.69f, 0.78f, 0.62f),
                    new Color(0.84f, 0.88f, 0.96f, 0.84f));
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.18f);
                var emission = particles.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(13f, 18f);
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.10f;
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.07f, 0.07f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.07f, 0.07f);
            }
            foreach (var renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
                renderer.sharedMaterial = ReleaseValveSteamMaterial(renderer.sharedMaterial);
        }

        static Material ReleaseValveSteamMaterial(Material source)
        {
            if (source == null) return null;
            const string path = "Assets/Materials/Greybox/Particles/ReleaseValveSteam_v2.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }
            var tint = new Color(0.72f, 0.78f, 0.88f, 0.86f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", tint);
            if (material.HasProperty("_EmissionEnabled")) material.SetFloat("_EmissionEnabled", 0f);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
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

    }
}
