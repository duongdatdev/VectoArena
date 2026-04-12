using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    public int width = 50;
    public int height = 50;
    public float tileSize = 1f;

    [Header("Base & Path")]
    public GameObject pathPrefab; // cobble_stones

    [Header("Borders")]
    public GameObject hedgeStraightPrefab;
    public GameObject hedgeCornerPrefab;

    [Header("Park Features")]
    public GameObject fountainPrefab;
    public GameObject benchPrefab;
    public GameObject lanternPrefab;
    public GameObject trashcanPrefab;

    [Header("Nature")]
    public GameObject[] treePrefabs;
    public GameObject[] bushPrefabs;
    public GameObject[] flowerPrefabs;
    public GameObject birdPrefab;

    [Header("Spawn Rates")]
    [Range(0f, 1f)] public float treeDensity = 0.05f;
    [Range(0f, 1f)] public float bushDensity = 0.08f;
    [Range(0f, 1f)] public float flowerDensity = 0.1f;
    [Range(0f, 1f)] public float birdDensity = 0.02f;

    [ContextMenu("Generate Park Map")]
    public void GenerateMap()
    {
        ClearMap();

        GameObject mapContainer = new GameObject("GeneratedMap");
        mapContainer.transform.parent = this.transform;
        mapContainer.transform.localPosition = Vector3.zero;

        float offsetX = (width * tileSize) / 2f - (tileSize / 2f);
        float offsetZ = (height * tileSize) / 2f - (tileSize / 2f);

        int centerX = width / 2;
        int centerZ = height / 2;
        
        int[] pathXs = { width / 4, width / 2, width * 3 / 4 };
        int[] pathZs = { height / 4, height / 2, height * 3 / 4 };

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 position = new Vector3(x * tileSize - offsetX, 0, z * tileSize - offsetZ);
                position += transform.position;

                // 1. (Đã xóa base floor vì dùng Plane)

                // 2. Xác định các khu vực logic
                bool isBorderX = (x == 0 || x == width - 1);
                bool isBorderZ = (z == 0 || z == height - 1);
                bool isBorder = isBorderX || isBorderZ;
                
                // Tìm đường gần nhất
                bool isPathX = false;
                int closestPathZ = -1;
                foreach (int pz in pathZs) {
                    if (z == pz) { isPathX = true; closestPathZ = pz; }
                    else if (Mathf.Abs(z - pz) == 1 && closestPathZ == -1) { closestPathZ = pz; }
                }

                bool isPathZ = false;
                int closestPathX = -1;
                foreach (int px in pathXs) {
                    if (x == px) { isPathZ = true; closestPathX = px; }
                    else if (Mathf.Abs(x - px) == 1 && closestPathX == -1) { closestPathX = px; }
                }

                bool isPath = (isPathX || isPathZ) && !isBorder;

                // Khu vực ngã tư
                bool isIntersectionPoint = isPathX && isPathZ;
                bool isIntersection = closestPathX != -1 && closestPathZ != -1 && Mathf.Abs(x - closestPathX) <= 1 && Mathf.Abs(z - closestPathZ) <= 1;

                // Lề đường ngang / dọc
                bool isPathEdgeX = !isPath && closestPathZ != -1 && !isPathZ;
                bool isPathEdgeZ = !isPath && closestPathX != -1 && !isPathX;

                // 3. Đặt các object theo quy luật logic mới
                if (isBorder)
                {
                    PlaceHedge(x, z, position, mapContainer);
                }
                else if (isPath)
                {
                    // Lát gạch mặt đường
                    if (pathPrefab != null && !isIntersectionPoint) // Giữ ngã tư để đặt fountain
                    {
                        InstantiatePrefab(pathPrefab, position, Quaternion.identity, mapContainer.transform);
                    }
                    else if (isIntersectionPoint && fountainPrefab != null)
                    {
                        // Đặt đài phun nước ở các ngã tư
                        InstantiatePrefab(fountainPrefab, position, Quaternion.identity, mapContainer.transform);
                    }
                }
                else if ((isPathEdgeX || isPathEdgeZ) && !isIntersection)
                {
                    // Đặt ghế và cột điện sát LỀ ĐƯỜNG, không đè lên đường, và phải đối xứng đều đặn
                    PlacePathDecorations(x, z, closestPathX, closestPathZ, isPathEdgeX, isPathEdgeZ, position, mapContainer);
                }
                else
                {
                    // Khu vực tự nhiên (Cỏ, Cây, Hoa, Chim)
                    PlaceNatureElements(position, mapContainer);
                }
            }
        }

        Debug.Log($"<color=green>Park Map successfully generated with size {width}x{height}</color>");
    }

    private void PlaceHedge(int x, int z, Vector3 pos, GameObject parent)
    {
        if ((x == 0 && z == 0) || (x == 0 && z == height - 1) || 
            (x == width - 1 && z == 0) || (x == width - 1 && z == height - 1))
        {
            if (hedgeCornerPrefab == null) return;
            float rot = 0;
            if (x == 0 && z == 0) rot = 90; 
            else if (x == 0 && z == height - 1) rot = 180;
            else if (x == width - 1 && z == height - 1) rot = 270;
            else if (x == width - 1 && z == 0) rot = 0;
            InstantiatePrefab(hedgeCornerPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
        }
        else 
        {
            if (hedgeStraightPrefab == null) return;
            float rot = (x == 0 || x == width - 1) ? 90 : 0;
            InstantiatePrefab(hedgeStraightPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
        }
    }

    private void PlacePathDecorations(int x, int z, int closestPathX, int closestPathZ, bool isEdgeX, bool isEdgeZ, Vector3 pos, GameObject parent)
    {
        // Chu kỳ lặp bố trí đèn và ghế: mỗi 5 ô
        int patternSpacing = 5;

        if (isEdgeX)
        {
            // Đường nằm ngang theo trục X
            // Để hai bên xen kẽ và đều nhau, ta sẽ dùng phép chia dư (modulo)
            int modX = x % patternSpacing;
            
            // Xoay mặt nhìn vào đường: Nếu mép dưới (z < đường) xoay 0 (nhìn lên +z), mép trên xoay 180 (nhìn xuống -z)
            float rot = (z < closestPathZ) ? 0 : 180;

            if (modX == 0) 
            {
                // Vị trí cột đèn
                if (lanternPrefab != null) InstantiatePrefab(lanternPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
            }
            else if (modX == 2) 
            {
                // Vị trí ghế
                if (benchPrefab != null) InstantiatePrefab(benchPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
            }
            else if (modX == 3)
            {
                // Thùng rác đặt xa cột đèn một chút, cạnh ghế đá
                if (trashcanPrefab != null && Random.value > 0.3f) InstantiatePrefab(trashcanPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
            }
        }
        else if (isEdgeZ)
        {
            // Đường dọc theo trục Z
            int modZ = z % patternSpacing;
            
            // Xoay mặt nhìn vào đường
            float rot = (x < closestPathX) ? 90 : 270;

            if (modZ == 0) 
            {
                if (lanternPrefab != null) InstantiatePrefab(lanternPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
            }
            else if (modZ == 2) 
            {
                if (benchPrefab != null) InstantiatePrefab(benchPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
            }
            else if (modZ == 3)
            {
                if (trashcanPrefab != null && Random.value > 0.3f) InstantiatePrefab(trashcanPrefab, pos, Quaternion.Euler(0, rot, 0), parent.transform);
            }
        }
    }

    private void PlaceNatureElements(Vector3 pos, GameObject parent)
    {
        float rand = Random.value;
        if (rand < treeDensity && treePrefabs != null && treePrefabs.Length > 0)
        {
            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            InstantiatePrefab(prefab, pos, GetRandomYRotation(), parent.transform);
        }
        else if (rand < treeDensity + bushDensity && bushPrefabs != null && bushPrefabs.Length > 0)
        {
            GameObject prefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
            InstantiatePrefab(prefab, pos, GetRandomYRotation(), parent.transform);
        }
        else if (rand < treeDensity + bushDensity + flowerDensity && flowerPrefabs != null && flowerPrefabs.Length > 0)
        {
            GameObject prefab = flowerPrefabs[Random.Range(0, flowerPrefabs.Length)];
            InstantiatePrefab(prefab, pos, GetRandomYRotation(), parent.transform);
        }
        
        if (Random.value < birdDensity && birdPrefab != null)
        {
            InstantiatePrefab(birdPrefab, pos, GetRandomYRotation(), parent.transform);
        }
    }

    private Quaternion GetRandomYRotation()
    {
        return Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
    }

    private void InstantiatePrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject go = Instantiate(prefab, position, rotation);
        go.transform.parent = parent;
        go.isStatic = true;
    }

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        Transform existingMap = transform.Find("GeneratedMap");
        if (existingMap != null)
        {
            if (Application.isPlaying) Destroy(existingMap.gameObject);
            else DestroyImmediate(existingMap.gameObject);
        }
    }
}
