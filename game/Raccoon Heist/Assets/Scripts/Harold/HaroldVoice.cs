using UnityEngine;

namespace RaccoonHeist.Harold
{
    // Harold's in-world sound identity. Every state change is audible before it is
    // visible: the snore is the all-clear, its absence is the alarm. Two sources so
    // the snore loop and one-shot voice lines never cut each other off.
    public sealed class HaroldVoice : MonoBehaviour
    {
        public AudioClip snoreLoop;
        public AudioClip[] grumbles = System.Array.Empty<AudioClip>();
        public AudioClip[] hearSomethingLines = System.Array.Empty<AudioClip>();
        public AudioClip[] cantSeeYouLines = System.Array.Empty<AudioClip>();

        [SerializeField, Range(0f, 1f)] float snoreVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float voiceVolume = 1f;
        [SerializeField, Min(0.1f)] float snoreFullVolumeDistance = 1.8f;
        [SerializeField, Min(0.1f)] float voiceFullVolumeDistance = 3.5f;
        [SerializeField, Min(1f)] float audibleRange = 22f;

        AudioSource loopSource;
        AudioSource voiceSource;
        int lastGrumble = -1;

        void Awake()
        {
            loopSource = CreateSource(snoreVolume, snoreFullVolumeDistance, loop: true);
            voiceSource = CreateSource(voiceVolume, voiceFullVolumeDistance, loop: false);
        }

        AudioSource CreateSource(float volume, float fullVolumeDistance, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = fullVolumeDistance;
            source.maxDistance = audibleRange;
            source.dopplerLevel = 0f;
            return source;
        }

        public bool IsSpeaking => voiceSource != null && voiceSource.isPlaying;

        public void StartSnore()
        {
            if (snoreLoop == null || loopSource.isPlaying) return;
            loopSource.clip = snoreLoop;
            loopSource.Play();
        }

        public void StopSnore() => loopSource.Stop();

        public void PlayGrumble() => PlayRandom(grumbles, ref lastGrumble);
        public void PlayHearSomething() { int _ = -1; PlayRandom(hearSomethingLines, ref _); }
        public void PlayCantSeeYou() { int _ = -1; PlayRandom(cantSeeYouLines, ref _); }

        void PlayRandom(AudioClip[] clips, ref int lastIndex)
        {
            if (clips.Length == 0 || IsSpeaking) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex) index = (index + 1) % clips.Length;
            lastIndex = index;
            voiceSource.PlayOneShot(clips[index]);
        }
    }
}
