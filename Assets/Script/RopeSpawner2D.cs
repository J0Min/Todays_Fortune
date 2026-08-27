using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public sealed class RopeSpawner2D : MonoBehaviour
{
    [SerializeField] private GameObject ropePrefab;
    [SerializeField, Min(1)] private int ropeCount = PlayerFortuneState.DefaultRopeIdCount;
    [SerializeField, Min(0f)] private float horizontalSpacing = 3.31f;
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;

    [Header("Rope Selection / Common")]
    [SerializeField, Min(0f)] private float selectionDuration = 0.35f;
    [SerializeField] private AnimationCurve selectionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Scale applied to the selected rope during the selection animation.")]
    private Vector3 selectedRopeScale = Vector3.one;
    [SerializeField, Min(2), Tooltip("Segment count applied to the selected rope before its selection movement.")]
    private int selectedRopeSegmentCount = 30;
    [SerializeField, Tooltip("Body sprite applied to the selected rope before its selection movement. Leave empty to keep the current sprite.")]
    private Sprite selectedRopeBodySprite;
    [SerializeField, Tooltip("Head sprite applied to the selected rope before its selection movement. Leave empty to keep the current sprite.")]
    private Sprite selectedRopeHeadSprite;

    [Header("Rope Selection / Pivot")]
    [SerializeField, Tooltip("Moves the selected rope's start anchor to Selected Rope Pivot.")]
    private bool moveSelectedRopeToPivot = true;
    [SerializeField, Tooltip("The selected rope's start anchor moves to this position after confirmation.")]
    private Transform selectedRopePivot;

    [Header("Rope Selection / Camera")]
    [SerializeField, Tooltip("Moves the selection camera to the selected rope's end anchor.")]
    private bool moveCameraToSelectedRopeEnd;
    [SerializeField, Tooltip("Camera moved when Move Camera To Selected Rope End is enabled. Uses Main Camera when empty.")]
    private Camera selectionCamera;
    [SerializeField, Tooltip("World-space offset added to the selected rope end when moving the camera.")]
    private Vector2 selectionCameraOffset = Vector2.zero;
    [SerializeField] private UnityEvent onRopeSelectionFinished;

    private readonly List<GameObject> spawnedRopes = new();
    private Coroutine selectionRoutine;
    private bool ropeSelected;

    private void Reset()
    {
        selectionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void OnEnable()
    {
        PlayerFortuneState state = PlayerFortuneState.Instance;
        if (state != null)
        {
            ropeCount = state.RopeIdCount;
        }
    }

    private void Start()
    {
        SpawnRopes();
    }

    public void SpawnRopes()
    {
        ClearSpawnedRopes();
        ropeSelected = false;

        if (ropePrefab == null)
        {
            Debug.LogError("[RopeSpawner2D] Rope Prefab을 등록해주세요.", this);
            return;
        }

        float firstRopeX = -0.5f * (ropeCount - 1) * horizontalSpacing;
        for (int index = 0; index < ropeCount; index++)
        {
            Vector3 localPosition = new Vector3(
                spawnOffset.x + firstRopeX + horizontalSpacing * index,
                spawnOffset.y,
                0f);
            Vector3 worldPosition = transform.TransformPoint(localPosition);
            Quaternion worldRotation = transform.rotation * ropePrefab.transform.localRotation;

            GameObject rope = Instantiate(ropePrefab, worldPosition, worldRotation, transform);
            rope.name = $"Rope {index + 1}";
            spawnedRopes.Add(rope);
        }
    }

    /// <summary>
    /// Connect this to DragManager2D.On Anchor Confirmed. The confirmed rope's
    /// start anchor moves to Selected Rope Pivot, then invokes On Rope Selection Finished.
    /// </summary>
    public void FocusSelectedRope(DragAnchor2D selectedAnchor)
    {
        if (ropeSelected || selectedAnchor == null)
            return;

        VerletRope2D selectedRope = selectedAnchor.GetComponentInParent<VerletRope2D>();
        if (selectedRope == null || !selectedRope.transform.IsChildOf(transform))
            return;
        if (moveSelectedRopeToPivot && selectedRopePivot == null)
            return;

        ropeSelected = true;
        if (selectionRoutine != null)
            StopCoroutine(selectionRoutine);

        selectedRope.ApplySelectionVisuals(
            selectedRopeBodySprite,
            selectedRopeHeadSprite,
            selectedRopeSegmentCount);

        HideUnselectedRopes(selectedRope.gameObject);
        selectionRoutine = StartCoroutine(FocusSelectedRopeRoutine(selectedRope));
    }

    private IEnumerator FocusSelectedRopeRoutine(VerletRope2D selectedRope)
    {
        Vector3 startPosition = selectedRope.StartAnchorPosition;
        Vector3 startScale = selectedRope.transform.localScale;
        Camera cameraToMove = moveCameraToSelectedRopeEnd
            ? selectionCamera != null ? selectionCamera : Camera.main
            : null;
        Vector3 cameraStartPosition = cameraToMove != null
            ? cameraToMove.transform.position
            : Vector3.zero;
        Vector3 ropeEndPosition = selectedRope.EndAnchorPosition;
        Vector3 cameraTargetPosition = new Vector3(
            startPosition.x + selectionCameraOffset.x,
            ropeEndPosition.y + selectionCameraOffset.y,
            cameraStartPosition.z);

        float elapsed = 0f;
        while (elapsed < selectionDuration)
        {
            elapsed += Time.deltaTime;
            float t = selectionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / selectionDuration);
            float easedT = selectionEase == null ? t : selectionEase.Evaluate(t);
            if (moveSelectedRopeToPivot)
            {
                selectedRope.SetStartAnchorPosition(
                    Vector3.Lerp(startPosition, selectedRopePivot.position, easedT));
            }

            selectedRope.transform.localScale = Vector3.Lerp(startScale, selectedRopeScale, easedT);

            if (cameraToMove != null)
                cameraToMove.transform.position = Vector3.Lerp(
                    cameraStartPosition, cameraTargetPosition, easedT);
            yield return null;
        }

        if (moveSelectedRopeToPivot)
            selectedRope.SetStartAnchorPosition(selectedRopePivot.position);

        selectedRope.transform.localScale = selectedRopeScale;

        if (cameraToMove != null)
            cameraToMove.transform.position = cameraTargetPosition;

        selectionRoutine = null;
        onRopeSelectionFinished?.Invoke();
    }

    private void ClearSpawnedRopes()
    {
        foreach (GameObject rope in spawnedRopes)
        {
            if (rope != null)
            {
                Destroy(rope);
            }
        }

        spawnedRopes.Clear();
    }

    private void HideUnselectedRopes(GameObject selectedRope)
    {
        foreach (GameObject rope in spawnedRopes)
        {
            if (rope != null && rope != selectedRope)
                rope.SetActive(false);
        }
    }
}
