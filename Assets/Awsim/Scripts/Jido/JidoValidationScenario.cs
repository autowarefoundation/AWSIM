using System.IO;
using UnityEngine;

namespace Awsim.Jido
{
    // Only attached to the dedicated validation scene, never the city player.
    public sealed class JidoValidationScenario : MonoBehaviour
    {
        GameObject barrier;
        JidoUdpVehicleBridge vehicle;
        void Awake()
        {
            if(!File.Exists("Logs/jido-validation-mode.txt")) return;
            if(File.ReadAllText("Logs/jido-validation-mode.txt").Trim()!="obstacle") return;
            barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name="JIDO physics barrier";
            barrier.transform.position=new Vector3(0,.6f,8);
            barrier.transform.localScale=new Vector3(12,1.2f,1);
            barrier.SetActive(false);
            vehicle=Object.FindFirstObjectByType<JidoUdpVehicleBridge>();
        }
        void Update()
        {
            if(barrier!=null&&!barrier.activeSelf&&vehicle.transform.position.z>1f) {
                barrier.SetActive(true);
                Debug.Log("JIDO validation barrier activated ahead of moving shuttle");
            }
        }
    }
}
