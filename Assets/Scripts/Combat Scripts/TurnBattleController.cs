using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BattlePhase
{
    None,
    PlayerTurn,
    EnemyTelegraph,
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

    // QTE result gate
    private QTEResult? lastParryResult;
    private bool qteActive;

    [Header("Input (New Input System - Keyboard)")]
    [SerializeField] private Key keyBaseAttack = Key.A;
    [SerializeField] private Key keyAbility1 = Key.Q;
    [SerializeField] private Key keyAbility2 = Key.W;
    [SerializeField] private Key keySkipTurn = Key.S;

    [Header("Enemy AI")]
    [SerializeField, Tooltip("Relative weight for choosing base attack vs abilities.")]
    private int enemyBaseAttackWeight = 50;
    [Header("Combo")]
    [SerializeField] private ComboSystem combo;

    [Header("VFX")]
    [SerializeField] private VFXManager vfx;
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private Transform playerMuzzle; // optional spawn origin
    [SerializeField] private Transform enemyMuzzle;  // optional spawn origin

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BattlePhase phase = BattlePhase.None;
    private System.Random rng;
    private bool waitingForPlayerChoice;
    private int pendingEnemyDamage;

    public BattlePhase Phase => phase;

    private void Awake()
    {
        if (rng == null)
            rng = new System.Random(unchecked(System.Environment.TickCount * 397) ^ System.Guid.NewGuid().GetHashCode());
        if (parryQTE == null) parryQTE = FindObjectOfType<ParryQTEController>();
    }

    private void OnEnable()
    {
        if (parryQTE != null) parryQTE.OnQTEFinished += HandleParryFinished;
    }

    private void OnDisable()
    {
        if (parryQTE != null) parryQTE.OnQTEFinished -= HandleParryFinished;
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
            HandlePlayerInput();
    }

    // ---------- Player Turn ----------

    private void HandlePlayerInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[keyBaseAttack].wasPressedThisFrame)
        {
            StartCoroutine(DoPlayerBaseAttack());
            return;
        }

        if (kb[keyAbility1].wasPressedThisFrame)
        {
            TryUseAbilityIndex(0);
            return;
        }

        if (kb[keyAbility2].wasPressedThisFrame)
        {
            TryUseAbilityIndex(1);
            return;
        }

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

        int raw = player.BaseAttackDamage;
        int final = combo != null ? combo.ApplyMultiplier(raw) : raw;
        enemy.TakeDamage(final);
        combo?.AddDamageContribution(final);


        if (enemy.IsDead) { phase = BattlePhase.Victory; if (logDebug) Debug.Log("[Battle] Victory!"); yield break; }
        yield return StartEnemyTurn();
    }

    private IEnumerator DoPlayerAbility(PlayerAbility ab)
    {
        if (logDebug) Debug.Log($"[Player] Cast {ab.Name} for {ab.Damage} dmg (AP spent).");
        yield return PlayPlayerAbilityAnim(ab.Name);

        int raw = ab.Damage;
        int final = combo != null ? combo.ApplyMultiplier(raw) : raw;
        enemy.TakeDamage(final);
        combo?.AddDamageContribution(final);

        if (enemy.IsDead) { phase = BattlePhase.Victory; if (logDebug) Debug.Log("[Battle] Victory!"); yield break; }
        yield return StartEnemyTurn();
    }

    private IEnumerator DoPlayerSkipTurn()
    {
        if (logDebug) Debug.Log("[Player] Skips turn.");
        yield return PlayPlayerSkipAnim();
        yield return StartEnemyTurn();
    }

    // ---------- Enemy Turn (QTE → resolve → then VFX if fail) ----------

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
            string label = action.kind == TBEnemyCharacter.EnemyActionKind.Base ? "Base Attack" : $"Ability: {action.ability.Name}";
            Debug.Log($"[Enemy] Telegraphing: {label} -> {pendingEnemyDamage} dmg");
        }

        // Minimal wind-up
        yield return PlayEnemyTelegraphAnim();

        // QTE gate
        lastParryResult = null;
        if (parryQTE != null)
        {
            qteActive = true;
            parryQTE.StartQTE();
            yield return new WaitUntil(() => !qteActive);
        }
        else
        {
            if (logDebug) Debug.LogWarning("[Battle] No ParryQTEController assigned. Treating as FAIL.");
            lastParryResult = new QTEResult { success = false, quality = QTEHitQuality.Fail };
        }

        var result = lastParryResult ?? new QTEResult { success = false, quality = QTEHitQuality.Fail };

        switch (result.quality)
        {
            default:
            case QTEHitQuality.Fail:
                if (logDebug) Debug.Log("[Parry] Fail -> enemy hits (VFX + shake + flash) then damage.");
                yield return EnemyDealDamageThenBackToPlayer(); // plays enemy slash VFX, then applies damage
                break;

            case QTEHitQuality.Success:
                if (logDebug) Debug.Log("[Parry] Success -> negate damage, gain AP. (No enemy VFX)");
                player.GainAP(player.APOnParrySuccess);
                yield return BackToPlayer();
                break;

            case QTEHitQuality.Perfect:
                if (logDebug) Debug.Log("[Parry] PERFECT -> negate damage, gain AP++, riposte VFX + damage.");
                player.GainAP(player.APOnParryPerfect);
                yield return EnemyRiposteThenBack();
                break;
        }
    }

    private void HandleParryFinished(QTEResult result)
    {
        lastParryResult = result;
        qteActive = false;
    }

    // ---------- Resolution ----------

    private IEnumerator EnemyDealDamageThenBackToPlayer()
    {
        // Spawn enemy slash now (after QTE) – this triggers camera shake + player hit flash in VFXManager
        yield return PlayEnemyStrikeAnim();

        player.TakeDamage(pendingEnemyDamage);
        combo?.ResetOnPlayerHit();

        if (player.IsDead)
        {
            phase = BattlePhase.Defeat;
            if (logDebug) Debug.Log("[Battle] Defeat...");
            yield break;
        }

        yield return BackToPlayer();
    }

    private IEnumerator EnemyRiposteThenBack()
    {
        // Enemy attack was negated; spawn player's riposte slash and deal damage to enemy
        yield return PlayPlayerRiposteAnim();

        int raw = player.RiposteDamage;
        int final = combo != null ? combo.ApplyMultiplier(raw) : raw;
        enemy.TakeDamage(final);
        combo?.AddDamageContribution(final);


        if (enemy.IsDead)
        {
            phase = BattlePhase.Victory;
            if (logDebug) Debug.Log("[Battle] Victory!");
            yield break;
        }

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

    // ---------- VFX-backed "anim stubs" ----------

    private IEnumerator PlayPlayerAttackAnim()
    {
        if (logDebug) Debug.Log("[VFX] Player Base Attack VFX");
        PlayPlayerSlash();
        yield return new WaitForSeconds(0.12f);
    }

    private IEnumerator PlayPlayerAbilityAnim(string name)
    {
        if (logDebug) Debug.Log($"[VFX] Player Ability '{name}' VFX");
        PlayPlayerSlash();
        yield return new WaitForSeconds(0.12f);
    }

    private IEnumerator PlayPlayerSkipAnim()
    {
        yield return null;
    }

    private IEnumerator PlayEnemyTelegraphAnim()
    {
        // Keep light; the post-QTE slash is the main visual
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator PlayEnemyStrikeAnim()
    {
        if (logDebug) Debug.Log("[VFX] Enemy Strike VFX");
        PlayEnemySlash();
        yield return new WaitForSeconds(0.12f);
    }

    private IEnumerator PlayPlayerRiposteAnim()
    {
        if (logDebug) Debug.Log("[VFX] Player Riposte VFX");
        PlayPlayerSlash(true);
        yield return new WaitForSeconds(0.12f);
    }

    // ---------- VFX triggers ----------

    private void PlayPlayerSlash(bool riposte = false)
    {
        if (vfx == null) { if (logDebug) Debug.LogWarning("[VFX] VFXManager not assigned."); return; }
        if (enemy == null) { if (logDebug) Debug.LogWarning("[VFX] Enemy missing."); return; }

        Vector3 from = playerMuzzle != null ? playerMuzzle.position : player.transform.position + Vector3.right * 0.35f;
        Vector3 to = enemy.transform.position + Vector3.left * 0.20f;

        // Slash enemy; VFXManager will flash enemy & shake camera
        vfx.SlashSprite(from, to, enemyRenderer);
    }

    private void PlayEnemySlash()
    {
        if (vfx == null) { if (logDebug) Debug.LogWarning("[VFX] VFXManager not assigned."); return; }
        if (player == null) { if (logDebug) Debug.LogWarning("[VFX] Player missing."); return; }

        Vector3 from = enemyMuzzle != null ? enemyMuzzle.position : enemy.transform.position + Vector3.left * 0.35f;
        Vector3 to = player.transform.position + Vector3.right * 0.20f;

        // Slash player; VFXManager will flash player & shake camera
        vfx.SlashSprite(from, to, playerRenderer);
    }
}
