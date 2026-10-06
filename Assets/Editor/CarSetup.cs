using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only helper: builds/repairs the test car in the open scene.
// Run it from the menu bar: Tools > Sim Racing > Setup Test Car
public static class CarSetup
{
    [MenuItem("Tools/Sim Racing/Setup Test Car")]
    private static void SetupTestCar()
    {
        if (BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();

        // --- Ground: make sure there is an active, large plane at the origin ---
        GameObject ground = FindRoot(scene, "Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }
        ground.SetActive(true);
        ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ground.transform.localScale = new Vector3(20f, 1f, 20f);

        // --- Car root with a Rigidbody ---
        GameObject car = FindRoot(scene, "car");
        if (car == null) car = new GameObject("car");
        car.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
        car.transform.localScale = Vector3.one;

        Rigidbody rb = GetOrAdd<Rigidbody>(car);
        if (rb.mass < 500f) rb.mass = 1500f;

        // --- Body: a cube centred on the car, sitting above the wheels ---
        Transform body = car.transform.Find("Body");
        if (body == null)
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            body.name = "Body";
            body.SetParent(car.transform);
        }
        body.SetLocalPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.identity);
        body.localScale = new Vector3(1.8f, 0.6f, 4f);

        // --- Four wheel colliders at the corners ---
        WheelCollider fl = SetupWheel(car, "WC_FL", new Vector3(-1.05f, 0.2f, 1.3f));
        WheelCollider fr = SetupWheel(car, "WC_FR", new Vector3(1.05f, 0.2f, 1.3f));
        WheelCollider rl = SetupWheel(car, "WC_RL", new Vector3(-1.05f, 0.2f, -1.3f));
        WheelCollider rr = SetupWheel(car, "WC_RR", new Vector3(1.05f, 0.2f, -1.3f));

        // --- CarController with its wheel slots wired up ---
        CarController controller = GetOrAdd<CarController>(car);
        controller.frontLeft = fl;
        controller.frontRight = fr;
        controller.rearLeft = rl;
        controller.rearRight = rr;

        // --- Camera placed behind and above the car ---
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0f, 6f, -12f), Quaternion.Euler(20f, 0f, 0f));
        }

        // --- Light: add a sun if the scene has no light at all ---
        if (!SceneHasLight(scene))
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.AddComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = car;
        Debug.Log("Test car set up. Press Play, click the Game view, then drive with WASD and brake with Space.");
    }

    // Adds CameraFollow to the Main Camera and points it at the car.
    [MenuItem("Tools/Sim Racing/Setup Camera Follow")]
    private static void SetupCameraFollow()
    {
        if (BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject car = FindRoot(scene, "car");
        Camera cam = Camera.main;
        if (car == null || cam == null)
        {
            Debug.LogError("Setup Camera Follow needs an object named 'car' and a camera tagged MainCamera.");
            return;
        }

        CameraFollow follow = GetOrAdd<CameraFollow>(cam.gameObject);
        follow.target = car.transform;

        // Interpolation smooths the car between physics steps so the camera doesn't jitter.
        Rigidbody rb = car.GetComponent<Rigidbody>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = cam.gameObject;
        Debug.Log("Camera Follow set up on the Main Camera, targeting the car.");
    }

    // Makes the car look like a car: coloured body, cabin, four visible wheels and a checkered ground.
    [MenuItem("Tools/Sim Racing/Setup Car Visuals")]
    private static void SetupCarVisuals()
    {
        if (BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject car = FindRoot(scene, "car");
        CarController controller = car != null ? car.GetComponent<CarController>() : null;
        if (controller == null)
        {
            Debug.LogError("Run Tools > Sim Racing > Setup Test Car first.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");

        Material bodyMaterial = GetOrCreateMaterial("CarBody", new Color(0.8f, 0.1f, 0.1f));
        Material cabinMaterial = GetOrCreateMaterial("CarCabin", new Color(0.1f, 0.15f, 0.25f));
        Material wheelMaterial = GetOrCreateMaterial("CarWheel", new Color(0.05f, 0.05f, 0.05f));
        Material groundMaterial = GetOrCreateMaterial("Ground", Color.white);

        // Checkered ground so you can see the car moving over it.
        groundMaterial.mainTexture = GetOrCreateCheckerTexture();
        groundMaterial.mainTextureScale = new Vector2(20f, 20f);
        EditorUtility.SetDirty(groundMaterial);

        GameObject ground = FindRoot(scene, "Ground");
        if (ground != null) ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        // Red body
        Transform body = car.transform.Find("Body");
        if (body != null) body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

        // Cabin sits on top of the body, towards the rear, so you can tell front from back.
        Transform cabin = car.transform.Find("Cabin");
        if (cabin == null)
        {
            cabin = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cabin.name = "Cabin";
            cabin.SetParent(car.transform);
            Object.DestroyImmediate(cabin.GetComponent<Collider>());
        }
        cabin.SetLocalPositionAndRotation(new Vector3(0f, 1.05f, -0.4f), Quaternion.identity);
        cabin.localScale = new Vector3(1.5f, 0.5f, 1.8f);
        cabin.GetComponent<Renderer>().sharedMaterial = cabinMaterial;

        // Visible wheels, driven by the CarController's mesh slots.
        controller.frontLeftMesh = SetupWheelMesh(car, controller.frontLeft, "WheelMesh_FL", wheelMaterial);
        controller.frontRightMesh = SetupWheelMesh(car, controller.frontRight, "WheelMesh_FR", wheelMaterial);
        controller.rearLeftMesh = SetupWheelMesh(car, controller.rearLeft, "WheelMesh_RL", wheelMaterial);
        controller.rearRightMesh = SetupWheelMesh(car, controller.rearRight, "WheelMesh_RR", wheelMaterial);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = car;
        Debug.Log("Car visuals set up: red body, cabin, wheels and checkered ground.");
    }

    // Creates a visible wheel (a cylinder turned on its side) at the WheelCollider's position.
    private static Transform SetupWheelMesh(GameObject car, WheelCollider wheel, string meshName, Material material)
    {
        // Push the wheel just outside the body so it is visible.
        Vector3 wheelPosition = wheel.transform.localPosition;
        wheelPosition.x = Mathf.Sign(wheelPosition.x) * 1.05f;
        wheel.transform.localPosition = wheelPosition;

        // Empty holder: this is what CarController moves and rotates.
        Transform holder = car.transform.Find(meshName);
        if (holder == null)
        {
            holder = new GameObject(meshName).transform;
            holder.SetParent(car.transform);

            // Unity's cylinder stands upright, so rotate it 90 degrees to lie along the axle.
            Transform cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            cylinder.name = "Cylinder";
            cylinder.SetParent(holder);
            cylinder.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 0f, 90f));
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        }
        holder.SetLocalPositionAndRotation(wheelPosition, Quaternion.identity);
        holder.localScale = Vector3.one;

        // Diameter = 2 x radius, 0.25 wide (a default cylinder is 2 units tall).
        Transform mesh = holder.GetChild(0);
        mesh.localScale = new Vector3(wheel.radius * 2f, 0.125f, wheel.radius * 2f);
        mesh.GetComponent<Renderer>().sharedMaterial = material;
        return holder;
    }

    // Loads Assets/Materials/<name>.mat, creating it if needed, and sets its colour.
    internal static Material GetOrCreateMaterial(string materialName, Color color)
    {
        string path = "Assets/Materials/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    // A tiny 2x2 texture of light and dark grey squares, repeated across the ground.
    private static Texture2D GetOrCreateCheckerTexture()
    {
        const string path = "Assets/Materials/GroundChecker.asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;

        Color dark = new Color(0.25f, 0.27f, 0.25f);
        Color light = new Color(0.4f, 0.43f, 0.4f);
        texture = new Texture2D(2, 2);
        texture.SetPixels(new[] { dark, light, light, dark });
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    // Builds a closed race track: road surface, red/white barriers, a finish line and
    // invisible checkpoints, plus a LapManager that knows their order.
    [MenuItem("Tools/Sim Racing/Setup Race Track")]
    private static void SetupRaceTrack()
    {
        if (BlockedByPlayMode()) return;

        const float roadWidth = 12f;
        const int samplesPerSection = 12;   // how many pieces each stretch between two corners is cut into
        const int gateCount = 6;            // finish line + 5 checkpoints

        // Corner points of the circuit on the ground (x, z), in driving order.
        // Edit these and run the menu item again to reshape the track.
        Vector2[] corners =
        {
            new Vector2(0f, -30f), new Vector2(0f, 30f), new Vector2(10f, 65f), new Vector2(40f, 80f),
            new Vector2(70f, 65f), new Vector2(75f, 35f), new Vector2(55f, 10f), new Vector2(60f, -20f),
            new Vector2(80f, -45f), new Vector2(60f, -75f), new Vector2(20f, -70f),
        };

        Scene scene = SceneManager.GetActiveScene();
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");

        // Smooth the corner points into a closed curve (a Catmull-Rom spline) along the middle of the road.
        List<Vector3> centre = new List<Vector3>();
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 p0 = corners[(i - 1 + corners.Length) % corners.Length];
            Vector2 p1 = corners[i];
            Vector2 p2 = corners[(i + 1) % corners.Length];
            Vector2 p3 = corners[(i + 2) % corners.Length];
            for (int s = 0; s < samplesPerSection; s++)
            {
                float t = s / (float)samplesPerSection;
                Vector2 p = 0.5f * (2f * p1 + (p2 - p0) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t)
                    + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t));
                centre.Add(new Vector3(p.x, 0f, p.y));
            }
        }
        int count = centre.Count;

        // Driving direction and "to the right" direction at every point of the curve.
        Vector3[] forward = new Vector3[count];
        Vector3[] right = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            forward[i] = (centre[(i + 1) % count] - centre[(i - 1 + count) % count]).normalized;
            right[i] = Vector3.Cross(Vector3.up, forward[i]);
        }

        // Start fresh each time so running this twice doesn't create duplicates.
        foreach (string oldName in new[] { "Track", "City" })
        {
            GameObject oldObject = FindRoot(scene, oldName);
            if (oldObject != null) Object.DestroyImmediate(oldObject);
        }
        GameObject track = new GameObject("Track");

        // --- Road: a flat strip of triangles lying just above the ground (the ground does the collision) ---
        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            Vector3 middle = centre[i] + Vector3.up * 0.02f;
            vertices[i * 2] = middle - right[i] * (roadWidth / 2f);       // left edge
            vertices[i * 2 + 1] = middle + right[i] * (roadWidth / 2f);   // right edge

            // Two triangles joining this pair of edge points to the next pair.
            int next = (i + 1) % count;
            int t = i * 6;
            triangles[t] = i * 2;
            triangles[t + 1] = next * 2;
            triangles[t + 2] = i * 2 + 1;
            triangles[t + 3] = i * 2 + 1;
            triangles[t + 4] = next * 2;
            triangles[t + 5] = next * 2 + 1;
        }
        Mesh roadMesh = new Mesh { name = "TrackRoad", vertices = vertices, triangles = triangles };
        roadMesh.RecalculateNormals();
        roadMesh.RecalculateBounds();
        AssetDatabase.DeleteAsset("Assets/Models/TrackRoad.asset");
        AssetDatabase.CreateAsset(roadMesh, "Assets/Models/TrackRoad.asset");

        GameObject road = new GameObject("Road");
        road.transform.SetParent(track.transform);
        road.AddComponent<MeshFilter>().sharedMesh = roadMesh;
        road.AddComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Asphalt", new Color(0.13f, 0.13f, 0.14f));

        // --- Barriers: red and white blocks along both edges that keep the car on the road ---
        Material red = GetOrCreateMaterial("BarrierRed", new Color(0.8f, 0.1f, 0.1f));
        Material white = GetOrCreateMaterial("BarrierWhite", Color.white);
        GameObject barriers = new GameObject("Barriers");
        barriers.transform.SetParent(track.transform);
        float edge = roadWidth / 2f + 0.7f;
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 a = centre[i] + right[i] * (side * edge);
                Vector3 b = centre[next] + right[next] * (side * edge);

                Transform block = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                block.name = "Barrier";
                block.SetParent(barriers.transform);
                block.SetPositionAndRotation((a + b) / 2f + Vector3.up * 0.3f, Quaternion.LookRotation(b - a));
                block.localScale = new Vector3(0.4f, 0.6f, Vector3.Distance(a, b) + 0.1f);
                block.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? red : white;
            }
        }

        // --- Start position (middle of the first straight) and finish line a little further on ---
        int startIndex = samplesPerSection / 2;
        int finishIndex = startIndex + 2;

        Transform finishStrip = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        finishStrip.name = "FinishStrip";
        finishStrip.SetParent(track.transform);
        finishStrip.SetPositionAndRotation(centre[finishIndex] + Vector3.up * 0.03f, Quaternion.LookRotation(forward[finishIndex]));
        finishStrip.localScale = new Vector3(roadWidth, 0.02f, 1.5f);
        finishStrip.GetComponent<Renderer>().sharedMaterial = white;
        Object.DestroyImmediate(finishStrip.GetComponent<Collider>());

        // --- Checkpoints: invisible gates spread evenly round the lap, finish line first ---
        // The LapSystem object is reused so RaceUI keeps its link to the LapManager.
        GameObject lapSystem = FindRoot(scene, "LapSystem");
        if (lapSystem == null) lapSystem = new GameObject("LapSystem");
        for (int i = lapSystem.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(lapSystem.transform.GetChild(i).gameObject);
        }

        Checkpoint[] gates = new Checkpoint[gateCount];
        for (int g = 0; g < gateCount; g++)
        {
            int index = (finishIndex + g * count / gateCount) % count;
            string gateName = g == 0 ? "FinishLine" : "Checkpoint_" + g;
            gates[g] = CreateGate(lapSystem, gateName, centre[index], forward[index], roadWidth + 2f);
        }
        GetOrAdd<LapManager>(lapSystem).checkpoints = gates;

        // --- Grass-coloured ground so the road stands out ---
        Material groundMaterial = GetOrCreateMaterial("Ground", new Color(0.35f, 0.7f, 0.3f));
        GameObject ground = FindRoot(scene, "Ground");
        if (ground != null) ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        // --- Put the car on the start position, facing along the road, with the camera behind it ---
        GameObject car = FindRoot(scene, "car");
        if (car != null)
        {
            Vector3 start = centre[startIndex];
            car.transform.SetPositionAndRotation(new Vector3(start.x, car.transform.position.y, start.z), Quaternion.LookRotation(forward[startIndex]));

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = car.transform.TransformPoint(new Vector3(0f, 3f, -7f));
                cam.transform.LookAt(car.transform.position + Vector3.up);
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = track;
        Debug.Log("Race track set up: follow the road round and cross the white finish line to complete a lap.");
    }

    // Creates one invisible checkpoint: a trigger box lying across the road.
    internal static Checkpoint CreateGate(GameObject parent, string gateName, Vector3 position, Vector3 forward, float width)
    {
        const float height = 3f;

        GameObject gate = new GameObject(gateName);
        gate.transform.SetParent(parent.transform);
        gate.transform.SetPositionAndRotation(position + Vector3.up * (height / 2f), Quaternion.LookRotation(forward));

        BoxCollider trigger = gate.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(width, height, 1f);

        return gate.AddComponent<Checkpoint>();
    }

    // Builds the on-screen display: a Canvas with three TextMeshPro texts and a RaceUI component.
    [MenuItem("Tools/Sim Racing/Setup Race UI")]
    private static void SetupRaceUI()
    {
        if (BlockedByPlayMode()) return;

        // TextMeshPro needs its fonts imported once per project.
        if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
        {
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
            EditorUtility.DisplayDialog("Import TextMeshPro first",
                "Click 'Import' in the window that just opened, wait for it to finish, then run Setup Race UI again.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject car = FindRoot(scene, "car");
        GameObject lapSystem = FindRoot(scene, "LapSystem");
        if (car == null || lapSystem == null)
        {
            Debug.LogError("Run Setup Test Car and Setup Lap System first.");
            return;
        }

        // Start fresh each time so running this twice doesn't create duplicates.
        GameObject old = FindRoot(scene, "RaceCanvas");
        if (old != null) Object.DestroyImmediate(old);

        // Canvas that covers the screen and scales with its size.
        GameObject canvasObject = new GameObject("RaceCanvas");
        canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RaceUI raceUI = canvasObject.AddComponent<RaceUI>();
        raceUI.carRigidbody = car.GetComponent<Rigidbody>();
        raceUI.lapManager = lapSystem.GetComponent<LapManager>();

        // Corners are given as (x, y) from 0 to 1: (0, 1) is top-left, (1, 0) is bottom-right.
        raceUI.lapText = CreateText(canvasObject, "LapText", new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);
        raceUI.lapTimeText = CreateText(canvasObject, "LapTimeText", new Vector2(1f, 1f), TextAlignmentOptions.TopRight);
        raceUI.speedText = CreateText(canvasObject, "SpeedText", new Vector2(1f, 0f), TextAlignmentOptions.BottomRight);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("Race UI set up: lap (top-left), lap time (top-right), speed (bottom-right).");
    }

    // Creates one text element pinned to a corner of the screen, with a 40 pixel margin.
    private static TMP_Text CreateText(GameObject canvas, string textName, Vector2 corner, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(textName);
        textObject.transform.SetParent(canvas.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 48f;
        text.alignment = alignment;
        text.text = textName;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = corner;
        rect.sizeDelta = new Vector2(600f, 140f);
        rect.anchoredPosition = new Vector2(corner.x == 0f ? 40f : -40f, corner.y == 0f ? 40f : -40f);
        return text;
    }

    // Replaces the box-shaped placeholder with the race car model from Assets/Models/KenneyCarKit.
    [MenuItem("Tools/Sim Racing/Setup Car Model/Race Car")]
    private static void SetupRaceCarModel()
    {
        SetupCarModel("race.fbx", "RaceCar", Color.white, 0.5f);
    }

    // A low, wide futuristic car painted glossy black.
    [MenuItem("Tools/Sim Racing/Setup Car Model/Black Night Car")]
    private static void SetupNightCarModel()
    {
        SetupCarModel("race-future.fbx", "NightCar", new Color(0.08f, 0.08f, 0.1f), 0.85f);
    }

    // modelFile: which model in the KenneyCarKit folder to use.
    // tint: multiplied with the model's colours (white = unchanged, near-black = black car).
    // smoothness: 0 = matte, 1 = mirror-like.
    private static void SetupCarModel(string modelFile, string materialName, Color tint, float smoothness)
    {
        if (BlockedByPlayMode()) return;

        const string folder = "Assets/Models/KenneyCarKit/";
        Scene scene = SceneManager.GetActiveScene();
        GameObject car = FindRoot(scene, "car");
        CarController controller = car != null ? car.GetComponent<CarController>() : null;
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + modelFile);
        if (controller == null || modelAsset == null)
        {
            Debug.LogError("Setup Car Model needs the car from Setup Test Car and the model at " + folder + modelFile);
            return;
        }

        // Remove a model left over from an earlier run, then put the car upright near the ground.
        DestroyChild(car, "Model");
        car.transform.SetPositionAndRotation(new Vector3(0f, 0.3f, 0f), Quaternion.identity);

        // Add a copy of the model under the car and scale it to a realistic 4.2 m length.
        GameObject model = Object.Instantiate(modelAsset, car.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        Bounds bounds = GetBounds(model);
        model.transform.localScale *= 4.2f / Mathf.Max(bounds.size.x, bounds.size.z);

        // The model's wheels are separate parts with "wheel" in their names.
        List<Renderer> wheels = new List<Renderer>();
        Renderer bodyRenderer = null;
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
        {
            if (part.name.Contains("wheel")) wheels.Add(part);
            else if (part.name == "body") bodyRenderer = part;
        }
        if (wheels.Count != 4)
        {
            Object.DestroyImmediate(model);
            Debug.LogError("Expected 4 wheel parts in the model but found " + wheels.Count + ".");
            return;
        }

        // Turn the model round if its front wheels are pointing backwards.
        float frontZ = 0f, backZ = 0f;
        foreach (Renderer wheel in wheels)
        {
            if (wheel.name.Contains("front")) frontZ += wheel.bounds.center.z;
            else backZ += wheel.bounds.center.z;
        }
        if (frontZ < backZ) model.transform.Rotate(0f, 180f, 0f);

        // Centre the model on the car, with its lowest point at the car's pivot.
        bounds = GetBounds(model);
        Vector3 carPosition = car.transform.position;
        model.transform.position += new Vector3(carPosition.x - bounds.center.x, carPosition.y - bounds.min.y, carPosition.z - bounds.center.z);

        // One material using the model's colour texture.
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        Material material = GetOrCreateMaterial(materialName, tint);
        material.SetFloat("_Smoothness", smoothness);
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Textures/colormap.png");
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = part.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            part.sharedMaterials = materials;
        }

        // The placeholder shapes are no longer needed.
        foreach (string placeholder in new[] { "Body", "Cabin", "WheelMesh_FL", "WheelMesh_FR", "WheelMesh_RL", "WheelMesh_RR" })
        {
            DestroyChild(car, placeholder);
        }

        // One box collider on the car, matching the model's body.
        Bounds bodyBounds = bodyRenderer != null ? bodyRenderer.bounds : GetBounds(model);
        BoxCollider box = GetOrAdd<BoxCollider>(car);
        box.center = bodyBounds.center - carPosition;
        box.size = bodyBounds.size;

        // Line each WheelCollider up with its wheel on the model, and let CarController move the wheel.
        foreach (Renderer wheel in wheels)
        {
            Vector3 centre = wheel.bounds.center - carPosition;
            bool front = centre.z > 0f;
            bool left = centre.x < 0f;
            string corner = (front ? "F" : "R") + (left ? "L" : "R");

            // Holder with no rotation of its own; the model's wheel part goes inside it.
            Transform holder = new GameObject("WheelMesh_" + corner).transform;
            holder.SetParent(car.transform);
            holder.SetLocalPositionAndRotation(centre, Quaternion.identity);
            wheel.transform.SetParent(holder, true);

            WheelCollider collider;
            switch (corner)
            {
                case "FL": collider = controller.frontLeft; controller.frontLeftMesh = holder; break;
                case "FR": collider = controller.frontRight; controller.frontRightMesh = holder; break;
                case "RL": collider = controller.rearLeft; controller.rearLeftMesh = holder; break;
                default: collider = controller.rearRight; controller.rearRightMesh = holder; break;
            }
            collider.radius = wheel.bounds.extents.y;
            collider.suspensionDistance = 0.2f;
            collider.transform.SetLocalPositionAndRotation(centre + Vector3.up * 0.05f, Quaternion.identity);
        }

        // The pivot is now at the bottom of the car, so the centre of mass sits just above it.
        controller.centerOfMassOffset = new Vector3(0f, 0.2f, 0f);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = car;
        Debug.Log("Car model set up from " + modelFile + " with body collider and four wheels.");
    }

    // The box that encloses every visible part of an object.
    private static Bounds GetBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer part in renderers) bounds.Encapsulate(part.bounds);
        return bounds;
    }

    internal static void DestroyChild(GameObject parent, string childName)
    {
        Transform child = parent.transform.Find(childName);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    // Finds or creates a child of the car, resets its transform and gives it a WheelCollider.
    private static WheelCollider SetupWheel(GameObject car, string wheelName, Vector3 localPosition)
    {
        Transform wheel = car.transform.Find(wheelName);
        if (wheel == null)
        {
            wheel = new GameObject(wheelName).transform;
            wheel.SetParent(car.transform);
        }
        wheel.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
        wheel.localScale = Vector3.one;

        WheelCollider collider = GetOrAdd<WheelCollider>(wheel.gameObject);
        collider.radius = 0.35f;
        return collider;
    }

    // Scene changes made while playing are thrown away when Play stops, so refuse to run then.
    internal static bool BlockedByPlayMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;

        EditorUtility.DisplayDialog("Stop Play mode first",
            "Press the Play button again to stop the game, then run this menu item.", "OK");
        return true;
    }

    internal static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    // Finds a top-level object by name, including disabled ones (GameObject.Find skips those).
    internal static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase)) return root;
        }
        return null;
    }

    private static bool SceneHasLight(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<Light>(true) != null) return true;
        }
        return false;
    }
}
