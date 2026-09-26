using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Editor
{
    [CustomEditor(typeof(WheelArcadeVehicleController))]
    public sealed class WheelArcadeVehicleControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (WheelArcadeVehicleController)target;
            serializedObject.Update();

            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("configuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("inputProviderComponent"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("resetter"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Tuning Overrides", EditorStyles.boldLabel);
            var useRuntimeTuningOverrides = serializedObject.FindProperty("useRuntimeTuningOverrides");
            var overridesWereDisabled = !useRuntimeTuningOverrides.boolValue;
            EditorGUILayout.PropertyField(useRuntimeTuningOverrides);
            serializedObject.ApplyModifiedProperties();

            if (overridesWereDisabled && useRuntimeTuningOverrides.boolValue)
            {
                Undo.RecordObject(controller, "Initialize Wheel Runtime Tuning Overrides");
                controller.CopyConfigurationToRuntimeTuningOverrides();
                EditorUtility.SetDirty(controller);
                serializedObject.Update();
            }

            if (useRuntimeTuningOverrides.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("runtimeTuningOverrides"), true);
                if (GUILayout.Button("Copy Configuration To Runtime Overrides"))
                {
                    Undo.RecordObject(controller, "Copy Wheel Configuration To Runtime Overrides");
                    controller.CopyConfigurationToRuntimeTuningOverrides();
                    EditorUtility.SetDirty(controller);
                    serializedObject.Update();
                }
            }
            else
            {
                var configuration = serializedObject.FindProperty("configuration").objectReferenceValue as WheelVehicleConfiguration;
                if (configuration == null)
                {
                    EditorGUILayout.HelpBox("Assign a Wheel Vehicle Configuration asset to view and edit the prototype's active tuning.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("The configuration asset below is the active source of truth. Enable Runtime Tuning Overrides to tune a temporary Play Mode snapshot instead.", MessageType.Info);
                    DrawConfigurationAssetTuning(configuration);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Composition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraTarget"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wheel Physics References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frontLeftWheel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frontRightWheel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rearLeftWheel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rearRightWheel"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wheel Visual References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frontLeftVisual"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frontRightVisual"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rearLeftVisual"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rearRightVisual"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Diagnostics", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Forward Speed (m/s)", controller.CurrentForwardSpeed);
                EditorGUILayout.FloatField("Lateral Speed (m/s)", controller.CurrentLateralSpeed);
                EditorGUILayout.FloatField("Planar Speed (m/s)", controller.CurrentPlanarSpeed);
                EditorGUILayout.FloatField("Vehicle Slip Angle (deg)", controller.CurrentVehicleSlipAngle);
                EditorGUILayout.FloatField("Steering Input", controller.CurrentSteeringInput);
                EditorGUILayout.FloatField("Reference Steering Speed", controller.CurrentSteeringSpeed);
                EditorGUILayout.FloatField("Front Wheel Steer Angle", controller.CurrentFrontWheelSteerAngle);
                EditorGUILayout.FloatField("Countersteer Correction", controller.CurrentCountersteerCorrection);
                EditorGUILayout.FloatField("Engine RPM", controller.CurrentEngineRpm);
                EditorGUILayout.IntField("Current Gear", controller.CurrentGear);
                EditorGUILayout.FloatField("RL Wheel RPM", controller.RearLeftWheelRpm);
                EditorGUILayout.FloatField("RR Wheel RPM", controller.RearRightWheelRpm);
                EditorGUILayout.FloatField("Average Driven Wheel RPM", controller.CurrentAverageDrivenWheelRpm);
                EditorGUILayout.FloatField("Current Gear Ratio", controller.CurrentGearRatio);
                EditorGUILayout.FloatField("Raw Calculated Engine RPM", controller.CurrentRawCalculatedEngineRpm);
                EditorGUILayout.FloatField("Power Curve Evaluation", controller.CurrentPowerCurveEvaluation);
                EditorGUILayout.FloatField("Torque Before Shift Suppression", controller.CurrentTorqueBeforeShiftSuppression);
                EditorGUILayout.Toggle("Shift In Progress", controller.ShiftInProgress);
                EditorGUILayout.FloatField("Shift Cooldown Remaining", controller.ShiftCooldownRemaining);
                EditorGUILayout.IntField("Requested Gear", controller.CurrentRequestedGear);
                EditorGUILayout.FloatField("Motor Torque / Rear Wheel", controller.CurrentMotorTorque);
                EditorGUILayout.FloatField("Normal Brake Torque", controller.CurrentNormalBrakeTorque);
                EditorGUILayout.FloatField("Handbrake Torque", controller.CurrentHandbrakeTorque);
                EditorGUILayout.Toggle("Handbrake Active", controller.HandbrakeActive);
                EditorGUILayout.Toggle("Forward Drift Active", controller.ForwardDriftActive);
                EditorGUILayout.FloatField("Drift Handbrake Multiplier", controller.CurrentDriftHandbrakeMultiplier);
                EditorGUILayout.FloatField("Rear Sideways Friction Stiffness", controller.CurrentRearSidewaysFrictionStiffness);
                DrawWheelSlip("FL", controller.FrontLeftForwardSlip, controller.FrontLeftSidewaysSlip);
                DrawWheelSlip("FR", controller.FrontRightForwardSlip, controller.FrontRightSidewaysSlip);
                DrawWheelSlip("RL", controller.RearLeftForwardSlip, controller.RearLeftSidewaysSlip);
                DrawWheelSlip("RR", controller.RearRightForwardSlip, controller.RearRightSidewaysSlip);
            }
        }

        private static void DrawWheelSlip(string wheelName, float forwardSlip, float sidewaysSlip)
        {
            EditorGUILayout.FloatField($"{wheelName} Forward Slip", forwardSlip);
            EditorGUILayout.FloatField($"{wheelName} Sideways Slip", sidewaysSlip);
        }

        private static void DrawConfigurationAssetTuning(WheelVehicleConfiguration configuration)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration Asset Tuning", EditorStyles.boldLabel);

            var serializedConfiguration = new SerializedObject(configuration);
            serializedConfiguration.Update();
            var property = serializedConfiguration.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                if (property.propertyPath != "m_Script")
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                enterChildren = false;
            }

            serializedConfiguration.ApplyModifiedProperties();
        }
    }
}
