using System;
using UnityEngine;

/// Stages: D(0), C(1), B(2), A(3), S(4)
/// Each stage adds +50% damage: D=1.0x, C=1.5x, B=2.0x, A=2.5x, S=3.0x
/// Progress rule: every 1 damage dealt = +0.5 stage => 2 damage = +1 stage.
/// Getting hit resets to D.
public class ComboSystem : MonoBehaviour
{
    public enum Stage { D = 0, C = 1, B = 2, A = 3, S = 4 }

    [Header("Config")]
    [Tooltip("Units required to advance one full stage (2 units = 2*0.5). Do not change unless you change the rule.")]
    [SerializeField] private int unitsPerStage = 2; // 2 damage = +1 stage (since 1 dmg = +0.5 stage)

    [Tooltip("Cap stage at S and clamp overflow progress.")]
    [SerializeField] private Stage maxStage = Stage.S;

    [Header("Runtime (read-only)")]
    [SerializeField, Range(0, 4)] private int currentStageIndex = 0; // 0..4
    [SerializeField, Range(0, 1)] private int stageUnits = 0;        // 0..(unitsPerStage-1), here 0..1

    // Events
    public event Action<Stage> OnStageChanged;                 // fired when the letter changes (e.g., B -> A)
    public event Action<Stage, float> OnStageProgressChanged;  // (stage, 0..1 fill of current stage)

    public Stage CurrentStage => (Stage)currentStageIndex;
    /// 1.0, 1.5, 2.0, 2.5, 3.0
    public float CurrentMultiplier => 1f + 0.5f * currentStageIndex;

    /// Call when the player deals final, integer damage to the enemy.
    public void AddDamageContribution(int damageDealt)
    {
        if (damageDealt <= 0) { FireProgress(); return; }

        int unitsToAdd = damageDealt; // 1 dmg = 1 unit; unitsPerStage=2 => 2 dmg = 1 stage
        int stage = currentStageIndex;
        int units = stageUnits;

        // advance units/stages
        units += unitsToAdd;
        while (units >= unitsPerStage && stage < (int)maxStage)
        {
            units -= unitsPerStage;
            stage += 1;
        }

        // Clamp at max stage
        if (stage >= (int)maxStage)
        {
            stage = (int)maxStage;
            units = Mathf.Clamp(units, 0, unitsPerStage - 1);
        }

        bool stageChanged = stage != currentStageIndex;

        currentStageIndex = stage;
        stageUnits = units;

        if (stageChanged) OnStageChanged?.Invoke(CurrentStage);
        FireProgress();
    }

    /// Call when the player takes damage (combo breaks).
    public void ResetOnPlayerHit()
    {
        bool stageChanged = currentStageIndex != 0;
        currentStageIndex = 0;
        stageUnits = 0;

        if (stageChanged) OnStageChanged?.Invoke(CurrentStage);
        FireProgress();
    }

    /// Multiplies outgoing damage by current combo. Returns final **integer** damage.
    public int ApplyMultiplier(int baseDamage)
    {
        if (baseDamage <= 0) return 0;
        float mul = CurrentMultiplier; // 1.0 .. 3.0
        // Round to nearest int; feel free to choose Floor/Ceil if preferred
        int final = Mathf.Max(0, Mathf.RoundToInt(baseDamage * mul));
        return final;
    }

    /// 0..1 fraction inside the current stage (0=empty, 1=full)
    public float CurrentStageFill01 => unitsPerStage <= 0 ? 0f : Mathf.Clamp01(stageUnits / (float)unitsPerStage);

    private void FireProgress() => OnStageProgressChanged?.Invoke(CurrentStage, CurrentStageFill01);

    // For convenience in inspector/debug
    [ContextMenu("Debug: Add 1 dmg worth of progress")]
    private void DebugAdd1() => AddDamageContribution(1);

    [ContextMenu("Debug: Reset combo")]
    private void DebugReset() => ResetOnPlayerHit();
}
