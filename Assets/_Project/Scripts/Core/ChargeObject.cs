using System;
using UnityEngine;

public enum ChargeType
{
    Positive,
    Negative
}

public class ChargeObject : MonoBehaviour
{
    public ChargeType chargeType = ChargeType.Positive;
    public float magnitude = 1e-9f; // 1 nC
    public event Action OnMoved;

    private void Update()
    {
        if (transform.hasChanged)
        {
            OnMoved?.Invoke();
            transform.hasChanged = false;
        }
    }
}
