using System;
using System.Collections.Generic;
using UnityEngine;

public class DungeonCreator : MonoBehaviour
{
    [Header("Dungeon")]
    public int dungeonWidth, dungeonLength, dungeonFloors;
    public int roomWidthMin, roomLengthMin, roomOffset, corridorWidth, maxIterations;
    [Range(0.1f, 0.3f)] public float bottomCornerModifier;
    [Range(0.7f, 1.0f)] public float topCornerModifier;

    [Header("Prefabs/Material")]
    public GameObject floorPrefab;
    public GameObject wallHorizontal;   // 1-cell long, pivot centered, points along +X
    public GameObject wallVertical;     // same mesh/orientation as horizontal is fine; we rotate it 90° Y
    public GameObject doorPrefab;       // 1-cell long along local X; will be stretched
    public Material material;

    [Header("Placement")]
    [SerializeField] private float cellSize = 1f; // world units per grid cell
    [SerializeField] private float yOffset = 0f;
    [Tooltip("Set to the door model's length (local X at scale=1). If 0 we try to auto-detect.")]
    [SerializeField] private float doorBaseLength = 0f;

    Transform floorsParent, wallsParent, doorsParent;

    // ---------- floor cells ----------
    private readonly HashSet<Vector3Int> floorCells = new();

    // ---------- edges ----------
    private enum EdgeDir : byte { H, V } // H: along +X at fixed Z ; V: along +Z at fixed X

    private struct EdgeKey : IEquatable<EdgeKey>
    {
        public int x, z; public EdgeDir dir;
        public EdgeKey(int x, int z, EdgeDir dir) { this.x = x; this.z = z; this.dir = dir; }
        public bool Equals(EdgeKey other) => x == other.x && z == other.z && dir == other.dir;
        public override bool Equals(object obj) => obj is EdgeKey e && Equals(e);
        public override int GetHashCode() => HashCode.Combine(x, z, (int)dir);
    }

    // boundary walls around the union of all floor cells
    private readonly HashSet<EdgeKey> wallEdges = new();

    // counts of per-room perimeter edges (to detect shared seams = doors)
    private readonly Dictionary<EdgeKey, int> seamCounts = new();

    void Awake()
    {
        // Try to infer cell size from the floor prefab footprint
        if (floorPrefab != null)
        {
            var r = floorPrefab.GetComponentInChildren<Renderer>();
            if (r != null) cellSize = Mathf.Max(0.0001f, r.bounds.size.x);
        }

        if (doorBaseLength <= 0f && doorPrefab != null)
        {
            // Prefer MeshFilter local bounds (not affected by rotation)
            var mf = doorPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                doorBaseLength = Mathf.Max(0.0001f, mf.sharedMesh.bounds.size.x);
            else
            {
                var rr = doorPrefab.GetComponentInChildren<Renderer>();
                if (rr != null) doorBaseLength = Mathf.Max(0.0001f, rr.bounds.size.x);
                else doorBaseLength = cellSize; // fallback
            }
        }
    }

    void Start()
    {
        CreateDungeon();
    }

    void CreateDungeon()
    {
        var generator = new DungeonGenerator(dungeonWidth, dungeonLength);
        var rects = generator.CalculateDungeon(
            maxIterations, roomWidthMin, roomLengthMin,
            bottomCornerModifier, topCornerModifier,
            roomOffset, corridorWidth);

        floorsParent = new GameObject("Floors").transform; floorsParent.SetParent(transform, false);
        wallsParent  = new GameObject("Walls").transform;  wallsParent.SetParent(transform, false);
        doorsParent  = new GameObject("Doors").transform;  doorsParent.SetParent(transform, false);

        // 1) Accumulate floor cells & seam counts (for door detection)
        foreach (var r in rects)
            AccumulateRoom(r.BottomLeftAreaCorner, r.TopRightAreaCorner);

        // 2) Build wall edges from the UNION boundary of floorCells
        BuildBoundaryWallEdges();

        // 3) Spawn floors, walls, doors
        SpawnFloors();
        SpawnWalls();
        SpawnDoors();   // stretched runs
    }

    // --- Accumulate union floors + count per-room perimeters (for doors) ---
    void AccumulateRoom(Vector2 bl, Vector2 tr)
    {
        int minX = Mathf.FloorToInt(bl.x);
        int minZ = Mathf.FloorToInt(bl.y);
        int maxX = Mathf.CeilToInt(tr.x);
        int maxZ = Mathf.CeilToInt(tr.y);

        // floor cells
        for (int x = minX; x < maxX; x++)
            for (int z = minZ; z < maxZ; z++)
                floorCells.Add(new Vector3Int(x, 0, z));

        // per-room perimeter edges -> count for seam detection
        for (int x = minX; x < maxX; x++)
        {
            IncEdge(new EdgeKey(x, minZ, EdgeDir.H)); // bottom edge at z=minZ
            IncEdge(new EdgeKey(x, maxZ, EdgeDir.H)); // top edge at z=maxZ
        }
        for (int z = minZ; z < maxZ; z++)
        {
            IncEdge(new EdgeKey(minX, z, EdgeDir.V)); // left edge at x=minX
            IncEdge(new EdgeKey(maxX, z, EdgeDir.V)); // right edge at x=maxX
        }
    }

    void IncEdge(EdgeKey k)
    {
        if (seamCounts.TryGetValue(k, out int c)) seamCounts[k] = c + 1;
        else seamCounts[k] = 1;
    }

    // --- Boundary walls from union of floors ---
    void BuildBoundaryWallEdges()
    {
        wallEdges.Clear();
        foreach (var c in floorCells)
        {
            int x = c.x, z = c.z;

            // if neighbor is empty, add that side's edge
            if (!floorCells.Contains(new Vector3Int(x, 0, z - 1)))
                wallEdges.Add(new EdgeKey(x, z, EdgeDir.H));     // bottom at z
            if (!floorCells.Contains(new Vector3Int(x, 0, z + 1)))
                wallEdges.Add(new EdgeKey(x, z + 1, EdgeDir.H)); // top at z+1
            if (!floorCells.Contains(new Vector3Int(x - 1, 0, z)))
                wallEdges.Add(new EdgeKey(x, z, EdgeDir.V));     // left at x
            if (!floorCells.Contains(new Vector3Int(x + 1, 0, z)))
                wallEdges.Add(new EdgeKey(x + 1, z, EdgeDir.V)); // right at x+1
        }
    }

    // --- Spawners ---
    void SpawnFloors()
    {
        foreach (var c in floorCells)
        {
            Vector3 pos = new Vector3((c.x + 0.5f) * cellSize, yOffset, (c.z + 0.5f) * cellSize);
            Instantiate(floorPrefab, pos, Quaternion.identity, floorsParent);
        }
    }

    void SpawnWalls()
    {
        foreach (var e in wallEdges)
        {
            if (e.dir == EdgeDir.H)
            {
                float worldX = (e.x + 0.5f) * cellSize;
                float worldZ = (e.z) * cellSize; // lies on gridline z
                var pos = new Vector3(worldX, yOffset, worldZ);
                Instantiate(wallHorizontal, pos, Quaternion.identity, wallsParent);
            }
            else
            {
                float worldX = (e.x) * cellSize; // gridline x
                float worldZ = (e.z + 0.5f) * cellSize;
                var pos = new Vector3(worldX, yOffset, worldZ);
                Instantiate(wallVertical, pos, Quaternion.Euler(0f, 90f, 0f), wallsParent);
            }
        }
    }

    void SpawnDoors()
    {
        if (doorPrefab == null) return;

        // Each shared edge between rectangles is a door tile
        foreach (var kv in seamCounts)
        {
            if (kv.Value < 2) continue; // not a shared seam → not a door

            EdgeKey e = kv.Key;
            if (e.dir == EdgeDir.H)
            {
                // Horizontal edge: midpoint at (x+0.5, z)
                float worldX = (e.x + 0.5f) * cellSize;
                float worldZ = (e.z) * cellSize;
                Vector3 pos = new Vector3(worldX, yOffset, worldZ);

                Instantiate(doorPrefab, pos, Quaternion.identity, doorsParent);
            }
            else // EdgeDir.V
            {
                // Vertical edge: midpoint at (x, z+0.5), rotated 90° around Y
                float worldX = (e.x) * cellSize;
                float worldZ = (e.z + 0.5f) * cellSize;
                Vector3 pos = new Vector3(worldX, yOffset, worldZ);

                Instantiate(doorPrefab, pos, Quaternion.Euler(0f, 90f, 0f), doorsParent);
            }
        }
    }


    // Stretch an instance along LOCAL X so it spans targetLength world units
    void StretchAlongLocalX(GameObject go, float targetLength)
    {
        if (doorBaseLength <= 0f) return;
        float factor = targetLength / doorBaseLength;
        var s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x * factor, s.y, s.z);
    }
}
