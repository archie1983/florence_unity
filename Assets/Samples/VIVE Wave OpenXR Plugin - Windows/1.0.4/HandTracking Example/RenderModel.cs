using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VIVE.HandTracking;
using UnityEngine.XR.OpenXR;
public class RenderModel : MonoBehaviour
{
    public Transform[] nodes = new Transform[(int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT];
    [Tooltip("Draw left hand if true, right hand otherwise")]
    public bool isLeft = false;
    [Tooltip("Use inferred or last-known posed when hand loses tracking if true.")]
    public bool allowUntrackedPose = false;
    [Tooltip("Root object of skinned mesh")]
    public GameObject Hand = null;
    private XrHandJointLocationEXT[] HandjointLocations = new
        XrHandJointLocationEXT[(int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT];
    // Start is called before the first frame update
    void Start()
    {
        UnityEngine.Debug.Log("Start");
        FrameWork.StartFrameWork(isLeft);
    }

    // Update is called once per frame
    void Update()
    {
        var feature = OpenXRSettings.Instance.GetFeature<HandTracking_OpenXR_API>();
        if (feature.m_leftHandle == ulong.MinValue && feature.m_rightHandle == ulong.MinValue)
            return;
        if (UpdateHandData.GetJointLocation(isLeft, out HandjointLocations))
        {
            Hand.SetActive(true);
            UpdateJointLocation();
        }
        else
        {
            Hand.SetActive(false);
        }
    }

    void UpdateJointLocation()
    {
        XrVector3f position;
        Quaternion orientation;
        position = HandjointLocations[1].pose.position;
        orientation = new Quaternion(
                -1 * (HandjointLocations[1].pose.orientation.x),
                1 * (HandjointLocations[1].pose.orientation.y),
                -1 * HandjointLocations[1].pose.orientation.z,
                1 * HandjointLocations[1].pose.orientation.w);
        nodes[1].transform.localPosition = new Vector3(position.x, position.y, -position.z);
        nodes[1].transform.localRotation = (orientation);
        nodes[1].transform.Rotate(new Vector3(180.0f,0.0f,0.0f), Space.World);

        for (int i = (int)XrHandJointEXT.XR_HAND_JOINT_PALM_EXT; i < (int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT; i++)
        {
            if (allowUntrackedPose) //Use inferred or last-known pose when lost tracking 
            {
                if ((HandjointLocations[i].locationFlags & (ulong)XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_VALID_BIT) != 0)
                {
                    orientation = new Quaternion(
                      -1 * (HandjointLocations[i].pose.orientation.x),
                      1 * (HandjointLocations[i].pose.orientation.y),
                      -1 * HandjointLocations[i].pose.orientation.z,
                      1 * HandjointLocations[i].pose.orientation.w);
                    nodes[i].transform.rotation = orientation;
                    nodes[i].transform.Rotate(new Vector3(180.0f, 0.0f, 0.0f), Space.World);
                }
            }
            else
            {
                if ((HandjointLocations[i].locationFlags & (ulong)XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT) != 0)
                {
                    orientation = new Quaternion(
                      -1 * (HandjointLocations[i].pose.orientation.x),
                      1 * (HandjointLocations[i].pose.orientation.y),
                      -1 * HandjointLocations[i].pose.orientation.z,
                      1 * HandjointLocations[i].pose.orientation.w);
                    nodes[i].transform.rotation = orientation;
                    nodes[i].transform.Rotate(new Vector3(180.0f, 0.0f, 0.0f), Space.World);
                }
            }
        }
    }

    public void OnDestroy()
    {
        UnityEngine.Debug.Log("OnDestroy");
        FrameWork.StopFrameWork(isLeft);
    }
}
