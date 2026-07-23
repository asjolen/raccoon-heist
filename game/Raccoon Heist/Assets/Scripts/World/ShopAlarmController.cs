using UnityEngine;

namespace RaccoonHeist.World
{
    // Prototype alarm tied only to street-side storefront openings. The generated scene supplies the
    // audio source and beacon rotors; this behaviour remains one small FishNet port.
    [DisallowMultipleComponent]
    public sealed class ShopAlarmController : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] HingedDoor storefrontDoor;
        [SerializeField] bool triggerOnlyOnce = true;

        [Header("Alarm output")]
        [SerializeField] AudioSource alarmSource;
        [SerializeField] Transform[] beaconRotors;
        [SerializeField] float beaconDegreesPerSecond = 250f;
        [SerializeField] bool counterRotateBeacons = true;

        [Header("Interior coverage")]
        [SerializeField] Bounds[] interiorZones = System.Array.Empty<Bounds>();
        [SerializeField, Range(0f, 1f)] float interiorSpatialBlend;
        [SerializeField, Range(0f, 1f)] float exteriorSpatialBlend = 1f;
        [SerializeField, Range(0f, 1f)] float interiorVolume = 0.62f;
        [SerializeField, Range(0f, 1f)] float exteriorVolume = 0.72f;
        [SerializeField, Min(0.05f)] float mixTransitionSeconds = 0.35f;

        bool alarmActive;
        bool hasTriggered;
        Transform listener;

        public bool IsAlarmActive => alarmActive;
        public bool IsInteriorPosition(Vector3 worldPosition) => IsInside(worldPosition);

        public void Configure(HingedDoor door, AudioSource source, Transform[] rotors, Bounds[] zones)
        {
            if (isActiveAndEnabled && storefrontDoor != null)
                storefrontDoor.OpenedFromStreet -= TriggerAlarm;

            storefrontDoor = door;
            alarmSource = source;
            beaconRotors = rotors;
            interiorZones = zones ?? System.Array.Empty<Bounds>();

            if (isActiveAndEnabled && storefrontDoor != null)
                storefrontDoor.OpenedFromStreet += TriggerAlarm;

            SetBeaconState(false);
        }

        void OnEnable()
        {
            if (storefrontDoor != null)
                storefrontDoor.OpenedFromStreet += TriggerAlarm;
        }

        void OnDisable()
        {
            if (storefrontDoor != null)
                storefrontDoor.OpenedFromStreet -= TriggerAlarm;
        }

        void Update()
        {
            if (!alarmActive) return;
            UpdateAlarmMix(false);
            if (beaconRotors == null) return;

            float rotation = beaconDegreesPerSecond * Time.deltaTime;
            for (int i = 0; i < beaconRotors.Length; i++)
            {
                var rotor = beaconRotors[i];
                if (rotor == null) continue;
                float direction = counterRotateBeacons && (i & 1) == 1 ? -1f : 1f;
                rotor.Rotate(0f, rotation * direction, 0f, Space.Self);
            }
        }

        public void TriggerAlarm()
        {
            if (triggerOnlyOnce && hasTriggered) return;
            hasTriggered = true;
            alarmActive = true;
            SetBeaconState(true);
            ResolveListener();
            UpdateAlarmMix(true);
            if (alarmSource != null && alarmSource.clip != null && !alarmSource.isPlaying)
                alarmSource.Play();
        }

        public void StopAlarm()
        {
            alarmActive = false;
            SetBeaconState(false);
            if (alarmSource != null) alarmSource.Stop();
        }

        public void ResetAlarm()
        {
            hasTriggered = false;
            StopAlarm();
        }

        void SetBeaconState(bool active)
        {
            if (beaconRotors == null) return;
            foreach (var rotor in beaconRotors)
                if (rotor != null)
                    rotor.gameObject.SetActive(active);
        }

        void UpdateAlarmMix(bool immediate)
        {
            if (alarmSource == null) return;
            if (listener == null) ResolveListener();

            bool inside = listener != null && IsInside(listener.position);
            float targetBlend = inside ? interiorSpatialBlend : exteriorSpatialBlend;
            float targetVolume = inside ? interiorVolume : exteriorVolume;
            if (immediate)
            {
                alarmSource.spatialBlend = targetBlend;
                alarmSource.volume = targetVolume;
                return;
            }

            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, mixTransitionSeconds);
            alarmSource.spatialBlend = Mathf.MoveTowards(alarmSource.spatialBlend, targetBlend, step);
            alarmSource.volume = Mathf.MoveTowards(alarmSource.volume, targetVolume, step);
        }

        void ResolveListener()
        {
            var activeListener = FindFirstObjectByType<AudioListener>();
            listener = activeListener != null ? activeListener.transform : null;
        }

        bool IsInside(Vector3 worldPosition)
        {
            foreach (var zone in interiorZones)
                if (zone.Contains(worldPosition)) return true;
            return false;
        }

        void OnValidate()
        {
            interiorSpatialBlend = Mathf.Clamp01(interiorSpatialBlend);
            exteriorSpatialBlend = Mathf.Clamp01(exteriorSpatialBlend);
            interiorVolume = Mathf.Clamp01(interiorVolume);
            exteriorVolume = Mathf.Clamp01(exteriorVolume);
            mixTransitionSeconds = Mathf.Max(0.05f, mixTransitionSeconds);
        }
    }
}
