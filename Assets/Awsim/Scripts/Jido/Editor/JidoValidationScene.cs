using UnityEditor.SceneManagement;
using UnityEngine;

namespace Awsim.Jido.Editor
{
    public static class JidoValidationScene
    {
        public const string Path="Assets/Awsim/Scenes/Jido/JidoShuttleValidationHDRP.unity";
        public static void Create()
        {
            JidoShuttlePhysicsCreator.CreateSensors();
            foreach(var obj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if(obj.name.StartsWith("JIDO measured sensor target")||obj.name=="JIDO physics barrier")
                    Object.DestroyImmediate(obj);
            new GameObject("JIDO validation scenario").AddComponent<JidoValidationScenario>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Path);
        }
    }
}
