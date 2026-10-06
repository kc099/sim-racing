using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper: builds a dark night city with four different districts and a street circuit.
// Run it from the menu bar: Tools > Sim Racing > Setup Night City
public static class CitySetup
{
    private const float Spacing = 56f;                      // distance between one street and the next
    private const float StreetWidth = 16f;
    private const float BlockSize = Spacing - StreetWidth;  // 40 m city blocks between the streets
    private const int Half = 3;                             // streets run from -3 to +3, giving 6 x 6 blocks
    private const float WindowTile = 48f;                   // the window texture repeats every 48 m (16 windows of 3 m)

    // The lap, as street crossings in driving order (grid steps, not metres). The car starts at Route[1].
    // Edit these and run the menu item again to change the route; each step must move exactly one crossing.
    private static readonly Vector2Int[] Route =
    {
        new Vector2Int(0, -1), new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1),
        new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(2, 1), new Vector2Int(2, 0),
        new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(1, -2), new Vector2Int(0, -2),
        new Vector2Int(-1, -2), new Vector2Int(-1, -1),
    };

    // --- The four districts. Each has its own wall colour, window colour, and building heights. ---
    //                                              Old town                      Finance                        Neon quarter                   Docks
    private static readonly Color[] WallColours = { new Color(0.10f, 0.08f, 0.06f), new Color(0.05f, 0.07f, 0.11f), new Color(0.09f, 0.05f, 0.11f), new Color(0.07f, 0.07f, 0.07f) };
    private static readonly Color[] WindowColours = { new Color(1f, 0.8f, 0.4f), new Color(0.6f, 0.85f, 1f), new Color(1f, 0.35f, 0.9f), new Color(1f, 0.4f, 0.2f) };
    private static readonly float[] LitShare = { 0.25f, 0.5f, 0.35f, 0.12f };          // share of windows that are lit
    private static readonly float[] MinHeight = { 15f, 40f, 20f, 16f };
    private static readonly float[] ExtraHeight = { 40f, 90f, 50f, 10f };
    private static readonly float[] TierAbove = { 30f, 60f, 45f, 999f };               // taller than this: add a narrower upper tier
    private static readonly float[] SpireAbove = { 30f, 100f, 999f, 999f };            // taller than this: add a spire as well

    // Neon sign colours; each street of the route gets the next one, so every street looks different.
    private static readonly Color[] NeonColours =
    {
        new Color(0f, 0.9f, 1f), new Color(1f, 0.1f, 0.7f), new Color(0.3f, 1f, 0.2f),
        new Color(1f, 0.5f, 0f), new Color(1f, 0.1f, 0.1f), new Color(0.5f, 0.3f, 1f),
    };

    // Positions in the glowing mesh's material list.
    private const int LampGlow = 0;
    private const int BeaconGlow = 1;
    private const int RoadMarkGlow = 2;
    private const int FirstNeonGlow = 3;

    [MenuItem("Tools/Sim Racing/Setup Night City")]
    private static void SetupNightCity()
    {
        if (CarSetup.BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");

        // Start fresh: remove an earlier city and the open race track, plus files from the first city version.
        foreach (string oldName in new[] { "City", "Track" })
        {
            GameObject oldObject = CarSetup.FindRoot(scene, oldName);
            if (oldObject != null) Object.DestroyImmediate(oldObject);
        }
        foreach (string oldFile in new[] { "Materials/CityBuilding.mat", "Materials/CityWindows.asset", "Materials/CityWindowsGlow.asset", "Models/CityLamps.asset" })
        {
            AssetDatabase.DeleteAsset("Assets/" + oldFile);
        }
        GameObject city = new GameObject("City");

        // Everything solid goes into one mesh with one triangle list per district (each has its own material).
        // Everything that glows goes into a second mesh with one triangle list per glow colour.
        List<Vector3> buildingVertices = new List<Vector3>();
        List<Vector2> buildingUvs = new List<Vector2>();
        List<int>[] buildingTriangles = NewLists(WallColours.Length);
        List<Vector3> glowVertices = new List<Vector3>();
        List<Vector2> glowUvs = new List<Vector2>();
        List<int>[] glowTriangles = NewLists(FirstNeonGlow + NeonColours.Length);

        GameObject colliders = new GameObject("BlockColliders");
        colliders.transform.SetParent(city.transform);

        // --- Buildings ---
        System.Random random = new System.Random(7);   // fixed seed: the same city every time
        for (int bx = -Half; bx < Half; bx++)
        {
            for (int bz = -Half; bz < Half; bz++)
            {
                Vector3 blockCentre = new Vector3((bx + 0.5f) * Spacing, 0f, (bz + 0.5f) * Spacing);

                // District depends on which quarter of the map the block is in.
                int district = (bx >= 1 ? 1 : 0) + (bz >= 0 ? 0 : 2);
                List<int> triangles = buildingTriangles[district];

                if (bx == 0 && bz == 0)
                {
                    // Landmark: one huge stepped tower beside the start, with a red beacon you can see from far away.
                    AddBox(buildingVertices, buildingUvs, triangles, blockCentre, new Vector3(34f, 90f, 34f), WindowTile);
                    AddBox(buildingVertices, buildingUvs, triangles, blockCentre + Vector3.up * 90f, new Vector3(24f, 45f, 24f), WindowTile);
                    AddBox(buildingVertices, buildingUvs, triangles, blockCentre + Vector3.up * 135f, new Vector3(14f, 25f, 14f), WindowTile);
                    AddBox(buildingVertices, buildingUvs, triangles, blockCentre + Vector3.up * 160f, new Vector3(2f, 30f, 2f), 0f);
                    AddBox(glowVertices, glowUvs, glowTriangles[BeaconGlow], blockCentre + Vector3.up * 190f, new Vector3(4f, 4f, 4f), 0f);
                }
                else
                {
                    // Docks have two long warehouses per block; the other districts have four buildings.
                    float[] offsetsZ = district == 3 ? new[] { 0f } : new[] { -0.25f, 0.25f };
                    float depth = district == 3 ? BlockSize : BlockSize / 2f;
                    foreach (float offsetX in new[] { -0.25f, 0.25f })
                    {
                        foreach (float offsetZ in offsetsZ)
                        {
                            float width = BlockSize / 2f;
                            float height = MinHeight[district] + ExtraHeight[district] * Mathf.Pow((float)random.NextDouble(), 1.5f);
                            Vector3 centre = blockCentre + new Vector3(offsetX * BlockSize, 0f, offsetZ * BlockSize);
                            AddBox(buildingVertices, buildingUvs, triangles, centre, new Vector3(width, height, depth), WindowTile);

                            if (height > TierAbove[district])
                            {
                                float tierHeight = height * 0.3f;
                                AddBox(buildingVertices, buildingUvs, triangles, centre + Vector3.up * height,
                                    new Vector3(width * 0.6f, tierHeight, depth * 0.6f), WindowTile);
                                if (height > SpireAbove[district])
                                {
                                    AddBox(buildingVertices, buildingUvs, triangles, centre + Vector3.up * (height + tierHeight),
                                        new Vector3(width * 0.12f, 18f, depth * 0.12f), 0f);
                                }
                            }
                        }
                    }
                }

                // One invisible box per block stops the car driving through the buildings.
                GameObject blockCollider = new GameObject("BlockCollider");
                blockCollider.transform.SetParent(colliders.transform);
                blockCollider.transform.position = blockCentre + Vector3.up * 50f;
                blockCollider.AddComponent<BoxCollider>().size = new Vector3(BlockSize, 100f, BlockSize);
            }
        }

        // --- Street lamps: a dark pole with a glowing head at one corner of every crossing ---
        for (int sx = -Half; sx <= Half; sx++)
        {
            for (int sz = -Half; sz <= Half; sz++)
            {
                Vector3 lampBase = new Vector3(sx * Spacing + 7.5f, 0f, sz * Spacing + 7.5f);
                AddBox(buildingVertices, buildingUvs, buildingTriangles[3], lampBase, new Vector3(0.3f, 7f, 0.3f), 0f);
                AddBox(glowVertices, glowUvs, glowTriangles[LampGlow], lampBase + Vector3.up * 7f, new Vector3(1f, 0.5f, 1f), 0f);
            }
        }

        // --- Along every street of the route: neon signs, a dashed centre line, and sometimes a bridge ---
        for (int i = 0; i < Route.Length; i++)
        {
            Vector3 from = GridToWorld(Route[i]);
            Vector3 to = GridToWorld(Route[(i + 1) % Route.Length]);
            Vector3 along = (to - from).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, along);
            Vector3 middle = (from + to) / 2f;
            bool alongX = Mathf.Abs(along.x) > 0.5f;   // boxes can't be rotated, so swap their length and width instead

            // Four neon signs on the walls, in this street's colour.
            List<int> neon = glowTriangles[FirstNeonGlow + i % NeonColours.Length];
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float distance in new[] { -11f, 11f })
                {
                    Vector3 signBase = middle + across * (side * (StreetWidth / 2f - 0.2f)) + along * distance
                        + Vector3.up * (5f + 7f * (float)random.NextDouble());
                    AddBox(glowVertices, glowUvs, neon, signBase, alongX ? new Vector3(8f, 2.5f, 0.5f) : new Vector3(0.5f, 2.5f, 8f), 0f);
                }
            }

            // Dashed line down the middle of the road.
            for (float distance = -24f; distance <= 24f; distance += 8f)
            {
                AddBox(glowVertices, glowUvs, glowTriangles[RoadMarkGlow], middle + along * distance,
                    alongX ? new Vector3(3f, 0.02f, 0.3f) : new Vector3(0.3f, 0.02f, 3f), 0f);
            }

            // Every third street has a covered bridge between the buildings, with a neon strip underneath.
            if (i % 3 == 0)
            {
                Vector3 bridgeBase = middle + along * 20f + Vector3.up * 11f;
                AddBox(buildingVertices, buildingUvs, buildingTriangles[i % WallColours.Length], bridgeBase,
                    alongX ? new Vector3(5f, 4f, StreetWidth + 1f) : new Vector3(StreetWidth + 1f, 4f, 5f), WindowTile);
                AddBox(glowVertices, glowUvs, neon, bridgeBase - Vector3.up * 0.3f,
                    alongX ? new Vector3(0.4f, 0.3f, StreetWidth) : new Vector3(StreetWidth, 0.3f, 0.4f), 0f);
            }
        }

        // --- Materials: one per district for the buildings, one per colour for the glowing parts ---
        Material[] buildingMaterials = new Material[WallColours.Length];
        for (int d = 0; d < buildingMaterials.Length; d++)
        {
            Material material = CarSetup.GetOrCreateMaterial("CityBuilding_" + d, Color.white);
            material.mainTexture = CreateWindowTexture("CityWindows_" + d, false, d);
            material.SetTexture("_EmissionMap", CreateWindowTexture("CityWindowsGlow_" + d, true, d));
            material.SetColor("_EmissionColor", Color.white * 3f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.SetFloat("_Smoothness", 0.3f);
            buildingMaterials[d] = material;
        }

        Material[] glowMaterials = new Material[glowTriangles.Length];
        glowMaterials[LampGlow] = GetOrCreateGlowMaterial("LampGlow", new Color(1f, 0.75f, 0.4f), 4f);
        glowMaterials[BeaconGlow] = GetOrCreateGlowMaterial("BeaconGlow", Color.red, 6f);
        glowMaterials[RoadMarkGlow] = GetOrCreateGlowMaterial("RoadMarkGlow", new Color(0.8f, 0.8f, 0.7f), 0.6f);
        for (int n = 0; n < NeonColours.Length; n++)
        {
            glowMaterials[FirstNeonGlow + n] = GetOrCreateGlowMaterial("Neon_" + n, NeonColours[n], 4f);
        }

        CreateMeshObject(city, "Buildings", buildingVertices, buildingUvs, buildingTriangles, buildingMaterials);
        CreateMeshObject(city, "Glow", glowVertices, glowUvs, glowTriangles, glowMaterials);

        // --- Route barriers: close every side street along the lap, so the route is the only way to go ---
        Material barrierMaterial = GetOrCreateGlowMaterial("RouteBarrier", new Color(1f, 0.6f, 0f), 2f);
        GameObject barriers = new GameObject("RouteBarriers");
        barriers.transform.SetParent(city.transform);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        for (int i = 0; i < Route.Length; i++)
        {
            Vector2Int previous = Route[(i - 1 + Route.Length) % Route.Length];
            Vector2Int next = Route[(i + 1) % Route.Length];
            foreach (Vector2Int direction in directions)
            {
                // Leave the way we came from and the way we are going open.
                Vector2Int neighbour = Route[i] + direction;
                if (neighbour == previous || neighbour == next) continue;

                Vector3 outward = new Vector3(direction.x, 0f, direction.y);
                Transform barrier = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                barrier.name = "RouteBarrier";
                barrier.SetParent(barriers.transform);
                barrier.SetPositionAndRotation(GridToWorld(Route[i]) + outward * (StreetWidth / 2f + 1f) + Vector3.up * 0.6f,
                    Quaternion.LookRotation(outward));
                barrier.localScale = new Vector3(StreetWidth, 1.2f, 0.6f);
                barrier.GetComponent<Renderer>().sharedMaterial = barrierMaterial;
            }
        }

        // --- Checkpoints: one invisible gate in the middle of every street on the route ---
        // The LapSystem object is reused so RaceUI keeps its link to the LapManager.
        GameObject lapSystem = CarSetup.FindRoot(scene, "LapSystem");
        if (lapSystem == null) lapSystem = new GameObject("LapSystem");
        for (int i = lapSystem.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(lapSystem.transform.GetChild(i).gameObject);
        }

        Checkpoint[] gates = new Checkpoint[Route.Length];
        for (int g = 0; g < Route.Length; g++)
        {
            // Gate 0 (the finish line) is on the street just after the start crossing, Route[1].
            Vector3 from = GridToWorld(Route[(g + 1) % Route.Length]);
            Vector3 to = GridToWorld(Route[(g + 2) % Route.Length]);
            string gateName = g == 0 ? "FinishLine" : "Checkpoint_" + g;
            gates[g] = CarSetup.CreateGate(lapSystem, gateName, (from + to) / 2f, (to - from).normalized, StreetWidth);
        }
        CarSetup.GetOrAdd<LapManager>(lapSystem).checkpoints = gates;

        // Glowing white finish line on the road.
        Vector3 finishPosition = gates[0].transform.position;
        Transform finishStrip = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        finishStrip.name = "FinishStrip";
        finishStrip.SetParent(city.transform);
        finishStrip.SetPositionAndRotation(new Vector3(finishPosition.x, 0.03f, finishPosition.z), gates[0].transform.rotation);
        finishStrip.localScale = new Vector3(StreetWidth, 0.02f, 1.5f);
        finishStrip.GetComponent<Renderer>().sharedMaterial = GetOrCreateGlowMaterial("FinishGlow", Color.white, 2f);
        Object.DestroyImmediate(finishStrip.GetComponent<Collider>());

        // --- Ground: bigger and dark, like wet tarmac ---
        GameObject ground = CarSetup.FindRoot(scene, "Ground");
        if (ground != null)
        {
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            Material groundMaterial = CarSetup.GetOrCreateMaterial("Ground", new Color(0.3f, 0.3f, 0.35f));
            groundMaterial.mainTextureScale = new Vector2(40f, 40f);
            groundMaterial.SetFloat("_Smoothness", 0.7f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        }

        // --- Night: dark foggy sky, dim blue moonlight ---
        Color night = new Color(0.03f, 0.04f, 0.07f);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.26f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = night;
        RenderSettings.fogDensity = 0.008f;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Light sun = root.GetComponent<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color = new Color(0.55f, 0.65f, 1f);
                sun.intensity = 0.5f;
            }
        }

        // --- Car: back to the start crossing, with two headlights ---
        GameObject car = CarSetup.FindRoot(scene, "car");
        if (car != null)
        {
            Vector3 start = GridToWorld(Route[1]);
            Vector3 startDirection = GridToWorld(Route[2]) - start;
            car.transform.SetPositionAndRotation(new Vector3(start.x, car.transform.position.y, start.z), Quaternion.LookRotation(startDirection));

            CarSetup.DestroyChild(car, "Headlights");
            GameObject headlights = new GameObject("Headlights");
            headlights.transform.SetParent(car.transform, false);
            foreach (float side in new[] { -0.5f, 0.5f })
            {
                GameObject lamp = new GameObject("Headlight");
                lamp.transform.SetParent(headlights.transform, false);
                lamp.transform.localPosition = new Vector3(side, 0.7f, 2f);
                lamp.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);   // tilted slightly down onto the road

                Light headlight = lamp.AddComponent<Light>();
                headlight.type = LightType.Spot;
                headlight.range = 70f;
                headlight.spotAngle = 70f;
                headlight.intensity = 60f;
                headlight.color = new Color(0.9f, 0.95f, 1f);
            }
        }

        // --- Camera: plain dark background that matches the fog, placed behind the car ---
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = night;
            if (car != null)
            {
                cam.transform.position = car.transform.TransformPoint(new Vector3(0f, 3f, -7f));
                cam.transform.LookAt(car.transform.position + Vector3.up);
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = city;
        Debug.Log("Night city set up: follow the open streets; glowing orange barriers close the side roads.");
    }

    private static List<int>[] NewLists(int count)
    {
        List<int>[] lists = new List<int>[count];
        for (int i = 0; i < count; i++) lists[i] = new List<int>();
        return lists;
    }

    // Converts a street crossing (grid steps) to its position in metres.
    private static Vector3 GridToWorld(Vector2Int crossing)
    {
        return new Vector3(crossing.x * Spacing, 0f, crossing.y * Spacing);
    }

    // Adds a box standing on the point "baseCentre" to a mesh being built: four walls and a roof.
    // uvTile is how many metres one repeat of the texture covers; 0 means a plain, windowless surface.
    private static void AddBox(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 baseCentre, Vector3 size, float uvTile)
    {
        Vector3 min = baseCentre - new Vector3(size.x / 2f, 0f, size.z / 2f);
        Vector3 max = baseCentre + new Vector3(size.x / 2f, size.y, size.z / 2f);
        Vector3 x = Vector3.right * size.x;
        Vector3 y = Vector3.up * size.y;
        Vector3 z = Vector3.forward * size.z;

        AddQuad(vertices, uvs, triangles, new Vector3(min.x, min.y, min.z), x, y, uvTile);    // front wall
        AddQuad(vertices, uvs, triangles, new Vector3(max.x, min.y, max.z), -x, y, uvTile);   // back wall
        AddQuad(vertices, uvs, triangles, new Vector3(max.x, min.y, min.z), z, y, uvTile);    // right wall
        AddQuad(vertices, uvs, triangles, new Vector3(min.x, min.y, max.z), -z, y, uvTile);   // left wall
        AddQuad(vertices, uvs, triangles, new Vector3(min.x, max.y, min.z), x, z, 0f);        // roof
    }

    // Adds one rectangle (two triangles) starting at "corner" and stretching along "across" and "up".
    private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 corner, Vector3 across, Vector3 up, float uvTile)
    {
        int first = vertices.Count;
        vertices.Add(corner);
        vertices.Add(corner + up);
        vertices.Add(corner + across + up);
        vertices.Add(corner + across);

        if (uvTile > 0f)
        {
            // Texture coordinates in metres, so windows are the same size on every building.
            float offset = (corner.x + corner.z) / uvTile;
            float u = across.magnitude / uvTile;
            float v = up.magnitude / uvTile;
            uvs.Add(new Vector2(offset, 0f));
            uvs.Add(new Vector2(offset, v));
            uvs.Add(new Vector2(offset + u, v));
            uvs.Add(new Vector2(offset + u, 0f));
        }
        else
        {
            // Plain surface: every corner uses the same wall-coloured pixel of the texture.
            for (int i = 0; i < 4; i++) uvs.Add(new Vector2(0.002f, 0.002f));
        }

        triangles.Add(first);
        triangles.Add(first + 1);
        triangles.Add(first + 2);
        triangles.Add(first);
        triangles.Add(first + 2);
        triangles.Add(first + 3);
    }

    // Turns the lists built by AddBox into a saved mesh and an object that shows it.
    // Each triangle list becomes a "submesh" drawn with the material at the same position.
    private static void CreateMeshObject(GameObject parent, string objectName, List<Vector3> vertices, List<Vector2> uvs, List<int>[] triangleLists, Material[] materials)
    {
        Mesh mesh = new Mesh { name = objectName };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = triangleLists.Length;
        for (int i = 0; i < triangleLists.Length; i++) mesh.SetTriangles(triangleLists[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        string path = "Assets/Models/City" + objectName + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);

        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(parent.transform);
        meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
    }

    // A material that glows in its own colour (brightness above 1 gives a bloom halo).
    private static Material GetOrCreateGlowMaterial(string materialName, Color color, float brightness)
    {
        Material material = CarSetup.GetOrCreateMaterial(materialName, color);
        material.SetColor("_EmissionColor", color * brightness);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        return material;
    }

    // A 16 x 16 grid of windows for one district, some of them lit. With glowOnly, everything except
    // the lit windows is black, so only they glow when the texture is used as an emission map.
    private static Texture2D CreateWindowTexture(string textureName, bool glowOnly, int district)
    {
        const int windows = 16;
        const int pixels = 8;   // pixels per window cell
        Color wall = glowOnly ? Color.black : WallColours[district];
        Color darkWindow = glowOnly ? Color.black : WallColours[district] * 0.5f;
        Color litWindow = WindowColours[district];

        Texture2D texture = new Texture2D(windows * pixels, windows * pixels);
        System.Random random = new System.Random(3 + district);   // same seed for both textures, so the lit windows line up
        for (int cellY = 0; cellY < windows; cellY++)
        {
            for (int cellX = 0; cellX < windows; cellX++)
            {
                bool lit = random.NextDouble() < LitShare[district];
                for (int py = 0; py < pixels; py++)
                {
                    for (int px = 0; px < pixels; px++)
                    {
                        bool insideWindow = px >= 2 && px < 6 && py >= 2 && py < 7;
                        Color color = insideWindow ? (lit ? litWindow : darkWindow) : wall;
                        color.a = 1f;
                        texture.SetPixel(cellX * pixels + px, cellY * pixels + py, color);
                    }
                }
            }
        }
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Apply();

        string path = "Assets/Materials/" + textureName + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }
}
