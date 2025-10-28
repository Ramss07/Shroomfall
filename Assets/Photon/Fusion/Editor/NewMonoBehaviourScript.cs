// Assets/Editor/CombinePrefabMeshes.cs
// Combines every MeshFilter and SkinnedMeshRenderer under a prefab (or scene object)
// into a single Mesh (preserving materials as submeshes) and saves a new prefab + mesh asset.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Presets;

public static class CombinePrefabMeshes
{
    [MenuItem("Tools/Combine Meshes/From Selected Prefab (Create New Prefab)")]
    public static void CombineFromSelectedPrefab()
    {
        var selected = Selection.activeObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "Select a prefab asset in the Project window.", "OK");
            return;
        }

        var path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path) || PrefabUtility.GetPrefabAssetType(selected) == PrefabAssetType.NotAPrefab)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "The selected asset is not a prefab.", "OK");
            return;
        }

        // Load the prefab contents into a temporary editing stage
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var result = CombineUnderRoot(root.transform, preserveInactive: false);
            if (result == null)
            {
                EditorUtility.DisplayDialog("Combine Meshes", "No valid renderers/meshes found to combine.", "OK");
                return;
            }

            // Create a new root with a single renderer/filter
            var combinedGo = new GameObject(root.name + "_Combined");
            combinedGo.transform.position = Vector3.zero;
            combinedGo.transform.rotation = Quaternion.identity;
            combinedGo.transform.localScale = Vector3.one;

            var mf = combinedGo.AddComponent<MeshFilter>();
            var mr = combinedGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = result.Mesh;
            mr.sharedMaterials = result.Materials;

            // Save the mesh asset next to the original prefab
            var dir = System.IO.Path.GetDirectoryName(path);
            var meshAssetPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(dir, root.name + "_Combined.asset"));
            AssetDatabase.CreateAsset(result.Mesh, meshAssetPath);
            AssetDatabase.SaveAssets();

            // Save as a new prefab next to the original
            var newPrefabPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(dir, root.name + "_Combined.prefab"));
            var newPrefab = PrefabUtility.SaveAsPrefabAsset(combinedGo, newPrefabPath);
            Object.DestroyImmediate(combinedGo);

            EditorUtility.DisplayDialog("Combine Meshes", $"Created:\n- Mesh: {meshAssetPath}\n- Prefab: {newPrefabPath}", "Nice");
            EditorGUIUtility.PingObject(newPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Combine Meshes/From Selected Scene Object (Create New Prefab)")]
    public static void CombineFromSelectedSceneObject()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "Select a root GameObject in the Hierarchy.", "OK");
            return;
        }

        var result = CombineUnderRoot(go.transform, preserveInactive: true);
        if (result == null)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "No valid renderers/meshes found to combine.", "OK");
            return;
        }

        // Build output GO
        var combinedGo = new GameObject(go.name + "_Combined");
        combinedGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        combinedGo.transform.localScale = Vector3.one;

        var mf = combinedGo.AddComponent<MeshFilter>();
        var mr = combinedGo.AddComponent<MeshRenderer>();
        mf.sharedMesh = result.Mesh;
        mr.sharedMaterials = result.Materials;

        // Ask user where to save
        var savePath = EditorUtility.SaveFilePanelInProject(
            "Save Combined Prefab",
            go.name + "_Combined.prefab",
            "prefab",
            "Choose location for the combined prefab");

        if (string.IsNullOrEmpty(savePath))
        {
            Object.DestroyImmediate(combinedGo);
            return;
        }

        // Save mesh alongside prefab
        var dir = System.IO.Path.GetDirectoryName(savePath);
        var meshAssetPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(dir, go.name + "_Combined.asset"));
        AssetDatabase.CreateAsset(result.Mesh, meshAssetPath);
        AssetDatabase.SaveAssets();

        var newPrefab = PrefabUtility.SaveAsPrefabAsset(combinedGo, savePath);
        Object.DestroyImmediate(combinedGo);

        EditorUtility.DisplayDialog("Combine Meshes", $"Created:\n- Mesh: {meshAssetPath}\n- Prefab: {savePath}", "Nice");
        EditorGUIUtility.PingObject(newPrefab);
    }

    /// <summary>
    /// Combines all MeshFilters and SkinnedMeshRenderers under root into one Mesh with multiple submeshes.
    /// Materials are preserved as submeshes; transforms are baked relative to root.
    /// </summary>
    private static CombineResult CombineUnderRoot(Transform root, bool preserveInactive)
    {
        var rootToWorld = root.localToWorldMatrix;
        var worldToRoot = root.worldToLocalMatrix;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: preserveInactive);
        if (renderers == null || renderers.Length == 0)
            return null;

        // Group geometry by material so we can preserve submeshes/materials
        var materialToCombis = new Dictionary<Material, List<CombineInstance>>();

        // Helper to register a mesh part against a material
        void AddCombine(Mesh mesh, int subMeshIndex, Material mat, Matrix4x4 toWorld)
        {
            if (mesh == null || subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount) return;
            if (!materialToCombis.TryGetValue(mat, out var list))
            {
                list = new List<CombineInstance>();
                materialToCombis[mat] = list;
            }

            var ci = new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = subMeshIndex,
                transform = worldToRoot * toWorld // bake into root space
            };
            list.Add(ci);
        }

        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue; // skip particles
            if (!r.gameObject.activeInHierarchy && !preserveInactive) continue;
            if (!r.enabled) continue;

            var mats = r.sharedMaterials; // may be longer than mesh submesh count; Unity ignores extras

            if (r is MeshRenderer mr)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var mesh = mf.sharedMesh;
                var toWorld = mf.transform.localToWorldMatrix;

                var subCount = mesh.subMeshCount;
                for (int i = 0; i < subCount; i++)
                {
                    var mat = i < mats.Length ? mats[i] : null;
                    AddCombine(mesh, i, mat, toWorld);
                }
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;

                // Bake skinned state to a temporary mesh in world space at editor time.
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                baked.name = (smr.sharedMesh.name + "_Baked");

                var subCount = baked.subMeshCount;
                var toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, smr.transform.lossyScale);

                for (int i = 0; i < subCount; i++)
                {
                    var mat = i < smr.sharedMaterials.Length ? smr.sharedMaterials[i] : null;
                    AddCombine(baked, i, mat, toWorld);
                }
            }
        }

        // Nothing to combine?
        if (materialToCombis.Count == 0) return null;

        // Step 1: For each material group, combine into one submesh mesh in root space.
        var subMeshes = new List<Mesh>();
        var materials = new List<Material>();

        foreach (var kvp in materialToCombis)
        {
            var mat = kvp.Key;
            var list = kvp.Value;
            if (list.Count == 0) continue;

            var sub = new Mesh();
            sub.name = (root.name + "_Sub_" + (mat ? mat.name : "NoMat"));
            // mergeSubMeshes=true inside each material group, useMatrices=true
            sub.CombineMeshes(list.ToArray(), true, true, false);
            subMeshes.Add(sub);
            materials.Add(mat);
        }

        // Step 2: Combine submeshes (each with a single submesh) into the final mesh with multiple submeshes.
        var finals = new List<CombineInstance>();
        foreach (var sm in subMeshes)
        {
            finals.Add(new CombineInstance
            {
                mesh = sm,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
        }

        var finalMesh = new Mesh();
        finalMesh.name = root.name + "_Combined";
        // mergeSubMeshes = false to preserve each material group as a separate submesh
        finalMesh.CombineMeshes(finals.ToArray(), false, true, false);

        // Clean up temp sub-meshes (their data is copied into finalMesh)
        foreach (var sm in subMeshes) Object.DestroyImmediate(sm);

        // Recalculate bounds to be safe (normals/tangents/uvs are preserved by CombineMeshes)
        finalMesh.RecalculateBounds();

        return new CombineResult
        {
            Mesh = finalMesh,
            Materials = materials.ToArray()
        };
    }

    private class CombineResult
    {
        public Mesh Mesh;
        public Material[] Materials;
    }
}
#endif
