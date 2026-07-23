using UnityEngine;
using UnityEngine.AI;

namespace RaccoonHeist.Harold
{
    public enum HaroldState { Sleeping, Stirring, Suspicious, Patrol, Chase, GrabYeet, AlarmAwake }

    // Plain C# state machine driving a NavMeshAgent (Animator comes later).
    // This slice implements Sleeping (snore at the cot) and Patrol (wander the
    // shop floor, grumbling). The remaining states are declared so tuning and
    // networking can build against the full shape from day one.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HaroldVoice))]
    public sealed class HaroldBrain : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] HaroldState startState = HaroldState.Patrol;

        [Header("Patrol wander area (world space, on the floor)")]
        [SerializeField] Vector3 wanderCenter = new(7f, 0f, 8f);
        [SerializeField] Vector3 wanderSize = new(12f, 0f, 14f);

        [Header("Patrol feel")]
        [SerializeField, Min(0.1f)] float walkSpeed = 0.8f;      // slipper shuffle
        [SerializeField, Min(0.5f)] float minHopDistance = 2f;   // skip trivial destinations
        [SerializeField] Vector2 dwellSeconds = new(2.5f, 6f);
        [SerializeField, Range(0f, 240f)] float lookAroundArc = 110f;
        [SerializeField, Min(0.1f)] float lookAroundPeriod = 3.2f;
        [SerializeField] Vector2 grumbleEverySeconds = new(9f, 22f);

        static readonly int SpeedId = Animator.StringToHash("Speed");

        NavMeshAgent agent;
        HaroldVoice voice;
        Animator animator;
        HaroldState state;
        float dwellUntil = -1f;
        float lookBaseYaw;
        float dwellStartedAt;
        float nextGrumbleAt;

        public HaroldState State => state;

        // Called by the greybox generator at edit time; safe to call at runtime too.
        public void ConfigureWander(Vector3 center, Vector3 size)
        {
            wanderCenter = center;
            wanderSize = size;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            voice = GetComponent<HaroldVoice>();
            animator = GetComponentInChildren<Animator>();
        }

        void Start()
        {
            // The Navigation surface may enable after Harold does on the first frame;
            // snap onto the mesh instead of silently idling off-mesh.
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            agent.speed = walkSpeed;
            ScheduleNextGrumble();
            SetState(startState);
        }

        public void SetState(HaroldState next)
        {
            state = next;
            bool asleep = next == HaroldState.Sleeping;
            if (agent.isOnNavMesh) agent.isStopped = asleep;
            if (asleep) voice.StartSnore();
            else voice.StopSnore();
        }

        void Update()
        {
            // The animator only renders what the brain decided: locomotion blend
            // follows actual agent velocity, so there is nothing to keep in sync.
            if (animator != null)
                animator.SetFloat(SpeedId, agent.velocity.magnitude, 0.15f, Time.deltaTime);

            switch (state)
            {
                case HaroldState.Patrol:
                    UpdatePatrol();
                    break;
                case HaroldState.Sleeping:
                    break; // the snore loop is the whole behaviour
                default:
                    UpdatePatrol(); // unimplemented states fall back to wandering
                    break;
            }
        }

        void UpdatePatrol()
        {
            if (Time.time >= nextGrumbleAt)
            {
                voice.PlayGrumble();
                ScheduleNextGrumble();
            }

            if (!agent.isOnNavMesh || agent.pathPending) return;
            if (agent.remainingDistance > agent.stoppingDistance)
            {
                dwellUntil = -1f;
                return;
            }

            if (dwellUntil < 0f)
            {
                dwellStartedAt = Time.time;
                dwellUntil = Time.time + Random.Range(dwellSeconds.x, dwellSeconds.y);
                lookBaseYaw = transform.eulerAngles.y;
            }

            // Sweep the head/body while standing still — reads as "looking around"
            // even before there is any animation.
            float sweep = Mathf.Sin((Time.time - dwellStartedAt) * (2f * Mathf.PI / lookAroundPeriod));
            transform.rotation = Quaternion.Euler(0f, lookBaseYaw + sweep * lookAroundArc * 0.5f, 0f);

            if (Time.time >= dwellUntil)
            {
                dwellUntil = -1f;
                agent.SetDestination(PickWanderPoint());
            }
        }

        Vector3 PickWanderPoint()
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var candidate = wanderCenter + new Vector3(
                    Random.Range(-wanderSize.x, wanderSize.x) * 0.5f,
                    0f,
                    Random.Range(-wanderSize.z, wanderSize.z) * 0.5f);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas)) continue;
                if ((hit.position - transform.position).sqrMagnitude < minHopDistance * minHopDistance) continue;
                return hit.position;
            }
            return transform.position;
        }

        void ScheduleNextGrumble() =>
            nextGrumbleAt = Time.time + Random.Range(grumbleEverySeconds.x, grumbleEverySeconds.y);

        void OnValidate()
        {
            dwellSeconds.y = Mathf.Max(dwellSeconds.x, dwellSeconds.y);
            grumbleEverySeconds.y = Mathf.Max(grumbleEverySeconds.x, grumbleEverySeconds.y);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(wanderCenter, new Vector3(wanderSize.x, 0.1f, wanderSize.z));
        }
    }
}
