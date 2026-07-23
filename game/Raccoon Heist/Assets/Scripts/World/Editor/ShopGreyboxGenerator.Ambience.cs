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
    // Lighting, environment audio, the shop alarm, and post-processing atmosphere.
    public static partial class ShopGreyboxGenerator
    {
        static readonly Color ExteriorPracticalColor = new(1f, 0.72f, 0.46f);

        // ---------- lighting ----------

        static void BuildLighting()
        {
            // Trilight gives upward faces cold moon fill while undersides and alley
            // recesses stay dark. Flat ambient was the main source of the cardboard look.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.105f, 0.125f, 0.195f);
            RenderSettings.ambientEquatorColor = new Color(0.060f, 0.070f, 0.112f);
            RenderSettings.ambientGroundColor = new Color(0.026f, 0.030f, 0.048f);
            RenderSettings.ambientIntensity = 1.08f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.40f;
            RenderSettings.reflectionBounces = 1;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(root, false);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            moon.color = new Color(0.55f, 0.66f, 1f);
            moon.intensity = 0.92f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.76f;

            // Real night sky: stars, painted moon, warm city-glow at the horizon
            string skyPath = "Assets/Materials/Greybox/NightSkyPano.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetTexture("_MainTex", TexNightSky);
            sky.SetFloat("_Exposure", 0.8f);
            RenderSettings.skybox = sky;
            RenderSettings.sun = moon;

            // Blue urban haze separates foreground, block, and skyline into readable
            // depth layers without putting translucent cone geometry in the scene.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.11f, 0.135f, 0.205f);
            RenderSettings.fogDensity = 0.030f;

            BuildAtmosphere();

            // Harold's lamp glow spilling out of the back room — his "location beacon"
            PointLight("BackRoomLamp", new Vector3(2.5f, 2.1f, D + T + 4f - 0.6f), new Color(1f, 0.62f, 0.32f), 1.6f, 6f);
            PointLight("FridgeGlow", new Vector3(1.8f, 1.9f, (D + 0f) / 2f), new Color(0.65f, 0.85f, 1f), 1.4f, 5f);
            // Overnight safety lights — the diegetic excuse for a readable interior
            PointLight("ShopNightLight_A", new Vector3(4.5f, 2.7f, 5.5f), new Color(0.55f, 0.68f, 0.72f), 0.5f, 8f);
            PointLight("ShopNightLight_B", new Vector3(10f, 2.7f, 11f), new Color(0.55f, 0.68f, 0.72f), 0.5f, 8f);
            // Bare bulb in the storage room — dim, warm, horror-pantry mood, slight waver
            var bulb = PointLight("StorageBulb", new Vector3((StorageX0 + StorageX1) / 2f, 2.5f, D + 2.5f), new Color(1f, 0.72f, 0.42f), 1.3f, 6f);
            var bulbFlicker = bulb.gameObject.AddComponent<FlickeringLight>();
            bulbFlicker.flickerAmount = 0.18f;
            bulbFlicker.dropoutChance = 0.03f;
            // The five shop-side exterior lamps are complete cloned assemblies built
            // with the shell. They deliberately share fixture, lens, colour and beam.
            var denGlow = PointLight("DenGlow", new Vector3(6.20f, 0.72f, OutD1 - 0.48f),
                ExteriorPracticalColor, 1.35f, 4.2f);
            ParentLightToFixture(denGlow, "DenFenceLamp");

            // The entrance pool now originates at the visible wall fixture instead
            // of floating above the street and blasting through the whole facade.
            SpotLight("EntranceCanopyPool", new Vector3(EntranceX, 2.10f, -0.53f),
                new Vector3(0f, -0.90f, -0.28f), ExteriorPracticalColor, 4.5f, 7.2f, 76f, true,
                "EntranceCanopyLight");

            // Local glow around the signs lets their colour touch nearby masonry and
            // wet pavement; small ranges keep the palette intentional.
            var openGlow = PointLight("OpenSignGlow", new Vector3(2.05f, 1.35f, -0.18f),
                new Color(0.15f, 0.75f, 1f), 0.8f, 3.3f);
            ParentLightToFixture(openGlow, "OpenSignFixture");
            var titleGlow = PointLight("RaccoonHeistSignGlow", new Vector3(7f, 2.55f, -0.42f), new Color(0.12f, 0.72f, 1f), 0.9f, 5f);
            ParentLightToFixture(titleGlow, "RaccoonHeistTitle");
            foreach (var titleFlicker in root.GetComponentsInChildren<NeonSignFlicker>(true))
            {
                if (!titleFlicker.name.EndsWith("_I")) continue;
                titleFlicker.spillLight = titleGlow;
                break;
            }
            AttachNeonGlow("WestNeonGlow", "WestNeonFixture", new Vector3(-18f, 1.5f, OutD0 - 1.05f),
                new Color(0.95f, 0.12f, 0.72f), 0.65f, 3.4f);
            AttachNeonGlow("EastNeonGlow", "EastNeonFixture", new Vector3(W + 24f, 1.5f, OutD0 - 1.05f),
                new Color(0.12f, 0.72f, 1f), 0.6f, 3.4f);
            AttachNeonGlow("RearNeonGlow", "RearNeonFixture", new Vector3(7.2f, 1.5f, OutD1 + 11.55f),
                new Color(0.8f, 0.14f, 1f), 0.55f, 3.8f);
        }

        static void AttachNeonGlow(string lightName, string fixtureName, Vector3 position,
            Color color, float intensity, float range)
        {
            var glow = PointLight(lightName, position, color, intensity, range);
            ParentLightToFixture(glow, fixtureName);
        }

        static void BuildEnvironmentAudio()
        {
            const string ambiencePath = "Assets/Audio/Environment/city_ambience.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ambiencePath);
            if (clip == null)
            {
                Debug.LogWarning($"Raccoon Heist: exterior ambience clip is missing at {ambiencePath}.");
                return;
            }

            var ambience = new GameObject("ExteriorCityAmbience");
            ambience.transform.SetParent(root, false);
            var source = ambience.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0.55f;
            source.spatialBlend = 0f; // A city bed surrounds the listener; it is not one point in the street.
            source.dopplerLevel = 0f;
            source.priority = 96;

            // These are the walkable interior volumes, inset slightly from their walls.
            // The roof and every exterior route remain outside the zones and keep the city audible.
            var interiorZones = InteriorAudioZones();
            var controller = ambience.AddComponent<ExteriorAmbienceController>();
            controller.Configure(0.55f, 0.8f, interiorZones);

            bool outsideAudible = !controller.IsInteriorPosition(new Vector3(EntranceX, 0.35f, -2f));
            bool shopMuted = controller.IsInteriorPosition(new Vector3(EntranceX, 0.35f, 2f));
            bool backRoomMuted = controller.IsInteriorPosition(new Vector3(1.5f, 0.35f, D + T + 2f));
            bool storageMuted = controller.IsInteriorPosition(new Vector3(8f, 0.35f, D + T + 2.5f));
            if (outsideAudible && shopMuted && backRoomMuted && storageMuted)
                Debug.Log("Raccoon Heist: exterior ambience threshold check passed (outside audible; all interiors muted).");
            else
                Debug.LogWarning("Raccoon Heist: exterior ambience threshold check failed; inspect configured interior bounds.");
        }

        static Bounds[] InteriorAudioZones()
        {
            return new[]
            {
                new Bounds(new Vector3(W * 0.5f, 1.38f, D * 0.5f), new Vector3(W - 0.1f, 3.55f, D - 0.1f)),
                new Bounds(new Vector3(1.5f, 1.38f, D + T + ShopConstants.BackRoomDepth * 0.5f),
                    new Vector3(2.9f, 3.55f, ShopConstants.BackRoomDepth - 0.1f)),
                new Bounds(new Vector3((StorageX0 + StorageX1) * 0.5f, 1.38f, D + T + ShopConstants.StorageDepth * 0.5f),
                    new Vector3(ShopConstants.StorageWidth - 0.4f, 3.55f, ShopConstants.StorageDepth - 0.1f))
            };
        }

        static void BuildNeonAudio()
        {
            var flickers = root.GetComponentsInChildren<NeonSignFlicker>(true);
            if (flickers.Length == 0) return;
            var primaryFlicker = flickers[0];
            foreach (var candidate in flickers)
            {
                if (!candidate.name.EndsWith("_I")) continue;
                primaryFlicker = candidate;
                break;
            }

            var humClip = EnsureGeneratedAudioClip("neon_transformer_hum_v1.wav", 44100, 2f, NeonHumSample);
            var faultClip = EnsureGeneratedAudioClip("neon_fault_crackle_v1.wav", 44100, 0.09f, NeonFaultSample);
            if (humClip == null || faultClip == null)
            {
                Debug.LogWarning("Raccoon Heist: generated neon audio clips could not be imported.");
                return;
            }

            var audioHolder = new GameObject("NeonSignAudio");
            audioHolder.transform.SetParent(root, false);
            audioHolder.transform.localPosition = new Vector3(EntranceX, 2.55f, -0.38f);

            var humSource = audioHolder.AddComponent<AudioSource>();
            humSource.clip = humClip;
            humSource.loop = true;
            humSource.playOnAwake = false;
            humSource.volume = primaryFlicker.humVolume;
            humSource.spatialBlend = 1f;
            humSource.minDistance = 0.55f;
            humSource.maxDistance = 7f;
            humSource.rolloffMode = AudioRolloffMode.Linear;
            humSource.dopplerLevel = 0f;
            humSource.spread = 0f;
            humSource.priority = 144;

            var faultSource = audioHolder.AddComponent<AudioSource>();
            faultSource.clip = faultClip;
            faultSource.loop = false;
            faultSource.playOnAwake = false;
            faultSource.volume = 1f;
            faultSource.spatialBlend = 1f;
            faultSource.minDistance = 0.45f;
            faultSource.maxDistance = 6f;
            faultSource.rolloffMode = AudioRolloffMode.Linear;
            faultSource.dopplerLevel = 0f;
            faultSource.spread = 0f;
            faultSource.priority = 136;

            primaryFlicker.humSource = humSource;
            foreach (var flicker in flickers)
                flicker.faultSource = faultSource;
            Debug.Log($"Raccoon Heist: generated localized neon transformer hum and independent fault crackle for {flickers.Length} letters.");
        }

        static void BuildExteriorSteamAudio()
        {
            var valve = FindGeneratedTransform("PassageReleaseValve");
            if (valve == null)
            {
                Debug.LogWarning("Raccoon Heist: passage release valve is missing; steam hiss was not created.");
                return;
            }

            var clip = EnsureGeneratedAudioClip("steam_release_hiss_v1.wav", 44100, 2f, SteamHissSample);
            if (clip == null)
            {
                Debug.LogWarning("Raccoon Heist: generated steam hiss could not be imported.");
                return;
            }

            var audioHolder = new GameObject("PassageReleaseValveAudio");
            audioHolder.transform.SetParent(valve, false);
            audioHolder.transform.localPosition = Vector3.zero;
            var source = audioHolder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.075f;
            source.spatialBlend = 1f;
            source.minDistance = 0.45f;
            source.maxDistance = 4.8f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.priority = 150;
            Debug.Log("Raccoon Heist: generated localized passage release-valve steam hiss.");
        }

        static AudioClip EnsureGeneratedAudioClip(string fileName, int sampleRate, float duration,
            System.Func<int, float> sampleAt)
        {
            const string parentFolder = "Assets/Audio/Environment";
            const string generatedFolder = parentFolder + "/Generated";
            if (!AssetDatabase.IsValidFolder(generatedFolder))
                AssetDatabase.CreateFolder(parentFolder, "Generated");

            string path = $"{generatedFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null) return existing;

            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = Mathf.Clamp(sampleAt(i), -1f, 1f);
            System.IO.File.WriteAllBytes(path, EncodeMonoPcmWav(samples, sampleRate));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static byte[] EncodeMonoPcmWav(float[] samples, int sampleRate)
        {
            int dataLength = samples.Length * sizeof(short);
            var stream = new System.IO.MemoryStream(44 + dataLength);
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataLength);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * sizeof(short));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataLength);
                foreach (float sample in samples)
                    writer.Write((short)Mathf.RoundToInt(sample * short.MaxValue));
            }
            return stream.ToArray();
        }

        static float NeonHumSample(int sampleIndex)
        {
            const float sampleRate = 44100f;
            float time = sampleIndex / sampleRate;
            float amplitudeWander = 0.94f + 0.06f * Mathf.Sin(2f * Mathf.PI * 0.5f * time);
            float transformer =
                0.14f * Mathf.Sin(2f * Mathf.PI * 50f * time)
                + 0.54f * Mathf.Sin(2f * Mathf.PI * 100f * time)
                + 0.18f * Mathf.Sin(2f * Mathf.PI * 200f * time)
                + 0.07f * Mathf.Sin(2f * Mathf.PI * 300f * time)
                + 0.035f * Mathf.Sin(2f * Mathf.PI * 850f * time);
            return transformer * amplitudeWander * 0.62f;
        }

        static float NeonFaultSample(int sampleIndex)
        {
            const float sampleRate = 44100f;
            float time = sampleIndex / sampleRate;
            float noise = Hash(sampleIndex, 917) * 2f - 1f;
            float initialArc = noise * Mathf.Exp(-time * 52f) * 0.72f
                + Mathf.Sin(2f * Mathf.PI * 1900f * time) * Mathf.Exp(-time * 68f) * 0.32f;
            float secondaryTime = Mathf.Max(0f, time - 0.032f);
            float secondaryArc = time < 0.032f ? 0f
                : (Hash(sampleIndex, 271) * 2f - 1f) * Mathf.Exp(-secondaryTime * 70f) * 0.48f;
            return Mathf.Clamp(initialArc + secondaryArc, -0.95f, 0.95f);
        }

        static float SteamHissSample(int sampleIndex)
        {
            const float sampleRate = 44100f;
            const int loopSamples = 88200;
            int previous = sampleIndex == 0 ? loopSamples - 1 : sampleIndex - 1;
            int next = sampleIndex == loopSamples - 1 ? 0 : sampleIndex + 1;
            float white = Hash(sampleIndex, 6401) * 2f - 1f;
            float shoulder = (Hash(previous, 6401) + Hash(next, 6401)) - 1f;
            float time = sampleIndex / sampleRate;
            float pressureWander = 0.91f + 0.09f * Mathf.Sin(2f * Mathf.PI * 0.5f * time);
            float pipeTone = 0.08f * Mathf.Sin(2f * Mathf.PI * 184f * time)
                + 0.035f * Mathf.Sin(2f * Mathf.PI * 368f * time);
            return Mathf.Clamp(((white * 0.72f + shoulder * 0.28f) * 0.19f + pipeTone)
                * pressureWander, -0.48f, 0.48f);
        }

        static void BuildShopAlarm()
        {
            const string alarmPath = "Assets/Audio/Environment/security_alarm.mp3";
            var alarmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(alarmPath);
            if (alarmClip == null)
                Debug.LogWarning($"Raccoon Heist: storefront alarm clip is missing at {alarmPath}.");

            var alarmSystem = new GameObject("ShopAlarmSystem");
            alarmSystem.transform.SetParent(root, false);

            var alarmSpeaker = new GameObject("AlarmSpeaker");
            alarmSpeaker.transform.SetParent(alarmSystem.transform, false);
            alarmSpeaker.transform.localPosition = new Vector3(EntranceX, 2.35f, 0.24f);

            // Give the localized source a visible origin just inside the entrance.
            var speakerFixture = PlaceSyntyDecorative("SM_Prop_FireAlarmBell_01",
                new Vector3(EntranceX, 0f, 0.24f), 180f);
            if (speakerFixture != null)
            {
                speakerFixture.name = "AlarmSpeakerFixture";
                var fixtureBounds = GeometryBounds(speakerFixture);
                speakerFixture.transform.position += Vector3.up * (2.35f - fixtureBounds.center.y);
                speakerFixture.transform.SetParent(alarmSystem.transform, true);
            }

            var alarmSource = alarmSpeaker.AddComponent<AudioSource>();
            alarmSource.clip = alarmClip;
            alarmSource.loop = true;
            alarmSource.playOnAwake = false;
            alarmSource.volume = 0.72f;
            alarmSource.spatialBlend = 1f;
            alarmSource.minDistance = 1f;
            alarmSource.maxDistance = 12f;
            alarmSource.rolloffMode = AudioRolloffMode.Linear;
            alarmSource.dopplerLevel = 0f;
            alarmSource.spread = 0f;
            alarmSource.priority = 72;

            var beaconRotors = new[]
            {
                CreateAlarmBeacon(alarmSystem.transform, "AlarmBeacon_Front", new Vector3(3.1f, 2.76f, 4f), 20f),
                CreateAlarmBeacon(alarmSystem.transform, "AlarmBeacon_Rear", new Vector3(10.9f, 2.76f, 12f), 200f)
            };

            var door = root.Find("EntranceDoorPivot")?.GetComponent<HingedDoor>();
            if (door == null)
                Debug.LogWarning("Raccoon Heist: storefront alarm could not find EntranceDoorPivot/HingedDoor.");
            var alarmController = alarmSystem.AddComponent<ShopAlarmController>();
            alarmController.Configure(door, alarmSource, beaconRotors, InteriorAudioZones());

            bool localized = Mathf.Approximately(alarmSource.spatialBlend, 1f)
                && alarmSource.maxDistance <= 12f
                && alarmSpeaker.transform.localPosition.z < 1f;
            bool interiorCovered = alarmController.IsInteriorPosition(new Vector3(0.1f, 0.35f, 0.1f))
                && alarmController.IsInteriorPosition(new Vector3(W - 0.1f, 0.35f, D - 0.1f))
                && alarmController.IsInteriorPosition(new Vector3(1.5f, 0.35f, D + T + 2f))
                && alarmController.IsInteriorPosition(new Vector3(8f, 0.35f, D + T + 2.5f))
                && !alarmController.IsInteriorPosition(new Vector3(EntranceX, 0.35f, -2f));
            if (localized && interiorCovered)
                Debug.Log("Raccoon Heist: storefront alarm coverage check passed (full interior mix; localized entrance spill).");
            else
                Debug.LogWarning("Raccoon Heist: storefront alarm coverage check failed.");
        }

        static Transform CreateAlarmBeacon(Transform parent, string name, Vector3 localPosition, float initialYaw)
        {
            var beacon = new GameObject(name).transform;
            beacon.SetParent(parent, false);
            beacon.localPosition = localPosition;

            var housingMaterial = Mat("AlarmBeaconHousing", new Color(0.09f, 0.10f, 0.12f), 0.28f);
            var reflectorMaterial = Mat("AlarmBeaconReflector", new Color(0.56f, 0.58f, 0.62f), 0.88f);
            var glassMaterial = TransparentMat("AlarmBeaconGlass",
                new Color(0.58f, 0.035f, 0.018f, 0.42f), 0.96f);
            var hotLensMaterial = EmissiveMat("AlarmBeaconHotLens", new Color(1f, 0.055f, 0.025f),
                new Color(18f, 0.12f, 0.035f));

            // The plate reaches the three-metre ceiling, and the neck closes the gap
            // between it and the beacon base so the fixture never reads as floating.
            var ceilingMount = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ceilingMount.name = "CeilingPlate";
            ceilingMount.transform.SetParent(beacon, false);
            ceilingMount.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ceilingMount.transform.localScale = new Vector3(0.24f, 0.06f, 0.24f);
            ApplyMat(ceilingMount, housingMaterial);
            Object.DestroyImmediate(ceilingMount.GetComponent<Collider>());

            var mountNeck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mountNeck.name = "MountNeck";
            mountNeck.transform.SetParent(beacon, false);
            mountNeck.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            mountNeck.transform.localScale = new Vector3(0.16f, 0.05f, 0.16f);
            ApplyMat(mountNeck, housingMaterial);
            Object.DestroyImmediate(mountNeck.GetComponent<Collider>());

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "BeaconRim";
            rim.transform.SetParent(beacon, false);
            rim.transform.localScale = new Vector3(0.28f, 0.035f, 0.28f);
            ApplyMat(rim, housingMaterial);
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            // The glass belongs to the fixed housing, not the rotor. It therefore
            // remains visible while the alarm is idle and clearly encloses every
            // moving reflector, lens, bulb, and light origin.
            var glassDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glassDome.name = "BeaconGlassDome";
            glassDome.transform.SetParent(beacon, false);
            glassDome.transform.localPosition = new Vector3(0f, -0.11f, 0f);
            glassDome.transform.localScale = new Vector3(0.28f, 0.22f, 0.28f);
            ApplyMat(glassDome, glassMaterial);
            var glassRenderer = glassDome.GetComponent<Renderer>();
            glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glassRenderer.receiveShadows = false;
            Object.DestroyImmediate(glassDome.GetComponent<Collider>());

            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(beacon, false);
            rotor.localRotation = Quaternion.Euler(0f, initialYaw, 0f);

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "BeaconBulb";
            bulb.transform.SetParent(rotor, false);
            bulb.transform.localPosition = new Vector3(0f, -0.11f, 0f);
            bulb.transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
            ApplyMat(bulb, hotLensMaterial);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());

            // Reflectors and bright lens panels turn inside the fixed glass dome, making
            // the sweep readable even when the cast light pools are outside the view.
            var spindle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spindle.name = "ReflectorSpindle";
            spindle.transform.SetParent(rotor, false);
            spindle.transform.localPosition = new Vector3(0f, -0.11f, 0f);
            spindle.transform.localScale = new Vector3(0.025f, 0.05f, 0.025f);
            ApplyMat(spindle, housingMaterial);
            Object.DestroyImmediate(spindle.GetComponent<Collider>());

            var frontReflector = Box("Reflector_Front", new Vector3(0f, -0.11f, 0.04f),
                new Vector3(0.11f, 0.075f, 0.014f), rotor, reflectorMaterial);
            Object.DestroyImmediate(frontReflector.GetComponent<Collider>());
            var rearReflector = Box("Reflector_Rear", new Vector3(0f, -0.11f, -0.04f),
                new Vector3(0.11f, 0.075f, 0.014f), rotor, reflectorMaterial);
            Object.DestroyImmediate(rearReflector.GetComponent<Collider>());

            var frontLens = Box("HotLens_Front", new Vector3(0f, -0.11f, 0.082f),
                new Vector3(0.085f, 0.064f, 0.012f), rotor, hotLensMaterial);
            Object.DestroyImmediate(frontLens.GetComponent<Collider>());
            var rearLens = Box("HotLens_Rear", new Vector3(0f, -0.11f, -0.082f),
                new Vector3(0.085f, 0.064f, 0.012f), rotor, hotLensMaterial);
            Object.DestroyImmediate(rearLens.GetComponent<Collider>());

            var beam = new GameObject("RotatingRedBeam").AddComponent<Light>();
            beam.transform.SetParent(rotor, false);
            beam.transform.localPosition = new Vector3(0f, -0.11f, 0.065f);
            beam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.18f, 1f));
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.04f, 0.02f);
            beam.intensity = 18f;
            beam.range = 12f;
            beam.spotAngle = 50f;
            beam.innerSpotAngle = 18f;
            beam.shadows = LightShadows.None;

            var reverseBeam = new GameObject("RotatingRedBeam_Reverse").AddComponent<Light>();
            reverseBeam.transform.SetParent(rotor, false);
            reverseBeam.transform.localPosition = new Vector3(0f, -0.11f, -0.065f);
            reverseBeam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.18f, -1f));
            reverseBeam.type = LightType.Spot;
            reverseBeam.color = new Color(1f, 0.025f, 0.012f);
            reverseBeam.intensity = 14f;
            reverseBeam.range = 10f;
            reverseBeam.spotAngle = 44f;
            reverseBeam.innerSpotAngle = 16f;
            reverseBeam.shadows = LightShadows.None;

            var localGlow = new GameObject("RedBeaconGlow").AddComponent<Light>();
            localGlow.transform.SetParent(rotor, false);
            localGlow.transform.localPosition = new Vector3(0f, -0.11f, 0f);
            localGlow.type = LightType.Point;
            localGlow.color = new Color(1f, 0.025f, 0.012f);
            localGlow.intensity = 4.8f;
            localGlow.range = 5.5f;
            localGlow.shadows = LightShadows.None;

            rotor.gameObject.SetActive(false);
            return rotor;
        }

        static Light PointLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.transform.position = pos;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        static Light SpotLight(string name, Vector3 pos, Vector3 direction, Color color, float intensity,
            float range, float angle, bool shadows, string fixtureName = null)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Spot;
            light.transform.position = pos;
            light.transform.rotation = Quaternion.LookRotation(direction.normalized);
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.48f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (!string.IsNullOrEmpty(fixtureName))
                ParentLightToFixture(light, fixtureName);
            return light;
        }

        // POLYGON lamp prefabs split their pole/arm/head into fitted mesh colliders.
        // The highest compact collider is the physical luminaire, so its underside
        // is a more reliable source position than an overall prefab-bounds guess.
        static Bounds FindLampHeadBounds(GameObject fixture)
        {
            var overall = GeometryBounds(fixture);
            Bounds best = overall;
            float bestScore = float.NegativeInfinity;
            foreach (var collider in fixture.GetComponentsInChildren<Collider>(true))
            {
                Bounds candidate = collider.bounds;
                if (candidate.size.y > overall.size.y * 0.62f) continue;
                float score = candidate.center.y - candidate.size.y * 0.15f;
                if (score <= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        static Vector3 LampUnderside(Bounds head)
            => new(head.center.x, head.min.y + Mathf.Min(0.018f, head.size.y * 0.20f), head.center.z);

        static void ParentLightToFixture(Light light, string fixtureName)
        {
            if (light == null) return;
            var fixture = FindGeneratedTransform(fixtureName);
            if (fixture == null)
            {
                Debug.LogWarning($"Raccoon Heist: exterior light {light.name} could not find fixture {fixtureName}.");
                return;
            }
            ParentLightToFixture(light, fixture.gameObject);
        }

        static void ParentLightToFixture(Light light, GameObject fixture)
        {
            if (light == null || fixture == null) return;
            light.transform.SetParent(fixture.transform, true);
        }

        static Transform FindGeneratedTransform(string name)
        {
            if (root == null) return null;
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            return null;
        }

        static void ValidateExteriorLightFixtures()
        {
            int checkedCount = 0;
            var missing = new List<string>();
            var darkFixtures = new List<string>();
            var floatingSources = new List<string>();
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (!IsExteriorFixtureLight(light)) continue;
                checkedCount++;
                var fixture = light.transform.parent;
                if (fixture == null || fixture == root
                    || fixture.GetComponentInChildren<Renderer>(true) == null)
                    missing.Add(light.name);
                else if (light.type == LightType.Spot && !FixtureHasEmissiveSurface(fixture))
                    darkFixtures.Add(light.name);
                else if (light.type == LightType.Spot && !FixtureContainsLightOrigin(light, fixture))
                    floatingSources.Add(light.name);
            }

            if (missing.Count == 0 && darkFixtures.Count == 0 && floatingSources.Count == 0)
                Debug.Log($"Raccoon Heist: exterior light fixture check passed ({checkedCount} sourced lights; no floating emitter geometry or Light origins).");
            else
                Debug.LogWarning($"Raccoon Heist: exterior light fixture issues. Missing fixtures: {string.Join(", ", missing)}. "
                    + $"Non-emissive fixtures: {string.Join(", ", darkFixtures)}. "
                    + $"Floating Light origins: {string.Join(", ", floatingSources)}.");
        }

        static bool FixtureHasEmissiveSurface(Transform fixture)
        {
            foreach (var renderer in fixture.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null
                        && (material.IsKeywordEnabled("_EMISSION")
                            || material.HasProperty("_Enable_Emission")
                               && material.GetFloat("_Enable_Emission") > 0.5f))
                        return true;
            return false;
        }

        static bool FixtureContainsLightOrigin(Light light, Transform fixture)
        {
            foreach (var collider in fixture.GetComponentsInChildren<Collider>(true))
            {
                Bounds physicalPart = collider.bounds;
                physicalPart.Expand(0.01f);
                if (physicalPart.Contains(light.transform.position)) return true;
            }

            // Decorative fixtures deliberately have their colliders stripped, so
            // renderer bounds are the fallback only for those wall-mounted meshes.
            bool hasBody = false;
            Bounds bodyBounds = default;
            foreach (var renderer in fixture.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBody) { bodyBounds = renderer.bounds; hasBody = true; }
                else bodyBounds.Encapsulate(renderer.bounds);
            }
            if (!hasBody) return false;
            bodyBounds.Expand(0.02f);
            return bodyBounds.Contains(light.transform.position);
        }

        static bool IsExteriorFixtureLight(Light light)
        {
            string name = light.name;
            return name.StartsWith("LampSpot_")
                || name.EndsWith("_Light")
                || name.StartsWith("Alley")
                || name == "DenGlow"
                || name.StartsWith("Passage")
                || name == "AlleySecondaryLight"
                || name == "EntranceCanopyPool"
                || name == "NeighbourFrontageGlow"
                || name.EndsWith("NeonGlow")
                || name == "OpenSignGlow"
                || name == "RaccoonHeistSignGlow";
        }

        // Procedural night sky: gradient with city light-pollution at the horizon,
        // stars that thin out low, drifting cloud noise, and a painted moon
        static Texture2D TexNightSky => EnsureTex("tex_nightsky_v3", (x, y) =>
        {
            float v = y / 511f;
            float alt = Mathf.Clamp01((v - 0.5f) * 2f);
            var horizon = new Color(0.21f, 0.15f, 0.12f);
            var zenith = new Color(0.012f, 0.022f, 0.062f);
            Color c = v >= 0.5f
                ? Color.Lerp(horizon, zenith, Mathf.Sqrt(alt))
                : Color.Lerp(horizon, new Color(0.01f, 0.01f, 0.015f), (0.5f - v) * 3f);
            float cloud = Mathf.PerlinNoise(x * 0.008f, y * 0.02f);
            c *= 0.92f + 0.16f * cloud;
            if (alt > 0.05f && Hash(x, y) > 0.9988f - alt * 0.0012f)
            {
                float b = 0.35f + 0.65f * Hash(x + 7, y + 3);
                c += new Color(b, b, b * 0.95f);
            }
            float dx = x - 720f, dy = y - 400f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < 14f) c += new Color(0.95f, 0.96f, 1f);
            else if (dist < 60f) c += new Color(0.5f, 0.55f, 0.7f) * Mathf.Pow(1f - (dist - 14f) / 46f, 2.5f);
            return c;
        }, 1024, 512);

        // Post-processing: the difference between "3D scene" and "night movie still".
        // Bloom makes every emissive window/lamp halo; grain+vignette kill the flatness.
        static void BuildAtmosphere()
        {
            string path = "Assets/Materials/Greybox/NightPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            // Drop null holes left by earlier runs: profile.Add() creates components
            // in memory only, so without AddObjectToAsset the saved profile held six
            // {fileID: 0} entries and post-processing silently vanished whenever the
            // asset reloaded from disk — the "scene randomly turns light" bug.
            profile.components.RemoveAll(c => c == null);
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c))
                {
                    c = profile.Add<T>();
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                return c;
            }
            var bloom = Get<Bloom>();
            bloom.intensity.Override(0.82f);
            bloom.threshold.Override(0.82f);
            bloom.scatter.Override(0.72f);
            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>();
            // A small global lift keeps the blue-black night intact while making
            // dark road surfaces, curb props, and unlit facade detail readable.
            color.postExposure.Override(0.78f);
            color.contrast.Override(9f);
            color.saturation.Override(-8f);
            var wb = Get<WhiteBalance>();
            wb.temperature.Override(-14f);
            var vig = Get<Vignette>();
            vig.intensity.Override(0.15f);
            vig.smoothness.Override(0.52f);
            var grain = Get<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Own exactly one active global volume. Reusing the template scene's first
            // global volume made edit/play transitions dependent on discovery order and
            // could briefly show the scene with a completely different exposure.
            foreach (var existing in Object.FindObjectsByType<Volume>())
                if (existing.isGlobal) existing.enabled = false;
            var go = new GameObject("NightVolume");
            go.transform.SetParent(root, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            vol.weight = 1f;
            vol.blendDistance = 0f;
            vol.sharedProfile = profile;
        }

    }
}
