using UnityEngine;
using UnityEngine.UI;

public class ComboUIVisualizer : MonoBehaviour
{
    [Header("Bind")]
    [SerializeField] private ComboSystem combo;

    [Header("Letter Outlines (D,C,B,A,S)")]
    [SerializeField] private Image[] outlineImages = new Image[5];

    [Header("Letter Fills (D,C,B,A,S) — Image Type MUST be 'Filled'")]
    [SerializeField] private Image[] fillImages = new Image[5];

    [Header("Appearance")]
    [SerializeField] private Color outlineInactive = new Color(1, 1, 1, 0.2f);
    [SerializeField] private Color outlineActive = new Color(1, 1, 1, 1f);
    [SerializeField] private Color fillColor = new Color(1f, 0.9f, 0.3f, 1f);

    private void OnEnable()
    {
        if (combo == null) combo = FindObjectOfType<ComboSystem>();
        Wire(true);
        FullRefresh();
    }

    private void OnDisable()
    {
        Wire(false);
    }

    private void Wire(bool sub)
    {
        if (combo == null) return;
        if (sub)
        {
            combo.OnStageChanged += HandleStageChanged;
            combo.OnStageProgressChanged += HandleProgress;
        }
        else
        {
            combo.OnStageChanged -= HandleStageChanged;
            combo.OnStageProgressChanged -= HandleProgress;
        }
    }

    private void FullRefresh()
    {
        if (combo == null) return;
        var stageIndex = (int)combo.CurrentStage;
        for (int i = 0; i < outlineImages.Length; i++)
        {
            if (outlineImages[i] != null)
                outlineImages[i].color = (i == stageIndex) ? outlineActive : outlineInactive;

            if (fillImages[i] != null)
            {
                fillImages[i].color = fillColor;
                fillImages[i].fillAmount = (i == stageIndex) ? combo.CurrentStageFill01 : 0f;
            }
        }
    }

    private void HandleStageChanged(ComboSystem.Stage stage)
    {
        // Switch active outline
        int idx = (int)stage;
        for (int i = 0; i < outlineImages.Length; i++)
            if (outlineImages[i] != null)
                outlineImages[i].color = (i == idx) ? outlineActive : outlineInactive;

        // Clear all fills except current
        for (int i = 0; i < fillImages.Length; i++)
            if (fillImages[i] != null)
                fillImages[i].fillAmount = (i == idx) ? 0f : 0f; // current starts at 0
    }

    private void HandleProgress(ComboSystem.Stage stage, float t)
    {
        int idx = (int)stage;
        if (idx >= 0 && idx < fillImages.Length && fillImages[idx] != null)
        {
            fillImages[idx].color = fillColor;
            fillImages[idx].fillAmount = Mathf.Clamp01(t);
        }
    }
}
