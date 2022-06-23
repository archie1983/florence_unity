using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.OpenXR;
using UnityEngine;
using System;
using VIVE.HandTracking;
public static class UpdateHandData
{
    private static XrHandJointLocationEXT[] jointLocations = new 
        XrHandJointLocationEXT[(int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT];
    private static XrHandJointLocationsEXT locations;
    private static void UpdateData(bool isleftHand)
    {
        var feature = OpenXRSettings.Instance.GetFeature<HandTracking_OpenXR_API>();
        XrHandJointsLocateInfoEXT locateInfo = new XrHandJointsLocateInfoEXT(
        XrStructureType.XR_TYPE_HAND_JOINTS_LOCATE_INFO_EXT,
        IntPtr.Zero,
        feature.m_space,
        (Int64)10 //An arbitrary number greater than 0
        );
        unsafe
        {
            fixed (XrHandJointLocationEXT* ptr = jointLocations)
            {
                locations.type = XrStructureType.XR_TYPE_HAND_JOINT_LOCATIONS_EXT;
                locations.next = IntPtr.Zero;
                locations.isActive = 0;
                locations.jointCount = (int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT;
                locations.jointLocations = (IntPtr)ptr;
                int res;
                if(isleftHand)
                {
                    res = feature.xrLocateHandJointsEXT(feature.m_leftHandle, locateInfo, ref locations);
                }
                else
                {
                    res = feature.xrLocateHandJointsEXT(feature.m_rightHandle, locateInfo, ref locations);
                }
            }
        }
    }
    public static bool GetJointLocation(bool isleft,out XrHandJointLocationEXT[] jointLocationData)
    {
        UpdateData(isleft);
        jointLocationData = jointLocations;
        if (locations.isActive == 1)
        {
            return true;
        }
        else// Not detect the hand input or the application lost input focus.
        {
            return false;
        }

    }
}
