using System;
using MelonLoader;
using UnityEngine;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.BoneMenu;

[assembly: MelonInfo(typeof(FBTFixPatch6.StandaloneFbtMod), "Standalone AI Body Tracking", "1.0.0", "Community Development")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace FBTFixPatch6
{
    public class StandaloneFbtMod : MelonMod
    {
        private RigManager _rigManager;
        private bool _menuInitialized = false;

        public static float TrackingForceMultiplier = 1.0f;

        private const float HipSinkFactor = 0.85f;      
        private const float StanceWidth = 0.28f;       
        private const float VelocityDampening = 0.15f;  

        private Vector3 _estimatedLeftFootPos;
        private Vector3 _estimatedRightFootPos;

        public override void OnUpdate()
        {
            if (_rigManager == null)
            {
                _rigManager = GameObject.FindObjectOfType<RigManager>();
                if (_rigManager != null && !_menuInitialized)
                {
                    SetupBoneMenu();
                    _menuInitialized = true;
                }
                return;
            }

            ExecuteTrackingEstimation();
        }

        private void ExecuteTrackingEstimation()
        {
            var physicsRig = _rigManager.physicsRig;
            if (physicsRig == null || _rigManager.avatar == null) return;

            Transform hmdTransform = physicsRig.m_head;
            Vector3 headPos = hmdTransform.position;
            Vector3 headForward = hmdTransform.forward;
            float avatarHeight = _rigManager.avatar.height;

            Vector3 groundForward = new Vector3(headForward.x, 0, headForward.z).normalized;
            Vector3 groundRight = Vector3.Cross(Vector3.up, groundForward).normalized;
            float floorY = headPos.y - avatarHeight;

            Vector3 projectedHips = new Vector3(headPos.x, headPos.y * HipSinkFactor, headPos.z) - (groundForward * 0.05f);

            Vector3 targetLeftFoot = new Vector3(projectedHips.x, floorY, projectedHips.z) - (groundRight * (StanceWidth * avatarHeight * 0.5f));
            Vector3 targetRightFoot = new Vector3(projectedHips.x, floorY, projectedHips.z) + (groundRight * (StanceWidth * avatarHeight * 0.5f));

            _estimatedLeftFootPos = Vector3.Lerp(_estimatedLeftFootPos, targetLeftFoot, VelocityDampening);
            _estimatedRightFootPos = Vector3.Lerp(_estimatedRightFootPos, targetRightFoot, VelocityDampening);

            ApplyForceToFootBone(physicsRig.leftFoot, _estimatedLeftFootPos);
            ApplyForceToFootBone(physicsRig.rightFoot, _estimatedRightFootPos);
        }

        private void ApplyForceToFootBone(GameObject footObject, Vector3 targetWorldPos)
        {
            if (footObject == null) return;
            Rigidbody footRb = footObject.GetComponent<Rigidbody>();
            if (footRb == null) return;

            Vector3 directionToTarget = targetWorldPos - footObject.transform.position;
            float scalarForce = 340f * TrackingForceMultiplier;
            footRb.AddForce(directionToTarget * scalarForce, ForceMode.Acceleration);
        }

        private void SetupBoneMenu()
        {
            MenuCategory mainCategory = MenuManager.CreateCategory("AI FBT STANDALONE", Color.cyan);
            MenuCategory tuningCategory = mainCategory.CreateCategory("TRACKING TUNING", Color.white);

            tuningCategory.CreateFloatElement(
                "Tracking Force", 
                Color.yellow, 
                0.1f,                       
                TrackingForceMultiplier,    
                0.1f,                       
                5.0f,                       
                new Action<float>(OnForceSliderChanged)
            );

            tuningCategory.CreateFunctionElement("Force Rig Recalibration", Color.red, new Action(OnEmergencyResetClicked));
        }

        private static void OnForceSliderChanged(float newValue)
        {
            TrackingForceMultiplier = newValue;
        }

        private void OnEmergencyResetClicked()
        {
            if (_rigManager == null || _rigManager.avatar == null) return;
            _rigManager.bodyTracking.ResetTracking();
            _rigManager.SwitchAvatar(_rigManager.AvatarCrateReference);
        }
    }
}
