using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The shared settings screen: audio sliders plus save slots. The same prefab is
// used in the menu and in battle - the save buttons simply disable themselves
// when there's no run in progress to capture.
//
// Recomeçar goes back to the newest save, or to the very beginning when there
// are none. Deleting every save puts the game back at the beginning too.
public class SettingsPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Audio")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text musicValueLabel;
    [SerializeField] private TMP_Text sfxValueLabel;

    [Header("Save slots")]
    [Tooltip("One authored row - Label / SaveBtn / LoadBtn / DeleteBtn - cloned " +
             "once per slot. Hand-authoring ten of these would just be ten things " +
             "to keep in sync. Left disabled; the clones are what you see.")]
    [SerializeField] private RectTransform slotTemplate;
    [Tooltip("Scroll view content the rows are built into.")]
    [SerializeField] private RectTransform slotContainer;

    [Header("Reset")]
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text resetLabel;
    [Tooltip("Seconds the reset button stays armed after the first press. " +
             "Wiping every save on a single misclick would be unkind.")]
    [SerializeField] private float resetConfirmWindow = 3f;

    [Header("Restart")]
    [Tooltip("Back to the newest save, or to the very beginning when there is " +
             "none. Mid-battle it asks for a second press, since it throws away " +
             "anything not saved.")]
    [SerializeField] private Button restartButton;
    [SerializeField] private TMP_Text restartLabel;

    private class SlotRow
    {
        public GameObject root;
        public TMP_Text label;
        public Button save, load, delete;
    }

    private readonly System.Collections.Generic.List<SlotRow> _rows =
        new System.Collections.Generic.List<SlotRow>();

    private float _resetArmedUntil;

    private float _restartArmedUntil;

    [Header("Battle scene")]
    [Tooltip("Build index of the battle scene, used when loading a save from the menu.")]
    [SerializeField] private int battleSceneBuildIndex = 1;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }

        BuildSlotRows();

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetPressed);
        }

        if (restartButton != null) restartButton.onClick.AddListener(OnRestartPressed);

        panelRoot.SetActive(false);
    }

    public void Open()
    {
        panelRoot.SetActive(true);
        RefreshAll();
    }

    public void Close()
    {
        GameSettings.Save();
        panelRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (panelRoot.activeSelf) Close(); else Open();
    }

    private void RefreshAll()
    {
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        UpdateVolumeLabels();
        RefreshSlots();
    }

    private void UpdateVolumeLabels()
    {
        if (musicValueLabel != null) musicValueLabel.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
        if (sfxValueLabel != null) sfxValueLabel.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
    }

    private void OnMusicChanged(float v) { GameSettings.MusicVolume = v; UpdateVolumeLabels(); }
    private void OnSfxChanged(float v)   { GameSettings.SfxVolume = v;   UpdateVolumeLabels(); }

    /// One row per slot, cloned from the authored template.
    private void BuildSlotRows()
    {
        if (slotTemplate == null) return;

        RectTransform parent = slotContainer != null ? slotContainer : (RectTransform)slotTemplate.parent;
        slotTemplate.gameObject.SetActive(false);

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int slot = i;                                   // capture per iteration
            var go = Instantiate(slotTemplate.gameObject, parent);
            go.name = "Slot" + i;
            go.SetActive(true);

            var row = new SlotRow
            {
                root   = go,
                label  = FindIn<TMP_Text>(go.transform, "Label"),
                save   = FindIn<Button>(go.transform, "SaveBtn"),
                load   = FindIn<Button>(go.transform, "LoadBtn"),
                delete = FindIn<Button>(go.transform, "DeleteBtn")
            };

            if (row.save != null)   row.save.onClick.AddListener(() => SaveToSlot(slot));
            if (row.load != null)   row.load.onClick.AddListener(() => LoadFromSlot(slot));
            if (row.delete != null) row.delete.onClick.AddListener(() => DeleteSlot(slot));

            _rows.Add(row);
        }
    }

    private static T FindIn<T>(Transform root, string childName) where T : Component
    {
        var t = root.Find(childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    private void RefreshSlots()
    {
        bool canSave = SaveSystem.Capture() != null; // only inside a run

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var data = SaveSystem.Load(i);

            if (row.label != null)
                row.label.text = data != null
                    ? Loc.T("settings.slot", i + 1) + "  <size=70%>" + SignProgression.Describe(data.Direction) + "</size>"
                      + "\n<size=70%>" + data.Describe(false) + "</size>"
                    : Loc.T("settings.slot", i + 1) + "\n<size=70%>" + Loc.T("settings.slot_empty") + "</size>";

            if (row.save != null)   row.save.interactable = canSave;
            if (row.load != null)   row.load.interactable = data != null;
            if (row.delete != null) row.delete.interactable = data != null;
        }

        // The size fitter otherwise settles a frame late, so the scroll range is
        // wrong for the first frame the panel is open.
        if (slotContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer);

        DisarmReset();
        DisarmRestart();
    }

    public void DeleteSlot(int slot)
    {
        SaveSystem.Delete(slot);

        // No saves left means back to the beginning: every word is plain
        // Portuguese again. A battle in progress carries on, with its cards
        // redrawn in text.
        if (SaveSystem.UsedSlotCount() == 0)
        {
            SignProgression.ResetAll();
            if (CardManager.instance != null) CardManager.instance.RefreshAllCardText();
        }

        RefreshSlots();
    }

    // Two presses rather than a confirmation dialog: the first arms the button
    // and relabels it, the second wipes. It disarms itself after a few seconds.
    private void OnResetPressed()
    {
        if (Time.unscaledTime > _resetArmedUntil)
        {
            _resetArmedUntil = Time.unscaledTime + Mathf.Max(0.5f, resetConfirmWindow);
            if (resetLabel != null) resetLabel.text = Loc.Signed("settings.confirm");
            return;
        }

        int removed = SaveSystem.DeleteAll();
        Debug.Log("[SettingsPanel] Reset removed " + removed + " save(s).");

        // Everything is gone, so the game goes back to the very beginning. In
        // battle that is a new run; in the menu, the next one will be.
        if (CombatManager.instance != null)
        {
            GameSettings.Save();
            SaveSystem.StartNewRun(battleSceneBuildIndex);
            return;
        }

        SignProgression.ResetAll();
        RefreshSlots();
    }

    private void OnRestartPressed()
    {
        bool inBattle = CombatManager.instance != null;
        if (inBattle && Time.unscaledTime > _restartArmedUntil)
        {
            _restartArmedUntil = Time.unscaledTime + Mathf.Max(0.5f, resetConfirmWindow);
            if (restartLabel != null) restartLabel.text = Loc.Signed("settings.confirm");
            return;
        }

        GameSettings.Save();
        SaveSystem.RestartFromLatest(battleSceneBuildIndex);
    }

    // Says where Recomeçar will take the player before they press it.
    private void DisarmRestart()
    {
        _restartArmedUntil = 0f;
        if (restartLabel == null) return;

        string from = Loc.T(SaveSystem.UsedSlotCount() > 0 ? "settings.restart_from_save" : "settings.restart_from_start");
        restartLabel.text = Loc.Signed("settings.restart") + "\n<size=62%>" + from + "</size>";
    }

    private void DisarmReset()
    {
        SignText.UsePlainSigns(resetLabel);
        SignText.UsePlainSigns(restartLabel);
        _resetArmedUntil = 0f;
        if (resetLabel != null) resetLabel.text = Loc.Signed("settings.delete_all");
        if (resetButton != null) resetButton.interactable = SaveSystem.UsedSlotCount() > 0;
    }

    private void Update()
    {
        // let the armed state lapse on its own
        if (_resetArmedUntil > 0f && Time.unscaledTime > _resetArmedUntil) DisarmReset();
        if (_restartArmedUntil > 0f && Time.unscaledTime > _restartArmedUntil) DisarmRestart();
    }

    public void SaveToSlot(int slot)
    {
        var data = SaveSystem.Capture();
        if (data == null)
        {
            Debug.LogWarning("[SettingsPanel] Nothing to save outside a battle.");
            return;
        }
        SaveSystem.Write(slot, data);
        RefreshSlots();
    }

    public void LoadFromSlot(int slot)
    {
        var data = SaveSystem.Load(slot);
        if (data == null) return;

        // Always through a fresh battle scene, even from inside a battle: the
        // hand, the draw pile and the enemy all have to be rebuilt from the save.
        GameSettings.Save();
        SaveSystem.LoadIntoBattle(data, battleSceneBuildIndex);
    }
}
