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
    // Synty POLYGON Shops set dressing (optional) and the interior parkour route.
    public static partial class ShopGreyboxGenerator
    {
        // ---------- Synty POLYGON Shops set dressing (optional — greybox fallback) ----------

        static readonly Dictionary<string, GameObject> syntyCache = new();

        static bool SyntyAvailable => SyntyPrefab("SM_Prop_Market_Aisle_Preset_01") != null;

        static GameObject SyntyPrefab(string name)
        {
            if (syntyCache.TryGetValue(name, out var cached)) return cached;
            // Both packs ship prefabs with identical names (roads, trees) — prefer City
            string bestPath = null;
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
                if (bestPath == null || path.Contains("/PolygonCity/")) bestPath = path;
                if (path.Contains("/PolygonCity/")) break;
            }
            var prefab = bestPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
            syntyCache[name] = prefab;
            return prefab;
        }

        // Instantiates a Synty prefab grounded at floorPos (feet on floor, footprint
        // centered) regardless of where its pivot is. Adds a fitted box collider if
        // the prefab ships without one.
        static GameObject PlaceSynty(string name, Vector3 floorPos, float yaw = 0f)
        {
            var prefab = SyntyPrefab(name);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = floorPos;
            var b = GeometryBounds(go);
            go.transform.position += new Vector3(floorPos.x - b.center.x, floorPos.y - b.min.y, floorPos.z - b.center.z);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                b = GeometryBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                var localSize = go.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            }
            return go;
        }

        // Facade skins, puddles, posters, and other visual-only dressing must not
        // accidentally seal a gameplay opening with PlaceSynty's fitted collider.
        static GameObject PlaceSyntyDecorative(string name, Vector3 floorPos, float yaw = 0f)
        {
            var go = PlaceSynty(name, floorPos, yaw);
            if (go == null) return null;
            foreach (var collider in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
            return go;
        }

        // Particle-only prefabs have no mesh bounds to ground against. Instantiate
        // them directly so steam/fog FX do not receive a zero-sized box collider.
        static GameObject PlaceSyntyFx(string name, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var prefab = SyntyPrefab(name);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static GameObject PlacePreferredSyntyFx(string preferredName, string fallbackName,
            Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var effect = PlaceSyntyFx(preferredName, position, yaw, scale)
                ?? PlaceSyntyFx(fallbackName, position, yaw, scale);
            if (effect == null) return null;

            ConfigureAmbientSmoke(effect, yaw, preferredName.Contains("Smoke"));
            return effect;
        }

        static void ConfigureAmbientSmoke(GameObject effect, float yaw, bool rooftop)
        {
            // Reuse the POLYGON Particle FX mesh and shader, but author a controlled
            // particle motion. The stock half-second jet accumulated mesh puffs at
            // the emitter when slowed down, producing one bright polygon ball.
            var templateRenderer = effect.GetComponentInChildren<ParticleSystemRenderer>(true);
            var templateMaterial = templateRenderer != null ? templateRenderer.sharedMaterial : null;
            float sourceScale = Mathf.Max(0.1f, effect.transform.localScale.x);
            effect.transform.localScale = Vector3.one;
            effect.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up);

            foreach (var template in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                template.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = template.emission;
                emission.enabled = false;
                var renderer = template.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = false;
            }

            var plume = new GameObject(rooftop ? "AmbientSmokeParticles" : "AmbientSteamParticles");
            plume.transform.SetParent(effect.transform, false);
            plume.transform.localPosition = Vector3.zero;
            plume.transform.localRotation = Quaternion.identity;

            var particles = plume.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 4f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = rooftop ? 35 : 55;
            main.startLifetime = rooftop
                ? new ParticleSystem.MinMaxCurve(5.5f, 8.5f)
                : new ParticleSystem.MinMaxCurve(3.0f, 4.8f);
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startSize = rooftop
                ? new ParticleSystem.MinMaxCurve(0.62f * sourceScale, 1.15f * sourceScale)
                : new ParticleSystem.MinMaxCurve(0.35f * sourceScale, 0.68f * sourceScale);
            main.startColor = rooftop
                ? new ParticleSystem.MinMaxGradient(
                    new Color(0.28f, 0.32f, 0.40f, 0.34f),
                    new Color(0.44f, 0.48f, 0.56f, 0.56f))
                : new ParticleSystem.MinMaxGradient(
                    new Color(0.46f, 0.51f, 0.60f, 0.60f),
                    new Color(0.66f, 0.71f, 0.80f, 0.82f));

            var plumeEmission = particles.emission;
            plumeEmission.rateOverTime = rooftop
                ? new ParticleSystem.MinMaxCurve(1.4f, 2.1f)
                : new ParticleSystem.MinMaxCurve(7f, 10f);

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = rooftop ? 0.16f : 0.085f;
            shape.radiusThickness = 1f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = rooftop
                ? new ParticleSystem.MinMaxCurve(0.05f, 0.14f)
                : new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = rooftop
                ? new ParticleSystem.MinMaxCurve(0.20f, 0.34f)
                : new ParticleSystem.MinMaxCurve(0.30f, 0.50f);
            velocity.z = rooftop
                ? new ParticleSystem.MinMaxCurve(-0.04f, 0.06f)
                : new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = rooftop
                ? new ParticleSystem.MinMaxCurve(0.08f, 0.16f)
                : new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            noise.frequency = rooftop ? 0.28f : 0.42f;
            noise.scrollSpeed = rooftop ? 0.08f : 0.14f;
            noise.damping = true;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.32f),
                    new Keyframe(0.35f, 0.92f),
                    new Keyframe(0.72f, 1.28f),
                    new Keyframe(1f, 1.50f)));

            var fade = new Gradient();
            fade.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.90f, 0.14f),
                    new GradientAlphaKey(0.70f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            var plumeRenderer = plume.GetComponent<ParticleSystemRenderer>();
            // The steam prefab's sphere mesh turns a smoke cutout into scattered
            // pebble-like fragments. Camera-facing cards preserve the supplied
            // low-poly smoke silhouette and overlap into one coherent plume.
            plumeRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            plumeRenderer.sharedMaterial = AmbientSmokeMaterial(templateMaterial, rooftop);
            plumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            plumeRenderer.receiveShadows = false;
            plumeRenderer.maxParticleSize = 0.25f;
            particles.Play(true);
        }

        static Material AmbientSmokeMaterial(Material source, bool rooftop)
        {
            if (source == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox/Particles"))
                AssetDatabase.CreateFolder("Assets/Materials/Greybox", "Particles");

            var smokeSource = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Synty/PolygonGeneric/Materials/FX/Generic_Circle_Soft_01.mat") ?? source;
            var smokeTextureSource = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Synty/PolygonParticleFX/Materials/FX_Smoke_01.mat");
            string path = rooftop
                ? "Assets/Materials/Greybox/Particles/Ambient_RooftopSmoke_v2.mat"
                : "Assets/Materials/Greybox/Particles/Ambient_SteamSmoke_v2.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(smokeSource);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = smokeSource.shader;
                material.CopyPropertiesFromMaterial(smokeSource);
            }

            var tint = rooftop
                ? new Color(0.34f, 0.39f, 0.48f, 0.72f)
                : new Color(0.54f, 0.60f, 0.70f, 0.94f);
            if (smokeTextureSource != null && material.HasProperty("_Albedo_Map"))
                material.SetTexture("_Albedo_Map", smokeTextureSource.GetTexture("_Albedo_Map"));
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", tint);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_BUILTIN_AlphaClip")) material.SetFloat("_BUILTIN_AlphaClip", 0f);
            if (material.HasProperty("_ColorMode")) material.SetFloat("_ColorMode", 0f);
            if (material.HasProperty("_Enable_Camera_Fade")) material.SetFloat("_Enable_Camera_Fade", 0f);
            if (material.HasProperty("_CameraFadingEnabled")) material.SetFloat("_CameraFadingEnabled", 0f);
            if (material.HasProperty("_EmissionEnabled")) material.SetFloat("_EmissionEnabled", 0f);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        // Full pre-stocked market aisles, sized on the fly so any Synty footprint works
        static void BuildSyntyAisles()
        {
            string[] presets = { "SM_Prop_Market_Aisle_Preset_01", "SM_Prop_Market_Aisle_Preset_02",
                                 "SM_Prop_Market_Aisle_Preset_03", "SM_Prop_Market_Aisle_Preset_04" };
            var probe = PlaceSynty(presets[0], new Vector3(0f, -50f, 0f));
            var size = GeometryBounds(probe).size;
            Object.DestroyImmediate(probe);
            float len = Mathf.Max(size.x, size.z);
            float depth = Mathf.Min(size.x, size.z);
            float yaw = size.x >= size.z ? 0f : 90f; // long axis along x
            int i = 0;
            for (float z = 5f; z + depth / 2f <= D - 4.5f; z += depth + ShopConstants.AisleWidth)
                for (float x = 2.8f; x + len <= W - 2f; x += len + 0.6f) // 0.6 m squeeze-gaps
                    PlaceSynty(presets[i++ % presets.Length], new Vector3(x + len / 2f, 0f, z), yaw);
        }

        static void BuildSyntyFridgeRun()
        {
            var probe = PlaceSynty("SM_Prop_Market_Wall_Fridge_01", new Vector3(0f, -50f, 0f));
            if (probe == null) return;
            var size = GeometryBounds(probe).size;
            Object.DestroyImmediate(probe);
            float len = Mathf.Max(size.x, size.z);
            float yaw = size.x >= size.z ? 90f : 0f; // long axis along z, off the west wall
            for (float z = 3.2f; z + len <= D - 3f; z += len + 0.05f)
                PlaceSynty("SM_Prop_Market_Wall_Fridge_01", new Vector3(0.9f, 0f, z + len / 2f), yaw);
        }

        static bool TryCreateSyntyProduct(Transform parent, Vector3 localBase)
        {
            var prefab = SyntyPrefab($"SM_Prop_Product_{Random.Range(1, 21):00}") ?? SyntyPrefab("SM_Prop_Product_01");
            if (prefab == null) return false;

            // Instantiate at origin first so bounds/collider math is clean, then parent
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var b = GeometryBounds(go);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            float lift = -b.min.y;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localBase + Vector3.up * (lift + 0.01f);
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return true;
        }

        // The vertical climbing layer. Every surface is reachable in steps of <= 1 m
        // (the raccoon's jump), forming floor-free routes around the whole shop:
        // crates -> ledges -> ceiling beams -> fridge top -> shelf tops.
        static void BuildParkour()
        {
            var beamMat = Mat("Beam", new Color(0.30f, 0.24f, 0.19f));

            // Interior window sill — walkable ledge with a view of the street
            BoxMinMax("WindowSill", new Vector3(3.5f, 0.85f, 0f), new Vector3(W - 0.5f, 0.9f, 0.22f), null, Wood);

            // High ledges along the east and rear walls
            BoxMinMax("EastLedge", new Vector3(W - 0.25f, 1.9f, 3f), new Vector3(W, 1.95f, D - 3f), null, Wood);
            BoxMinMax("RearLedge", new Vector3(3f, 2.15f, D - 0.22f), new Vector3(W - 3f, 2.2f, D), null, Wood);

            // Ceiling beams — the raccoon highway. Harold (1.8 m) walks under them.
            for (float z = 4f; z < D - 3f; z += 3.6f)
                BoxMinMax($"Beam_{z}", new Vector3(0f, 2.25f, z - 0.12f), new Vector3(W, 2.4f, z + 0.12f), null, beamMat);

            // Crate steps sit beside the centered door, leaving its swing and the
            // direct entry sightline clear while preserving the front parkour route.
            BoxMinMax("Crates_Door_A", new Vector3(5.0f, 0f, 0.5f), new Vector3(5.6f, 0.6f, 1.1f), null, Crate);
            BoxMinMax("Crates_Door_B", new Vector3(5.0f, 0f, 1.15f), new Vector3(5.6f, 1.2f, 1.75f), null, Crate);

            // Crate steps up to the east ledge
            BoxMinMax("Crates_Vent_A", new Vector3(W - 0.65f, 0f, D - 4.2f), new Vector3(W - 0.05f, 0.6f, D - 3.6f), null, Crate);
            BoxMinMax("Crates_Vent_B", new Vector3(W - 0.65f, 0f, D - 3.55f), new Vector3(W - 0.05f, 1.2f, D - 2.95f), null, Crate);

            // Crate steps up to the fridge-top high route
            BoxMinMax("Crates_Fridge_A", new Vector3(1.4f, 0f, 2.3f), new Vector3(2f, 0.6f, 2.9f), null, Crate);
            BoxMinMax("Crates_Fridge_B", new Vector3(2.05f, 0f, 2.3f), new Vector3(2.65f, 1.2f, 2.9f), null, Crate);
        }

    }
}
