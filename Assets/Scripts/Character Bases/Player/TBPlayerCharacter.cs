using System;
using UnityEngine;

[Serializable]
public class PlayerAbility
{
    [SerializeField] private string abilityName = "Ability";
    [SerializeField] private int apCost = 2;
    [SerializeField] private int damage = 8;

    public string Name  => abilityName;
    public int APCost   => Mathf.Max(0, apCost);
    public int Damage   => Mathf.Max(0, damage);
}

public class TBPlayerCharacter : TBCharacterBase, APBarBinder.IAPSource
{
    [Header("Player Combat")]
    [SerializeField] private int baseAttackDamage = 1;

    [Header("Attack Points (AP)")]
    [SerializeField, Range(0, 5)] private int maxAP = 5;     // cap at 5 per your design
    [SerializeField, Range(0, 5)] private int startingAP = 0;

    [Tooltip("AP gained on a successful parry (negate damage).")]
    [SerializeField] private int apOnParrySuccess = 1;

    [Tooltip("AP gained on a perfect parry (negate + riposte).")]
    [SerializeField] private int apOnParryPerfect = 2;

    [Header("Riposte (Perfect Parry only)")]
    [SerializeField] private int riposteDamage = 2;

    [Header("Abilities (consume AP)")]
    [SerializeField] private PlayerAbility[] abilities;

    private int currentAP;

    // ---- Public API / Getters ----
    public int BaseAttackDamage   => Mathf.Max(0, baseAttackDamage);
    public int RiposteDamage      => Mathf.Max(0, riposteDamage);
    public int APOnParrySuccess   => Mathf.Max(0, apOnParrySuccess);
    public int APOnParryPerfect   => Mathf.Max(0, apOnParryPerfect);
    public PlayerAbility[] Abilities => abilities;

    // IAPSource (for APBarBinder)
    public int MaxAP => maxAP;
    public int CurrentAP => currentAP;
    public event Action<int,int> OnAPChanged; // (current, max)

    protected override void Awake()
    {
        base.Awake();
        maxAP = Mathf.Clamp(maxAP, 0, 5);
        currentAP = Mathf.Clamp(startingAP, 0, maxAP);
        OnAPChanged?.Invoke(currentAP, maxAP); // initialize AP UI
    }

    public void GainAP(int amount)
    {
        if (amount <= 0) return;
        int before = currentAP;
        currentAP = Mathf.Clamp(currentAP + amount, 0, maxAP);
        if (currentAP != before) OnAPChanged?.Invoke(currentAP, maxAP);
    }

    public bool SpendAP(int amount)
    {
        if (amount <= 0) return true;
        if (currentAP < amount) return false;
        currentAP -= amount;
        OnAPChanged?.Invoke(currentAP, maxAP);
        return true;
    }

    // Optional helpers (useful in battle setup/reset)
    public void ResetAP()
    {
        if (currentAP == 0) return;
        currentAP = 0;
        OnAPChanged?.Invoke(currentAP, maxAP);
    }

    public void SetMaxAP(int newMax, bool clampCurrent = true)
    {
        int oldMax = maxAP;
        maxAP = Mathf.Clamp(newMax, 0, 5);
        if (clampCurrent) currentAP = Mathf.Clamp(currentAP, 0, maxAP);
        if (maxAP != oldMax) OnAPChanged?.Invoke(currentAP, maxAP);
    }
}
