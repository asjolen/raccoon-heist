using UnityEngine;

namespace RaccoonHeist.World
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ExteriorAmbienceController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float exteriorVolume = 0.55f;
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.8f;
        [SerializeField] Bounds[] interiorZones = System.Array.Empty<Bounds>();

        AudioSource source;
        Transform listener;

        public void Configure(float volume, float fadeDuration, Bounds[] zones)
        {
            exteriorVolume = Mathf.Clamp01(volume);
            fadeSeconds = Mathf.Max(0.05f, fadeDuration);
            interiorZones = zones ?? System.Array.Empty<Bounds>();
        }

        public bool IsInteriorPosition(Vector3 worldPosition) => IsInside(worldPosition);

        void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        void Start()
        {
            ResolveListener();
            source.volume = listener != null && IsInside(listener.position) ? 0f : exteriorVolume;
            if (source.clip != null) source.Play();
        }

        void Update()
        {
            if (source.clip == null) return;
            if (!source.isPlaying) source.Play();
            if (listener == null) ResolveListener();

            float target = listener != null && IsInside(listener.position) ? 0f : exteriorVolume;
            float speed = exteriorVolume / Mathf.Max(0.05f, fadeSeconds);
            source.volume = Mathf.MoveTowards(source.volume, target, speed * Time.unscaledDeltaTime);
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
            exteriorVolume = Mathf.Clamp01(exteriorVolume);
            fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
        }
    }
}
