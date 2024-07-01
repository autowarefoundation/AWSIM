using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ROS2;

namespace AWSIM
{
    /// <summary>
    /// This class for using ROS2 with AWSIM.
    /// </summary>
    public static class SimulatorROS2Node
    {
        static ROS2UnityCore ros2UnityCore;
        static ROS2Node node;
        const string NODE_NAME = "AWSIM";
        static ROS2Clock ros2Clock;

        public static ITimeSource TimeSource { get; private set; } = new UnityTimeSource();

        // Initialize faster than Awake() or Start()
        // Use Scripting Define Symbols to turn on and off.
#if ROS2
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
        static void Initialize()
        {
            // subscribe to events
            TimeSourceProvider.onTimeSourceChanged += OnTimeSourceChanged;
            Application.quitting += OnQuit;

            // get time source from time source provide
            TimeSource = TimeSourceProvider.GetTimeSource();

            // create ros2 node
            ros2UnityCore = new ROS2UnityCore();
            if (ros2UnityCore.Ok())
            {
                node = ros2UnityCore.CreateNode(NODE_NAME);
                ros2Clock = new ROS2Clock(TimeSource);
            }
        }

        /// <summary>
        /// Handler of Application.quitting event.
        /// </summary>
        static void OnQuit()
        {
            TimeSourceProvider.onTimeSourceChanged -= OnTimeSourceChanged;
            Application.quitting -= OnQuit;
        }

        /// <summary>
        /// Handler of onTimeSourceChanged event, it is invoked from TimeSourceProvider.
        /// </summary>
        static void OnTimeSourceChanged()
        {
            // get time source from time source provide
            TimeSource = TimeSourceProvider.GetTimeSource();
            if (ros2UnityCore.Ok())
            {
                ros2Clock = new ROS2Clock(TimeSource);
            }
        }

        /// <summary>
        /// Create a publisher with indicated QoS.
        /// </summary>
        /// <returns>The publisher</returns>
        /// <param name="topicName">topic that will be used for publishing</param>
        /// <param name="qos">QoS for publishing. If no QoS is selected, it will default to reliable, keep 10 last</param>
        static public Publisher<T> CreatePublisher<T>(string topicName, QualityOfServiceProfile qos = null) where T : Message, new()
        {
            return node.CreatePublisher<T>(topicName, qos);
        }

        /// <summary>
        /// Create a subscription
        /// </summary>
        /// <returns>The subscription</returns>
        /// <param name="topicName">topic to subscribe to</param>
        /// <param name="qos">QoS for subscription. If no QoS is selected, it will default to reliable, keep 10 last</param>
        static public Subscription<T> CreateSubscription<T>(string topicName, Action<T> callback, QualityOfServiceProfile qos = null) where T : Message, new()
        {
            return node.CreateSubscription<T>(topicName, callback, qos);
        }

        /// <summary>
        /// Update header timestamp
        /// </summary>
        /// <param name="message">Message with header to be updated</param>
        public static void UpdateROSTimestamp(ref MessageWithHeader message)
        {
            ros2Clock.UpdateROSTimestamp(ref message);
        }

        /// <summary>
        /// Update clock time
        /// </summary>
        /// <param name="time"></param>
        static public void UpdateROSClockTime(builtin_interfaces.msg.Time time)
        {
            ros2Clock.UpdateROSClockTime(time);
        }

        /// <summary>
        /// Remove ros2 publisher
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="publisher"></param>
        static public void RemovePublisher<T>(IPublisherBase publisher)
        {
            if (ros2UnityCore.Ok())
            {
                node.RemovePublisher<T>(publisher);
            }
        }

        /// <summary>
        /// Remove ros2 subscriber
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="subscription"></param>
        static public void RemoveSubscription<T>(ISubscriptionBase subscription)
        {
            if (ros2UnityCore.Ok())
            {
                node.RemoveSubscription<T>(subscription);
            }
        }

        /// <summary>
        /// Get current time
        /// </summary>
        /// <returns></returns>
        static public builtin_interfaces.msg.Time GetCurrentRosTime()
        {
            var timeMsg = new builtin_interfaces.msg.Time();
            ros2Clock.UpdateROSClockTime(timeMsg);
            return timeMsg;
        }

        static public bool Ok()
        {
            return ros2UnityCore.Ok();
        }
    }
}
