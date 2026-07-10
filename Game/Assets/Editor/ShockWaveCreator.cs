using UnityEngine;
using UnityEditor;

/// <summary>
/// ShockWave ParticleSystem Prefab Creator.
/// Menu: Tools > Create ShockWave Prefab
/// Assets/ShockWave/ に TX_ShockWave.png と MS_ShockWave.fbx が必要.
/// 既存の ShockWave.prefab と MT_ShockWave.mat は上書きされる.
/// </summary>
public static class ShockWaveCreator
{
    [MenuItem("Tools/Create ShockWave Prefab")]
    public static void CreateShockWavePrefab()
    {
        // --- Asset paths ---
        const string vfxFolder = "Assets/ShockWave";
        const string texPath = vfxFolder + "/TX_ShockWave.png";
        const string fbxPath = vfxFolder + "/MS_ShockWave.fbx";
        const string matPath = vfxFolder + "/MT_ShockWave.mat";
        const string prefabPath = vfxFolder + "/ShockWave.prefab";

        // --- Load texture ---
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            Debug.LogError($"[ShockWaveCreator] Texture not found: {texPath}");
            return;
        }

        // --- Load FBX mesh ---
        var fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        Mesh mesh = null;
        if (fbxObj != null)
        {
            var mf = fbxObj.GetComponentInChildren<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mesh == null)
        {
            Debug.LogError($"[ShockWaveCreator] Mesh not found in: {fbxPath}");
            return;
        }

        // --- Delete existing mat/prefab to avoid version conflict ---
        if (AssetDatabase.LoadAssetAtPath<Object>(matPath) != null)
            AssetDatabase.DeleteAsset(matPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(prefabPath) != null)
            AssetDatabase.DeleteAsset(prefabPath);

        // --- Create Material (URP Particles/Unlit for 2D Renderer compatibility) ---
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null)
        {
            Debug.LogError("[ShockWaveCreator] No particle shader found.");
            return;
        }
        var mat = new Material(particleShader);
        mat.name = "MT_ShockWave";
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);
        AssetDatabase.CreateAsset(mat, matPath);
        Debug.Log($"[ShockWaveCreator] Material created: {matPath}");

        // --- Create GameObject ---
        // 2D カメラ (Z軸方向) でメッシュリング (XZ平面) を表示するため
        // 90° X回転で XZ → XY 平面にマッピング.
        var go = new GameObject("ShockWave");
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // --- ParticleSystem ---
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = true;
        main.startDelay = 0f;
        main.startLifetime = 0.3f;
        main.startSpeed = 0.5f;
        main.startSize = 1f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 1000;

        // --- Emission ---
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 1, 1, 1, 0.01f)
        });

        // --- Shape: Circle, rotated 90 on X, radius ~0 ---
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.radius = 0.0001f;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        shape.angle = 1f;

        // --- Size over Lifetime (Separate Axes) ---
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.separateAxes = true;

        // X: 0 -> 1, start slope=2
        var xCurve = new AnimationCurve();
        var xKey0 = new Keyframe(0f, 0f) { inTangent = 2f, outTangent = 2f };
        xCurve.AddKey(xKey0);
        var xKey1 = new Keyframe(1f, 1f) { inTangent = 0f, outTangent = 0f };
        xCurve.AddKey(xKey1);
        sol.x = new ParticleSystem.MinMaxCurve(1f, xCurve);

        // Y: constant 1
        sol.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));

        // Z: same as X
        var zCurve = new AnimationCurve();
        var zKey0 = new Keyframe(0f, 0f) { inTangent = 2f, outTangent = 2f };
        zCurve.AddKey(zKey0);
        var zKey1 = new Keyframe(1f, 1f) { inTangent = 0f, outTangent = 0f };
        zCurve.AddKey(zKey1);
        sol.z = new ParticleSystem.MinMaxCurve(1f, zCurve);

        // --- Disable unused modules ---
        var col = ps.colorOverLifetime; col.enabled = false;
        var rot = ps.rotationOverLifetime; rot.enabled = false;
        var vel = ps.velocityOverLifetime; vel.enabled = false;
        var fol = ps.forceOverLifetime; fol.enabled = false;
        var lbl = ps.limitVelocityOverLifetime; lbl.enabled = false;
        var iv = ps.inheritVelocity; iv.enabled = false;
        var noise = ps.noise; noise.enabled = false;
        var sbs = ps.sizeBySpeed; sbs.enabled = false;
        var trail = ps.trails; trail.enabled = false;
        var trig = ps.trigger; trig.enabled = false;
        var lights = ps.lights; lights.enabled = false;
        var sub = ps.subEmitters; sub.enabled = false;
        var ta = ps.textureSheetAnimation; ta.enabled = false;
        var ef = ps.externalForces; ef.enabled = false;
        var cd = ps.customData; cd.enabled = false;
        var les = ps.lifetimeByEmitterSpeed; les.enabled = false;

        // --- Renderer: Mesh mode ---
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Mesh;
        psr.mesh = mesh;
        psr.alignment = ParticleSystemRenderSpace.Local;
        psr.material = mat;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.enableGPUInstancing = true;
        psr.sortingOrder = 100;

        // --- Save as Prefab ---
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        if (prefab != null)
        {
            Debug.Log($"[ShockWaveCreator] Prefab created successfully: {prefabPath}");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            Debug.LogError("[ShockWaveCreator] Failed to create prefab.");
        }

        AssetDatabase.Refresh();
    }
}
