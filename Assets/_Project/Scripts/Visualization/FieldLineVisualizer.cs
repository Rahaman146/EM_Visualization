using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FieldLineVisualizer : MonoBehaviour
{
    public int linesPerCharge = 64;
    public float step = 0.05f;
    public int maxSteps = 200;
    public Material fieldLineMaterial;

    private ChargeSpawnManager spawnManager;
    private ElectricFieldComputer fieldComputer;
    private readonly List<GameObject> lineObjects = new List<GameObject>();
    private BoxCollider boundsCollider;

    private void Start()
    {
        spawnManager = FindObjectOfType<ChargeSpawnManager>();
        fieldComputer = FindObjectOfType<ElectricFieldComputer>();
        boundsCollider = gameObject.GetComponent<BoxCollider>();
        if (boundsCollider == null)
        {
            boundsCollider = gameObject.AddComponent<BoxCollider>();
        }

        if (boundsCollider == null)
        {
            Debug.LogWarning("FieldLineVisualizer could not create BoxCollider; disabling visualizer.");
            enabled = false;
            return;
        }
        boundsCollider.isTrigger = true;

        if (spawnManager != null)
        {
            spawnManager.OnChargesChanged += RebuildLines;
        }

        RebuildLines();
    }

    private void OnDestroy()
    {
        if (spawnManager != null)
        {
            spawnManager.OnChargesChanged -= RebuildLines;
        }
    }

    private void RebuildLines()
    {
        foreach (var line in lineObjects)
        {
            if (line != null) Destroy(line);
        }
        lineObjects.Clear();

        if (spawnManager == null || fieldComputer == null) return;

        Bounds totalBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        bool hasBounds = false;

        foreach (var charge in spawnManager.activeCharges)
        {
            if (charge == null) continue;

            for (int i = 0; i < linesPerCharge; i++)
            {
                Vector3 dir = FibonacciDirection(i, linesPerCharge);
                Vector3 start = charge.transform.position + dir * 0.07f;
                List<Vector3> points = TraceFieldLine(start);
                if (points.Count < 2) continue;

                GameObject lineGo = new GameObject($"FieldLine_{charge.chargeType}_{i}");
                lineGo.transform.SetParent(transform, true);
                var lr = lineGo.AddComponent<LineRenderer>();
                lr.material = fieldLineMaterial;
                lr.widthMultiplier = 0.005f;
                lr.useWorldSpace = true;
                lr.positionCount = points.Count;
                lr.SetPositions(points.ToArray());
                lr.numCapVertices = 4;

                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(1f, 0.8f, 0f, 1f), 0f),
                        new GradientColorKey(new Color(0f, 1f, 1f, 0.3f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.3f, 1f)
                    });
                lr.colorGradient = gradient;

                lineObjects.Add(lineGo);

                foreach (var p in points)
                {
                    if (!hasBounds)
                    {
                        totalBounds = new Bounds(p, Vector3.one * 0.05f);
                        hasBounds = true;
                    }
                    else
                    {
                        totalBounds.Encapsulate(p);
                    }
                }
            }
        }

        if (hasBounds && boundsCollider != null)
        {
            boundsCollider.center = transform.InverseTransformPoint(totalBounds.center);
            boundsCollider.size = totalBounds.size;
        }
    }

    private List<Vector3> TraceFieldLine(Vector3 start)
    {
        var points = new List<Vector3> { start };
        Vector3 p = start;

        for (int i = 0; i < maxSteps; i++)
        {
            Vector3 e = fieldComputer.ComputeFieldAt(p);
            if (e.magnitude < 1e-3f) break;

            if (IsNearAnyCharge(p, 0.05f)) break;

            Vector3 k1 = Direction(e);
            Vector3 k2 = Direction(fieldComputer.ComputeFieldAt(p + 0.5f * step * k1));
            Vector3 k3 = Direction(fieldComputer.ComputeFieldAt(p + 0.5f * step * k2));
            Vector3 k4 = Direction(fieldComputer.ComputeFieldAt(p + step * k3));

            Vector3 delta = (step / 6f) * (k1 + 2f * k2 + 2f * k3 + k4);
            p += delta;
            points.Add(p);
        }

        return points;
    }

    private bool IsNearAnyCharge(Vector3 point, float minDist)
    {
        if (spawnManager == null) return false;
        foreach (var charge in spawnManager.activeCharges)
        {
            if (charge == null) continue;
            if (Vector3.Distance(point, charge.transform.position) < minDist)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 Direction(Vector3 v)
    {
        if (v.sqrMagnitude < 1e-8f) return Vector3.zero;
        return v.normalized;
    }

    private static Vector3 FibonacciDirection(int i, int n)
    {
        float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
        float m = i + 0.5f;
        float cosTheta = 1f - 2f * m / n;
        float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
        float angle = 2f * Mathf.PI * m / phi;
        return new Vector3(Mathf.Cos(angle) * sinTheta, cosTheta, Mathf.Sin(angle) * sinTheta).normalized;
    }
}
