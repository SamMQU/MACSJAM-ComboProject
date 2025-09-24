using UnityEngine;

public class HeartBarBinder : MonoBehaviour
{
    [SerializeField] private TBCharacterBase character; // player or enemy
    [SerializeField] private HeartBarUI heartBar;

    private void Awake()
    {
        if (!heartBar) heartBar = GetComponentInChildren<HeartBarUI>(true);
        if (!character) character = GetComponentInParent<TBCharacterBase>();
    }

    private void OnEnable()
    {
        if (!character || !heartBar) return;

        // initialize once (covers scene load)
        heartBar.SetMaxHP(character.MaxHP, character.CurrentHP);

        character.OnHealthChanged += HandleHPChanged;
    }

    private void OnDisable()
    {
        if (character != null)
            character.OnHealthChanged -= HandleHPChanged;
    }

    private void HandleHPChanged(TBCharacterBase who, int cur, int max)
    {
        if (heartBar) heartBar.SetMaxHP(max, cur);
    }
}
