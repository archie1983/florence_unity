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
    /**
     * This enum will allow controlling the process of gesture detection
     */
    private enum HandActionState
    {
        IDLE,
        ACTION_DETECTED,
        ACTION_IN_PROGRESS
    };

    /**
     * This enum will allow to distinguish between operation of discrete crawl and rotate - where robot crawls or rotates by the requested amount and continuous- where robot continues movement
     * with speed dependent on the hand offset from the centre until the gesture stops.
     */
    private enum OperationStyle
    {
        NONE,
        DISCRETE,
        CONTINUOUS
    };

    private HandActionState state = HandActionState.IDLE;
    private OperationStyle oper_style = OperationStyle.CONTINUOUS;
    private Vector3 fistStartPos, fistEndPos;
    private float userDraggedSidewaysDistance = 0.0f, userDraggedForwardDistance = 0.0f; //# When user want to rotate, we will store here by how much
    public ROSConnection ros;
    private Twist continuous_move = new Twist();
    private Float32 rotation_x = new Float32();
    private Float32 diving_x = new Float32();

    //private bool exit = true;
    private String cmd_vel_topic = "cmd_vel";
    private String cont_drv_topic = "base_cntrl/continuous"; //# Continuous drive topic
    
    private String rotation_cntrl_topic = "/base_cntrl/rotate_x"; //# rotation conrol topic
    private String crawl_cntrl_topic = "/base_cntrl/crawl_x"; //# crawl conrol topic

    // Gesture types for crawling and rotation.
    private GestureType crawl_gesture = GestureType.Victory;
    private GestureType rotation_gesture = GestureType.Fist;

    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<Twist>(cmd_vel_topic);
        ros.RegisterPublisher<Twist>(cont_drv_topic);
        ros.RegisterPublisher<Float32>(rotation_cntrl_topic);
        ros.RegisterPublisher<Float32>(crawl_cntrl_topic);
    }

    // Update is called once per frame
    void Update()
    {
        if (oper_style == OperationStyle.DISCRETE)
        {
            evaluateGestureForDiscreteOperation();
        }
        else if (oper_style == OperationStyle.CONTINUOUS)
        {
            evaluateGestureForContinuousOperation();
        }
    }

    /**
     * This function allows for continuous operation- e.g. gesture detected and offset is continuously calculated from the detected place. Offset then sets the speed of the movement. This continues
     * until the hand is released.
     */
    private void evaluateGestureForContinuousOperation()
    {
        //# AE: First let's examine if we want to rotate
        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == rotation_gesture && state == HandActionState.IDLE)
        {
            //# Remember that we've started rotation and where we started
            state = HandActionState.ACTION_DETECTED;
            fistStartPos = GestureProvider.LeftHand.position;
        }
        else if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == rotation_gesture && (state == HandActionState.ACTION_DETECTED || state == HandActionState.ACTION_IN_PROGRESS))
        {
            //# Now send speed updates according to the offset
            state = HandActionState.ACTION_IN_PROGRESS;
            fistEndPos = GestureProvider.LeftHand.position;

            userDraggedSidewaysDistance = fistStartPos.x - fistEndPos.x;
            userDraggedForwardDistance = fistStartPos.z - fistEndPos.z;

            //# Ok, so now we have a user's command to move the robot. We also have an indication by how much we want to move it.
            //# We can now issue a Twist command, which is essentially the offsets detected.
            continuous_move.linear.x = userDraggedForwardDistance * -1.0;
            continuous_move.linear.y = 0;
            continuous_move.linear.z = 0;

            continuous_move.angular.x = 0;
            continuous_move.angular.y = 0;
            continuous_move.angular.z = userDraggedSidewaysDistance; //# we will be rotating on Z axis at the speed requested by user.

            UnityEngine.Debug.Log("CMove: " + continuous_move.linear.x + " " + continuous_move.angular.z);

            ros.Publish(cont_drv_topic, continuous_move);

            //# Now that we've sent off the rotation, we need to stop it after a little while -- dependent on how much the user dragged
            //new Thread(this.stopRobotMotionAfterTime).Start(Convert.ToInt32(Math.Abs(userDraggedSidewaysDistance * 1000)));
        }
        else if (GestureProvider.LeftHand == null || (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture != rotation_gesture))
        {
            //# Now end the process
            ros.Publish(cmd_vel_topic, new Twist()); //# tell robot to stop
            state = HandActionState.IDLE;
        }
    }

    /**
     * This function allows for a discrete operation- e.g. gesture detected and position remembered, then hand moved and released, now the offset is known and we move by that.
     */
    private void evaluateGestureForDiscreteOperation()
    {
        //# AE: First let's examine if we want to rotate
        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == rotation_gesture && state == HandActionState.IDLE)
        {
            state = HandActionState.ACTION_DETECTED;
            fistStartPos = GestureProvider.LeftHand.position;
            /*
            txtCoord.text = string.Format("{0:0.##}", GestureProvider.LeftHand.position.x * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.y * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.z * SCALE)
                + "\n" + GestureProvider.LeftHand.rotation.ToString();
            */
        }
        else if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture != rotation_gesture && state == HandActionState.ACTION_DETECTED)
        {
            state = HandActionState.ACTION_IN_PROGRESS;
            fistEndPos = GestureProvider.LeftHand.position;
            UnityEngine.Debug.Log("Drag: " + (fistStartPos.x - fistEndPos.x));
            userDraggedSidewaysDistance = fistStartPos.x - fistEndPos.x;

            rotation_x.data = userDraggedSidewaysDistance;
            ros.Publish(rotation_cntrl_topic, rotation_x);
            state = HandActionState.IDLE; //# Finally let's get ready for new actions

            //ros.Publish(cmd_vel_topic, rotation);

            //# Now that we've sent off the rotation, we need to stop it after a little while -- dependent on how much the user dragged
            //new Thread(this.stopRobotMotionAfterTime).Start(Convert.ToInt32(Math.Abs(userDraggedSidewaysDistance * 1000)));
        }

        //# AE: now for the crawl part
        if (GestureProvider.RightHand != null && GestureProvider.RightHand.gesture == crawl_gesture && state == HandActionState.IDLE)
        {
            state = HandActionState.ACTION_DETECTED;
            fistStartPos = GestureProvider.RightHand.position;
            /*
            txtCoord.text = string.Format("{0:0.##}", GestureProvider.LeftHand.position.x * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.y * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.z * SCALE)
                + "\n" + GestureProvider.LeftHand.rotation.ToString();
            */
        }
        else if (GestureProvider.RightHand != null && GestureProvider.RightHand.gesture != crawl_gesture && state == HandActionState.ACTION_DETECTED)
        {
            state = HandActionState.ACTION_IN_PROGRESS;
            fistEndPos = GestureProvider.RightHand.position;
            UnityEngine.Debug.Log("Dive: " + (fistStartPos.y - fistEndPos.y));
            userDraggedForwardDistance = fistStartPos.y - fistEndPos.y;

            diving_x.data = userDraggedForwardDistance;
            ros.Publish(crawl_cntrl_topic, diving_x);
            state = HandActionState.IDLE; //# Finally let's get ready for new actions

            //ros.Publish(cmd_vel_topic, rotation);

            //# Now that we've sent off the rotation, we need to stop it after a little while -- dependent on how much the user dragged
            //new Thread(this.stopRobotMotionAfterTime).Start(Convert.ToInt32(Math.Abs(userDraggedForwardDistance * 1000)));
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
            ros.Publish(cmd_vel_topic, continuous_move);
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
