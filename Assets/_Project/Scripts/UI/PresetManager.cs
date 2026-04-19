using UnityEngine;

public class PresetManager : MonoBehaviour
{
    public ChargeSpawnManager spawnManager;

    private void Awake()
    {
        if (spawnManager == null)
        {
            spawnManager = FindObjectOfType<ChargeSpawnManager>();
        }
    }

    public void LoadPreset(string name)
    {
        if (spawnManager == null) return;

        spawnManager.ClearAll();
        switch (name)
        {
            case "Single Charge":
                spawnManager.SpawnCharge(Vector3.zero, ChargeType.Positive);
                break;
            case "Dipole":
                spawnManager.SpawnCharge(new Vector3(-0.3f, 0, 0), ChargeType.Positive);
                spawnManager.SpawnCharge(new Vector3(0.3f, 0, 0), ChargeType.Negative);
                break;
            case "Quadrupole":
                spawnManager.SpawnCharge(new Vector3(-0.3f, 0, -0.3f), ChargeType.Positive);
                spawnManager.SpawnCharge(new Vector3(0.3f, 0, -0.3f), ChargeType.Negative);
                spawnManager.SpawnCharge(new Vector3(-0.3f, 0, 0.3f), ChargeType.Negative);
                spawnManager.SpawnCharge(new Vector3(0.3f, 0, 0.3f), ChargeType.Positive);
                break;
            case "Three Random":
                for (int i = 0; i < 3; i++)
                {
                    spawnManager.SpawnCharge(
                        new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)),
                        i % 2 == 0 ? ChargeType.Positive : ChargeType.Negative);
                }
                break;
        }
    }
}
