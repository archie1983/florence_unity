# VIVE Wave OpenXR Hand Tracking Unity Feature

To help software developers create an application for locating hand joints with the OpenXR hand tracking extension [XR_EXT_hand_tracking](https://www.khronos.org/registry/OpenXR/specs/1.0/html/xrspec.html#XR_EXT_hand_tracking).

## Load sample code
**Window** > **Package Manager** > **VIVE Wave OpenXR Plugin - Windows** > **Samples** > Click to import **HandTracking Example**

## Play the sample scene    
1. **Edit** > **Project Settings** > **XR Plug-in Management** > Select **OpenXR** , click Exclamation mark next to it then choose **Fix All**.
2. **Edit** > **Project Settings** > **XR Plug-in Management** > **OpenXR** > Add Interaction profiles for your device.
3. **Edit** > **Project Settings** > **XR Plug-in Management** > **OpenXR** > Select **Hand Tracking** under **VIVE Wave OpenXR** Feature Groups.
4. In the Unity Project window, select the sample scene file in **Assets** > **Samples** > **VIVE Wave OpenXR Plugin - Windows** > **1.0.4** > **HandTracking Example** > **Scenes** > **HandTrackingScene.unity** then click Play.

## Use VIVE Wave OpenXR Hand Tracking Unity Feature to draw skeleton hand.
1. Import VIVE Wave OpenXR Plugin - Windows
2. Add Hand gameobject to the Unity scene
    - Refer to functions **StartFrameWork** and **StopFrameWork** in **FrameWork.cs** for creating and releasing handle for hand.
    - Refer to the function **GetJointLocation** in **RenderHand.cs** for getting the information to locate hand joints.
    - Drag "Skeleton" prefab into scene hierarchy or Create an empty object and attach **RenderHand.cs**.

## Use VIVE Wave OpenXR Hand Tracking Unity Feature to draw 3D hand.
1. Import VIVE Wave OpenXR Plugin - Windows
2. Add Hand gameobject to the Unity scene
    - Refer to functions **StartFrameWork** and **StopFrameWork** in **FrameWork.cs** for creating and releasing handle for hand.
    - Refer to the function **GetJointLocation** in **RenderModel.cs** for getting the information to locate hand joints.
    - Drag "OBJModel" prefab into scene hierarchy.