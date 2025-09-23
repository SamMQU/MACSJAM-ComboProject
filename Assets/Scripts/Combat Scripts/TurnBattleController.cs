using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BattlePhase
{
    None,
    PlayerTurn,
    EnemyTelegraph, // wind-up during which parry QTE runs
    EnemyResolve,
    Victory,
    Defeat
}

public class TurnBattleController : MonoBehaviour
{
    [Header("Participants")]
    [SerializeField] private TBPlayerCharacter player;
    [SerializeField] private TBEnemyCharacter enemy;

    [Header("Parry QTE")]
    [SerializeField] private ParryQTEController parryQTE;
    [SerializeField, Tooltip("Seconds the QTE runs before the 'impact' moment.")]
    private float qteLeadTime = 0.8f;

    [Header("Input (New Input System - Keyboard)")]
    [SerializeField] private Key keyBaseAttack = Key.A;
    [SerializeField] private Key keyAbility1   = Key.Q;
    [SerializeField] private Key keyAbility2   = Key.W;
    [SerializeField] private Key keySkipTurn   = Key.S;

    [Header("Enemy AI")]
    [SerializeField, Tooltip("Relative weight for choosing base attack vs abilities.")]
    private int enemyBaseAttackWeight = 50;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BattlePhase phase = BattlePhase.None;
    private System.Random rng;
    private bool waitingForPlayerChoice;
    private bool qteActive;
    private int pendingEnemyDamage;

    // Getters
    public BattlePhase Phase => phase;

    private void Awake()
    {
        if (rng == null)
            rng = new System.Random(unchecked(System.Environment.TickCount * 397) ^ System.Guid.NewGuid().GetHashCode());

        if (parryQTE != null)
            parryQTE.OnQTEFinished += HandleParryFinished;
    }

    private void OnDestroy()
    {
        if (parryQTE != null)
            parryQTE.OnQTEFinished -= HandleParryFinished;
    }

    private void Start()
    {
        StartCoroutine(BeginBattle());
    }

    private IEnumerator BeginBattle()
    {
        phase = BattlePhase.PlayerTurn;
        waitingForPlayerChoice = true;
        if (logDebug) Debug.Log("[Battle] Start: Player Turn");
        yield break;
    }

    private void Update()
    {
        if (phase == BattlePhase.PlayerTurn && waitingForPlayerChoice)
        {
            HandlePlayerInput();
        }
    }

    // ---------- Player Turn ----------

    private void HandlePlayerInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Base Attack
        if (kb[keyBaseAttack].wasPressedThisFrame)
        {
            StartCoroutine(DoPlayerBaseAttack());
            return;
        }

        // Ability 1
        if (kb[keyAbility1].wasPressedThisFrame)
        {
            TryUseAbilityIndex(0);
            return;
        }

        // Ability 2
        if (kb[keyAbility2].wasPressedThisFrame)
        {
            TryUseAbilityIndex(1);
            return;
        }

        // Skip
        if (kb[keySkipTurn].wasPressedThisFrame)
        {
            StartCoroutine(DoPlayerSkipTurn());
            return;
        }
    }

    private void TryUseAbilityIndex(int idx)
    {
        var abilities = player.Abilities;
        if (abilities == null || idx < 0 || idx >= abilities.Length)
        {
            if (logDebug) Debug.Log("[Player] No such ability slot.");
            return;
        }

        var ab = abilities[idx];
        if (ab == null)
        {
            if (logDebug) Debug.Log("[Player] Ability slot is empty.");
            return;
        }

        if (!player.SpendAP(ab.APCost))
        {
            if (logDebug) Debug.Log($"[Player] Not enough AP for {ab.Name} (cost {ab.APCost}).");
            return;
        }

        waitingForPlayerChoice = false;
        StartCoroutine(DoPlayerAbility(ab));
    }

    private IEnumerator DoPlayerBaseAttack()
    {
        waitingForPlayerChoice = false;
        if (logDebug) Debug.Log($"[Player] Base Attack for {player.BaseAttackDamage} dmg.");
        yield return PlayPlayerAttackAnim();
        enemy.TakeDamage(player.BaseAttackDamage);

        if (enemy.IsDead) { phase = BattlePhase.Victory; if (logDebug) Debug.Log("[Battle] Victory!"); yield break; }

        yield return StartEnemyTurn();
    }

    private IEnumerator DoPlayerAbility(PlayerAbility ab)
    {
        if (logDebug) Debug.Log($"[Player] Cast {ab.Name} for {ab.Damage} dmg (AP spent).");
        yield return PlayPlayerAbilityAnim(ab.Name);
        enemy.TakeDamage(ab.Damage);

        if (enemy.IsDead) { phase = BattlePhase.Victory; if (logDebug) Debug.Log("[Battle] Victory!"); yield break; }

        yield return StartEnemyTurn();
    }

    private IEnumerator DoPlayerSkipTurn()
    {
        if (logDebug) Debug.Log("[Player] Skips turn.");
        yield return PlayPlayerSkipAnim();
        yield return StartEnemyTurn();
    }

    // ---------- Enemy Turn + Parry ----------

    private IEnumerator StartEnemyTurn()
    {
        phase = BattlePhase.EnemyTelegraph;

        // Decide the attack
        var action = enemy.PickAction(rng, enemyBaseAttackWeight);
        pendingEnemyDamage = action.kind == TBEnemyCharacter.EnemyActionKind.Base
            ? enemy.BaseAttackDamage
            : (action.ability != null ? action.ability.Damage : enemy.BaseAttackDamage);

        if (logDebug)
        {
            string label = action.kind == TBEnemyCharacter.EnemyActionKind.Base
                ? "Base Attack"
                : $"Ability: {action.ability.Name}";
            Debug.Log($"[Enemy] Telegraphing: {label} -> {pendingEnemyDamage} dmg");
        }

        // Telegraph animation here if you have one
        yield return PlayEnemyTelegraphAnim();

        // Start the parry QTE so the sweep reaches the impact near the end
        if (parryQTE != null)
        {
            qteActive = true;
            parryQTE.StartQTE();
        }

        // Wait until impact moment (qteLeadTime later)
        float end = Time.time + Mathf.Max(0.1f, qteLeadTime);
        while (Time.time < end && qteActive)
            yield return null;

        // If the QTE already finished (success/fail), HandleParryFinished will resolve. If not, we time out:
        if (qteActive)
        {
            // Time-out means no button press -> treat as fail
            if (logDebug) Debug.Log("[Parry] Timeout -> Fail (full damage).");
            ResolveEnemyHit_ParryFail();
        }
    }

    private void HandleParryFinished(QTEResult result)
    {
        if (!qteActive) return;
        qteActive = false;

        switch (result.quality)
        {
            case QTEHitQuality.Fail:
                if (logDebug) Debug.Log("[Parry] Fail -> take full damage.");
                ResolveEnemyHit_ParryFail();
                break;

            case QTEHitQuality.Success:
                if (logDebug) Debug.Log("[Parry] Success -> negate damage, gain AP.");
                ResolveEnemyHit_ParrySuccess();
                break;

            case QTEHitQuality.Perfect:
                if (logDebug) Debug.Log("[Parry] PERFECT -> negate damage, gain AP+, RIPOSTE!");
                ResolveEnemyHit_ParryPerfect();
                break;
        }
    }

    private void ResolveEnemyHit_ParryFail()
    {
        phase = BattlePhase.EnemyResolve;
        StartCoroutine(EnemyDealDamageThenBackToPlayer());
    }

    private void ResolveEnemyHit_ParrySuccess()
    {
        phase = BattlePhase.EnemyResolve;
        player.GainAP(player.APOnParrySuccess);
        // no damage taken
        StartCoroutine(BackToPlayerAfterEnemyAnimation());
    }

    private void ResolveEnemyHit_ParryPerfect()
    {
        phase = BattlePhase.EnemyResolve;
        player.GainAP(player.APOnParryPerfect);
        // no damage; plus riposte
        StartCoroutine(EnemyRiposteThenBack());
    }

    private IEnumerator EnemyDealDamageThenBackToPlayer()
    {
        yield return PlayEnemyStrikeAnim();
        player.TakeDamage(pendingEnemyDamage);

        if (player.IsDead) { phase = BattlePhase.Defeat; if (logDebug) Debug.Log("[Battle] Defeat..."); yield break; }

        yield return BackToPlayer();
    }

    private IEnumerator BackToPlayerAfterEnemyAnimation()
    {
        yield return PlayEnemyStrikeAnim(); // show the swing, but damage was negated
        yield return BackToPlayer();
    }

    private IEnumerator EnemyRiposteThenBack()
    {
        // Enemy swings (negated)
        yield return PlayEnemyStrikeAnim();

        // Player ripostes
        yield return PlayPlayerRiposteAnim();
        enemy.TakeDamage(player.RiposteDamage);

        if (enemy.IsDead) { phase = BattlePhase.Victory; if (logDebug) Debug.Log("[Battle] Victory!"); yield break; }
        yield return BackToPlayer();
    }

    private IEnumerator BackToPlayer()
    {
        pendingEnemyDamage = 0;
        phase = BattlePhase.PlayerTurn;
        waitingForPlayerChoice = true;
        if (logDebug) Debug.Log("[Battle] Player Turn");
        yield break;
    }

    // ---------- Tiny animation stubs (replace with your anims/VFX) ----------

    private IEnumerator PlayPlayerAttackAnim()  { yield return null; }
    private IEnumerator PlayPlayerAbilityAnim(string name) { yield return null; }
    private IEnumerator PlayPlayerSkipAnim()    { yield return null; }
    private IEnumerator PlayEnemyTelegraphAnim(){ yield return null; }
    private IEnumerator PlayEnemyStrikeAnim()   { yield return null; }
    private IEnumerator PlayPlayerRiposteAnim() { yield return null; }
}
