using UnityEngine;
using UnityEngine.AI;

namespace RaccoonHeist.Harold
{
    // Slipper steps triggered by distance travelled, not animation events — robust
    // against clip retargeting and blend speeds. The shuffle cadence is diegetic
    // information: players track Harold through shelves by ear.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HaroldFootsteps : MonoBehaviour
    {
        public AudioClip[] stepClips = System.Array.Empty<AudioClip>();
        [SerializeField, Min(0.1f)] float strideLength = 0.65f;
        [SerializeField, Range(0f, 1f)] float volume = 0.55f;
        [SerializeField] Vector2 pitchJitter = new(0.92f, 1.08f);
        [SerializeField, Min(1f)] float audibleRange = 14f;

        NavMeshAgent agent;
        AudioSource source;
        float distanceSinceStep;
        int lastIndex = -1;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = audibleRange;
            source.dopplerLevel = 0f;
        }

        void Update()
        {
            float speed = agent.velocity.magnitude;
            if (speed < 0.05f)
            {
                // Prime most of a stride so the first step lands just after he moves off.
                distanceSinceStep = strideLength * 0.7f;
                return;
            }

            distanceSinceStep += speed * Time.deltaTime;
            if (distanceSinceStep < strideLength || stepClips.Length == 0) return;
            distanceSinceStep = 0f;

            int index = Random.Range(0, stepClips.Length);
            if (stepClips.Length > 1 && index == lastIndex) index = (index + 1) % stepClips.Length;
            lastIndex = index;
            source.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
            source.PlayOneShot(stepClips[index], volume);
        }
    }
}
