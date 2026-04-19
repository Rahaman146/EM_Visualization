using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ElectricFieldComputer : MonoBehaviour
{
    private const float k = 8.99e9f;
    private const int GridSize = 8;
    private const int GridCount = GridSize * GridSize * GridSize;
    private const float GridMin = -1f;
    private const float GridStep = 0.25f;

    private ChargeSpawnManager spawnManager;
    public NativeArray<float3> fieldGrid;
    public bool gridReady = false;

    private NativeArray<float3> chargePositions;
    private NativeArray<float> chargeSignedMagnitudes;

    private void Start()
    {
        spawnManager = FindObjectOfType<ChargeSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.OnChargesChanged += RecomputeGrid;
        }

        fieldGrid = new NativeArray<float3>(GridCount, Allocator.Persistent);
        RecomputeGrid();
    }

    public Vector3 ComputeFieldAt(Vector3 point)
    {
        if (spawnManager == null)
        {
            return Vector3.zero;
        }

        Vector3 total = Vector3.zero;
        foreach (var c in spawnManager.activeCharges)
        {
            if (c == null) continue;
            Vector3 r = point - c.transform.position;
            float dist = r.magnitude;
            if (dist < 0.01f) continue;
            float sign = c.chargeType == ChargeType.Positive ? 1f : -1f;
            total += (k * sign * c.magnitude / (dist * dist)) * r.normalized;
        }

        return total;
    }

    public void RecomputeGrid()
    {
        if (!fieldGrid.IsCreated)
        {
            return;
        }

        gridReady = false;

        var charges = spawnManager != null ? spawnManager.activeCharges : null;
        int chargeCount = charges != null ? charges.Count : 0;

        if (chargePositions.IsCreated) chargePositions.Dispose();
        if (chargeSignedMagnitudes.IsCreated) chargeSignedMagnitudes.Dispose();

        if (chargeCount <= 0)
        {
            for (int i = 0; i < GridCount; i++) fieldGrid[i] = float3.zero;
            gridReady = true;
            return;
        }

        chargePositions = new NativeArray<float3>(chargeCount, Allocator.TempJob);
        chargeSignedMagnitudes = new NativeArray<float>(chargeCount, Allocator.TempJob);

        for (int i = 0; i < chargeCount; i++)
        {
            var c = charges[i];
            chargePositions[i] = c != null ? c.transform.position : float3.zero;
            float sign = c != null && c.chargeType == ChargeType.Positive ? 1f : -1f;
            float magnitude = c != null ? c.magnitude : 0f;
            chargeSignedMagnitudes[i] = sign * magnitude;
        }

        var job = new ComputeFieldGridJob
        {
            k = k,
            gridSize = GridSize,
            gridMin = GridMin,
            gridStep = GridStep,
            chargePositions = chargePositions,
            chargeSignedMagnitudes = chargeSignedMagnitudes,
            output = fieldGrid
        };

        JobHandle handle = job.Schedule(GridCount, 64);
        handle.Complete();

        chargePositions.Dispose();
        chargeSignedMagnitudes.Dispose();
        gridReady = true;
    }

    [BurstCompile]
    private struct ComputeFieldGridJob : IJobParallelFor
    {
        public float k;
        public int gridSize;
        public float gridMin;
        public float gridStep;

        [ReadOnly] public NativeArray<float3> chargePositions;
        [ReadOnly] public NativeArray<float> chargeSignedMagnitudes;
        public NativeArray<float3> output;

        public void Execute(int index)
        {
            int x = index % gridSize;
            int y = (index / gridSize) % gridSize;
            int z = index / (gridSize * gridSize);

            float3 point = new float3(
                gridMin + x * gridStep,
                gridMin + y * gridStep,
                gridMin + z * gridStep
            );

            float3 total = float3.zero;
            for (int i = 0; i < chargePositions.Length; i++)
            {
                float3 r = point - chargePositions[i];
                float dist = math.length(r);
                if (dist < 0.01f) continue;

                float3 dir = r / dist;
                total += (k * chargeSignedMagnitudes[i] / (dist * dist)) * dir;
            }

            output[index] = total;
        }
    }

    private void OnDestroy()
    {
        if (spawnManager != null)
        {
            spawnManager.OnChargesChanged -= RecomputeGrid;
        }

        if (fieldGrid.IsCreated) fieldGrid.Dispose();
        if (chargePositions.IsCreated) chargePositions.Dispose();
        if (chargeSignedMagnitudes.IsCreated) chargeSignedMagnitudes.Dispose();
    }
}
