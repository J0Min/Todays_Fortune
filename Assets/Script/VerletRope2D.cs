using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lightweight 2D rope made from Verlet-simulated points and repeated sprite segments.
/// Put this on an empty GameObject, assign a horizontal rope sprite, then assign anchors.
/// </summary>
public class VerletRope2D : MonoBehaviour
{
    private const float BendCorrectionRatio = 0.25f;

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
    [SerializeField, Range(0f, 0.5f)] private float damping = 0.02f;

    [Header("Bend Limit")]
    [SerializeField, Range(0f, 180f), Tooltip("Maximum direction change allowed between adjacent rope segments. 0 keeps segments straight; 180 applies no practical limit.")]
    private float maxBendAngle = 180f;

    [Header("Visual")]
    [Tooltip("The image used repeatedly for each rope section.")]
    [SerializeField] private Sprite ropeSprite;
    [Tooltip("Optional sprite used only for the segment connected to the End Anchor. Uses Rope Sprite when empty.")]
    [SerializeField] private Sprite endSprite;
    [Tooltip("Local X/Y scale multiplier applied only to the final rope segment.")]
    [SerializeField] private Vector2 endScale = Vector2.one;
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
    private readonly List<Vector2> idleShape = new List<Vector2>();
    private readonly List<Vector2> returnStartShape = new List<Vector2>();
    private DragAnchor2D endDragAnchor;
    private float segmentLength;
    private bool initialized;
    private bool interactionInProgress;
    private bool endWasDragging;
    private bool endWasReturning;

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

        bool endIsDragging = endDragAnchor != null && endDragAnchor.IsDragging;
        bool endIsReturning = endDragAnchor != null && endDragAnchor.IsReturning;

        if (endIsDragging && !endWasDragging && !interactionInProgress)
            interactionInProgress = true;

        if (endIsReturning && !endWasReturning)
            CaptureShape(returnStartShape);

        if (endIsReturning)
        {
            ApplyKinematicReturn(endDragAnchor.ReturnProgress);
            endWasDragging = endIsDragging;
            endWasReturning = true;
            return;
        }

        if (endWasReturning)
        {
            if (endIsDragging)
                SynchronizePreviousPositions();
            else
            {
                ApplyKinematicReturn(1f);
                interactionInProgress = false;
                CaptureShape(idleShape);
                endWasDragging = false;
                endWasReturning = false;
                return;
            }
        }

        endWasDragging = endIsDragging;
        endWasReturning = false;

        PinAnchors();
        Simulate(Time.fixedDeltaTime);
        SolveConstraints();

        if (!ArePointPositionsFinite())
        {
            Debug.LogWarning("[VerletRope2D] Invalid rope position detected. Rebuilding rope.", this);
            BuildRope();
            return;
        }

        SyncReleasedEndAnchor();

        if (!interactionInProgress && !endIsDragging)
            CaptureShape(idleShape);
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
        idleShape.Clear();
        returnStartShape.Clear();
        CacheEndDragAnchor();
        interactionInProgress = false;
        endWasDragging = endDragAnchor != null && endDragAnchor.IsDragging;
        endWasReturning = endDragAnchor != null && endDragAnchor.IsReturning;

        segmentCount = Mathf.Max(2, segmentCount);
        segmentLength = ropeLength / segmentCount;
        Vector2 start = GetStartPosition();
        Vector2 end = GetEndPosition();

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            points.Add(new RopePoint(Vector2.Lerp(start, end, t), i == 0));
        }

        CaptureShape(idleShape);

        if (ropeSprite != null)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                var segment = new GameObject($"Rope Segment {i + 1}");
                segment.transform.SetParent(transform, false);
                var spriteRenderer = segment.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = i == segmentCount - 1 && endSprite != null
                    ? endSprite
                    : ropeSprite;
                spriteRenderer.color = ropeColor;
                spriteRenderer.sortingLayerName = sortingLayerName;
                spriteRenderer.sortingOrder = sortingOrder + (i == segmentCount - 1 ? 1 : 0);
                renderers.Add(spriteRenderer);
            }
        }

        initialized = true;
        PinAnchors();
        SolveConstraints();
        UpdateVisuals();
    }

    public Vector3 StartAnchorPosition => startAnchor != null ? startAnchor.position : transform.position;
    public Vector3 EndAnchorPosition => endAnchor != null ? endAnchor.position : transform.position;

    public void SetStartAnchorPosition(Vector3 worldPosition)
    {
        if (startAnchor == null)
            return;

        startAnchor.position = new Vector3(worldPosition.x, worldPosition.y, startAnchor.position.z);
    }

    /// <summary>
    /// Applies supplied rope visuals and segment count, then rebuilds the generated segments.
    /// </summary>
    public void ApplySelectionVisuals(Sprite bodySprite, Sprite headSprite, int newSegmentCount)
    {
        if (bodySprite != null)
            ropeSprite = bodySprite;
        if (headSprite != null)
            endSprite = headSprite;
        segmentCount = Mathf.Max(2, newSegmentCount);

        BuildRope();
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
            SolveLengthConstraints();
            SolveBendConstraints();
        }
    }

    private void SolveLengthConstraints()
    {
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

    private void SolveBendConstraints()
    {
        for (int i = 1; i < points.Count - 1; i++)
        {
            RopePoint previous = points[i - 1];
            RopePoint current = points[i];
            RopePoint next = points[i + 1];
            Vector2 incoming = current.position - previous.position;
            Vector2 outgoing = next.position - current.position;
            float incomingLength = incoming.magnitude;
            float outgoingLength = outgoing.magnitude;

            if (!IsFinite(incoming) || !IsFinite(outgoing) ||
                incomingLength < 0.0001f || outgoingLength < 0.0001f)
                continue;

            float turnAngle = Vector2.SignedAngle(incoming, outgoing);
            if (Mathf.Abs(turnAngle) <= maxBendAngle)
                continue;

            float limitedTurnAngle = Mathf.Clamp(turnAngle, -maxBendAngle, maxBendAngle);
            if (!next.pinned)
            {
                Vector2 limitedOutgoing = Rotate(incoming / incomingLength, limitedTurnAngle) * outgoingLength;
                Vector2 targetPosition = current.position + limitedOutgoing;
                next.position = Vector2.Lerp(next.position, targetPosition, BendCorrectionRatio);
                points[i + 1] = next;
            }
            else if (!previous.pinned)
            {
                Vector2 limitedIncoming = Rotate(outgoing / outgoingLength, -limitedTurnAngle) * incomingLength;
                Vector2 targetPosition = current.position - limitedIncoming;
                previous.position = Vector2.Lerp(previous.position, targetPosition, BendCorrectionRatio);
                points[i - 1] = previous;
            }
        }
    }

    private static Vector2 Rotate(Vector2 vector, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angleRadians);
        float sine = Mathf.Sin(angleRadians);
        return new Vector2(
            vector.x * cosine - vector.y * sine,
            vector.x * sine + vector.y * cosine);
    }

    private bool ArePointPositionsFinite()
    {
        foreach (RopePoint point in points)
        {
            if (!IsFinite(point.position) || !IsFinite(point.previousPosition))
                return false;
        }

        return true;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y);
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

        return endDragAnchor == null || endDragAnchor.IsDragging;
    }

    private void CacheEndDragAnchor()
    {
        endDragAnchor = endAnchor != null
            ? endAnchor.GetComponent<DragAnchor2D>()
            : null;
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
        Vector2 end = GetEndPosition();
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

    private void CaptureShape(List<Vector2> target)
    {
        target.Clear();
        for (int i = 0; i < points.Count; i++)
            target.Add(points[i].position);
    }

    private void ApplyKinematicReturn(float progress)
    {
        if (returnStartShape.Count != points.Count || idleShape.Count != points.Count)
            return;

        float t = Mathf.Clamp01(progress);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 position = Vector2.LerpUnclamped(returnStartShape[i], idleShape[i], t);
            RopePoint point = points[i];
            point.position = position;
            point.previousPosition = position;
            point.pinned = i == 0 || i == points.Count - 1;
            points[i] = point;
        }

        Vector2 endPosition = points[points.Count - 1].position;
        endAnchor.position = new Vector3(endPosition.x, endPosition.y, endAnchor.position.z);
    }

    private void SynchronizePreviousPositions()
    {
        for (int i = 0; i < points.Count; i++)
        {
            RopePoint point = points[i];
            point.previousPosition = point.position;
            points[i] = point;
        }
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
                spriteIsVertical ? directionAngle + 90f : directionAngle);

            Sprite segmentSprite = spriteRenderer.sprite;
            float spriteLength = spriteIsVertical
                ? segmentSprite.bounds.size.y
                : segmentSprite.bounds.size.x;
            float lengthScale = spriteLength > 0.0001f
                ? (direction.magnitude / spriteLength) * segmentOverlap
                : 1f;
            Vector3 visualScale = spriteIsVertical
                ? new Vector3(thickness, lengthScale, 1f)
                : new Vector3(lengthScale, thickness, 1f);

            if (i == renderers.Count - 1)
            {
                visualScale.x *= endScale.x;
                visualScale.y *= endScale.y;
            }

            spriteRenderer.transform.localScale = visualScale;
        }
    }

    private Vector2 GetStartPosition() => GetValidAnchorPosition(startAnchor, startPosition);
    private Vector2 GetEndPosition() => GetValidAnchorPosition(endAnchor, endPosition);

    private Vector2 GetValidAnchorPosition(Transform anchor, Vector2 fallbackLocalPosition)
    {
        Vector2 fallbackPosition = transform.TransformPoint(fallbackLocalPosition);
        if (anchor == null || !IsFinite(anchor.position))
        {
            if (anchor != null)
                anchor.position = new Vector3(fallbackPosition.x, fallbackPosition.y, transform.position.z);

            return fallbackPosition;
        }

        return anchor.position;
    }

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
