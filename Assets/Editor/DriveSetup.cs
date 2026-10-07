using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only helper for the driving extras: dashboard and camera views, obstacles, and traffic.
public static class DriveSetup
{
    private const string KitFolder = "Assets/Models/KenneyCarKit/";
    private static readonly string[] TrafficModels =
    {
        "sedan.fbx", "taxi.fbx", "suv.fbx", "van.fbx", "police.fbx", "hatchback-sports.fbx", "delivery.fbx",
    };

    // ------------------------------------------------------------------------------------------
    // Dashboard: speedometer, rev counter, gear display and an on-screen steering wheel
    // ------------------------------------------------------------------------------------------

    [MenuItem("Tools/Sim Racing/Setup Dashboard and Views")]
    internal static void SetupDashboard()
    {
        if (CarSetup.BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject car = CarSetup.FindRoot(scene, "car");
        GameObject canvas = CarSetup.FindRoot(scene, "RaceCanvas");
        Camera cam = Camera.main;
        CameraFollow cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (car == null || canvas == null || cameraFollow == null)
        {
            Debug.LogError("Setup Dashboard needs the car, the RaceCanvas (Setup Race UI) and Camera Follow (Setup Camera Follow).");
            return;
        }

        // The speed text lives inside the speedometer; rescue it before removing an old dashboard.
        RaceUI raceUI = canvas.GetComponent<RaceUI>();
        if (raceUI != null && raceUI.speedText != null) raceUI.speedText.transform.SetParent(canvas.transform, false);
        CarSetup.DestroyChild(canvas, "Dashboard");

        if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");

        // A full-screen holder for all the instruments.
        RectTransform root = CreateRect(canvas.transform, "Dashboard", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        root.anchorMax = Vector2.one;
        Dashboard dashboard = root.gameObject.AddComponent<Dashboard>();
        dashboard.car = car.GetComponent<CarController>();
        dashboard.cameraFollow = cameraFollow;

        // --- Steering wheel: bottom centre, half off the screen like a real dashboard ---
        RectTransform wheel = CreateRect(root, "SteeringWheel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(560f, 560f), new Vector2(0f, 90f));
        wheel.gameObject.AddComponent<RawImage>().texture = CreateWheelTexture();
        dashboard.steeringWheel = wheel;

        // --- Speedometer: bottom right, 0 to 240 km/h ---
        RectTransform speedDial = CreateRect(root, "Speedometer", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(320f, 320f), new Vector2(-30f, 30f));
        speedDial.gameObject.AddComponent<RawImage>().texture = CreateDialTexture("SpeedDial", 6, 2f);
        for (int i = 0; i <= 6; i++) CreateDialLabel(speedDial, (i * 40).ToString(), i / 6f, 98f, 22f);

        if (raceUI != null && raceUI.speedText != null)
        {
            // Reuse RaceUI's speed text as the digital readout inside the dial.
            raceUI.speedText.transform.SetParent(speedDial, false);
            raceUI.speedText.fontSize = 28f;
            raceUI.speedText.alignment = TextAlignmentOptions.Center;
            PlaceRect(raceUI.speedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 50f), new Vector2(0f, -75f));
        }
        dashboard.speedNeedle = CreateNeedle(speedDial, 118f);

        // --- Rev counter: left of the speedometer, 0 to 8 (x1000 rpm), with the gear in the middle ---
        RectTransform rpmDial = CreateRect(root, "RevCounter", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(240f, 240f), new Vector2(-370f, 30f));
        rpmDial.gameObject.AddComponent<RawImage>().texture = CreateDialTexture("RevDial", 8, 0.8f);
        for (int i = 0; i <= 8; i++) CreateDialLabel(rpmDial, i.ToString(), i / 8f, 72f, 20f);

        dashboard.gearText = CreateLabel(rpmDial, "GearText", "1", 56f, new Vector2(0f, -48f));
        dashboard.gearModeText = CreateLabel(rpmDial, "GearModeText", "AUTO", 16f, new Vector2(0f, -90f));
        dashboard.rpmNeedle = CreateNeedle(rpmDial, 86f);

        // --- Key reminder along the top ---
        TMP_Text hint = CreateLabel(root, "KeysHint", "C view     M auto / manual     Q / E gear down / up     Space handbrake", 22f, Vector2.zero);
        PlaceRect(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000f, 40f), new Vector2(0f, -15f));
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Dashboard set up. While playing: C changes view, M switches automatic/manual, Q/E change gear in manual.");
    }

    // Creates an empty UI rectangle. "anchor" is the point of the parent it is pinned to, and
    // "pivot" the point of itself that sits there (both from 0,0 = bottom-left to 1,1 = top-right).
    private static RectTransform CreateRect(Transform parent, string rectName, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        GameObject rectObject = new GameObject(rectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)rectObject.transform;
        PlaceRect(rect, anchor, pivot, size, position);
        return rect;
    }

    private static void PlaceRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    // Centred text at "position" pixels from the middle of the parent.
    private static TMP_Text CreateLabel(Transform parent, string labelName, string content, float fontSize, Vector2 position)
    {
        RectTransform rect = CreateRect(parent, labelName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(160f, 70f), position);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    // A number on the dial face. "share" is how far round the scale it sits (0 = start, 1 = end).
    private static void CreateDialLabel(Transform dial, string content, float share, float radius, float fontSize)
    {
        // The scale sweeps from 135 degrees left of straight up to 135 degrees right of it.
        float angle = Mathf.Lerp(135f, -135f, share) * Mathf.Deg2Rad;
        Vector2 position = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
        CreateLabel(dial, "Label_" + content, content, fontSize, position);
    }

    // A thin red needle that turns about the centre of the dial.
    private static RectTransform CreateNeedle(Transform dial, float length)
    {
        RectTransform needle = CreateRect(dial, "Needle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(6f, length), Vector2.zero);
        needle.gameObject.AddComponent<Image>().color = new Color(1f, 0.2f, 0.15f);
        return needle;
    }

    // Draws a round dial face: dark disc, pale rim, long and short tick marks.
    // majorTicks is the number of gaps between numbers; from "redFrom" (0 to 1) onwards the scale is red.
    private static Texture2D CreateDialTexture(string textureName, int majorTicks, float redFrom)
    {
        const int size = 256;
        Color face = new Color(0.03f, 0.03f, 0.05f, 0.8f);
        Color rim = new Color(0.75f, 0.75f, 0.8f, 1f);
        Color red = new Color(1f, 0.15f, 0.1f, 1f);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Position from -1 to 1 across the texture, and distance from the centre.
                float dx = (x + 0.5f - size / 2f) / (size / 2f);
                float dy = (y + 0.5f - size / 2f) / (size / 2f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                Color color = Color.clear;
                if (distance <= 1f)
                {
                    color = distance > 0.95f ? rim : face;

                    // How far round the scale this pixel is: 0 at the start, 1 at the end.
                    float angle = Mathf.Atan2(-dx, dy) * Mathf.Rad2Deg;
                    float share = (135f - angle) / 270f;
                    if (share > -0.005f && share < 1.005f)
                    {
                        float major = share * majorTicks;
                        float minor = major * 2f;
                        float thickness = 0.006f * majorTicks;
                        bool onMajor = Mathf.Abs(major - Mathf.Round(major)) < thickness && distance > 0.76f && distance < 0.93f;
                        bool onMinor = Mathf.Abs(minor - Mathf.Round(minor)) < thickness && distance > 0.86f && distance < 0.93f;
                        if (onMajor || onMinor) color = share >= redFrom ? red : Color.white;
                        else if (share >= redFrom && distance > 0.89f && distance < 0.93f) color = red;
                    }
                }
                texture.SetPixel(x, y, color);
            }
        }
        return SaveTexture(texture, textureName);
    }

    // Draws a steering wheel seen from the driver's seat: rim, hub, three spokes and a yellow mark at the top.
    private static Texture2D CreateWheelTexture()
    {
        const int size = 512;
        Color rim = new Color(0.08f, 0.08f, 0.09f, 1f);
        Color rimEdge = new Color(0.2f, 0.2f, 0.22f, 1f);
        Color spoke = new Color(0.14f, 0.14f, 0.16f, 1f);
        Color mark = new Color(1f, 0.8f, 0f, 1f);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size / 2f) / (size / 2f);
                float dy = (y + 0.5f - size / 2f) / (size / 2f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                Color color = Color.clear;
                if (distance <= 1f)
                {
                    if (distance > 0.8f)
                    {
                        color = distance < 0.83f || distance > 0.98f ? rimEdge : rim;
                        if (Mathf.Abs(dx) < 0.035f && dy > 0f) color = mark;
                    }
                    else if (distance < 0.24f) color = rimEdge;                       // hub
                    else if (Mathf.Abs(dy + 0.03f) < 0.075f) color = spoke;           // left and right spokes
                    else if (Mathf.Abs(dx) < 0.075f && dy < 0f) color = spoke;        // bottom spoke
                }
                texture.SetPixel(x, y, color);
            }
        }
        return SaveTexture(texture, "SteeringWheel");
    }

    private static Texture2D SaveTexture(Texture2D texture, string textureName)
    {
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        string path = "Assets/UI/" + textureName + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    // ------------------------------------------------------------------------------------------
    // Obstacles: cones and crates you can knock over, placed at the checkpoints of the current track
    // ------------------------------------------------------------------------------------------

    [MenuItem("Tools/Sim Racing/Setup Obstacles")]
    private static void SetupObstacles()
    {
        if (CarSetup.BlockedByPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        BuildObstacles(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    internal static void BuildObstacles(Scene scene)
    {
        GameObject lapSystem = CarSetup.FindRoot(scene, "LapSystem");
        LapManager lapManager = lapSystem != null ? lapSystem.GetComponent<LapManager>() : null;
        if (lapManager == null || lapManager.checkpoints == null || lapManager.checkpoints.Length == 0)
        {
            Debug.LogError("Setup Obstacles needs a track first (Setup Race Track or one of the city setups).");
            return;
        }

        // Start fresh each time so running this twice doesn't create duplicates.
        GameObject old = CarSetup.FindRoot(scene, "Obstacles");
        if (old != null) Object.DestroyImmediate(old);
        GameObject obstacles = new GameObject("Obstacles");

        // One group at each checkpoint (every second one on long routes), never on the finish line.
        Checkpoint[] gates = lapManager.checkpoints;
        int step = gates.Length > 8 ? 2 : 1;
        int group = 0;
        for (int g = 1; g < gates.Length; g += step)
        {
            Transform gate = gates[g].transform;
            float width = gate.GetComponent<BoxCollider>().size.x;
            Vector3 centre = new Vector3(gate.position.x, 0f, gate.position.z);
            float side = group % 2 == 0 ? -1f : 1f;   // alternate between the left and right side of the road
            group++;

            // Four cones in a slanting line that squeeze the road from the edge inwards...
            for (int j = 0; j < 4; j++)
            {
                Vector3 position = centre + gate.right * (side * width * (0.42f - j * 0.09f)) + gate.forward * (j * 2.5f);
                CreateProp(obstacles, "cone.fbx", "Cone", position, 0.8f, 3f);
            }

            // ...and a heavy crate behind them.
            CreateProp(obstacles, "box.fbx", "Crate", centre + gate.right * (side * width * 0.3f) + gate.forward * 11f, 1f, 25f);
        }

        Debug.Log("Obstacles set up: " + group + " groups of cones and crates.");
    }

    // A loose object with a Rigidbody, so the car can knock it away.
    private static void CreateProp(GameObject parent, string modelFile, string propName, Vector3 position, float height, float mass)
    {
        GameObject prop = new GameObject(propName);
        prop.transform.SetParent(parent.transform);
        prop.transform.position = position;

        GameObject model = PlaceModel(modelFile, prop, height, true);
        if (model == null) return;

        AddBoxCollider(prop, model);
        prop.AddComponent<Rigidbody>().mass = mass;
    }

    // ------------------------------------------------------------------------------------------
    // Traffic: cars that drive round the route in lanes (used by the day city)
    // ------------------------------------------------------------------------------------------

    // route: the corners of a closed loop in driving order. Traffic keeps to the left, as in India:
    // two lanes go the same way as the player, two come the other way.
    internal static void BuildTraffic(GameObject parent, Vector3[] route, Vector3 playerStart)
    {
        float[] laneOffsets = { 1.5f, 4.5f };   // metres from the centre line to the middle of each lane
        float[] laneSpeeds = { 11f, 8f };       // metres per second; the lane by the centre line is the faster one
        const int carsPerLane = 5;

        GameObject traffic = new GameObject("Traffic");
        traffic.transform.SetParent(parent.transform);

        Vector3[] reversed = (Vector3[])route.Clone();
        System.Array.Reverse(reversed);

        System.Random random = new System.Random(11);   // fixed seed: the same traffic every time
        for (int direction = 0; direction < 2; direction++)
        {
            for (int lane = 0; lane < laneOffsets.Length; lane++)
            {
                Vector3[] waypoints = LaneWaypoints(direction == 0 ? route : reversed, laneOffsets[lane]);
                for (int k = 0; k < carsPerLane; k++)
                {
                    // Spread the cars evenly round the loop, shifted a little per lane so they aren't side by side.
                    int index = (k * waypoints.Length / carsPerLane + lane * 2 + direction) % waypoints.Length;
                    int next = (index + 1) % waypoints.Length;
                    if (Vector3.Distance(waypoints[index], playerStart) < 30f) continue;   // keep the start clear

                    GameObject trafficCar = new GameObject("TrafficCar");
                    trafficCar.transform.SetParent(traffic.transform);
                    trafficCar.transform.position = waypoints[index];

                    GameObject model = PlaceModel(TrafficModels[random.Next(TrafficModels.Length)], trafficCar, 4.4f, false);
                    if (model == null) return;
                    AddBoxCollider(trafficCar, model);

                    // Kinematic: the script moves the car; physics only uses it to push other things.
                    Rigidbody rb = trafficCar.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    TrafficCar driver = trafficCar.AddComponent<TrafficCar>();
                    driver.waypoints = waypoints;
                    driver.nextWaypoint = next;
                    driver.cruiseSpeed = laneSpeeds[lane];

                    trafficCar.transform.rotation = Quaternion.LookRotation(waypoints[next] - waypoints[index]);
                }
            }
        }
    }

    // Shifts a loop sideways by "offset" metres to the left of the driving direction,
    // and adds extra points along each straight so cars settle into their lane quickly.
    private static Vector3[] LaneWaypoints(Vector3[] loop, float offset)
    {
        int count = loop.Length;
        Vector3[] corners = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            Vector3 directionIn = (loop[i] - loop[(i - 1 + count) % count]).normalized;
            Vector3 directionOut = (loop[(i + 1) % count] - loop[i]).normalized;
            Vector3 leftIn = Vector3.Cross(directionIn, Vector3.up);
            Vector3 leftOut = Vector3.Cross(directionOut, Vector3.up);

            // Going straight on, move left once; at a corner, move left of both streets.
            bool straight = Vector3.Dot(directionIn, directionOut) > 0.9f;
            corners[i] = loop[i] + (straight ? leftIn : leftIn + leftOut) * offset;
        }

        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            for (int s = 0; s < 4; s++) points.Add(Vector3.Lerp(corners[i], corners[(i + 1) % count], s / 4f));
        }
        return points.ToArray();
    }

    // ------------------------------------------------------------------------------------------
    // Shared helpers for models from the car kit
    // ------------------------------------------------------------------------------------------

    // Adds a kit model under "parent" (which must not be rotated yet), resting on the parent's position.
    // The model is scaled so its height (measureHeight) or its length is "size" metres.
    internal static GameObject PlaceModel(string modelFile, GameObject parent, float size, bool measureHeight)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + modelFile);
        if (asset == null)
        {
            Debug.LogError("Missing model: " + KitFolder + modelFile);
            return null;
        }

        GameObject model = Object.Instantiate(asset, parent.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;

        Bounds bounds = CarSetup.GetBounds(model);
        model.transform.localScale *= size / (measureHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z));

        // Turn the model round if its front wheels are pointing backwards.
        float frontZ = 0f, backZ = 0f;
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
        {
            if (part.name.Contains("wheel-front-")) frontZ += part.bounds.center.z;
            if (part.name.Contains("wheel-back-")) backZ += part.bounds.center.z;
        }
        if (frontZ < backZ) model.transform.Rotate(0f, 180f, 0f, Space.World);

        // Centre it on the parent, with its lowest point at the parent's position.
        bounds = CarSetup.GetBounds(model);
        Vector3 origin = parent.transform.position;
        model.transform.position += new Vector3(origin.x - bounds.center.x, origin.y - bounds.min.y, origin.z - bounds.center.z);

        // All kit models share one colour texture.
        Material material = CarSetup.GetOrCreateMaterial("KenneyKit", Color.white);
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(KitFolder + "Textures/colormap.png");
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = part.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            part.sharedMaterials = materials;
        }
        return model;
    }

    // One box collider on "root" that encloses the whole model.
    private static void AddBoxCollider(GameObject root, GameObject model)
    {
        Bounds bounds = CarSetup.GetBounds(model);
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = bounds.center - root.transform.position;
        box.size = bounds.size;
    }
}
