using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum QTEState { Idle, Running, Succeeded, Failed, Cancelled }
public enum QTEHitQuality { Fail, Success, Perfect }

[Serializable]
public struct QTEResult
{
    public bool success;
    public float accuracy;
    public QTEHitQuality quality;
    public Key requiredKey;
    public float duration;
    public float hitTime;
    public float center;
    public float successHalf;
    public float perfectHalf;
}

public class ParryQTEController : MonoBehaviour
{
    [SerializeField] private ParryQTEConfig config;
    [SerializeField] private int randomSeed = -1;
    [SerializeField] private bool logDebug = false;

    private QTEState state = QTEState.Idle;
    private float startTime, duration, center, successHalf, perfectHalf;
    private Key requiredKey = Key.Space;
    private System.Random rng;

    // NEW: guard to ensure we emit only once
    private bool hasEmitted;

    public event Action OnQTEStarted;
    public event Action<float> OnQTEProgress;
    public event Action<QTEResult> OnQTEFinished;

    public QTEState State => state;
    public float Progress => state == QTEState.Running ? Mathf.Clamp01((Time.time - startTime) / Mathf.Max(0.0001f, duration)) : 0f;
    public float SuccessCenter => center;
    public float SuccessHalf   => successHalf;
    public float PerfectHalf   => perfectHalf;
    public Key RequiredKey     => requiredKey;

    private void Awake()
    {
        if (config == null)
        {
            // Debug.LogWarning("[ParryQTEController] No config assigned; creating default.");
            config = ScriptableObject.CreateInstance<ParryQTEConfig>();
        }
        if (rng == null)
        {
            rng = (randomSeed >= 0)
                ? new System.Random(randomSeed)
                : new System.Random(unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());
        }
    }

    private void Update()
    {
        if (state != QTEState.Running) return;

        float p = Progress;
        OnQTEProgress?.Invoke(p);

        // 1) GIVE KEY PRESS PRIORITY OVER TIMEOUT
        if (Keyboard.current != null && TryGetPressedAllowedKey(out var pressed))
        {
            // Optional choice:
            // A) Wrong keys = ignore (recommended to avoid accidental fails):
            if (pressed != requiredKey)
            {
                // ignore wrong key and keep running
                // return; // uncomment if you want to consume the event; otherwise let it continue
            }
            else
            {
                var q = GetQuality(p);
                if (q == QTEHitQuality.Fail)
                {
                    Finish(false, QTEHitQuality.Fail, 0f, Time.time - startTime);
                }
                else
                {
                    Finish(true, q, ComputeAccuracy(p), Time.time - startTime);
                }
                return;
            }

            // B) If you prefer Undertale-style instant fail on wrong key, use this instead of the block above:
            // if (pressed != requiredKey) { Finish(false, QTEHitQuality.Fail, 0f, Time.time - startTime); return; }
        }

        // 2) Only if no key press this frame, check timeout
        if (p >= 1f)
        {
            Finish(false, QTEHitQuality.Fail, 0f, -1f);
            return;
        }
    }

    public void StartQTE()
    {
        if (state == QTEState.Running) CancelQTE();

        duration    = config.PickDuration(rng);
        successHalf = config.PickSuccessHalf(rng);
        perfectHalf = config.PickPerfectHalf(successHalf, rng);

        // Enforce "not in first 30%" in LOGIC too (not just visuals)
        float minStart = Mathf.Clamp01(config.MinWindowStartFraction); // add this float to your config (default 0.30)
        float minCenter = minStart + successHalf;

        float pickedCenter = config.PickCenter(successHalf, rng);
        center = Mathf.Max(pickedCenter, minCenter);
        center = Mathf.Clamp01(center);

        requiredKey = config.RandomizeKey
            ? config.PickKey(rng)
            : (config.AllowedKeys.Length > 0 ? config.AllowedKeys[0] : Key.Space);

        startTime = Time.time;
        state = QTEState.Running;
        hasEmitted = false; // reset guard

        if (logDebug)
        {
            float s0 = Mathf.Clamp01(center - successHalf);
            float s1 = Mathf.Clamp01(center + successHalf);
            // Debug.Log($"[QTE] START key:{requiredKey} dur:{duration:0.00}s center:{center:0.000} success:[{s0:0.000}..{s1:0.000}]");
        }

        OnQTEStarted?.Invoke();
        OnQTEProgress?.Invoke(0f);
    }

    public void CancelQTE()
    {
        if (state != QTEState.Running) return;
        state = QTEState.Cancelled;
        Emit(false, QTEHitQuality.Fail, 0f, -1f, QTEState.Cancelled);
    }

    private void Finish(bool ok, QTEHitQuality qual, float acc, float hitTime)
    {
        if (hasEmitted) return; // 3) DEBOUNCE
        hasEmitted = true;

        state = ok ? QTEState.Succeeded : QTEState.Failed;
        Emit(ok, qual, acc, hitTime, state);
    }

    private void Emit(bool ok, QTEHitQuality qual, float acc, float hitTime, QTEState final)
    {
        if (hasEmitted && final == QTEState.Running) return; // safety
        var r = new QTEResult
        {
            success = ok,
            accuracy = Mathf.Clamp01(acc),
            quality = qual,
            requiredKey = requiredKey,
            duration = duration,
            hitTime = hitTime,
            center = center,
            successHalf = successHalf,
            perfectHalf = perfectHalf
        };
        OnQTEFinished?.Invoke(r);
    }

    private QTEHitQuality GetQuality(float p01)
    {
        float s0 = Mathf.Clamp01(center - successHalf);
        float s1 = Mathf.Clamp01(center + successHalf);
        if (p01 < s0 || p01 > s1) return QTEHitQuality.Fail;

        float p0 = Mathf.Clamp01(center - perfectHalf);
        float p1 = Mathf.Clamp01(center + perfectHalf);
        return (p01 >= p0 && p01 <= p1) ? QTEHitQuality.Perfect : QTEHitQuality.Success;
    }

    private float ComputeAccuracy(float p01)
    {
        float d = Mathf.Abs(p01 - center);
        return Mathf.InverseLerp(successHalf, 0f, d);
    }

    private bool TryGetPressedAllowedKey(out Key key)
    {
        key = Key.None;
        var pool = config.AllowedKeys;
        if (Keyboard.current == null || pool == null || pool.Length == 0) return false;

        // Scan for any press this frame
        for (int i = 0; i < pool.Length; i++)
        {
            var k = pool[i];
            if (k == Key.None) continue;
            if (Keyboard.current[k].wasPressedThisFrame) { key = k; return true; }
        }
        return false;
    }
}
