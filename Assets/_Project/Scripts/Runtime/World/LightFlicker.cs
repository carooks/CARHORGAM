using UnityEngine;

namespace HorrorGame.World
{
    /// <summary>
    /// Makes a light waver with noise and occasionally cut out, like a failing fluorescent tube.
    /// Optionally dims an emissive renderer in step with the light.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] float noiseSpeed = 6f;
        [Range(0f, 1f)] [SerializeField] float noiseAmount = 0.2f;
        [Tooltip("Average number of brief blackouts per second.")]
        [SerializeField] float dropoutsPerSecond = 0.3f;
        [SerializeField] Vector2 dropoutDuration = new Vector2(0.04f, 0.3f);
        [SerializeField] Renderer emissiveRenderer;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Light target;
        float baseIntensity;
        float seed;
        float dropoutTimer;
        MaterialPropertyBlock block;
        Color baseEmission;

        void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
            if (emissiveRenderer != null)
            {
                block = new MaterialPropertyBlock();
                baseEmission = emissiveRenderer.sharedMaterial.GetColor(EmissionColorId);
            }
        }

        void Update()
        {
            float factor;
            if (dropoutTimer > 0f)
            {
                dropoutTimer -= Time.deltaTime;
                factor = 0.05f;
            }
            else
            {
                if (Random.value < dropoutsPerSecond * Time.deltaTime)
                    dropoutTimer = Random.Range(dropoutDuration.x, dropoutDuration.y);
                float noise = Mathf.PerlinNoise(seed, Time.time * noiseSpeed);
                factor = 1f - noiseAmount + noiseAmount * 2f * noise;
            }

            target.intensity = baseIntensity * factor;
            if (block != null)
            {
                block.SetColor(EmissionColorId, baseEmission * factor);
                emissiveRenderer.SetPropertyBlock(block);
            }
        }
    }
}
