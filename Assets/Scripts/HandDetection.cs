using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.HandTracking;

public class HandDetection : MonoBehaviour
{
    public Transform[] nodes = new Transform[(int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT];
    [Tooltip("Draw left hand if true, right hand otherwise")]
    public bool isLeft = false;
    [Tooltip("Use inferred or last-known posed when hand loses tracking if true.")]
    public bool allowUntrackedPose = false;

    [Tooltip("Do we want to render a skeleton hand")]
    public bool isSkel = false;

    //# If we want a skeleton hand, then we will need to keep track of its components and know how to generate them on the fly
    // Links between keypoints, 2*i & 2*i+1 forms a link.
    // keypoint index: 1: palm, 2-5: thumb, 6-10: index, 11-15: middle, 16-20: ring, 21-25: pinky
    // fingers are counted from bottom to top
    private static int[] Connections = new int[] {
    1,  2,  1,  6,  1,  11, 1,  16, 1, 21,  // palm and finger starts
    3,  6,  6,  11, 11, 16, 16, 21,         // finger starts
    2,  3,  3,  4,  4,  5,                  // thumb
    6,  7,  7,  8,  8,  9,  9,  10,                  // index
    11, 12, 12, 13, 13, 14, 14, 15,                 // middle
    16, 17, 17, 18, 18, 19, 19, 20,                // ring
    21, 22, 22, 23, 23, 24, 24, 25                 // pinky
  };
    [Tooltip("Default color of hand points")]
    public Color pointColor = Color.green;
    [Tooltip("Default color of links between keypoints in skeleton mode")]
    public Color linkColor = Color.white;
    [Tooltip("Material for hand points and links")]
    [SerializeField]
    private Material material = null;

    //# The actual points and links between them will be stored here
    private List<GameObject> points = new List<GameObject>();
    // list of links created (only for skeleton)
    private List<GameObject> links = new List<GameObject>();
    // shared material for all point objects
    private Material pointMat = null;
    // shared material for all link objects
    private Material linkMat = null;

    //# If we don't want a skeleton hand, then use this Hand GameObject
    [Tooltip("Root object of skinned mesh")]
    public GameObject Hand = null;

    //# Either way our hand joint locations will be updated with each frame and stored here
    private XrHandJointLocationEXT[] HandjointLocations = new
        XrHandJointLocationEXT[(int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT];

    private static XrHandJointLocationsEXT locations;

    // Start is called before the first frame update
    void Start()
    {
        //AE: all we do here is create objects, e.g. createInfo and feature.
        var feature = OpenXRSettings.Instance.GetFeature<HandTracking_OpenXR_API>();
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
        }

        //# AE: If we want a skeleton hand and not a prepared prefab, then create the skeleton hand components:
        if (isSkel)
        {
            UnityEngine.Debug.Log("Creating a skeleton hand");
            createSkeletonHandFromScratch();
        }
    }

    // Update is called once per frame
    void Update()
    {
        //# AE: If we have no hands, then do nothing, otherwise let's run the detection mechanism.
        var feature = OpenXRSettings.Instance.GetFeature<HandTracking_OpenXR_API>();
        if (feature.m_leftHandle == ulong.MinValue && feature.m_rightHandle == ulong.MinValue)
        {
            //UnityEngine.Debug.Log("No feature");
            return;
        } else
        {
            getHandTrackingDetectionResult();
        }
    }

    /**
     * The actual hand detection. We get the hand tracking detection result here and then we will update the position of the hand later
     * in a different function.
     */
    private void getHandTrackingDetectionResult()
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
            fixed (XrHandJointLocationEXT* ptr = HandjointLocations)
            {
                locations.type = XrStructureType.XR_TYPE_HAND_JOINT_LOCATIONS_EXT;
                locations.next = IntPtr.Zero;
                locations.isActive = 0;
                locations.jointCount = (int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT;
                locations.jointLocations = (IntPtr)ptr;
                int res;
                if (isLeft)
                {
                    res = feature.xrLocateHandJointsEXT(feature.m_leftHandle, locateInfo, ref locations);
                }
                else
                {
                    res = feature.xrLocateHandJointsEXT(feature.m_rightHandle, locateInfo, ref locations);
                }
            }
        }
        if (locations.isActive == 1)
        {
            UpdateJointLocation();//Update your hand model here.
        }
        else
        {
            UnityEngine.Debug.Log("Not active");
            //Hide your hand model due to not detect the hand input 
        }
    }

    /**
     * Here we will re-draw our hand- whether a skeleton one or a prefab one.
     */
    private void UpdateJointLocation()
    {
        if (isSkel) //# For skeleton hand:
        {
            for (int i = 0; i < points.Count; i++)
            {
                var go = points[i];
                XrQuaternionf orientation;
                XrVector3f position;
                float radius = HandjointLocations[i].radius;
                if (allowUntrackedPose) //Use inferred or last-known pose when lost tracking 
                {
                    if ((HandjointLocations[i].locationFlags & (ulong)XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_VALID_BIT) != 0)
                    {
                        orientation = HandjointLocations[i].pose.orientation;
                    }
                    if ((HandjointLocations[i].locationFlags & (ulong)XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_VALID_BIT) != 0)
                    {
                        position = HandjointLocations[i].pose.position;
                        go.transform.localPosition = new Vector3(position.x, position.y, -position.z);
                        go.SetActive(true);
                    }
                    else
                    {
                        go.SetActive(false);
                    }
                }
                else
                {
                    if ((HandjointLocations[i].locationFlags & (ulong)XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT) != 0)
                    {
                        orientation = HandjointLocations[i].pose.orientation;
                    }
                    if ((HandjointLocations[i].locationFlags & (ulong)XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_TRACKED_BIT) != 0)
                    {
                        position = HandjointLocations[i].pose.position;
                        go.transform.localPosition = new Vector3(position.x, position.y, -position.z);
                        go.SetActive(true);
                    }
                    else
                    {
                        go.SetActive(false);
                    }
                }
            }

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                var pose1 = points[Connections[i * 2]].transform.position;
                var pose2 = points[Connections[i * 2 + 1]].transform.position;

                // calculate link position and rotation based on points on both end
                link.SetActive(true);
                link.transform.position = (pose1 + pose2) / 2;
                var direction = pose2 - pose1;
                link.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
                link.transform.localScale = new Vector3(0.006f, direction.magnitude / 2f - 0.0051f, 0.006f);
            }
        } else //# For a hand prefab
        {
            Hand.SetActive(true);
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
            nodes[1].transform.Rotate(new Vector3(180.0f, 0.0f, 0.0f), Space.World);

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
    }

    /**
     * Creates a skeleton hand by generating spheres- the joints and cylinders- the tendons
     */
    private void createSkeletonHandFromScratch()
    {
        pointMat = new Material(material);
        if (isLeft)
        {
            pointColor = Color.blue;
        }
        else
        {
            pointColor = Color.red;
        }
        pointMat.color = pointColor;
        linkMat = new Material(material);
        linkMat.color = linkColor;

        for (int i = 0; i < (int)XrHandJointEXT.XR_HAND_JOINT_MAX_ENUM_EXT; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = ((XrHandJointEXT)i).ToString();
            go.transform.parent = transform;
            go.transform.localScale = Vector3.one * 0.012f;
            go.SetActive(false);
            points.Add(go);
            go.transform.position = new Vector3((float)i * 0.1f, 0, 0);
            // handle layer
            go.layer = gameObject.layer;
            // handle material
            go.GetComponent<Renderer>().sharedMaterial = pointMat;
        }

        // create game objects for links between keypoints, only used in skeleton mode
        for (int i = 0; i < Connections.Length; i += 2)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "link" + i;
            go.transform.parent = transform;
            go.transform.localScale = Vector3.one * 0.005f;
            go.SetActive(false);
            links.Add(go);
            // handle layer
            go.layer = gameObject.layer;
            // handle material
            go.GetComponent<Renderer>().sharedMaterial = linkMat;
        }
    }

    /**
     * Hides the hand if it becomes inactive- no data for it.
     */
    private void hideHand()
    {
        if (isSkel)
        {
            for (int i = 0; i < points.Count; i++)
            {
                var go = points[i];
                go.SetActive(false);
            }

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                link.SetActive(false);
            }
        } else
        {
            Hand.SetActive(false);
        }
    }

    // End hand detection
    private void OnDestroy()
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
    }
}
