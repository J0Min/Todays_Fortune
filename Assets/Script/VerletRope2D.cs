using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lightweight 2D rope made from Verlet-simulated points and repeated sprite segments.
/// Put this on an empty GameObject, assign a horizontal rope sprite, then assign anchors.
/// </summary>
public class VerletRope2D : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField, Tooltip("로프의 시작점을 고정할 오브젝트입니다. 비워 두면 Start Position을 사용합니다.")] private Transform startAnchor;
    [SerializeField, Tooltip("로프의 끝점을 연결하거나 드래그할 오브젝트입니다. 비워 두면 End Position을 사용합니다.")] private Transform endAnchor;
    [SerializeField, Tooltip("Start Anchor가 없을 때 사용할 시작 위치입니다. 로프 오브젝트 기준 로컬 좌표입니다.")] private Vector2 startPosition = new Vector2(-3f, 1f);
    [SerializeField, Tooltip("End Anchor가 없을 때 사용할 끝 위치입니다. 로프 오브젝트 기준 로컬 좌표입니다.")] private Vector2 endPosition = new Vector2(3f, 1f);

    [Header("Rope Shape")]
    [SerializeField, Min(2), Tooltip("로프를 나눌 구간 수입니다. 높을수록 더 부드럽게 휘지만 연산량이 늘어납니다.")] private int segmentCount = 16;
    [SerializeField, Min(0.01f), Tooltip("로프의 전체 물리 길이입니다.")] private float ropeLength = 6f;
    [SerializeField, Tooltip("끝 앵커가 로프 길이를 벗어나지 않도록 제한합니다. 켜면 로프가 과하게 늘어나 보이지 않습니다.")]
    private bool clampEndAnchorToRopeLength = true;
    [SerializeField, Range(0f, 1f), Tooltip("Rope Length보다 추가로 허용할 최대 거리 비율입니다. 0.25면 최대 25% 더 멀리 이동할 수 있습니다.")]
    private float maxStretchRatio = 0f;
    [SerializeField, Range(0f, 1f), Tooltip("시작점과 끝점 사이에 유지할 최소 거리 비율입니다. 높을수록 로프의 늘어짐이 줄어듭니다.")]
    private float minEndDistanceRatio = 0.9f;
    [SerializeField, Tooltip("End Anchor에 DragAnchor2D가 있을 때, 마우스를 놓으면 끝점을 고정 해제하고 로프 물리에 맡깁니다.")]
    private bool releaseEndAnchorOnMouseUp = true;
    [SerializeField, Range(1, 30), Tooltip("한 물리 프레임에서 줄 길이 제약을 계산하는 횟수입니다. 높을수록 탄탄해지지만 연산량이 증가합니다.")] private int constraintIterations = 10;
    [SerializeField, Tooltip("로프 각 점에 적용할 중력 방향과 세기입니다.")] private Vector2 gravity = new Vector2(0f, -12f);
    [SerializeField, Range(0f, 0.5f), Tooltip("점의 이동 속도를 줄이는 정도입니다. 높을수록 흔들림이 빨리 멈춥니다.")] private float damping = 0.02f;

    [Header("Bend Limit")]
    [SerializeField, Range(0f, 180f), Tooltip("서로 이웃한 줄 구간이 꺾일 수 있는 최대 각도입니다. 0이면 직선으로 유지되고, 180이면 각도 제한이 사실상 없습니다.")]
    private float maxBendAngle = 180f;
    [SerializeField, Range(1, 8), Tooltip("각도 제한 시 함께 이동시킬 점의 개수입니다. 값이 클수록 꺾임이 넓게 분산되어 더 부드러운 곡선이 됩니다.")]
    private int bendInfluencePoints = 3;

    [Header("Visual")]
    [Tooltip("로프의 각 구간에 반복해서 표시할 스프라이트 이미지입니다.")]
    [SerializeField] private Sprite ropeSprite;
    [Tooltip("스프라이트가 좌우가 아니라 아래에서 위 방향으로 그려져 있으면 켭니다.")]
    [SerializeField] private bool spriteIsVertical = true;
    [SerializeField, Min(0.01f), Tooltip("로프 스프라이트의 두께 배율입니다. 로프가 두꺼워 보이면 낮추세요.")]
    private float thickness = 0.35f;
    [SerializeField, Range(1f, 2f), Tooltip("이웃한 스프라이트를 겹치는 정도입니다. 꺾이는 부분의 틈을 가리는 데 사용합니다.")]
    private float segmentOverlap = 1.25f;
    [SerializeField, Tooltip("로프 스프라이트를 그릴 Sorting Layer입니다.")] private string sortingLayerName = "Default";
    [SerializeField, Tooltip("같은 Sorting Layer 안에서의 표시 순서입니다. 값이 클수록 앞에 그려집니다.")] private int sortingOrder = 5;
    [SerializeField, Tooltip("로프 스프라이트에 적용할 색상입니다.")] private Color ropeColor = Color.white;

    private readonly List<RopePoint> points = new List<RopePoint>();
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<Vector2> overlapSourcePoints = new List<Vector2>();
    private readonly List<float> overlapSourceDistances = new List<float>();
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

        Vector2 requestedEndPosition = GetEndPosition();
        if (endAnchor != null)
        {
            DragAnchor2D dragAnchor = endAnchor.GetComponent<DragAnchor2D>();
            if (dragAnchor != null && dragAnchor.IsDragging)
                requestedEndPosition = dragAnchor.CurrentMouseWorldPosition;
        }

        SetPinnedPoint(0, GetStartPosition());
        if (TrySolveSegmentOverlap(requestedEndPosition))
            return;

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
        int iterations = Mathf.Max(1, constraintIterations);
        float bendCorrectionStrength = Mathf.Clamp01(4f / iterations);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            PinAnchors();
            SolveLengthConstraints();
            PinAnchors();
            SolveBendConstraints(bendCorrectionStrength);
        }

        PinAnchors();
        SolveLengthConstraints();
        PinAnchors();
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

    private void SolveBendConstraints(float correctionStrength)
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
                ApplyDistributedBendCorrection(
                    i + 1,
                    1,
                    (targetPosition - next.position) * correctionStrength);
            }
            else if (!previous.pinned)
            {
                Vector2 limitedIncoming = Rotate(outgoing / outgoingLength, -limitedTurnAngle) * incomingLength;
                Vector2 targetPosition = current.position - limitedIncoming;
                ApplyDistributedBendCorrection(
                    i - 1,
                    -1,
                    (targetPosition - previous.position) * correctionStrength);
            }
        }
    }

    private void ApplyDistributedBendCorrection(int firstIndex, int direction, Vector2 correction)
    {
        int count = Mathf.Max(1, bendInfluencePoints);
        float totalWeight = count * (count + 1) * 0.5f;

        for (int offset = 0; offset < count; offset++)
        {
            int pointIndex = firstIndex + direction * offset;
            if (pointIndex < 0 || pointIndex >= points.Count)
                break;

            RopePoint point = points[pointIndex];
            if (point.pinned)
                break;

            float weight = (count - offset) / totalWeight;
            Vector2 weightedCorrection = correction * weight;
            point.position += weightedCorrection;
            point.previousPosition += weightedCorrection;
            points[pointIndex] = point;
        }
    }

    private bool TrySolveSegmentOverlap(Vector2 requestedEndPosition)
    {
        if (!ShouldPinEndAnchor() || points.Count < 2)
            return false;

        DragAnchor2D dragAnchor = endAnchor != null
            ? endAnchor.GetComponent<DragAnchor2D>()
            : null;
        if (dragAnchor != null && !dragAnchor.HasMovedSinceBeginDrag)
            return false;

        CaptureOverlapSourcePath();

        float snapDistance = Mathf.Max(segmentLength * 0.35f, 0.02f);
        float bestDistanceSquared = snapDistance * snapDistance;
        float overlapDistance = 0f;
        Vector2 snappedEnd = requestedEndPosition;
        bool foundOverlap = false;
        int lastSnapSegment = Mathf.Max(0, points.Count - 3);

        for (int i = 0; i < lastSnapSegment; i++)
        {
            Vector2 from = overlapSourcePoints[i];
            Vector2 to = overlapSourcePoints[i + 1];
            Vector2 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.0001f)
                continue;

            float t = Mathf.Clamp01(Vector2.Dot(requestedEndPosition - from, segment) / lengthSquared);
            Vector2 closestPoint = from + segment * t;
            float distanceSquared = (requestedEndPosition - closestPoint).sqrMagnitude;
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            snappedEnd = closestPoint;
            overlapDistance = overlapSourceDistances[i] + Mathf.Sqrt(lengthSquared) * t;
            foundOverlap = true;
        }

        if (!foundOverlap)
            return false;

        float totalLength = overlapSourceDistances[overlapSourceDistances.Count - 1];
        if (totalLength < 0.0001f || totalLength - overlapDistance < segmentLength)
            return false;

        float foldDistance = (totalLength + overlapDistance) * 0.5f;

        for (int i = 0; i < points.Count; i++)
        {
            float distanceAlongRope = totalLength * i / (points.Count - 1);
            float sourceDistance = distanceAlongRope <= foldDistance
                ? distanceAlongRope
                : foldDistance - (distanceAlongRope - foldDistance);
            Vector2 position = SampleOverlapSourcePath(sourceDistance);

            RopePoint point = points[i];
            point.position = position;
            point.previousPosition = position;
            points[i] = point;
        }

        SetPinnedPoint(0, GetStartPosition());
        SetPinnedPoint(points.Count - 1, snappedEnd);
        endAnchor.position = new Vector3(snappedEnd.x, snappedEnd.y, endAnchor.position.z);
        return true;
    }

    private void CaptureOverlapSourcePath()
    {
        overlapSourcePoints.Clear();
        overlapSourceDistances.Clear();

        float distance = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0)
                distance += Vector2.Distance(points[i - 1].position, points[i].position);

            overlapSourcePoints.Add(points[i].position);
            overlapSourceDistances.Add(distance);
        }
    }

    private Vector2 SampleOverlapSourcePath(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, overlapSourceDistances[overlapSourceDistances.Count - 1]);

        for (int i = 0; i < overlapSourceDistances.Count - 1; i++)
        {
            float fromDistance = overlapSourceDistances[i];
            float toDistance = overlapSourceDistances[i + 1];
            if (distance > toDistance)
                continue;

            float length = toDistance - fromDistance;
            float t = length > 0.0001f ? (distance - fromDistance) / length : 0f;
            return Vector2.Lerp(overlapSourcePoints[i], overlapSourcePoints[i + 1], t);
        }

        return overlapSourcePoints[overlapSourcePoints.Count - 1];
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
        Vector2 end = GetEndPosition();
        if (!clampEndAnchorToRopeLength) return end;

        Vector2 offset = end - start;
        float minimumReach = ropeLength * Mathf.Min(minEndDistanceRatio, 1f + maxStretchRatio);
        float maximumReach = ropeLength * (1f + maxStretchRatio);
        float distanceSquared = offset.sqrMagnitude;

        if (distanceSquared < minimumReach * minimumReach)
        {
            Vector2 direction = distanceSquared > 0.0001f
                ? offset.normalized
                : GetFallbackEndDirection(start);
            Vector2 minimumClampedEnd = start + direction * minimumReach;
            endAnchor.position = new Vector3(minimumClampedEnd.x, minimumClampedEnd.y, endAnchor.position.z);
            return minimumClampedEnd;
        }

        if (distanceSquared <= maximumReach * maximumReach) return end;

        Vector2 clampedEnd = start + offset.normalized * maximumReach;
        endAnchor.position = new Vector3(clampedEnd.x, clampedEnd.y, endAnchor.position.z);
        return clampedEnd;
    }

    private Vector2 GetFallbackEndDirection(Vector2 start)
    {
        Vector2 initialOffset = (Vector2)transform.TransformPoint(endPosition) - start;
        return initialOffset.sqrMagnitude > 0.0001f ? initialOffset.normalized : Vector2.down;
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
