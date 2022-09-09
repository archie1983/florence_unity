using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using UnityEngine.UI;
//using System.Numerics;

using CompressedImage = RosMessageTypes.Sensor.CompressedImageMsg;
using PanTiltAngle = RosMessageTypes.ROS.PanTiltAngleMsg;

public class CameraSubscriber : MonoBehaviour
{
    public ROSConnection ros;
    public Camera mainCamera;

    private Texture2D texture2D;
    public RawImage rimg;
    private byte[] imageData;
    private bool isMessageReceived;

    // This is set from Unity Editor, but it should be "sim_camera1/image_raw/compressed" for Gazebo simulation images and /camera1 for USB camera images
    public string topicName = "";

    // Topic name for where to send HMD rotation position updates so that our camera is pan-tilted where we want to see.
    private string head_rotation_topic_name = "head_rotation";
    private PanTiltAngle head_rotation = new PanTiltAngle();
   
    /*
     * Our head rotation will be relative, so we need to know the initial position, which we'll keep here.
     */
    private Vector2 initial_head_rotation = new Vector2(-1000, -1000);

    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<CompressedImage>(topicName, ShowImage);

        ros.RegisterPublisher<PanTiltAngle>(head_rotation_topic_name);

        texture2D = new Texture2D(1, 1);
    }

    private void Update()
    {
        if (isMessageReceived)
            ProcessImage();

        /*
        if (initial_head_rotation.x == -1000)
        {
            initial_head_rotation = getPanTiltOfHMD();
        } else
        {
            Vector2 pan_tild_of_HMD = getPanTiltOfHMD();
            //UnityEngine.Debug.Log("PTH: " + pan_tild_of_HMD.x + " " + last_detected_pan_region  + " " + pan_tild_of_HMD.y);

            //mainCamera.transform.eulerAngles.ToString()

            head_rotation.pan_angle = pan_tild_of_HMD.x;
            head_rotation.tilt_angle = pan_tild_of_HMD.y;
            ros.Publish(head_rotation_topic_name, head_rotation);
        }
        */

    }

    /**
     * When we detect pan and tilt angles, it will be important where we detected it (particularly important will be lower right and lower left quadrants
     * because on that will depend where will we rotate next if we keep going in the same direction, e.g. if we have 179 degrees and then later we get 182, we really
     * want to turn 3 degrees to the right, not suddenly jerk all the way left to -178. This enum will help track that.
     */
    private enum AngleRegion
    {
        UR,
        LR,
        LL,
        UL,
        NONE
    };

    private AngleRegion last_detected_pan_region = AngleRegion.NONE;
    private AngleRegion last_detected_tilt_region = AngleRegion.NONE;

    private Vector2 getPanTiltOfHMD()
    {
        float pan = mainCamera.transform.eulerAngles.y;
        float tilt = mainCamera.transform.eulerAngles.x;

        /*
         * Now we have pan and tilt in 0-360 Euler degrees. This hasn't yet been subtracted from the initial pose.
         * But we want it to go +-degrees. 
         */
        pan = normalizeEulerAngle(pan, ref last_detected_pan_region);
        tilt = normalizeEulerAngle(tilt, ref last_detected_tilt_region);

        return new Vector2(pan, tilt);
    }

    /**
     * Now if we're getting 0-90 degrees, we want to leave it at that (turn right).
     * If we're getting 270-360 degrees, then we want to do: degrees - 360 (turn left because of negative degrees).
     * Problems start at the other side of the circle, e.g. if we have 179 degrees and then later we get 182, we really
     * want to turn 3 degrees to the right, not suddenly jerk all the way left to -178.
     */
    private int normalizeEulerAngle(float angle, ref AngleRegion last_detected_angle_region)
    {
        if (angle >= 0 && angle <= 90)
        {
            // all good, nothing to adjust
            //angle = angle;
            last_detected_angle_region = AngleRegion.UR;
        }
        else if (angle >= 270 && angle <= 360)
        {
            angle = angle - 360;
            last_detected_angle_region = AngleRegion.UL;
        }
        else if (angle < 270 && angle >= 180 && last_detected_angle_region != AngleRegion.LR)
        {
            last_detected_angle_region = AngleRegion.LL;
            angle = angle - 360;
        }
        else if (angle > 90 && angle <= 180 && last_detected_angle_region != AngleRegion.LL)
        {
            last_detected_angle_region = AngleRegion.LR;
            //angle = angle;
        }

        return (int)angle;
    }

    void ShowImage(CompressedImage ImgMsg)
    {
        imageData = ImgMsg.data;
        isMessageReceived = true;
        //Debug.Log(ImgMsg.format);
        //Debug.Log(ImgMsg.encoding);
        //Debug.Log(imageData.Length);

        //# Debug 1st 10 bytes of the received image
        // string byteText = "";

        // for(int i = 0; i < 10;i++)
        // {
        //     byte b = imageData[i];
        //     byteText += b;
        //     if (i < imageData.Length -1)
        //     {
        //         byteText += ", ";
        //     }
        // }
        // byteText += (" }");

        // Debug.Log(byteText);
    }

    void ProcessImage()
    {
        texture2D.LoadImage(imageData);
        //texture2D.Apply();
        //dome.GetComponent<Renderer>().material.SetTexture("_MainTex", texture2D);

        //dome.GetComponent<Renderer>().material.mainTexture = texture2D;
        rimg.texture = texture2D;
        isMessageReceived = false;
    }

    private static Vector2 Quaternion_to_pan_tilt_degrees(Quaternion q)
    {
        Vector3 v = ToEulerAngles(q);
        //# Z seems to be YAW (so head turning left or right) and Y seems to be PITCH (so head turning up and down)
        return new Vector2((float)(v.z * 180 / Math.PI), (float)(v.y * 180 / Math.PI));
    }
    private static Vector3 ToEulerAngles(Quaternion q)
    {
        Vector3 angles = new();

        // roll / x
        double sinr_cosp = 2 * (q.w * q.x + q.y * q.z);
        double cosr_cosp = 1 - 2 * (q.x * q.x + q.y * q.y);
        angles.x = (float)Math.Atan2(sinr_cosp, cosr_cosp);

        // pitch / y
        double sinp = 2 * (q.w * q.y - q.z * q.x);
        if (Math.Abs(sinp) >= 1)
        {
            //angles.y = (float)Math.CopySign(Math.PI / 2, sinp);
            angles.y = (float)Math.Abs(Math.PI / 2) * Math.Sign(sinp);
        }
        else
        {
            angles.y = (float)Math.Asin(sinp);
        }

        // yaw / z
        double siny_cosp = 2 * (q.w * q.z + q.x * q.y);
        double cosy_cosp = 1 - 2 * (q.y * q.y + q.z * q.z);
        angles.z = (float)Math.Atan2(siny_cosp, cosy_cosp);

        return angles;
    }
}
