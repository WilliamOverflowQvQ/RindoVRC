// VRCLens Copyright (c) 2020-2022 Hirabiki. All rights reserved.
// Usage of this product is subject to Terms of Use in readme_DoNotDelete.txt

using UnityEngine;
using UnityEditor;

namespace Hirabiki.AV3.Works.VRCLens
{
    [CustomEditor(typeof(VRCLensSetup))]
    public class ConfigInspector : Editor
    {
        VRCLensSetup config;
        SerializedProperty _avatarProp, _setupMode, _targetSubMenu;
        SerializedProperty _customSensorRes;
        SerializedProperty _cameraModelPosition, _cameraModelRotation, _cameraRotation, _focusPenRotation, _cameraSize, _previewSize, _previewPosition;
        SerializedProperty _cameraModel;
        SerializedProperty _useWriteDefaults, _useDisableButton;
        SerializedProperty _av3Puppet, _av3Zoom, _av3Exposure, _av3Aperture, _av3Focus;
        SerializedProperty _animAE, _animDOF, _animGrid, _animLevel, _animDirectStream, _animLegacyStream;

        Transform camBase, headBase, camModel, previewBase;
        Transform focusPen;
        Transform selfFocus;

        Camera touchDetectCam;

        bool showConfigJson, rotationGizmos;

        readonly string[] perfOptions = {
            "Very Large - GTX 1080Ti／RTX 2070 Super／RX 5700 XT",
            "Large - GTX 1080／RTX 2060／RX 5700",
            "Medium - GTX 1070／GTX 1660 Super／RX 5600 XT",
            "Small - GTX 1060／GTX 1650／RX 570",
            "Very Small - GTX 1050Ti／RX 560"
        };

        readonly string[] droneGestureOptions = { "[F2] Fist (VIVE)", "Other/[F3] HandOpen", "Other/[F4] FingerPoint", "Other/[F5] Victory", "Other/[F6] RockNRoll", "[F7] HandGun (Oculus／Index)", "[F8] ThumbsUp (Desktop)"};
        readonly string[] focalOptions = { "12mm", "17mm", "24mm", "35mm", "50mm", "70mm", "85mm", "135mm", "200mm", "400mm", "1200mm" };
        readonly string[] apertureOptions = { "F1.4", "F2.0", "F2.8", "F4.0", "F5.6", "F8.0", "F11", "F16", "F22" };
        readonly string[] sensorTypeOptions = { "Full Size [1.0x]", "APS-H [1.3x]", "APS-C [1.5x]", "APS-C [1.6x]", "MFT [2.0x]", "1\" [2.7x]" };
        readonly string[] afModeOptions = { "Normal", "Avatar AF", "Selfie AF" };
        readonly string[] sensorResOptions = { "2.1MP HD (1920×1080)", "3.7MP QHD (2560×1440)", "8.3MP 4K (3840×2160)", "Custom (16:9)" };
        readonly string[] msaaOptions = { "Off", "Auto", "MSAA 2x", "MSAA 4x", "MSAA 8x" };
        readonly string[] tonemapOptions = { "Off (RAW)", "Filmic 1 (ACES)", "Filmic 2 (EVILS)", "HDR (Hybrid Log-Gamma)", "Neutral (Highlight Saver)" };
        readonly string[] streamModeOptions = { "Disable", "DirectCast", "Enable" };

        void OnEnable()
        {

            config = (VRCLensSetup)target;
            if(PrefabUtility.IsAnyPrefabInstanceRoot(config.gameObject))
            {
                PrefabUtility.UnpackPrefabInstance(config.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }

            VRCLensSetup p = config; // for shorthand

            _avatarProp = FindProp(nameof(p.avatarDescriptor));
            _setupMode = FindProp(nameof(p.setupMode));
            _targetSubMenu = FindProp(nameof(p.targetSubMenu));

            _customSensorRes = FindProp(nameof(p.customSensorRes));

            _cameraModelPosition = FindProp(nameof(p.cameraModelPosition));
            _cameraModelRotation = FindProp(nameof(p.cameraModelRotation));
            _cameraRotation = FindProp(nameof(p.cameraRotation));
            _focusPenRotation = FindProp(nameof(p.focusPenRotation));
            _cameraSize = FindProp(nameof(p.cameraSize));
            _previewPosition = FindProp(nameof(p.previewPosition));
            _previewSize = FindProp(nameof(p.previewSize));
            _cameraModel = FindProp(nameof(p.cameraModel));

            _useWriteDefaults = FindProp(nameof(p.useWriteDefaults));
            _useDisableButton = FindProp(nameof(p.useDisableButton));

            _av3Puppet = FindProp(nameof(p.av3Puppet));
            _av3Zoom = FindProp(nameof(p.av3Zoom));
            _av3Exposure = FindProp(nameof(p.av3Exposure));
            _av3Aperture = FindProp(nameof(p.av3Aperture));
            _av3Focus = FindProp(nameof(p.av3Focus));

            _animAE = FindProp(nameof(p.animAE));
            _animDOF = FindProp(nameof(p.animDoF));
            _animGrid = FindProp(nameof(p.animGrid));
            _animLevel = FindProp(nameof(p.animLevel));

            RefreshReferences();
            if(IsInScene(config))
            {
                config.RefreshCopyFolderPath();
                config.gameObject.SetActive(true);
                config.FixSetupOnEnable();
            }
            
            if(camBase != null)
            {
                camBase.localScale = Vector3.one;
                camModel.parent.localPosition = Vector3.zero;
                camModel.parent.localRotation = Quaternion.identity;
                camModel.parent.localScale = Vector3.one;
                
            }
            if(headBase != null) headBase.localScale = Vector3.one;
            if(focusPen != null) focusPen.localScale = Vector3.one;

            //* ---- RIGHT NOW I AM UNABLE TO PRODUCE DLL FILES ----
            // This is hacky, but is the only good way to make sure it will appear after updating in-place
            //UnityEditorInternal.ComponentUtility.
            if(config.GetComponent<Managed.VRCLensVerifier>() == null)
            {
                Component c = config.gameObject.AddComponent<Managed.VRCLensVerifier>();
                UnityEditorInternal.ComponentUtility.MoveComponentUp(c);
            }
            //*/
        }

        void OnDisable()
        {
            if(config == null) return;
            if(!IsInScene(config)) return;
            //if(PrefabUtility.IsAnyPrefabInstanceRoot(config.gameObject)) return;
            //Debug.Log($"Selection changed to {(Selection.activeTransform == null ? "(nothing)" : Selection.activeTransform.name)}");

            Transform p = Selection.activeTransform;
            while(p != null && p.name != "CamBase" && p.name != "HeadMountPoint" && p.name != "FocusObject" && p.name != "SelfieFocusPoint")
            {
                p = p.parent;
            }
            if((p == null || p.root != config.transform) && IsInScene(config))
            {
                config.gameObject.SetActive(false);
            } else if(p.name != "CamBase")
            {
                Selection.activeTransform = p;
            }
        }

        void OnSceneGUI()
        {
            if(camBase != null && headBase != null && focusPen != null && selfFocus != null)
            {
                Quaternion newCamRot, newHeadRot, newFocRot;
                newCamRot = newHeadRot = newFocRot = Quaternion.identity;
                Vector3 newCamPos, newHeadPos, newFocPos;
                newCamPos = newHeadPos = newFocPos = Vector3.zero;

                EditorGUI.BeginChangeCheck();

                if(rotationGizmos)
                {
                    newCamRot = Handles.RotationHandle(camBase.rotation, camBase.position);
                    newHeadRot = Handles.RotationHandle(headBase.rotation, headBase.position);
                    newFocRot = Handles.RotationHandle(focusPen.rotation, focusPen.position);
                } else
                {
                    newCamPos = Handles.PositionHandle(camBase.position, camBase.rotation);
                    newHeadPos = Handles.PositionHandle(headBase.position, headBase.rotation);
                    newFocPos = Handles.PositionHandle(focusPen.position, focusPen.rotation);
                }
                Vector3 newSelfFocusPos = Handles.PositionHandle(selfFocus.position, selfFocus.rotation);

                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(camBase, "VRCLens Camera Move");
                    Undo.RecordObject(headBase, "VRCLens Head Camera Move");
                    Undo.RecordObject(focusPen, "VRCLens Pointer Move");
                    Undo.RecordObject(selfFocus, "VRCLens Focus Move");
                    if(rotationGizmos)
                    {
                        camBase.rotation = newCamRot;
                        headBase.rotation = newHeadRot;
                        focusPen.rotation = newFocRot;

                        config.cameraRotation = camBase.localRotation.eulerAngles;
                        config.focusPenRotation = focusPen.localRotation.eulerAngles;
                    } else
                    {
                        camBase.position = newCamPos;
                        headBase.position = newHeadPos;
                        focusPen.position = newFocPos;
                    }
                    selfFocus.position = newSelfFocusPos;
                    config.queueValidate = true;
                }
                Transform lensC = camBase.Find("CamObject/LensC");
                if(lensC != null)
                {
                    Debug.DrawRay(lensC.position, lensC.forward);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            bool isPostAdd = config.transform.childCount == 0; // BACKWARD COMPATIBILITY
            if(!isPostAdd) // BACKWARD COMPATIBILITY
            {
                EnforceInspectorAdjustments(false);
            }

            //----------  ----------//
            EditorGUILayout.LabelField("Hint: Hover over a name of a setting for details.", EditorStyles.boldLabel);
            GuiLine();
            if(config.transform.Find("HeadMountPoint") == null)
            {
                EditorGUILayout.HelpBox("これをPrefabsよりのVRCLensに置き換えてください。\nPlease replace this setup script with new one in Prefabs.", MessageType.Warning);
            }
            
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_avatarProp, new GUIContent("Target Avatar", "Select your avatar here.\nアバター本体をここに移動しよう"));
            EditorGUIUtility.labelWidth += 32;
            EditorGUILayout.PropertyField(_setupMode, new GUIContent("Camera Handedness",
                @"Choose which hand the camera goes to.
右利きと左利きのカメラ操作を選べます

In desktop mode, the camera will be attached to your head where the green camera is.
デスクトップモードでプレイしたら、緑のカメラがあるところに頭に取り付いています。"));
            //config.droneGestureIndex = EditorGUILayout.Popup(new GUIContent("Drone Vertical Gesture"), config.droneGestureIndex, droneGestureOptions);
            EditorGUIUtility.labelWidth -= 32;
            EditorGUILayout.LabelField(new GUIContent("Max Blur Size (Performance Adjustment)",
                "The maximum size of the blur. This option will affect the in-game performance. The smaller the size, the weaker the blur, but the less performace is required.\n\n最大のボカシサイズの調整。お使いのGPUに\n合わせて調整してお勧めします。"));
            config.maxBlurIndex = EditorGUILayout.Popup(config.maxBlurIndex, perfOptions);
            switch(config.maxBlurIndex)
            {
                case 0: config.maxBlurSize = 348; break;
                case 1: config.maxBlurSize = 228; break;
                case 2: config.maxBlurSize = 178; break;
                case 3: config.maxBlurSize = 134; break;
                case 4: config.maxBlurSize = 64; break;
            }
            

            
            EditorGUI.BeginDisabledGroup(isPostAdd);
            EditorGUILayout.PropertyField(_cameraModel, new GUIContent("Camera Model", "Camera model can be changed\nカメラモデルを切り替えます"));
            EditorGUILayout.PropertyField(_cameraModelPosition, new GUIContent("Model Position", "Local position of the camera model\nカメラモデルのローカル位置"));
            EditorGUILayout.PropertyField(_cameraModelRotation, new GUIContent("Model Rotation", "Local rotation of the camera model\nカメラモデルのローカル回転"));

            if(GUILayout.Button("Change Camera Model"))
            {
                ChangeCameraModel(false, Quaternion.identity);
            }

            EditorGUILayout.PropertyField(_cameraRotation, new GUIContent("Camera Rotation", "The overall rotation of the camera\nカメラの回転"));
            EditorGUILayout.PropertyField(_cameraSize, new GUIContent("Camera Scale", "Size of the camera model\nカメラモデルのサイズ"));
            EditorGUILayout.PropertyField(_previewPosition, new GUIContent("Preview Position", "Position of preview screen relative to the camera\nカメラ画面のローカル位置"));
            EditorGUILayout.PropertyField(_previewSize, new GUIContent("Preview Scale", "Size of preview screen\nカメラ画面のサイズ"));
            EditorGUILayout.PropertyField(_focusPenRotation, new GUIContent("Focus Pen Rotation", "Rotation of the focus pen\nフォーカスのタッチペンの回転"));
            if(GUILayout.Button(new GUIContent("Auto Arrange（自動配置）", "Attempt to automatically place VRCLens objects appropriately on your avatar")))
            {
                config.AutoArrangeSetup();
            }
            EditorGUI.EndDisabledGroup();

            GuiLine();
            EditorGUILayout.LabelField("Object Gizmos Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                Color highlightColor = new Color(0.6f, 0.8f, 1.0f);
                GUI.backgroundColor = rotationGizmos ? Color.white : highlightColor;
                if(GUILayout.Button("Move（移動）")) rotationGizmos = false;
                GUI.backgroundColor = rotationGizmos ? highlightColor : Color.white;
                if(GUILayout.Button("Rotate（回転）")) rotationGizmos = true;
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("VRCLens Settings Profile", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button(new GUIContent("Save Profile as...", "Save the current VRCLens settings to a file.")))
            {
                config.SaveSettingsToFile();
            }
            if(GUILayout.Button(new GUIContent("Load Profile...", "Load a saved VRCLens settings from a file.")))
            {
                VRCLensProfileManager loadedProfile = config.ReadSettingsFromFile();
                if(loadedProfile == null) return;

                bool doApply = false;
                int ret = EditorUtility.DisplayDialogComplex("VRCLens - Load Profile", @"What to load from this profile?
[Settings Only] Import only the settings of VRCLens.
[Load All] Also load positions of VRCLens objects.

どのような設定を読み込むのか？
「Settings Only」　設定のみを読み込む
「Load All」　設定とオブジェクト位置を読み込む", "Settings Only", "Cancel", "Load All");
                switch(ret)
                {
                    case 0: // Choice 1
                        doApply = true;
                        config.ApplySettingsFromProfile(loadedProfile, true);
                        break;
                    case 1: // CANCEL
                        break;
                    case 2: // Choice 3
                        doApply = true;
                        config.ApplySettingsFromProfile(loadedProfile, false);
                        break;
                }
                if(doApply)
                {
                    EnforceInspectorAdjustments(true);
                    ChangeCameraModel(false, Quaternion.identity);
                }
            }
            EditorGUILayout.EndHorizontal();

            GuiLine();
            EditorGUILayout.LabelField("Expression Parameter Usage Settings", EditorStyles.boldLabel);
            EditorGUIUtility.labelWidth += 32;
            EditorGUILayout.PropertyField(_av3Puppet, new GUIContent("Enable drone controls", "Enable all puppet controls of VRCLens\nAlso used in VR mode for moving the HUD\n- Uses 2 Expression Parameters"));
            EditorGUILayout.PropertyField(_av3Focus, new GUIContent("Use manual focus Dial", "Use radial manual focus control\nButton-based manual focus available if disabled\n- Uses 1 Expression Parameter"));
            EditorGUILayout.PropertyField(_av3Zoom, new GUIContent("Use zoom Dial", "Use radial focal length (zoom) control\nButton-based smooth zoom available if disabled\n- Uses 1 Expression Parameter"));
            EditorGUILayout.PropertyField(_av3Exposure, new GUIContent("Use exposure Dial", "Use radial to change brightness of photo\nStepped adjustment available if disabled\n- Uses 1 Expression Parameter"));
            EditorGUILayout.PropertyField(_av3Aperture, new GUIContent("Use aperture Dial", "Use radial for depth of field control\nStepped adjustment available if disabled\n- Uses 1 Expression Parameter"));
            int up = config.OccupiedParameters();
            int rp = config.RequiredParameters();
            if(up != int.MaxValue)
            {
                EditorGUILayout.LabelField(new GUIContent($"Expression Parameter Usage: {rp} bits ({config.AV3_MAXPARAMALLOC - up - rp} bits remaining)",
                    $@"Avatars 3.0 allows for the maximum of {config.AV3_MAXPARAMALLOC} bits of expression parameters in an avatar.
It is recommended to not use more expression parameters on VRCLens than needed.
Note: Bool-type parameter uses 1 bit rather than 8 bits"));
                //EditorGUILayout.LabelField($"Remaining parameters: {fp - rp} ({rp} used)");
            }

            GuiLine();
            EditorGUILayout.LabelField("Default Camera Settings（カメラの初期設定）", EditorStyles.boldLabel);
            config.focalIndex = EditorGUILayout.Popup(new GUIContent("35mm Focal Length", "The default 35mm focal length of the camera"), config.focalIndex, focalOptions);
            config.apertureIndex = EditorGUILayout.Popup(new GUIContent("Aperture", "The strength of the depth-of-field"), config.apertureIndex, apertureOptions);
            config.sensorSizeIndex = EditorGUILayout.Popup(new GUIContent("Virtual Sensor Type", "The virtual size of the image sensor"), config.sensorSizeIndex, sensorTypeOptions);
            EditorGUILayout.PropertyField(_animDOF, new GUIContent("Depth of Field", "Turn depth-of-field simulation off/on by default"));
            EditorGUILayout.PropertyField(_animAE, new GUIContent("Auto Exposure", "Automatic brightness adjustment of picture"));
            config.tonemapIndex = EditorGUILayout.Popup(new GUIContent("Picture Style", "Tone mapping (HDR) presets"), config.tonemapIndex, tonemapOptions);
            config.afModeIndex = EditorGUILayout.Popup(new GUIContent("Autofocus Mode", "The default automatic focusing mode\nNormal: Focus on everything\nAvatar AF: Focus on avatars only\nSelfie AF: Focus on your face"), config.afModeIndex, afModeOptions);
            EditorGUILayout.PropertyField(_animLevel, new GUIContent("Virtual Horizon", "Turn the Electronic Level display on/off by default"));
            EditorGUILayout.PropertyField(_animGrid, new GUIContent("Grid", "Turn the rule-of-thirds grid on/off by default"));
            //EditorGUILayout.PropertyField(_animDirectStream, new GUIContent("DirectCast", "Turn DirectCast on/off by default\n(mode for high-performance streaming)\n\nNote: DirectCast will take videos without Post-processing used in a world."));
            //EditorGUILayout.PropertyField(_animDirectStream, new GUIContent("Stream Camera Mode", "Turn Stream camera (movie) mode on/off by default\n\nMakes VRCLens run at full frame rate for Stream Camera for a performance cost."));
            config.streamModeIndex = EditorGUILayout.Popup(new GUIContent("Stream Camera Mode", @"Default Stream Camera (Movie) Mode

[Disable] For taking photos, best performance
[DirectCast] Record without using VRChat camera for a good performance (No post-processing)
[Enable] Use Stream Camera to record with post-processing for a performance cost"), config.streamModeIndex, streamModeOptions);

            GuiLine();
            EditorGUILayout.LabelField("Advanced Settings（詳細設定）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useDisableButton, new GUIContent("Add Disable Button",
                @"Add a dedicated disable button in the main VRCLens menu.

Disable this setting to make each button in the main VRCLens menu larger and easier to access.
The camera can be disabled without this button by holding the Enable button."));
            if(!config.useDisableButton)
            {
                EditorGUILayout.HelpBox("「Enable」を長押してカメラを無効します。\nHold \"Enable\" button to disable camera.", MessageType.Info);
            }

            /* ---- EMERGENCY PATCH (Breaking changes on VRChat made this option redundant)
            EditorGUILayout.PropertyField(_animLegacyStream, new GUIContent("Stream Camera support", @"Enabling this option will reduce performace.

Enable legacy support for VRChat Stream Camera to allow for taking videos without using DirectCast.
Useful when post-processing is a requirement for video recording.
(Does not affect the performance of DirectCast)

Otherwise, use DirectCast to stream instead."));
            */
            EditorGUILayout.PropertyField(_useWriteDefaults, new GUIContent("Write Defaults", @"VRChat's Avatars 3.0 creation guide recommends that Write Defaults on animation states are OFF.

However, this might cause issues for avatars which has Write Defaults ON.
This setting is for backward compatibility with those avatars and should be set to OFF if possible."));

            if(config.Avatar != null)
            {
                if (config.useWriteDefaults != config.ContainsWriteDefaults()) {
                    if(config.useWriteDefaults)
                    {
                        EditorGUILayout.HelpBox(@"アバターの表情がWriteDefaultsを利用しません
上記のWriteDefaults設定のチェックを外すしてください

This avatar's animation doesn't use Write Defaults.
Please Disable Write Defaults for VRCLens.", MessageType.Warning);
                    } else
                    {
                        EditorGUILayout.HelpBox(@"アバターの表情がWriteDefaultsを利用しています
必ず上記のWriteDefaults設定を有効にしてください
なお、VRChatの将来のアップデートでアバターに
干渉が発生する可能性があります

This avatar's animation uses Write Defaults
which is deprecated by VRChat. Please enable
Write Defaults to mitigate avatar interference.
Future VRchat updates might cause this avatar
to no longer work correctly.", MessageType.Warning);
                    }
                } else
                {
                    if(config.useWriteDefaults)
                    {
                        EditorGUILayout.HelpBox(@"アバターの表情がWriteDefaultsを利用しています
VRChatの将来のアップデートでアバターに
干渉が発生する可能性があります

This avatar's animation uses Write Defaults
which is deprecated by VRChat. Future updates
might cause this avatar not work correctly.", MessageType.Info);
                    }
                }
            }

            EditorGUILayout.PropertyField(_targetSubMenu, new GUIContent("Destination Sub Menu", "The menu in which to put VRCLens in.\nLeave blank to put in the base menu."));
            int fc = config.FreeMenuControls();
            if(fc != int.MaxValue) EditorGUILayout.LabelField($"Remaining Menu Slots: {fc - 1}");


            config.sensorResIndex = EditorGUILayout.Popup(new GUIContent("Sensor Resolution", "The internal resolution of VRCLens image.\nHigher resolution require stronger GPU.\nDo not set this higher than necessery."), config.sensorResIndex, sensorResOptions);
            if(config.sensorResIndex == 3)
            {
                EditorGUILayout.PropertyField(_customSensorRes, new GUIContent(" "));
                if(config.customSensorRes.x > 3840)
                {
                    EditorGUILayout.HelpBox("超高い解像度の設定ではゲームが止まるの可能性があります\nVery high resolutions may crash the game.\nVRChat limits output resolution to 3840x2160.", MessageType.Warning);
                }
            }
            config.msaaIndex = EditorGUILayout.Popup(new GUIContent("Anti-Aliasing", @"Pixel smoothing at the edge of objects.
Higher anti-aliasing needs more GPU usage.

Anti-aliasing settings for Auto:
4x at 1080p
2x at 1440p
No AA at 4K"), config.msaaIndex, msaaOptions);

            showConfigJson = EditorGUILayout.Foldout(showConfigJson, new GUIContent("Config.json Custom Resolution Settings"));
            if(showConfigJson)
            {
                char ds = System.IO.Path.DirectorySeparatorChar;
                string vrcDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + $"{ds}AppData{ds}LocalLow{ds}VRChat{ds}VRChat{ds}";
                string configContents = $@"{{
    ""camera_res_height"": {config.customSensorRes.y},
    ""camera_res_width"": {config.customSensorRes.x},
    ""screenshot_res_height"": {config.customSensorRes.y},
    ""screenshot_res_width"": {config.customSensorRes.x}
}}";
                EditorGUILayout.TextArea(configContents);
                if(GUILayout.Button(new GUIContent("Save config.json...", "Save this custom config.json to VRChat data folder")))
                {
                    string fullPath = EditorUtility.SaveFilePanel("Save custom VRChat config.json", vrcDataPath, "config.json", "json");
                    if(fullPath != "")
                    {
                        System.IO.File.WriteAllText(fullPath, configContents);
                    }
                }
            }

            if(config.sensorResIndex != 0)
            {
                EditorGUILayout.HelpBox("Higher resolutions need more GPU usage.\nCustom photo resolution via config.json\nin VRChat folder is recommended", MessageType.Info);
            }

            EditorGUIUtility.labelWidth -= 32;

            GuiLine();

            bool except = true;
            bool canFix = false;
            bool canRestart = false;
            bool fatalError = false;
            if(config.transform.childCount == 0) // BACKWARD COMPATIBILITY
            {
                //EditorGUILayout.HelpBox("Setup complete.\nConfirm your animation settings.", MessageType.Info);
                canRestart = true;
            } else if(config.transform.position != Vector3.zero || config.transform.eulerAngles != Vector3.zero)
            {
                EditorGUILayout.HelpBox("Please move this prefab to origin.", MessageType.Error);
                canFix = true;
            } else if(config.avatarDescriptor == null)
            {
                EditorGUILayout.HelpBox("Select an avatar to add VRCLens to.", MessageType.Info);
            } else if(config.Avatar == null)
            {
                EditorGUILayout.HelpBox("The avatar's animator component is missing.", MessageType.Error);
            } else if(config.Avatar.transform.parent != null)
            {
                EditorGUILayout.HelpBox("The avatar cannot be a child of a GameObject.", MessageType.Error);
                canFix = true;
            } else if(config.Avatar.transform.position != Vector3.zero || config.Avatar.transform.eulerAngles != Vector3.zero)
            {
                EditorGUILayout.HelpBox("Please move the avatar to origin.", MessageType.Error);
                canFix = true;
            } else if(!config.IsAvatarUniformScale())
            {
                EditorGUILayout.HelpBox("The scale of the avatar is not uniform.", MessageType.Error);
                canFix = true;
            } else if(!config.IsAvatarHumanoid())
            {
                EditorGUILayout.HelpBox("Generic avatar is not supported.", MessageType.Error);
            } else if(!config.IsSetupCompatible())
            {
                EditorGUILayout.HelpBox(@"選択したアバターがVRCLens組み込み処理が
実行でない。hirabiki様に連絡してください。
この不具合を直せるかもしれない。

The selected avatar has a setup that is incompatible with VRCLens. Please contact hirabiki on Twitter to help make every avatar compatible.", MessageType.Error);

                if(GUILayout.Button("これを転送してください | Show Debug"))
                {
                    EditorUtility.DisplayDialog("VRCLens Setup Troubleshooting Information", config.DumpDiagnostics(), "OK");
                }
                if(GUILayout.Button("TwitterのDMで連絡 | DM on Twitter"))
                {
                    Application.OpenURL("https://twitter.com/Hibihira_Mii");
                }
                if(GUILayout.Button("BOOTHで連絡"))
                {
                    Application.OpenURL("https://hirabiki.booth.pm/");
                }
            } else if(!config.IsSetupErrorFree())
            {
                EditorGUILayout.HelpBox("VRCLens組み込み処理の不具合が発生されました。\nhirabiki様に連絡してください。\n\nThere is a problem with the current VRCLens setup.\nPlease contact hirabiki on Twitter for troubleshooting.", MessageType.Error);
                except = false;
                fatalError = true;
            } else
            {
                except = false;
            }

            bool canSwap = false;
            bool canCorrect = false;
            if(!except && !config.IsInCorrectHand())
            {
                canSwap = true;
                EditorGUILayout.HelpBox("Camera object is not in the correct hand.", MessageType.Warning);
            }

            if(canSwap && GUILayout.Button("Auto Swap Camera's Hand"))
            {
                config.SwapCameraHand();
            }
            if(canCorrect && GUILayout.Button("Auto Align Camera to Head"))
            {
                config.AlignCameraDesktopHead();
            }
            if(canFix && GUILayout.Button("Auto Fix"))
            {
                config.FixSetup();
            }


            if(!config.HasEnoughParameters())
            {
                EditorGUILayout.HelpBox($"Not enough free expression parameters memory space of {rp} bits to use on the avatar.", MessageType.Error);
                //except = true;
            } else if(!config.IsSubMenuUsedByAvatar())
            {
                EditorGUILayout.HelpBox("Selected Menu is not used by the avatar.", MessageType.Warning);
                except = true;
            } else if(!config.HasEnoughMenuControls())
            {
                EditorGUILayout.HelpBox("No space to put VRCLens in the (sub) menu.", MessageType.Warning);
                except = true;
            } else if(config.Avatar != null && !except && !fatalError)
            {
                EditorGUILayout.HelpBox("VRCLens can be added to the avatar.", MessageType.None);
            }

            EditorGUI.BeginDisabledGroup(except);
            if(GUILayout.Button(new GUIContent("Apply VRCLens", "Automatically add or update VRCLens into the chosen avatar"), GUILayout.Height(27)))
            {
                bool avatarWD = config.ContainsWriteDefaults();
                bool doApply = config.useWriteDefaults == avatarWD;
                if(!doApply)
                {
                    int ret = EditorUtility.DisplayDialogComplex("VRCLens",
                        @"The Write Defaults setting is different from the avatar. This may cause animation issues on the avatar and VRCLens. Fix this before applying?

Write Defaults設定がアバター環境とは異なっています。
アバターとVRCLensに干渉が発生する可能性がある。
訂正して適用してよろしいですか？", "Yes", "Cancel", "No");
                    switch(ret)
                    {
                        case 0:
                            _useWriteDefaults.boolValue = avatarWD;
                            config.useWriteDefaults = avatarWD;
                            doApply = true;
                            break;
                        case 1:
                            break;
                        case 2:
                            doApply = true;
                            break;
                    }
                }
                if(doApply)
                {
                    EditorUtility.DisplayProgressBar("VRCLens Setup", "Updating VRCLens from prefab", 0.0f);
                    try
                    {
                        config.RefreshCopyFolderPath();
                        config.RefreshAssetReserializeList();
                        ApplySetup();
                        ApplyAnimations();
                        config.ReserializeAssetList();
                    } finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            GuiLine();

            if(config.IsAlreadySetup() && GUILayout.Button(new GUIContent("Remove VRCLens", "Remove everything added by VRCLens from the avatar"))
                && EditorUtility.DisplayDialog("VRCLens", @"Do you want to remove VRCLens and all its animations, parameters, and menu from your avatar?

VRCLens本体とVRCLensが使用するアニメーションを
アバターから取り出してよろしいですか？", "Yes", "No"))
            {
                config.RefreshAssetReserializeList();
                config.RemovePreviousAnimations();
                config.RemovePreviousSetup();
                config.ReserializeAssetList();
                //EditorUtility.ClearProgressBar();
            }
            bool willSelfDestruct = false;
            if(canRestart && GUILayout.Button("Reset Setup")) // BACKWARD COMPATIBILITY
            {
                config.RefreshCopyFolderPath();
                GameObject replacement = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{config.PrefabPath}/VRCLens.prefab"));
                replacement.name = replacement.name.Substring(0, replacement.name.Length - 7);
                Selection.activeTransform = replacement.transform;
                // Hacky internal sutff
                UnityEditorInternal.ComponentUtility.CopyComponent(config);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(replacement.GetComponent<VRCLensSetup>());
                // Call the preserve reset on the replacement, not here
                replacement.GetComponent<VRCLensSetup>().PreserveSetupOnReset();
                willSelfDestruct = true;
            }
            if(!canRestart && config.IsAlreadySetup() && GUILayout.Button(new GUIContent("Load Avatar Setup", "Move VRCLens objects to what the avatar has currently")))
            {
                config.PreserveSetupOnReset();
                EnforceInspectorAdjustments(true);
            }

            //EditorGUILayout.LabelField($"VRCLens v{Managed.VRCLensVerifier.VRCLENS_VERSION} - by hirabiki", EditorStyles.boldLabel);
            //EditorGUILayout.PropertyField(_readme, new GUIContent("README FILE"));

            serializedObject.ApplyModifiedProperties();
            if(willSelfDestruct)
            {
                DestroyImmediate(config.gameObject);
            }

            //Debug.Log(System.IO.Path.GetDirectoryName(Application.dataPath));
        }

        private void ApplySetup()
        {
            if(PrefabUtility.IsAnyPrefabInstanceRoot(config.gameObject)) // FAIL-SAFE
            {
                PrefabUtility.UnpackPrefabInstance(config.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }

            // Force refresh for trouble-free upgrade
            RebuildVRCLensObjects(true);

            // Now configure
            EditorUtility.DisplayProgressBar("VRCLens Setup", "Adding VRCLens objects to your avatar", 0.0f);
            config.Configurate();

            // Refresh it again
            EditorUtility.DisplayProgressBar("VRCLens Setup", "Restoring VRCLens setup objects", 0.1f);
            RebuildVRCLensObjects(false);
        }
        private void ApplyAnimations()
        {
            config.AppendAnimationSetup();
        }

        private void RebuildVRCLensObjects(bool keepLastSetup)
        {   // HACK: camModel == null --> HOW IS THAT EVEN POSSIBLE?
            Transform cm = camModel == null || camModel.childCount < 2 ? null : camModel.GetChild(camModel.childCount - 1);
            Quaternion lastRot = cm == null ? Quaternion.identity : cm.localRotation;
            // Detatch from child first so it won't get deleted
            if(keepLastSetup)
            {
                camBase.SetParent(null);
                headBase.SetParent(null);
                if(selfFocus != null) selfFocus.SetParent(null);
                focusPen.SetParent(null);
            }

            // Force refresh - delete the old and re-add the new
            while(config.transform.childCount > 0)
            {
                DestroyImmediate(config.transform.GetChild(0).gameObject);
            }
            Transform newChildren = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{config.PrefabPath}/VRCLens.prefab")).transform;
            while(newChildren.childCount > 0)
            {
                newChildren.GetChild(0).SetParent(config.transform);
            }
            DestroyImmediate(newChildren.gameObject);

            // Renew references
            if(keepLastSetup)
            {
                config.PreserveSetupOnReset(camBase, headBase, selfFocus, focusPen, !keepLastSetup);
            } else
            {
                config.PreserveSetupOnReset();
            }

            // Delete detached objects
            if(keepLastSetup)
            {
                DestroyImmediate(camBase.gameObject);
                DestroyImmediate(headBase.gameObject);
                if(selfFocus != null) DestroyImmediate(selfFocus.gameObject);
                DestroyImmediate(focusPen.gameObject);
            }

            // Re-reference the 3 objects
            RefreshReferences();
            ChangeCameraModel(true, lastRot);
            EnforceInspectorAdjustments(true);
        }

        private void EnforceInspectorAdjustments(bool ignoreValidate)
        {
            camBase.localRotation = Quaternion.Euler(config.cameraRotation);
            focusPen.localRotation = Quaternion.Euler(config.focusPenRotation);
            camBase.localScale = Vector3.one;
            camBase.GetChild(0).localScale = Vector3.one;
            camBase.GetChild(0).localRotation = Quaternion.identity;
            camModel.localPosition = Vector3.zero;
            camModel.localScale = config.cameraSize * Vector3.one;

            Transform cm = camModel == null || camModel.childCount < 2 ? null : camModel.GetChild(camModel.childCount - 1);
            if(cm != null)
            {
                cm.localPosition = config.cameraModelPosition;
                cm.localRotation = Quaternion.Euler(config.cameraModelRotation);
            }

            if(config.queueValidate || ignoreValidate)
            {
                // This is always ran (ignoreValidate == true) after hitting Apply VRCLens
                previewBase.localPosition = config.previewPosition;
                previewBase.GetChild(0).localScale = config.previewSize * new Vector3(1f, 0.5625f, 0f);
                Vector3 s = previewBase.lossyScale;
                touchDetectCam.orthographicSize = Mathf.Min(s.x, s.y) * config.previewSize * 0.5625f * 0.5f;
                // touchDetectCam.aspect = 16f / 9f; // This does not survive an avatar upload
                config.queueValidate = false;
            }
        }

        private void ChangeCameraModel(bool keepRotation, Quaternion lastLocalRot)
        {
            Transform protect = camModel.GetChild(camModel.childCount - 1);
            if(protect.name != "PreviewBase")
            {
                DestroyImmediate(protect.gameObject);
            }
            if(config.cameraModel != null)
            {
                GameObject o = Instantiate(config.cameraModel, camModel);
                o.name = o.name.Substring(0, o.name.Length - 7);
                if(keepRotation)
                {
                    o.transform.localRotation = lastLocalRot;
                }
            }
        }

        private void RefreshReferences()
        {
            focusPen = config.transform.Find("FocusObject");
            selfFocus = config.transform.Find("SelfieFocusPoint");

            camBase = config.transform.Find("CamBase");
            headBase = config.transform.Find("HeadMountPoint");
            if(camBase != null)
            {
                camModel = camBase.Find("CamObject/CameraModel");
                previewBase = camModel.Find("PreviewBase");
                touchDetectCam = previewBase.Find("PreviewMesh/FocusPad").GetComponent<Camera>();
            }
        }

        private bool IsInScene(VRCLensSetup s)
        {
            if(s == null) return false;
            else return !string.IsNullOrEmpty(s.gameObject.scene.path);
        }

        private SerializedProperty FindProp(string name)
        {
            // Use nameof to make for easy maintainance!
            return serializedObject.FindProperty(name);
        }

        private void GuiLine(int i_height = 2)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

    }
}