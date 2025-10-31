using UnityEditor;
using UnityEngine;

public class MeshWeightTransferAndAssign : EditorWindow
{
    enum Mode { ByIndex, ByNearestVertexLocal }

    // Inputs
    Mesh sourceSkinnedMesh;   // has boneWeights + bindposes
    Mesh targetGeometryMesh;  // remodeled geometry (no weights)
    Mode mode = Mode.ByNearestVertexLocal;
    int progressEvery = 5000;

    // Optional assignment
    bool assignToRenderer = false;
    SkinnedMeshRenderer targetRenderer; // optional: assign the new mesh to this SMR

    [MenuItem("Tools/Skinning/Mesh → Mesh (Create Skinned Asset & Assign)")]
    static void Open() => GetWindow<MeshWeightTransferAndAssign>("Mesh → Mesh Skinned");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Create a NEW skinned Mesh asset by copying weights/bindposes", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        sourceSkinnedMesh = (Mesh)EditorGUILayout.ObjectField("Source Mesh (skinned)", sourceSkinnedMesh, typeof(Mesh), false);
        targetGeometryMesh = (Mesh)EditorGUILayout.ObjectField("Target Mesh (geometry)", targetGeometryMesh, typeof(Mesh), false);

        mode = (Mode)EditorGUILayout.EnumPopup("Transfer Mode", mode);
        progressEvery = Mathf.Max(200, EditorGUILayout.IntField("Progress Every N Verts", progressEvery));

        EditorGUILayout.Space();
        assignToRenderer = EditorGUILayout.ToggleLeft("Also assign to a SkinnedMeshRenderer / Prefab", assignToRenderer);
        using (new EditorGUI.DisabledScope(!assignToRenderer))
        {
            targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target Renderer (optional)", targetRenderer, typeof(SkinnedMeshRenderer), true);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!sourceSkinnedMesh || !targetGeometryMesh))
        {
            if (GUILayout.Button("Generate NEW Skinned Mesh Asset"))
            {
                try
                {
                    var newAsset = GenerateSkinnedAsset(sourceSkinnedMesh, targetGeometryMesh, mode, progressEvery);
                    if (newAsset)
                    {
                        EditorGUIUtility.PingObject(newAsset);
                        Debug.Log($"Created skinned mesh: {AssetDatabase.GetAssetPath(newAsset)}");

                        if (assignToRenderer && targetRenderer)
                        {
                            AssignToRendererAndSave(targetRenderer, newAsset);
                        }

                        EditorUtility.DisplayDialog("Done", "New skinned mesh asset created."
                            + (assignToRenderer && targetRenderer ? "\nAssigned to renderer / prefab." : ""), "OK");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Error", ex.Message, "OK");
                }
                finally { EditorUtility.ClearProgressBar(); }
            }
        }

        EditorGUILayout.HelpBox(
            "By Index → identical vertex order/count.\n" +
            "By Nearest (local) → works if meshes align in local space.\n\n" +
            "The tool forces Read/Write Enabled on the new .asset so prefab assignments persist.\n" +
            "If assigning to a Model Prefab (FBX), a Prefab Variant will be created automatically.",
            MessageType.Info);
    }

    // --- Core generation ---
    static Mesh GenerateSkinnedAsset(Mesh src, Mesh dstGeom, Mode mode, int progressEvery)
    {
        if (!src) throw new System.Exception("Source mesh is null.");
        if (!dstGeom) throw new System.Exception("Target geometry mesh is null.");

        var srcWeights = src.boneWeights;
        var srcBind = src.bindposes;

        if (srcWeights == null || srcWeights.Length == 0)
            throw new System.Exception("Source mesh has no boneWeights.");
        if (srcBind == null || srcBind.Length == 0)
            throw new System.Exception("Source mesh has no bindposes.");

        var outMesh = Object.Instantiate(dstGeom);
        outMesh.name = dstGeom.name + "_SkinnedCopy";

        BoneWeight[] outWeights;

        if (mode == Mode.ByIndex)
        {
            if (src.vertexCount != outMesh.vertexCount)
                throw new System.Exception("ByIndex mode requires identical vertex counts.");
            if (srcWeights.Length != outMesh.vertexCount)
                throw new System.Exception("Source weights length != target vertex count.");
            outWeights = (BoneWeight[])srcWeights.Clone();
        }
        else // nearest in LOCAL space
        {
            var srcV = src.vertices;
            var dstV = outMesh.vertices;

            if (srcV == null || srcV.Length == 0 || dstV == null || dstV.Length == 0)
                throw new System.Exception("Missing vertices on source or target.");

            outWeights = new BoneWeight[dstV.Length];

            float lastP = -1f;
            for (int i = 0; i < dstV.Length; i++)
            {
                if (i % Mathf.Max(1, progressEvery) == 0)
                {
                    float p = i / (float)dstV.Length;
                    if (p - lastP > 0.01f)
                    {
                        EditorUtility.DisplayProgressBar("Transferring Weights (Nearest, local)",
                            $"Vertex {i}/{dstV.Length}", p);
                        lastP = p;
                    }
                }

                int nearest = FindNearestLocal(srcV, dstV[i]);
                outWeights[i] = srcWeights[nearest];
            }

            static int FindNearestLocal(Vector3[] cloud, Vector3 point)
            {
                int best = 0;
                float bestD = (cloud[0] - point).sqrMagnitude;
                for (int k = 1; k < cloud.Length; k++)
                {
                    float d = (cloud[k] - point).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = k; }
                }
                return best;
            }
        }

        outMesh.boneWeights = outWeights;
        outMesh.bindposes = (Matrix4x4[])srcBind.Clone();
        outMesh.RecalculateBounds();

        // Save asset
        var path = EditorUtility.SaveFilePanelInProject(
            "Save NEW skinned mesh asset",
            outMesh.name + ".asset",
            "asset",
            "Choose where to save the new Mesh asset (.asset).");

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(outMesh, path);

            // Ensure Read/Write Enabled so prefab assignments persist
            ForceMeshReadable(outMesh);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogWarning("Save canceled; mesh exists only in memory for this session.");
        }

        return outMesh;
    }

    // Force Read/Write Enabled on a Mesh asset
    static void ForceMeshReadable(Mesh mesh)
    {
        if (!mesh) return;
        var so = new SerializedObject(mesh);
        var prop = so.FindProperty("m_IsReadable");
        if (prop != null && !prop.boolValue)
        {
            prop.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
            Debug.Log($"Enabled Read/Write on mesh: {mesh.name}");
        }
    }

    // --- Assignment & prefab handling ---
    static void AssignToRendererAndSave(SkinnedMeshRenderer smr, Mesh meshAsset)
    {
        if (!smr) throw new System.Exception("Target renderer is null.");
        if (!meshAsset) throw new System.Exception("Mesh asset is null.");
        if (!AssetDatabase.Contains(meshAsset))
            throw new System.Exception("The mesh is not a saved Project asset. Save it first.");

        // Assign
        smr.sharedMesh = meshAsset;
        PrefabUtility.RecordPrefabInstancePropertyModifications(smr);

        // Determine prefab context
        var root = PrefabUtility.GetNearestPrefabInstanceRoot(smr.gameObject);
        if (!root)
        {
            // Not a prefab instance (editing in Prefab Mode or a scene-only object)
            EditorUtility.SetDirty(smr);
            AssetDatabase.SaveAssets();
            return;
        }

        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(root);
        var assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
        var status = PrefabUtility.GetPrefabInstanceStatus(root);

        if (assetType == PrefabAssetType.Regular && status == PrefabInstanceStatus.Connected)
        {
            // Regular prefab → Apply works
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned mesh and applied to prefab: {prefabAsset.name}");
        }
        else if (assetType == PrefabAssetType.Model)
        {
            // Model prefab (FBX) → create variant and assign there
            var suggested = prefabAsset.name + "_Variant";
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Prefab Variant",
                suggested + ".prefab",
                "prefab",
                "Model Prefabs cannot be modified. Choose a path to save a Prefab Variant.");

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("Canceled creating Prefab Variant; assignment exists only in this editing session.");
                return;
            }

            bool success;
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction, out success);
            if (!success) throw new System.Exception("Failed to create Prefab Variant.");

            // Open variant, assign, save, close
            var variantRoot = PrefabUtility.LoadPrefabContents(path);
            var variantSmr = variantRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!variantSmr) throw new System.Exception("Variant has no SkinnedMeshRenderer.");
            variantSmr.sharedMesh = meshAsset;
            PrefabUtility.SaveAsPrefabAsset(variantRoot, path);
            PrefabUtility.UnloadPrefabContents(variantRoot);

            AssetDatabase.SaveAssets();
            Debug.Log($"Created Prefab Variant and assigned mesh: {path}");
        }
        else
        {
            // Unpacked or not a prefab
            EditorUtility.SetDirty(smr);
            AssetDatabase.SaveAssets();
        }
    }
}
