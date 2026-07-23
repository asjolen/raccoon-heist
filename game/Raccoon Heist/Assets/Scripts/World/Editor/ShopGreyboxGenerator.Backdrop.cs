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
    // Visual-only city beyond the playable walls: street ring, buildings, vehicles.
    public static partial class ShopGreyboxGenerator
    {
        // The visible district is intentionally finite. Every road ray ends against
        // a mid-rise facade after a short city-block view, while the authored Synty
        // skyline continues behind it. This avoids both a boxed-in wall and an empty
        // kilometre-long corridor.
        const float CityWestEnd = -34f;
        const float CityEastEnd = 50f;
        const float CityNorthEnd = -34f;
        const float CitySouthEnd = 52f;
        const float CityWestCapFacade = -39f;
        const float CityEastCapFacade = 55f;
        const float CityNorthCapFacade = -39f;
        const float CitySouthCapFacade = 57f;

        // ---------- backdrop: the city beyond the walls ----------

        static void BuildBackdrop()
        {
            var backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root, false);

            // Dark floor far past the walls so gaps between buildings never show void
            BoxMinMax("VoidFloor", new Vector3(-70f, -0.25f, -70f), new Vector3(W + 70f, -0.15f, D + 70f), backdrop,
                Mat("Void", new Color(0.04f, 0.04f, 0.06f)));

            BuildVisualStreetRing(backdrop);

            // Synty's normal apartment/shop pieces are one-sided facade modules.
            // Freestanding rows expose their blank backs and create the huge slabs
            // that used to dominate high and oblique views. Build complete blocks:
            // two-sided corner modules turn every corner, straight modules remain
            // shoulder-to-shoulder, and every street ray ends on a genuinely
            // multi-sided landmark prefab.
            BuildSyntyPerimeterDistrict(backdrop);
            BuildStreetEndLandmarks(backdrop);
            BuildAuthoredCitySkyline(backdrop);

            BuildLowPolyClouds(backdrop);

            StreetDressing();

            // The backdrop is unreachable set dressing; primitive cubes arrive with
            // BoxColliders that would only feed the physics broadphase and raycasts.
            // Playable-boundary InvisibleWalls parent to root, so they are untouched.
            foreach (var collider in backdrop.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        // Roads wrap visually around the entire block. Only the front face, east
        // service passage, and rear alley are playable; the other streets sit beyond
        // chain-link boundaries and sell a larger city without bloating traversal.
        static void BuildVisualStreetRing(Transform backdrop)
        {
            var road = TiledMat("CityAsphaltWet", new Color(0.30f, 0.32f, 0.38f), TexAsphalt, 32f, 4f, 0.42f);
            var curb = TiledMat("CityCurbStone", new Color(0.54f, 0.55f, 0.58f), TexSlabs, 24f, 1f, 0.05f);

            // Rear cross street, east side street, and west side street.
            BoxMinMax("CityRoad_Back", new Vector3(CityWestEnd, 0f, OutD1 + 2f), new Vector3(CityEastEnd, 0.018f, OutD1 + 9f), backdrop, road);
            BoxMinMax("CityRoad_East", new Vector3(OutW1 + 2f, 0f, CityNorthEnd), new Vector3(OutW1 + 9f, 0.018f, CitySouthEnd), backdrop, road);
            BoxMinMax("CityRoad_West", new Vector3(OutW0 - 9f, 0f, CityNorthEnd), new Vector3(OutW0 - 2f, 0.018f, CitySouthEnd), backdrop, road);

            // Sidewalks and their curb caps stop at every carriageway edge. The old
            // unbroken strips crossed the intersections as raised concrete bars.
            var horizontalRuns = new[]
            {
                new Vector2(CityWestEnd, OutW0 - 9f),
                new Vector2(OutW0 - 2f, OutW1 + 2f),
                new Vector2(OutW1 + 9f, CityEastEnd)
            };
            for (int i = 0; i < horizontalRuns.Length; i++)
            {
                float width = horizontalRuns[i].y - horizontalRuns[i].x;
                BoxMinMax($"CityWalk_BackNear_{i}", new Vector3(horizontalRuns[i].x, 0f, OutD1),
                    new Vector3(horizontalRuns[i].y, 0.12f, OutD1 + 2f), backdrop, SidewalkMat(width, 2f));
                BoxMinMax($"CityWalk_BackFar_{i}", new Vector3(horizontalRuns[i].x, 0f, OutD1 + 9f),
                    new Vector3(horizontalRuns[i].y, 0.12f, OutD1 + 13.5f), backdrop, SidewalkMat(width, 4.5f));
                BoxMinMax($"CityCurb_BackNear_{i}", new Vector3(horizontalRuns[i].x, 0.12f, OutD1 + 1.84f),
                    new Vector3(horizontalRuns[i].y, 0.18f, OutD1 + 2f), backdrop, curb);
                BoxMinMax($"CityCurb_BackFar_{i}", new Vector3(horizontalRuns[i].x, 0.12f, OutD1 + 9f),
                    new Vector3(horizontalRuns[i].y, 0.18f, OutD1 + 9.16f), backdrop, curb);
            }

            var verticalRuns = new[]
            {
                new Vector2(CityNorthEnd, -12f),
                new Vector2(-2f, OutD1 + 2f),
                new Vector2(OutD1 + 9f, CitySouthEnd)
            };
            for (int i = 0; i < verticalRuns.Length; i++)
            {
                float depth = verticalRuns[i].y - verticalRuns[i].x;
                BoxMinMax($"CityWalk_EastNear_{i}", new Vector3(OutW1, 0f, verticalRuns[i].x),
                    new Vector3(OutW1 + 2f, 0.12f, verticalRuns[i].y), backdrop, SidewalkMat(2f, depth));
                BoxMinMax($"CityWalk_EastFar_{i}", new Vector3(OutW1 + 9f, 0f, verticalRuns[i].x),
                    new Vector3(OutW1 + 13.5f, 0.12f, verticalRuns[i].y), backdrop, SidewalkMat(4.5f, depth));
                BoxMinMax($"CityWalk_WestNear_{i}", new Vector3(OutW0 - 2f, 0f, verticalRuns[i].x),
                    new Vector3(OutW0, 0.12f, verticalRuns[i].y), backdrop, SidewalkMat(2f, depth));
                BoxMinMax($"CityWalk_WestFar_{i}", new Vector3(OutW0 - 13.5f, 0f, verticalRuns[i].x),
                    new Vector3(OutW0 - 9f, 0.12f, verticalRuns[i].y), backdrop, SidewalkMat(4.5f, depth));
                BoxMinMax($"CityCurb_EastNear_{i}", new Vector3(OutW1 + 1.84f, 0.12f, verticalRuns[i].x),
                    new Vector3(OutW1 + 2f, 0.18f, verticalRuns[i].y), backdrop, curb);
                BoxMinMax($"CityCurb_EastFar_{i}", new Vector3(OutW1 + 9f, 0.12f, verticalRuns[i].x),
                    new Vector3(OutW1 + 9.16f, 0.18f, verticalRuns[i].y), backdrop, curb);
                BoxMinMax($"CityCurb_WestNear_{i}", new Vector3(OutW0 - 2f, 0.12f, verticalRuns[i].x),
                    new Vector3(OutW0 - 1.84f, 0.18f, verticalRuns[i].y), backdrop, curb);
                BoxMinMax($"CityCurb_WestFar_{i}", new Vector3(OutW0 - 9.16f, 0.12f, verticalRuns[i].x),
                    new Vector3(OutW0 - 9f, 0.18f, verticalRuns[i].y), backdrop, curb);
            }

            // Measured 5 m POLYGON road tiles provide painted lines on every side.
            for (float x = CityWestEnd; x < CityEastEnd; x += 5f)
            {
                var tile = PlaceSynty("SM_Env_Road_Lines_01", new Vector3(x + 2.5f, 0.024f, OutD1 + 5.5f), 90f);
                if (tile != null) tile.transform.SetParent(backdrop, true);
            }
            for (float z = CityNorthEnd; z < CitySouthEnd; z += 5f)
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
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Muscle_01", new Vector3(-32f, 0.02f, OutD1 + 8.05f), 90f);
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Sedan_01", new Vector3(41f, 0.02f, OutD1 + 2.95f), 270f);
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Van_01", new Vector3(OutW1 + 7.6f, 0.02f, -23f), 180f);
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Medium_01", new Vector3(OutW1 + 2.95f, 0.02f, 44f));
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Sedan_01", new Vector3(OutW0 - 8.05f, 0.02f, -22f));
            PlaceBackdropProp(backdrop, "SM_Veh_Car_Taxi_01", new Vector3(OutW0 - 3.2f, 0.02f, 44f), 180f);

            // Flat POLYGON road details break up the long asphalt runs without
            // putting obstacles in a traffic lane.
            PlaceBackdropProp(backdrop, "SM_Env_Road_Patch_01",
                new Vector3(7f, 0.022f, OutD1 + 5.5f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Manhole_01",
                new Vector3(OutW1 + 5.5f, 0.022f, 11f), 15f);
            PlaceBackdropProp(backdrop, "SM_Prop_Manhole_02",
                new Vector3(OutW0 - 5.5f, 0.022f, -20f), 205f);
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
            foreach (float x in new[] { -25f, -18f, 5f, 17f, 36f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(x, 0.12f, OutD1 + 1.64f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(-28f, 0.12f, OutD1 + 1.55f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(-3f, 0.12f, OutD1 + 0.78f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_02", new Vector3(-1.45f, 0.12f, OutD1 + 0.72f), 20f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(7.2f, 0.12f, OutD1 + 0.74f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(14f, 0.12f, OutD1 + 0.7f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(19.7f, 0.12f, OutD1 + 0.7f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Trashbin_01", new Vector3(33.8f, 0.12f, OutD1 + 1.3f), 335f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_01", new Vector3(31f, 0.12f, OutD1 + 0.72f), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_02", new Vector3(31.7f, 0.12f, OutD1 + 1.18f), 35f);

            // Rear far pavement: a small transit/commercial strip against the flats.
            foreach (float x in new[] { -6f, 10f, 32.8f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(x, 0.12f, rearFarCurb));
            PlaceBackdropProp(backdrop, "SM_Prop_BusStop_01", new Vector3(-33f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_01", new Vector3(-25.2f, 0.12f, rearFarWall - 0.5f), 15f);
            PlaceBackdropProp(backdrop, "SM_Prop_Phones_01", new Vector3(-4f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(0.2f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_ATM_01", new Vector3(8f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(13.2f, 0.12f, rearFarWall));
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(18f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(19.5f, 0.12f, rearFarWall), 155f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(31f, 0.12f, rearFarWall), 180f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(-30f, 0.12f, rearFarWall), 330f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_01", new Vector3(-23f, 0.12f, rearFarWall - 0.15f), 18f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_03", new Vector3(3.5f, 0.12f, rearFarWall - 0.12f), 208f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_01", new Vector3(37f, 0.12f, rearFarWall - 0.12f), 284f);

            // East side: delivery/service clutter on the near walk, public furniture
            // against the opposite shop row. Keep the road itself clear.
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(OutW1 + 1.55f, 0.12f, -16f), 25f);
            foreach (float z in new[] { -17.5f, 6.5f, 19f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(OutW1 + 1.64f, 0.12f, z), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_PowerBox_01", new Vector3(OutW1 + 0.75f, 0.12f, -1f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_01", new Vector3(OutW1 + 0.68f, 0.12f, 5.2f), 35f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_03", new Vector3(OutW1 + 0.72f, 0.12f, 6.15f), 300f);
            PlaceBackdropProp(backdrop, "SM_Prop_Warehouse_Pallet_Stacked_02", new Vector3(OutW1 + 0.72f, 0.12f, 11f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_03", new Vector3(OutW1 + 0.72f, 0.12f, 16f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(OutW1 + 0.72f, 0.12f, 22f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(OutW1 + 0.75f, 0.12f, 25.5f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(OutW1 + 0.72f, 0.12f, 34.5f), 270f);

            foreach (float z in new[] { -24f, 6f, 20f, 38f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(eastFarCurb, 0.12f, z), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(eastFarWall, 0.12f, -14f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Phones_01", new Vector3(eastFarWall - 0.35f, 0.12f, 1f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Trashbin_02", new Vector3(eastFarWall, 0.12f, 7f), 20f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(eastFarWall, 0.12f, 13f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(eastFarWall, 0.12f, 18f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(eastFarWall, 0.12f, 23.5f), 255f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_01", new Vector3(eastFarWall, 0.12f, 35.5f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_02", new Vector3(eastFarWall, 0.12f, 37.5f), 15f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_02", new Vector3(eastFarWall - 0.12f, 0.12f, -22f), 68f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_01", new Vector3(eastFarWall - 0.14f, 0.12f, 10f), 176f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_03", new Vector3(eastFarWall - 0.12f, 0.12f, 26f), 292f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_02", new Vector3(eastFarWall - 0.16f, 0.12f, 35f), 105f);

            // West side: this is the most exposed perimeter in the player views, so
            // use curb rhythm along the full run and several small residential clusters.
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(OutW0 - 1.55f, 0.12f, -24f), 25f);
            foreach (float z in new[] { -1f, 4.5f, 17f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(OutW0 - 1.64f, 0.12f, z), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Trashbin_01", new Vector3(OutW0 - 0.75f, 0.12f, 1f), 20f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(OutW0 - 0.75f, 0.12f, 6f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(OutW0 - 0.78f, 0.12f, 13f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(OutW0 - 0.75f, 0.12f, 19f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_01", new Vector3(OutW0 - 0.72f, 0.12f, 26f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_TrashBag_01", new Vector3(OutW0 - 0.7f, 0.12f, 25.2f), 320f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(OutW0 - 0.72f, 0.12f, 38.5f), 75f);

            foreach (float z in new[] { -24f, 7f, 22f, 38f })
                PlaceBackdropProp(backdrop, "SM_Prop_ParkingMeter_01", new Vector3(westFarCurb, 0.12f, z), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Hydrant_01", new Vector3(westFarWall, 0.12f, -24f), 25f);
            PlaceBackdropProp(backdrop, "SM_Prop_Phones_01", new Vector3(westFarWall + 0.35f, 0.12f, 0f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ParkBench_01", new Vector3(westFarWall, 0.12f, 4f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Planter_02", new Vector3(westFarWall, 0.12f, 8f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Rubbish_Bin_03", new Vector3(westFarWall, 0.12f, 11f), 15f);
            PlaceBackdropProp(backdrop, "SM_Prop_Newspaper_02", new Vector3(westFarWall, 0.12f, 16f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_ATM_01", new Vector3(westFarWall, 0.12f, 21f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(westFarWall, 0.12f, 25f), 105f);
            PlaceBackdropProp(backdrop, "SM_Prop_Mailbox_01", new Vector3(westFarWall, 0.12f, 37.4f), 90f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_03", new Vector3(westFarWall + 0.12f, 0.12f, -20f), 32f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_01", new Vector3(westFarWall + 0.14f, 0.12f, 13f), 147f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_02", new Vector3(westFarWall + 0.12f, 0.12f, 24f), 254f);
            PlaceBackdropTree(backdrop, "SM_Env_Tree_03", new Vector3(westFarWall + 0.16f, 0.12f, 35f), 341f);

            // Stop signs sit on the corner verges, never in the crossing or the
            // clear middle of a pavement. They make the new road openings read as
            // deliberate intersections rather than holes between buildings.
            PlaceBackdropProp(backdrop, "SM_Prop_Sign_Stop_01",
                new Vector3(OutW0 - 1.35f, 0.12f, OutD0 - 0.75f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Sign_Stop_01",
                new Vector3(OutW1 + 1.35f, 0.12f, OutD0 - 0.75f), 270f);
            PlaceBackdropProp(backdrop, "SM_Prop_Sign_GiveWay_01",
                new Vector3(OutW0 - 1.35f, 0.12f, OutD1 + 0.75f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_Sign_GiveWay_02",
                new Vector3(OutW1 + 1.35f, 0.12f, OutD1 + 0.75f), 270f);
        }

        static GameObject PlaceBackdropTree(Transform parent, string name, Vector3 position, float yaw)
        {
            var tree = PlaceBackdropProp(parent, name, position, yaw);
            if (tree != null) tree.name = $"CityTree_{name}_{position.x:0}_{position.z:0}";
            return tree;
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
            lamp.transform.SetParent(pole.transform, true);
            lamp.type = LightType.Spot;
            // This compact straight streetlight has collision proxy pieces outside
            // its visible head. Its renderer bounds place the source safely inside
            // the actual glowing cap; the collider-head method is for L-arm poles.
            lamp.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.35f, bounds.center.z);
            lamp.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            lamp.spotAngle = 94f;
            lamp.innerSpotAngle = 44f;
            lamp.color = ExteriorPracticalColor;
            lamp.intensity = intensity;
            lamp.range = 9.2f;
            lamp.shadows = LightShadows.None;
        }

        static readonly string[] BlockShopFronts = { "SM_Bld_Shop_01", "SM_Bld_Shop_02",
                                                     "SM_Bld_Shop_04", "SM_Bld_Shop_05",
                                                     "SM_Bld_Shop_06" };
        static readonly string[] Apartments = { "SM_Bld_Apartment_Stack_01", "SM_Bld_Apartment_Stack_02" };

        static void BuildSyntyPerimeterDistrict(Transform backdrop)
        {
            // Eight real blocks surround the compact playable block. Their 12-21 m
            // street walls use Synty's modular grammar exactly as the City demo does:
            // shop/door base, stacked apartments, roof cap, and a two-sided corner
            // at every turn. Lower stepped apartment/roof modules fill each centre,
            // avoiding exposed courtyards in aerial and roof-route views.
            BuildSyntyCityBlock(backdrop, "NorthWest", -34.5f, -18.5f, -31.5f, -15.5f, 2, true, 1);
            BuildSyntyCityBlock(backdrop, "North", -6f, 20f, -31.5f, -15.5f, 1, true, 2);
            BuildSyntyCityBlock(backdrop, "NorthEast", 32.5f, 48.5f, -31.5f, -15.5f, 1, true, 3);

            BuildSyntyCityBlock(backdrop, "West", -34.5f, -18.5f, -0.5f, 25.5f, 1, false, 4);
            BuildSyntyCityBlock(backdrop, "East", 32.5f, 48.5f, -0.5f, 25.5f, 2, true, 5);

            BuildSyntyCityBlock(backdrop, "SouthWest", -34.5f, -18.5f, 39.5f, 55.5f, 1, false, 6);
            BuildSyntyCityBlock(backdrop, "South", -6f, 20f, 39.5f, 55.5f, 1, true, 7);
            BuildSyntyCityBlock(backdrop, "SouthEast", 32.5f, 48.5f, 39.5f, 55.5f, 2, true, 8);
        }

        static void BuildSyntyCityBlock(Transform backdrop, string blockName,
            float xMin, float xMax, float zMin, float zMax,
            int upperStacks, bool commercial, int variant)
        {
            bool modularKitAvailable = SyntyPrefab("SM_Bld_Apartment_Stack_01") != null
                && SyntyPrefab("SM_Bld_Apartment_Corner_01") != null
                && SyntyPrefab("SM_Bld_Apartment_Roof_01") != null
                && SyntyPrefab(commercial
                    ? "SM_Bld_Shop_Corner_01"
                    : "SM_Bld_Apartment_Door_Corner_01") != null;
            if (!modularKitAvailable)
            {
                BoxMinMax($"BackdropBld_Fallback_{blockName}",
                    new Vector3(xMin, 0f, zMin),
                    new Vector3(xMax, 3f + upperStacks * 9f, zMax),
                    backdrop,
                    BuildingMat(variant % 2,
                        Mathf.Max(2f, (xMax - xMin) / 3f),
                        Mathf.Max(3f, upperStacks * 3f)));
                return;
            }

            var block = new GameObject($"SyntyCityBlock_{blockName}").transform;
            block.SetParent(backdrop, false);

            // Corner prefab decorated faces:
            // yaw 0 = +X/+Z, 90 = +X/-Z, 180 = -X/-Z, 270 = -X/+Z.
            BuildSyntyBlockCorner(block, blockName, xMin, zMin, 180f, upperStacks, commercial, variant);
            BuildSyntyBlockCorner(block, blockName, xMax, zMin, 90f, upperStacks, commercial, variant + 1);
            BuildSyntyBlockCorner(block, blockName, xMax, zMax, 0f, upperStacks, commercial, variant + 2);
            BuildSyntyBlockCorner(block, blockName, xMin, zMax, 270f, upperStacks, commercial, variant + 3);

            BuildSyntyBlockSide(block, blockName, true, zMin, xMin, xMax, 180f,
                upperStacks, commercial, variant);
            BuildSyntyBlockSide(block, blockName, false, xMax, zMin, zMax, 90f,
                upperStacks, commercial, variant + 3);
            BuildSyntyBlockSide(block, blockName, true, zMax, xMin, xMax, 0f,
                upperStacks, commercial, variant + 6);
            BuildSyntyBlockSide(block, blockName, false, xMin, zMin, zMax, 270f,
                upperStacks, commercial, variant + 9);

            FillSyntyBlockInterior(block, blockName, xMin, xMax, zMin, zMax,
                upperStacks, variant);
            DressSyntyCityBlock(block, blockName, xMin, xMax, zMin, zMax,
                3f + upperStacks * 9f, commercial, variant);
        }

        static void BuildSyntyBlockCorner(Transform block, string blockName,
            float xEdge, float zEdge, float yaw, int upperStacks,
            bool commercial, int variant)
        {
            string groundName = commercial
                ? "SM_Bld_Shop_Corner_01"
                : variant % 2 == 0
                    ? "SM_Bld_Apartment_Door_Corner_01"
                    : "SM_Bld_Apartment_Door_Corner_02";
            var ground = PlaceSynty(groundName, new Vector3(xEdge, 0f, zEdge), yaw);
            FinishSyntyBlockCorner(ground, block, blockName, "Ground", xEdge, zEdge, yaw, variant);

            int apartmentFloors = upperStacks * 3;
            for (int floor = 0; floor < apartmentFloors; floor++)
            {
                string apartmentName = $"SM_Bld_Apartment_Corner_0{1 + (variant + floor) % 3}";
                var upper = PlaceSynty(apartmentName,
                    new Vector3(xEdge, 3f + floor * 3f, zEdge), yaw);
                FinishSyntyBlockCorner(upper, block, blockName, $"Upper_{floor:00}",
                    xEdge, zEdge, yaw, variant);
            }

            string roofName = $"SM_Bld_Apartment_Roof_Corner_0{1 + variant % 3}";
            var roof = PlaceSynty(roofName,
                new Vector3(xEdge, 3f + upperStacks * 9f, zEdge), yaw);
            FinishSyntyBlockCorner(roof, block, blockName, "Roof", xEdge, zEdge, yaw, variant);
        }

        static void FinishSyntyBlockCorner(GameObject piece, Transform block, string blockName,
            string storeyName, float xEdge, float zEdge, float yaw, int variant)
        {
            if (piece == null) return;
            var bounds = GeometryBounds(piece);
            bool positiveX = Mathf.Approximately(yaw, 0f) || Mathf.Approximately(yaw, 90f);
            bool positiveZ = Mathf.Approximately(yaw, 0f) || Mathf.Approximately(yaw, 270f);
            piece.transform.position += Vector3.right
                * (xEdge - (positiveX ? bounds.max.x : bounds.min.x));
            bounds = GeometryBounds(piece);
            piece.transform.position += Vector3.forward
                * (zEdge - (positiveZ ? bounds.max.z : bounds.min.z));
            piece.name = $"CityBlockBuilding_{blockName}_Corner_{variant:00}_{storeyName}_{piece.name}";
            MakeEmissive(piece);
            piece.transform.SetParent(block, true);
        }

        static void BuildSyntyBlockSide(Transform block, string blockName,
            bool alongX, float facadeEdge, float from, float to, float yaw,
            int upperStacks, bool commercial, int variant)
        {
            // The corner pieces occupy roughly 5.2 m along either edge. Fill the
            // remainder with measured ~5 m straight bays with no random gaps.
            const float cornerRun = 5.2f;
            const float bayRun = 5f;
            float cursor = from + cornerRun;
            int bay = 0;
            while (cursor + bayRun <= to - cornerRun + 0.05f)
            {
                float along = cursor + bayRun * 0.5f;
                Vector3 position = alongX
                    ? new Vector3(along, 0f, facadeEdge)
                    : new Vector3(facadeEdge, 0f, along);
                string groundName = commercial
                    ? BlockShopFronts[(variant + bay) % BlockShopFronts.Length]
                    : (variant + bay) % 2 == 0
                        ? "SM_Bld_Apartment_Door_01"
                        : "SM_Bld_Apartment_Door_02";
                var ground = PlaceSynty(groundName, position, yaw);
                FinishSyntyBlockFacade(ground, block, blockName, $"Bay_{variant:00}_{bay:00}_Ground",
                    yaw, facadeEdge);

                for (int stack = 0; stack < upperStacks; stack++)
                {
                    string upperName = Apartments[(variant + bay + stack) % Apartments.Length];
                    position.y = 3f + stack * 9f;
                    var upper = PlaceSynty(upperName, position, yaw);
                    FinishSyntyBlockFacade(upper, block, blockName,
                        $"Bay_{variant:00}_{bay:00}_Upper_{stack:00}", yaw, facadeEdge);
                }

                position.y = 3f + upperStacks * 9f;
                string roofName = $"SM_Bld_Apartment_Roof_0{1 + (variant + bay) % 3}";
                var roof = PlaceSynty(roofName, position, yaw);
                FinishSyntyBlockFacade(roof, block, blockName,
                    $"Bay_{variant:00}_{bay:00}_Roof", yaw, facadeEdge);

                cursor += bayRun;
                bay++;
            }
        }

        static void FinishSyntyBlockFacade(GameObject piece, Transform block, string blockName,
            string pieceName, float yaw, float facadeEdge)
        {
            if (piece == null) return;
            AlignFacadeEdge(piece, yaw, facadeEdge);
            piece.name = $"CityBlockBuilding_{blockName}_{pieceName}_{piece.name}";
            MakeEmissive(piece);
            piece.transform.SetParent(block, true);
        }

        static void FillSyntyBlockInterior(Transform block, string blockName,
            float xMin, float xMax, float zMin, float zMax,
            int upperStacks, int variant)
        {
            // A perimeter-only block leaves a black courtyard rectangle in aerial
            // and roof-route views. Fill the core with actual 5 m apartment stacks
            // one storey-step lower than the street wall, then cap every stack with
            // the matching Synty roof prefab. The setback keeps a readable roofline
            // while ensuring there is no see-through void.
            int cellIndex = 0;
            for (float x = xMin + 8f; x <= xMax - 8f + 0.05f; x += 5f)
            {
                for (float z = zMin + 8f; z <= zMax - 8f + 0.05f; z += 5f)
                {
                    float yaw = ((variant + cellIndex) % 4) * 90f;
                    for (int stack = 0; stack < upperStacks; stack++)
                    {
                        string stackName = Apartments[(variant + cellIndex + stack) % Apartments.Length];
                        var interior = PlaceSynty(stackName,
                            new Vector3(x, stack * 9f, z), yaw);
                        if (interior == null) continue;
                        interior.name = $"CityBlockBuilding_{blockName}_Interior_{cellIndex:00}_Stack_{stack:00}";
                        MakeEmissive(interior);
                        interior.transform.SetParent(block, true);
                    }

                    string roofName = $"SM_Bld_Apartment_Roof_0{1 + (variant + cellIndex) % 3}";
                    var roof = PlaceSynty(roofName,
                        new Vector3(x, upperStacks * 9f, z), yaw);
                    if (roof != null)
                    {
                        roof.name = $"CityBlockBuilding_{blockName}_Interior_{cellIndex:00}_Roof";
                        roof.transform.SetParent(block, true);
                    }
                    cellIndex++;
                }
            }
        }

        static void DressSyntyCityBlock(Transform block, string blockName,
            float xMin, float xMax, float zMin, float zMax,
            float roofY, bool commercial, int variant)
        {
            float centreX = (xMin + xMax) * 0.5f;
            float centreZ = (zMin + zMax) * 0.5f;

            // Roof hardware uses large readable clusters, not evenly scattered
            // trinkets. Everything here is an actual City prefab.
            PlaceBlockProp(block, $"CityBlock_{blockName}_RoofAccess",
                "SM_Bld_Roof_Access_01",
                new Vector3(centreX - 2.2f, roofY, zMin + 2.7f), variant * 31f);
            PlaceBlockProp(block, $"CityBlock_{blockName}_RoofAirconA",
                "SM_Prop_Roof_Aircon_02",
                new Vector3(centreX + 2.1f, roofY, zMin + 2.5f), 90f);
            PlaceBlockProp(block, $"CityBlock_{blockName}_RoofAirconB",
                "SM_Prop_Roof_Aircon_03",
                new Vector3(xMin + 2.6f, roofY, centreZ), 0f);

            if (variant % 3 == 0)
                PlaceBlockProp(block, $"CityBlock_{blockName}_WaterTower",
                    "SM_Prop_Water_Tower_01",
                    new Vector3(xMax - 2.8f, roofY, centreZ), variant * 17f);
            else
                PlaceBlockProp(block, $"CityBlock_{blockName}_Satellite",
                    "SM_Prop_SatDish_01",
                    new Vector3(xMax - 2.7f, roofY, centreZ), variant * 29f);

            if (commercial)
            {
                string signName = variant % 2 == 0
                    ? "SM_Prop_LargeSign_Donut_01"
                    : "SM_Prop_LargeSign_Pizza_01";
                PlaceBlockProp(block, $"CityBlock_{blockName}_RoofSign", signName,
                    new Vector3(centreX, roofY, zMin + 2.6f), variant * 19f);
            }

            // One fire escape per block breaks up the long side wall and gives the
            // upper storeys the authored layered depth seen in Synty's demo.
            var fireEscape = PlaceSynty(variant % 2 == 0
                    ? "SM_Bld_FireEscape_02"
                    : "SM_Bld_FireEscape_03",
                new Vector3(xMax, 3f, centreZ), 90f);
            if (fireEscape != null)
            {
                var bounds = GeometryBounds(fireEscape);
                fireEscape.transform.position += Vector3.right * (xMax + 0.18f - bounds.min.x);
                fireEscape.name = $"CityBlock_{blockName}_FireEscape";
                fireEscape.transform.SetParent(block, true);
            }
        }

        static void PlaceBlockProp(Transform block, string instanceName, string prefabName,
            Vector3 position, float yaw)
        {
            var prop = PlaceSynty(prefabName, position, yaw);
            if (prop == null) return;
            prop.name = instanceName;
            MakeEmissive(prop);
            prop.transform.SetParent(block, true);
        }

        static void BuildStreetEndLandmarks(Transform backdrop)
        {
            // Road ends use complete multi-sided prefabs. Apartment stacks and normal
            // shops are invalid here: their blank backs caused the visible purple caps.
            PlaceStreetEndLandmark(backdrop, "StreetCap_MainWest",
                "SM_Bld_OfficeOld_Large_01", new Vector3(CityWestCapFacade, 0f, -7f), 90f,
                CityWestCapFacade, 1);
            PlaceStreetEndLandmark(backdrop, "StreetCap_MainEast",
                "SM_Bld_OfficeOld_Large_02", new Vector3(CityEastCapFacade, 0f, -7f), 270f,
                CityEastCapFacade, 2);
            PlaceStreetEndLandmark(backdrop, "StreetCap_RearWest",
                "SM_Bld_OfficeOld_Small_01", new Vector3(CityWestCapFacade, 0f, OutD1 + 5.5f), 90f,
                CityWestCapFacade, 3);
            PlaceStreetEndLandmark(backdrop, "StreetCap_RearEast",
                "SM_Bld_OfficeSquare_03", new Vector3(CityEastCapFacade, 0f, OutD1 + 5.5f), 270f,
                CityEastCapFacade, 4);
            PlaceStreetEndLandmark(backdrop, "StreetCap_WestNorth",
                "SM_Bld_CityHall_01", new Vector3(OutW0 - 5.5f, 0f, CityNorthCapFacade), 0f,
                CityNorthCapFacade, 5);
            PlaceStreetEndLandmark(backdrop, "StreetCap_WestSouth",
                "SM_Bld_OfficeOctagon_01", new Vector3(OutW0 - 5.5f, 0f, CitySouthCapFacade), 180f,
                CitySouthCapFacade, 6);
            PlaceStreetEndLandmark(backdrop, "StreetCap_EastNorth",
                "SM_Bld_OfficeRound_01", new Vector3(OutW1 + 5.5f, 0f, CityNorthCapFacade), 0f,
                CityNorthCapFacade, 7);
            PlaceStreetEndLandmark(backdrop, "StreetCap_EastSouth",
                "SM_Bld_OfficeOld_Large_01", new Vector3(OutW1 + 5.5f, 0f, CitySouthCapFacade), 180f,
                CitySouthCapFacade, 8);

            BuildStreetEndScenes(backdrop);
        }

        static void PlaceStreetEndLandmark(Transform backdrop, string markerName,
            string prefabName, Vector3 position, float yaw, float facadeEdge, int variant)
        {
            var building = PlaceSynty(prefabName, position, yaw);
            if (building == null)
            {
                const float fallbackWidth = 16f;
                const float fallbackDepth = 14f;
                Vector3 min;
                Vector3 max;
                if (Mathf.Approximately(yaw, 90f))
                {
                    min = new Vector3(facadeEdge - fallbackDepth, 0f, position.z - fallbackWidth * 0.5f);
                    max = new Vector3(facadeEdge, 18f, position.z + fallbackWidth * 0.5f);
                }
                else if (Mathf.Approximately(yaw, 270f))
                {
                    min = new Vector3(facadeEdge, 0f, position.z - fallbackWidth * 0.5f);
                    max = new Vector3(facadeEdge + fallbackDepth, 18f, position.z + fallbackWidth * 0.5f);
                }
                else if (Mathf.Approximately(yaw, 180f))
                {
                    min = new Vector3(position.x - fallbackWidth * 0.5f, 0f, facadeEdge);
                    max = new Vector3(position.x + fallbackWidth * 0.5f, 18f, facadeEdge + fallbackDepth);
                }
                else
                {
                    min = new Vector3(position.x - fallbackWidth * 0.5f, 0f, facadeEdge - fallbackDepth);
                    max = new Vector3(position.x + fallbackWidth * 0.5f, 18f, facadeEdge);
                }

                building = BoxMinMax($"CompleteLandmark_{markerName}_Fallback",
                    min, max, backdrop, BuildingMat(variant % 2, 5f, 6f));
            }
            else
            {
                AlignFacadeEdge(building, yaw, facadeEdge);
                building.name = $"CompleteLandmark_{markerName}_{prefabName}";
                MakeEmissive(building);
                building.transform.SetParent(backdrop, true);
            }

            var bounds = GeometryBounds(building);
            string roofPropName = variant % 2 == 0
                ? "SM_Prop_Billboard_Roof_01"
                : "SM_Prop_Roof_Aircon_03";
            var roofProp = PlaceSynty(roofPropName,
                new Vector3(bounds.center.x, bounds.max.y + 0.02f, bounds.center.z),
                variant * 37f);
            if (roofProp != null)
            {
                roofProp.name = $"CompleteLandmarkRoof_{markerName}";
                roofProp.transform.SetParent(backdrop, true);
            }

            MarkStreetCap(backdrop, markerName, position);
        }

        static void BuildStreetEndScenes(Transform backdrop)
        {
            // Five-metre aprons bridge each road to its landmark. Four use Synty's
            // complete construction-sidewalk prefab; the others are compact transit
            // or food scenes. No raised strip crosses a carriageway.
            var capWalk = SidewalkMat(5f, 12f);
            BoxMinMax("CapWalk_MainWest", new Vector3(CityWestCapFacade, 0f, -13f),
                new Vector3(CityWestEnd, 0.12f, -1f), backdrop, capWalk);
            BoxMinMax("CapWalk_MainEast", new Vector3(CityEastEnd, 0f, -13f),
                new Vector3(CityEastCapFacade, 0.12f, -1f), backdrop, capWalk);
            BoxMinMax("CapWalk_RearWest", new Vector3(CityWestCapFacade, 0f, OutD1 + 1f),
                new Vector3(CityWestEnd, 0.12f, OutD1 + 10f), backdrop, capWalk);
            BoxMinMax("CapWalk_RearEast", new Vector3(CityEastEnd, 0f, OutD1 + 1f),
                new Vector3(CityEastCapFacade, 0.12f, OutD1 + 10f), backdrop, capWalk);
            BoxMinMax("CapWalk_WestNorth", new Vector3(OutW0 - 12f, 0f, CityNorthCapFacade),
                new Vector3(OutW0 + 1f, 0.12f, CityNorthEnd), backdrop, capWalk);
            BoxMinMax("CapWalk_WestSouth", new Vector3(OutW0 - 12f, 0f, CitySouthEnd),
                new Vector3(OutW0 + 1f, 0.12f, CitySouthCapFacade), backdrop, capWalk);
            BoxMinMax("CapWalk_EastNorth", new Vector3(OutW1 - 1f, 0f, CityNorthCapFacade),
                new Vector3(OutW1 + 12f, 0.12f, CityNorthEnd), backdrop, capWalk);
            BoxMinMax("CapWalk_EastSouth", new Vector3(OutW1 - 1f, 0f, CitySouthEnd),
                new Vector3(OutW1 + 12f, 0.12f, CitySouthCapFacade), backdrop, capWalk);

            PlaceBackdropProp(backdrop, "SM_Env_Sidewalk_Construction_01",
                new Vector3(-36.5f, 0.12f, -7f), 90f);
            PlaceBackdropProp(backdrop, "SM_Env_Sidewalk_Construction_01",
                new Vector3(52.5f, 0.12f, OutD1 + 5.5f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_BusStop_01",
                new Vector3(52.4f, 0.12f, -7f), 90f);
            PlaceBackdropProp(backdrop, "SM_Prop_HotdogStand_01",
                new Vector3(-36.5f, 0.12f, OutD1 + 5.5f), 90f);
        }

        static void MarkStreetCap(Transform backdrop, string name, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(backdrop, false);
            marker.position = position;
        }

        static void BuildAuthoredCitySkyline(Transform backdrop)
        {
            // Synty's own 476 m skyline was built specifically to close distant
            // horizons. At 55% scale it remains beyond the authored street caps but
            // inside the review camera/fog range.
            var skyline = PlaceSyntyDecorative("SM_Env_Skyline_01", new Vector3(W * 0.5f, -1.5f, 8f));
            if (skyline != null)
            {
                skyline.name = "SyntyAuthoredSkyline";
                skyline.transform.localScale = Vector3.one * 0.55f;
                DisableShadows(skyline);
                skyline.transform.SetParent(backdrop, true);
            }

            // Near landmarks are authored by BuildStreetEndLandmarks. Duplicating
            // another loose ring of towers here caused intersections and exposed
            // isolated silhouettes. The large Synty skyline remains the far layer.
        }

        static void AlignFacadeEdge(GameObject building, float yaw, float facadeEdge)
        {
            var bounds = GeometryBounds(building);
            if (Mathf.Approximately(yaw, 180f))
                building.transform.position += Vector3.forward * (facadeEdge - bounds.min.z);
            else if (Mathf.Approximately(yaw, 270f))
                building.transform.position += Vector3.right * (facadeEdge - bounds.min.x);
            else if (Mathf.Approximately(yaw, 90f))
                building.transform.position += Vector3.right * (facadeEdge - bounds.max.x);
            else
                building.transform.position += Vector3.forward * (facadeEdge - bounds.max.z);
        }

        static void BuildLowPolyClouds(Transform backdrop)
        {
            var cloudMaterial = Mat("MoonlitCityCloud", new Color(0.16f, 0.20f, 0.31f), 0.04f);
            var clouds = new[]
            {
                ("SM_Env_Cloud_01", new Vector3(-34f, 22f, -48f), 12f),
                ("SM_Env_Cloud_02", new Vector3(8f, 27f, -55f), 194f),
                ("SM_Env_Cloud_03", new Vector3(48f, 21f, -38f), 328f),
                ("SM_Env_Cloud_02", new Vector3(-42f, 25f, 58f), 52f),
                ("SM_Env_Cloud_03", new Vector3(10f, 30f, 66f), 211f),
                ("SM_Env_Cloud_01", new Vector3(52f, 23f, 48f), 306f)
            };

            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = PlaceSyntyDecorative(clouds[i].Item1, clouds[i].Item2, clouds[i].Item3);
                if (cloud == null) continue;
                cloud.name = $"CityCloud_{i:00}";
                ApplyMat(cloud, cloudMaterial);
                DisableShadows(cloud);
                cloud.transform.SetParent(backdrop, true);
            }
        }

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
                var head = FindLampHeadBounds(pole);
                // Downward SPOT, not a point: lamps make cones and pools, not a wash
                var lamp = new GameObject($"LampSpot_{x}").AddComponent<Light>();
                lamp.transform.SetParent(pole.transform, true);
                lamp.type = LightType.Spot;
                lamp.transform.position = LampUnderside(head);
                lamp.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                lamp.spotAngle = 100f;
                lamp.innerSpotAngle = 45f;
                lamp.color = ExteriorPracticalColor;
                lamp.intensity = 5.5f;
                lamp.range = 9f;
            }

            // Parked cars down the road, outside the boundary
            PlaceSynty("SM_Veh_Car_Van_01", new Vector3(-28f, 0.02f, -3.9f), 272f);
            PlaceSynty("SM_Veh_Car_Taxi_01", new Vector3(W + 27f, 0.02f, -4f), 268f);
            PlaceSynty("SM_Veh_Car_Small_01", new Vector3(-7f, 0.02f, -9.5f), 270f);
            PlaceSynty("SM_Veh_Car_Medium_01", new Vector3(W + 10f, 0.02f, -9.4f), 88f);

            PlaceSynty("SM_Prop_BusStop_01", new Vector3(-17f, 0.12f, OutD0 - 0.72f));

            // Opposite pavement: an urban sequence that carries beyond both frame
            // edges. Large objects stay well separated; small curb fixtures fill the
            // visual gaps without spilling into either traffic lane.
            float mainStreetFurnitureZ = OutD0 - 0.68f;
            PlaceSynty("SM_Prop_Planter_01", new Vector3(-32.5f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Mailbox_01", new Vector3(-31f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Newspaper_02", new Vector3(-25f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Trashbin_01", new Vector3(-22f, 0.12f, mainStreetFurnitureZ), 15f);
            PlaceSynty("SM_Prop_Hydrant_01", new Vector3(-6f, 0.12f, mainStreetFurnitureZ), 25f);
            PlaceSynty("SM_Prop_Phones_01", new Vector3(-3.5f, 0.12f, mainStreetFurnitureZ));
            PlaceSynty("SM_Prop_Planter_02", new Vector3(W + 1f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Newspaper_02", new Vector3(W + 6f, 0.12f, mainStreetFurnitureZ), 180f);
            PlaceSynty("SM_Prop_Hydrant_01", new Vector3(W + 15f, 0.12f, mainStreetFurnitureZ), 335f);
            PlaceSynty("SM_Prop_Cafe_Sign_Outdoor_01", new Vector3(W + 19f, 0.12f, mainStreetFurnitureZ), 165f);
            PlaceSynty("SM_Prop_Rubbish_Bin_02", new Vector3(W + 21f, 0.12f, mainStreetFurnitureZ), 15f);
            PlaceSynty("SM_Prop_ParkBench_01", new Vector3(W + 31f, 0.12f, mainStreetFurnitureZ), 180f);

            foreach (float x in new[] { -33.5f, -26f, -17f, -6.5f, 0f, W + 5f, W + 20f, W + 27f, 48.5f })
                PlaceSynty("SM_Prop_ParkingMeter_01", new Vector3(x, 0.12f, -12.22f), 180f);

            // Small storefront neons punctuate the street ring. They are kept across
            // the road or beyond the playable fence, so they add depth without noise
            // around the interaction route.
            var westNeon = PlaceSyntyDecorative("SM_Sign_Neon_Open_01", new Vector3(-18f, 1.35f, OutD0 - 1.15f));
            var eastNeon = PlaceSyntyDecorative("SM_Sign_Neon_Open_02", new Vector3(W + 24f, 1.45f, OutD0 - 1.15f));
            var rearNeon = PlaceSyntyDecorative("SM_Sign_Neon_Open_04", new Vector3(7.2f, 1.45f, OutD1 + 11.75f), 180f);
            if (westNeon != null) westNeon.name = "WestNeonFixture";
            if (eastNeon != null) eastNeon.name = "EastNeonFixture";
            if (rearNeon != null) rearNeon.name = "RearNeonFixture";
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
            foreach (float x in new[] { -28f, W + 20f, W + 28f })
                PlaceSynty(trees[t++ % 3], new Vector3(x, 0.12f, OutD0 - 0.72f), t * 77f);
            PlaceSynty(trees[0], new Vector3(-22f, 0.12f, -0.42f), 30f);
            PlaceSynty(trees[1], new Vector3(W + 22f, 0.12f, -0.42f), 130f);

            // Rooftop clutter on OUR roof — cover for the roof route (skylight at x 6.5-7.5, z 10-11)
            PlaceSynty("SM_Prop_Vents_Straight_01", new Vector3(3.5f, H + 0.1f, 6f), 20f);
            PlaceSynty("SM_Prop_Vents_Corner_01", new Vector3(10.5f, H + 0.1f, 13f));
            var roofExhaust = PlaceSynty("SM_Prop_Vents_Exhaust_01", new Vector3(11.5f, H + 0.1f, 4f), 90f);
            if (roofExhaust != null)
            {
                var exhaustBounds = GeometryBounds(roofExhaust);
                var smoke = PlacePreferredSyntyFx("FX_Smoke_White_01", "FX_Smoke_01",
                    new Vector3(exhaustBounds.center.x, exhaustBounds.max.y - 0.05f, exhaustBounds.center.z),
                    0f, 0.32f);
                if (smoke != null) smoke.name = "RoofExhaustSmoke";
            }
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

        // Billboard_Roof_01 is the scaffold; the selected sign prefab supplies its
        // ad face. Exterior.cs also uses this for the west neighbour roof.
        static void RoofBillboard(Vector3 roofPos, float yaw, int variant)
        {
            var holder = PlaceSynty("SM_Prop_Billboard_Roof_01", roofPos, yaw);
            if (holder == null) return;
            DisableShadows(holder);
            var holderBounds = GeometryBounds(holder);
            var facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float extent = Mathf.Abs(facing.x) > 0.5f
                ? holderBounds.extents.x
                : holderBounds.extents.z;
            var facePosition = new Vector3(holderBounds.center.x, 0f, holderBounds.center.z)
                + facing * (extent - 0.25f);
            var sign = PlaceSynty($"SM_Prop_Billboard_Sign_0{1 + variant % 7}",
                new Vector3(facePosition.x, holderBounds.min.y + 0.85f, facePosition.z), yaw);
            if (sign == null) return;
            MakeEmissive(sign);
            DisableShadows(sign);
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
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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
            // Without an emissive GI flag, URP validation treats emission as disabled
            // and strips _EMISSION from the saved asset on the next revalidation.
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

    }
}
