using UnityEngine;

public class QTEBarProgressor : MonoBehaviour
{
    [SerializeField] private ParryQTEController controller;
    [SerializeField] private Transform fillTransform;
    [SerializeField] private bool autoWireOnAwake = true;

    private Vector3 baseScale;

    private void Awake()
    {
        if (autoWireOnAwake && controller == null)
            controller = FindObjectOfType<ParryQTEController>();

        if (fillTransform == null) fillTransform = transform;
        baseScale = fillTransform.localScale;

        if (controller != null)
        {
            controller.OnQTEStarted += HandleStarted;
            controller.OnQTEProgress += HandleProgress;
        }
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnQTEStarted -= HandleStarted;
            controller.OnQTEProgress -= HandleProgress;
        }
    }

    private void HandleStarted() => SetProgress(0f);
    private void HandleProgress(float p) => SetProgress(p);

    private void SetProgress(float p)
    {
        float clamped = Mathf.Clamp01(p);
        Vector3 s = baseScale;
        s.x = Mathf.Max(0.0001f, clamped * baseScale.x);
        fillTransform.localScale = s;
    }
}
