// Copyright 2025 TIER IV, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using ROS2;

namespace Awsim.Common
{
    /// <summary>
    /// Ros2 node class for unified handling of ros2 in AWSIM.
    /// Ros2 topic publishers and subscriber. Ros2 Service and client. Ros2 time.
    /// </summary>
    public static class AwsimRos2Node
    {
        static ROS2UnityCore _ros2UnityCore = null;
        static ROS2Node _ros2Node;
        static ITimeSource _timeSource;
        static string _nodeName = "AWSIM";
        static TimeSourceType _timeSourceType = TimeSourceType.Ros2;

        /// <summary>
        /// Initlaize AwsimRos2Node class.
        /// </summary>
        public static void Initialize()
        {
            _timeSource = TimeSourceFactory.GetTimeSource(_timeSourceType);

            _ros2UnityCore = new ROS2UnityCore();
            if (_ros2UnityCore.Ok())
            {
                _ros2Node = _ros2UnityCore.CreateNode(_nodeName);
                _ros2Node.clock = new ROS2Clock(_timeSource);
            }
        }

        /// <summary>
        /// Initlaize AwsimRos2Node class.
        /// </summary>
        /// <param name="nodeName">Name used for ros2 node.</param>
        /// <param name="timeSourceType">Time source used for clock.</param>
        public static void Initialize(string nodeName, TimeSourceType timeSourceType)
        {
            _nodeName = nodeName;
            _timeSourceType = timeSourceType;

            Initialize();
        }

        /// <summary>
        /// Create a publisher with indicated QoS.
        /// </summary>
        /// <returns>The publisher</returns>
        /// <param name="topicName">topic that will be used for publishing</param>
        /// <param name="qos">QoS for publishing. If no QoS is selected, it will default to reliable, keep 10 last</param>
        public static Publisher<T> CreatePublisher<T>(string topicName, QualityOfServiceProfile qos = null) where T : Message, new()
        {
            return _ros2Node.CreatePublisher<T>(topicName, qos);
        }

        /// <summary>
        /// Create a subscription.
        /// </summary>
        /// <returns>The subscription</returns>
        /// <param name="topicName">topic to subscribe to</param>
        /// <param name="qos">QoS for subscription. If no QoS is selected, it will default to reliable, keep 10 last</param>
        public static Subscription<T> CreateSubscription<T>(string topicName, Action<T> callback, QualityOfServiceProfile qos = null) where T : Message, new()
        {
            return _ros2Node.CreateSubscription<T>(topicName, callback, qos);
        }


        /// <summary>
        /// Create a service.
        /// </summary>
        /// <returns>The service</returns>
        /// <param name="topicName">topic under which the service will be request able</param>
        /// <param name="qos">QoS for requests. If no QoS is selected, it will default to reliable, keep 10 last</param>
        public static IService<T, S> CreateService<T, S>(string topicName, Func<T, S> callback, QualityOfServiceProfile qos = null)
            where T : Message, new()
            where S : Message, new()
        {
            return _ros2Node.CreateService<T, S>(topicName, callback, qos);
        }

        /// <summary>
        /// Create a client service.
        /// </summary>
        /// <returns>The client</returns>
        /// <param name="topicName">topic under which the client will call the service</param>
        /// <param name="qos">QoS for requests. If no QoS is selected, it will default to reliable, keep 10 last</param>
        public static Client<T, S> CreateClient<T, S>(string topicName, QualityOfServiceProfile qos = null)
            where T : Message, new()
            where S : Message, new()
        {
            return _ros2Node.CreateClient<T, S>(topicName, qos);
        }

        /// <summary>
        /// Remove ros2 publisher.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="publisher"></param>
        public static void RemovePublisher<T>(IPublisherBase publisher)
        {
            // ROS-free scenes can destroy prefab publishers without initializing ROS.
            if (publisher != null && _ros2UnityCore != null && _ros2Node != null && _ros2UnityCore.Ok())
            {
                _ros2Node.RemovePublisher<T>(publisher);
            }
        }

        /// <summary>
        /// Remove ros2 subscriber.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="subscription"></param>
        public static void RemoveSubscription<T>(ISubscriptionBase subscription)
        {
            if (_ros2UnityCore.Ok())
            {
                _ros2Node.RemoveSubscription<T>(subscription);
            }
        }

        /// <summary>
        /// Remove ros2 client.
        /// </summary>
        /// <param name="client"></param>
        public static void RemoveClient(IClientBase client)
        {
            if (_ros2UnityCore.Ok())
            {
                _ros2Node.RemoveClient(client);
            }
        }

        /// <summary>
        /// Remove ros2 service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="service"></param>
        public static void RemoveService(IServiceBase service)
        {
            if (_ros2UnityCore.Ok())
            {
                _ros2Node.RemoveService(service);
            }
        }

        /// <summary>
        /// Get time from acrive ros2 timesource.
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="nanoseconds"></param>
        public static void GetTime(out int seconds, out uint nanoseconds)
        {
            _timeSource.GetTime(out seconds, out nanoseconds);
        }

        public static ITimeSource GetTimeSource()
        {
            return _timeSource;
        }

        /// <summary>
        /// Update the time in rosgraph_msgs.msg.Clock.
        /// </summary>
        /// <param name="clockMessage"></param>
        public static void UpdateClockMessage(ref rosgraph_msgs.msg.Clock clockMessage)
        {
            _ros2Node.clock.UpdateClockMessage(ref clockMessage);
        }

        /// <summary>
        /// Update the time in builtin_interfaces.msg.Time.
        /// </summary>
        /// <param name="time"></param>
        public static void UpdateROSClockTime(builtin_interfaces.msg.Time time)
        {
            _ros2Node.clock.UpdateROSClockTime(time);
        }

        /// <summary>
        /// Update the time int MessageWithHeader.
        /// </summary>
        /// <param name="message"></param>
        public static void UpdateROSTimestamp(ref MessageWithHeader message)
        {
            _ros2Node.clock.UpdateROSTimestamp(ref message);
        }

        /// <summary>
        /// Update the time of multiple rosgraph_msgs.msg.Clock. All with the same time value.
        /// </summary>
        /// <param name="clockMessages"></param>
        public static void UpdateClockMessages(params rosgraph_msgs.msg.Clock[] clockMessages)
        {
            GetTime(out var seconds, out var nanoseconds);
            foreach (var e in clockMessages)
            {
                e.Clock_.Sec = seconds;
                e.Clock_.Nanosec = nanoseconds;
            }
        }

        /// <summary>
        /// Update the time of multiple builtin_interfaces.msg.Time. All with the same time value.
        /// </summary>
        /// <param name="times"></param>
        public static void UpdateROSClockTimes(params builtin_interfaces.msg.Time[] times)
        {
            GetTime(out var seconds, out var nanoseconds);
            foreach (var e in times)
            {
                e.Sec = seconds;
                e.Nanosec = nanoseconds;
            }
        }

        /// <summary>
        /// Update the time of multiple MessageWithHeader. All with the same time value.
        /// </summary>
        /// <param name="messages"></param>
        public static void UpdateROSTimestamps(params ROS2.MessageWithHeader[] messages)
        {
            GetTime(out var seconds, out var nanoseconds);
            foreach (var e in messages)
            {
                e.UpdateHeaderTime(seconds, nanoseconds);
            }
        }
    }
}
