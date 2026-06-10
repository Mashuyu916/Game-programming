#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class CleanupLegacyRollAnimation
{
    const string ClipPath = "Assets/Animations/Player/Player_roll.anim";
    const string ControllerPath = "Assets/Animations/Player/PlayerVisual.controller";

    static CleanupLegacyRollAnimation()
    {
        EditorApplication.delayCall += Cleanup;
    }

    [MenuItem("Tools/Player/Cleanup Legacy Roll Animation")]
    public static void Cleanup()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null && controller.layers.Length > 0)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState rollState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Player_roll");
            if (rollState != null)
            {
                foreach (var transition in stateMachine.anyStateTransitions
                             .Where(item => item.destinationState == rollState)
                             .ToArray())
                {
                    stateMachine.RemoveAnyStateTransition(transition);
                }
                stateMachine.RemoveState(rollState);
            }

            int rollParameter = System.Array.FindIndex(
                controller.parameters, parameter => parameter.name == "Roll");
            if (rollParameter >= 0)
                controller.RemoveParameter(rollParameter);

            EditorUtility.SetDirty(controller);
        }

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null)
            AssetDatabase.DeleteAsset(ClipPath);

        AssetDatabase.SaveAssets();
    }
}
#endif
