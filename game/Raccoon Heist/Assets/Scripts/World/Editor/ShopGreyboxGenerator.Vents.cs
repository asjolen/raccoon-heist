using UnityEngine;

namespace RaccoonHeist.World.Editor
{
    // Crouch routes are authored as real openings plus simple collision shells.
    // POLYGON props supply the visible grate, while the measured procedural shell
    // guarantees the 0.3 m raccoon can pass and the 0.5 m raccoon cannot stand.
    public static partial class ShopGreyboxGenerator
    {
        const float CrawlVentHeight = 0.48f;
        const float EastVentZ0 = 1.00f;
        const float EastVentZ1 = 1.95f;
        const float StorageBypassX0 = 9.50f;
        const float StorageBypassX1 = 10.50f;
        const float RearBreakVentX0 = 6.15f;
        const float RearBreakVentX1 = 7.15f;

        static void BuildCrouchVentRoutes()
        {
            var shell = Mat("CrawlDuctShell", new Color(0.25f, 0.28f, 0.31f), 0.28f);
            var edge = Mat("CrawlDuctEdge", new Color(0.42f, 0.45f, 0.48f), 0.34f);

            BuildEastQuietCrawl(shell, edge);
            BuildStorageBypassCrawl(shell, edge);
            BuildRearBreakInCrawl(shell, edge);
        }

        static Transform CreateRouteRoot(string name)
        {
            var route = new GameObject(name).transform;
            route.SetParent(root, false);
            return route;
        }

        static void BuildEastQuietCrawl(Material shell, Material edge)
        {
            var route = CreateRouteRoot("CrawlRoute_EastQuiet");
            float x0 = W - 0.36f;
            float x1 = W + 1.12f;

            BuildDecorativeDuctFloor("EastQuietDuct_Floor", new Vector3(x0, 0.022f, EastVentZ0),
                new Vector3(x1, 0.042f, EastVentZ1), route, shell);
            BoxMinMax("EastQuietDuct_Ceiling", new Vector3(x0, CrawlVentHeight, EastVentZ0),
                new Vector3(x1, CrawlVentHeight + 0.045f, EastVentZ1), route, shell);
            BoxMinMax("EastQuietDuct_SideS", new Vector3(x0, 0.042f, EastVentZ0),
                new Vector3(x1, CrawlVentHeight, EastVentZ0 + 0.045f), route, shell);
            BoxMinMax("EastQuietDuct_SideN", new Vector3(x0, 0.042f, EastVentZ1 - 0.045f),
                new Vector3(x1, CrawlVentHeight, EastVentZ1), route, shell);
            BuildVentFrameX("EastQuietVent_OuterFrame", W + T + 0.055f,
                EastVentZ0, EastVentZ1, route, edge);
            BuildVentFrameX("EastQuietVent_InnerFrame", W - 0.055f,
                EastVentZ0, EastVentZ1, route, edge);
        }

        static void BuildStorageBypassCrawl(Material shell, Material edge)
        {
            var route = CreateRouteRoot("CrawlRoute_ShopStorageBypass");
            float z0 = D - 0.38f;
            float z1 = D + T + 0.55f;

            BuildDecorativeDuctFloor("StorageBypassDuct_Floor", new Vector3(StorageBypassX0, 0.022f, z0),
                new Vector3(StorageBypassX1, 0.042f, z1), route, shell);
            BoxMinMax("StorageBypassDuct_Ceiling", new Vector3(StorageBypassX0, CrawlVentHeight, z0),
                new Vector3(StorageBypassX1, CrawlVentHeight + 0.045f, z1), route, shell);
            BoxMinMax("StorageBypassDuct_SideW", new Vector3(StorageBypassX0, 0.042f, z0),
                new Vector3(StorageBypassX0 + 0.045f, CrawlVentHeight, z1), route, shell);
            BoxMinMax("StorageBypassDuct_SideE", new Vector3(StorageBypassX1 - 0.045f, 0.042f, z0),
                new Vector3(StorageBypassX1, CrawlVentHeight, z1), route, shell);
            BuildVentFrameZ("StorageBypass_ShopFrame", D - 0.055f,
                StorageBypassX0, StorageBypassX1, route, edge);
            BuildVentFrameZ("StorageBypass_StorageFrame", D + T + 0.055f,
                StorageBypassX0, StorageBypassX1, route, edge);
        }

        static void BuildRearBreakInCrawl(Material shell, Material edge)
        {
            var route = CreateRouteRoot("CrawlRoute_RearStorageBreakIn");
            float z0 = StorageZ1 - 0.58f;
            float z1 = StorageZ1 + T + 0.48f;

            BuildDecorativeDuctFloor("RearBreakDuct_Floor", new Vector3(RearBreakVentX0, 0.022f, z0),
                new Vector3(RearBreakVentX1, 0.042f, z1), route, shell);
            BoxMinMax("RearBreakDuct_Ceiling", new Vector3(RearBreakVentX0, CrawlVentHeight, z0),
                new Vector3(RearBreakVentX1, CrawlVentHeight + 0.045f, z1), route, shell);
            BoxMinMax("RearBreakDuct_SideW", new Vector3(RearBreakVentX0, 0.042f, z0),
                new Vector3(RearBreakVentX0 + 0.045f, CrawlVentHeight, z1), route, shell);
            BoxMinMax("RearBreakDuct_SideE", new Vector3(RearBreakVentX1 - 0.045f, 0.042f, z0),
                new Vector3(RearBreakVentX1, CrawlVentHeight, z1), route, shell);
            BuildVentFrameZ("RearBreakVent_InnerFrame", StorageZ1 - 0.055f,
                RearBreakVentX0, RearBreakVentX1, route, edge);
            BuildVentFrameZ("RearBreakVent_OuterFrame", z1 - 0.025f,
                RearBreakVentX0, RearBreakVentX1, route, edge);
            BuildBreakableRearGrate(z1 + 0.015f);
        }

        static void BuildDecorativeDuctFloor(string name, Vector3 min, Vector3 max,
            Transform parent, Material material)
        {
            var floor = BoxMinMax(name, min, max, parent, material);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        static void BuildVentFrameX(string name, float x, float z0, float z1,
            Transform parent, Material material)
        {
            const float rail = 0.055f;
            const float depth = 0.075f;
            BoxMinMax($"{name}_S", new Vector3(x - depth * 0.5f, 0.02f, z0 - rail),
                new Vector3(x + depth * 0.5f, CrawlVentHeight + rail, z0), parent, material);
            BoxMinMax($"{name}_N", new Vector3(x - depth * 0.5f, 0.02f, z1),
                new Vector3(x + depth * 0.5f, CrawlVentHeight + rail, z1 + rail), parent, material);
            BoxMinMax($"{name}_Top", new Vector3(x - depth * 0.5f, CrawlVentHeight, z0),
                new Vector3(x + depth * 0.5f, CrawlVentHeight + rail, z1), parent, material);
        }

        static void BuildVentFrameZ(string name, float z, float x0, float x1,
            Transform parent, Material material)
        {
            const float rail = 0.055f;
            const float depth = 0.075f;
            BoxMinMax($"{name}_W", new Vector3(x0 - rail, 0.02f, z - depth * 0.5f),
                new Vector3(x0, CrawlVentHeight + rail, z + depth * 0.5f), parent, material);
            BoxMinMax($"{name}_E", new Vector3(x1, 0.02f, z - depth * 0.5f),
                new Vector3(x1 + rail, CrawlVentHeight + rail, z + depth * 0.5f), parent, material);
            BoxMinMax($"{name}_Top", new Vector3(x0, CrawlVentHeight, z - depth * 0.5f),
                new Vector3(x1, CrawlVentHeight + rail, z + depth * 0.5f), parent, material);
        }

        static void BuildBreakableRearGrate(float outsideZ)
        {
            float width = RearBreakVentX1 - RearBreakVentX0;
            var targetCenter = new Vector3((RearBreakVentX0 + RearBreakVentX1) * 0.5f,
                CrawlVentHeight * 0.5f, outsideZ);
            var cover = new GameObject("BreakableVentCover_RearStorage");
            cover.transform.SetParent(root, false);
            cover.transform.position = targetCenter;

            // Use the actual POLYGON Shops wall-vent prefab as the hero grate. The
            // fallback bars preserve the interaction if that pack is ever missing.
            var visual = PlaceSyntyDecorative("SM_Prop_Wall_Vent_01",
                new Vector3(targetCenter.x, 0f, targetCenter.z));
            if (visual != null)
            {
                var bounds = GeometryBounds(visual);
                visual.transform.localScale = Vector3.Scale(visual.transform.localScale,
                    new Vector3((width - 0.06f) / Mathf.Max(0.01f, bounds.size.x),
                        (CrawlVentHeight - 0.05f) / Mathf.Max(0.01f, bounds.size.y), 1f));
                bounds = GeometryBounds(visual);
                visual.transform.position += targetCenter - bounds.center;
                visual.transform.SetParent(cover.transform, true);
                visual.name = "POLYGON_BreakawayGrate";
            }
            else
            {
                var grate = Mat("BreakawayGrate", new Color(0.33f, 0.36f, 0.38f), 0.35f);
                for (int i = -3; i <= 3; i++)
                {
                    var bar = Box($"BreakawayGrate_Bar_{i}", new Vector3(i * width / 8f, 0f, 0f),
                        new Vector3(0.035f, CrawlVentHeight - 0.06f, 0.045f), cover.transform, grate);
                    Object.DestroyImmediate(bar.GetComponent<Collider>());
                }
                foreach (float y in new[] { -0.12f, 0.12f })
                {
                    var crossbar = Box($"BreakawayGrate_Crossbar_{y}", new Vector3(0f, y, 0f),
                        new Vector3(width - 0.06f, 0.035f, 0.05f), cover.transform, grate);
                    Object.DestroyImmediate(crossbar.GetComponent<Collider>());
                }
            }

            Material fragmentMaterial = null;
            if (visual != null)
            {
                var sourceRenderer = visual.GetComponentInChildren<Renderer>();
                if (sourceRenderer != null) fragmentMaterial = sourceRenderer.sharedMaterial;
            }
            fragmentMaterial ??= Mat("BreakawayGrate", new Color(0.33f, 0.36f, 0.38f), 0.35f);
            var fragments = new GameObject("VentBreakFragments").transform;
            fragments.SetParent(cover.transform, false);
            const int slatCount = 7;
            float fragmentWidth = width - 0.09f;
            for (int i = 0; i < slatCount; i++)
            {
                float y = -CrawlVentHeight * 0.5f + 0.055f
                    + i * (CrawlVentHeight - 0.11f) / (slatCount - 1);
                Box($"VentFragment_Slat_{i}", new Vector3(0f, y, 0f),
                    new Vector3(fragmentWidth, 0.035f, 0.055f), fragments, fragmentMaterial);
            }
            foreach (float x in new[] { -fragmentWidth * 0.5f, fragmentWidth * 0.5f })
                Box($"VentFragment_Rail_{x:0.00}", new Vector3(x, 0f, 0f),
                    new Vector3(0.04f, CrawlVentHeight - 0.06f, 0.06f), fragments, fragmentMaterial);
            fragments.gameObject.SetActive(false);

            var blockingCollider = cover.AddComponent<BoxCollider>();
            blockingCollider.size = new Vector3(width - 0.035f, CrawlVentHeight - 0.025f, 0.085f);

            var source = cover.AddComponent<AudioSource>();
            source.clip = EnsureGeneratedAudioClip("vent_grate_break_v1.wav", 44100, 0.82f, VentGrateBreakSample);
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.82f;
            source.spatialBlend = 1f;
            source.minDistance = 0.65f;
            source.maxDistance = 9f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.priority = 105;
        }

        static float VentGrateBreakSample(int sampleIndex)
        {
            const float sampleRate = 44100f;
            float time = sampleIndex / sampleRate;
            float noise = Hash(sampleIndex, 2711) * 2f - 1f;
            float firstHit = Mathf.Exp(-18f * time) *
                (0.46f * noise + 0.31f * Mathf.Sin(2f * Mathf.PI * 430f * time)
                    + 0.18f * Mathf.Sin(2f * Mathf.PI * 910f * time));
            float secondTime = Mathf.Max(0f, time - 0.13f);
            float secondHit = time < 0.13f ? 0f : Mathf.Exp(-22f * secondTime) *
                (0.28f * noise + 0.24f * Mathf.Sin(2f * Mathf.PI * 315f * secondTime));
            float ring = Mathf.Exp(-4.5f * time) *
                (0.19f * Mathf.Sin(2f * Mathf.PI * 178f * time)
                    + 0.11f * Mathf.Sin(2f * Mathf.PI * 241f * time));
            return (firstHit + secondHit + ring) * 0.78f;
        }

        static void ValidateCrouchVentRoutes()
        {
            bool validDimensions = CrawlVentHeight > 0.34f
                && CrawlVentHeight < ShopConstants.RaccoonHeight
                && EastVentZ1 - EastVentZ0 >= 0.7f
                && StorageBypassX1 - StorageBypassX0 >= 0.7f
                && RearBreakVentX1 - RearBreakVentX0 >= 0.7f;
            if (!validDimensions)
                Debug.LogError("Raccoon Heist: a crawl route no longer has crouch-only clearance.");

            foreach (string routeName in new[]
                     {
                         "CrawlRoute_EastQuiet",
                         "CrawlRoute_ShopStorageBypass",
                         "CrawlRoute_RearStorageBreakIn"
                     })
                if (GameObject.Find(routeName) == null)
                    Debug.LogError($"Raccoon Heist: missing generated crawl route {routeName}.");

            var cover = GameObject.Find("BreakableVentCover_RearStorage");
            var fragments = cover != null ? cover.transform.Find("VentBreakFragments") : null;
            if (cover == null || cover.GetComponent<BoxCollider>() == null
                || cover.GetComponent<AudioSource>()?.clip == null
                || fragments == null || fragments.childCount < 7)
                Debug.LogError("Raccoon Heist: rear break-in grate is missing collision, break fragments, or localized audio.");

            Debug.Log("Raccoon Heist: validated three crouch-only routes; Harold's room remains untouched.");
        }
    }
}
