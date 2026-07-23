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

        bool alarmActive;
        bool hasTriggered;

        public bool IsAlarmActive => alarmActive;

        public void Configure(HingedDoor door, AudioSource source, Transform[] rotors)
        {
            if (isActiveAndEnabled && storefrontDoor != null)
                storefrontDoor.OpenedFromStreet -= TriggerAlarm;

            storefrontDoor = door;
            alarmSource = source;
            beaconRotors = rotors;

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
            if (!alarmActive || beaconRotors == null) return;
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
    }
}
