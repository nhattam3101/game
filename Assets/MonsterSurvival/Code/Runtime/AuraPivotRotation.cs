using UnityEngine;

public class AuraPivotRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 25f;
    [SerializeField] private bool rotateClockwise = true;

    private void Update()
    {
        float direction = rotateClockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, rotationSpeed * direction * Time.deltaTime);
    }
}