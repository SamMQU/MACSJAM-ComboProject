using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RadialOptionMenuManager : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private TurnBattleController battle;
    [SerializeField] private TBPlayerCharacter player;

    [Tooltip("Order should match the radial options: Attack, Ability1, Ability2, Skip, etc.")]
    [SerializeField] private int[] apCosts; // e.g. [0, 2, 3, 0]

    private Image[][] optionImages; // all Image comps per option
    private TMP_Text[] optionTexts;

    private void Awake()
    {
        // Cache children
        int count = transform.childCount;
        optionImages = new Image[count][];
        optionTexts = new TMP_Text[count];

        for (int i = 0; i < count; i++)
        {
            var option = transform.GetChild(i);

            // collect all Images under this option
            optionImages[i] = option.GetComponentsInChildren<Image>(true);

            // assume only one TMP_Text under this option
            optionTexts[i] = option.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void Update()
    {
        RefreshOptions();
    }

    private void RefreshOptions()
    {
        if (battle == null || player == null) return;

        bool isPlayerTurn = battle.Phase == BattlePhase.PlayerTurn;

        for (int i = 0; i < optionImages.Length; i++)
        {
            int cost = (apCosts != null && i < apCosts.Length) ? apCosts[i] : 0;
            bool canAfford = player.CurrentAP >= cost;

            bool enable = isPlayerTurn && canAfford;

            // Grey out
            Color tint = enable ? Color.white : new Color(1f, 1f, 1f, 0.35f);

            foreach (var img in optionImages[i])
                if (img) img.color = tint;

            if (optionTexts[i]) optionTexts[i].color = tint;
        }
    }
}
