using System;
using UnityEngine;

[Serializable]
public class EnemyAbility
{
    [SerializeField] private string abilityName = "Enemy Skill";
    [SerializeField] private int damage = 9;
    [Range(0, 100)]
    [SerializeField] private int chanceWeight = 50;

    [Header("VFX")]
    [SerializeField] private GameObject vfxPrefab;  // optional

    public string Name => abilityName;
    public int Damage => Mathf.Max(0, damage);
    public int Weight => Mathf.Max(0, chanceWeight);
    public GameObject VFXPrefab => vfxPrefab;
}

public class TBEnemyCharacter : TBCharacterBase
{
    [Header("Enemy Combat")]
    [SerializeField] private int baseAttackDamage = 5;

    [Header("Ability Table (2 entries recommended)")]
    [SerializeField] private EnemyAbility[] abilities = new EnemyAbility[2];

    public int BaseAttackDamage => Mathf.Max(0, baseAttackDamage);
    public EnemyAbility[] Abilities => abilities;

    /// Weighted pick between base attack and abilities.
    /// Provide a weight for the base attack as well.
    public enum EnemyActionKind { Base, Ability }
    public struct EnemyAction
    {
        public EnemyActionKind kind;
        public EnemyAbility ability; // null for base
        public int Damage => kind == EnemyActionKind.Base ? baseDamage : (ability != null ? ability.Damage : 0);

        private int baseDamage;
        public EnemyAction(EnemyActionKind k, int baseDmg, EnemyAbility ab)
        {
            kind = k; baseDamage = baseDmg; ability = ab;
        }
    }

    public EnemyAction PickAction(System.Random rng, int baseAttackWeight = 50)
    {
        int total = Mathf.Max(0, baseAttackWeight);
        if (abilities != null)
            foreach (var a in abilities) if (a != null) total += a.Weight;

        if (total <= 0) return new EnemyAction(EnemyActionKind.Base, BaseAttackDamage, null);

        int roll = rng.Next(0, total);
        int acc = 0;

        // base first
        acc += Mathf.Max(0, baseAttackWeight);
        if (roll < acc) return new EnemyAction(EnemyActionKind.Base, BaseAttackDamage, null);

        // abilities
        if (abilities != null)
        {
            foreach (var a in abilities)
            {
                if (a == null) continue;
                int w = Mathf.Max(0, a.Weight);
                if (w <= 0) continue;
                if (roll < acc + w) return new EnemyAction(EnemyActionKind.Ability, BaseAttackDamage, a);
                acc += w;
            }
        }

        return new EnemyAction(EnemyActionKind.Base, BaseAttackDamage, null);
    }
}
