using System;
using UnityEngine;

namespace RaccoonHeist.World
{
    // Localized glass-neon fault behavior. Healthy tubes stay steady; one failing
    // tube group chatters or drops out without pulsing the whole storefront.
    [DisallowMultipleComponent]
    public sealed class NeonSignFlicker : MonoBehaviour
    {
        [Header("Faulty tube renderers")]
        public Renderer[] targetRenderers = Array.Empty<Renderer>();
        [ColorUsage(false, true)] public Color litBaseColor = new(0.08f, 0.48f, 0.62f);
        [ColorUsage(false, true)] public Color litEmission = new(0.2f, 2.4f, 3.2f);

        [Header("Time between faults (seconds)")]
        [Min(0.1f)] public float quietDurationMin = 5f;
        [Min(0.1f)] public float quietDurationMax = 14f;

        [Header("Contact chatter (seconds)")]
        [Min(0.01f)] public float chatterDurationMin = 0.28f;
        [Min(0.01f)] public float chatterDurationMax = 0.72f;
        [Min(0.01f)] public float pulseIntervalMin = 0.035f;
        [Min(0.01f)] public float pulseIntervalMax = 0.11f;
        [Range(0f, 1f)] public float offPulseChance = 0.62f;
        [Range(0f, 1f)] public float dimLevel = 0.16f;

        [Header("Occasional sustained dropout")]
        [Range(0f, 1f)] public float blackoutChance = 0.35f;
        [Min(0.01f)] public float blackoutDurationMin = 0.45f;
        [Min(0.01f)] public float blackoutDurationMax = 1.25f;

        [Header("Nearby glow")]
        public Light spillLight;
        [Range(0f, 0.25f)] public float spillLightInfluence = 0.08f;

        [Header("Localized electrical audio")]
        public AudioSource humSource;
        public AudioSource faultSource;
        [Range(0f, 0.25f)] public float humVolume = 0.045f;
        [Range(0f, 0.25f)] public float faultHumVolume = 0.085f;
        [Range(0f, 0.25f)] public float blackoutHumVolume = 0.008f;
        [Range(0f, 0.5f)] public float faultClickVolume = 0.12f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock propertyBlock;
        System.Random random;
        float baseSpillIntensity;
        float nextFaultAt;
        float faultEndsAt;
        float blackoutStartsAt = float.PositiveInfinity;
        float blackoutEndsAt = float.NegativeInfinity;
        float nextPulseAt;
        float currentLevel = -1f;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            random = new System.Random(gameObject.name.GetHashCode() ^ Environment.TickCount);
            if (spillLight != null) baseSpillIntensity = spillLight.intensity;
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (random == null) random = new System.Random(gameObject.name.GetHashCode() ^ Environment.TickCount);
            if (spillLight != null) baseSpillIntensity = spillLight.intensity;
            faultEndsAt = Time.time;
            blackoutStartsAt = float.PositiveInfinity;
            blackoutEndsAt = float.NegativeInfinity;
            ApplyLevel(1f);
            ScheduleNextFault(Time.time);
            if (humSource != null && humSource.clip != null && !humSource.isPlaying)
                humSource.Play();
        }

        void Update()
        {
            float now = Time.time;
            if (now >= faultEndsAt)
            {
                if (currentLevel < 1f) ApplyLevel(1f);
                if (now >= nextFaultAt) BeginFault(now);
                return;
            }

            if (now >= blackoutStartsAt && now < blackoutEndsAt)
            {
                ApplyLevel(0.015f);
                SetHumVolume(blackoutHumVolume);
                return;
            }

            if (now < nextPulseAt) return;
            float level = Next01() < offPulseChance ? 0.015f : Range(dimLevel, 0.72f);
            ApplyLevel(level);
            SetHumVolume(Mathf.Lerp(faultHumVolume, humVolume, level));
            nextPulseAt = now + Range(pulseIntervalMin, pulseIntervalMax);
        }

        void BeginFault(float now)
        {
            float chatterDuration = Range(chatterDurationMin, chatterDurationMax);
            faultEndsAt = now + chatterDuration;
            blackoutStartsAt = float.PositiveInfinity;
            blackoutEndsAt = float.NegativeInfinity;

            if (Next01() < blackoutChance)
            {
                blackoutStartsAt = now + Range(0.06f, 0.18f);
                blackoutEndsAt = blackoutStartsAt + Range(blackoutDurationMin, blackoutDurationMax);
                faultEndsAt = Mathf.Max(faultEndsAt, blackoutEndsAt + Range(0.08f, 0.24f));
            }

            nextPulseAt = now;
            ScheduleNextFault(faultEndsAt);
        }

        void ScheduleNextFault(float fromTime)
        {
            nextFaultAt = fromTime + Range(quietDurationMin, quietDurationMax);
        }

        void ApplyLevel(float level)
        {
            if (Mathf.Approximately(currentLevel, level)) return;
            currentLevel = level;

            // At full strength, remove our override completely. The healthy I then
            // renders from the exact same shared material as every other title letter.
            if (level >= 0.999f)
            {
                foreach (var targetRenderer in targetRenderers)
                {
                    if (targetRenderer != null)
                        targetRenderer.SetPropertyBlock(null);
                }
                if (spillLight != null) spillLight.intensity = baseSpillIntensity;
                SetHumVolume(humVolume);
                return;
            }

            float baseLevel = Mathf.Clamp01(level * 2f);
            Color baseColor = Color.Lerp(litBaseColor * 0.08f, litBaseColor, baseLevel);
            baseColor.a = litBaseColor.a;
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(LegacyColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, litEmission * level);
            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer != null)
                    targetRenderer.SetPropertyBlock(propertyBlock);
            }

            if (spillLight != null)
                spillLight.intensity = baseSpillIntensity
                    * Mathf.Lerp(1f - spillLightInfluence, 1f, level);
            SetHumVolume(Mathf.Lerp(faultHumVolume, humVolume, level));
            if (Application.isPlaying && faultSource != null && faultSource.clip != null)
            {
                faultSource.pitch = Range(0.88f, 1.12f);
                faultSource.PlayOneShot(faultSource.clip,
                    faultClickVolume * Mathf.Lerp(1f, 0.55f, level));
            }
        }

        void SetHumVolume(float volume)
        {
            if (humSource != null) humSource.volume = volume;
        }

        float Range(float min, float max)
        {
            if (max <= min) return min;
            return min + Next01() * (max - min);
        }

        float Next01() => (float)random.NextDouble();

        void OnDisable()
        {
            if (Application.isPlaying && propertyBlock != null)
                ApplyLevel(1f);
            if (humSource != null) humSource.Stop();
            if (faultSource != null) faultSource.Stop();
        }

        void OnValidate()
        {
            quietDurationMax = Mathf.Max(quietDurationMin, quietDurationMax);
            chatterDurationMax = Mathf.Max(chatterDurationMin, chatterDurationMax);
            pulseIntervalMax = Mathf.Max(pulseIntervalMin, pulseIntervalMax);
            blackoutDurationMax = Mathf.Max(blackoutDurationMin, blackoutDurationMax);
        }
    }
}
