using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using UnityEditor.SceneManagement;

public class RenderToImageUtility
{
    [MenuItem("Render to image/Game to PNG")]
    static void RenderToPNG()
    {
        // Find target object
        var targets = Selection.gameObjects;

        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Attention", "Use Inspector window to select objects for render.", "Ok");
            return;
        }

        var targetsCache = new List<LayerCache>();

        foreach (var target in targets)
        {
            targetsCache.Add(new LayerCache(target));
            target.SetLayerRecursevly(LayerMask.NameToLayer("Water"));
        }
        // Find main camera
        Camera mainCam = Camera.main;
        //Copy mainCam
        Camera camCopy = GameObject.Instantiate(mainCam);
        //camCopy.CopyFrom(mainCam);
        camCopy.gameObject.SetActive(false);
        // Set camera render to Depth only
#if UNITY_2021_1_OR_NEWER
        camCopy.clearFlags = CameraClearFlags.Nothing;
#else
        camCopy.clearFlags = CameraClearFlags.Depth;
#endif
        // Set camra render layer to Water
        camCopy.cullingMask = 1 << LayerMask.NameToLayer("Water");
        // Force camera to render
        RenderTexture renderTexture = new RenderTexture(camCopy.pixelWidth, camCopy.pixelHeight, 24);
        camCopy.targetTexture = renderTexture;
        camCopy.forceIntoRenderTexture = true;
        camCopy.Render();
        // Get path to save folder
        string pathToSaveFolder = EditorUtility.SaveFilePanel("Save output", "", "screenshot", "png");
        // Save render output
        DumpRenderTexture(renderTexture, pathToSaveFolder);
        // Clean up
        GameObject.DestroyImmediate(camCopy.gameObject);

        foreach (var targetCache in targetsCache)
        {
            targetCache.RecoverLayer();
        }
    }

    [MenuItem("Render to image/Scene to PNG")]
    public static void RenderFromSceneView()
    {
        Light light = null;

        bool useCustomLight = !SceneView.lastActiveSceneView.sceneLighting;

        if (useCustomLight)
        {
            light = new GameObject("light").AddComponent<Light>();
            StageUtility.PlaceGameObjectInCurrentStage(light.gameObject);
            light.type = LightType.Directional;
            light.color = new Color(100f / 255f, 100f / 255f, 100f / 255f);
            light.intensity = 2f;
            light.transform.SetPositionAndRotation(SceneView.GetAllSceneCameras()[0].transform.position, SceneView.GetAllSceneCameras()[0].transform.rotation);
        }

        Camera camCopy = SceneView.GetAllSceneCameras()[0];
        camCopy.clearFlags = CameraClearFlags.Depth;
        RenderTexture renderTexture = camCopy.targetTexture;
        camCopy.targetTexture = renderTexture;
        camCopy.forceIntoRenderTexture = true;
        camCopy.Render();
        camCopy.forceIntoRenderTexture = false;
        // Get path to save folder
        string pathToSaveFolder = EditorUtility.SaveFilePanel("Save output", "", "screenshot", "png");
        // Save render output
        DumpRenderTexture(renderTexture, pathToSaveFolder);

        if (useCustomLight)
        {
            MonoBehaviour.DestroyImmediate(light.gameObject);
        }
        // Clean up
        camCopy.clearFlags = CameraClearFlags.Skybox;
    }

    public static void DumpRenderTexture(RenderTexture rt, string pngOutPath)
    {
        var tex = new Texture2D(rt.width, rt.height);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(pngOutPath, tex.EncodeToPNG());
    }

    public class LayerCache
    {
        public GameObject Target;
        public int Layer;
        public List<LayerCache> Children;

        public LayerCache(GameObject target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Layer = Target.layer;
            Children = new List<LayerCache>();

            for (int i = 0; i < Target.transform.childCount; i++)
            {
                LayerCache childCache = new LayerCache(Target.transform.GetChild(i).gameObject);
                Children.Add(childCache);
            }
        }

        public void RecoverLayer()
        {
            Target.layer = Layer;

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].RecoverLayer();
            }
        }
    }
}

public static class GameObjectExtensions
{
    public static void SetLayerRecursevly(this GameObject target, int layer)
    {
        target.layer = layer;

        for (int i = 0; i < target.transform.childCount; i++)
        {
            target.transform.GetChild(i).gameObject.SetLayerRecursevly(layer);
        }
    }
}
