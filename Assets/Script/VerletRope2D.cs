using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lightweight 2D rope made from Verlet-simulated points and repeated sprite segments.
/// Put this on an empty GameObject, assign a horizontal rope sprite, then assign anchors.
/// </summary>
public class VerletRope2D : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;
    [SerializeField] private Vector2 startPosition = new Vector2(-3f, 1f);
    [SerializeField] private Vector2 endPosition = new Vector2(3f, 1f);

    [Header("Rope Shape")]
    [SerializeField, Min(2)] private int segmentCount = 16;
    [SerializeField, Min(0.01f)] private float ropeLength = 6f;
    [SerializeField, Tooltip("Keeps the End Anchor within Rope Length of the Start Anchor so the rope cannot visually stretch.")]
    private bool clampEndAnchorToRopeLength = true;
    [SerializeField, Range(0f, 1f), Tooltip("Extra reach allowed as a fraction of Rope Length. 0.25 allows the rope to stretch 25% farther.")]
    private float maxStretchRatio = 0f;
    [SerializeField, Tooltip("When the End Anchor has DragAnchor2D, release it into rope physics after the mouse button is released.")]
    private bool releaseEndAnchorOnMouseUp = true;
    [SerializeField, Range(1, 30)] private int constraintIterations = 10;
    [SerializeField] private Vector2 gravity = new Vector2(0f, -12f);
    [SerializeField, Range(0f, 0.2f)] private float damping = 0.02f;

    [Header("Visual")]
    [Tooltip("The image used repeatedly for each rope section.")]
    [SerializeField] private Sprite ropeSprite;
    [Tooltip("Enable when the rope image is drawn bottom-to-top instead of left-to-right.")]
    [SerializeField] private bool spriteIsVertical = true;
    [SerializeField, Min(0.01f), Tooltip("Visual width multiplier. Lower this when the rope looks too thick.")]
    private float thickness = 0.35f;
    [SerializeField, Range(1f, 2f), Tooltip("Overlaps neighbouring rope images to hide visible seams at bends.")]
    private float segmentOverlap = 1.25f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 5;
    [SerializeField] private Color ropeColor = Color.white;

    private readonly List<RopePoint> points = new List<RopePoint>();
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private float segmentLength;
    private bool initialized;

    private struct RopePoint
    {
        public Vector2 position;
        public Vector2 previousPosition;
        public bool pinned;

        public RopePoint(Vector2 position, bool pinned)
        {
            this.position = position;
            previousPosition = position;
            this.pinned = pinned;
        }
    }

    private void OnEnable()
    {
        BuildRope();
    }

    private void OnDisable()
    {
        initialized = false;
    }

    private void FixedUpdate()
    {
        if (!initialized) BuildRope();
        if (!initialized) return;

        PinAnchors();
        Simulate(Time.fixedDeltaTime);
        SolveConstraints();
        SyncReleasedEndAnchor();
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        UpdateVisuals();
    }

    [ContextMenu("Rebuild Rope")]
    public void BuildRope()
    {
        ClearVisuals();
        points.Clear();

        segmentCount = Mathf.Max(2, segmentCount);
        segmentLength = ropeLength / segmentCount;
        Vector2 start = GetStartPosition();
        Vector2 end = GetEndPosition();

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            points.Add(new RopePoint(Vector2.Lerp(start, end, t), i == 0));
        }

        if (ropeSprite != null)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                var segment = new GameObject($"Rope Segment {i + 1}");
                segment.transform.SetParent(transform, false);
                var spriteRenderer = segment.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = ropeSprite;
                spriteRenderer.color = ropeColor;
                spriteRenderer.sortingLayerName = sortingLayerName;
                spriteRenderer.sortingOrder = sortingOrder;
                renderers.Add(spriteRenderer);
            }
        }

        initialized = true;
        PinAnchors();
        SolveConstraints();
        UpdateVisuals();
    }

    private void Simulate(float deltaTime)
    {
        float dt = Mathf.Min(deltaTime, 0.033f);
        float dtSquared = dt * dt;

        for (int i = 1; i < points.Count; i++)
        {
            RopePoint point = points[i];
            if (point.pinned) continue;

            Vector2 velocity = (point.position - point.previousPosition) * (1f - damping);
            point.previousPosition = point.position;
            point.position += velocity + gravity * dtSquared;
            points[i] = point;
        }
    }

    private void SolveConstraints()
    {
        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            PinAnchors();
            for (int i = 0; i < points.Count - 1; i++)
            {
                RopePoint first = points[i];
                RopePoint second = points[i + 1];
                Vector2 delta = second.position - first.position;
                float distance = delta.magnitude;
                if (distance < 0.0001f) continue;

                Vector2 correction = delta * ((distance - segmentLength) / distance);
                if (first.pinned)
                    second.position -= correction;
                else if (second.pinned)
                    first.position += correction;
                else
                {
                    first.position += correction * 0.5f;
                    second.position -= correction * 0.5f;
                }
                points[i] = first;
                points[i + 1] = second;
            }
        }
    }

    private void PinAnchors()
    {
        if (points.Count == 0) return;

        Vector2 start = GetStartPosition();
        SetPinnedPoint(0, start);
        if (ShouldPinEndAnchor())
            SetPinnedPoint(points.Count - 1, GetClampedEndAnchorPosition(start));
        else
            SetEndPointPinned(false);
    }

    private bool ShouldPinEndAnchor()
    {
        if (endAnchor == null) return false;
        if (!releaseEndAnchorOnMouseUp) return true;

        DragAnchor2D dragAnchor = endAnchor.GetComponent<DragAnchor2D>();
        return dragAnchor == null || dragAnchor.IsDragging;
    }

    private void SetEndPointPinned(bool pinned)
    {
        int lastIndex = points.Count - 1;
        RopePoint point = points[lastIndex];
        point.pinned = pinned;
        points[lastIndex] = point;
    }

    private void SyncReleasedEndAnchor()
    {
        if (endAnchor == null || ShouldPinEndAnchor()) return;

        Vector2 endPosition = points[points.Count - 1].position;
        endAnchor.position = new Vector3(endPosition.x, endPosition.y, endAnchor.position.z);
    }

    private Vector2 GetClampedEndAnchorPosition(Vector2 start)
    {
        Vector2 end = endAnchor.position;
        if (!clampEndAnchorToRopeLength) return end;

        Vector2 offset = end - start;
        float maximumReach = ropeLength * (1f + maxStretchRatio);
        if (offset.sqrMagnitude <= maximumReach * maximumReach) return end;

        Vector2 clampedEnd = start + offset.normalized * maximumReach;
        endAnchor.position = new Vector3(clampedEnd.x, clampedEnd.y, endAnchor.position.z);
        return clampedEnd;
    }

    private void SetPinnedPoint(int index, Vector2 position)
    {
        RopePoint point = points[index];
        point.position = position;
        point.previousPosition = position;
        point.pinned = true;
        points[index] = point;
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            Vector2 from = points[i].position;
            Vector2 to = points[i + 1].position;
            Vector2 direction = to - from;
            SpriteRenderer spriteRenderer = renderers[i];

            spriteRenderer.transform.position = (from + to) * 0.5f;
            float directionAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f,
                spriteIsVertical ? directionAngle - 90f : directionAngle);

            float spriteLength = spriteIsVertical ? ropeSprite.bounds.size.y : ropeSprite.bounds.size.x;
            float lengthScale = spriteLength > 0.0001f
                ? (direction.magnitude / spriteLength) * segmentOverlap
                : 1f;
            spriteRenderer.transform.localScale = spriteIsVertical
                ? new Vector3(thickness, lengthScale, 1f)
                : new Vector3(lengthScale, thickness, 1f);
        }
    }

    private Vector2 GetStartPosition() => startAnchor != null ? startAnchor.position : (Vector2)transform.TransformPoint(startPosition);
    private Vector2 GetEndPosition() => endAnchor != null ? endAnchor.position : (Vector2)transform.TransformPoint(endPosition);

    private void ClearVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Rope Segment "))
                Destroy(child.gameObject);
        }
        renderers.Clear();
    }
}
