using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class SemiCircularQTEVisualizer : MonoBehaviour
{
    [Header("QTE Source")]
    [SerializeField] private ParryQTEController controller;

    [Header("Geometry (Visualizer fields)")]
    [SerializeField] private float radius = 1.2f;          // visual radius in world units
    [SerializeField] private int arcSegments = 48;         // smoothness (points per arc)
    [SerializeField] private float startAngleDeg = 180f;   // where t=0 sits (180 = left end)
    [SerializeField] private float sweepAngleDeg = 180f;   // 180 = semi circle
    [SerializeField] private bool leftToRight = true;      // flip sweep direction

    [Header("Visual Parts (assign)")]
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

    private Coroutine flashRoutine;

    private void OnEnable()
    {
        Wire(true);
        ConfigureLR(trackArc,   trackWidth,   trackColor);
        ConfigureLR(successArc, successWidth, successColor);
        ConfigureLR(perfectArc, perfectWidth, perfectColor);
        RedrawAll();
        SyncMarkerImmediate();
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
            RedrawAll();
            SyncMarkerImmediate();
        }
    }

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

    private void HandleStart()
    {
        RedrawAll();
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        SetLRColor(successArc, successColor);
        SetLRColor(perfectArc, perfectColor);
        ApplyMarker(0f);
    }

    private void HandleProgress(float p) => ApplyMarker(p);

    private void HandleFinish(QTEResult r)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash(r.success));
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

    // -------- drawing --------

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

        float s0 = Mathf.Clamp01(c - sH);
        float s1 = Mathf.Clamp01(c + sH);
        float p0 = Mathf.Clamp01(c - pH);
        float p1 = Mathf.Clamp01(c + pH);

        DrawArc(successArc, s0, s1);
        DrawArc(perfectArc, p0, p1);

        SetLRColor(successArc, successColor);
        SetLRColor(perfectArc, perfectColor);
    }

    // t0/t1 in [0,1] mapped along semi-arc; writes positions in LOCAL SPACE
    private void DrawArc(LineRenderer lr, float t0, float t1)
    {
        if (lr == null) return;

        lr.useWorldSpace = false;
        lr.positionCount = Mathf.Max(2, arcSegments + 1);

        float sweep = (leftToRight ? -1f : 1f) * sweepAngleDeg; // left->right visual
        float a0 = (startAngleDeg + sweep * Mathf.Clamp01(t0)) * Mathf.Deg2Rad;
        float a1 = (startAngleDeg + sweep * Mathf.Clamp01(t1)) * Mathf.Deg2Rad;

        for (int i = 0; i < lr.positionCount; i++)
        {
            float u = i / (float)(lr.positionCount - 1);
            float a = Mathf.Lerp(a0, a1, u);
            Vector3 p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            lr.SetPosition(i, p); // local space
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

        // Optional: point the marker outward (so its up vector is radial)
        float zDeg = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - 90f;
        var e = marker.localEulerAngles; e.z = zDeg; marker.localEulerAngles = e;
    }

    // -------- helpers --------

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
        lr.endColor = c;
    }

    private void SyncMarkerImmediate()
    {
        if (!Application.isPlaying || controller == null) { ApplyMarker(0f); return; }
        ApplyMarker(controller.Progress);
    }
}
