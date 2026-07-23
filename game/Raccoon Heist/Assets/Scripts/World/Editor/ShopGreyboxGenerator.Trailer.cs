using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RaccoonHeist.World;

namespace RaccoonHeist.World.Editor
{
    // Editor-only cinematic tools. They use the real generated environment and
    // imported Poly Art raccoon without adding a second gameplay controller.
    public static partial class ShopGreyboxGenerator
    {
        const string RaccoonModelPath = "Assets/Models/RaccoonPoly/Raccoon PA.FBX";
        const string RaccoonTexturePath = "Assets/Models/RaccoonPoly/Textures/Rac-PA-Common.psd";
        const string RaccoonMaterialPath = "Assets/Models/RaccoonPoly/RaccoonPoly_URP.mat";
        const string RaccoonAnimationFolder = "Assets/Models/RaccoonPoly/Animations";
        const int TrailerWidth = 1280;
        const int TrailerHeight = 720;
        const int TrailerFps = 24;
        const float TrailerDuration = 36f;
        const string TrailerFrameFolder = "/tmp/RaccoonHeist_TrailerFrames";

        // One-shot renders are grounded and collision-validated before any frame is accepted.
        [InitializeOnLoadMethod]
        static void RunRequestedTrailerRender()
        {
            string marker = Path.Combine(Path.GetDirectoryName(Application.dataPath)!,
                "RenderTrailer.once");
            if (!File.Exists(marker)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.playModeStateChanged -= RenderTrailerWhenEditMode;
                    EditorApplication.playModeStateChanged += RenderTrailerWhenEditMode;
                    Debug.Log("Raccoon Heist: trailer render is waiting for Edit mode.");
                    return;
                }

                try
                {
                    RenderTrailerFrames();
                    File.Delete(marker);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        static void RenderTrailerWhenEditMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= RenderTrailerWhenEditMode;
            RunRequestedTrailerRender();
        }

        [MenuItem("Raccoon Heist/Trailer/Capture Poly Art Raccoon Test")]
        public static void CapturePolyArtRaccoonTest()
        {
            var actor = CreateCinematicRaccoon(new Vector3(7.1f, 0f, 21.8f), 180f,
                "Raccoon Sneak.FBX", "Rac_Sneak Forward", 0.42f);
            if (actor == null) return;

            try
            {
                CapturePreview("RaccoonPolyTest", new Vector3(10.2f, 0.72f, 24.1f),
                    new Vector3(7.1f, 0.42f, 21.8f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
            Debug.Log("Raccoon Heist: captured Poly Art raccoon test to /tmp/RaccoonHeist_RaccoonPolyTest.png.");
        }

        [MenuItem("Raccoon Heist/Trailer/Render 36 Second Trailer Frames")]
        public static void RenderTrailerFrames()
        {
            var sceneRoot = GameObject.Find("ShopGreybox");
            if (sceneRoot == null)
            {
                Debug.LogError("Raccoon Heist: generate the shop before rendering the trailer.");
                return;
            }

            Directory.CreateDirectory(TrailerFrameFolder);
            foreach (string oldFrame in Directory.GetFiles(TrailerFrameFolder, "frame_*.jpg"))
                File.Delete(oldFrame);

            var player = GameObject.Find("Raccoon");
            bool playerWasActive = player != null && player.activeSelf;
            if (playerWasActive) player.SetActive(false);

            var raccoon = CreateCinematicRaccoon(new Vector3(7.1f, 0f, 22f), 0f,
                "Raccoon Sneak.FBX", "Rac_Sneak Forward", 0f);
            if (raccoon == null)
            {
                if (playerWasActive) player.SetActive(true);
                return;
            }
            var raccoonVisual = raccoon.transform.Find("PolyArtVisual")?.gameObject;

            var harold = GameObject.Find("Harold");
            bool haroldWasActive = harold != null && harold.activeSelf;
            Vector3 haroldPosition = harold != null ? harold.transform.position : Vector3.zero;
            Quaternion haroldRotation = harold != null ? harold.transform.rotation : Quaternion.identity;
            var haroldAnimator = harold != null ? harold.GetComponentInChildren<Animator>(true) : null;
            var haroldWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Models/Harold/CorrectedClips/Harold_Walk.anim");

            var alarm = sceneRoot.GetComponentInChildren<ShopAlarmController>(true);
            var door = sceneRoot.transform.Find("EntranceDoorPivot");
            Quaternion doorRotation = door != null ? door.localRotation : Quaternion.identity;
            var beacons = new List<Transform>();
            foreach (var candidate in sceneRoot.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "AlarmBeacon_Front" || candidate.name == "AlarmBeacon_Rear")
                    beacons.Add(candidate);

            var cameraHolder = new GameObject("TrailerCamera");
            var camera = cameraHolder.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 180f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var actorLightHolder = new GameObject("TrailerActorRim");
            var actorLight = actorLightHolder.AddComponent<Light>();
            actorLight.type = LightType.Point;
            actorLight.color = new Color(0.56f, 0.70f, 1f);
            actorLight.intensity = 2.2f;
            actorLight.range = 4.5f;
            actorLight.shadows = LightShadows.None;

            var targetTexture = new RenderTexture(TrailerWidth, TrailerHeight, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            targetTexture.Create();
            camera.targetTexture = targetTexture;
            var image = new Texture2D(TrailerWidth, TrailerHeight, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            var particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>();
            foreach (var particle in particles)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.useAutoRandomSeed = false;
                particle.randomSeed = (uint)(Mathf.Abs(particle.name.GetHashCode()) + 1);
                particle.Simulate(0f, true, true, true);
            }

            bool alarmTriggered = false;
            AnimationMode.StartAnimationMode();
            try
            {
                int frameCount = Mathf.RoundToInt(TrailerDuration * TrailerFps);
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float time = frame / (float)TrailerFps;
                    ConfigureTrailerFrame(time, cameraHolder.transform, camera, raccoon,
                        raccoonVisual, actorLightHolder.transform, harold, haroldAnimator,
                        haroldWalk, alarm, door, beacons, ref alarmTriggered);
                    ApplyTrailerNeon(time);
                    foreach (var particle in particles)
                        particle.Simulate(1f / TrailerFps, true, false, false);

                    camera.Render();
                    RenderTexture.active = targetTexture;
                    image.ReadPixels(new Rect(0f, 0f, TrailerWidth, TrailerHeight), 0, 0, false);
                    image.Apply(false);
                    File.WriteAllBytes(
                        Path.Combine(TrailerFrameFolder, $"frame_{frame:00000}.jpg"),
                        image.EncodeToJPG(90));

                    if (frame % (TrailerFps * 2) == 0)
                        Debug.Log($"Raccoon Heist trailer: rendered {frame}/{frameCount} frames "
                            + $"({time:0}/{TrailerDuration:0}s).");
                }
                File.WriteAllText(Path.Combine(TrailerFrameFolder, "complete.txt"),
                    $"{frameCount} frames at {TrailerFps} fps");
                Debug.Log($"Raccoon Heist: rendered {frameCount} trailer frames to {TrailerFrameFolder}.");
            }
            finally
            {
                ApplyTrailerNeon(-1f);
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                if (alarm != null) alarm.ResetAlarm();
                if (door != null) door.localRotation = doorRotation;
                if (harold != null)
                {
                    harold.transform.SetPositionAndRotation(haroldPosition, haroldRotation);
                    harold.SetActive(haroldWasActive);
                }
                if (playerWasActive) player.SetActive(true);
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                targetTexture.Release();
                UnityEngine.Object.DestroyImmediate(targetTexture);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(actorLightHolder);
                UnityEngine.Object.DestroyImmediate(cameraHolder);
                UnityEngine.Object.DestroyImmediate(raccoon);
            }
        }

        public static void RenderTrailerFramesFromSavedScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            RenderTrailerFrames();
        }

        static void ConfigureTrailerFrame(
            float time,
            Transform cameraTransform,
            Camera camera,
            GameObject raccoon,
            GameObject raccoonVisual,
            Transform actorLight,
            GameObject harold,
            Animator haroldAnimator,
            AnimationClip haroldWalk,
            ShopAlarmController alarm,
            Transform door,
            List<Transform> beacons,
            ref bool alarmTriggered)
        {
            bool showRaccoon = false;
            bool showHarold = false;
            Vector3 cameraPosition;
            Vector3 cameraTarget;
            float fieldOfView = 58f;

            if (time < 4f)
            {
                float u = ShotProgress(time, 0f, 4f);
                cameraPosition = Vector3.Lerp(new Vector3(19f, 6.4f, -16f),
                    new Vector3(11.5f, 2.4f, -8.2f), u);
                cameraTarget = Vector3.Lerp(new Vector3(7f, 1.2f, 1.5f),
                    new Vector3(7f, 1.6f, 0.5f), u);
                fieldOfView = Mathf.Lerp(56f, 48f, u);
            }
            else if (time < 7f)
            {
                float u = ShotProgress(time, 4f, 7f);
                cameraPosition = Vector3.Lerp(new Vector3(11.4f, 2.5f, -6.3f),
                    new Vector3(8.8f, 2.0f, -4.1f), u);
                cameraTarget = new Vector3(7f, 2.55f, -0.05f);
                fieldOfView = 45f;
            }
            else if (time < 11f)
            {
                float u = ShotProgress(time, 7f, 11f);
                showRaccoon = true;
                // Stay in the rear alley. The old route crossed the storage wall
                // to fake a break-in and visibly sent the model through masonry.
                Vector3 start = new(8.0f, 0f, 22.8f);
                Vector3 end = new(6.65f, 0f, 22.8f);
                PlaceAnimatedRaccoon(raccoon, raccoonVisual, start, end, u,
                    "Raccoon Sneak.FBX", "Rac_Sneak Forward", time - 7f);
                cameraPosition = Vector3.Lerp(new Vector3(10.6f, 0.72f, 24.45f),
                    new Vector3(9.4f, 0.62f, 24.0f), u);
                cameraTarget = raccoon.transform.position + Vector3.up * 0.33f;
                fieldOfView = 52f;
            }
            else if (time < 14f)
            {
                float u = ShotProgress(time, 11f, 14f);
                showRaccoon = true;
                Vector3 start = new(15.8f, 0f, 4.5f);
                // Stop in front of the crawl duct; do not overlap its north rail.
                Vector3 end = new(15.55f, 0f, 2.30f);
                PlaceAnimatedRaccoon(raccoon, raccoonVisual, start, end, u,
                    "Raccoon Sneak.FBX", "Rac_Sneak Forward", time - 11f);
                cameraPosition = Vector3.Lerp(new Vector3(17.3f, 0.65f, 4.6f),
                    new Vector3(16.2f, 0.48f, 2.3f), u);
                cameraTarget = raccoon.transform.position + Vector3.up * 0.28f;
                fieldOfView = 52f;
            }
            else if (time < 18f)
            {
                float u = ShotProgress(time, 14f, 18f);
                showRaccoon = true;
                // Begin on the raised pavement instead of intersecting its curb.
                Vector3 start = new(7f, 0f, -1.65f);
                // Finish at the open doorway, then cut to the interior. Crossing
                // the 10 cm floor-height change in one sampled frame made the body
                // overlap the rear edge of the pavement.
                Vector3 end = new(7f, 0f, -0.30f);
                float entranceMove = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(15.45f, 18f, time));
                PlaceAnimatedRaccoon(raccoon, raccoonVisual, start, end, entranceMove,
                    "Raccoon Sneak.FBX", "Rac_Sneak Forward", time - 14f);
                cameraPosition = Vector3.Lerp(new Vector3(10.5f, 1.25f, -5.7f),
                    new Vector3(8.9f, 1.05f, -3.7f), u);
                cameraTarget = Vector3.Lerp(raccoon.transform.position + Vector3.up * 0.35f,
                    new Vector3(7f, 1.1f, 0.1f), 0.45f);
                fieldOfView = 49f;

                if (time >= 14.65f && !alarmTriggered)
                {
                    alarm?.TriggerAlarm();
                    alarmTriggered = true;
                }
                if (door != null)
                    door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 72f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14.25f, 15.4f, time))), 0f);
            }
            else if (time < 22f)
            {
                float u = ShotProgress(time, 18f, 22f);
                showRaccoon = true;
                // Track down the centre of a real aisle. Keeping both actor and
                // lens between shelf rows prevents foreground stock from hiding
                // the raccoon during the chase beat.
                Vector3 start = new(3.2f, 0f, 5.95f);
                Vector3 end = new(8.8f, 0f, 5.95f);
                PlaceAnimatedRaccoon(raccoon, raccoonVisual, start, end, u,
                    "Raccoon Run.FBX", "Rac_Run Forward", time - 18f);
                cameraPosition = Vector3.Lerp(new Vector3(11.8f, 0.78f, 5.95f),
                    new Vector3(11.35f, 0.70f, 5.95f), u);
                cameraTarget = raccoon.transform.position + Vector3.up * 0.34f;
                fieldOfView = 54f;
            }
            else if (time < 26f)
            {
                float u = ShotProgress(time, 22f, 26f);
                showHarold = harold != null;
                if (harold != null)
                {
                    Vector3 start = new(3.2f, 0f, 12.45f);
                    Vector3 end = new(9.4f, 0f, 12.45f);
                    harold.transform.position = Vector3.Lerp(start, end, u);
                    harold.transform.rotation = Quaternion.LookRotation(end - start);
                    if (haroldAnimator != null && haroldWalk != null)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(haroldAnimator.gameObject, haroldWalk,
                            Mathf.Repeat(time - 22f, haroldWalk.length));
                        AnimationMode.EndSampling();
                    }
                    float haroldGroundY = FindTrailerGroundY(
                        harold.transform.position,
                        harold.transform);
                    SnapAnimatedVisualToGround(
                        harold,
                        harold.transform.Find("HaroldVisual")?.gameObject,
                        haroldGroundY,
                        false);
                }
                cameraPosition = Vector3.Lerp(new Vector3(11.45f, 1.30f, 14.45f),
                    new Vector3(10.75f, 1.18f, 14.1f), u);
                cameraTarget = (harold != null ? harold.transform.position : new Vector3(7f, 0f, 12.45f))
                    + Vector3.up * 1.05f;
                fieldOfView = 51f;
            }
            else if (time < 29.5f)
            {
                float u = ShotProgress(time, 26f, 29.5f);
                showRaccoon = true;
                // Character close-up instead of a three-and-a-half-second "jump"
                // arc. That shot repeatedly sampled a one-shot clip while adding a
                // second artificial arc, which made the raccoon hover over shelves.
                Vector3 start = new(7.15f, 0f, 8.0f);
                Vector3 end = start;
                PlaceAnimatedRaccoon(raccoon, raccoonVisual, start, end, u,
                    "Raccoon Idle.FBX", "Rac_Sneak Idle", time - 26f);
                raccoon.transform.rotation = Quaternion.Euler(0f, 220f, 0f);
                cameraPosition = Vector3.Lerp(new Vector3(8.65f, 0.64f, 9.05f),
                    new Vector3(8.15f, 0.52f, 8.72f), u);
                cameraTarget = raccoon.transform.position + Vector3.up * 0.30f;
                fieldOfView = Mathf.Lerp(48f, 42f, u);
            }
            else if (time < 33f)
            {
                float u = ShotProgress(time, 29.5f, 33f);
                showRaccoon = true;
                // A fully open pavement escape replaces the prop-filled alley roll.
                Vector3 start = new(10.8f, 0f, -1.65f);
                Vector3 end = new(3.2f, 0f, -1.65f);
                PlaceAnimatedRaccoon(raccoon, raccoonVisual, start, end, u,
                    "Raccoon Run.FBX", "Rac_Run Forward", time - 29.5f);
                cameraPosition = Vector3.Lerp(new Vector3(8.8f, 0.78f, -5.2f),
                    new Vector3(5.8f, 0.70f, -5.0f), u);
                cameraTarget = raccoon.transform.position + Vector3.up * 0.32f;
                fieldOfView = 53f;
            }
            else
            {
                float u = ShotProgress(time, 33f, 36f);
                cameraPosition = Vector3.Lerp(new Vector3(12.4f, 1.45f, -7.7f),
                    new Vector3(8.8f, 1.05f, -4.9f), u);
                cameraTarget = Vector3.Lerp(new Vector3(7f, 1.45f, 0f),
                    new Vector3(7f, 2.25f, 0f), u);
                fieldOfView = Mathf.Lerp(52f, 45f, u);
                if (alarmTriggered)
                {
                    alarm?.ResetAlarm();
                    alarmTriggered = false;
                }
                if (door != null) door.localRotation = Quaternion.identity;
            }

            raccoon.SetActive(showRaccoon);
            if (harold != null) harold.SetActive(showHarold);
            if (showRaccoon)
                ValidateTrailerRaccoonClearance(raccoon, time);
            cameraTransform.SetPositionAndRotation(cameraPosition,
                Quaternion.LookRotation(cameraTarget - cameraPosition));
            camera.fieldOfView = fieldOfView;
            actorLight.position = (showRaccoon ? raccoon.transform.position : cameraTarget)
                + new Vector3(0.45f, 1.1f, -0.45f);

            if (alarmTriggered)
            {
                float degrees = 250f / TrailerFps;
                for (int i = 0; i < beacons.Count; i++)
                    beacons[i].Rotate(0f, (i & 1) == 0 ? degrees : -degrees, 0f, Space.Self);
            }
        }

        static void PlaceAnimatedRaccoon(
            GameObject actor,
            GameObject visual,
            Vector3 start,
            Vector3 end,
            float progress,
            string animationFile,
            string clipName,
            float clipTime)
        {
            actor.SetActive(true);
            actor.transform.position = Vector3.Lerp(start, end, progress);
            Vector3 direction = end - start;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                actor.transform.rotation = Quaternion.LookRotation(direction);
            var clip = LoadRaccoonClip(animationFile, clipName);
            if (visual != null && clip != null)
            {
                Vector3 stableScale = visual.transform.localScale;
                Quaternion stableRotation = visual.transform.localRotation;
                clip.SampleAnimation(visual, Mathf.Repeat(clipTime, clip.length));
                visual.transform.localScale = stableScale;
                visual.transform.localRotation = stableRotation;
            }

            float groundY = FindTrailerGroundY(actor.transform.position, actor.transform);
            var groundedPosition = actor.transform.position;
            groundedPosition.y = groundY;
            actor.transform.position = groundedPosition;
            SnapAnimatedVisualToGround(actor, visual, groundY, true);
        }

        static float FindTrailerGroundY(Vector3 position, Transform ignoredRoot)
        {
            var origin = new Vector3(position.x, 0.8f, position.z);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                1.2f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float groundY = 0f;
            bool found = false;
            foreach (var hit in hits)
            {
                if (hit.collider == null
                    || hit.collider.transform.IsChildOf(ignoredRoot)
                    || hit.normal.y < 0.65f
                    || hit.point.y > 0.3f)
                    continue;
                if (!found || hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    found = true;
                }
            }
            return found ? groundY : 0f;
        }

        static void SnapAnimatedVisualToGround(
            GameObject actor,
            GameObject visual,
            float groundY,
            bool centerOnActor)
        {
            if (actor == null || visual == null)
                return;

            Bounds bounds = RendererBounds(visual);
            Vector3 correction = Vector3.up * (groundY - bounds.min.y);
            if (centerOnActor)
            {
                correction.x = actor.transform.position.x - bounds.center.x;
                correction.z = actor.transform.position.z - bounds.center.z;
            }
            visual.transform.position += correction;
        }

        static void ValidateTrailerRaccoonClearance(GameObject actor, float time)
        {
            Physics.SyncTransforms();
            Vector3 bottom = actor.transform.position + Vector3.up * 0.20f;
            Vector3 top = actor.transform.position + Vector3.up * 0.32f;
            foreach (var collider in Physics.OverlapCapsule(
                bottom,
                top,
                0.16f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                if (collider == null || collider.transform.IsChildOf(actor.transform))
                    continue;
                throw new InvalidOperationException(
                    $"Trailer raccoon intersects '{collider.name}' at {time:0.00}s "
                    + $"(position {actor.transform.position}).");
            }
        }

        static float ShotProgress(float time, float start, float end)
        {
            float linear = Mathf.InverseLerp(start, end, time);
            return linear * linear * (3f - 2f * linear);
        }

        static void ApplyTrailerNeon(float time)
        {
            Type flickerType = typeof(NeonSignFlicker);
            var propertyBlockField = flickerType.GetField("propertyBlock",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var baseSpillIntensityField = flickerType.GetField("baseSpillIntensity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var applyLevel = flickerType.GetMethod("ApplyLevel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (applyLevel == null) return;
            var flickers = UnityEngine.Object.FindObjectsByType<NeonSignFlicker>();
            for (int i = 0; i < flickers.Length; i++)
            {
                if (propertyBlockField != null && propertyBlockField.GetValue(flickers[i]) == null)
                    propertyBlockField.SetValue(flickers[i], new MaterialPropertyBlock());
                if (baseSpillIntensityField != null && flickers[i].spillLight != null)
                {
                    float baseSpillIntensity = (float)baseSpillIntensityField.GetValue(flickers[i]);
                    if (baseSpillIntensity <= 0f)
                        baseSpillIntensityField.SetValue(flickers[i], flickers[i].spillLight.intensity);
                }

                float level = 1f;
                if (time >= 0f)
                {
                    float period = i == 0 ? 4.7f : i == 1 ? 6.15f : 8.05f;
                    float phase = Mathf.Repeat(time + i * 1.73f, period);
                    float faultLength = i == 2 ? 0.48f : 0.24f;
                    if (phase < faultLength)
                        level = phase < faultLength * 0.46f ? 0.02f : 0.22f + 0.5f * Mathf.Abs(Mathf.Sin(time * 39f));
                }
                applyLevel.Invoke(flickers[i], new object[] { level });
            }
        }

        static GameObject CreateCinematicRaccoon(
            Vector3 floorPosition,
            float yaw,
            string animationFile,
            string clipName,
            float normalizedTime)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonModelPath);
            if (model == null)
            {
                Debug.LogError($"Raccoon Heist: Poly Art raccoon model is missing at {RaccoonModelPath}.");
                return null;
            }

            var actor = new GameObject("CinematicRaccoon");
            actor.transform.SetPositionAndRotation(floorPosition, Quaternion.Euler(0f, yaw, 0f));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, actor.transform);
            visual.name = "PolyArtVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true))
                lod.ForceLOD(0);
            var material = EnsureRaccoonMaterial();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }

            var clip = LoadRaccoonClip(animationFile, clipName);
            if (clip != null)
                clip.SampleAnimation(visual, clip.length * Mathf.Repeat(normalizedTime, 1f));

            var bounds = RendererBounds(actor);
            if (bounds.size.y > 0.001f)
            {
                visual.transform.localScale *= ShopConstants.RaccoonHeight / bounds.size.y;
                bounds = RendererBounds(actor);
                visual.transform.position += Vector3.up * (floorPosition.y - bounds.min.y);
            }

            bounds = RendererBounds(actor);
            Debug.Log($"Raccoon Heist: Poly Art raccoon test bounds {bounds.size}, "
                + $"height {bounds.size.y:0.00} m, material '{material?.name}'.");
            return actor;
        }

        static Material EnsureRaccoonMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RaccoonMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "RaccoonPoly_URP"
                };
                AssetDatabase.CreateAsset(material, RaccoonMaterialPath);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaccoonTexturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static AnimationClip LoadRaccoonClip(string fileName, string clipName)
        {
            string assetPath = $"{RaccoonAnimationFolder}/{fileName}";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset is AnimationClip clip
                    && !clip.name.StartsWith("__preview", StringComparison.Ordinal)
                    && clip.name == clipName)
                    return clip;

            Debug.LogError($"Raccoon Heist: clip '{clipName}' was not found in {assetPath}.");
            return null;
        }
    }
}
