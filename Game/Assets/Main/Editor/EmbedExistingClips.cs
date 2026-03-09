using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class EmbedClipWindow : EditorWindow
{
    private AnimatorController targetController;
    private List<AnimationClip> clipsToEmbed = new List<AnimationClip>();

    [MenuItem("Tools/Embed AnimationClips Into Controller")]
    static void ShowWindow()
    {
        GetWindow<EmbedClipWindow>("Embed Clips");
    }

    void OnGUI()
    {
        GUILayout.Label("Animator Controller", EditorStyles.boldLabel);
        targetController = (AnimatorController)EditorGUILayout.ObjectField("Controller", targetController, typeof(AnimatorController), false);

        GUILayout.Label("Animation Clips to Embed", EditorStyles.boldLabel);

        // Clip List の表示
        int removeIndex = -1;
        for (int i = 0; i < clipsToEmbed.Count; i++)
        {
            GUILayout.BeginHorizontal();
            clipsToEmbed[i] = (AnimationClip)EditorGUILayout.ObjectField(clipsToEmbed[i], typeof(AnimationClip), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                removeIndex = i;
            }
            GUILayout.EndHorizontal();
        }
        if (removeIndex >= 0)
        {
            clipsToEmbed.RemoveAt(removeIndex);
        }

        if (GUILayout.Button("＋ Add Clip"))
        {
            clipsToEmbed.Add(null);
        }

        GUILayout.Space(10);
        if (GUILayout.Button("▶ Embed Clips"))
        {
            EmbedClips();
        }
    }

    void EmbedClips()
    {
        if (targetController == null)
        {
            Debug.LogError("AnimatorController が未設定です");
            return;
        }

        int count = 0;
        foreach (var clip in clipsToEmbed)
        {
            if (clip == null) continue;

            // Sub-Asset として埋め込み
            AssetDatabase.AddObjectToAsset(clip, targetController);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{count} 個の AnimationClip を {targetController.name} に埋め込みました！");
    }
}
