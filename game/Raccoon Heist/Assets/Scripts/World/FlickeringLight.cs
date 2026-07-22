using UnityEngine;

namespace RaccoonHeist.World
{
    // Perlin wobble plus occasional full dropouts — dying-fluorescent feel.
    // Purely visual; safe as a plain MonoBehaviour even after netcode arrives.
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        public float flickerSpeed = 9f;
        public float flickerAmount = 0.2f;   // fraction of base intensity
        public float dropoutChance = 0.05f;  // average dropouts per second
        public float dropoutDuration = 0.1f;

        Light li;
        float baseIntensity;
        float seed;
        float dropoutUntil;

        void Awake()
        {
            li = GetComponent<Light>();
            baseIntensity = li.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (Time.time < dropoutUntil)
            {
                li.intensity = baseIntensity * 0.05f;
                return;
            }
            if (Random.value < dropoutChance * Time.deltaTime)
            {
                dropoutUntil = Time.time + dropoutDuration * Random.Range(0.5f, 2f);
                return;
            }
            float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
            li.intensity = baseIntensity * (1f - flickerAmount * n);
        }
    }
}
