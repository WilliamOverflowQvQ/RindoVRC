// VRCLens Copyright (c) 2020-2022 Hirabiki. All rights reserved.
// Usage of this product is subject to Terms of Use in readme_DoNotDelete.txt

// Avatars 3.0 Manager (AnimatorCloner.cs) is Copyright (c) 2020 VRLabs
// under MIT License. See LICENSE.txt in Lib/AnimatorCloner folder.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
#if UNITY_EDITOR
using Hirabiki.Extern.VRLabs.AV3Manager;
using UnityEditor.Animations;
#endif
using UnityEngine;
using UnityEngine.Animations;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace Hirabiki.AV3.Works.VRCLens
{
    public enum SetupMode { VRLeftHand, VRRightHand };
    public class VRCLensSetup : MonoBehaviour
    {
#if UNITY_EDITOR

        public VRCAvatarDescriptor avatarDescriptor;
        public VRCExpressionsMenu targetSubMenu;
        public GameObject cameraModel;
        // No longer needed a readme file, now using the path of this script as reference
        public SetupMode setupMode = SetupMode.VRRightHand;
        public int maxBlurSize = 225;
        public int maxBlurIndex = 1, droneGestureIndex = 5, sensorResIndex = 0;
        public int tonemapIndex = 1;
        public Vector2Int customSensorRes = new Vector2Int(1920, 1080);
        private Vector2Int _lastSensorRes;
        public Vector3 cameraModelPosition = Vector3.zero;
        public Vector3 cameraModelRotation = Vector3.zero;

        public Vector3 cameraRotation = Vector3.zero;
        public Vector3 focusPenRotation = Vector3.zero;
        [Min(0f)] public float cameraSize = 1f;
        public Vector3 previewPosition;
        [Min(0f)] public float previewSize = 1f;
        public bool useWriteDefaults = true; // Does not satisfy VRChat avatar creation guidelines, but is required for compatibility with most avatars
        public bool useDisableButton = true;

        public bool av3Puppet, av3Zoom, av3Exposure, av3Aperture, av3Focus;
        public bool animAE, animDoF, animGrid, animLevel;// animDirectStream, animLegacyStream;

        public int afModeIndex = 0;
        public int focalIndex = 2, apertureIndex = 2, sensorSizeIndex = 0, msaaIndex = 1, streamModeIndex = 0;
        private readonly float[] _focalValues = { .00f, .12f, .25f, .38f, .50f, .60f, .64f, .75f, .84f, .90f, 1f };
        private readonly float[] _apertureValues = { 0f, .125f, .25f, .375f, .50f, .625f, .75f, .875f, 1f };
        private readonly float[] _sensorSizeValues = { 1.0f, 1.3f, 1.5f, 1.6f, 2.0f, 2.7f };

        private Transform _armatureHead;
        private VRCLensAssetManager _reserializeManager;
        private VRCLensProfileManager _profileManager;

        private bool _hasNoErrors = true;

        private string _userCopyFolderPath = "";
        [NonSerialized] public bool queueValidate;
        
        public readonly int AV3_MAXPARAMALLOC = 256;
        public readonly int AV3_MAXMENUS = 8;
        public readonly int AV3_MAXMENUDEPTH = 16;

        private readonly string[] _pNameList = { "VRCLFeatureToggle", "VRCLInterrupt", "VRCFaceBlendH", "VRCFaceBlendV", "VRCLZoomRadial", "VRCLExposureRadial", "VRCLApertureRadial", "VRCLFocusRadial" };
        private readonly string[] _defaultUsedList = { "VRCFaceBlendH", "VRCFaceBlendV" };
        private readonly string[] _defaultParamsList = {
            "IsLocal", "Viseme", "GestureLeft", "GestureRight", "GestureLeftWeight", "GestureRightWeight",
            "AngularY", "VelocityX", "VelocityY", "VelocityZ", "Upright", "Grounded", "Seated", "AFK",
            "TrackingType", "VRMode", "MuteSelf", "InStation",
            "VRCFaceBlendH", "VRCFaceBlendV" };
        private readonly int[] _pTypeList = { 0, 0, 1, 1, 1, 1, 1, 1 };

        public string RootPath { get; set; }
        public string ResourcePath => $"{RootPath}/Resource";
        public string PrefabPath => $"{RootPath}/Prefabs";
        public string UserPath => $"{RootPath}/User";
        public Animator Avatar => avatarDescriptor != null ? avatarDescriptor.GetComponent<Animator>() ?? null : null;

        void OnValidate()
        {
            Vector2Int r = customSensorRes;
            if(r.x != _lastSensorRes.x)
            {
                r.x = (r.x + 8) / 16 * 16;
                r.y = r.x / 16 * 9;
            } else if(r.y != _lastSensorRes.y)
            {
                r.y = (r.y + 4) / 9 * 9;
                r.x = r.y / 9 * 16;
            }
            r.x = Mathf.Clamp(r.x, 16, 7680);
            r.y = Mathf.Clamp(r.y, 9, 4320);

            customSensorRes = r;
            _lastSensorRes = customSensorRes;
            queueValidate = true;
        }

        public bool IsInCorrectHand()
        {
            if(!IsSetupCompatible()) return true;

            Vector3 camPos = transform.Find("CamBase").position;
            // The following two lines could be the root of the problem here
            float leftHandDist = (Avatar.GetBoneTransform(HumanBodyBones.LeftHand).position - camPos).sqrMagnitude;
            float rightHandDist = (Avatar.GetBoneTransform(HumanBodyBones.RightHand).position - camPos).sqrMagnitude;

            return leftHandDist < rightHandDist ^ setupMode == SetupMode.VRRightHand;
        }

        public bool IsAvatarUniformScale()
        {
            if(avatarDescriptor == null) return false;
            Vector3 t = avatarDescriptor.transform.localScale;
            return t.x == t.y && t.y == t.z;
        }

        public bool IsAlreadySetup()
        {
            return avatarDescriptor != null ? avatarDescriptor.transform.Find("VRCLens/WorldA/AnimDummy") != null : false;
        }

        public bool IsAvatarHumanoid()
        {
            if(avatarDescriptor == null) return true;

            return Avatar.GetBoneTransform(HumanBodyBones.Hips) != null;
        }

        public bool IsSetupCompatible()
        {
            if(avatarDescriptor == null) return true;
            bool compat = true;
            compat &= null != Avatar.GetBoneTransform(HumanBodyBones.Head);
            compat &= null != Avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            compat &= null != Avatar.GetBoneTransform(HumanBodyBones.RightHand);
            compat &= null != Avatar.GetBoneTransform(HumanBodyBones.LeftFoot);
            compat &= null != Avatar.GetBoneTransform(HumanBodyBones.RightFoot);

            return compat;
        }

        public string DumpDiagnostics()
        {
            string debugText = "";
            debugText += $"VRCLens version: {Managed.VRCLensVerifier.VRCLENS_VERSION}\n";
            if(Avatar == null)
            {
                debugText += "Animator is missing from your avatar";
                return debugText;
            }

            debugText += $"Valid Mecanim: {Avatar.avatar.isValid}\n";
            debugText += $"Valid Humanoid: {Avatar.avatar.isHuman}\n";
            debugText += $"==== GameObject name ====\n{Avatar.name}\n";
            debugText += $"==== Animator Avatar name ====\n{(Avatar.avatar == null ? "null" : Avatar.avatar.name)}\n\n";

            SkinnedMeshRenderer avatarBody = null;

            for(int i = 0; i < avatarDescriptor.transform.childCount; i++)
            {
                SkinnedMeshRenderer ab = avatarDescriptor.transform.GetChild(i).GetComponent<SkinnedMeshRenderer>();
                if(ab != null)
                {
                    avatarBody = ab;
                    break;
                }
            }
            if(avatarBody != null)
            {
                debugText += $"==== Model data path ====\n{AssetDatabase.GetAssetPath(avatarBody.sharedMesh)}\n";
            }

            debugText += $"==== Animator Avatar path ====\n{AssetDatabase.GetAssetPath(Avatar.avatar)}\n\n==== Missing Bones ====";
            for(int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform t = Avatar.GetBoneTransform((HumanBodyBones)i);
                if(t == null)
                {
                    debugText += $"\n{(HumanBodyBones)i}";
                }
            }
            return debugText;
        }

        public bool IsSetupErrorFree()
        {
            if(avatarDescriptor == null) return _hasNoErrors;

            AnimatorController anim = FindFXLayer(avatarDescriptor, out _);
            if(anim == null) return _hasNoErrors;

            bool validCheck = _hasNoErrors;
            foreach(AnimatorControllerLayer layer in anim.layers)
            {
                if(layer.stateMachine == null)
                {
                    validCheck = false;
                }
            }
            return validCheck;
        }

        public int FreeMenuControls()
        {
            if(avatarDescriptor == null) return int.MaxValue;

            VRCExpressionsMenu expMenu = targetSubMenu == null ? avatarDescriptor.expressionsMenu : targetSubMenu; // Expressions -> Menu
            if(expMenu == null) return int.MaxValue;

            List<VRCExpressionsMenu.Control> menuList = expMenu.controls;
            
            int usedCount = menuList.Count;
            foreach(VRCExpressionsMenu.Control c in menuList)
            {
                if(c.name == " VRCLens " && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    --usedCount;
                }
            }
            return AV3_MAXMENUS - usedCount;
        }
        public int OccupiedParameters()
        {
            if(avatarDescriptor == null) return int.MaxValue;

            VRCExpressionParameters param = avatarDescriptor.expressionParameters; // Expressions -> Parameters
            if(param == null) return int.MaxValue;

            VRCExpressionParameters.Parameter[] existingParams = param.parameters;

            VRCExpressionParameters.Parameter[] newParams = GenerateAllNewParams();

            int usedCount = 0;
            for(int i = 0; i < existingParams.Length; i++)
            {
                if(existingParams[i] == null) Debug.LogWarning($"Param {i+1} is null", param);
                if(existingParams[i] == null || existingParams[i].name == "") // The slot is empty
                {
                    // The slot is free
                } else
                {
                    bool isSlotVRCLens = false;
                    foreach(VRCExpressionParameters.Parameter p in newParams)
                    {
                        // If the slot's name is part of the new list...
                        if(existingParams[i].name == p.name)
                        {
                            // The slot is free
                            isSlotVRCLens = true;
                            break;
                        }
                    }
                    if(!isSlotVRCLens)
                    {
                        // Otherwise it's occupied. But check its value type
                        usedCount += existingParams[i].valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
                    }
                }
            }
            return usedCount;
        }
        public int RequiredParameters()
        {
            int required = 8;
            required += false ? 8 : 0;// av3Interrupt ? 8 : 0; [Backward compatibility reasons]
            required += av3Puppet ? 16 : 0;
            required += av3Zoom ? 8 : 0;
            required += av3Exposure ? 8 : 0;
            required += av3Aperture ? 8 : 0;
            required += av3Focus ? 8 : 0;
            return required; // For now they're all int/floats (8 bits)
        }
        public int RequiredParameterSlots()
        {
            int slots = 1;
            slots += false ? 1 : 0;// av3Interrupt ? 1 : 0; [Backward compatibility reasons]
            slots += av3Puppet ? 2 : 0;
            slots += av3Zoom ? 1 : 0;
            slots += av3Exposure ? 1 : 0;
            slots += av3Aperture ? 1 : 0;
            slots += av3Focus ? 1 : 0;
            return slots;
        }

        public bool HasEnoughParameters()
        {
            return OccupiedParameters() + RequiredParameters() <= AV3_MAXPARAMALLOC;
        }
        public bool HasEnoughMenuControls()
        {
            return FreeMenuControls() >= 1;
        }

        public bool IsSubMenuUsedByAvatar()
        {
            if(avatarDescriptor == null || targetSubMenu == null) return true;
            RefreshCopyFolderPath();

            string path = AssetDatabase.GetAssetPath(targetSubMenu);
            if(path.StartsWith($"{PrefabPath}/Menus2") || path.StartsWith($"{_userCopyFolderPath}/Menus2")) return false;

            if(avatarDescriptor.expressionsMenu == targetSubMenu) return true;

            return IsSubMenuUsedByAvatar(avatarDescriptor.expressionsMenu);
        }
        private bool IsSubMenuUsedByAvatar(VRCExpressionsMenu menu, int recursionDepth = 0)
        {
            if(recursionDepth >= AV3_MAXMENUDEPTH)
            {
                Debug.LogWarning($"Sub Menu circular reference found!", menu);
                return false;
            }
            if(menu == null) return false;

            foreach(VRCExpressionsMenu.Control c in menu.controls)
            {
                if(c.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    // Try to prevent referring to VRCLens menu itself
                    if(c.name == " VRCLens ") continue;

                    if(c.subMenu == targetSubMenu)
                    {
                        return true;
                    }
                    else if(recursionDepth < AV3_MAXMENUDEPTH)
                    {
                        bool ret = IsSubMenuUsedByAvatar(c.subMenu, recursionDepth + 1);
                        if(ret) return true;
                    }
                }
            }
            return false;
        }

        public void SwapCameraHand()
        {
            Transform cam = transform.Find("CamBase");
            Transform foc = transform.Find("FocusObject");
            cam.position = Vector3.Scale(cam.position, new Vector3(-1f, 1f, 1f));
            cam.rotation *= Quaternion.FromToRotation(Vector3.left, Vector3.right);
            foc.position = Vector3.Scale(foc.position, new Vector3(-1f, 1f, 1f));
            foc.rotation *= Quaternion.FromToRotation(Vector3.left, Vector3.right);

            cameraRotation = cam.localRotation.eulerAngles;
            focusPenRotation = foc.localRotation.eulerAngles;
        }
        public void AlignCameraDesktopHead()
        {
            Transform cam = transform.Find("CamBase");
            cam.localRotation = Quaternion.identity;

            cameraRotation = cam.localRotation.eulerAngles;
        }
        public void FixSetup()
        {
            if(transform.parent != null) transform.SetParent(null);
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            if(avatarDescriptor != null)
            {
                avatarDescriptor.transform.SetParent(null);
                avatarDescriptor.transform.position = Vector3.zero;
                avatarDescriptor.transform.rotation = Quaternion.identity;
                float yScale = avatarDescriptor.transform.localScale.y;
                avatarDescriptor.transform.localScale = yScale * Vector3.one;
            }
        }
        public bool FixSetupOnEnable()
        {
            // HACK: Relevant when upgrading from older version of VRCLens that have not accounted for Unity 2019
            // This is to fix the black screen in Unity 2019 Editor scene view as the FOV of the editor collides with VRChat desktop mode
            Transform previewMesh = transform.Find("CamBase/CamObject/CameraModel/PreviewBase/PreviewMesh");
            if(previewMesh == null) return false;
            Renderer renderer = previewMesh.GetComponent<Renderer>();
            if(renderer == null) return false;
            renderer.enabled = false;
            return true;
        }
        public void AutoArrangeSetup()
        {
            if(Avatar == null) return;
            Transform cam = transform.Find("CamBase");
            Transform foc = transform.Find("FocusObject");
            Transform eyeFoc = transform.Find("SelfieFocusPoint");
            Transform camHead = transform.Find("HeadMountPoint");

            Transform eyeL = Avatar.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform eyeR = Avatar.GetBoneTransform(HumanBodyBones.RightEye);

            Transform leftHand = Avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = Avatar.GetBoneTransform(HumanBodyBones.RightHand);

            Transform leftFinger = Avatar.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) ?? leftHand;
            Transform rightFinger = Avatar.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? rightHand;

            Transform leftFingerTip = Avatar.GetBoneTransform(HumanBodyBones.LeftIndexDistal) ?? leftHand;
            Transform rightFingerTip = Avatar.GetBoneTransform(HumanBodyBones.RightIndexDistal) ?? rightHand;

            bool leftHanded = setupMode == SetupMode.VRLeftHand;

            Transform mainFinger = leftHanded ? leftFinger : rightFinger;
            Transform mainHand = leftHanded ? leftHand : rightHand;

            Transform offFingerTip = leftHanded ? rightFingerTip : leftFingerTip;
            Transform offHand = leftHanded ? rightHand : leftHand;

            Vector3 leftArmDirection = Vector3.Normalize(leftHand.position - Avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm).position);
            Vector3 rightArmDirection = Vector3.Normalize(rightHand.position - Avatar.GetBoneTransform(HumanBodyBones.RightLowerArm).position);

            Vector3 mainDir = leftHanded ? leftArmDirection : rightArmDirection;
            Vector3 offDir = leftHanded ? rightArmDirection : leftArmDirection;


            cam.position = mainFinger.position + cameraSize * 0.09f * Vector3.Cross(mainDir, leftHanded ? Vector3.back : Vector3.forward).normalized;

            cam.rotation = Quaternion.LookRotation(mainDir, Vector3.forward);

            foc.position = offFingerTip.position + 0.05f * offFingerTip.TransformDirection(new Vector3(0f, 1f, -0.1f));
            foc.rotation = Quaternion.LookRotation(offDir, Vector3.forward);


            // If no eye bones, use avatar descriptor's view position instead
            Vector3 midPoint = eyeL != null && eyeR != null ? Vector3.Lerp(eyeL.position, eyeR.position, 0.5f) : avatarDescriptor.ViewPosition;

            eyeFoc.position = midPoint + 0.05f * Vector3.forward;
            camHead.position = midPoint + 0.25f * Vector3.right;
            camHead.rotation = Quaternion.identity;

            cameraRotation = cam.localRotation.eulerAngles;
            focusPenRotation = foc.localRotation.eulerAngles;
        }

        public void PreserveSetupOnReset()
        {
            Transform l = Avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform r = Avatar.GetBoneTransform(HumanBodyBones.RightHand);
            Transform h = Avatar.GetBoneTransform(HumanBodyBones.Head);
            Transform b = avatarDescriptor.transform;

            Transform cameraPos = b.Find("VRCLens/WorldC/CamPickup/CamBase");
            //Transform cameraPos = r.Find("PickupA/DynVR");
            //cameraPos = cameraPos == null ? l.Find("PickupA/DynVR") : cameraPos;

            Transform tempPickC = h.Find("PickupC");
            Transform headPos = h.Find("PickupC/DynDesktop");

            Transform selfFocus = b.Find("VRCLens/CamScreen/AuxCopy");
            Transform focusPen = b.Find("VRCLens/WorldC/FocusPickup/FocusObject");

            if(tempPickC != null) // Conditional check for v1.3.x legacy VRCLens setup support
            {
                // HACK: VRChat scales down head by 10000 so when restoring, scale back down to 1 first and restore it back
                tempPickC.localScale = Vector3.one;
                PreserveSetupOnReset(cameraPos, headPos, selfFocus, focusPen, true);
                // Scale constraint is now used so do not adjust back!!
                // tempPickC.localScale = 10000f * Vector3.one;
            }

        }
        public void PreserveSetupOnReset(Transform mCamBase, Transform mHeadBase, Transform mSelfFocus, Transform mFocusPen, bool revertToInspector)
        {
            if(avatarDescriptor == null) return;
            Transform thisCamBase = transform.Find("CamBase");
            Transform thisHeadBase = transform.Find("HeadMountPoint");
            Transform thisSelfFocus = transform.Find("SelfieFocusPoint");
            Transform thisFocusPen = transform.Find("FocusObject");

            SetPosRot(thisCamBase, mCamBase);
            SetPosRot(thisHeadBase, mHeadBase);
            SetPosRot(thisSelfFocus, mSelfFocus);
            SetPosRot(thisFocusPen, mFocusPen);


            if(!revertToInspector) return;
            // Restoring inspector values

            if(mFocusPen != null)
            {
                focusPenRotation = mFocusPen.localRotation.eulerAngles;
            }
            if(mCamBase != null)
            {
                cameraRotation = mCamBase.localRotation.eulerAngles;
                cameraRotation.x = Mathf.Round(cameraRotation.x * 1000f) / 1000f;
                cameraRotation.y = Mathf.Round(cameraRotation.y * 1000f) / 1000f;
                cameraRotation.z = Mathf.Round(cameraRotation.z * 1000f) / 1000f;

                Transform camModel = mCamBase.Find("CamObject/CameraModel");
                Transform cm = null;
                if(camModel != null)
                {
                    cameraSize = camModel.localScale.y;
                    cm = camModel.childCount == 0 ? null : camModel.GetChild(0);
                    if(cm != null)
                    {
                        cameraModelPosition = cm.localPosition;
                        cameraModelRotation = cm.localRotation.eulerAngles;
                    }
                }
            }
        }

        private void SetPosRot(Transform from, Transform to)
        {
            if(to == null || from == null) return;
            from.position = to.position;
            from.rotation = to.rotation;
        }

        private void OverwriteAnimationSetup()
        {
            // Assumed not null from prior checks
            avatarDescriptor.customExpressions = true;
            avatarDescriptor.expressionsMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>($"{PrefabPath}/Menus2/VRCL2_Base.asset");
            avatarDescriptor.expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>($"{PrefabPath}/VRCLensParams.asset");

            RebuildPlayableLayers(avatarDescriptor);

            FindFXLayer(avatarDescriptor, out int overwriteIndex);
            VRCAvatarDescriptor.CustomAnimLayer anim = avatarDescriptor.baseAnimationLayers[overwriteIndex]; // Element 4 should be the FX layer
            if(anim.isDefault || anim.animatorController == null) // If it's default or empty
            {
                avatarDescriptor.baseAnimationLayers[overwriteIndex].isDefault = false;
                avatarDescriptor.baseAnimationLayers[overwriteIndex].animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{PrefabPath}/VRCLensFX.controller");
            }
        }

        public void RemovePreviousAnimations()
        {
            if(avatarDescriptor == null) return;
            RefreshCopyFolderPath();

            VRCExpressionParameters userParam = avatarDescriptor.expressionParameters;

            bool skipAppendParam = false, skipAppendFX = false;
            if(AssetDatabase.GetAssetPath(userParam) == $"{PrefabPath}/VRCLensParams.asset")
            {
                skipAppendParam = true;
                Debug.Log("Skipped AV3 VRCLens Parameter removal");
            }
            AnimatorController testAnim = FindFXLayer(avatarDescriptor, out _);
            if(testAnim == null || AssetDatabase.GetAssetPath(testAnim) == $"{PrefabPath}/VRCLensFX.controller")
            {
                skipAppendFX = true;
                Debug.Log("Skipped VRCLens FX Layer removal");
            }

            RemovePreviousAnimations(skipAppendParam, skipAppendFX);
        }

        public void AppendAnimationSetup()
        {
            RefreshCopyFolderPath();
            RebuildUserFolder();
            // Assumed not null from prior checks

            VRCExpressionsMenu userMenu = targetSubMenu == null ? avatarDescriptor.expressionsMenu : targetSubMenu;
            VRCExpressionParameters userParam = avatarDescriptor.expressionParameters;

            // Repair broken playerable layers made by broken SDK
            RepairPlayableLayers(avatarDescriptor);
            VRCAvatarDescriptor.CustomAnimLayer[] userBaseLayer = avatarDescriptor.baseAnimationLayers;
            // --- Has Menu and Parameters in Expressions customized and set up and is Playable Layers custom? --- //
            if(!avatarDescriptor.customExpressions || userMenu == null || userParam == null
                || !avatarDescriptor.customizeAnimationLayers || userBaseLayer == null || userBaseLayer.Length == 0)
            {
                // If not, do the overwrite setup
                OverwriteAnimationSetup();
                // It's been rebuilt from scratch so reference the newly created one
                userMenu = avatarDescriptor.expressionsMenu;
                userParam = avatarDescriptor.expressionParameters;
                userBaseLayer = avatarDescriptor.baseAnimationLayers;
                // However, Menu need to be modified to reflect the settings. So still got work to do
            }

            // If there is, then check Playable Layers' Base...
            AnimatorController prefabAnim = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{PrefabPath}/VRCLensFX.controller");

            // If FX layer is not customized, then I can skip Animator Controller's parameter and layer concatenation and just use VRCLensFX directly
            bool skipAppendParam = false, skipAppendFX = false;
            if(AssetDatabase.GetAssetPath(userParam) == $"{PrefabPath}/VRCLensParams.asset")
            {
                skipAppendParam = true;
                Debug.Log("Using default AV3 Parameter");
            }
            AnimatorController foundFXLayer = FindFXLayer(userBaseLayer, out int fxLayerIndex);
            if(foundFXLayer == null || foundFXLayer == prefabAnim)
            {
                avatarDescriptor.baseAnimationLayers[fxLayerIndex].isDefault = false;
                avatarDescriptor.baseAnimationLayers[fxLayerIndex].animatorController = prefabAnim;
                skipAppendFX = true;
                Debug.Log("Using default FX Layer");
            }
            RemovePreviousAnimations(skipAppendParam, skipAppendFX);

            // ---- STAGE 1: Append only new Avatars 3.0 parameters needed by this prefab ---- //
            EditorUtility.DisplayProgressBar("VRCLens Setup", "Adding VRCLens parameters", 0.3f);
            // IF parameter is directly from the prefab, no need to do this whole appending thing, regardless of unused parameters
            if(skipAppendParam)
            {
                AssetDatabase.CopyAsset($"{ResourcePath}/Template/VRCEP_Empty.asset", $"{_userCopyFolderPath}/VRCLensParams_Instance.asset");
                userParam = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>($"{_userCopyFolderPath}/VRCLensParams_Instance.asset"); // Error reported, but later allegedly user mistake
                userParam.parameters = new VRCExpressionParameters.Parameter[RequiredParameterSlots()]; // It's now dynamic
                for(int i = 0; i < userParam.parameters.Length; i++)
                {
                    userParam.parameters[i] = new VRCExpressionParameters.Parameter();
                    userParam.parameters[i].name = "";
                }
                avatarDescriptor.expressionParameters = userParam;
            }

            // Now append new params into the old list
            VRCExpressionParameters.Parameter[] appendList = GenerateRequiredParams();

            List<VRCExpressionParameters.Parameter> userList = new List<VRCExpressionParameters.Parameter>(userParam.parameters);
            // Remove all slots that is empty and...
            userList.RemoveAll(p => p.name == "");

            // Add back parameters used by VRCLens except if already added
            foreach(VRCExpressionParameters.Parameter appendParam in appendList)
            {
                bool exists = false;
                foreach(VRCExpressionParameters.Parameter existingParam in userList)
                {
                    if(appendParam.name == existingParam.name)
                    {
                        exists = true;
                        break;
                    }
                }
                if(exists) continue;

                userList.Add(appendParam);
            }
            userParam.parameters = userList.ToArray();

            // Finalize the changes of the AV3 parameters
            _reserializeManager.Add(AssetDatabase.GetAssetPath(userParam));

            // Debug.Log(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(avatar.avatar)));
            // ---- STAGE 2: Append only a submenu in Expression Menu, and remove controls using missing params ---- //
            EditorUtility.DisplayProgressBar("VRCLens Setup", "Removing old VRCLens menu controls", 0.4f);
            // If the user menu is the prefab itself, don't do anything!
            if(AssetDatabase.GetAssetPath(userMenu) == $"{PrefabPath}/Menus2/VRCL2_Base.asset")
            {
                Debug.Log("Using default AV3 Menu");
                AssetDatabase.CopyAsset($"{ResourcePath}/Template/VRCEM_Empty.asset", $"{_userCopyFolderPath}/VRCLensBaseMenu_Instance.asset");

                userMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>($"{_userCopyFolderPath}/VRCLensBaseMenu_Instance.asset");
                avatarDescriptor.expressionsMenu = userMenu;
            }

            // Make a copy of all the VRCLens menu to keep the original (DO NOT MODIFY CUSTOMER's MENUS)
            // HACK: Copying or deleting an entire folder caused issues with AssetDatabase sync - it's better to manually copy the assets itself

            string m = $"{_userCopyFolderPath}/Menus2";
            string[] expMenuFileNames = GetAllFileNamesInPath($"{PrefabPath}/Menus2", "*.asset");

            if(AssetDatabase.IsValidFolder(m))
            {
                int idx = 0;
                foreach(string n in expMenuFileNames)
                {
                    float p = Mathf.Lerp(0.4f, 0.5f, (float)idx++ / expMenuFileNames.Length);
                    EditorUtility.DisplayProgressBar("VRCLens Setup", "Removing old menu controls", p);

                    AssetDatabase.DeleteAsset($"{m}/{n}");
                }
            } else
            {
                AssetDatabase.CreateFolder(_userCopyFolderPath, "Menus2");
            }

            for(int i = 0; i < expMenuFileNames.Length; i++)
            {
                string n = expMenuFileNames[i];
                float p = Mathf.Lerp(0.5f, 0.70f, (float)i / expMenuFileNames.Length);
                EditorUtility.DisplayProgressBar("VRCLens Setup", "Copying VRCLens menu controls", p);

                AssetDatabase.CopyAsset($"{PrefabPath}/Menus2/{n}", $"{m}/{n}");
            }

            EditorUtility.DisplayProgressBar("VRCLens Setup", "Adding VRCLens menu controls", 0.70f);
            // Create a copy of VRCLens controls, then remove the copy's controls that uses invalid parameters
            VRCExpressionsMenu instanceMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>($"{m}/VRCL2_Base.asset");
            VRCExpressionsMenu altControls = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>($"{m}/VRCL2_Template.asset");

            RebuildAllMenuControls(instanceMenu, false);

            if(!av3Zoom)
            {
                ReplaceSubMenuControl(instanceMenu, VRCExpressionsMenu.Control.ControlType.RadialPuppet, "Zoom", altControls.controls[1]);
            }
            if(!av3Exposure)
            {
                ReplaceSubMenuControl(instanceMenu, VRCExpressionsMenu.Control.ControlType.RadialPuppet, "EV Dial", altControls.controls[3]);
            }
            if(!av3Aperture)
            {
                ReplaceSubMenuControl(instanceMenu, VRCExpressionsMenu.Control.ControlType.RadialPuppet, "Av Dial", altControls.controls[3]);
            }
            if(!av3Focus)
            {
                ReplaceSubMenuControl(instanceMenu, VRCExpressionsMenu.Control.ControlType.RadialPuppet, "Manual Focus", altControls.controls[2]);
            }
            if(!av3Puppet)
            {
                ReplaceSubMenuControl(instanceMenu, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, "Move Focus", altControls.controls[4]);
            }



            if(!useDisableButton)
            {
                ReplaceSubMenuControl(instanceMenu, VRCExpressionsMenu.Control.ControlType.Button, "Disable", null);
            }

            RebuildAllMenuControls(instanceMenu, true, altControls.controls[0]);


            userMenu.controls.Add(instanceMenu.controls[0]);

            // Finalize the addition of the prefab's submenu control, hopefully - Credits to Haï~ for discovery
            _reserializeManager.Add(AssetDatabase.GetAssetPath(userMenu));


            // ---- STAGE 3: Append only new animator parameters and layers ---- //
            EditorUtility.DisplayProgressBar("VRCLens Setup", "Copying VRCLens animations", 0.80f);
            // Make and use a copy of animator because state machine and animator are referenced, not copied!
            AssetDatabase.CopyAsset($"{PrefabPath}/VRCLensFX.controller", $"{_userCopyFolderPath}/VRCLensFX_REF.controller");
            AssetDatabase.CopyAsset($"{PrefabPath}/Anim/HeadScaleLocal.anim", $"{_userCopyFolderPath}/HeadScaleLocal.anim");
            AssetDatabase.CopyAsset($"{PrefabPath}/Anim/HeadScaleRemote.anim", $"{_userCopyFolderPath}/HeadScaleRemote.anim");

            // --- PROCEDURE 1: Animator Defaults Manipulation --- //

            AnimatorController instanceAnim = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{_userCopyFolderPath}/VRCLensFX_REF.controller");
            ModifyStateDefaults(instanceAnim);

            // --- PROCEDURE 2: User Animator Initialization --- //

            // If the FX layer is from the default/prefab, create default instance of it
            if(skipAppendFX)
            {
                // Things has changed now - ALWAYS copy over the animator, even for the overwrite setup
                AssetDatabase.CreateAsset(new AnimatorController(), $"{_userCopyFolderPath}/VRCLensFX_Instance.controller");
                AnimatorController newInst = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{_userCopyFolderPath}/VRCLensFX_Instance.controller");
                newInst.AddLayer("Base Layer");
                userBaseLayer[fxLayerIndex].animatorController = newInst;
            }

            // Append new animator layers
            AnimatorController userAnim = userBaseLayer[fxLayerIndex].animatorController as AnimatorController;


            // --- PROCEDURE 3: "MERGING ANIMATORS" --- //
            // Animator 1: instanceAnim
            // Animator 2: userAnim
            try
            {
                // Utilize VRLabs' Avatars 3.0 Manager's controller merging library (MIT License)
                // Make sure not to include base layer in VRCLens animator prefab

                
                EditorUtility.DisplayProgressBar("VRCLens Setup", "Combining user and VRCLens animators", 0.90f);

                AnimatorCloner.MergeControllers(userAnim, instanceAnim);
                // Using Jun 17, 2021 commit of AnimatorCloner, which should now deep copy blend tree
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(instanceAnim));

                Debug.Log($"[VRCLens {Managed.VRCLensVerifier.VRCLENS_VERSION}] {DateTime.Now.ToString("hh:mm tt")} -- Setup Complete -- 組み込み処理完了 --");
            } catch(Exception e)
            {
                _hasNoErrors = false;
                Debug.LogException(e);
            } finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public void Configurate()
        {
            RefreshCopyFolderPath();
            RebuildUserFolder();

            if(transform.parent != null) transform.SetParent(null);
            RemovePreviousSetup();
            Transform pickupA = transform.Find("PickupA");
            Transform pickupB = transform.Find("PickupB");
            Transform pickupC = transform.Find("PickupC");

            Transform camBase = transform.Find("CamBase");
            Transform camObject = camBase.Find("CamObject");
            Transform focusObject = transform.Find("FocusObject");
            Transform camPickup = transform.Find("WorldC/CamPickup");
            Transform camPickupAlways = transform.Find("WorldC/CamPickupAlways");
            Transform focusPickup = transform.Find("WorldC/FocusPickup");
            Transform pivotPickup = transform.Find("WorldC/PivotPickup");
            Transform lookAtC = transform.Find("WorldC/LookAtC");

            Transform camScreen = transform.Find("CamScreen");
            Transform screenOverride = camScreen.Find("ScreenOverride");
            Transform auxCopy = camScreen.Find("AuxCopy");

            Transform selfiePoint = transform.Find("SelfieFocusPoint");
            Transform camHeadPoint = transform.Find("HeadMountPoint");

            Transform camModel = camObject.Find("CameraModel");
            Transform previewBase = camObject.Find("CameraModel/PreviewBase");

            Transform vrcLensRoot = transform.Find("VRCLens");

            _armatureHead = Avatar.GetBoneTransform(HumanBodyBones.Head);
            Transform armatureRoot = avatarDescriptor.transform;
            Transform armatureLeftHand = Avatar.GetBoneTransform(setupMode == SetupMode.VRRightHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform armatureRightHand = Avatar.GetBoneTransform(setupMode == SetupMode.VRRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform armatureHips = Avatar.GetBoneTransform(HumanBodyBones.Hips);
            Transform armatureLeftFoot = Avatar.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform armatureRightFoot = Avatar.GetBoneTransform(HumanBodyBones.RightFoot);
            
            // [1]
            pickupA.SetParent(armatureRightHand);
            pickupA.localPosition = Vector3.zero;
            pickupA.localRotation = Quaternion.identity;
            pickupA.localScale = Vector3.one;

            pickupB.SetParent(armatureLeftHand);
            pickupB.localPosition = Vector3.zero;
            pickupB.localRotation = Quaternion.identity;
            pickupB.localScale = Vector3.one;

            pickupC.SetParent(_armatureHead);
            pickupC.localPosition = Vector3.zero;
            pickupC.localRotation = Quaternion.identity;
            pickupC.localScale = Vector3.one;

            vrcLensRoot.SetParent(armatureRoot);
            vrcLensRoot.localPosition = Vector3.zero;
            vrcLensRoot.localRotation = Quaternion.identity;
            //vrcLensRoot.localScale = Vector3.one;

            // [2]
            Transform dynVR = pickupA.Find("DynVR");
            Transform dynVRAlt = pickupB.Find("DynVRAlt");
            Transform dynDesktop = pickupC.Find("DynDesktop");
            Transform headForward = pickupC.Find("HeadForward");
            dynVR.SetPositionAndRotation(camBase.position, camBase.rotation);
            dynVRAlt.SetPositionAndRotation(focusObject.position, focusObject.rotation);
            dynDesktop.SetPositionAndRotation(camHeadPoint.position, camHeadPoint.rotation);
            headForward.position = selfiePoint.position;

            Transform pBase = pivotPickup.Find("PBase");
            pBase.position = focusObject.position;
            pBase.rotation = focusObject.rotation;
            Transform pDroneBase = pBase.Find("PObject/PDroneBase");

            // ActivateRotationConstraint(pBase.Find("PObject/PDroneBase").GetComponent<RotationConstraint>(), pickupB);
            // ActivateRotationConstraint(pBase.Find("PObject/PDroneBase").GetComponent<RotationConstraint>(), pickupC);

            ActivateParentConstraint(focusPickup.GetComponent<ParentConstraint>(), pickupB);
            // ActivateParentConstraint(focusPickup.GetComponent<ParentConstraint>(), pickupC);
            focusObject.SetParent(focusPickup);

            // UNDONE: These might actually be unnecessery!
            /*
            ActivateParentConstraint(pivotPickup.GetComponent<ParentConstraint>(), pickupB);
            ActivateParentConstraint(pivotPickup.GetComponent<ParentConstraint>(), pickupC);

            ActivateParentConstraint(camPickup.GetComponent<ParentConstraint>(), pickupA);
            ActivateParentConstraint(camPickup.GetComponent<ParentConstraint>(), pickupC);
            */
            camPickupAlways.SetPositionAndRotation(previewBase.position, previewBase.rotation);
            pivotPickup.SetPositionAndRotation(pBase.position, pBase.rotation);
            SetPositionConstraintSource(lookAtC.GetComponent<PositionConstraint>(), armatureHips, 1);
            SetPositionConstraintSource(lookAtC.GetComponent<PositionConstraint>(), armatureLeftFoot, 2);
            SetPositionConstraintSource(lookAtC.GetComponent<PositionConstraint>(), armatureRightFoot, 3);
            lookAtC.GetComponent<PositionConstraint>().locked = true;
            lookAtC.GetComponent<PositionConstraint>().constraintActive = true;

            ActivateParentConstraint(camPickupAlways.GetComponent<ParentConstraint>(), dynVR);
            // ActivateParentConstraint(camPickupAlways.GetComponent<ParentConstraint>(), pickupC);
            ActivateScaleConstraint(pickupC.GetComponent<ScaleConstraint>(), _armatureHead.parent);

            // [3]
            camBase.SetParent(camPickup);
            previewBase.SetParent(camPickupAlways);

            while(transform.childCount > 0)
            {
                Transform c = transform.GetChild(0);
                c.SetParent(vrcLensRoot);
                c.localScale = Vector3.one;
            }

            camBase.GetComponent<ParentConstraint>().translationAtRest = camBase.localPosition;
            camBase.GetComponent<ParentConstraint>().rotationAtRest = camBase.localEulerAngles;
            camBase.GetComponent<ParentConstraint>().locked = true;
            camBase.GetComponent<ParentConstraint>().constraintActive = true;

            //pBase.GetComponent<ParentConstraint>().translationAtRest = pBase.localPosition;
            //pBase.GetComponent<ParentConstraint>().rotationAtRest = pBase.localEulerAngles;
            pBase.GetComponent<ParentConstraint>().locked = true;
            pBase.GetComponent<ParentConstraint>().constraintActive = true;

            //pDroneBase.GetComponent<RotationConstraint>().rotationAtRest = Vector3.zero;
            pDroneBase.GetComponent<RotationConstraint>().locked = true;
            pDroneBase.GetComponent<RotationConstraint>().constraintActive = true;

            // ZeroPositionConstraint(camScreen.GetComponent<PositionConstraint>(), _head);
            camScreen.SetPositionAndRotation(_armatureHead.position, _armatureHead.rotation);
            // ActivateRotationConstraint(screenOverride.GetComponent<RotationConstraint>(), _head);

            auxCopy.position = selfiePoint.position; // Pre-emptively adjust before activating constraint
            ActivateParentConstraint(auxCopy.GetComponent<ParentConstraint>(), _armatureHead);

            // -- Editor indicator removals -- //
            DestroyImmediate(selfiePoint.gameObject);
            DestroyImmediate(camHeadPoint.gameObject);
            DestroyImmediate(focusObject.Find("FocusP/EditorDisplay").gameObject);
            DestroyImmediate(previewBase.Find("PreviewMesh/EditorDisplay").gameObject);

            // -- Post-setup alterations -- //
            Transform previewMesh = previewBase.Find("PreviewMesh");
            previewMesh.localScale = new Vector3(previewMesh.localScale.x, previewMesh.localScale.y, previewMesh.localScale.x);
            previewBase.gameObject.SetActive(false);
            focusObject.gameObject.SetActive(false);
            camModel.gameObject.SetActive(false);

            EditorUtility.DisplayProgressBar("VRCLens Setup", "Copying materials", 0.00f);

            // -- Make a copy of material, don't use the prefab, don't use instance -- //

            /* UNDONE: Copying auxMat is no longer necessery: Desktop/VR switch is now controlled via animation using built-in VRMode parameter
            Material prefabAuxMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabPath}/DisplayAux.mat");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefabAuxMat), GetCopyFilePath(prefabAuxMat));
            Material auxMat = AssetDatabase.LoadAssetAtPath<Material>(GetCopyFilePath(prefabAuxMat));
            */

            Material prefabCamMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabPath}/CamMaterial.mat");
            Material prefabOutMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabPath}/OutputMaterial.mat");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefabCamMat), GetCopyFilePath(prefabCamMat));
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefabOutMat), GetCopyFilePath(prefabOutMat));
            Material camMat = AssetDatabase.LoadAssetAtPath<Material>(GetCopyFilePath(prefabCamMat));
            Material outMat = AssetDatabase.LoadAssetAtPath<Material>(GetCopyFilePath(prefabOutMat));

            camMat.SetFloat("_BlurSamples", maxBlurSize);
            camMat.SetFloat("_SensorScale", 1f / _sensorSizeValues[sensorSizeIndex]);

            EditorUtility.DisplayProgressBar("VRCLens Setup", "Copying render textures", 0.05f);

            RenderTexture prefabCamTexColor = camMat.GetTexture("_RenderTex") as RenderTexture; // Checked cast (C# feature)
            RenderTexture prefabCamTexDepth = camMat.GetTexture("_DepthTex") as RenderTexture;
            RenderTexture prefabCamTexOutpt = outMat.GetTexture("_OutputTex") as RenderTexture;

            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefabCamTexColor), GetCopyFilePath(prefabCamTexColor));
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefabCamTexDepth), GetCopyFilePath(prefabCamTexDepth));
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(prefabCamTexOutpt), GetCopyFilePath(prefabCamTexOutpt));

            RenderTexture camTexColor = AssetDatabase.LoadAssetAtPath<RenderTexture>(GetCopyFilePath(prefabCamTexColor));
            RenderTexture camTexDepth = AssetDatabase.LoadAssetAtPath<RenderTexture>(GetCopyFilePath(prefabCamTexDepth));
            RenderTexture camTexOutpt = AssetDatabase.LoadAssetAtPath<RenderTexture>(GetCopyFilePath(prefabCamTexOutpt));

            switch(sensorResIndex)
            {
                case 0: customSensorRes = new Vector2Int(1920, 1080); break;
                case 1: customSensorRes = new Vector2Int(2560, 1440); break;
                case 2: customSensorRes = new Vector2Int(3840, 2160); break;
            }
            int multisample = 1;
            switch(msaaIndex)
            {
                case 1: // Up to 2240x1260 = 4x | Up to 3200x1800 = 2x | Otherwise 1x
                    multisample = customSensorRes.x * customSensorRes.y <= 2822400 ? 4
                        : customSensorRes.x * customSensorRes.y <= 5760000 ? 2 : 1;
                    break;
                case 2: multisample = 2; break;
                case 3: multisample = 4; break;
                case 4: multisample = 8; break;
            }
            Vector3Int sensorWHS = new Vector3Int(customSensorRes.x, customSensorRes.y, multisample);

            if(camTexColor != null)
            {
                camTexColor.width = sensorWHS.x;
                camTexColor.height = sensorWHS.y;
                camTexColor.antiAliasing = sensorWHS.z; // I assume that's 4 samples?
            }
            if(camTexDepth != null)
            {
                camTexDepth.width = sensorWHS.x;
                camTexDepth.height = sensorWHS.y;
            }
            if(camTexOutpt != null)
            {
                camTexOutpt.width = sensorWHS.x;
                camTexOutpt.height = sensorWHS.y;
            }


            previewMesh.GetComponent<Renderer>().sharedMaterial = outMat;
            screenOverride.GetComponent<Renderer>().sharedMaterial = camMat;
            // auxCopy.GetComponent<Renderer>().sharedMaterial = auxMat;

            screenOverride.GetComponent<Renderer>().sharedMaterial.SetTexture("_RenderTex", camTexColor);
            screenOverride.GetComponent<Renderer>().sharedMaterial.SetTexture("_DepthTex", camTexDepth);
            previewMesh.GetComponent<Renderer>().sharedMaterial.SetTexture("_OutputTex", camTexOutpt);

            Transform lensChild = camObject.Find("LensC/LensParent/LensChild");
            lensChild.Find("Camera_Color").GetComponent<Camera>().targetTexture = camTexColor;
            lensChild.Find("Camera_ColorAvatar").GetComponent<Camera>().targetTexture = camTexColor;
            lensChild.Find("Camera_Depth").GetComponent<Camera>().targetTexture = camTexDepth;
            lensChild.Find("Camera_DepthAvatar").GetComponent<Camera>().targetTexture = camTexDepth;
            // Stereographic
            lensChild.Find("Stereo/Left/CamLeft_Color").GetComponent<Camera>().targetTexture = camTexColor;
            lensChild.Find("Stereo/Left/CamLeft_Depth").GetComponent<Camera>().targetTexture = camTexDepth;
            lensChild.Find("Stereo/Right/CamRight_Color").GetComponent<Camera>().targetTexture = camTexColor;
            lensChild.Find("Stereo/Right/CamRight_Depth").GetComponent<Camera>().targetTexture = camTexDepth;
            // Output camera
            screenOverride.Find("OutputCamera").GetComponent<Camera>().targetTexture = camTexOutpt;

            // Debug.Log("-- Object setup finished -- オブジェクト組み込み処理終了 --");
        }

        private void ReplaceSubMenuControl(VRCExpressionsMenu menu, VRCExpressionsMenu.Control.ControlType type, string controlName, VRCExpressionsMenu.Control replacement, int recursionDepth = 0)
        {
            if(recursionDepth >= AV3_MAXMENUDEPTH)
            {
                Debug.LogWarning($"Sub Menu circular reference found!", menu);
                return;
            }

            for(int i = 0; i < menu.controls.Count; i++)
            {
                VRCExpressionsMenu.Control c = menu.controls[i];

                // If the control's name match, replace or remove it!
                if(c.type == type && c.name == controlName)
                {
                    if(replacement == null)
                    {
                        menu.controls.RemoveAt(i--);
                    } else
                    {
                        menu.controls[i] = replacement;
                    }
                    // Finalize the replacement (unmodified submenu are left alone)
                    _reserializeManager.Add(AssetDatabase.GetAssetPath(menu));
                }
                if(c.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    if(c.subMenu != null && recursionDepth < AV3_MAXMENUDEPTH)
                    {
                        // Otherwise recursion to the rescue!
                        ReplaceSubMenuControl(c.subMenu, type, controlName, replacement, recursionDepth + 1);
                    }
                }
            }
            // If there's no submenu with matching control's name, return null
        }
        // Only call this with VRCLens base menu
        private void RebuildAllMenuControls(VRCExpressionsMenu menu, bool replaceDisabled, VRCExpressionsMenu.Control replacement = null)
        {
            if(menu == null) return; // Something is definitely wrong as this part is what is in control
            string copyFolder = $"{_userCopyFolderPath}/Menus2";

            bool hasRemoved = false;
            for(int i = 0; i < menu.controls.Count; i++)
            {
                VRCExpressionsMenu.Control c = menu.controls[i];
                if(c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null)
                {
                    // HACK: The references don't update by themselves! Change it manually!
                    string[] path = AssetDatabase.GetAssetPath(c.subMenu).Split('/');
                    string filename = path[path.Length - 1];

                    c.subMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>($"{copyFolder}/{filename}");

                    RebuildAllMenuControls(c.subMenu, replaceDisabled, replacement);
                    // Finalize the change of menu references
                    _reserializeManager.Add(AssetDatabase.GetAssetPath(menu));
                }

                bool doRemove = false;
                if(replaceDisabled)
                {
                    if(c.type == VRCExpressionsMenu.Control.ControlType.Button || c.type == VRCExpressionsMenu.Control.ControlType.Toggle)
                    {
                        if(!IsValidParameter(c.parameter))
                        {
                            doRemove = true;
                        }
                    }

                    if(c.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet
                        || c.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet
                        || c.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet)
                    {
                        foreach(VRCExpressionsMenu.Control.Parameter param in c.subParameters)
                        {
                            if(!IsValidParameter(param))
                            {
                                doRemove = true;
                            }
                        }
                    }

                    if(doRemove)
                    {
                        if(replacement == null)
                        {
                            menu.controls.RemoveAt(i--);
                        } else
                        {
                            menu.controls[i] = replacement;
                        }
                        hasRemoved = true;
                    }
                }
            }

            if(hasRemoved)
            {
                // Finalize the removal of menu controls
                _reserializeManager.Add(AssetDatabase.GetAssetPath(menu));
            }
        }

        private bool IsValidParameter(VRCExpressionsMenu.Control.Parameter param)
        {
            if(param == null) return true; // It's null or empty

            int i = Array.IndexOf(_pNameList, param.name);
            if(i == -1) return true; // Foreign parameter - consider it valid

            // If the index found is what the bool is using, then that is valid
            bool exists = false;
            exists |= i == 0;
            exists |= i == 1 && false;// av3Interrupt; [Backward compatibility reasons]
            exists |= i == 2 && av3Puppet;
            exists |= i == 3 && av3Puppet;
            exists |= i == 4 && av3Zoom;
            exists |= i == 5 && av3Exposure;
            exists |= i == 6 && av3Aperture;
            exists |= i == 7 && av3Focus;
            return exists;
        }

        public void RemovePreviousSetup()
        {
            Transform l = Avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform r = Avatar.GetBoneTransform(HumanBodyBones.RightHand);
            Transform h = Avatar.GetBoneTransform(HumanBodyBones.Head);
            Transform b = avatarDescriptor.transform;

            SearchAndDestroy(l, "PickupA");
            SearchAndDestroy(l, "PickupB");

            SearchAndDestroy(r, "PickupA");
            SearchAndDestroy(r, "PickupB");

            SearchAndDestroy(h, "PickupA"); // v1.3.x legacy migration
            SearchAndDestroy(h, "PickupC");

            SearchAndDestroy(b, "VRCLens");
        }

        private void RemovePreviousAnimations(bool skipParam, bool skipFXLayer)
        {
            if(avatarDescriptor == null) return;

            // ---- PART 1: Remove VRCLens parameters ---- //
            VRCExpressionParameters param = avatarDescriptor.expressionParameters;
            if(param != null && !skipParam)
            {
                List<VRCExpressionParameters.Parameter> currentList = new List<VRCExpressionParameters.Parameter>(param.parameters);
                VRCExpressionParameters.Parameter[] removeList = GenerateAllNewParams();
                for(int i = 0; i < currentList.Count; i++)
                {
                    // Don't remove the commonly used parameter names, including default ones - you'll destroy their animations!!
                    if(Array.Exists(_defaultUsedList, name => name == currentList[i].name)) continue;

                    foreach(VRCExpressionParameters.Parameter p in removeList)
                    {
                        // If the old slot's name is in new list, remove it from slot
                        if(currentList[i].name == p.name)
                        {
                            currentList.RemoveAt(i--);
                            break;
                        }
                    }
                }
                param.parameters = currentList.ToArray(); // Back to array
                // Finalize the removal of AV3 parameters
                _reserializeManager.Add(AssetDatabase.GetAssetPath(param));
            }

            // ---- PART 2: Remove VRCLens menu controls ---- //
            VRCExpressionsMenu menu = avatarDescriptor.expressionsMenu;
            if(menu != null && AssetDatabase.GetAssetPath(menu) != $"{PrefabPath}/Menus2/VRCL2_Base.asset")
            {
                // DO NOT REMOVE CONTROLS INSIDE VRCLENS! Only remove user's access to VRCLens and nothing else
                // Also, DO NOT REMOVE THE PREFAB's MENU ITSELF!
                ReplaceSubMenuControl(menu, VRCExpressionsMenu.Control.ControlType.SubMenu, " VRCLens ", null);
                // Also still have to make a copy of VRCLens controls and modify it to reflect parameter restrictions
            }

            // ---- PART 3: Remove VRLens animator parameters and layers ---- //
            if(!skipFXLayer)
            {
                _hasNoErrors = true;
                AnimatorController fx = skipFXLayer ? null : FindFXLayer(avatarDescriptor, out _);
                AnimatorController brokenFX = avatarDescriptor.baseAnimationLayers == null || avatarDescriptor.baseAnimationLayers.Length < 5
                    ? null : avatarDescriptor.baseAnimationLayers[3].animatorController as AnimatorController;

                RemoveAnimatorSetup(brokenFX);
                RemoveAnimatorSetup(fx);
            }
        }

        private void RemoveAnimatorSetup(AnimatorController fx)
        {
            if(fx == null) return;

            AnimatorController sourceAnim = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{PrefabPath}/VRCLensFX.controller");
            try
            {
                // Remove existing animator parameters
                for(int i = 0; i < fx.parameters.Length; i++)
                {
                    // Don't remove default parameter names - you'll destroy their animations!!
                    if(Array.Exists(_defaultParamsList, resParamName => resParamName == fx.parameters[i].name))
                    {
                        continue;
                    }

                    foreach(AnimatorControllerParameter p in sourceAnim.parameters)
                    {
                        if(p.name == fx.parameters[i].name)
                        {
                            fx.RemoveParameter(i--);
                            break;
                        }
                    }
                }
                // Remove existing layers (without using RemoveLayer)
                List<AnimatorControllerLayer> userLayersList = new List<AnimatorControllerLayer>(fx.layers);
                userLayersList.RemoveAll(elem
                    => elem.name.StartsWith("vCNT_")
                    || elem.name.StartsWith("vCNG_")
                    || elem.name.StartsWith("vCNR_")
                    || elem.name.StartsWith("vCNP_"));
                fx.layers = userLayersList.ToArray();
            } catch(Exception e)
            {
                _hasNoErrors = false;
                Debug.LogException(e);
            } finally
            {
                EditorUtility.SetDirty(fx);
                //AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private bool SearchAndDestroy(Transform root, string name)
        {
            Transform t = root.Find(name);
            bool exists = t != null && !PrefabUtility.IsPartOfPrefabInstance(t);
            if(exists)
            {
                DestroyImmediate(t.gameObject);
            }
            return exists;
        }

        private void RebuildUserFolder()
        {
            if(!AssetDatabase.IsValidFolder(UserPath))
            {
                AssetDatabase.CreateFolder(RootPath, "User");
            }
            if(!AssetDatabase.IsValidFolder(_userCopyFolderPath))
            {
                AssetDatabase.CreateFolder(UserPath, StringToFilename(Avatar.name));
            }

        }

        public bool RefreshCopyFolderPath()
        {
            RootPath = GetParentDirectory(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this)), 2);
            if(Avatar == null) return false;

            _userCopyFolderPath = $"{UserPath}/{StringToFilename(Avatar.name)}";
            return true;
        }

        public void RefreshAssetReserializeList()
        {
            if(_reserializeManager == null)
            {
                _reserializeManager = new VRCLensAssetManager();
            }
        }
        public int ReserializeAssetList()
        {
            return _reserializeManager.ForceReserializeAll();
        }
        public bool SaveSettingsToFile()
        {
            if(_profileManager == null)
            {
                _profileManager = new VRCLensProfileManager();
                _profileManager.SetContext(this);
            }

            string unityPath = EditorUtility.SaveFilePanelInProject("Save VRCLens profile", StringToFilename(Avatar.name), "json", "Enter the name of the settings profile to be saved", UserPath);
            return _profileManager.SaveToJSONFile(unityPath);
        }
        public VRCLensProfileManager ReadSettingsFromFile()
        {
            if(_profileManager == null)
            {
                _profileManager = new VRCLensProfileManager();
                _profileManager.SetContext(this);
            }

            string unityPath = EditorUtility.OpenFilePanelWithFilters("Load VRCLens profile", UserPath, new string[] { "json file", "json" });
            return _profileManager.ReadFromJSONFile(unityPath);
        }
        public void ApplySettingsFromProfile(VRCLensProfileManager source, bool keepPos)
        {
            _profileManager.ApplyLoadedProfile(source, keepPos);
        }

        private string[] GetAllFileNamesInPath(string unityPath, string filter)
        {
            string fullPath = Path.GetDirectoryName($"{Application.dataPath}") + $@"/{unityPath}";

            string[] fileNames = Directory.GetFiles(fullPath, filter);
            for(int i = 0; i < fileNames.Length; i++)
            {
                fileNames[i] = Path.GetFileName(fileNames[i]);
            }
            return fileNames;
        }

        private string GetCopyFilePath(UnityEngine.Object theAsset)
        {
            if(theAsset == null) return null;

            string[] path = AssetDatabase.GetAssetPath(theAsset).Split('/');
            string filename = path[path.Length - 1];

            return $"{_userCopyFolderPath}/{filename}";
        }
        private string GetParentDirectory(string path, int levels = 1)
        {
            string[] file = path.Split('/');
            int last = file.Length - 1;

            int sumLength = 0;
            for(int i = last; i > last - levels; i--)
            {
                sumLength += file[i].Length + 1;
            }

            return path.Substring(0, path.Length - sumLength);
        }
        private string StringToFilename(string fileName)
        {
            foreach(char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            string[] systemFilenames = { "CON", "PRN", "AUX", "CLOCK$", "NUL",
                "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

            foreach(string fn in systemFilenames)
            {
                if(string.Equals(fn, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    fileName += "_";
                    break;
                }
            }

            string trimmedName = fileName.Replace('.', '_').Trim();

            // This should normally not be performed with normal avatar names
            // Will only do so if avatar name is just whitespaces
            return trimmedName == "" ? fileName.GetHashCode().ToString() : trimmedName;
        }

        private void RebuildPlayableLayers(VRCAvatarDescriptor av)
        {
            CustomizeBaseLayers(av);
            CustomizeSpecialLayers(av);
        }
        private void CustomizeBaseLayers(VRCAvatarDescriptor av)
        {
            av.customizeAnimationLayers = true;
            if(av.baseAnimationLayers == null || av.baseAnimationLayers.Length == 0)
            {
                av.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[5];
                av.baseAnimationLayers[0].isDefault = true;
                av.baseAnimationLayers[1].isDefault = true;
                av.baseAnimationLayers[2].isDefault = true;
                av.baseAnimationLayers[3].isDefault = true;
                av.baseAnimationLayers[4].isDefault = true;

                av.baseAnimationLayers[0].type = VRCAvatarDescriptor.AnimLayerType.Base;
                av.baseAnimationLayers[1].type = VRCAvatarDescriptor.AnimLayerType.Additive;
                av.baseAnimationLayers[2].type = VRCAvatarDescriptor.AnimLayerType.Gesture;
                av.baseAnimationLayers[3].type = VRCAvatarDescriptor.AnimLayerType.Action;
                av.baseAnimationLayers[4].type = VRCAvatarDescriptor.AnimLayerType.FX;
            }
        }
        private void CustomizeSpecialLayers(VRCAvatarDescriptor av)
        {
            av.customizeAnimationLayers = true;
            if(av.specialAnimationLayers == null || av.specialAnimationLayers.Length == 0)
            {
                av.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[3];
                av.specialAnimationLayers[0].isDefault = true;
                av.specialAnimationLayers[1].isDefault = true;
                av.specialAnimationLayers[2].isDefault = true;

                av.specialAnimationLayers[0].type = VRCAvatarDescriptor.AnimLayerType.Sitting;
                av.specialAnimationLayers[1].type = VRCAvatarDescriptor.AnimLayerType.TPose;
                av.specialAnimationLayers[2].type = VRCAvatarDescriptor.AnimLayerType.IKPose;
            }
        }

        private void RepairPlayableLayers(VRCAvatarDescriptor av)
        {
            // Automatically repair broken layer created by broken SDK
            if(av.customizeAnimationLayers == false) return;
            if(av.baseAnimationLayers == null || av.baseAnimationLayers.Length == 0) return;

            if(av.baseAnimationLayers.Length > 5)
            {
                VRCAvatarDescriptor.CustomAnimLayer[] sub = new VRCAvatarDescriptor.CustomAnimLayer[5];
                Array.Copy(av.baseAnimationLayers, sub, 5);
                av.baseAnimationLayers = sub;
            } // Fix made in 1.7.4: If it's null, it need to be default
            av.baseAnimationLayers[0].isDefault = av.baseAnimationLayers[0].animatorController == null;
            av.baseAnimationLayers[1].isDefault = av.baseAnimationLayers[1].animatorController == null;
            av.baseAnimationLayers[2].isDefault = av.baseAnimationLayers[2].animatorController == null;
            av.baseAnimationLayers[3].isDefault = av.baseAnimationLayers[3].animatorController == null;
            av.baseAnimationLayers[4].isDefault = av.baseAnimationLayers[4].animatorController == null;

            av.baseAnimationLayers[0].type = VRCAvatarDescriptor.AnimLayerType.Base;
            av.baseAnimationLayers[1].type = VRCAvatarDescriptor.AnimLayerType.Additive;
            av.baseAnimationLayers[2].type = VRCAvatarDescriptor.AnimLayerType.Gesture;
            av.baseAnimationLayers[3].type = VRCAvatarDescriptor.AnimLayerType.Action;
            av.baseAnimationLayers[4].type = VRCAvatarDescriptor.AnimLayerType.FX;

            if(av.specialAnimationLayers == null || av.specialAnimationLayers.Length == 0) return;

            if(av.specialAnimationLayers.Length > 3)
            {
                VRCAvatarDescriptor.CustomAnimLayer[] sub = new VRCAvatarDescriptor.CustomAnimLayer[5];
                Array.Copy(av.specialAnimationLayers, sub, 3);
                av.specialAnimationLayers = sub;
            }
            av.specialAnimationLayers[0].isDefault = av.specialAnimationLayers[0].animatorController == null;
            av.specialAnimationLayers[1].isDefault = av.specialAnimationLayers[1].animatorController == null;
            av.specialAnimationLayers[2].isDefault = av.specialAnimationLayers[2].animatorController == null;

            av.specialAnimationLayers[0].type = VRCAvatarDescriptor.AnimLayerType.Sitting;
            av.specialAnimationLayers[1].type = VRCAvatarDescriptor.AnimLayerType.TPose;
            av.specialAnimationLayers[2].type = VRCAvatarDescriptor.AnimLayerType.IKPose;
        }

        private void ModifyStateDefaults(AnimatorController anim)
        {
            /* TODO: Doesn't work yet - DISABLED!
            int newGestureValue = droneGestureIndex + 1;
            ChildAnimatorState state_MoveH = FindState(anim, out AnimatorStateMachine sm_Drone, "vCNP_Drone", "MoveH [i2]"); // _ is discard
            ChildAnimatorState state_MoveV = FindState(anim, out _, "vCNP_Drone", "MoveV");
            for(int j = 0; j < state_MoveH.state.transitions.Length; j++)
            {
                if(state_MoveH.state.transitions[j].destinationState != state_MoveV.state) continue;
                AnimatorStateTransition t = state_MoveH.state.transitions[j];

                for(int i = 0; i < t.conditions.Length; i++)
                {
                    AnimatorCondition c = t.conditions[i];
                    if(c.mode == AnimatorConditionMode.Equals)
                    {
                        c.threshold = newGestureValue;
                        t.conditions[i] = c;
                    }
                }
                state_MoveH.state.transitions[j] = t;
            }
            for(int j = 0; j < state_MoveV.state.transitions.Length; j++)
            {
                if(state_MoveV.state.transitions[j].destinationState != state_MoveH.state) continue;
                AnimatorStateTransition t = state_MoveV.state.transitions[j];

                for(int i = 0; i < state_MoveV.state.transitions[j].conditions.Length; i++)
                {
                    AnimatorCondition c = t.conditions[i];
                    if(c.mode == AnimatorConditionMode.NotEqual)
                    {
                        c.threshold = newGestureValue;
                        t.conditions[i] = c;
                    }
                }
                state_MoveV.state.transitions[j] = t;
            }
            //UpdateState(sm_Drone, state_MoveH);
            //UpdateState(sm_Drone, state_MoveV);
            //*/

            AnimatorState state_HeadScaleLocal = FindState(anim, out _, "vCNG_HeadScale", "LocalScale").state;
            AnimatorState state_HeadScaleRemote = FindState(anim, out _, "vCNG_HeadScale", "RemoteScale").state;
            state_HeadScaleLocal.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{_userCopyFolderPath}/HeadScaleLocal.anim");
            state_HeadScaleRemote.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{_userCopyFolderPath}/HeadScaleRemote.anim");
            ModifyAnimationClipDefaults(state_HeadScaleLocal.motion as AnimationClip, _armatureHead.Find("PickupC"));
            ModifyAnimationClipDefaults(state_HeadScaleRemote.motion as AnimationClip, _armatureHead.Find("PickupC"));

            AnimatorState state_DoFDisabled = FindState(anim, out AnimatorStateMachine sm_DoF, "vCNT_DoF", "DoFDisabled").state;
            AnimatorState state_AEDisabled = FindState(anim, out AnimatorStateMachine sm_AE, "vCNT_ExposureMode", "AEDisabled").state;
            AnimatorState state_GridEnabled = FindState(anim, out AnimatorStateMachine sm_Grid, "vCNT_Thirds", "ThirdsEnabled").state;
            AnimatorState state_ELEnabled = FindState(anim, out AnimatorStateMachine sm_Level, "vCNT_ElectroLevel", "ELEnabled").state;

            AnimatorState state_DSDisabled = FindState(anim, out AnimatorStateMachine sm_DirectStream, "vCNT_DirectStream", "DSDisabled").state;
            AnimatorState state_DSEnabled = FindState(anim, out _, "vCNT_DirectStream", "DSEnabled").state;
            AnimatorState state_DSHotFixed = FindState(anim, out _, "vCNT_DirectStream", "DSHotfixed").state;
            //AnimatorState state_DSDisabled_Old = FindState(anim, out _, "vCNT_DirectStream", "Old_DSDisabled").state;
            //AnimatorState state_DSEnabled_Old = FindState(anim, out _, "vCNT_DirectStream", "Old_DSEnabled").state;

            if(!animDoF) sm_DoF.defaultState = state_DoFDisabled;
            if(!animAE) sm_AE.defaultState = state_AEDisabled;
            if(animGrid) sm_Grid.defaultState = state_GridEnabled;
            if(animLevel) sm_Level.defaultState = state_ELEnabled;

            AnimatorStateMachine layer_DirectStream = FindLayer(anim, "vCNT_DirectStream");
            ChildAnimatorState[] state_sm = {
                FindState(layer_DirectStream, "DSDisabled"),
                FindState(layer_DirectStream, "DSEnabled"),
                FindState(layer_DirectStream, "DSHotfixed")
            };
            layer_DirectStream.defaultState = state_sm[streamModeIndex].state;

            AnimatorStateMachine layer_AFMode = FindLayer(anim, "vCNT_AFMode");
            ChildAnimatorState[] state_af = {
                FindState(layer_AFMode, "AFNormalActive"),
                FindState(layer_AFMode, "AFAvatarActive"),
                FindState(layer_AFMode, "AFSelfActive"),
            };
            layer_AFMode.defaultState = state_af[afModeIndex].state;

            AnimatorStateMachine layer_Tonemap = FindLayer(anim, "vCNT_TonemapFilter");
            ChildAnimatorState[] state_tm = {
                FindState(layer_Tonemap, "Off"),
                FindState(layer_Tonemap, "ACES"),
                FindState(layer_Tonemap, "EVILS"),
                FindState(layer_Tonemap, "HLG"),
                FindState(layer_Tonemap, "Reinhard")
            };
            layer_Tonemap.defaultState = state_tm[tonemapIndex].state;

            AnimatorStateMachine layer_SensorSize = FindLayer(anim, "vCNT_SensorSize");
            ChildAnimatorState[] state_ss = {
                FindState(layer_SensorSize, "Sensor0"),
                FindState(layer_SensorSize, "Sensor1"),
                FindState(layer_SensorSize, "Sensor2"),
                FindState(layer_SensorSize, "Sensor3"),
                FindState(layer_SensorSize, "Sensor4"),
                FindState(layer_SensorSize, "Sensor5")
            };
            layer_SensorSize.defaultState = state_ss[sensorSizeIndex].state;

            AnimatorState state_InitRadials = FindState(anim, out AnimatorStateMachine sm_Base, "vCNT_Base", "InitRadials").state;

            VRCAvatarParameterDriver paramDriver = state_InitRadials.behaviours[0] as VRCAvatarParameterDriver;
            paramDriver.parameters[0].value = _focalValues[focalIndex];
            paramDriver.parameters[2].value = _apertureValues[apertureIndex];
            // Make sure to not use parameter driver on synced expression parameters to not cause saved value to be overwritten
            if(av3Aperture) paramDriver.parameters.RemoveAt(2);
            if(av3Exposure) paramDriver.parameters.RemoveAt(1);
            if(av3Zoom) paramDriver.parameters.RemoveAt(0);

            if(!av3Zoom)
            {
                AnimatorState state_DefaultZoom = FindState(anim, out AnimatorStateMachine sm_PresetZoom, "vCNT_PresetZoom", "ZoomLinear").state;
                sm_PresetZoom.defaultState = state_DefaultZoom;
            }
            if(!av3Exposure)
            {
                AnimatorState state_DefaultExposure = FindState(anim, out AnimatorStateMachine sm_PresetExposure, "vCNT_PresetExposure", "ExpLinear").state;
                sm_PresetExposure.defaultState = state_DefaultExposure;
            }
            if(!av3Aperture)
            {
                string state = "";
                switch(apertureIndex)
                {
                    case 0: state = "SetF1,4"; break;
                    case 1: state = "SetF2,0"; break;
                    case 2: state = "SetF2,8"; break;
                    case 3: state = "SetF4,0"; break;
                    case 4: state = "SetF5,6"; break;
                    case 5: state = "SetF8,0"; break;
                    case 6: state = "SetF11"; break;
                    case 7: state = "SetF16"; break;
                    case 8: state = "SetF22"; break;
                }
                AnimatorState state_DefaultAperture = FindState(anim, out AnimatorStateMachine sm_PresetAperture, "vCNT_PresetAperture", state).state;
                sm_PresetAperture.defaultState = state_DefaultAperture;
            }
            if(!av3Focus)
            {
                AnimatorState state_DefaultFocus = FindState(anim, out AnimatorStateMachine sm_PresetFocus, "vCNT_PresetFocus", "FocusLinear").state;
                sm_PresetFocus.defaultState = state_DefaultFocus;
            }
            if(!av3Puppet)
            {
                AnimatorState stateDefault = FindState(anim, out AnimatorStateMachine sm, "vCNT_PresetFocusMove", "FocusMoveLinear").state;
                sm.defaultState = stateDefault;
                stateDefault = FindState(anim, out sm, "vCNP_FocusMove", "FocusMovePuppet").state;
                sm.defaultState = stateDefault;
            }

            // HACK: Write defaults for all states for avatar compatibility reasons
            foreach(AnimatorControllerLayer ly in anim.layers)
            {
                SetWriteDefaultsAll(ly.stateMachine, useWriteDefaults);
            }
        }

        private void ModifyAnimatorLayerControlIndices(AnimatorController dest, AnimatorController src)
        {
            int srcLayerCount = src.layers.Length;
            int destLayerCount = dest.layers.Length;

            // Find states that will have Animator Layer Control
            AnimatorStateMachine sm_Base = FindLayer(dest, "vCNT_Base");

            VRCAnimatorLayerControl[] disableLocal = FindState(sm_Base, "____DISABLE____").state.behaviours as VRCAnimatorLayerControl[];

            // TODO Finish the layer logic!
            for(int i = 1; i < destLayerCount; i++)
            {
                disableLocal[i].layer = srcLayerCount + i;
                disableLocal[i].goalWeight = 0;
            }
        }

        private void ModifyAnimationClipDefaults(AnimationClip clip, Transform srcObject)
        {
            // Experimental modification of animation clip itself
            if(clip == null || srcObject == null) return;

            Transform current = srcObject;
            string path = "";
            while(avatarDescriptor.transform != current) // Loop until parent (with animator) is found
            {
                // x0 -> x1/x0 -> x2/x1/x0 -> x3/x2/x1/x0...
                path = srcObject == current ? current.name : $"{current.name}/{path}";

                current = current.parent;
                if(current == null) return; // This should not happen (animator not found)
            }

            foreach(EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                EditorCurveBinding bd = binding;
                AnimationUtility.SetEditorCurve(clip, bd, null);
                bd.path = path;
                AnimationUtility.SetEditorCurve(clip, bd, curve);
            }
        }

        private void SetWriteDefaultsAll(AnimatorStateMachine asm, bool writeDefaults)
        {
            foreach(ChildAnimatorStateMachine casm in asm.stateMachines)
            {
                SetWriteDefaultsAll(casm.stateMachine, writeDefaults);
            }
            foreach(ChildAnimatorState cas in asm.states)
            {
                cas.state.writeDefaultValues = writeDefaults;
            }
        }

        // Nullable boolean - makes sense
        public bool ContainsWriteDefaults()
        {
            if(avatarDescriptor == null) return false;
            AnimatorController anim = FindFXLayer(avatarDescriptor, out _);
            if(anim == null) return false;

            foreach(AnimatorControllerLayer ly in anim.layers)
            {
                // Ignore VRCLens own layers
                if(ly.name.StartsWith("vCNT_") || ly.name.StartsWith("vCNG_") || ly.name.StartsWith("vCNR_") || ly.name.StartsWith("vCNP_"))
                {
                    continue;
                }
                if(ContainsWriteDefaults(ly.stateMachine))
                {
                    return true;
                }
            }
            return false;
        }
        private bool ContainsWriteDefaults(AnimatorStateMachine asm)
        {
            bool wd = false;
            foreach(ChildAnimatorStateMachine casm in asm.stateMachines)
            {
                wd |= ContainsWriteDefaults(casm.stateMachine);
            }
            foreach(ChildAnimatorState cas in asm.states)
            {
                wd |= cas.state.writeDefaultValues;
            }
            return wd;
        }

        private AnimatorController FindFXLayer(VRCAvatarDescriptor av, out int index)
        {
            index = -1;
            if(av == null || !av.customizeAnimationLayers) return null;
            return FindFXLayer(av.baseAnimationLayers, out index);
        }
        private AnimatorController FindFXLayer(VRCAvatarDescriptor.CustomAnimLayer[] animLayer, out int index)
        {
            index = -1;
            if(animLayer == null) return null;
            for(int i = 0; i < animLayer.Length; i++)
            {
                if(animLayer[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    index = i;

                    if(animLayer[i].animatorController == null) return null;
                    else return animLayer[i].animatorController as AnimatorController;
                }
            }
            return null;
        }

        private AnimatorStateMachine FindLayer(AnimatorController a, string layerName)
        {
            return Array.Find(a.layers, x => x.name.StartsWith(layerName)).stateMachine;
        }
        private ChildAnimatorState FindState(AnimatorController a, out AnimatorStateMachine machine, string layerName, string stateName)
        {
            machine = FindLayer(a, layerName);
            return FindState(machine, stateName);
        }
        private ChildAnimatorState FindState(AnimatorStateMachine machine, string stateName)
        {
            return Array.Find(machine.states, x => x.state.name == stateName);
        }
        private bool UpdateState(AnimatorStateMachine machine, ChildAnimatorState replacement)
        {
            /*
            int replaceIndex = Array.FindIndex(machine.states, x => x.state.name == replacement.state.name);
            if(replaceIndex >= 0)
            {
                machine.states[replaceIndex] = replacement;
                return true;
            } else
            {
                return false;
            }
            */
            return false;
        }

        private VRCExpressionParameters.Parameter[] GenerateAllNewParams()
        {
            // These are just for comparison purposes, not to be added to the user expression parameters
            VRCExpressionParameters.Parameter[] newList = new VRCExpressionParameters.Parameter[_pNameList.Length];
            for(int i = 0; i < newList.Length; i++)
            {
                VRCExpressionParameters.Parameter p = new VRCExpressionParameters.Parameter {
                    name = _pNameList[i],
                    valueType = (VRCExpressionParameters.ValueType)_pTypeList[i],
                    defaultValue = 0f,
                    saved = false
                };

                newList[i] = p;
            }
            return newList;
        }

        private VRCExpressionParameters.Parameter[] GenerateRequiredParams()
        {
            VRCExpressionParameters.Parameter[] newList = new VRCExpressionParameters.Parameter[RequiredParameterSlots()];
            int k = 0;
            for(int i = 0; i < _pNameList.Length; i++)
            {
                bool skip = false;
                skip |= i == 1 && !false;// !av3Interrupt; [Backward compatibility reasons]
                skip |= i == 2 && !av3Puppet;
                skip |= i == 3 && !av3Puppet;
                skip |= i == 4 && !av3Zoom;
                skip |= i == 5 && !av3Exposure;
                skip |= i == 6 && !av3Aperture;
                skip |= i == 7 && !av3Focus;

                bool saveValue = i >= 4 && i <= 6;

                if(!skip)
                {
                    newList[k] = new VRCExpressionParameters.Parameter {
                        name = _pNameList[i],
                        valueType = (VRCExpressionParameters.ValueType)_pTypeList[i],
                        defaultValue = 0f,
                        saved = saveValue
                    };

                    if(i == 4) newList[k].defaultValue = _focalValues[focalIndex];
                    if(i == 5) newList[k].defaultValue = 0.5f;
                    if(i == 6) newList[k].defaultValue = _apertureValues[apertureIndex];

                    k++;
                }
            }

            return newList;
        }

        private void ActivateParentConstraint(ParentConstraint constraint, Transform sourceTransform)
        {
            int srcIndex = constraint.sourceCount;
            ConstraintSource conSrc = new ConstraintSource {
                sourceTransform = sourceTransform,
                weight = srcIndex == 0f ? 1f : 0f
            };

            constraint.AddSource(conSrc);

            Transform conTran = constraint.transform;
            Transform srcTran = constraint.GetSource(srcIndex).sourceTransform;

            Vector3 posDelta = Vector3.Scale(srcTran.InverseTransformPoint(conTran.position), srcTran.lossyScale); // Fix non-unity transforms
            Quaternion rotDelta = Quaternion.Inverse(srcTran.rotation) * conTran.rotation;

            constraint.SetTranslationOffset(srcIndex, posDelta);
            constraint.SetRotationOffset(srcIndex, rotDelta.eulerAngles);

            constraint.weight = 1f;
            constraint.constraintActive = true;
            constraint.locked = true;
        }
        private void ActivateScaleConstraint(ScaleConstraint constraint, Transform sourceTransform)
        {
            int srcIndex = constraint.sourceCount;
            ConstraintSource conSrc = new ConstraintSource {
                sourceTransform = sourceTransform,
                weight = srcIndex == 0f ? 1f : 0f
            };

            constraint.AddSource(conSrc);

            Vector3 conScale = constraint.transform.localScale;
            Vector3 srcScale = constraint.GetSource(0).sourceTransform.localScale;

            constraint.scaleAtRest = conScale;
            constraint.scaleOffset = InverseScale(conScale, srcScale);

            constraint.weight = 1f;
            constraint.constraintActive = true;
            constraint.locked = true;
        }
        private void SetPositionConstraintSource(PositionConstraint constraint, Transform sourceTransform, int srcIndex)
        {
            ConstraintSource conSrc = constraint.GetSource(srcIndex);
            conSrc.sourceTransform = sourceTransform;
            constraint.SetSource(srcIndex, conSrc);
        }
        /*
        private void ZeroPositionConstraint(PositionConstraint constraint, Transform sourceTransform)
        {
            ConstraintSource conSrc = new ConstraintSource {
                sourceTransform = sourceTransform,
                weight = 1f
            };

            if(constraint.sourceCount == 0) constraint.AddSource(conSrc);
            else constraint.SetSource(0, conSrc);

            Transform conTran = constraint.transform;
            Transform srcTran = constraint.GetSource(0).sourceTransform;

            constraint.translationAtRest = sourceTransform.position - _avatarRoot.position;
            constraint.translationOffset = Vector3.zero;
            conTran.localPosition = constraint.translationAtRest; // Immediately snap there

            constraint.weight = 1f;
            constraint.constraintActive = true;
            constraint.locked = true;
        }
        
        private void ActivateRotationConstraint(RotationConstraint constraint, Transform sourceTransform)
        {
            int srcIndex = constraint.sourceCount;
            ConstraintSource conSrc = new ConstraintSource {
                sourceTransform = sourceTransform,
                weight = srcIndex == 0f ? 1f : 0f
            };

            constraint.AddSource(conSrc);

            Transform conTran = constraint.transform;
            Transform srcTran = constraint.GetSource(srcIndex).sourceTransform;

            Quaternion rotDelta = Quaternion.Inverse(srcTran.rotation) * conTran.rotation;
            constraint.rotationAtRest = conTran.localRotation.eulerAngles;
            constraint.rotationOffset = rotDelta.eulerAngles;

            constraint.weight = 1f;
            constraint.constraintActive = true;
            constraint.locked = true;
        }
        
        private void ActivateAimConstraintOnly(AimConstraint constraint)
        {
            Transform conTran = constraint.transform;
            Transform srcTran = constraint.GetSource(0).sourceTransform;

            Quaternion rotDelta = Quaternion.Inverse(srcTran.rotation) * conTran.rotation;
            constraint.rotationAtRest = conTran.localRotation.eulerAngles;
            constraint.rotationOffset = rotDelta.eulerAngles;

            constraint.constraintActive = true;
            constraint.locked = true;
        }
        */

        private Vector3 InverseScale(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }
#endif
    }
}