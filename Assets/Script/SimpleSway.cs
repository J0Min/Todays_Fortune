using UnityEngine;

/// <summary>
/// Applies a small, non-accumulating sine-wave rotation around a transform's pivot.
/// </summary>
public sealed class SimpleSway : MonoBehaviour
{
    [SerializeField, Min(0f)] private float angle = 2f;
    [SerializeField, Min(0.01f)] private float periodSeconds = 3f;
    [SerializeField, Range(0f, 360f)] private float phaseOffset;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    private Quaternion initialLocalRotation;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        initialLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        Vector3 axis = rotationAxis.sqrMagnitude > 0f
            ? rotationAxis.normalized
            : Vector3.forward;
        float phaseRadians = phaseOffset * Mathf.Deg2Rad;
        float timeRadians = Time.time * Mathf.PI * 2f / Mathf.Max(0.01f, periodSeconds);
        float currentAngle = Mathf.Sin(timeRadians + phaseRadians) * angle;

        transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(currentAngle, axis);
    }

    private void OnDisable()
    {
        transform.localRotation = initialLocalRotation;
    }
}
