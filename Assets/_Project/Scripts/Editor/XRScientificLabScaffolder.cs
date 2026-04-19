#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

public static class XRScientificLabScaffolder
{
    private const string MarkerKey = "XRScientificLabScaffolded_v1";

    [InitializeOnLoadMethod]
    private static void Bootstrap()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(MarkerKey, false))
            {
                return;
            }

            try
            {
                BuildAll();
                EditorPrefs.SetBool(MarkerKey, true);
                Debug.Log("XR_SIMULATOR_READY");
            }
            catch (Exception ex)
            {
                Debug.LogError($"XR lab scaffolding failed: {ex}");
            }
        };
    }

    [MenuItem("Tools/XR Scientific Lab/Rebuild Full Project")]
    public static void BuildAll()
    {
        EnsureFolders();
        ConfigureProjectSettings();
        RepairProjectMaterials();

        Material positiveMat = CreateChargeMaterial("ChargePositive", new Color(1f, 0.2f, 0.2f), new Color(5f, 0f, 0f));
        Material negativeMat = CreateChargeMaterial("ChargeNegative", new Color(0.2f, 0.4f, 1f), new Color(0f, 1f, 5f));
        Material arrowMat = CreateChargeMaterial("ArrowMat", new Color(0.2f, 1f, 1f), new Color(0f, 0.4f, 0.8f));
        Material fieldLineMat = CreateChargeMaterial("FieldLineMat", new Color(1f, 0.8f, 0f), new Color(0.7f, 0.5f, 0f));
        Material wallMat = CreateChargeMaterial("LabWall", new Color(0.75f, 0.75f, 0.78f), Color.black);
        Material floorMat = CreateChargeMaterial("LabFloor", new Color(0.45f, 0.45f, 0.45f), Color.black);
        Material gradientMat = CreateChargeMaterial("ColorGradientField", new Color(0.1f, 0.5f, 0.5f), new Color(0.3f, 0.25f, 0f));
        gradientMat.SetFloat("_FieldStrength", 0f);
        EditorUtility.SetDirty(gradientMat);

        var posPrefab = CreateChargePrefab("PositiveCharge", positiveMat, ChargeType.Positive);
        var negPrefab = CreateChargePrefab("NegativeCharge", negativeMat, ChargeType.Negative);
        var arrowPrefab = CreateArrowPrefab(arrowMat);
        var fieldLinePrefab = CreateFieldLinePrefab(fieldLineMat);

        var actionsAsset = CreateInputActionsAsset();

        BuildScene(posPrefab, negPrefab, arrowPrefab, fieldLineMat, wallMat, floorMat, actionsAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/_Project",
            "Assets/_Project/Scripts",
            "Assets/_Project/Scripts/Core",
            "Assets/_Project/Scripts/Visualization",
            "Assets/_Project/Scripts/Interaction",
            "Assets/_Project/Scripts/UI",
            "Assets/_Project/Prefabs",
            "Assets/_Project/Materials",
            "Assets/_Project/Scenes",
            "Assets/_Project/InputActions"
        };

        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
                string name = System.IO.Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }

    private static void ConfigureProjectSettings()
    {
        ConfigureRenderPipelineAssignment();

        PlayerSettings.companyName = "Codex";
        PlayerSettings.productName = "XRElectricFieldSim";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.SetArchitecture(BuildTargetGroup.Android, 2); // ARM64 only
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;

        SetLayerName(8, "Charge");
        Physics.IgnoreLayerCollision(8, 8, true);

        var burstAot = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/BurstAotSettings_Android.json");
        if (burstAot != null)
        {
            // Presence check only; Burst package manages this file.
        }
    }

    private static void ConfigureRenderPipelineAssignment()
    {
        string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset");
        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning("No RenderPipelineAsset found; skipping URP assignment.");
            return;
        }

        RenderPipelineAsset selected = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var candidate = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (candidate == null) continue;

            // Prefer URP-style assets when available.
            if (path.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("URP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                selected = candidate;
                break;
            }

            if (selected == null)
            {
                selected = candidate;
            }
        }

        if (selected == null)
        {
            Debug.LogWarning("RenderPipelineAsset lookup returned no valid asset.");
            return;
        }

        GraphicsSettings.defaultRenderPipeline = selected;
        QualitySettings.renderPipeline = selected;
    }

    private static void SetLayerName(int index, string name)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");
        if (layersProp != null && index < layersProp.arraySize)
        {
            SerializedProperty layer = layersProp.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tagManager.ApplyModifiedProperties();
            }
        }
    }

    private static Material CreateChargeMaterial(string name, Color baseColor, Color emission)
    {
        string path = $"Assets/_Project/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = ResolvePreferredUnlitShader();
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            var preferredShader = ResolvePreferredUnlitShader();
            if (preferredShader != null && mat.shader != preferredShader)
            {
                mat.shader = preferredShader;
            }
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Shader ResolvePreferredUnlitShader()
    {
        if (IsUrpPipelineActive())
        {
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit != null) return urpUnlit;
        }

        Shader standard = Shader.Find("Standard");
        if (standard != null) return standard;

        return Shader.Find("Unlit/Color");
    }

    private static void RepairProjectMaterials()
    {
        string[] materialPaths =
        {
            "Assets/_Project/Materials/ChargePositive.mat",
            "Assets/_Project/Materials/ChargeNegative.mat",
            "Assets/_Project/Materials/ArrowMat.mat",
            "Assets/_Project/Materials/FieldLineMat.mat",
            "Assets/_Project/Materials/ColorGradientField.mat",
            "Assets/_Project/Materials/LabWall.mat",
            "Assets/_Project/Materials/LabFloor.mat"
        };

        Shader shader = ResolvePreferredUnlitShader();
        if (shader == null) return;

        foreach (string path in materialPaths)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            if (mat.shader == shader) continue;
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }
    }

    private static bool IsUrpPipelineActive()
    {
        var rp = GraphicsSettings.defaultRenderPipeline != null
            ? GraphicsSettings.defaultRenderPipeline
            : QualitySettings.renderPipeline;
        if (rp == null) return false;
        return rp.GetType().Name.IndexOf("UniversalRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static GameObject CreateChargePrefab(string name, Material material, ChargeType type)
    {
        string path = $"Assets/_Project/Prefabs/{name}.prefab";
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = name;
        root.transform.localScale = Vector3.one * 0.12f;

        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        var sphereCollider = root.GetComponent<SphereCollider>();
        sphereCollider.radius = 0.06f;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var grab = root.AddComponent<XRGrabInteractable>();
        grab.trackPosition = true;
        grab.trackRotation = false;

        var charge = root.AddComponent<ChargeObject>();
        charge.chargeType = type;

        root.layer = LayerMask.NameToLayer("Charge");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateArrowPrefab(Material arrowMat)
    {
        string path = "Assets/_Project/Prefabs/ArrowInstance.prefab";
        var root = new GameObject("ArrowInstance");

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        shaft.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);

        var tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tip.name = "Tip";
        tip.transform.SetParent(root.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0.27f, 0f);
        tip.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterial = arrowMat;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateFieldLinePrefab(Material mat)
    {
        string path = "Assets/_Project/Prefabs/FieldLinePrefab.prefab";
        var root = new GameObject("FieldLinePrefab");
        var lr = root.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.widthMultiplier = 0.005f;
        lr.useWorldSpace = true;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        return prefab;
    }

    private static InputActionAsset CreateInputActionsAsset()
    {
        string path = "Assets/_Project/InputActions/XRInputActions.inputactions";
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var existingMap = asset.FindActionMap("XRActions", true);
        if (existingMap != null)
        {
            asset.RemoveActionMap(existingMap);
        }

        var map = new InputActionMap("XRActions");

        var grab = map.AddAction("Grab", InputActionType.Button);
        grab.AddBinding("<XRController>{LeftHand}/gripPressed");
        grab.AddBinding("<XRController>{RightHand}/gripPressed");
        grab.AddBinding("<Keyboard>/e");

        var uiNavigate = map.AddAction("UINavigate", InputActionType.Value);
        uiNavigate.expectedControlType = "Vector2";
        uiNavigate.AddBinding("<XRController>{LeftHand}/thumbstick");
        uiNavigate.AddBinding("<XRController>{RightHand}/thumbstick");
        uiNavigate.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        var confirm = map.AddAction("Confirm", InputActionType.Button);
        confirm.AddBinding("<XRController>{LeftHand}/triggerPressed");
        confirm.AddBinding("<XRController>{RightHand}/triggerPressed");
        confirm.AddBinding("<Keyboard>/space");

        var teleportStart = map.AddAction("TeleportStart", InputActionType.Button);
        teleportStart.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
        teleportStart.AddBinding("<XRController>{RightHand}/primary2DAxisClick");
        teleportStart.AddBinding("<Keyboard>/t");

        asset.AddActionMap(map);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void BuildScene(GameObject positivePrefab, GameObject negativePrefab, GameObject arrowPrefab, Material fieldLineMat, Material wallMat, Material floorMat, InputActionAsset actionsAsset)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var xrOriginGo = new GameObject("XROrigin");
        var xrOrigin = xrOriginGo.AddComponent<XROrigin>();
        xrOriginGo.AddComponent<MouseDesktopController>();

        var cc = xrOriginGo.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;

        var box = xrOriginGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.5f, 0.5f, 0.5f);

        var cameraOffset = new GameObject("CameraOffset");
        cameraOffset.transform.SetParent(xrOriginGo.transform, false);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.transform.SetParent(cameraOffset.transform, false);
        var cam = cameraGo.AddComponent<Camera>();
        cameraGo.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f, 1f);
        cam.fieldOfView = 90f;
        cameraGo.AddComponent<AudioListener>();
        var trackedPose = cameraGo.AddComponent<TrackedPoseDriver>();
        trackedPose.positionInput = new InputActionProperty(new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition"));
        trackedPose.rotationInput = new InputActionProperty(new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
        trackedPose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

        xrOrigin.Camera = cam;
        xrOrigin.CameraFloorOffsetObject = cameraOffset;

        CreateHandController("LeftHand Controller", xrOriginGo.transform, actionsAsset);
        CreateHandController("RightHand Controller", xrOriginGo.transform, actionsAsset);

        var spawnGo = new GameObject("ChargeSpawnManager");
        var spawnMgr = spawnGo.AddComponent<ChargeSpawnManager>();
        spawnMgr.positiveChargePrefab = positivePrefab;
        spawnMgr.negativeChargePrefab = negativePrefab;

        var fieldComputerGo = new GameObject("FieldComputer");
        fieldComputerGo.AddComponent<ElectricFieldComputer>();

        var lineVizGo = new GameObject("FieldLineRenderer");
        var lineViz = lineVizGo.AddComponent<FieldLineVisualizer>();
        lineViz.fieldLineMaterial = fieldLineMat;

        var arrowVizGo = new GameObject("VectorArrowField");
        var arrowViz = arrowVizGo.AddComponent<VectorArrowVisualizer>();
        arrowViz.arrowPrefab = arrowPrefab;

        BuildLabRoom(wallMat, floorMat);
        EnsureEventSystem();
        BuildHud(spawnMgr, arrowViz, cam);

        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/ElectricFieldLab.unity");

        var smokeSpawn = UnityEngine.Object.FindObjectOfType<ChargeSpawnManager>();
        var smokeField = UnityEngine.Object.FindObjectOfType<ElectricFieldComputer>();
        if (smokeSpawn != null)
        {
            smokeSpawn.SpawnCharge(Vector3.zero, ChargeType.Positive);
            if (smokeField != null)
            {
                float mag = smokeField.ComputeFieldAt(new Vector3(1f, 0f, 0f)).magnitude;
                if (Mathf.Abs(mag - 8990f) > 350f)
                {
                    Debug.LogWarning($"Smoke test field magnitude out of expected range: {mag}");
                }
            }

            if (UnityEngine.Object.FindObjectsOfType<LineRenderer>().Length < 1)
            {
                Debug.LogWarning("Smoke test found no active LineRenderer in scene.");
            }
        }
    }

    private static void CreateHandController(string name, Transform parent, InputActionAsset actionsAsset)
    {
        var hand = new GameObject(name);
        hand.transform.SetParent(parent, false);

        hand.AddComponent<ActionBasedController>();
        hand.AddComponent<XRRayInteractor>();
        hand.AddComponent<LineRenderer>();
        hand.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();

        var sphere = hand.AddComponent<SphereCollider>();
        sphere.radius = 0.05f;
        sphere.isTrigger = true;

        var leftAction = actionsAsset.FindAction("XRActions/Grab", true);
        var rightAction = actionsAsset.FindAction("XRActions/Confirm", true);
        var teleportAction = actionsAsset.FindAction("XRActions/TeleportStart", true);
        var controller = hand.GetComponent<ActionBasedController>();
        if (controller != null)
        {
            controller.selectAction = new InputActionProperty(leftAction);
            controller.activateAction = new InputActionProperty(rightAction);
            controller.uiPressAction = new InputActionProperty(rightAction);
            controller.uiScrollAction = new InputActionProperty(actionsAsset.FindAction("XRActions/UINavigate", true));
            if (teleportAction != null)
            {
                controller.rotateAnchorAction = new InputActionProperty(teleportAction);
            }
        }
    }

    private static void BuildLabRoom(Material wallMat, Material floorMat)
    {
        var room = new GameObject("LabRoom");

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(room.transform, false);
        floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
        floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        CreateWall(room.transform, "Wall_North", new Vector3(0f, 1.5f, 4f), new Vector3(8f, 3f, 0.1f), wallMat);
        CreateWall(room.transform, "Wall_South", new Vector3(0f, 1.5f, -4f), new Vector3(8f, 3f, 0.1f), wallMat);
        CreateWall(room.transform, "Wall_East", new Vector3(4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f), wallMat);
        CreateWall(room.transform, "Wall_West", new Vector3(-4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f), wallMat);

        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(room.transform, false);
        ceiling.transform.localScale = new Vector3(8f, 0.1f, 8f);
        ceiling.transform.localPosition = new Vector3(0f, 3f, 0f);
        ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.name = "SpawnDesk";
        desk.transform.SetParent(room.transform, false);
        desk.transform.localScale = new Vector3(1.2f, 0.6f, 0.75f);
        desk.transform.localPosition = new Vector3(0f, 0.3f, 1.6f);
        desk.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        CreatePointLight(room.transform, new Vector3(3.5f, 2.8f, 3.5f));
        CreatePointLight(room.transform, new Vector3(-3.5f, 2.8f, 3.5f));
        CreatePointLight(room.transform, new Vector3(3.5f, 2.8f, -3.5f));
        CreatePointLight(room.transform, new Vector3(-3.5f, 2.8f, -3.5f));
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void CreatePointLight(Transform parent, Vector3 pos)
    {
        var go = new GameObject("CornerLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.8f, 0.9f, 1f);
        light.intensity = 1.2f;
        light.range = 8f;
    }

    private static void BuildHud(ChargeSpawnManager spawnMgr, VectorArrowVisualizer arrowViz, Camera eventCamera)
    {
        var canvasGo = new GameObject("LabHUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 600f);
        canvasGo.transform.position = new Vector3(0f, 1.5f, 1.0f);
        canvasGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        canvasGo.transform.localScale = Vector3.one * 0.001f;

        var label = CreateLabel(canvasGo.transform, "ActiveChargesLabel", "Active Charges: 0", new Vector2(0f, 220f), 36, TextAnchor.MiddleCenter);

        var addPos = CreateButton(canvasGo.transform, "Add+", "Add + Charge", new Vector2(-300f, 100f));
        var addNeg = CreateButton(canvasGo.transform, "Add-", "Add - Charge", new Vector2(0f, 100f));
        var toggle = CreateButton(canvasGo.transform, "Toggle", "Toggle Arrows", new Vector2(300f, 100f));
        var clear = CreateButton(canvasGo.transform, "Clear", "Clear All", new Vector2(0f, 10f));

        var dropdown = CreateDropdown(canvasGo.transform, new Vector2(0f, -90f), new[] { "Single Charge", "Dipole", "Quadrupole", "Three Random" });

        var presetMgrGo = new GameObject("PresetManager");
        var presetMgr = presetMgrGo.AddComponent<PresetManager>();
        presetMgr.spawnManager = spawnMgr;

        var hud = canvasGo.AddComponent<LabHUDController>();
        hud.spawnManager = spawnMgr;
        hud.arrowVisualizer = arrowViz;
        hud.presetManager = presetMgr;
        hud.addPositiveButton = addPos;
        hud.addNegativeButton = addNeg;
        hud.toggleArrowsButton = toggle;
        hud.clearAllButton = clear;
        hud.presetDropdown = dropdown;
        hud.activeChargeLabel = label;
    }

    private static void EnsureEventSystem()
    {
        var existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        go.AddComponent<XRUIInputModule>();
    }

    private static Text CreateLabel(Transform parent, string name, string text, Vector2 anchoredPos, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500f, 60f);
        rt.anchoredPosition = anchoredPos;
        var textComp = go.AddComponent<Text>();
        textComp.text = text;
        textComp.fontSize = fontSize;
        textComp.color = Color.white;
        textComp.alignment = anchor;
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return textComp;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240f, 70f);
        rt.anchoredPosition = anchoredPos;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.15f, 0.2f, 0.95f);

        var button = go.AddComponent<Button>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var textComp = textGo.AddComponent<Text>();
        textComp.text = label;
        textComp.alignment = TextAnchor.MiddleCenter;
        textComp.fontSize = 24;
        textComp.color = Color.white;
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return button;
    }

    private static Dropdown CreateDropdown(Transform parent, Vector2 anchoredPos, string[] options)
    {
        var go = new GameObject("PresetDropdown");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(450f, 70f);
        rt.anchoredPosition = anchoredPos;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.2f, 0.25f, 0.95f);

        var dropdown = go.AddComponent<Dropdown>();
        var label = CreateLabel(go.transform, "Label", options.Length > 0 ? options[0] : "Preset", Vector2.zero, 24, TextAnchor.MiddleCenter);
        dropdown.captionText = label;
        dropdown.options = new List<Dropdown.OptionData>();
        foreach (var option in options)
        {
            dropdown.options.Add(new Dropdown.OptionData(option));
        }
        return dropdown;
    }
}
#endif
