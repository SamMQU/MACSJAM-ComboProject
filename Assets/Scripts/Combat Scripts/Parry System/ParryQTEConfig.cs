using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "ParryQTEConfig", menuName = "QTE/ParryQTEConfig")]
public class ParryQTEConfig : ScriptableObject
{
    [Header("Keys")]
    [Tooltip("Keys the QTE will accept. If RandomizeKey is true, one is picked at random each time.")]
    public Key[] AllowedKeys = new Key[] { Key.Space, Key.J, Key.K, Key.L };
    public bool RandomizeKey = true;

    [Header("Timing (seconds)")]
    [Tooltip("Duration of the whole QTE sweep.")]
    public Vector2 DurationRange = new Vector2(0.70f, 1.15f);

    [Header("Window Sizes (fractions of track length)")]
    [Tooltip("Half-width of the SUCCESS band. Actual success width = 2 * SuccessHalf.")]
    public Vector2 SuccessHalfRange = new Vector2(0.08f, 0.16f);

    [Tooltip("PerfectHalf = SuccessHalf * random in [PerfectRatioMin..PerfectRatioMax].")]
    [Range(0.05f, 0.95f)] public float PerfectRatioMin = 0.30f;
    [Range(0.05f, 0.95f)] public float PerfectRatioMax = 0.45f;

    [Header("Window Placement")]
    [Tooltip("Ensure the success window does NOT start in the first X% of the track. (e.g., 0.30 = first 30%)")]
    [Range(0f, 1f)] public float MinWindowStartFraction = 0.30f;

    // --------- API used by ParryQTEController ---------

    public float PickDuration(System.Random rng)
    {
        return RandRange(rng, DurationRange.x, DurationRange.y);
    }

    public float PickSuccessHalf(System.Random rng)
    {
        return Mathf.Clamp01(RandRange(rng, SuccessHalfRange.x, SuccessHalfRange.y));
    }

    public float PickPerfectHalf(float successHalf, System.Random rng)
    {
        float rMin = Mathf.Min(PerfectRatioMin, PerfectRatioMax);
        float rMax = Mathf.Max(PerfectRatioMin, PerfectRatioMax);
        float ratio = RandRange(rng, rMin, rMax);
        return Mathf.Clamp01(successHalf * ratio);
    }

    /// <summary>
    /// Pick a center so that:
    /// - start of success window (center - sH) >= MinWindowStartFraction
    /// - end of success window (center + sH) <= 1
    /// </summary>
    public float PickCenter(float successHalf, System.Random rng)
    {
        float minCenter = MinWindowStartFraction + successHalf;
        float maxCenter = 1f - successHalf;

        if (minCenter > maxCenter)
        {
            // If impossible (window too big for constraints), clamp to the only feasible point.
            float mid = Mathf.Clamp01((MinWindowStartFraction + 1f) * 0.5f);
            return Mathf.Clamp01(mid);
        }

        return RandRange(rng, minCenter, maxCenter);
    }

    public Key PickKey(System.Random rng)
    {
        if (AllowedKeys == null || AllowedKeys.Length == 0) return Key.Space;
        int i = rng.Next(0, AllowedKeys.Length);
        return AllowedKeys[i];
    }

    // --------- helpers ---------
    private static float RandRange(System.Random rng, float a, float b)
    {
        if (a > b) { var t = a; a = b; b = t; }
        return a + (float)rng.NextDouble() * (b - a);
    }
}
