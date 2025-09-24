using System;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public enum SlashPlaceMode { AtSource, Midpoint, AtTarget, FractionAlongLine }

    [Header("Slash VFX (Sprite/Animator Prefab)")]
    [Tooltip("Your slash sprite prefab (can have an Animator/flipbook).")]
    [SerializeField] private GameObject slashPrefab;
    [SerializeField] private SlashPlaceMode placeMode = SlashPlaceMode.Midpoint;
    [Tooltip("Used only when placeMode = FractionAlongLine (0=start, 1=end).")]
    [SerializeField, Range(0f, 1f)] private float placeT = 0.5f;

    [Header("Slash Orientation & Scale")]
    [Tooltip("Rotate Z so the slash points along the attack direction.")]
    [SerializeField] private bool alignToDirection = true;
    [Tooltip("Uniformly scale the slash vs. distance between source and target.")]
    [SerializeField] private bool scaleByDistance = false;
    [SerializeField] private float scalePerUnit = 0.0f; // e.g., 0.15 means 10u distance => +1.5 scale
    [SerializeField] private float minScale = 1f;
    [SerializeField] private float maxScale = 2.5f;

    [Header("Hit Flash")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float hitFlashTime = 0.12f;

    [Header("Camera Shake")]
    [SerializeField] private CameraShake2D cameraShake;
    [SerializeField] private float shakeAmp = 0.15f;
    [SerializeField] private float shakeTime = 0.2f;

    [Header("Auto Despawn (optional)")]
    [Tooltip("If your prefab doesn't self-destroy, set a TTL here.")]
    [SerializeField] private float slashTTL = 0.7f;

    // ---------------- Public one-shot slash ----------------

    /// <summary>
    /// Spawns your slash prefab oriented from 'from' -> 'to', flashes the target renderer, shakes camera.
    /// </summary>
    public void SlashSprite(Vector3 from, Vector3 to, Renderer hitRenderer = null)
    {
        float z = 0f;
        if (hitRenderer != null)
            z = hitRenderer.transform.position.z;
        else if (cameraShake != null)
            z = cameraShake.transform.position.z + 1f; // in front of camera

        if (slashPrefab != null)
        {
            Vector3 pos = ComputeSlashPosition(from, to); pos.z = z;
            Quaternion rot = ComputeRotation(from, to);
            float scl = ComputeScale(from, to);

            var inst = Instantiate(slashPrefab, pos, rot);

            // Ensure renders above the target by bumping sorting order
            var sr = inst.GetComponentInChildren<SpriteRenderer>();
            var hitSR = hitRenderer ? hitRenderer.GetComponent<SpriteRenderer>() : null;
            if (sr != null && hitSR != null)
            {
                sr.sortingLayerID = hitSR.sortingLayerID;
                sr.sortingOrder = hitSR.sortingOrder + 5;
            }

            if (scaleByDistance) inst.transform.localScale *= scl;
            if (slashTTL > 0f) inst.AddComponent<VFXTimedDespawn>().Init(slashTTL);
        }

        if (hitRenderer != null) FlashHit(hitRenderer);
        Shake();
    }

    /// <summary>Flash a renderer to a color briefly.</summary>
    public void FlashHit(Renderer r)
    {
        if (r == null) return;

        // Prefer SpriteRenderer color flash (no materials/shaders involved)
        var sr = r as SpriteRenderer;
        if (sr == null) sr = r.GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            var fl = sr.GetComponent<SpriteColorFlash>();
            if (fl == null) fl = sr.gameObject.AddComponent<SpriteColorFlash>();
            fl.Play(hitFlashColor, hitFlashTime);
            return;
        }

        // Fallback: no sprite renderer, just shake
        Shake();
    }


    /// <summary>Camera shake pulse.</summary>
    public void Shake()
    {
        if (cameraShake != null) cameraShake.Shake(shakeAmp, shakeTime);
    }

    // ---------------- QTE-synced enemy slash ----------------

    // State
    private GameObject _activeEnemySlash;
    private Animator _activeEnemySlashAnim;
    private int _activeEnemySlashStateHash;
    private bool _hashReady;
    private ParryQTEController _boundQTE;
    private Action _onPerfectCallback;

    /// <summary>
    /// Start an enemy slash VFX that's driven by QTE progress (0..1).
    /// - Animator (if present) is scrubbed to match QTE progress each frame.
    /// - On Success: slash stops mid-way (destroyed).
    /// - On Perfect: slash stops and calls onPerfect (e.g., spawn player's riposte slash).
    /// </summary>
    public void StartEnemySlashSynced(
        ParryQTEController qte,
        Vector3 from, Vector3 to,
        Renderer hitRenderer = null,
        Action onPerfect = null)
    {
        StopEnemySlashSynced(); // cleanup any previous

        if (slashPrefab == null || qte == null) return;

        // Place at the target's Z so it's visible in 2D
        float z = hitRenderer != null
            ? hitRenderer.transform.position.z
            : (cameraShake != null ? cameraShake.transform.position.z + 1f : 0f);

        Vector3 pos = ComputeSlashPosition(from, to); pos.z = z;
        Quaternion rot = ComputeRotation(from, to);
        float scl = ComputeScale(from, to);

        _activeEnemySlash = Instantiate(slashPrefab, pos, rot);
        if (scaleByDistance) _activeEnemySlash.transform.localScale *= scl;

        // Sort above the hit target
        var sr = _activeEnemySlash.GetComponentInChildren<SpriteRenderer>();
        var hitSR = hitRenderer ? hitRenderer.GetComponent<SpriteRenderer>() : null;
        if (sr != null && hitSR != null)
        {
            sr.sortingLayerID = hitSR.sortingLayerID;
            sr.sortingOrder = hitSR.sortingOrder + 5;
        }

        _activeEnemySlashAnim = _activeEnemySlash.GetComponentInChildren<Animator>();
        _hashReady = false;
        _activeEnemySlashStateHash = 0;
        if (_activeEnemySlashAnim != null)
        {
            // We will drive normalized time manually
            _activeEnemySlashAnim.speed = 0f;
        }

        _boundQTE = qte;
        _onPerfectCallback = onPerfect;

        // Subscribe to QTE events
        _boundQTE.OnQTEProgress += HandleQTEProgress_SlashSync;
        _boundQTE.OnQTEFinished += HandleQTEFinished_SlashSync;
    }

    /// <summary>Stop and clean up any QTE-synced slash currently active.</summary>
    public void StopEnemySlashSynced()
    {
        if (_boundQTE != null)
        {
            _boundQTE.OnQTEProgress -= HandleQTEProgress_SlashSync;
            _boundQTE.OnQTEFinished -= HandleQTEFinished_SlashSync;
            _boundQTE = null;
        }
        _onPerfectCallback = null;
        _hashReady = false;
        _activeEnemySlashAnim = null;

        if (_activeEnemySlash != null)
        {
            Destroy(_activeEnemySlash);
            _activeEnemySlash = null;
        }
    }

    private void HandleQTEProgress_SlashSync(float p01)
    {
        if (_activeEnemySlashAnim == null) return;

        // Cache the default state's hash once
        if (!_hashReady)
        {
            var st = _activeEnemySlashAnim.GetCurrentAnimatorStateInfo(0);
            _activeEnemySlashStateHash = st.fullPathHash;
            _hashReady = true;
        }

        float t = Mathf.Clamp01(p01);
        // Scrub animation to normalized time t
        _activeEnemySlashAnim.Play(_activeEnemySlashStateHash, 0, t);
        _activeEnemySlashAnim.Update(0f); // apply immediately this frame
    }

    private void HandleQTEFinished_SlashSync(QTEResult r)
    {
        // Unhook events
        if (_boundQTE != null)
        {
            _boundQTE.OnQTEProgress -= HandleQTEProgress_SlashSync;
            _boundQTE.OnQTEFinished -= HandleQTEFinished_SlashSync;
            _boundQTE = null;
        }

        switch (r.quality)
        {
            case QTEHitQuality.Fail:
                // QTE reached 1.0; optionally let the slash linger briefly
                if (_activeEnemySlash != null && slashTTL > 0f)
                {
                    var despawn = _activeEnemySlash.GetComponent<VFXTimedDespawn>();
                    if (despawn == null) _activeEnemySlash.AddComponent<VFXTimedDespawn>().Init(Mathf.Max(0.05f, slashTTL * 0.5f));
                    else despawn.Init(Mathf.Max(0.05f, slashTTL * 0.5f));
                }
                break;

            case QTEHitQuality.Success:
                // Cut off mid-swing (destroy)
                if (_activeEnemySlash != null) Destroy(_activeEnemySlash);
                break;

            case QTEHitQuality.Perfect:
                // Cut off + trigger player's riposte VFX callback
                if (_activeEnemySlash != null) Destroy(_activeEnemySlash);
                _onPerfectCallback?.Invoke();
                break;
        }

        _activeEnemySlash = null;
        _activeEnemySlashAnim = null;
        _onPerfectCallback = null;
        _hashReady = false;
    }
    public void PlayCustomVFX(GameObject prefab, Vector3 from, Vector3 to, Renderer hitRenderer = null)
    {
        if (prefab == null) { SlashSprite(from, to, hitRenderer); return; }

        float z = 0f;
        if (hitRenderer != null) z = hitRenderer.transform.position.z;
        else if (cameraShake != null) z = cameraShake.transform.position.z + 1f;

        // position/orient roughly like SlashSprite
        Vector3 pos = (from + to) * 0.5f; pos.z = z;

        Quaternion rot = Quaternion.identity;
        var dir = (to - from);
        if (dir.sqrMagnitude > 1e-6f)
        {
            float zDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            rot = Quaternion.Euler(0f, 0f, zDeg);
        }

        var inst = Instantiate(prefab, pos, rot);

        // make sure it renders above target if both are SpriteRenderers
        var sr = inst.GetComponentInChildren<SpriteRenderer>();
        var hitSR = hitRenderer ? hitRenderer.GetComponent<SpriteRenderer>() : null;
        if (sr != null && hitSR != null)
        {
            sr.sortingLayerID = hitSR.sortingLayerID;
            sr.sortingOrder = hitSR.sortingOrder + 5;
        }

        if (slashTTL > 0f) inst.AddComponent<VFXTimedDespawn>().Init(slashTTL);
        if (hitRenderer != null) FlashHit(hitRenderer);
        Shake();
    }
    // 1) Force spawn at the target's position/bounds center
    public void PlayCustomVFXAtTarget(GameObject prefab, Renderer targetRenderer)
    {
        if (prefab == null && slashPrefab == null) return;

        // Position: target center (fallback to transform)
        Vector3 pos = targetRenderer ? targetRenderer.bounds.center : Vector3.zero;
        float z = targetRenderer ? targetRenderer.transform.position.z
                                 : (cameraShake ? cameraShake.transform.position.z + 1f : 0f);
        pos.z = z;

        // Rotation: none (or face camera); adjust if your prefab expects direction
        Quaternion rot = Quaternion.identity;

        var inst = Instantiate(prefab != null ? prefab : slashPrefab, pos, rot);

        // Sort above target
        var sr = inst.GetComponentInChildren<SpriteRenderer>();
        var hitSR = targetRenderer ? targetRenderer.GetComponent<SpriteRenderer>() : null;
        if (sr != null && hitSR != null)
        {
            sr.sortingLayerID = hitSR.sortingLayerID;
            sr.sortingOrder = hitSR.sortingOrder + 5;
        }

        if (slashTTL > 0f) inst.AddComponent<VFXTimedDespawn>().Init(slashTTL);

        if (targetRenderer != null) FlashHit(targetRenderer);
        Shake();
    }

    // 2) Same idea but using your existing slash logic with a per-call placement override
    public void SlashSpriteWithPlacement(Vector3 from, Vector3 to, Renderer hitRenderer, SlashPlaceMode modeOverride)
    {
        // Temporarily compute position using the override
        float z = hitRenderer ? hitRenderer.transform.position.z
                              : (cameraShake ? cameraShake.transform.position.z + 1f : 0f);

        Vector3 pos;
        switch (modeOverride)
        {
            case SlashPlaceMode.AtSource: pos = from; break;
            case SlashPlaceMode.AtTarget: pos = to; break;
            case SlashPlaceMode.FractionAlongLine: pos = Vector3.Lerp(from, to, Mathf.Clamp01(placeT)); break;
            default: pos = (from + to) * 0.5f; break;
        }
        pos.z = z;

        Quaternion rot = ComputeRotation(from, to);
        float scl = ComputeScale(from, to);

        if (slashPrefab != null)
        {
            var inst = Instantiate(slashPrefab, pos, rot);

            var sr = inst.GetComponentInChildren<SpriteRenderer>();
            var hitSR = hitRenderer ? hitRenderer.GetComponent<SpriteRenderer>() : null;
            if (sr != null && hitSR != null)
            {
                sr.sortingLayerID = hitSR.sortingLayerID;
                sr.sortingOrder = hitSR.sortingOrder + 5;
            }

            if (scaleByDistance) inst.transform.localScale *= scl;
            if (slashTTL > 0f) inst.AddComponent<VFXTimedDespawn>().Init(slashTTL);
        }

        if (hitRenderer != null) FlashHit(hitRenderer);
        Shake();
    }


    // ---------------- Internals ----------------

    private Vector3 ComputeSlashPosition(Vector3 from, Vector3 to)
    {
        switch (placeMode)
        {
            case SlashPlaceMode.AtSource: return from;
            case SlashPlaceMode.AtTarget: return to;
            case SlashPlaceMode.FractionAlongLine: return Vector3.Lerp(from, to, Mathf.Clamp01(placeT));
            default:
            case SlashPlaceMode.Midpoint: return (from + to) * 0.5f;
        }
    }

    private Quaternion ComputeRotation(Vector3 from, Vector3 to)
    {
        if (!alignToDirection)
            return Quaternion.identity;

        Vector2 dir = (to - from);
        if (dir.sqrMagnitude < 0.000001f) return Quaternion.identity;

        float zDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        // If your sprite's "up" is the slash direction, subtract 90; change as needed for your art.
        return Quaternion.Euler(0f, 0f, zDeg - 90f);
    }

    private float ComputeScale(Vector3 from, Vector3 to)
    {
        if (!scaleByDistance) return 1f;
        float d = Vector3.Distance(from, to);
        float s = 1f + d * Mathf.Max(0f, scalePerUnit);
        return Mathf.Clamp(s, minScale, maxScale);
    }
}
