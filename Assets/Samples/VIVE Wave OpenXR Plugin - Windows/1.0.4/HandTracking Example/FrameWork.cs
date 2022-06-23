using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using System;
using VIVE.HandTracking;

public static class FrameWork
{
    public static void StartFrameWork(bool isLeft)
    {
        var feature = OpenXRSettings.Instance.GetFeature<HandTracking_OpenXR_API>();

        // Create a hand tracker for left hand that tracks default set of hand joints.
        XrHandTrackerCreateInfoEXT createInfo = new XrHandTrackerCreateInfoEXT(
            XrStructureType.XR_TYPE_HAND_TRACKER_CREATE_INFO_EXT,
            IntPtr.Zero,
            XrHandEXT.XR_HAND_LEFT_EXT,
            XrHandJointSetEXT.XR_HAND_JOINT_SET_DEFAULT_EXT);
        int res;
        if (isLeft)
        {
            res = feature.xrCreateHandTrackerEXT(createInfo, out feature.m_leftHandle);
        }
        else
        {
            createInfo.hand = XrHandEXT.XR_HAND_RIGHT_EXT;
            res = feature.xrCreateHandTrackerEXT(createInfo, out feature.m_rightHandle);
        }
        if (res != (int)XrResult.XR_SUCCESS)
        {
            UnityEngine.Debug.LogError("Failed to create hand tracker with error code " + res);
            return;
        }

        return;
    }
    public static void StopFrameWork(bool isLeft)
    {
        var feature = OpenXRSettings.Instance.GetFeature<HandTracking_OpenXR_API>();
        {
            int res = (int)XrResult.XR_SUCCESS;
            if (isLeft)
            {
                if (feature.m_leftHandle == ulong.MinValue) return;
                res = feature.xrDestroyHandTrackerEXT(feature.m_leftHandle);
            }
            else
            {
                if (feature.m_rightHandle == ulong.MinValue) return;
                res = feature.xrDestroyHandTrackerEXT(feature.m_rightHandle);
            }
            if (res != (int)XrResult.XR_SUCCESS)
            {
                UnityEngine.Debug.LogError("Failed to destroy hand tracker with error code " + res);
            }
        }
        return;
    }
}
