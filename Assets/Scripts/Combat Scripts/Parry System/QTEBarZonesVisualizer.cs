using UnityEngine;

/// Undertale-style bar with random SUCCESS/PERFECT bands from ParryQTEController.
/// - Recomputes positions on every QTE start
/// - Left→Right progress mapping (0..1)
/// - Optional SpriteRenderer-size mode to avoid pivot/scale issues
public class QTEBarZonesVisualizer : MonoBehaviour
{
    [Header("QTE Source")]
    [SerializeField] private ParryQTEController controller;

    [Header("Bar Geometry")]
    [Tooltip("World-space length of the bar (X).")]
    [SerializeField] private float barLength = 4f;

    [Tooltip("World-space height of the bar (Y).")]
    [SerializeField] private float barHeight = 0.3f;

    [Tooltip("Local offset of the bar center.")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Header("Visuals (assign children)")]
    [SerializeField] private Transform track;        // optional
    [SerializeField] private Transform zoneSuccess;  // success band
    [SerializeField] private Transform zonePerfect;  // perfect band (inside success)
    [SerializeField] private Transform marker;       // moving vertical line

    [Header("Renderer color tints (optional)")]
    [SerializeField] private Color trackColor   = new Color(1,1,1,0.1f);
    [SerializeField] private Color successColor = new Color(1.0f,0.9f,0.2f,0.85f);
    [SerializeField] private Color perfectColor = new Color(0.2f,1.0f,0.4f,0.95f);
    [SerializeField] private Color markerColor  = Color.white;

    [Header("Advanced")]
    [Tooltip("If true and the child has a SpriteRenderer, set SpriteRenderer.size instead of scaling the Transform (requires Sliced/Tiled sprite).")]
    [SerializeField] private bool useSpriteSize = false;

    [Tooltip("If true, redraw zones every frame (handy while testing).")]
    [SerializeField] private bool alwaysRedraw = false;

    private void OnEnable()
    {
        Wire(true);
        LayoutTrack();
        RedrawZones();
        SyncMarker();
    }

    private void OnDisable() => Wire(false);

    private void Update()
    {
        if (!Application.isPlaying)
        {
            LayoutTrack();
            RedrawZones();
            SyncMarker();
            return;
        }

        if (controller != null && controller.State == QTEState.Running)
            SyncMarker();

        if (alwaysRedraw) RedrawZones();
    }

    private void Wire(bool sub)
    {
        if (controller == null) controller = FindObjectOfType<ParryQTEController>();
        if (controller == null) return;

        if (sub)
        {
            controller.OnQTEStarted  += HandleStarted;
            controller.OnQTEProgress += HandleProgress;
            controller.OnQTEFinished += HandleFinished;
        }
        else
        {
            controller.OnQTEStarted  -= HandleStarted;
            controller.OnQTEProgress -= HandleProgress;
            controller.OnQTEFinished -= HandleFinished;
        }
    }

    private void HandleStarted()
    {
        LayoutTrack();
        RedrawZones();          // <- critical: pulls new randomized center/width
        SetColor(track,   trackColor);
        SetColor(zoneSuccess, successColor);
        SetColor(zonePerfect, perfectColor);
        SetColor(marker,  markerColor);
        SetMarker(0f);
    }

    private void HandleProgress(float p) => SetMarker(p);

    private void HandleFinished(QTEResult r)
    {
        // Optional finish tint
        if (marker == null) return;
        SetColor(marker, r.success
            ? (r.quality == QTEHitQuality.Perfect ? Color.green : new Color(1f,0.85f,0.2f))
            : Color.red);
    }

    // ---------- Layout & Drawing ----------

    private void LayoutTrack()
    {
        if (track == null) return;
        // Place track centered at localOffset; make it barLength × barHeight
        track.localPosition = localOffset;
        if (!useSpriteSize || !TrySizeSprite(track, barLength, barHeight))
            track.localScale = new Vector3(barLength, barHeight, 1f);
        SetColor(track, trackColor);
    }

    private void RedrawZones()
    {
        if (controller == null) return;

        float L = Mathf.Max(0.0001f, barLength);
        float H = barHeight;

        // Controller exposes randomized values each run
        float c    = Mathf.Clamp01(controller.SuccessCenter);
        float sH   = Mathf.Clamp01(controller.SuccessHalf);
        float pH   = Mathf.Clamp01(controller.PerfectHalf);

        // Success band [min, max] along 0..1
        float sMin = Mathf.Clamp01(c - sH);
        float sMax = Mathf.Clamp01(c + sH);
        float sMid = (sMin + sMax) * 0.5f;
        float sWid = Mathf.Max(0f, sMax - sMin) * L;

        // Perfect band [min, max] along 0..1
        float pMin = Mathf.Clamp01(c - pH);
        float pMax = Mathf.Clamp01(c + pH);
        float pMid = (pMin + pMax) * 0.5f;
        float pWid = Mathf.Max(0f, pMax - pMin) * L;

        // Map progress p ∈ [0,1] to bar local X:
        // left edge = -L/2, right edge = +L/2
        float X(float p) => (p - 0.5f) * L;

        // Position bands by their midpoints; size to width
        if (zoneSuccess != null)
        {
            zoneSuccess.localPosition = localOffset + new Vector3(X(sMid), 0f, 0f);
            if (!useSpriteSize || !TrySizeSprite(zoneSuccess, sWid, H * 0.9f))
                zoneSuccess.localScale = new Vector3(Mathf.Max(0.0001f, sWid), H * 0.9f, 1f);
            SetColor(zoneSuccess, successColor);
        }

        if (zonePerfect != null)
        {
            zonePerfect.localPosition = localOffset + new Vector3(X(pMid), 0f, 0f);
            if (!useSpriteSize || !TrySizeSprite(zonePerfect, pWid, H * 0.8f))
                zonePerfect.localScale = new Vector3(Mathf.Max(0.0001f, pWid), H * 0.8f, 1f);
            SetColor(zonePerfect, perfectColor);
        }
    }

    private void SyncMarker()
    {
        if (controller == null) return;
        SetMarker(controller.Progress);
    }

    private void SetMarker(float progress01)
    {
        if (marker == null) return;
        float L = Mathf.Max(0.0001f, barLength);
        float x = (Mathf.Clamp01(progress01) - 0.5f) * L;
        marker.localPosition = localOffset + new Vector3(x, 0f, 0f);
        // Thin vertical bar
        if (!useSpriteSize || !TrySizeSprite(marker, Mathf.Max(0.02f, L * 0.01f), barHeight * 1.2f))
            marker.localScale = new Vector3(Mathf.Max(0.02f, L * 0.01f), barHeight * 1.2f, 1f);
    }

    // ---------- Helpers ----------

    private static void SetColor(Transform t, Color c)
    {
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        if (r == null) return;
        if (Application.isPlaying) { if (r.material) r.material.color = c; }
        else { if (r.sharedMaterial) r.sharedMaterial.color = c; }
    }

    // Use SpriteRenderer.size if possible to avoid pivot issues
    private static bool TrySizeSprite(Transform t, float width, float height)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) return false;
        // Only works in Sliced/Tiled modes (Simple ignores size)
        if (sr.drawMode == SpriteDrawMode.Simple) return false;
        // size.x/size.y are in local units
        sr.size = new Vector2(Mathf.Max(0.0001f, width), Mathf.Max(0.0001f, height));
        // Ensure transform scale is 1 so size is authoritative
        t.localScale = new Vector3(1f, 1f, 1f);
        return true;
    }
}
