using System;
using UnityEngine;

[Serializable]
public class PlayerAbility
{
    [SerializeField] private string abilityName = "Ability";
    [SerializeField] private int apCost = 2;
    [SerializeField] private int damage = 8;

    public string Name => abilityName;
    public int APCost => Mathf.Max(0, apCost);
    public int Damage => Mathf.Max(0, damage);
}

public class TBPlayerCharacter : TBCharacterBase
{
    [Header("Player Combat")]
    [SerializeField] private int baseAttackDamage = 6;

    [Header("Attack Points (AP)")]
    [SerializeField] private int maxAP = 10;
    [SerializeField] private int startingAP = 0;

    [Tooltip("AP gained on a successful parry (negate damage).")]
    [SerializeField] private int apOnParrySuccess = 2;

    [Tooltip("AP gained on a perfect parry (negate + riposte).")]
    [SerializeField] private int apOnParryPerfect = 3;

    [Header("Riposte (Perfect Parry only)")]
    [SerializeField] private int riposteDamage = 10;

    [Header("Abilities (consume AP)")]
    [SerializeField] private PlayerAbility[] abilities;

    private int currentAP;

    // Getters
    public int BaseAttackDamage => Mathf.Max(0, baseAttackDamage);
    public int CurrentAP => currentAP;
    public int MaxAP => maxAP;
    public PlayerAbility[] Abilities => abilities;
    public int RiposteDamage => Mathf.Max(0, riposteDamage);
    public int APOnParrySuccess => Mathf.Max(0, apOnParrySuccess);
    public int APOnParryPerfect => Mathf.Max(0, apOnParryPerfect);

    protected override void Awake()
    {
        base.Awake();
        currentAP = Mathf.Clamp(startingAP, 0, maxAP);
    }

    public void GainAP(int amount)
    {
        if (amount <= 0) return;
        currentAP = Mathf.Min(maxAP, currentAP + amount);
    }

    public bool SpendAP(int amount)
    {
        if (amount <= 0) return true;
        if (currentAP < amount) return false;
        currentAP -= amount;
        return true;
    }
}
