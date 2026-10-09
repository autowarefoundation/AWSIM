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

using UnityEngine;

namespace Awsim.Common
{
    /// <summary>
    /// First order lagged float.
    /// Used in cases where first order lag is applied, such as vehicle steering.
    /// </summary>
    /// NOTE: Might as well add "Gain".
    public class FirstOrderLaggedFloat
    {
        float _timeConstant = 0;
        float _desiredValue = 0;
        float _currentValue = 0;
        float _lastTime = 0;

        /// <summary>
        /// First order lagged value.
        /// </summary>
        public float Value
        {
            get
            {
                Update();
                return _currentValue;
            }
        }

        /// <summary>
        /// No first order lagged desired value.
        /// </summary>
        public float DesiredValue
        {
            set
            {
                _desiredValue = value;
            }
            get
            {
                return _desiredValue;
            }
        }

        /// <summary>
        /// Construct FirstOrderLaggedFloat.
        /// </summary>
        /// <param name="timeConstant">time constant value.</param>
        /// <param name="initialValue">initial value. <see cref="Value"/> and <see cref="DesiredValue"/> is initialized.</param>
        public FirstOrderLaggedFloat(float timeConstant, float initialValue)
        {
            _timeConstant = timeConstant;

            _desiredValue = initialValue;
            _currentValue = initialValue;

            _lastTime = Time.time;
        }

        void Update()
        {
            float dt = Time.time - _lastTime;

            if (dt == 0)
                return;

            if (_timeConstant == 0f)
                _currentValue = _desiredValue;
            else
                // Exact first-order step stays bounded even after a long idle
                // interval, when this lazy getter has not been sampled.
                _currentValue += (1f - Mathf.Exp(-dt / _timeConstant)) * (_desiredValue - _currentValue);

            _lastTime = Time.time;
        }
    }
}
