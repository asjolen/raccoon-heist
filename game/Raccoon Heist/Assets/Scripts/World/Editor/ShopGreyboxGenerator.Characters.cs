using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RaccoonHeist.Harold;
using RaccoonHeist.Player;
using RaccoonHeist.World;

namespace RaccoonHeist.World.Editor
{
    // Harold and the raccoon player rig.
    public static partial class ShopGreyboxGenerator
    {
        // ---------- characters ----------

        const string HaroldFbxPath = "Assets/Models/Harold/Meshy_AI_Harold_biped_Character_output.fbx";
        const string HaroldAnimationsFbxPath = "Assets/Models/Harold/Meshy_AI_Harold_biped_Meshy_AI_Meshy_Merged_Animations.fbx";
        const string HaroldControllerPath = "Assets/Models/Harold/HaroldAnimator.controller";
        const string HaroldCorrectedClipsFolder = "Assets/Models/Harold/CorrectedClips";
        const string HaroldTexturePath = "Assets/Models/Harold/Meshy_AI_Harold_biped_texture_0.png";
        const string HaroldMaterialPath = "Assets/Models/Harold/Harold.mat";

        static void SpawnHarold(Vector3 floorPosition)
        {
            // Strip Meshy's constant animation scale keys before instantiating the
            // model. Some clips key target_character at 1 while the FBX bind pose
            // is 100, which otherwise makes Harold change size when animation starts.
            EnsureHaroldAnimationImport();
            EnsureHaroldMaterial();
            // Visuals come from the merged-animations FBX: rig and clips live in one
            // file, so binding is guaranteed. The separate character export has a
            // different hierarchy root — its Animator played the clips into thin air
            // and Harold glided around in a T-pose.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HaroldAnimationsFbxPath);
            if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(HaroldFbxPath);
            if (model == null)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Harold_Placeholder";
                capsule.transform.SetParent(root, false);
                capsule.transform.localScale = new Vector3(0.6f, ShopConstants.HaroldHeight / 2f, 0.6f);
                capsule.transform.localPosition = floorPosition + Vector3.up * (ShopConstants.HaroldHeight / 2f);
                Debug.LogWarning($"Harold model not found at {HaroldFbxPath} — using capsule placeholder.");
                SetupHaroldComponents(capsule);
                return;
            }

            // Keep navigation on a clean, upright root. Meshy's FBX is authored
            // Z-up and its visible hierarchy retains a -90° X bind rotation even
            // after Unity's axis conversion; a dedicated visual child cancels it
            // without letting NavMeshAgent overwrite the correction while turning.
            var harold = new GameObject("Harold");
            harold.transform.SetParent(root, false);
            harold.transform.position = floorPosition;
            harold.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the doorway

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, harold.transform);
            visual.name = "HaroldVisual";
            visual.transform.localPosition = Vector3.zero;
            // The imported mesh's forward axis is also opposite Unity's +Z.
            // The 180° yaw keeps his feet/rig correction while making his body
            // face the same direction the upright NavMesh root is travelling.
            visual.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
            visual.transform.localScale = Vector3.one;

            // Meshy's rig has a hidden 100x target_character transform. Measuring
            // shared mesh geometry ignores that rig scale and previously enlarged
            // an already enormous rendered character. Renderer bounds include the
            // complete skinned hierarchy, so normalize the character the player
            // actually sees.
            var bounds = RendererBounds(harold);
            if (bounds.size.y > 0.001f)
            {
                visual.transform.localScale *= ShopConstants.HaroldHeight / bounds.size.y;
                bounds = RendererBounds(harold);
                visual.transform.position += Vector3.up * (floorPosition.y - bounds.min.y);
            }

            SetupHaroldComponents(harold);
            GroundHaroldForLocomotion(harold, visual, floorPosition.y + 0.02f);
            ValidateHaroldRenderedSize(harold);
        }

        // Agent, brain, and voice — same wiring for the real model and the capsule
        // fallback, so behaviour work never blocks on the model importing.
        static void SetupHaroldComponents(GameObject harold)
        {
            var agent = harold.AddComponent<NavMeshAgent>();
            agent.height = ShopConstants.HaroldHeight;
            agent.radius = 0.35f; // belly-width: 0.3 let him shear through shelf corners
            agent.angularSpeed = 150f;
            agent.acceleration = 2.2f;
            agent.stoppingDistance = 0.15f;

            var animator = harold.GetComponentInChildren<Animator>();
            if (animator == null) animator = harold.AddComponent<Animator>();
            animator.applyRootMotion = false; // the agent moves him; clips only pose him
            var controller = BuildHaroldAnimatorController();
            if (controller != null) animator.runtimeAnimatorController = controller;

            var voice = harold.AddComponent<HaroldVoice>();
            voice.snoreLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Harold/harold_snore.wav");
            voice.grumbles = LoadHaroldClips("harold_grumble_0");
            voice.hearSomethingLines = LoadHaroldClips("harold_grumble_hear_something");
            voice.cantSeeYouLines = LoadHaroldClips("harold_i_cant_see_you");

            var steps = harold.AddComponent<HaroldFootsteps>();
            steps.stepClips = LoadHaroldClips("harold_step_");

            var brain = harold.AddComponent<HaroldBrain>();
            // Wander the main shop floor; walls and fixtures clamp the rest. The
            // margin keeps destinations out of wall-hugging dead ends.
            brain.ConfigureWander(new Vector3(W / 2f, 0f, D / 2f), new Vector3(W - 2f, 0f, D - 2f));
        }

        // Meshy clips import non-looping, Z-up, and avatar-less by default. Without
        // baked axis conversion the clips override the -90° import rotation (Harold
        // "walks" lying on the floor), and without an avatar the position curves
        // apply in raw file units (giant Harold).
        static void EnsureHaroldAnimationImport()
        {
            var importer = AssetImporter.GetAtPath(HaroldAnimationsFbxPath) as ModelImporter;
            if (importer == null) return;
            bool dirty = false;

            if (!importer.bakeAxisConversion)
            {
                importer.bakeAxisConversion = true;
                dirty = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            if (!importer.removeConstantScaleCurves)
            {
                importer.removeConstantScaleCurves = true;
                dirty = true;
            }
            if (importer.clipAnimations.Length == 0)
            {
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips) clip.loopTime = true;
                importer.clipAnimations = clips;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        static RuntimeAnimatorController BuildHaroldAnimatorController()
        {
            EnsureHaroldAnimationImport();
            var clips = new List<AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(HaroldAnimationsFbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    clips.Add(clip);
            if (clips.Count == 0)
            {
                Debug.LogWarning($"Raccoon Heist: no animation clips in {HaroldAnimationsFbxPath} — Harold stays in T-pose.");
                return null;
            }

            var names = new List<string>();
            foreach (var clip in clips) names.Add(clip.name);
            Debug.Log("Raccoon Heist: Harold clips found: " + string.Join(", ", names));

            AnimationClip Find(params string[] keywords)
            {
                foreach (var keyword in keywords)
                    foreach (var clip in clips)
                        if (clip.name.ToLowerInvariant().Contains(keyword))
                            return clip;
                return null;
            }
            var idle = Find("idle") ?? clips[0];
            var walk = Find("walking", "walk") ?? idle;
            var run = Find("running", "run") ?? walk;
            idle = BuildCorrectedHaroldClip(idle, "Idle");
            walk = BuildCorrectedHaroldClip(walk, "Walk");
            run = BuildCorrectedHaroldClip(run, "Run");
            ValidateHaroldRootTransformCurves(idle);
            ValidateHaroldRootTransformCurves(walk);
            ValidateHaroldRootTransformCurves(run);

            AssetDatabase.DeleteAsset(HaroldControllerPath);
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(HaroldControllerPath);
            controller.AddParameter("Speed", UnityEngine.AnimatorControllerParameterType.Float);
            // CreateBlendTreeInController persists the tree as a sub-asset — trees
            // built by hand must be AddObjectToAsset'ed or they die on reload.
            controller.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 0.8f);  // matches HaroldBrain walk speed (m/s)
            tree.AddChild(run, 2.6f);   // ready for the future Chase state
            return controller;
        }

        static AnimationClip BuildCorrectedHaroldClip(AnimationClip source, string role)
        {
            EnsureAssetFolder(HaroldCorrectedClipsFolder);
            string path = $"{HaroldCorrectedClipsFolder}/Harold_{role}.anim";
            AssetDatabase.DeleteAsset(path);

            var corrected = Object.Instantiate(source);
            corrected.name = $"Harold_{role}";
            corrected.wrapMode = WrapMode.Loop;

            // The Meshy file is authored Z-up. Axis conversion makes its bind pose
            // upright in Unity, but every take still contains a constant -90° root
            // rotation on target_character. When the Animator samples that curve it
            // undoes the import correction and lays Harold on the floor. Keep all
            // bone animation, but let this one transform inherit the upright bind pose.
            // NavMeshAgent owns world translation, so root position curves are also
            // removed; even constant-looking values can lift the rendered rig once
            // the corrected visual axis and scale are applied.
            foreach (var binding in AnimationUtility.GetCurveBindings(corrected))
            {
                if (binding.path != "target_character") continue;
                bool isRotation = binding.propertyName.StartsWith("m_LocalRotation")
                    || binding.propertyName.StartsWith("localEulerAngles");
                bool isScale = binding.propertyName.StartsWith("m_LocalScale");
                bool isPosition = binding.propertyName.StartsWith("m_LocalPosition");
                if (isRotation || isScale || isPosition)
                    AnimationUtility.SetEditorCurve(corrected, binding, null);
            }

            AssetDatabase.CreateAsset(corrected, path);
            return corrected;
        }

        static void ValidateHaroldRootTransformCurves(AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path != "target_character"
                    || (!binding.propertyName.StartsWith("m_LocalScale")
                        && !binding.propertyName.StartsWith("m_LocalPosition")
                        && !binding.propertyName.StartsWith("m_LocalRotation")
                        && !binding.propertyName.StartsWith("localEulerAngles")))
                    continue;

                Debug.LogError($"Raccoon Heist: corrected Harold clip '{clip.name}' still keys "
                    + $"target_character through '{binding.propertyName}'. Harold can change "
                    + "size, hover, or lie down when this clip plays.");
                return;
            }
        }

        static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static AudioClip[] LoadHaroldClips(string prefix)
        {
            var clips = new List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Harold" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(prefix))
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            if (clips.Count == 0) Debug.LogWarning($"Raccoon Heist: no Harold clips matched '{prefix}'.");
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips.ToArray();
        }

        // Harold's NavMesh is interior-only. The raccoon can use the exterior street,
        // passage, and alley loop, but Harold must never treat those routes as patrol
        // space (and the Scene view should not show a blue bake across the city).
        static void BuildNavigation()
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = ShopConstants.HaroldHeight;
            settings.agentClimb = 0.25f;
            settings.agentSlope = 40f;
            // Default voxels (radius/3) round the eroded 0.9 m doorway shut at this
            // radius; a finer grid keeps the back-room doorway walkable.
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.09f;

            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            const float edgeInset = 0.04f;
            float interiorDepth = StorageZ1;
            var bounds = new Bounds(
                new Vector3(W * 0.5f, H * 0.5f, interiorDepth * 0.5f),
                new Vector3(W - edgeInset * 2f, H + 0.5f, interiorDepth - edgeInset * 2f));
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);

            const string navPath = "Assets/Scenes/ShopNavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(data, navPath);

            var navGo = new GameObject("Navigation");
            navGo.transform.SetParent(root, false);
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.navMeshData = data;
        }

        // Meshy FBX materials lose their texture link on import — build a URP material
        // with the texture and remap the model's embedded materials onto it once.
        static void EnsureHaroldMaterial()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HaroldTexturePath);
            if (texture == null) return;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(HaroldMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", texture);
                mat.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(mat, HaroldMaterialPath);
            }

            RemapFbxMaterials(HaroldFbxPath, mat);
            RemapFbxMaterials(HaroldAnimationsFbxPath, mat);
        }

        static void RemapFbxMaterials(string fbxPath, Material mat)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;
            var existing = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
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

        static Bounds RendererBounds(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool has = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return has ? bounds : new Bounds(go.transform.position, Vector3.zero);
        }

        static void GroundHaroldForLocomotion(
            GameObject harold,
            GameObject visual,
            float floorY)
        {
            var animator = harold.GetComponentInChildren<Animator>(true);
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"{HaroldCorrectedClipsFolder}/Harold_Walk.anim");
            if (animator == null || walk == null || walk.length <= 0f)
                return;

            // A bind-pose grounding check is not enough: the authored walking
            // pose carries a different lowest vertex. Measure a full gait cycle,
            // take the median foot correction, then apply that offset outside
            // AnimationMode so it persists in the generated scene.
            var corrections = new List<float>();
            AnimationMode.StartAnimationMode();
            try
            {
                const int samples = 12;
                for (int i = 0; i < samples; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(
                        animator.gameObject,
                        walk,
                        walk.length * i / samples);
                    AnimationMode.EndSampling();
                    corrections.Add(floorY - RendererBounds(harold).min.y);
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
            }

            corrections.Sort();
            float correction = corrections[corrections.Count / 2];
            visual.transform.position += Vector3.up * correction;
            Debug.Log($"Raccoon Heist: grounded Harold locomotion to y={floorY:0.000} "
                + $"with visual offset {correction:+0.000;-0.000;0.000} m.");
        }

        static void ValidateHaroldRenderedSize(GameObject harold)
        {
            var bounds = RendererBounds(harold);
            float heightError = Mathf.Abs(bounds.size.y - ShopConstants.HaroldHeight);
            if (heightError > 0.08f || bounds.size.x > 2f || bounds.size.z > 2f)
            {
                Debug.LogError($"Raccoon Heist: Harold rendered bounds are {bounds.size}; "
                    + $"expected about {ShopConstants.HaroldHeight:0.0} m tall and under 2 m wide/deep.");
                return;
            }

            Debug.Log($"Raccoon Heist: Harold scale validated at {bounds.size.y:0.00} m tall "
                + $"(rendered bounds {bounds.size}).");
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
            // The alley asphalt tops out at roughly 0.02 m. The previous 0.05 m
            // root left a visible air gap under the capsule and controller.
            raccoon.transform.position = new Vector3(6.9f, 0.02f, OutD1 - 1.2f); // at the den in the alley
            raccoon.transform.rotation = Quaternion.Euler(0f, 180f, 0f);         // facing the shop

            var cc = raccoon.AddComponent<CharacterController>();
            cc.height = ShopConstants.RaccoonHeight;
            cc.radius = 0.2f;
            cc.center = new Vector3(0f, ShopConstants.RaccoonHeight / 2f, 0f);
            cc.stepOffset = 0.15f;
            cc.skinWidth = 0.025f;
            cc.minMoveDistance = 0f;
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

    }
}
