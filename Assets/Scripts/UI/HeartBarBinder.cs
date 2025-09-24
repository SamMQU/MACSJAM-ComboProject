using UnityEngine;

public class HeartBarBinder : MonoBehaviour
{
    [SerializeField] private TBCharacterBase character; // current target
    [SerializeField] private HeartBarUI heartBar;
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        if (!heartBar) heartBar = GetComponentInChildren<HeartBarUI>(true);
        if (!character) character = GetComponentInParent<TBCharacterBase>();
    }

    private void OnEnable()
    {
        if (character != null) Subscribe(character);
    }

    private void OnDisable()
    {
        if (character != null) Unsubscribe(character);
    }

    public void BindCharacter(TBCharacterBase newChar)
    {
        // if (logDebug) Debug.Log($"[HeartBarBinder] BindCharacter -> {(newChar ? newChar.name : "null")}");
        if (character == newChar) { ForceRefresh(); return; }

        if (character != null) Unsubscribe(character);
        character = newChar;

        if (isActiveAndEnabled && character != null)
            Subscribe(character);
    }

    public void ForceRefresh()
    {
        if (heartBar != null && character != null)
            heartBar.SetMaxHP(character.MaxHP, character.CurrentHP);
    }

    private void Subscribe(TBCharacterBase c)
    {
        if (!heartBar || c == null) return;
        heartBar.SetMaxHP(c.MaxHP, c.CurrentHP);   // immediate draw
        c.OnHealthChanged += HandleHPChanged;
        // if (logDebug) Debug.Log("[HeartBarBinder] Subscribed.");
    }

    private void Unsubscribe(TBCharacterBase c)
    {
        c.OnHealthChanged -= HandleHPChanged;
        // if (logDebug) Debug.Log("[HeartBarBinder] Unsubscribed.");
    }

    private void HandleHPChanged(TBCharacterBase who, int cur, int max)
    {
        if (heartBar != null) heartBar.SetMaxHP(max, cur);
    }
}
