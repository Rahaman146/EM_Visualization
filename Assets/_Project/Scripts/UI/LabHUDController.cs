using UnityEngine;
using UnityEngine.UI;

public class LabHUDController : MonoBehaviour
{
    public ChargeSpawnManager spawnManager;
    public VectorArrowVisualizer arrowVisualizer;
    public PresetManager presetManager;

    public Button addPositiveButton;
    public Button addNegativeButton;
    public Button toggleArrowsButton;
    public Button clearAllButton;
    public Dropdown presetDropdown;
    public Text activeChargeLabel;

    private void Awake()
    {
        ResolveReferencesIfMissing();

        if (spawnManager == null) spawnManager = FindObjectOfType<ChargeSpawnManager>();
        if (arrowVisualizer == null) arrowVisualizer = FindObjectOfType<VectorArrowVisualizer>();
        if (presetManager == null) presetManager = FindObjectOfType<PresetManager>();

        if (addPositiveButton != null)
        {
            addPositiveButton.onClick.RemoveAllListeners();
            addPositiveButton.onClick.AddListener(() => spawnManager?.SpawnCharge(new Vector3(0f, 0.75f, 0f), ChargeType.Positive));
        }

        if (addNegativeButton != null)
        {
            addNegativeButton.onClick.RemoveAllListeners();
            addNegativeButton.onClick.AddListener(() => spawnManager?.SpawnCharge(new Vector3(0.2f, 0.75f, 0f), ChargeType.Negative));
        }

        if (toggleArrowsButton != null)
        {
            toggleArrowsButton.onClick.RemoveAllListeners();
            toggleArrowsButton.onClick.AddListener(() => arrowVisualizer?.Toggle());
        }

        if (clearAllButton != null)
        {
            clearAllButton.onClick.RemoveAllListeners();
            clearAllButton.onClick.AddListener(() => spawnManager?.ClearAll());
        }

        if (presetDropdown != null)
        {
            presetDropdown.onValueChanged.RemoveAllListeners();
            presetDropdown.onValueChanged.AddListener(OnPresetSelected);
        }

        if (spawnManager != null)
            spawnManager.OnChargesChanged += RefreshLabel;

        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (spawnManager != null) spawnManager.OnChargesChanged -= RefreshLabel;
    }

    private void OnPresetSelected(int index)
    {
        if (presetDropdown == null || presetManager == null) return;
        string option = presetDropdown.options[index].text;
        presetManager.LoadPreset(option);
    }

    private void RefreshLabel()
    {
        if (activeChargeLabel != null && spawnManager != null)
        {
            activeChargeLabel.text = $"Active Charges: {spawnManager.activeCharges.Count}";
        }
    }

    private void ResolveReferencesIfMissing()
    {
        if (addPositiveButton == null) addPositiveButton = FindButtonByName("Add+");
        if (addNegativeButton == null) addNegativeButton = FindButtonByName("Add-");
        if (toggleArrowsButton == null) toggleArrowsButton = FindButtonByName("Toggle");
        if (clearAllButton == null) clearAllButton = FindButtonByName("Clear");

        if (presetDropdown == null)
        {
            presetDropdown = GetComponentInChildren<Dropdown>(true);
        }

        if (activeChargeLabel == null)
        {
            var labelTransform = FindDeepChild(transform, "ActiveChargesLabel");
            if (labelTransform != null)
            {
                activeChargeLabel = labelTransform.GetComponent<Text>();
            }
            if (activeChargeLabel == null)
            {
                activeChargeLabel = GetComponentInChildren<Text>(true);
            }
        }
    }

    private Button FindButtonByName(string name)
    {
        var child = FindDeepChild(transform, name);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName) return child;
            var found = FindDeepChild(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
