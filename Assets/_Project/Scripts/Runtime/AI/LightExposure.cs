using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.AI
{
    /// <summary>
    /// Answers "is this spot inside a lamp's pool of light?" so monsters see players standing under
    /// lamps from farther away. Cutting the power at the breaker turns those pools off.
    /// Cheap approximation: inside the inner part of an active point or spot light's range, with a clear
    /// line to the bulb. It does not measure actual brightness.
    /// </summary>
    public static class LightExposure
    {
        /// <summary>Fraction of a lamp's range that counts as its bright pool.</summary>
        const float PoolFraction = 0.6f;
        /// <summary>Lamps dimmer than this are ignored (glows, indicator lights).</summary>
        const float MinIntensity = 1f;

        static Light[] lights;
        static int cachedFrame = -1;

        /// <summary>
        /// True if <paramref name="point"/> is in a lamp's pool of light. Lights carried by <paramref name="self"/>
        /// (its own flashlight) don't count, and its own body doesn't block the light.
        /// </summary>
        public static bool IsLitByLamp(Vector3 point, Transform self)
        {
            Refresh();
            foreach (var light in lights)
            {
                if (light == null)
                {
                    cachedFrame = -10000; // a scene was unloaded; rescan next time
                    continue;
                }
                if (!light.isActiveAndEnabled || light.intensity < MinIntensity)
                    continue;
                if (light.type != LightType.Point && light.type != LightType.Spot)
                    continue;
                if (self != null && light.transform.IsChildOf(self))
                    continue;

                Vector3 toPoint = point - light.transform.position;
                float poolRadius = light.range * PoolFraction;
                if (toPoint.sqrMagnitude > poolRadius * poolRadius)
                    continue;
                if (light.type == LightType.Spot &&
                    Vector3.Angle(light.transform.forward, toPoint) > light.spotAngle * 0.5f)
                    continue;
                if (Physics.Linecast(light.transform.position, point, out var hit, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) &&
                    (self == null || !hit.collider.transform.IsChildOf(self)) &&
                    (point - hit.point).sqrMagnitude > 0.5f * 0.5f)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>Re-scans the scene for lights every few seconds, so lights added later are picked up.</summary>
        static void Refresh()
        {
            if (lights != null && Time.frameCount - cachedFrame < 300)
                return;
            lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            cachedFrame = Time.frameCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lights = null;
            cachedFrame = -1;
        }
    }
}
