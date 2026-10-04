using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

// The TrailRenderer of a flight effect, for every flight of a FlightBatch in one mesh: each flight keeps the
// positions it had over the trail's time, and a Burst job builds camera-facing ribbons from them with the
// TrailRenderer's width curve, colour gradient, texture mode and material. A trail keeps fading for its time
// after its projectile is gone, like a TrailRenderer that stopped emitting. One draw for all trails, no work per
// shot (emitting a trail particle per projectile cost ~30 us each).
public sealed class TrailMesh : System.IDisposable
{
    public const int PointsPerTrail = 8;
    private const int CurveSamples = 32;

    // See ProjectileSystem.WarmUpJobs
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmUpJobs()
    {
        IJobParallelForExtensions.EarlyJobInit<BuildJob>();
    }

    private struct Vertex
    {
        public float3 Position;
        public Color32 Color;
        public float2 Uv;
    }

    public readonly float TrailTime;
    private readonly Material material;
    private readonly float minVertexDistance;
    private readonly bool tile;
    private readonly float widthMultiplier;
    private readonly int layer;

    private NativeArray<float> widthCurve;
    private NativeArray<Color32> colorCurve;
    // Per flight: a ring of recent points (position, game time) and how many are in it
    private NativeArray<float4> points;
    private NativeArray<int> pointCount;
    private NativeArray<int> pointHead;
    private NativeArray<byte> active;
    private NativeArray<int> vertexCount;
    private readonly Mesh mesh;
    private int capacity;

    public TrailMesh(TrailRenderer trail, int flights)
    {
        TrailTime = Mathf.Max(0.001f, trail.time);
        material = trail.sharedMaterial;
        minVertexDistance = trail.minVertexDistance;
        tile = trail.textureMode == LineTextureMode.Tile;
        widthMultiplier = trail.widthMultiplier;
        layer = trail.gameObject.layer;

        widthCurve = new NativeArray<float>(CurveSamples, Allocator.Persistent);
        colorCurve = new NativeArray<Color32>(CurveSamples, Allocator.Persistent);
        AnimationCurve width = trail.widthCurve;
        Gradient color = trail.colorGradient;
        for (int i = 0; i < CurveSamples; i++)
        {
            float t = i / (CurveSamples - 1f);
            widthCurve[i] = width.Evaluate(t) * widthMultiplier;
            // Like TrailRenderer: gradient colours are authored in gamma space
            Color sample = color.Evaluate(t);
            colorCurve[i] = QualitySettings.activeColorSpace == ColorSpace.Linear ? sample.linear : sample;
        }

        mesh = new Mesh { name = trail.name + " (trails)" };
        mesh.MarkDynamic();
        Allocate(flights);
    }

    public void Dispose()
    {
        widthCurve.Dispose();
        colorCurve.Dispose();
        points.Dispose();
        pointCount.Dispose();
        pointHead.Dispose();
        active.Dispose();
        if (vertexCount.IsCreated)
            vertexCount.Dispose();
        Object.Destroy(mesh);
    }

    private void Allocate(int size)
    {
        NativeArray<float4> newPoints = new NativeArray<float4>(size * PointsPerTrail, Allocator.Persistent);
        NativeArray<int> newCount = new NativeArray<int>(size, Allocator.Persistent);
        NativeArray<int> newHead = new NativeArray<int>(size, Allocator.Persistent);
        NativeArray<byte> newActive = new NativeArray<byte>(size, Allocator.Persistent);
        if (points.IsCreated)
        {
            NativeArray<float4>.Copy(points, newPoints, points.Length);
            NativeArray<int>.Copy(pointCount, newCount, pointCount.Length);
            NativeArray<int>.Copy(pointHead, newHead, pointHead.Length);
            NativeArray<byte>.Copy(active, newActive, active.Length);
            points.Dispose();
            pointCount.Dispose();
            pointHead.Dispose();
            active.Dispose();
        }
        points = newPoints;
        pointCount = newCount;
        pointHead = newHead;
        active = newActive;
        capacity = size;
    }

    public void EnsureCapacity(int size)
    {
        if (size > capacity)
            Allocate(Mathf.NextPowerOfTwo(size));
    }

    // A new trail starts at the projectile's muzzle
    public void Begin(int id, Vector3 position, float now)
    {
        pointCount[id] = 1;
        pointHead[id] = 0;
        points[id * PointsPerTrail] = new float4(position, now);
        active[id] = 1;
    }

    // Whether the trail of a finished flight has faded out
    public bool IsFaded(int id, float now)
    {
        if (pointCount[id] == 0)
            return true;
        float newest = points[id * PointsPerTrail + pointHead[id]].w;
        return now - newest > TrailTime;
    }

    public void End(int id)
    {
        active[id] = 0;
        pointCount[id] = 0;
    }

    // Adds the points of moving flights from a job (FlightBatch); valid until the next EnsureCapacity
    public Writer GetWriter()
    {
        return new Writer
        {
            Points = points,
            PointCount = pointCount,
            PointHead = pointHead,
            MinVertexDistanceSq = minVertexDistance * minVertexDistance,
        };
    }

    public struct Writer
    {
        public NativeArray<float4> Points;
        public NativeArray<int> PointCount;
        public NativeArray<int> PointHead;
        public float MinVertexDistanceSq;

        // The flight moved: a new point when it got far enough from the last one (TrailRenderer.minVertexDistance)
        public void Add(int id, float3 position, float now)
        {
            int count = PointCount[id];
            int head = PointHead[id];
            int baseIndex = id * PointsPerTrail;
            float4 last = Points[baseIndex + head];
            if (count > 0 && math.distancesq(last.xyz, position) < MinVertexDistanceSq)
            {
                // Keep the head on the projectile without adding a point
                if (count > 1)
                    Points[baseIndex + head] = new float4(position, now);
                else
                    AddPoint(id, position, now);
                return;
            }
            AddPoint(id, position, now);
        }

        private void AddPoint(int id, float3 position, float now)
        {
            int head = (PointHead[id] + 1) % PointsPerTrail;
            PointHead[id] = head;
            PointCount[id] = math.min(PointCount[id] + 1, PointsPerTrail);
            Points[id * PointsPerTrail + head] = new float4(position, now);
        }
    }

    // Builds and draws all trails; flights up to highestId
    public void Draw(int highestId, float now, Camera camera)
    {
        if (highestId == 0 || camera == null || material == null)
            return;

        int maxVertices = highestId * PointsPerTrail * 2;
        int maxIndices = highestId * (PointsPerTrail - 1) * 6;
        Mesh.MeshDataArray dataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData data = dataArray[0];
        data.SetVertexBufferParams(maxVertices,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
        data.SetIndexBufferParams(maxIndices, IndexFormat.UInt32);

        if (!vertexCount.IsCreated || vertexCount.Length < highestId)
        {
            if (vertexCount.IsCreated)
                vertexCount.Dispose();
            vertexCount = new NativeArray<int>(math.max(highestId, capacity), Allocator.Persistent);
        }

        new BuildJob
        {
            Points = points,
            PointCount = pointCount,
            PointHead = pointHead,
            WidthCurve = widthCurve,
            ColorCurve = colorCurve,
            Vertices = data.GetVertexData<Vertex>(),
            Indices = data.GetIndexData<uint>(),
            VertexCount = vertexCount,
            Camera = camera.transform.position,
            Now = now,
            Time = TrailTime,
            Tile = tile,
        }.Schedule(highestId, 64).Complete();

        data.subMeshCount = 1;
        data.SetSubMesh(0, new SubMeshDescriptor(0, maxIndices), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        Mesh.ApplyAndDisposeWritableMeshData(dataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);

        RenderParams parameters = new RenderParams(material)
        {
            layer = layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };
        Graphics.RenderMesh(parameters, mesh, 0, Matrix4x4.identity);
    }

    // Each flight owns a fixed block of vertices and indices; unused ones collapse to degenerate triangles
    [BurstCompile]
    private struct BuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Points;
        [ReadOnly] public NativeArray<int> PointCount;
        [ReadOnly] public NativeArray<int> PointHead;
        [ReadOnly] public NativeArray<float> WidthCurve;
        [ReadOnly] public NativeArray<Color32> ColorCurve;
        [NativeDisableParallelForRestriction] public NativeArray<Vertex> Vertices;
        // Vertex and index data of one MeshData share a safety handle; they don't overlap
        [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<uint> Indices;
        [NativeDisableParallelForRestriction] public NativeArray<int> VertexCount;
        public float3 Camera;
        public float Now;
        public float Time;
        public bool Tile;

        public void Execute(int id)
        {
            int vertexBase = id * PointsPerTrail * 2;
            int indexBase = id * (PointsPerTrail - 1) * 6;
            int count = PointCount[id];
            int used = 0;

            // Newest first: the head, then older points while they are younger than the trail's time
            float length = 0f;
            float3 previous = default;
            for (int k = 0; k < count; k++)
            {
                int ring = (PointHead[id] - k + PointsPerTrail) % PointsPerTrail;
                float4 point = Points[id * PointsPerTrail + ring];
                float age = Now - point.w;
                float3 position = point.xyz;
                if (age > Time)
                {
                    // Cut the trail where it is exactly Time old, between this point and the newer one
                    if (k == 0)
                        break;
                    float4 newer = Points[id * PointsPerTrail + (ring + 1) % PointsPerTrail];
                    float span = newer.w - point.w;
                    float t = span > 1e-6f ? math.saturate((Now - Time - point.w) / span) : 1f;
                    position = math.lerp(point.xyz, newer.xyz, t);
                    age = Time;
                }

                if (k > 0)
                    length += math.distance(position, previous);
                float u = math.saturate(age / Time);
                float3 along = k == 0 && count > 1 ? Direction(id, 0, 1) : Direction(id, k - 1, k);
                float3 side = math.normalizesafe(math.cross(along, Camera - position)) * 0.5f * Sample(WidthCurve, u);
                Color32 color = ColorCurve[(int)(u * (ColorCurve.Length - 1))];
                float uv = Tile ? length : u;
                Vertices[vertexBase + used * 2] = new Vertex { Position = position - side, Color = color, Uv = new float2(uv, 0f) };
                Vertices[vertexBase + used * 2 + 1] = new Vertex { Position = position + side, Color = color, Uv = new float2(uv, 1f) };
                used++;
                previous = position;
                if (age >= Time)
                    break;
            }

            // Fill the rest of the block with degenerate geometry
            for (int v = used * 2; v < PointsPerTrail * 2; v++)
                Vertices[vertexBase + v] = new Vertex { Position = previous };
            for (int s = 0; s < PointsPerTrail - 1; s++)
            {
                int i = indexBase + s * 6;
                if (s + 1 < used)
                {
                    uint a = (uint)(vertexBase + s * 2);
                    Indices[i] = a;
                    Indices[i + 1] = a + 1;
                    Indices[i + 2] = a + 2;
                    Indices[i + 3] = a + 1;
                    Indices[i + 4] = a + 3;
                    Indices[i + 5] = a + 2;
                }
                else
                {
                    uint a = (uint)vertexBase;
                    Indices[i] = a;
                    Indices[i + 1] = a;
                    Indices[i + 2] = a;
                    Indices[i + 3] = a;
                    Indices[i + 4] = a;
                    Indices[i + 5] = a;
                }
            }
            VertexCount[id] = used;
        }

        private float3 Direction(int id, int newerIndex, int olderIndex)
        {
            int count = PointCount[id];
            if (olderIndex >= count)
                return new float3(0f, 0f, 1f);
            float4 newer = Points[id * PointsPerTrail + (PointHead[id] - newerIndex + PointsPerTrail) % PointsPerTrail];
            float4 older = Points[id * PointsPerTrail + (PointHead[id] - olderIndex + PointsPerTrail) % PointsPerTrail];
            return math.normalizesafe(newer.xyz - older.xyz, new float3(0f, 0f, 1f));
        }

        private static float Sample(NativeArray<float> curve, float t)
        {
            float x = t * (curve.Length - 1);
            int i = math.min((int)x, curve.Length - 2);
            return math.lerp(curve[i], curve[i + 1], x - i);
        }
    }
}
