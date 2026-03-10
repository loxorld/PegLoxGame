using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MapGraphConnectionsGraphic : MaskableGraphic
{
    public readonly struct Segment
    {
        public Segment(Vector2 start, Vector2 end, Color color, float thickness)
        {
            Start = start;
            End = end;
            Color = color;
            Thickness = Mathf.Max(1f, thickness);
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
        public Color Color { get; }
        public float Thickness { get; }
    }

    private readonly List<Segment> segments = new();

    public void SetSegments(IReadOnlyList<Segment> sourceSegments)
    {
        segments.Clear();
        if (sourceSegments != null)
        {
            for (int i = 0; i < sourceSegments.Count; i++)
                segments.Add(sourceSegments[i]);
        }

        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        for (int i = 0; i < segments.Count; i++)
            AddSegment(vh, segments[i]);
    }

    private static void AddSegment(VertexHelper vh, Segment segment)
    {
        Vector2 direction = segment.End - segment.Start;
        float length = direction.magnitude;
        if (length <= 0.01f)
            return;

        direction /= length;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (segment.Thickness * 0.5f);

        var vertex = UIVertex.simpleVert;
        vertex.color = segment.Color;

        int vertexStart = vh.currentVertCount;

        vertex.position = segment.Start - normal;
        vh.AddVert(vertex);

        vertex.position = segment.Start + normal;
        vh.AddVert(vertex);

        vertex.position = segment.End + normal;
        vh.AddVert(vertex);

        vertex.position = segment.End - normal;
        vh.AddVert(vertex);

        vh.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
        vh.AddTriangle(vertexStart, vertexStart + 2, vertexStart + 3);
    }
}
