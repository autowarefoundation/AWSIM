using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ROS2;

namespace AWSIM
{
    /// <summary>
    /// This class subscribes to the vehicleCommand and turnSignal  msg output from Autoware to ROS,
    /// and after converting the msg, it inputs it to the Vehicle class of E2ESimualtor.
    /// </summary>
    [RequireComponent(typeof(Vehicle))]
    public class VehicleRosInput : MonoBehaviour
    {
        [SerializeField] string turnIndicatorsCommandTopic = "/control/command/turn_indicators_cmd";
        [SerializeField] string hazardLightsCommandTopic = "/control/command/hazard_lights_cmd";
        [SerializeField] string ackermannControlCommandTopic = "/control/command/control_cmd";
        [SerializeField] string gearCommandTopic = "/control/command/gear_cmd";
        [SerializeField] string vehicleEmergencyStampedTopic = "/control/command/emergency_cmd";
        [SerializeField] string positionTopic = "/initialpose";

        [SerializeField] QoSSettings qosSettings = new QoSSettings();
        [SerializeField] Vehicle vehicle;
        [SerializeField] private QoSSettings positionQosInput;

        // subscribers.
        ISubscription<autoware_vehicle_msgs.msg.TurnIndicatorsCommand> turnIndicatorsCommandSubscriber;
        ISubscription<autoware_vehicle_msgs.msg.HazardLightsCommand> hazardLightsCommandSubscriber;
        ISubscription<autoware_control_msgs.msg.Control> ackermanControlCommandSubscriber;
        ISubscription<autoware_vehicle_msgs.msg.GearCommand> gearCommandSubscriber;
        ISubscription<tier4_vehicle_msgs.msg.VehicleEmergencyStamped> vehicleEmergencyStampedSubscriber;
        ISubscription<geometry_msgs.msg.PoseWithCovarianceStamped> positionSubscriber;

        // Latest Emergency value.
        // If emergency is true, emergencyDeceleration is applied to the vehicle's deceleration.
        // TODO: In case of reverse gear?
        bool isEmergency = false;
        float emergencyDeceleration = -3.0f; // m/s^2

        private Vector3 positionVector;
        private Quaternion rotationVector;
        
        
        // Latest value of TurnSignals.
        // HAZARD and LEFT/RIGHT are different msgs in Autoware.universe.
        // Priority : HAZARD > LEFT/RIGHT > NONE
        Vehicle.TurnSignal turnIndicatorsSignal = Vehicle.TurnSignal.NONE;
        Vehicle.TurnSignal hazardLightsSignal = Vehicle.TurnSignal.NONE;
        Vehicle.TurnSignal input = Vehicle.TurnSignal.NONE;


        void Reset()
        {
            if (vehicle == null)
                vehicle = GetComponent<Vehicle>();

            // initialize default QoS params.
            qosSettings.ReliabilityPolicy = ReliabilityPolicy.QOS_POLICY_RELIABILITY_RELIABLE;
            qosSettings.DurabilityPolicy = DurabilityPolicy.QOS_POLICY_DURABILITY_TRANSIENT_LOCAL;
            qosSettings.HistoryPolicy = HistoryPolicy.QOS_POLICY_HISTORY_KEEP_LAST;
            qosSettings.Depth = 1;
        }

        /// <summary>
        /// Processes the TurnSignal to be applied to the vehicle from the latest turnIndicatorsSignal and hazardLightsSignal values.
        /// Priority : HAZARD > LEFT/RIGHT > NONE
        /// </summary>
        void UpdateVehicleTurnSignal()
        {
            // HAZARD > LEFT, RIGHT > NONE
            if (hazardLightsSignal == Vehicle.TurnSignal.HAZARD)
                input = hazardLightsSignal;
            else if (turnIndicatorsSignal == Vehicle.TurnSignal.LEFT || turnIndicatorsSignal == Vehicle.TurnSignal.RIGHT)
                input = turnIndicatorsSignal;
            else
                input = Vehicle.TurnSignal.NONE;

            // input
            if (vehicle.SignalInput != input)
                vehicle.SignalInput = input;
        }

        void Start()
        {
            var qos = qosSettings.GetQoSProfile();
            var qosp = positionQosInput.GetQoSProfile();

            turnIndicatorsCommandSubscriber
                = SimulatorROS2Node.CreateSubscription<autoware_vehicle_msgs.msg.TurnIndicatorsCommand>(
                    turnIndicatorsCommandTopic, msg =>
                    {
                        turnIndicatorsSignal = VehicleROS2Utility.RosToUnityTurnSignal(msg);
                        UpdateVehicleTurnSignal();
                    }, qos);

            hazardLightsCommandSubscriber
                = SimulatorROS2Node.CreateSubscription<autoware_vehicle_msgs.msg.HazardLightsCommand>(
                    hazardLightsCommandTopic, msg =>
                    {
                        hazardLightsSignal = VehicleROS2Utility.RosToUnityHazard(msg);
                        UpdateVehicleTurnSignal();
                    }, qos);

            ackermanControlCommandSubscriber
                = SimulatorROS2Node.CreateSubscription<autoware_control_msgs.msg.Control>(
                    ackermannControlCommandTopic, msg =>
                    {
                        // highest priority is EMERGENCY.
                        // If Emergency is true, ControlCommand is not used for vehicle acceleration input.
                        if (!isEmergency)
                            vehicle.AccelerationInput = msg.Longitudinal.Acceleration;

                        vehicle.SteerAngleInput = -(float)msg.Lateral.Steering_tire_angle * Mathf.Rad2Deg;
                    }, qos);

            gearCommandSubscriber
                = SimulatorROS2Node.CreateSubscription<autoware_vehicle_msgs.msg.GearCommand>(
                    gearCommandTopic, msg =>
                    {
                        vehicle.AutomaticShiftInput = VehicleROS2Utility.RosToUnityShift(msg);
                    }, qos);

            vehicleEmergencyStampedSubscriber
                = SimulatorROS2Node.CreateSubscription<tier4_vehicle_msgs.msg.VehicleEmergencyStamped>(
                    vehicleEmergencyStampedTopic, msg =>
                    {
                        // highest priority is EMERGENCY.
                        // If emergency is true, emergencyDeceleration is applied to the vehicle's deceleration.
                        isEmergency = msg.Emergency;
                        if (isEmergency)
                            vehicle.AccelerationInput = emergencyDeceleration;
                    });
            positionSubscriber
                = SimulatorROS2Node.CreateSubscription<geometry_msgs.msg.PoseWithCovarianceStamped>(
                    positionTopic, msg =>
                    {
                        positionVector.x = (float)msg.Pose.Pose.Position.X;
                        positionVector.y = (float)msg.Pose.Pose.Position.Y;
                        positionVector.z = (float)msg.Pose.Pose.Position.Z;
                        
                        rotationVector.x = (float)msg.Pose.Pose.Orientation.X;
                        rotationVector.y = (float)msg.Pose.Pose.Orientation.Y;
                        rotationVector.z = (float)msg.Pose.Pose.Orientation.Z;
                        rotationVector.w = (float)msg.Pose.Pose.Orientation.W;
                        
                        vehicle.PositionInput= ROS2Utility.RosToUnityPosition(positionVector - Environment.Instance.MgrsOffsetPosition);
                        vehicle.RotationInput = ROS2Utility.RosToUnityRotation(rotationVector);

                    }, qosp);
        }

        void OnDestroy()
        {
            SimulatorROS2Node.RemoveSubscription<autoware_vehicle_msgs.msg.TurnIndicatorsCommand>(turnIndicatorsCommandSubscriber);
            SimulatorROS2Node.RemoveSubscription<autoware_vehicle_msgs.msg.HazardLightsCommand>(hazardLightsCommandSubscriber);
            SimulatorROS2Node.RemoveSubscription<autoware_control_msgs.msg.Control>(ackermanControlCommandSubscriber);
            SimulatorROS2Node.RemoveSubscription<autoware_vehicle_msgs.msg.GearCommand>(gearCommandSubscriber);
            SimulatorROS2Node.RemoveSubscription<tier4_vehicle_msgs.msg.VehicleEmergencyStamped>(vehicleEmergencyStampedSubscriber);
            SimulatorROS2Node.RemoveSubscription<geometry_msgs.msg.PoseWithCovarianceStamped>(positionSubscriber);

        }
    }
}