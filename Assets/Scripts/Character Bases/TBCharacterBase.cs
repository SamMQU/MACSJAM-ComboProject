using System;
using UnityEngine;

public abstract class TBCharacterBase : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Unit";

    [Header("Stats (integers)")]
    [SerializeField] private int maxHP = 50;
    [SerializeField] private int startingHP = 50;

    // Runtime
    private int currentHP;

    // Events
    public event Action<TBCharacterBase, int> OnDamaged;  // (target, amount)
    public event Action<TBCharacterBase, int> OnHealed;   // (target, amount)
    public event Action<TBCharacterBase> OnDied;

    // Getters
    public string DisplayName => displayName;
    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public bool IsDead => currentHP <= 0;

    protected virtual void Awake()
    {
        currentHP = Mathf.Clamp(startingHP, 0, maxHP);
    }

    public virtual void ResetToFull()
    {
        currentHP = maxHP;
    }

    public virtual void TakeDamage(int amount)
    {
        int dmg = Mathf.Max(0, amount);
        if (dmg <= 0 || IsDead) return;

        int before = currentHP;
        currentHP = Mathf.Max(0, currentHP - dmg);
        OnDamaged?.Invoke(this, dmg);

        if (before > 0 && currentHP == 0)
            OnDied?.Invoke(this);
    }

    public virtual void Heal(int amount)
    {
        int heal = Mathf.Max(0, amount);
        if (heal <= 0 || IsDead) return;

        int before = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + heal);
        OnHealed?.Invoke(this, currentHP - before);
    }
}
