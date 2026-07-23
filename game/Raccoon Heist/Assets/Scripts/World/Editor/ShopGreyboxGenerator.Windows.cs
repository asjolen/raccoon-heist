using UnityEditor;
using UnityEngine;

namespace RaccoonHeist.World.Editor
{
    // Playable windows are real collision boundaries. Pressing the same interaction
    // key as a breakable vent swaps the intact pane for lightweight physical shards,
    // plays a localized recording, and opens the route.
    public static partial class ShopGreyboxGenerator
    {
        const string GlassAudioFolder = "Assets/Audio/Environment/Glass";

        static GameObject BuildBreakableWindowPane(string suffix, Vector3 min, Vector3 max,
            Material glass, int audioVariant)
        {
            Vector3 size = max - min;
            Vector3 center = (min + max) * 0.5f;
            var pane = new GameObject($"BreakableWindow_{suffix}");
            pane.transform.SetParent(root, false);
            pane.transform.position = center;

            var intact = Box("IntactGlass", Vector3.zero, size, pane.transform, glass);
            Object.DestroyImmediate(intact.GetComponent<Collider>());
            DisableShadows(intact);
            BuildGlassHighlights(intact.transform, pane.transform, size);

            var blocker = pane.AddComponent<BoxCollider>();
            blocker.size = new Vector3(size.x, size.y, Mathf.Max(0.08f, size.z));

            BuildGlassFragments(pane.transform, size, glass);

            int variant = Mathf.Clamp(audioVariant, 1, 4);
            var source = pane.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"{GlassAudioFolder}/glass_break_{variant:00}.mp3");
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.88f;
            source.spatialBlend = 1f;
            source.minDistance = 0.65f;
            source.maxDistance = 12f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.priority = 103;
            return pane;
        }

        static void BuildGlassHighlights(Transform intactGlass, Transform pane, Vector3 paneSize)
        {
            var highlight = TransparentMat("BreakableGlassHighlight",
                new Color(0.30f, 0.58f, 0.78f, 0.19f), 0.94f);
            var broad = Box("GlassHighlight_Broad",
                new Vector3(-paneSize.x * 0.16f, paneSize.y * 0.04f, 0f),
                new Vector3(Mathf.Max(0.055f, paneSize.x * 0.11f), paneSize.y * 0.78f,
                    Mathf.Max(0.024f, paneSize.z + 0.006f)),
                pane, highlight);
            broad.transform.localRotation = Quaternion.Euler(0f, 0f, -13f);
            Object.DestroyImmediate(broad.GetComponent<Collider>());
            DisableShadows(broad);
            broad.transform.SetParent(intactGlass, true);

            var narrow = Box("GlassHighlight_Narrow",
                new Vector3(paneSize.x * 0.04f, -paneSize.y * 0.10f, 0f),
                new Vector3(Mathf.Max(0.026f, paneSize.x * 0.045f), paneSize.y * 0.42f,
                    Mathf.Max(0.025f, paneSize.z + 0.007f)),
                pane, highlight);
            narrow.transform.localRotation = Quaternion.Euler(0f, 0f, -13f);
            Object.DestroyImmediate(narrow.GetComponent<Collider>());
            DisableShadows(narrow);
            narrow.transform.SetParent(intactGlass, true);
        }

        static void BuildGlassFragments(Transform pane, Vector3 paneSize, Material glass)
        {
            var fragments = new GameObject("GlassBreakFragments").transform;
            fragments.SetParent(pane, false);

            int columns = Mathf.Clamp(Mathf.CeilToInt(paneSize.x / 0.52f), 2, 6);
            const int rows = 3;
            float cellWidth = paneSize.x / columns;
            float cellHeight = paneSize.y / rows;
            float shardDepth = Mathf.Max(0.022f, paneSize.z);
            int shardIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float x = -paneSize.x * 0.5f + cellWidth * (column + 0.5f);
                    float y = -paneSize.y * 0.5f + cellHeight * (row + 0.5f);
                    float widthVariation = 0.72f + Mathf.Repeat(shardIndex * 0.173f, 0.22f);
                    float heightVariation = 0.73f + Mathf.Repeat(shardIndex * 0.127f, 0.20f);
                    var shard = Box($"GlassShard_{shardIndex:00}", new Vector3(x, y, 0f),
                        new Vector3(cellWidth * widthVariation, cellHeight * heightVariation, shardDepth),
                        fragments, glass);
                    shard.transform.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Sin(shardIndex * 1.71f) * 8f);
                    DisableShadows(shard);
                    shardIndex++;
                }
            }

            fragments.gameObject.SetActive(false);
        }

        static void ValidateBreakableWindows()
        {
            int paneCount = 0;
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!candidate.name.StartsWith("BreakableWindow_")) continue;
                paneCount++;
                var fragments = candidate.Find("GlassBreakFragments");
                if (candidate.GetComponent<BoxCollider>() == null
                    || candidate.GetComponent<AudioSource>()?.clip == null
                    || candidate.Find("IntactGlass") == null
                    || fragments == null
                    || fragments.childCount < 6)
                    Debug.LogError($"Raccoon Heist: {candidate.name} is missing collision, shards, or localized glass audio.");
            }

            if (paneCount != 5)
                Debug.LogError($"Raccoon Heist: expected five playable breakable panes, generated {paneCount}.");
            else
                Debug.Log("Raccoon Heist: validated five solid breakable panes with localized glass audio.");
        }
    }
}
