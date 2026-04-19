using System;
using System.Collections.Generic;
using UnityEngine;

public class ChargeSpawnManager : MonoBehaviour
{
    public GameObject positiveChargePrefab;
    public GameObject negativeChargePrefab;
    public List<ChargeObject> activeCharges = new List<ChargeObject>();
    public event Action OnChargesChanged;

    public void SpawnCharge(Vector3 pos, ChargeType type)
    {
        var prefab = type == ChargeType.Positive ? positiveChargePrefab : negativeChargePrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"Missing prefab for {type} charge.");
            return;
        }

        var go = Instantiate(prefab, pos, Quaternion.identity);
        var charge = go.GetComponent<ChargeObject>();
        if (charge == null)
        {
            Debug.LogError("Spawned charge prefab has no ChargeObject component.");
            Destroy(go);
            return;
        }

        charge.OnMoved += NotifyChargesChanged;
        activeCharges.Add(charge);
        NotifyChargesChanged();
    }

    public void RemoveCharge(ChargeObject charge)
    {
        if (charge == null)
        {
            return;
        }

        charge.OnMoved -= NotifyChargesChanged;
        activeCharges.Remove(charge);
        Destroy(charge.gameObject);
        NotifyChargesChanged();
    }

    public void ClearAll()
    {
        foreach (var charge in activeCharges)
        {
            if (charge != null)
            {
                Destroy(charge.gameObject);
            }
        }

        activeCharges.Clear();
        NotifyChargesChanged();
    }

    private void NotifyChargesChanged()
    {
        OnChargesChanged?.Invoke();
    }
}
