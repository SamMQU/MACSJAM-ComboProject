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

    // Active/current enemy (runtime)
    [SerializeField] private TBEnemyCharacter enemy;

    [Header("Enemy Waves (3 total)")]
    [SerializeField] private TBEnemyCharacter[] enemyPrefabs = new TBEnemyCharacter[3];
    [SerializeField] private Transform enemySpawnPoint;
    private int enemyIndex = -1; // will increment on spawn

    [Header("Parry QTE")]
    [SerializeField] private ParryQTEController parryQTE;
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

    [Header("UI Bindings")]
    [SerializeField] private HeartBarBinder enemyHeartBinder; // HUD binder for the current enemy

    [Header("Combo")]
    [SerializeField] private ComboSystem combo;

    [Header("VFX")]
    [SerializeField] private VFXManager vfx;
    [SerializeField] private Renderer playerRenderer;        // target for enemy hit flashes
    [SerializeField] private Renderer enemyRenderer;         // target for player hit flashes (updated on spawn)
    [SerializeField] private Transform playerMuzzle;         // optional spawn origin
    [SerializeField] private Transform enemyMuzzle;          // optional spawn origin
    [Tooltip("Fallback VFX for Player base attack (if no prefab on ability).")]
    [SerializeField] private GameObject playerBaseVFXPrefab;
    [Tooltip("Fallback VFX for Enemy base attack (if no prefab on ability).")]
    [SerializeField] private GameObject enemyBaseVFXPrefab;

    [Header("UI (simple panels)")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BattlePhase phase = BattlePhase.None;
    private System.Random rng;
    private bool waitingForPlayerChoice;
    private int pendingEnemyDamage;
    private TBEnemyCharacter.EnemyAction currentEnemyAction;

    public BattlePhase Phase => phase;

    // ------------- Unity -------------

    private void Awake()
    {
        if (rng == null)
            rng = new System.Random(unchecked(System.Environment.TickCount * 397) ^ System.Guid.NewGuid().GetHashCode());
        if (parryQTE == null) parryQTE = FindObjectOfType<ParryQTEController>();
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
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
        // Spawn first enemy of the wave set
        yield return SpawnNextEnemy();
        if (phase == BattlePhase.Victory || enemy == null) yield break;

        phase = BattlePhase.PlayerTurn;
        waitingForPlayerChoice = true;
        // if (logDebug) Debug.Log("[Battle] Start: Player Turn");
    }

    private void Update()
    {
        if (phase == BattlePhase.PlayerTurn && waitingForPlayerChoice)
            HandlePlayerInput();
    }

    // ------------- Enemy Waves -------------

    private IEnumerator SpawnNextEnemy()
    {
        // destroy old enemy instance (if any)
        if (enemy != null)
        {
            Destroy(enemy.gameObject);
            enemy = null;
            enemyRenderer = null;
            yield return null;
        }

        enemyIndex++;
        if (enemyPrefabs == null || enemyIndex >= enemyPrefabs.Length)
        {
            // No more enemies -> victory
            phase = BattlePhase.Victory;
            ShowVictoryUI();
            // if (logDebug) Debug.Log("[Battle] Victory! All enemies defeated.");
            yield break;
        }

        var prefab = enemyPrefabs[enemyIndex];
        if (prefab == null)
        {
            // if (logDebug) Debug.LogError($"[Battle] enemyPrefabs[{enemyIndex}] is null.");
            phase = BattlePhase.Defeat; // abort cleanly
            ShowDefeatUI();
            yield break;
        }

        Vector3 pos = enemySpawnPoint ? enemySpawnPoint.position : Vector3.zero;
        enemy = Instantiate(prefab, pos, Quaternion.identity);

        // cache renderer for hit flashes / sorting
        enemyRenderer = enemy.GetComponentInChildren<SpriteRenderer>();

        // Rebind HUD enemy heart bar (immediate + deferred refresh)
        if (enemyHeartBinder == null)
            enemyHeartBinder = FindObjectOfType<HeartBarBinder>(true); // best-effort auto-find

        if (enemyHeartBinder != null)
        {
            enemyHeartBinder.BindCharacter(enemy);
            enemyHeartBinder.ForceRefresh();
            StartCoroutine(RefreshEnemyHPNextFrame()); // covers late-enabling canvases
        }

        // if (logDebug) Debug.Log($"[Battle] Spawned enemy {enemyIndex + 1}/{enemyPrefabs.Length}: {enemy.DisplayName}");
    }

    private IEnumerator RefreshEnemyHPNextFrame()
    {
        yield return null; // wait 1 frame
        if (enemyHeartBinder != null && enemy != null)
            enemyHeartBinder.ForceRefresh();
    }

    private IEnumerator HandleEnemyDeathThenMaybeNext()
    {
        // tiny delay so the last VFX reads
        yield return new WaitForSeconds(0.12f);

        // Spawn the next enemy or finish with victory
        yield return SpawnNextEnemy();

        if (phase == BattlePhase.Victory) yield break;

        // Back to player turn
        phase = BattlePhase.PlayerTurn;
        waitingForPlayerChoice = true;
    }

    // ------------- Player Turn -------------

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
            // if (logDebug) Debug.Log("[Player] No such ability slot.");
            return;
        }

        var ab = abilities[idx];
        if (ab == null)
        {
            // if (logDebug) Debug.Log("[Player] Ability slot is empty.");
            return;
        }

        if (!player.SpendAP(ab.APCost))
        {
            // if (logDebug) Debug.Log($"[Player] Not enough AP for {ab.Name} (cost {ab.APCost}).");
            return;
        }

        waitingForPlayerChoice = false;
        StartCoroutine(DoPlayerAbility(ab));
    }

    private IEnumerator DoPlayerBaseAttack()
    {
        waitingForPlayerChoice = false;

        // VFX (player base)
        yield return PlayPlayerVFX(playerBaseVFXPrefab);

        // Damage with combo
        int raw = player.BaseAttackDamage;
        int final = combo != null ? combo.ApplyMultiplier(raw) : raw;
        enemy.TakeDamage(final);
        combo?.AddDamageContribution(final);

        if (enemy.IsDead) { yield return HandleEnemyDeathThenMaybeNext(); yield break; }
        yield return StartEnemyTurn();
    }

    private IEnumerator DoPlayerAbility(PlayerAbility ab)
    {
        // VFX (per-ability)
        yield return PlayPlayerVFX(ab.VFXPrefab);

        // Damage with combo
        int rawDmg = ab.Damage;
        int finalDmg = combo != null ? combo.ApplyMultiplier(rawDmg) : rawDmg;

        if (finalDmg > 0)
        {
            enemy.TakeDamage(finalDmg);
            combo?.AddDamageContribution(finalDmg);
        }

        // Healing
        if (ab.HealAmount > 0)
        {
            player.Heal(ab.HealAmount);
            // if (logDebug) Debug.Log($"[Player] Healed {ab.HealAmount} HP with {ab.Name}");
        }

        if (enemy.IsDead)
        {
            yield return HandleEnemyDeathThenMaybeNext();
            yield break;
        }

        yield return StartEnemyTurn();
    }


    private IEnumerator DoPlayerSkipTurn()
    {
        yield return null;
        yield return StartEnemyTurn();
    }

    // ------------- Enemy Turn (QTE → resolve) -------------

    private IEnumerator StartEnemyTurn()
    {
        phase = BattlePhase.EnemyTelegraph;

        // Decide action
        currentEnemyAction = enemy.PickAction(rng, enemyBaseAttackWeight);
        pendingEnemyDamage = (currentEnemyAction.kind == TBEnemyCharacter.EnemyActionKind.Base)
            ? enemy.BaseAttackDamage
            : (currentEnemyAction.ability != null ? currentEnemyAction.ability.Damage : enemy.BaseAttackDamage);

        if (logDebug)
        {
            string label = currentEnemyAction.kind == TBEnemyCharacter.EnemyActionKind.Base
                ? "Base Attack"
                : $"Ability: {currentEnemyAction.ability.Name}";
            // Debug.Log($"[Enemy] Telegraphing: {label} -> {pendingEnemyDamage} dmg");
        }

        // Telegraph (brief)
        yield return new WaitForSeconds(0.2f);

        // QTE gate (wait until finished)
        lastParryResult = null;
        if (parryQTE != null)
        {
            qteActive = true;
            parryQTE.StartQTE();
            yield return new WaitUntil(() => !qteActive);
        }
        else
        {
            // if (logDebug) Debug.LogWarning("[Battle] No ParryQTEController assigned. Treating as FAIL.");
            lastParryResult = new QTEResult { success = false, quality = QTEHitQuality.Fail };
        }

        var result = lastParryResult ?? new QTEResult { success = false, quality = QTEHitQuality.Fail };

        switch (result.quality)
        {
            default:
            case QTEHitQuality.Fail:
                // Enemy hits -> VFX then damage
                yield return PlayEnemyVFXForCurrentAction();
                player.TakeDamage(pendingEnemyDamage);
                combo?.ResetOnPlayerHit();

                if (player.IsDead) { phase = BattlePhase.Defeat; ShowDefeatUI(); yield break; }
                yield return BackToPlayer();
                break;

            case QTEHitQuality.Success:
                // Negate damage, gain AP
                player.GainAP(player.APOnParrySuccess);
                yield return BackToPlayer();
                break;

            case QTEHitQuality.Perfect:
                // Negate + riposte
                player.GainAP(player.APOnParryPerfect);

                // Player riposte VFX (use playerBaseVFXPrefab as fallback)
                yield return PlayPlayerVFX(playerBaseVFXPrefab);

                int raw = player.RiposteDamage;
                int final = combo != null ? combo.ApplyMultiplier(raw) : raw;
                enemy.TakeDamage(final);
                combo?.AddDamageContribution(final);

                if (enemy.IsDead) { yield return HandleEnemyDeathThenMaybeNext(); yield break; }
                yield return BackToPlayer();
                break;
        }
    }

    private void HandleParryFinished(QTEResult result)
    {
        lastParryResult = result;
        qteActive = false;
    }

    private IEnumerator BackToPlayer()
    {
        pendingEnemyDamage = 0;
        phase = BattlePhase.PlayerTurn;
        waitingForPlayerChoice = true;
        yield break;
    }

    // ------------- VFX helpers -------------

    private IEnumerator PlayPlayerVFX(GameObject prefabOverride)
    {
        if (vfx != null && enemy != null)
        {
            Vector3 from = playerMuzzle ? playerMuzzle.position : player.transform.position + Vector3.right * 0.35f;
            Vector3 to = enemy.transform.position + Vector3.left * 0.20f;

            if (prefabOverride != null)
            {
                // Place directly on the enemy (impact-style)
                vfx.PlayCustomVFXAtTarget(prefabOverride, enemyRenderer);
            }
            else
            {
                // Fallback slash, placed at the enemy target
                vfx.SlashSpriteWithPlacement(from, to, enemyRenderer, VFXManager.SlashPlaceMode.AtTarget);
            }
        }

        // keep timing consistent for readability
        yield return new WaitForSeconds(0.12f);
    }

    private IEnumerator PlayEnemyVFXForCurrentAction()
    {
        if (vfx != null && player != null)
        {
            Vector3 from = enemyMuzzle ? enemyMuzzle.position : enemy.transform.position + Vector3.left * 0.35f;
            Vector3 to = player.transform.position + Vector3.right * 0.20f;

            GameObject prefab = null;
            if (currentEnemyAction.kind == TBEnemyCharacter.EnemyActionKind.Base)
            {
                prefab = enemyBaseVFXPrefab;
            }
            else if (currentEnemyAction.ability != null)
            {
                prefab = currentEnemyAction.ability.VFXPrefab;
            }

            if (prefab != null)
            {
                vfx.PlayCustomVFXAtTarget(prefab, playerRenderer);
            }
            else
            {
                vfx.SlashSpriteWithPlacement(from, to, playerRenderer, VFXManager.SlashPlaceMode.AtTarget);
            }
        }

        yield return new WaitForSeconds(0.12f);
    }

    // ------------- UI -------------

    private void ShowVictoryUI()
    {
        if (victoryPanel) victoryPanel.SetActive(true);
    }

    private void ShowDefeatUI()
    {
        if (defeatPanel) defeatPanel.SetActive(true);
    }
}
