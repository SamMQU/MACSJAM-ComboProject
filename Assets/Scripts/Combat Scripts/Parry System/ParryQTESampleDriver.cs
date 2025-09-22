// Assets/Scripts/Combat/QTE/ParryQTESampleDriver.cs
using UnityEngine;

public class ParryQTESampleDriver : MonoBehaviour
{
    [SerializeField] private ParryQTEController qte;
    [SerializeField] private float leadTimeBeforeImpact = 0.8f;

    private bool pendingAttack;
    private float impactTime;

    private void Awake()
    {
        if (qte != null)
            qte.OnQTEFinished += HandleQteFinished;
    }

    private void OnDestroy()
    {
        if (qte != null)
            qte.OnQTEFinished -= HandleQteFinished;
    }

    // Call this from your enemy AI when it starts the attack animation
    public void ScheduleIncomingAttack(float timeFromNow)
    {
        pendingAttack = true;
        impactTime = Time.time + Mathf.Max(0.05f, timeFromNow);

        // Start the QTE a bit before the impact so the bar runs into the hit window at the strike moment
        float startAt = impactTime - Mathf.Max(0.05f, leadTimeBeforeImpact);
        StartCoroutine(StartQteAt(startAt));
    }

    private System.Collections.IEnumerator StartQteAt(float atTime)
    {
        while (Time.time < atTime) yield return null;
        if (qte != null) qte.StartQTE();
    }

    private void HandleQteFinished(QTEResult result)
    {
        // Hook into your parry logic:
        // - On success: refund AP, negate damage, trigger counter, etc.
        // - Use result.accuracy to scale rewards (e.g., more AP for tighter hit).
        if (result.success)
        {
            Debug.Log($"PARRY! Key {result.requiredKey} | accuracy {result.accuracy:0.00}");
            // TODO: Your parry reward logic here
        }
        else
        {
            Debug.Log("Parry failed.");
            // TODO: Your damage application here
        }
    }
}
