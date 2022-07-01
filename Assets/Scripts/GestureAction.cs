using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ViveHandTracking;

//# Needed to send Twist messages
using Unity.Robotics.ROSTCPConnector;
using Twist = RosMessageTypes.Geometry.TwistMsg;
using Float32 = RosMessageTypes.Std.Float32Msg;

//# Needed for threading
using System;
using System.Diagnostics;
using System.Threading;

public class GestureAction : MonoBehaviour
{
    private enum HandActionState
    {
        IDLE,
        ACTION_DETECTED,
        ACTION_IN_PROGRESS
    };

    private HandActionState state = HandActionState.IDLE;
    private Vector3 fistStartPos, fistEndPos;
    private float userDraggedDistance = 0.0f; //# When user want to rotate, we will store here by how much
    public ROSConnection ros;
    private Twist rotation = new Twist();
    private Float32 rotation_x = new Float32();

    //private bool exit = true;
    private String cmd_vel_topic = "cmd_vel";
    private String rotation_cntrl_topic = "/base_cntrl/rotate_x"; //# rotation conrol topic

    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<Twist>(cmd_vel_topic);
        ros.RegisterPublisher<Float32>(rotation_cntrl_topic);
    }

    // Update is called once per frame
    void Update()
    {
        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Fist && state == HandActionState.IDLE)
        {
            state = HandActionState.ACTION_DETECTED;
            fistStartPos = GestureProvider.LeftHand.position;
            /*
            txtCoord.text = string.Format("{0:0.##}", GestureProvider.LeftHand.position.x * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.y * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.z * SCALE)
                + "\n" + GestureProvider.LeftHand.rotation.ToString();
            */
        } else if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture != GestureType.Fist && state == HandActionState.ACTION_DETECTED)
        {
            state = HandActionState.ACTION_IN_PROGRESS;
            fistEndPos = GestureProvider.LeftHand.position;
            UnityEngine.Debug.Log("Drag: " + (fistStartPos.x - fistEndPos.x));
            userDraggedDistance = fistStartPos.x - fistEndPos.x;

            //# Ok, so now we have a user's command to rotate the robot. We also have an indication by how much we want to move it.
            //# We can now issue a Twist command, which is essentially
            rotation.linear.x = 0;
            rotation.linear.y = 0;
            rotation.linear.z = 0;

            rotation.angular.x = 0;
            rotation.angular.y = 0;
            if (userDraggedDistance > 0)
            {
                rotation.angular.z = 0.5; //# we will be rotating on Z axis at a modest speed for a duration that we'll figure out from the draged amount by user.
            } else
            {
                rotation.angular.z = -0.5; //# we will be rotating on Z axis at a modest speed for a duration that we'll figure out from the draged amount by user.
            }

            rotation_x.data = userDraggedDistance;
            ros.Publish(rotation_cntrl_topic, rotation_x);
            state = HandActionState.IDLE; //# Finally let's get ready for new actions

            //ros.Publish(cmd_vel_topic, rotation);

            //# Now that we've sent off the rotation, we need to stop it after a little while -- dependent on how much the user dragged
            //new Thread(this.stopRobotMotionAfterTime).Start(Convert.ToInt32(Math.Abs(userDraggedDistance * 1000)));
        }
    }

    private void stopRobotMotionAfterTime(System.Object robot_runs_for_this_long_ms)
    {
        int delay_time_ms;
        try
        {
            delay_time_ms = (int)robot_runs_for_this_long_ms;
        }
        catch (InvalidCastException)
        {
            //UnityEngine.Debug.Log("EXCE");
            delay_time_ms = 500;
        }
        var sw = Stopwatch.StartNew();
        do
        {
            //UnityEngine.Debug.Log("SLEEPE: " + sw.ElapsedMilliseconds);
            ros.Publish(cmd_vel_topic, rotation);
            Thread.Sleep(100);
        } while (sw.ElapsedMilliseconds <= delay_time_ms);

        ros.Publish(cmd_vel_topic, new Twist()); //# tell robot to stop
        state = HandActionState.IDLE; //# Finally let's get ready for new actions

        sw.Stop();
    }

    void OnTriggerEnter(Collider other)
    {
        UnityEngine.Debug.Log("TRIG ENTER");
    }

    void OnTriggerExit(Collider other)
    {
        UnityEngine.Debug.Log("TRIG EXIT");
        //if (other.GetComponent<Rigidbody>() != target) return;
        //if (state == 1) exit = true;
    }

    public void OnStateChanged(int state)
    {
        UnityEngine.Debug.Log("OK detected");
        /*        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Like)
                {
                    txtCoord.text = GestureProvider.LeftHand.position.x + " # " + GestureProvider.LeftHand.position.y + " # " + GestureProvider.LeftHand.position.z;
                }*/
    }
}
