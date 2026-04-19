using System.Collections.Generic;
using UnityEngine;

public class VectorArrowVisualizer : MonoBehaviour
{
    public bool isVisible = true;
    public float spacing = 0.25f;
    public int gridSize = 8;
    public GameObject arrowPrefab;
    public Material arrowMaterial;

    private ChargeSpawnManager spawnManager;
    private ElectricFieldComputer fieldComputer;
    private readonly List<Transform> arrows = new List<Transform>();

    private void Start()
    {
        spawnManager = FindObjectOfType<ChargeSpawnManager>();
        fieldComputer = FindObjectOfType<ElectricFieldComputer>();

        BuildArrows();

        if (spawnManager != null)
        {
            spawnManager.OnChargesChanged += UpdateArrows;
        }

        UpdateArrows();
    }

    private void OnDestroy()
    {
        if (spawnManager != null)
        {
            spawnManager.OnChargesChanged -= UpdateArrows;
        }
    }

    public void Toggle()
    {
        isVisible = !isVisible;
        foreach (var arrow in arrows)
        {
            if (arrow != null) arrow.gameObject.SetActive(isVisible);
        }
    }

    private void BuildArrows()
    {
        float half = (gridSize - 1) * spacing * 0.5f;
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x * spacing - half, y * spacing - half, z * spacing - half);
                    GameObject arrow = arrowPrefab != null ? Instantiate(arrowPrefab, transform) : CreateArrowPrimitive();
                    arrow.transform.localPosition = pos;
                    arrow.transform.localRotation = Quaternion.identity;
                    arrows.Add(arrow.transform);
                }
            }
        }
    }

    private GameObject CreateArrowPrimitive()
    {
        var root = new GameObject("Arrow");
        root.transform.SetParent(transform, false);

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);
        shaft.transform.localPosition = new Vector3(0f, 0.12f, 0f);

        var tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tip.name = "Tip";
        tip.transform.SetParent(root.transform, false);
        tip.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        tip.transform.localPosition = new Vector3(0f, 0.27f, 0f);

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (arrowMaterial != null) mr.sharedMaterial = arrowMaterial;
        }

        return root;
    }

    private void UpdateArrows()
    {
        if (fieldComputer == null) return;

        for (int i = 0; i < arrows.Count; i++)
        {
            Transform arrow = arrows[i];
            if (arrow == null) continue;
            arrow.gameObject.SetActive(isVisible);

            Vector3 worldPos = arrow.position;
            Vector3 field = fieldComputer.ComputeFieldAt(worldPos);
            float magnitude = field.magnitude;
            float scale = Mathf.Max(0.01f, Mathf.Log(1f + magnitude / 1000f));

            if (field.sqrMagnitude > 1e-8f)
            {
                arrow.rotation = Quaternion.LookRotation(field.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            }

            arrow.localScale = Vector3.one * scale;

            float t = Mathf.Clamp01(magnitude / 10000f);
            Color c = Color.HSVToRGB(Mathf.Lerp(0.55f, 0.12f, t), 1f, Mathf.Lerp(0.4f, 1f, t));

            foreach (var mr in arrow.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr == null) continue;
                if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", c);
                if (mr.material.HasProperty("_Color")) mr.material.SetColor("_Color", c);
                if (mr.material.HasProperty("_EmissionColor")) mr.material.SetColor("_EmissionColor", c * 0.5f);
            }
        }
    }
}
