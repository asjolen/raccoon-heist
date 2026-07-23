using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RaccoonHeist.World.Editor
{
    // Diagnostic for the recurring "environment turns light/dark" bug.
    // Drop PlayModeRoundtrip.once at the project root: the editor captures
    // previews and a lighting-state dump, plays for a few seconds, exits, then
    // captures both again. Comparing the pre/post dumps pins down exactly which
    // lighting variable drifts across the edit -> play -> edit roundtrip.
    public static class LightingDriftProbe
    {
        static string MarkerPath => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "PlayModeRoundtrip.once");
        const float PlaySeconds = 4f;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (!File.Exists(MarkerPath)) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            string stage = File.ReadAllText(MarkerPath).Trim();
            if (stage == "" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () =>
                {
                    DumpState("/tmp/RH_state_preplay.txt");
                    ShopGreyboxGenerator.CaptureEnvironmentPreviews();
                    foreach (var file in Directory.GetFiles("/tmp", "RaccoonHeist_*.png"))
                        File.Copy(file, "/tmp/RH_preplay_" + Path.GetFileName(file), true);
                    File.WriteAllText(MarkerPath, "played");
                    EditorApplication.EnterPlaymode();
                };
            }
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!File.Exists(MarkerPath) || File.ReadAllText(MarkerPath).Trim() != "played") return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                double exitAt = EditorApplication.timeSinceStartup + PlaySeconds;
                void Tick()
                {
                    if (EditorApplication.timeSinceStartup < exitAt) return;
                    EditorApplication.update -= Tick;
                    DumpState("/tmp/RH_state_inplay.txt");
                    EditorApplication.ExitPlaymode();
                }
                EditorApplication.update += Tick;
                return;
            }
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.delayCall += () =>
            {
                DumpState("/tmp/RH_state_postplay.txt");
                ShopGreyboxGenerator.CaptureEnvironmentPreviews();
                File.Delete(MarkerPath);
                Debug.Log("LightingDriftProbe: roundtrip complete — compare /tmp/RH_preplay_* vs /tmp/RaccoonHeist_* and the RH_state_* dumps.");
            };
        }

        static void DumpState(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ambientMode={RenderSettings.ambientMode}");
            sb.AppendLine($"ambientSky={RenderSettings.ambientSkyColor}");
            sb.AppendLine($"ambientEquator={RenderSettings.ambientEquatorColor}");
            sb.AppendLine($"ambientGround={RenderSettings.ambientGroundColor}");
            sb.AppendLine($"ambientIntensity={RenderSettings.ambientIntensity}");
            sb.AppendLine($"fog={RenderSettings.fog} mode={RenderSettings.fogMode} color={RenderSettings.fogColor} density={RenderSettings.fogDensity}");
            sb.AppendLine($"skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")}");
            sb.AppendLine($"skyboxExposure={(RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure") ? RenderSettings.skybox.GetFloat("_Exposure").ToString() : "n/a")}");
            sb.AppendLine($"sun={(RenderSettings.sun != null ? RenderSettings.sun.name + " on=" + RenderSettings.sun.isActiveAndEnabled + " intensity=" + RenderSettings.sun.intensity : "null")}");
            sb.AppendLine($"reflectionMode={RenderSettings.defaultReflectionMode} reflectionIntensity={RenderSettings.reflectionIntensity}");
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"volume={volume.gameObject.name} goActive={volume.gameObject.activeInHierarchy} compEnabled={volume.enabled} global={volume.isGlobal} priority={volume.priority} weight={volume.weight} profile={(volume.sharedProfile != null ? volume.sharedProfile.name : "null")}");
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    sb.AppendLine($"directional={light.gameObject.name} active={light.isActiveAndEnabled} intensity={light.intensity} color={light.color}");
            File.WriteAllText(path, sb.ToString());
        }
    }
}

