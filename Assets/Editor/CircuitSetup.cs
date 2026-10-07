using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper: builds two grand prix style circuits from lists of corner points.
// Run them from the menu bar: Tools > Sim Racing > Setup Circuit
public static class CircuitSetup
{
    private const float SampleSpacing = 5f;   // metres between the points the road is built from
    private const float WallHeight = 1.3f;
    private const int GateCount = 12;         // finish line + 11 checkpoints

    // The circuit is one mesh; each of these is a part of it with its own material.
    private const int Asphalt = 0;
    private const int White = 1;
    private const int Red = 2;
    private const int Blue = 3;
    private const int Wall = 4;
    private const int Glow = 5;
    private const int PartCount = 6;

    // Corner points (x, z) in metres, in driving order. Both circuits run anticlockwise, like the real ones.
    // They are drawn from memory in the style of the real layouts at roughly half size, not surveyed copies.
    // Edit the points and run the menu item again to reshape a circuit.

    // Americas style: long straight into a tight left, fast esses, a far hairpin, a long back straight,
    // a twisty stadium section, a long multi-apex right-hander and two lefts back to the line.
    private static readonly Vector2[] AmericasCorners =
    {
        new Vector2(-300, -250), new Vector2(-100, -250), new Vector2(120, -250),                         // main straight
        new Vector2(230, -245), new Vector2(285, -215), new Vector2(270, -165), new Vector2(235, -120),   // turns 1-2
        new Vector2(250, -60), new Vector2(215, -10), new Vector2(255, 40), new Vector2(220, 90),         // esses
        new Vector2(260, 140), new Vector2(240, 200), new Vector2(215, 250), new Vector2(210, 310),
        new Vector2(200, 360), new Vector2(170, 385), new Vector2(135, 370),                              // far hairpin
        new Vector2(0, 300), new Vector2(-200, 230), new Vector2(-380, 170),                              // back straight
        new Vector2(-430, 130), new Vector2(-430, 70), new Vector2(-460, 20), new Vector2(-465, -30),     // stadium section
        new Vector2(-430, -70), new Vector2(-380, -60),
        new Vector2(-320, -20), new Vector2(-250, 10), new Vector2(-190, -20), new Vector2(-170, -80),    // long right-hander
        new Vector2(-195, -140), new Vector2(-290, -180),
        new Vector2(-370, -195), new Vector2(-400, -225), new Vector2(-375, -250),                        // last two lefts
    };

    // Marina Bay style: a street circuit of short straights and right-angle corners between walls, at night.
    private static readonly Vector2[] MarinaCorners =
    {
        new Vector2(-150, -300), new Vector2(60, -300), new Vector2(170, -300),                           // pit straight
        new Vector2(225, -285), new Vector2(240, -245), new Vector2(260, -210), new Vector2(295, -190),   // opening left-right-left
        new Vector2(310, -150), new Vector2(310, -60), new Vector2(325, -20), new Vector2(370, -5),
        new Vector2(600, -5), new Vector2(680, -5),                                                       // long boulevard straight
        new Vector2(720, 10), new Vector2(735, 50), new Vector2(735, 120), new Vector2(750, 160),         // left, right, left
        new Vector2(790, 175), new Vector2(830, 190), new Vector2(845, 230), new Vector2(845, 300),
        new Vector2(830, 345), new Vector2(785, 360), new Vector2(500, 360), new Vector2(300, 360),       // top straight
        new Vector2(250, 375), new Vector2(200, 400), new Vector2(150, 410), new Vector2(90, 395),        // bridge kink
        new Vector2(55, 360), new Vector2(65, 315), new Vector2(105, 295),                                // hairpin
        new Vector2(180, 290), new Vector2(220, 265), new Vector2(230, 220), new Vector2(225, 100),
        new Vector2(200, -20), new Vector2(180, -70), new Vector2(130, -90),
        new Vector2(-50, -90), new Vector2(-200, -90),                                                    // back straight
        new Vector2(-250, -105), new Vector2(-265, -150), new Vector2(-265, -230),
        new Vector2(-250, -280), new Vector2(-210, -300),                                                 // final lefts
    };

    // The mesh being built: shared corner points, and one triangle list per part.
    private static List<Vector3> vertices;
    private static List<Vector2> uvs;
    private static List<int>[] triangles;

    // The sampled centre line of the road, with the driving direction and "to the right" at each point.
    private static List<Vector3> centre;
    private static Vector3[] forward;
    private static Vector3[] right;

    [MenuItem("Tools/Sim Racing/Setup Circuit/Americas style (day)")]
    internal static void SetupAmericasCircuit()
    {
        // 15 m wide road with 6 m of painted run-off before the walls.
        BuildCircuit(AmericasCorners, 15f, 6f, false);
    }

    [MenuItem("Tools/Sim Racing/Setup Circuit/Marina Bay style (night)")]
    internal static void SetupMarinaCircuit()
    {
        // 12 m wide road with the walls right at its edge.
        BuildCircuit(MarinaCorners, 12f, 1f, true);
    }

    private static void BuildCircuit(Vector2[] corners, float roadWidth, float runoff, bool night)
    {
        if (CarSetup.BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");

        // Start fresh: remove whichever track, city and obstacles are in the scene.
        foreach (string oldName in new[] { "Track", "City", "Obstacles" })
        {
            GameObject oldObject = CarSetup.FindRoot(scene, oldName);
            if (oldObject != null) Object.DestroyImmediate(oldObject);
        }
        GameObject track = new GameObject("Track");

        SampleCentreLine(corners);
        int count = centre.Count;
        float half = roadWidth / 2f;
        float wallAt = half + runoff;   // distance from the centre line to the walls

        vertices = new List<Vector3>();
        uvs = new List<Vector2>();
        triangles = new List<int>[PartCount];
        for (int i = 0; i < PartCount; i++) triangles[i] = new List<int>();

        // --- Road surface, edge lines, kerbs, run-off and walls, one 5 m piece at a time ---
        bool[] isCorner = FindCorners();
        for (int i = 0; i < count; i++)
        {
            AddStrip(i, -half, half, 0.02f, Asphalt);
            // Wide white line along each edge, and a dashed line down the middle, so the road stands out.
            AddStrip(i, -half + 0.2f, -half + 0.9f, 0.03f, White);
            AddStrip(i, half - 0.9f, half - 0.2f, 0.03f, White);
            if (i % 2 == 0) AddStrip(i, -0.15f, 0.15f, 0.03f, White);

            if (isCorner[i])
            {
                // Red and white kerbs on both sides of every corner.
                int kerb = i % 2 == 0 ? Red : White;
                AddStrip(i, -half - 1.2f, -half, 0.03f, kerb);
                AddStrip(i, half, half + 1.2f, 0.03f, kerb);

                // Where there is room, painted run-off between the kerb and the wall.
                if (runoff > 2f)
                {
                    int paint = (i / 2) % 2 == 0 ? Blue : White;
                    AddStrip(i, -wallAt, -half - 1.2f, 0.02f, paint);
                    AddStrip(i, half + 1.2f, wallAt, 0.02f, paint);
                }
            }

            // Walls are plain on the straights and red and white through the corners.
            int wallPart = !isCorner[i] ? Wall : (i / 2) % 2 == 0 ? Red : White;
            AddWall(i, -wallAt, -1f, wallPart);
            AddWall(i, wallAt, 1f, wallPart);
        }

        // --- Wall colliders: invisible boxes, one for every two wall pieces ---
        GameObject wallColliders = new GameObject("WallColliders");
        wallColliders.transform.SetParent(track.transform);
        for (int i = 0; i < count; i += 2)
        {
            int next = (i + 2) % count;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 a = centre[i] + right[i] * (side * (wallAt + 0.5f));
                Vector3 b = centre[next] + right[next] * (side * (wallAt + 0.5f));

                GameObject piece = new GameObject("WallCollider");
                piece.transform.SetParent(wallColliders.transform);
                piece.transform.SetPositionAndRotation((a + b) / 2f + Vector3.up * 1.25f, Quaternion.LookRotation(b - a));
                piece.AddComponent<BoxCollider>().size = new Vector3(1f, 2.5f, Vector3.Distance(a, b) + 0.5f);
            }
        }

        // --- Scenery ---
        if (night)
        {
            // Floodlight masts every 40 m, alternating sides.
            for (int i = 0; i < count; i += 8)
            {
                float side = (i / 8) % 2 == 0 ? 1f : -1f;
                Vector3 mast = centre[i] + right[i] * (side * (wallAt + 2f));
                CitySetup.AddBox(vertices, uvs, triangles[Wall], mast, new Vector3(0.4f, 12f, 0.4f), 0f);
                CitySetup.AddBox(vertices, uvs, triangles[Glow], mast + Vector3.up * 12f, new Vector3(2.5f, 0.5f, 2.5f), 0f);
            }
            BuildSkyline(track, wallAt);
        }
        else
        {
            // Grandstands beside the main straight, and a tall observation tower in the middle of the circuit.
            for (int i = 4; i < 40 && i < count; i += 7)
            {
                CitySetup.AddBox(vertices, uvs, triangles[Wall], centre[i] + right[i] * (wallAt + 9f), new Vector3(28f, 8f, 12f), 0f);
                CitySetup.AddBox(vertices, uvs, triangles[Blue], centre[i] + right[i] * (wallAt + 9f) + Vector3.up * 8f, new Vector3(28f, 0.5f, 12f), 0f);
            }
            CitySetup.AddBox(vertices, uvs, triangles[White], Vector3.zero, new Vector3(6f, 70f, 6f), 0f);
            CitySetup.AddBox(vertices, uvs, triangles[Red], Vector3.up * 70f, new Vector3(14f, 4f, 14f), 0f);
        }

        // --- Turn the lists into the circuit object ---
        Material[] materials = new Material[PartCount];
        materials[Asphalt] = CarSetup.GetOrCreateMaterial("Asphalt", new Color(0.13f, 0.13f, 0.14f));
        materials[White] = CarSetup.GetOrCreateMaterial("BarrierWhite", Color.white);
        materials[Red] = CarSetup.GetOrCreateMaterial("BarrierRed", new Color(0.8f, 0.1f, 0.1f));
        materials[Blue] = CarSetup.GetOrCreateMaterial("RunoffBlue", new Color(0.1f, 0.2f, 0.7f));
        materials[Wall] = CarSetup.GetOrCreateMaterial("CircuitWall", new Color(0.6f, 0.6f, 0.62f));
        materials[Glow] = CitySetup.GetOrCreateGlowMaterial("FloodlightGlow", new Color(1f, 1f, 0.95f), 5f);
        CreateMeshObject(track, "Circuit", materials);

        // --- Start position, and the finish line 20 m further on ---
        int startIndex = 4;
        int finishIndex = 8;

        Transform finishStrip = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        finishStrip.name = "FinishStrip";
        finishStrip.SetParent(track.transform);
        finishStrip.SetPositionAndRotation(centre[finishIndex] + Vector3.up * 0.04f, Quaternion.LookRotation(forward[finishIndex]));
        finishStrip.localScale = new Vector3(roadWidth, 0.02f, 1.5f);
        finishStrip.GetComponent<Renderer>().sharedMaterial = materials[White];
        Object.DestroyImmediate(finishStrip.GetComponent<Collider>());

        // --- Checkpoints: invisible gates spread evenly round the lap, finish line first ---
        // The LapSystem object is reused so RaceUI keeps its link to the LapManager.
        GameObject lapSystem = CarSetup.FindRoot(scene, "LapSystem");
        if (lapSystem == null) lapSystem = new GameObject("LapSystem");
        for (int i = lapSystem.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(lapSystem.transform.GetChild(i).gameObject);
        }

        Checkpoint[] gates = new Checkpoint[GateCount];
        for (int g = 0; g < GateCount; g++)
        {
            int index = (finishIndex + g * count / GateCount) % count;
            string gateName = g == 0 ? "FinishLine" : "Checkpoint_" + g;
            gates[g] = CarSetup.CreateGate(lapSystem, gateName, centre[index], forward[index], wallAt * 2f);
        }
        CarSetup.GetOrAdd<LapManager>(lapSystem).checkpoints = gates;

        // --- Ground: big enough for the whole circuit; grass by day, dark by night ---
        float reach = 0f;
        foreach (Vector3 point in centre) reach = Mathf.Max(reach, Mathf.Abs(point.x), Mathf.Abs(point.z));
        float groundSize = (reach + 200f) * 2f;

        GameObject ground = CarSetup.FindRoot(scene, "Ground");
        if (ground != null)
        {
            ground.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f);   // a plane is 10 m across at scale 1
            // A plain material of its own (no pattern), so grass and road are easy to tell apart at a distance.
            Material groundMaterial = CarSetup.GetOrCreateMaterial("CircuitGround", night ? new Color(0.16f, 0.17f, 0.19f) : new Color(0.25f, 0.62f, 0.2f));
            groundMaterial.SetFloat("_Smoothness", 0f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        }

        // --- Sky and light ---
        Color nightSky = new Color(0.03f, 0.04f, 0.08f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        if (night)
        {
            // Dark sky, but the track itself is brightly floodlit.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);
            RenderSettings.fogColor = nightSky;
            RenderSettings.fogDensity = 0.0012f;
        }
        else
        {
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.fogColor = new Color(0.75f, 0.82f, 0.9f);
            RenderSettings.fogDensity = 0.0003f;
        }
        DynamicGI.UpdateEnvironment();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Light sun = root.GetComponent<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color = night ? new Color(0.9f, 0.93f, 1f) : new Color(1f, 0.96f, 0.88f);
                sun.intensity = night ? 0.8f : 1.3f;
            }
        }

        // --- Car on the start position, facing along the road (no headlights: the circuit is lit) ---
        GameObject car = CarSetup.FindRoot(scene, "car");
        if (car != null)
        {
            Vector3 start = centre[startIndex];
            car.transform.SetPositionAndRotation(new Vector3(start.x, car.transform.position.y, start.z), Quaternion.LookRotation(forward[startIndex]));
            CarSetup.DestroyChild(car, "Headlights");
        }

        // --- Camera: sees far enough for the long straights, and starts behind the car ---
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = night ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = nightSky;
            cam.farClipPlane = 3000f;
            if (car != null)
            {
                cam.transform.position = car.transform.TransformPoint(new Vector3(0f, 3f, -7f));
                cam.transform.LookAt(car.transform.position + Vector3.up);
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = track;
        Debug.Log("Circuit set up: " + Mathf.RoundToInt(count * SampleSpacing) + " m lap with " + GateCount + " checkpoints.");
    }

    // ------------------------------------------------------------------------------------------
    // Centre line
    // ------------------------------------------------------------------------------------------

    // Smooths the corner points into a closed curve and samples it every 5 m.
    // Fills in centre, forward and right. The circuit is shifted so its middle is at the origin.
    private static void SampleCentreLine(Vector2[] corners)
    {
        Vector2 min = corners[0];
        Vector2 max = corners[0];
        foreach (Vector2 corner in corners)
        {
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        Vector2 middle = (min + max) / 2f;

        // A closely spaced version of the curve...
        List<Vector3> dense = new List<Vector3>();
        int cornerCount = corners.Length;
        for (int i = 0; i < cornerCount; i++)
        {
            Vector2 p0 = corners[(i - 1 + cornerCount) % cornerCount] - middle;
            Vector2 p1 = corners[i] - middle;
            Vector2 p2 = corners[(i + 1) % cornerCount] - middle;
            Vector2 p3 = corners[(i + 2) % cornerCount] - middle;
            for (int s = 0; s < 16; s++) dense.Add(CurvePoint(p0, p1, p2, p3, s / 16f));
        }

        // ...then walk along it, dropping a point every 5 m so all the road pieces are the same length.
        centre = new List<Vector3> { dense[0] };
        float sinceLast = 0f;
        for (int i = 0; i < dense.Count; i++)
        {
            Vector3 a = dense[i];
            Vector3 b = dense[(i + 1) % dense.Count];
            float length = Vector3.Distance(a, b);
            float position = SampleSpacing - sinceLast;
            while (position <= length)
            {
                centre.Add(Vector3.Lerp(a, b, position / length));
                position += SampleSpacing;
            }
            sinceLast = length - (position - SampleSpacing);
        }

        // Drop the last point if it has landed almost on top of the first.
        if (Vector3.Distance(centre[centre.Count - 1], centre[0]) < SampleSpacing / 2f) centre.RemoveAt(centre.Count - 1);

        int count = centre.Count;
        forward = new Vector3[count];
        right = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            forward[i] = (centre[(i + 1) % count] - centre[(i - 1 + count) % count]).normalized;
            right[i] = Vector3.Cross(Vector3.up, forward[i]);
        }
    }

    // A point between p1 and p2 on a centripetal Catmull-Rom spline. This kind of curve passes through
    // every corner point and does not loop or overshoot when the points are unevenly spaced.
    private static Vector3 CurvePoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float share)
    {
        float t0 = 0f;
        float t1 = t0 + Mathf.Sqrt(Vector2.Distance(p0, p1));
        float t2 = t1 + Mathf.Sqrt(Vector2.Distance(p1, p2));
        float t3 = t2 + Mathf.Sqrt(Vector2.Distance(p2, p3));
        float t = Mathf.Lerp(t1, t2, share);

        Vector2 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
        Vector2 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
        Vector2 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
        Vector2 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
        Vector2 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
        Vector2 point = (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        return new Vector3(point.x, 0f, point.y);
    }

    // Marks the pieces of road that are in (or just next to) a bend, so they get kerbs.
    private static bool[] FindCorners()
    {
        int count = centre.Count;
        bool[] bending = new bool[count];
        for (int i = 0; i < count; i++)
        {
            // Compare the driving direction 10 m before and 10 m after this point.
            bending[i] = Vector3.Angle(forward[(i - 2 + count) % count], forward[(i + 2) % count]) > 7f;
        }

        bool[] isCorner = new bool[count];
        for (int i = 0; i < count; i++)
        {
            for (int near = -2; near <= 2; near++)
            {
                if (bending[(i + near + count) % count]) isCorner[i] = true;
            }
        }
        return isCorner;
    }

    // ------------------------------------------------------------------------------------------
    // Mesh building
    // ------------------------------------------------------------------------------------------

    // A flat piece of road from sample i to the next one, between two distances from the centre line
    // (negative = left of centre, positive = right), at the given height.
    private static void AddStrip(int i, float fromOffset, float toOffset, float height, int part)
    {
        int next = (i + 1) % centre.Count;
        Vector3 up = Vector3.up * height;
        AddQuad(centre[i] + right[i] * fromOffset + up, centre[next] + right[next] * fromOffset + up,
            centre[next] + right[next] * toOffset + up, centre[i] + right[i] * toOffset + up, part);
    }

    // A piece of wall from sample i to the next one: a face you can see from both sides, and a flat top.
    private static void AddWall(int i, float offset, float side, int part)
    {
        int next = (i + 1) % centre.Count;
        Vector3 a = centre[i] + right[i] * offset;
        Vector3 b = centre[next] + right[next] * offset;
        Vector3 up = Vector3.up * WallHeight;
        AddQuad(a, a + up, b + up, b, part);
        AddQuad(b, b + up, a + up, a, part);

        float outer = offset + side * 0.6f;
        AddStrip(i, Mathf.Min(offset, outer), Mathf.Max(offset, outer), WallHeight, part);
    }

    // Adds one four-cornered face (two triangles). It is visible from the side where a, b, c, d run clockwise.
    private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int part)
    {
        int first = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        for (int i = 0; i < 4; i++) uvs.Add(new Vector2(0.002f, 0.002f));

        List<int> list = triangles[part];
        list.Add(first);
        list.Add(first + 1);
        list.Add(first + 2);
        list.Add(first);
        list.Add(first + 2);
        list.Add(first + 3);
    }

    // Turns the lists into a saved mesh and an object that shows it, one material per part.
    private static void CreateMeshObject(GameObject parent, string objectName, Material[] materials)
    {
        Mesh mesh = new Mesh { name = objectName };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;   // allows more than 65,000 corner points
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = triangles.Length;
        for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        string path = "Assets/Models/" + objectName + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);

        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(parent.transform);
        meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
    }

    // ------------------------------------------------------------------------------------------
    // Night skyline
    // ------------------------------------------------------------------------------------------

    // Scatters tall buildings with lit windows around (but never on) the circuit.
    private static void BuildSkyline(GameObject parent, float wallAt)
    {
        // The skyline is its own mesh, so park the circuit's lists while it is built.
        List<Vector3> circuitVertices = vertices;
        List<Vector2> circuitUvs = uvs;
        List<int>[] circuitTriangles = triangles;
        vertices = new List<Vector3>();
        uvs = new List<Vector2>();
        triangles = new[] { new List<int>() };

        float reach = 0f;
        foreach (Vector3 point in centre) reach = Mathf.Max(reach, Mathf.Abs(point.x), Mathf.Abs(point.z));
        reach += 120f;

        System.Random random = new System.Random(5);   // fixed seed: the same skyline every time
        for (int attempt = 0; attempt < 260; attempt++)
        {
            float size = 22f + 22f * (float)random.NextDouble();
            float height = 30f + 130f * Mathf.Pow((float)random.NextDouble(), 2f);
            Vector3 position = new Vector3(((float)random.NextDouble() * 2f - 1f) * reach, 0f, ((float)random.NextDouble() * 2f - 1f) * reach);

            // Skip this spot if it is too close to any part of the road.
            float clearance = size * 0.75f + wallAt + 12f;
            bool tooClose = false;
            foreach (Vector3 point in centre)
            {
                if ((point - position).sqrMagnitude < clearance * clearance)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            CitySetup.AddBox(vertices, uvs, triangles[0], position, new Vector3(size, height, size), 48f);
        }

        Material material = CarSetup.GetOrCreateMaterial("SkylineBuilding", Color.white);
        material.mainTexture = CreateWindowTexture("SkylineWindows", false);
        material.SetTexture("_EmissionMap", CreateWindowTexture("SkylineWindowsGlow", true));
        material.SetColor("_EmissionColor", Color.white * 3f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        CreateMeshObject(parent, "CircuitSkyline", new[] { material });

        vertices = circuitVertices;
        uvs = circuitUvs;
        triangles = circuitTriangles;
    }

    // A 16 x 16 grid of windows, a third of them lit in warm or cool white. With glowOnly, everything
    // except the lit windows is black, so only they glow when the texture is used as an emission map.
    private static Texture2D CreateWindowTexture(string textureName, bool glowOnly)
    {
        const int windows = 16;
        const int pixels = 8;   // pixels per window cell
        Color wall = glowOnly ? Color.black : new Color(0.06f, 0.07f, 0.1f);
        Color darkWindow = glowOnly ? Color.black : new Color(0.03f, 0.04f, 0.06f);

        Texture2D texture = new Texture2D(windows * pixels, windows * pixels);
        System.Random random = new System.Random(9);   // same seed for both textures, so the lit windows line up
        for (int cellY = 0; cellY < windows; cellY++)
        {
            for (int cellX = 0; cellX < windows; cellX++)
            {
                bool lit = random.NextDouble() < 0.35;
                Color litWindow = random.NextDouble() < 0.5 ? new Color(1f, 0.85f, 0.55f) : new Color(0.7f, 0.9f, 1f);
                for (int py = 0; py < pixels; py++)
                {
                    for (int px = 0; px < pixels; px++)
                    {
                        bool insideWindow = px >= 2 && px < 6 && py >= 2 && py < 7;
                        texture.SetPixel(cellX * pixels + px, cellY * pixels + py, insideWindow ? (lit ? litWindow : darkWindow) : wall);
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
