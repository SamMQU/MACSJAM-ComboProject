using UnityEngine;
using UnityEngine.InputSystem;

public class QTEManualTrigger : MonoBehaviour
{
    [SerializeField] private ParryQTEController qte;
    [SerializeField] private Key triggerKey = Key.T;

    private void Awake()
    {
        if (qte == null) qte = FindObjectOfType<ParryQTEController>();
        if (qte != null) qte.OnQTEFinished += OnFinished;
    }

    private void OnDestroy()
    {
        if (qte != null) qte.OnQTEFinished -= OnFinished;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            if (qte != null) qte.StartQTE();
            else Debug.LogError("[QTEManualTrigger] No ParryQTEController found.");
        }
    }

    private void OnFinished(QTEResult r)
    {
        Debug.Log($"[QTEManualTrigger] Finished: success={r.success}, key={r.requiredKey}, acc={r.accuracy:0.00}");
    }
}
