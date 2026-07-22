using System.Text;
using UnityEditor;
using UnityEngine;

namespace RaccoonHeist.World.Editor
{
    // Dumps real bounds data for every Synty prefab so environment placement can be
    // driven by measurements instead of guesses. Output: SyntyReport.txt (project root).
    public static class SyntyAssetReport
    {
        // Resets ALL Synty materials to no-emission baseline. Selective night glow is
        // applied per-instance by the shop generator (emissive material variants), so
        // buildings can glow while parked cars and closed shopfronts stay dark.
        [MenuItem("Raccoon Heist/Reset Synty Emission")]
        public static void ResetSyntyEmission()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:material", new[] { "Assets/Synty" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || !mat.HasProperty("_Emission_Map")) continue;
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_Enable_Emission")) mat.SetFloat("_Enable_Emission", 0f);
                if (mat.HasProperty("_Emission_Color")) mat.SetColor("_Emission_Color", Color.black);
                EditorUtility.SetDirty(mat);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Raccoon Heist: reset emission on {changed} Synty materials to baseline.");
        }

        [MenuItem("Raccoon Heist/Dump Synty Asset Report")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# name | sizeX | sizeY | sizeZ | pivotToBottomY | centerOffsetX | centerOffsetZ");

            var guids = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Synty" });
            int measured = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var b = Bounds(go);
                Object.DestroyImmediate(go);
                if (b.size == Vector3.zero) continue;

                sb.AppendLine($"{prefab.name}|{b.size.x:F2}|{b.size.y:F2}|{b.size.z:F2}|{(-b.min.y):F2}|{b.center.x:F2}|{b.center.z:F2}");
                measured++;
            }

            string outPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath)!, "SyntyReport.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"Raccoon Heist: measured {measured} Synty prefabs -> {outPath}");
        }

        static Bounds Bounds(GameObject go)
        {
            var bounds = new Bounds();
            bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var world = r.transform.TransformPoint(corner);
                    if (!has) { bounds = new Bounds(world, Vector3.zero); has = true; }
                    else bounds.Encapsulate(world);
                }
            }
            return has ? bounds : new Bounds(Vector3.zero, Vector3.zero);
        }
    }
}
