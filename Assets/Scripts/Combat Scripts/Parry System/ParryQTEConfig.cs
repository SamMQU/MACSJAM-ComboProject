using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "Combat/QTE/Parry QTE Config", fileName = "ParryQTEConfig")]
public class ParryQTEConfig : ScriptableObject
{
    [Header("Allowed Keys")]
    [SerializeField] private Key[] allowedKeys = new Key[] { Key.Z, Key.X, Key.C, Key.Space, Key.J, Key.K, Key.L };

    [Header("Duration (seconds)")]
    [SerializeField] private float minDuration = 1.0f;
    [SerializeField] private float maxDuration = 1.4f;

    [Header("Success Band WIDTH (half, fraction of bar)")]
    [SerializeField] private Vector2 successHalfWidthRange = new Vector2(0.08f, 0.14f);

    [Header("Perfect Band WIDTH (as fraction of success half)")]
    [SerializeField] private Vector2 perfectOfSuccessRange = new Vector2(0.40f, 0.55f);

    [Header("Center Randomization")]
    [SerializeField] private bool randomizeWindowCenter = true;
    [SerializeField] private float fixedWindowCenter = 0.5f;

    [Header("Randomize Key & Duration")]
    [SerializeField] private bool randomizeKey = true;
    [SerializeField] private bool randomizeDuration = true;

    public Key[] AllowedKeys => allowedKeys;
    public float MinDuration => Mathf.Max(0.05f, minDuration);
    public float MaxDuration => Mathf.Max(MinDuration, maxDuration);
    public Vector2 SuccessHalfWidthRange => new Vector2(Mathf.Clamp(successHalfWidthRange.x, 0f, 0.49f),
                                                        Mathf.Clamp(successHalfWidthRange.y, 0f, 0.49f));
    public Vector2 PerfectOfSuccessRange => new Vector2(Mathf.Clamp01(perfectOfSuccessRange.x),
                                                        Mathf.Clamp01(perfectOfSuccessRange.y));
    public bool RandomizeWindowCenter => randomizeWindowCenter;
    public float FixedWindowCenter => Mathf.Clamp01(fixedWindowCenter);
    public bool RandomizeKey => randomizeKey;
    public bool RandomizeDuration => randomizeDuration;

    public Key PickKey(System.Random rng)
    {
        if (allowedKeys == null || allowedKeys.Length == 0) return Key.Space;
        return allowedKeys[rng.Next(0, allowedKeys.Length)];
    }

    public float PickDuration(System.Random rng)
    {
        if (!randomizeDuration) return (MinDuration + MaxDuration) * 0.5f;
        return Mathf.Lerp(MinDuration, MaxDuration, (float)rng.NextDouble());
    }

    public float PickSuccessHalf(System.Random rng)
    {
        var r = SuccessHalfWidthRange;
        return Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());
    }

    public float PickPerfectHalf(float successHalf, System.Random rng)
    {
        var r = PerfectOfSuccessRange;
        float mul = Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());
        return Mathf.Clamp(mul * successHalf, 0f, successHalf);
    }

    // ✅ Random center across the bar, while keeping the full band inside [0..1]
    public float PickCenter(float successHalf, System.Random rng)
    {
        if (!randomizeWindowCenter) return FixedWindowCenter;
        float minC = successHalf;        // so (center - half) >= 0
        float maxC = 1f - successHalf;   // so (center + half) <= 1
        if (maxC <= minC) return 0.5f;   // degenerate safety
        return Mathf.Lerp(minC, maxC, (float)rng.NextDouble());
    }
}
