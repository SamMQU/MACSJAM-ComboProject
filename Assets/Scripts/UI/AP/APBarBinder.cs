using UnityEngine;

public class APBarBinder : MonoBehaviour
{
    [SerializeField] private MonoBehaviour apOwner; // assign TBPlayerCharacter (or your AP component)
    [SerializeField] private APBarUI apBar;

    // The AP interface we expect (implemented by your player component)
    public interface IAPSource
    {
        int MaxAP { get; }
        int CurrentAP { get; }
        event System.Action<int,int> OnAPChanged; // (current, max)
    }

    private IAPSource source;

    private void Awake()
    {
        if (!apBar) apBar = GetComponentInChildren<APBarUI>(true);
        source = apOwner as IAPSource;
        if (source == null && apOwner != null)
            Debug.LogError("[APBarBinder] apOwner does not implement IAPSource.");
    }

    private void OnEnable()
    {
        if (source == null || apBar == null) return;
        apBar.SetMaxAP(source.MaxAP, source.CurrentAP);
        source.OnAPChanged += HandleAP;
    }

    private void OnDisable()
    {
        if (source != null) source.OnAPChanged -= HandleAP;
    }

    private void HandleAP(int current, int max)
    {
        apBar.SetMaxAP(max, current);
    }
}
