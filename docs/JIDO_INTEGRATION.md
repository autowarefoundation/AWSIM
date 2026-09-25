# JIDO scene (ROS-free first milestone)

The JIDO code lives in `Assets/Awsim/Scripts/Jido`. It exchanges UDP packets
with the sibling `jido_AWSIM_bridge` Python project. The vehicle component
carries controls, truth pose and IMU. `JidoLidarUdpPublisher` carries a VLP16
point cloud in bounded binary fragments. GNSS, wheel odometry and heading for
the EKF are currently virtual sensors derived from truth by the Python bridge.

## Scene setup in Unity 6000.0.61f1

1. In the Unity menu, choose **JIDO → Create UDP Test Track → URP** or
   **HDRP**, matching the project's active render pipeline. The editor script
   saves `Assets/Awsim/Scenes/Jido/JidoUdpTestTrack*.unity` with a flat
   100 × 100 m surface, Lexus prefab, IMU and wired JIDO bridge.
2. Inspect the new scene's `JIDO Ego Vehicle` and verify the `vehicle`,
   `vehicleBody` and `imu` references. The bridge owns their `Initialize`,
   `OnUpdate` and `OnFixedUpdate` calls; do not tick them elsewhere.
3. Do not add `AutowareSimulationDemo` as a controller: its `Start()`
   initializes `AwsimRos2Node` and ROS publishers.
4. Keep the JIDO bridge transport ports at 25010 (telemetry) and 25011
   (commands), or change both ends together. Set the ENU anchor and the
   vehicle-origin-to-rear-axle offset for the scene. These are profile data,
   not values to copy from CARLA.
5. Start the Python bridge, then press Play. The vehicle remains under full
   brake until valid commands arrive. The Unity side also brakes after 500 ms
   without a valid command, even if the Python process stops.
6. For obstacle tests, use **JIDO → Add VLP16 to Current Test Track** with the
   matching render pipeline and save the scene. This adds the RGL scene
   manager, VLP16 prefab and JIDO binary UDP publisher. The prefab's ROS 2
   and raw UDP publishers are disabled in the generated scene.

Use the Python bridge README for installation and Kafka startup. Its
`localizacao` message is simulator truth, so run the JIDO Core without
`ekf_fusion_node` for this first control test. Do not start both truth and EKF
publishers on `sil.jidocab.fusion.localization`.

For an EKF test, restart the Python bridge with `--ekf-mode` and run
`ekf_fusion_node` with `JIDO_TOPIC_PREFIX=sil.`. That mode suppresses truth
localization while publishing virtual GPS, wheel speed and heading. These
virtual measurements come from simulator truth and need sensor error models
before evaluating localization robustness.

## Coordinate contract

Unity `(+x right, +y up, +z forward)` is emitted as JIDO
`(+x forward, +y left, +z up)`. Angular velocity uses the axial-vector
transformation, so positive JIDO yaw is a left turn. JIDO steering is positive
left; AWSIM's keyboard input uses negative tire angle for left, so the adapter
negates the steering command. The sign and realized radius must be measured
with a short steering test before autonomous routes.

The current localization output assumes a flat test track (pitch and roll are
zero) and uses the receive timestamp on the Python side. It is appropriate
for the first straight-line and turning control gate only. Time alignment,
pitch/roll and sensor-to-rear-axle calibration are required before claiming
EKF or full Londrina validation.
