using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class CircularQTEVisualizer : MonoBehaviour
{
    [Header("QTE Source")]
    [SerializeField] private ParryQTEController controller;

    [Header("Geometry")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private int arcSegments = 96;
    [SerializeField] private float startAngleDeg = -90f;  // t=0 at top
    [SerializeField] private float sweepAngleDeg = 360f;  // full circle
    [SerializeField] private bool leftToRight = true;

    [Header("Visual Parts")]
    [SerializeField] private LineRenderer trackArc;
    [SerializeField] private LineRenderer successArc;
    [SerializeField] private LineRenderer perfectArc;
    [SerializeField] private Transform marker;

    [Header("Appearance")]
    [SerializeField] private float trackWidth = 0.06f;
    [SerializeField] private float successWidth = 0.08f;
    [SerializeField] private float perfectWidth = 0.05f;
    [SerializeField] private Color trackColor   = new Color(1,1,1,0.15f);
    [SerializeField] private Color successColor = new Color(1.0f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color perfectColor = new Color(0.2f, 1.0f, 0.4f, 1f);

    [Header("Finish Flash")]
    [SerializeField] private Color successFlash = Color.green;
    [SerializeField] private Color failFlash    = Color.red;
    [SerializeField] private float flashDuration = 0.18f;

    [Header("Center Image")]
    [SerializeField] private SpriteRenderer centerRenderer;
    [SerializeField] private float centerSize = 0.75f;
    [SerializeField] private bool hideCenterOnFinish = true;

    [System.Serializable]
    public struct KeySprite
    {
        public Key key;
        public Sprite sprite;
    }

    [Header("Hotkey → Center Sprite")]
    [Tooltip("When a QTE starts, the center sprite is selected based on the required key.")]
    [SerializeField] private List<KeySprite> keySprites = new List<KeySprite>();
    [SerializeField] private Sprite fallbackCenterSprite;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenIdle = true;

    [Header("Window Placement Rules")]
    [Tooltip("Visual minimum start position for the success band (0..1). Recommended the controller also enforces this.")]
    [Range(0f,1f)] [SerializeField] private float minWindowStartFraction = 0.30f;

    private Coroutine flashRoutine;
    private bool visibleNow;

    // ---------- Unity ----------

    private void OnEnable()
    {
        Wire(true);
        ConfigureLR(trackArc,   trackWidth,   trackColor);
        ConfigureLR(successArc, successWidth, successColor);
        ConfigureLR(perfectArc, perfectWidth, perfectColor);
        UpdateCenterImageSize();
        RedrawAll();
        SyncMarkerImmediate();
        if (hideWhenIdle && Application.isPlaying) SetVisible(false);
    }

    private void OnDisable()
    {
        Wire(false);
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ConfigureLR(trackArc,   trackWidth,   trackColor);
            ConfigureLR(successArc, successWidth, successColor);
            ConfigureLR(perfectArc, perfectWidth, perfectColor);
            UpdateCenterImageSize();
            RedrawAll();
            SyncMarkerImmediate();
        }
    }

    // ---------- Wiring ----------

    private void Wire(bool sub)
    {
        if (controller == null) controller = FindObjectOfType<ParryQTEController>();
        if (controller == null) return;

        if (sub)
        {
            controller.OnQTEStarted  += HandleStart;
            controller.OnQTEProgress += HandleProgress;
            controller.OnQTEFinished += HandleFinish;
        }
        else
        {
            controller.OnQTEStarted  -= HandleStart;
            controller.OnQTEProgress -= HandleProgress;
            controller.OnQTEFinished -= HandleFinish;
        }
    }

    // ---------- Event Handlers ----------

    private void HandleStart()
    {
        // appear
        if (hideWhenIdle) SetVisible(true);

        // choose center sprite for hotkey
        UpdateCenterIconFromKey();
        if (centerRenderer != null) centerRenderer.enabled = true;

        // reset colors & draw
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        SetLRColor(successArc, successColor);
        SetLRColor(perfectArc, perfectColor);

        RedrawAll();
        ApplyMarker(0f);
    }

    private void HandleProgress(float p) => ApplyMarker(p);

    private void HandleFinish(QTEResult r)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash(r.success));

        if (hideCenterOnFinish && centerRenderer != null)
            centerRenderer.enabled = false;

        if (hideWhenIdle) StartCoroutine(HideAfterFlash());
    }

    private IEnumerator Flash(bool ok)
    {
        Color aFrom = successColor, aTo = ok ? successFlash : failFlash;
        Color bFrom = perfectColor, bTo = ok ? successFlash : failFlash;

        float t = 0f;
        while (t < flashDuration)
        {
            float u = Mathf.InverseLerp(0f, flashDuration, t);
            SetLRColor(successArc, Color.Lerp(aFrom, aTo, u));
            SetLRColor(perfectArc, Color.Lerp(bFrom, bTo, u * 0.75f));
            t += Time.deltaTime;
            yield return null;
        }

        SetLRColor(successArc, successColor);
        SetLRColor(perfectArc, perfectColor);
        flashRoutine = null;
    }

    private IEnumerator HideAfterFlash()
    {
        // small delay so the flash can be seen
        if (flashDuration > 0f) yield return new WaitForSeconds(flashDuration * 0.6f);
        SetVisible(false);
    }

    // ---------- Drawing ----------

    private void RedrawAll()
    {
        DrawFullTrack();
        DrawBands();
    }

    private void DrawFullTrack()
    {
        if (trackArc == null) return;
        DrawArc(trackArc, 0f, 1f);
        SetLRColor(trackArc, trackColor);
    }

    private void DrawBands()
    {
        if (controller == null) return;

        float c  = Mathf.Clamp01(controller.SuccessCenter);
        float sH = Mathf.Clamp01(controller.SuccessHalf);
        float pH = Mathf.Clamp01(controller.PerfectHalf);

        // --- Enforce: success band must not start in first X% (visual clamp) ---
        // NOTE: This only affects visuals. Ideally, clamp in the controller too
        // so logic & visuals match perfectly.
        float minStart = Mathf.Clamp01(minWindowStartFraction);
        float minCenter = minStart + sH;
        if (c < minCenter) c = minCenter;

        float s0 = Mathf.Clamp01(c - sH);
        float s1 = Mathf.Clamp01(c + sH);
        float p0 = Mathf.Clamp01(c - pH);
        float p1 = Mathf.Clamp01(c + pH);

        DrawArc(successArc, s0, s1);
        DrawArc(perfectArc, p0, p1);

        SetLRColor(successArc, successColor);
        SetLRColor(perfectArc, perfectColor);
    }

    // t0/t1 in [0,1] along ring; local space
    private void DrawArc(LineRenderer lr, float t0, float t1)
    {
        if (lr == null) return;

        lr.useWorldSpace = false;
        int segs = Mathf.Max(2, Mathf.CeilToInt(arcSegments * Mathf.Clamp01(Mathf.Abs(t1 - t0))));
        lr.positionCount = segs;

        float sweep = (leftToRight ? -1f : 1f) * sweepAngleDeg;
        float a0 = (startAngleDeg + sweep * Mathf.Clamp01(t0)) * Mathf.Deg2Rad;
        float a1 = (startAngleDeg + sweep * Mathf.Clamp01(t1)) * Mathf.Deg2Rad;

        for (int i = 0; i < segs; i++)
        {
            float u = (segs == 1) ? 0f : i / (float)(segs - 1);
            float a = Mathf.Lerp(a0, a1, u);
            Vector3 p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            lr.SetPosition(i, p);
        }
    }

    private void ApplyMarker(float t01)
    {
        if (marker == null) return;

        t01 = Mathf.Clamp01(t01);
        float sweep = (leftToRight ? -1f : 1f) * sweepAngleDeg;
        float ang = (startAngleDeg + sweep * t01) * Mathf.Deg2Rad;

        Vector3 local = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
        marker.localPosition = local;

        float zDeg = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - 90f;
        var e = marker.localEulerAngles; e.z = zDeg; marker.localEulerAngles = e;
    }

    // ---------- Center sprite logic ----------

    public void SetCenterSprite(Sprite sprite, float size = -1f)
    {
        if (sprite != null) fallbackCenterSprite = sprite;
        if (size > 0f) centerSize = size;
        UpdateCenterIcon(fallbackCenterSprite);
    }

    private void UpdateCenterIconFromKey()
    {
        if (centerRenderer == null) return;

        // Try to read a 'RequiredKey' or 'CurrentKey' property from controller (common names)
        Key? keyFound = null;
        try
        {
            var type = controller.GetType();
            var prop = type.GetProperty("RequiredKey") ?? type.GetProperty("CurrentKey");
            if (prop != null && prop.PropertyType == typeof(Key))
            {
                keyFound = (Key)prop.GetValue(controller, null);
            }
        }
        catch { /* ignore */ }

        Sprite chosen = fallbackCenterSprite;
        if (keyFound.HasValue)
        {
            for (int i = 0; i < keySprites.Count; i++)
            {
                if (keySprites[i].key == keyFound.Value)
                {
                    chosen = keySprites[i].sprite != null ? keySprites[i].sprite : fallbackCenterSprite;
                    break;
                }
            }
        }

        UpdateCenterIcon(chosen);
    }

    private void UpdateCenterIcon(Sprite sprite)
    {
        if (centerRenderer == null) return;

        if (sprite != null)
        {
            centerRenderer.sprite = sprite;
            centerRenderer.enabled = true;
            UpdateCenterImageSize();
            centerRenderer.transform.localPosition = Vector3.zero;
        }
        else
        {
            centerRenderer.enabled = false;
        }
    }

    private void UpdateCenterImageSize()
    {
        if (centerRenderer == null || centerRenderer.sprite == null) return;

        var b = centerRenderer.sprite.bounds.size;
        float maxDim = Mathf.Max(b.x, b.y);
        if (maxDim <= 0f) return;

        float s = centerSize / maxDim;
        centerRenderer.transform.localScale = Vector3.one * s;
    }

    // ---------- Visibility ----------

    private void SetVisible(bool v)
    {
        visibleNow = v;

        if (trackArc)   trackArc.enabled   = v;
        if (successArc) successArc.enabled = v;
        if (perfectArc) perfectArc.enabled = v;
        if (marker)     marker.gameObject.SetActive(v);
        if (centerRenderer) centerRenderer.enabled = v && centerRenderer.sprite != null;
    }

    private static void ConfigureLR(LineRenderer lr, float width, Color c)
    {
        if (lr == null) return;
        lr.useWorldSpace = false;
        lr.widthMultiplier = width;
        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;
        SetLRColor(lr, c);
    }

    private static void SetLRColor(LineRenderer lr, Color c)
    {
        if (lr == null) return;
        lr.startColor = c;
        lr.endColor   = c;
    }

    private void SyncMarkerImmediate()
    {
        if (!Application.isPlaying || controller == null) { ApplyMarker(0f); return; }
        ApplyMarker(controller.Progress);
    }
}
