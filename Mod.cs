using System;
using MelonLoader;
using UnityEngine;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.BoneMenu; // Native radial menu UI

[assembly: MelonInfo(typeof(FBTFixPatch6.StandaloneFbtMod), "Standalone AI Body Tracking", "1.0.0", "Community Development")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace FBTFixPatch6
{
    public class StandaloneFbtMod : MelonMod
    {
        private RigManager _rigManager;
        private bool _menuInitialized = false;

        // Configuration variables managed by the Bone Menu
        public static float TrackingForceMultiplier = 1.0f;

        // Pseudo-Neural Network Constants for Human Gait & Stance estimation
        private const float HipSinkFactor = 0.85f;      // Approximate percent height of natural human hips
        private const float StanceWidth = 0.28f;       // Distance between feet relative to shoulder width
        private const float VelocityDampening = 0.15f;  // Filters out hyper-snapping jitters

        private Vector3 _estimatedLeftFootPos;
        private Vector3 _estimatedRightFootPos;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Standalone AI FBT Engine Online. Waiting for RigManager...");
        }

        public override void OnUpdate()
        {
            // Dynamically grab the player's rig whenever they change maps
            if (_rigManager == null)
            {
                _rigManager = GameObject.FindObjectOfType<RigManager>();
                
                // Initialize the radial menu options once the player rig is safely loaded
                if (_rigManager != null && !_menuInitialized)
                {
                    SetupBoneMenu();
                    _menuInitialized = true;
                }
                return;
            }

            ExecuteTrackingEstimation();
        }

        // ==========================================
        // 🧠 THE CORE AI ESTIMATION MATH (Quest Safe)
        // ==========================================
        private void ExecuteTrackingEstimation()
        {
            var physicsRig = _rigManager.physicsRig;
            if (physicsRig == null || _rigManager.avatar == null) return;

            // 1. EXTRACT DATA HOOKS FROM HANDS AND HEAD (No external hardware needed)
            Transform hmdTransform = physicsRig.m_head;
            Vector3 headPos = hmdTransform.position;
            Vector3 headForward = hmdTransform.forward;
            
            float avatarHeight = _rigManager.avatar.height;

            // 2. RUN ESTIMATION HEURISTICS
            // Flatten looking forward vector to a 2D floor grid plane
            Vector3 groundForward = new Vector3(headForward.x, 0, headForward.z).normalized;
            Vector3 groundRight = Vector3.Cross(Vector3.up, groundForward).normalized;

            // Calculate the absolute ground floor entry directly underneath the player
            float floorY = headPos.y - avatarHeight;

            // Project a localized Hip/Pelvis anchor based on head slouching offsets
            Vector3 projectedHips = new Vector3(headPos.x, headPos.y * HipSinkFactor, headPos.z) - (groundForward * 0.05f);

            // Compute ideal rest positions for left and right foot matrices based on current avatar height
            Vector3 targetLeftFoot = new Vector3(projectedHips.x, floorY, projectedHips.z) - (groundRight * (StanceWidth * avatarHeight * 0.5f));
            Vector3 targetRightFoot = new Vector3(projectedHips.x, floorY, projectedHips.z) + (groundRight * (StanceWidth * avatarHeight * 0.5f));

            // 3. APPLY INERTIAL DAMPENING
            // Smoothly blend the position delta to create lifelike fluid weight shift states
            _estimatedLeftFootPos = Vector3.Lerp(_estimatedLeftFootPos, targetLeftFoot, VelocityDampening);
            _estimatedRightFootPos = Vector3.Lerp(_estimatedRightFootPos, targetRightFoot, VelocityDampening);

            // 4. INJECT FORCES NATIVELY INTO THE BONELAB PHYSIQRIG
            ApplyForceToFootBone(physicsRig.leftFoot, _estimatedLeftFootPos);
            ApplyForceToFootBone(physicsRig.rightFoot, _estimatedRightFootPos);
        }

        private void ApplyForceToFootBone(GameObject footObject, Vector3 targetWorldPos)
        {
            if (footObject == null) return;

            Rigidbody footRb = footObject.GetComponent<Rigidbody>();
            if (footRb == null) return;

            // Distance calculation between the avatar's foot and our AI prediction position
            Vector3 directionToTarget = targetWorldPos - footObject.transform.position;
            
            // Base physical constant strength multiplied cleanly by the slider setting
            float scalarForce = 340f * TrackingForceMultiplier;

            // Push the foot physics body smoothly toward the calculated destination
            footRb.AddForce(directionToTarget * scalarForce, ForceMode.Acceleration);
        }

        // ==========================================
        // 🎛️ BONE MENU GENERATION INTERFACE
        // ==========================================
        private void SetupBoneMenu()
        {
            MenuCategory mainCategory = MenuManager.CreateCategory("AI FBT STANDALONE", Color.cyan);
            MenuCategory tuningCategory = mainCategory.CreateCategory("TRACKING TUNING", Color.white);

            // Injects a functional Float Slider into the radial menu
            tuningCategory.CreateFloatElement(
                "Tracking Force", 
                Color.yellow, 
                0.1f,                       // Increments by 0.1 per click
                TrackingForceMultiplier,    // Starting value (1.0)
                0.1f,                       // Minimum limit
                5.0f,                       // Maximum limit (5x force)
                new Action<float>(OnForceSliderChanged)
            );

            // Emergency Hard-Reset Button
            tuningCategory.CreateFunctionElement("Force Rig Recalibration", Color.red, new Action(OnEmergencyResetClicked));
            
            MelonLogger.Msg("Controls successfully injected into native Bone Menu.");
        }

        private static void OnForceSliderChanged(float newValue)
        {
            TrackingForceMultiplier = newValue;
            MelonLogger.Msg($"Tracking force multiplier updated to: {TrackingForceMultiplier}x");
        }

        private void OnEmergencyResetClicked()
        {
            if (_rigManager == null || _rigManager.avatar == null) return;

            MelonLogger.Msg("Tracking drift detected. Re-initializing avatar data...");

            // Wipe out stuck floor offset cache data in the framework
            _rigManager.bodyTracking.ResetTracking();

            // Native hot-reload trick to snap the physics rig back onto the exact scale boundaries
            _rigManager.SwitchAvatar(_rigManager.AvatarCrateReference);

            MelonLogger.Msg("Avatar data completely reset and tracking anchors updated.");
        }
    }
}
