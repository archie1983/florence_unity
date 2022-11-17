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
using System.IO;
using System.Collections;

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
        CONTINUOUS,
        CONTINUOUS_PERSISTENT
    };

    public ROSConnection ros;

    private HandActionState state = HandActionState.IDLE;
    private OperationStyle oper_style = OperationStyle.CONTINUOUS_PERSISTENT;

    private Vector3 fistStartPos, fistEndPos;
    private float userDraggedSidewaysDistance = 0.0f, userDraggedForwardDistance = 0.0f; //# When user want to rotate, we will store here by how much

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

    // Gesture type for crawling and rotating in a continuous fashion. We only need one here.
    private GestureType continuous_action_gesture = GestureType.Point;

    private GestureType action_gesture_to_test = GestureType.Victory;

    // Gesture types to stop continuous action. We still need to remove the continuous_action_gesture from this collection at the start of the app.
    private List<GestureType> continuous_action_stop_gestures = new List<GestureType> { 
        GestureType.Fist,
        GestureType.Five,
        GestureType.Like,
        GestureType.OK,
        GestureType.Point,
//        GestureType.Unknown, //# don't want unknown gesture to interrupt the action
        GestureType.Victory };

    private StreamWriter file = null;
    private String fileName = "";
    private int start_stop_counter = 0;
    private ArrayList gesture_confidences = null;

    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<Twist>(cmd_vel_topic);
        ros.RegisterPublisher<Twist>(cont_drv_topic);
        ros.RegisterPublisher<Float32>(rotation_cntrl_topic);
        ros.RegisterPublisher<Float32>(crawl_cntrl_topic);

        // Remove the continuous_action_gesture from the collection that stops the action.
        continuous_action_stop_gestures.Remove(continuous_action_gesture);

        startNewDetectionAnalysis();

        using (file = new(fileName))
        {
            file.WriteLine(start_stop_counter + ":");
        }
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
        else if (oper_style == OperationStyle.CONTINUOUS_PERSISTENT)
        {
            evaluateGestureForContinuousPersistentOperation();
        }

        //logGestureReliability();
    }

    private void FixedUpdate()
    {
        //UnityEngine.Debug.Log("NULLGEST: " + (GestureProvider.LeftHand == null ? "NULL" : GestureProvider.LeftHand.gesture.ToString() + " " + GestureProvider.LeftHand.confidence));
    }

    private void logGestureReliability()
    {
        
        //# AE: First let's examine if we want to start an action
        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == action_gesture_to_test && state == HandActionState.IDLE)
        {
            //# Remember that we've started rotation and where we started
            state = HandActionState.ACTION_DETECTED;
            UnityEngine.Debug.Log("ACTION DETECTED");
            watchDog(DEFAULT_WATCHDOG_BITE_TIME);
            start_stop_counter++;

            using (file = new(fileName))
            {
                file.WriteLine(start_stop_counter + ":");
            }
            gesture_confidences.Add(GestureProvider.LeftHand.confidence);
        }
        /*
         * Continuous hand detection
         */
        else if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == action_gesture_to_test && state == HandActionState.ACTION_DETECTED)
        {
            gesture_confidences.Add(GestureProvider.LeftHand.confidence);
            watchDog(DEFAULT_WATCHDOG_BITE_TIME);
        }
        /*
         * End condition - only for detecting a different gesure. It doesn't work for "hand lost" event detection, because hand SDK doesn't run Update() of this 
         * script when hand is not detected. This is actually handled in a watchdog thread.
         */
        else if (GestureProvider.LeftHand != null && state == HandActionState.ACTION_DETECTED && GestureProvider.LeftHand.gesture != action_gesture_to_test)
        {
            state = HandActionState.IDLE;
            UnityEngine.Debug.Log("ACTION STOPPED");

            using (file = new(fileName))
            {
                file.WriteLine(":" + start_stop_counter);
            }

            /*
             * Still kick the dog, because hand hasn't disappeared. We want to catch this kind of situation and discuss it in paper.
             */
            watchDog(DEFAULT_WATCHDOG_BITE_TIME);
            gesture_confidences.Add(-1.0f);
        }
        /*
         * Hand still detected, but not the gesture we're looking for. Still kick the dog because required gesture might come back.
         */
        else if (GestureProvider.LeftHand != null && state == HandActionState.IDLE && GestureProvider.LeftHand.gesture != action_gesture_to_test)
        {
            watchDog(DEFAULT_WATCHDOG_BITE_TIME);
            gesture_confidences.Add(-1.0f);
        }

    }

    /**
     * Time in ms after which the watchdog will bite if not kicked.
     */
    private int watchdog_bite_time = 0;
    private static int DEFAULT_WATCHDOG_BITE_TIME = 200;
    private Stopwatch watchdog_sw = null;
    private Thread watchdog_thread = null;
    /**
     * Starts a new thread to monitor hand gesture work. If the hand SDK doesn't detect any more hands, then the dog will not be
     * kicked and it will expire, setting the state to IDLE and notifying whatever needs notification.
     */
    private void watchDog(int expire_after_this_long)
    {
        watchdog_bite_time = expire_after_this_long;

        if (watchdog_thread == null)
        {
            watchdog_thread = new Thread(this.watchdogExpiry);
            watchdog_thread.Start();
        }
        else
        {
            watchdog_sw.Restart();
        }
    }

    private void watchdogExpiry()
    {
        watchdog_sw = Stopwatch.StartNew();
        do
        {
            //UnityEngine.Debug.Log("SLEEPE: " + watchdog_sw.ElapsedMilliseconds);
            Thread.Sleep(25);
        } while (watchdog_sw.ElapsedMilliseconds <= watchdog_bite_time);

        /*
         * Watchdog hasn't been kicked for a while, now it's going to bite- the action has been finished and hand gesture gone back to null.
         */
        watchdog_sw.Stop();
        watchdog_thread = null;

        state = HandActionState.IDLE;
        UnityEngine.Debug.Log("ACTION EXPIRED");

        using (file = new(fileName))
        {
            file.WriteLine(":" + start_stop_counter);
            /*
             * Also store all the confidences of the detected gesture
             */
            String confidences_string = "";
            for (int cnt = 0; cnt < gesture_confidences.Count; cnt++)
            {
                try
                {
                    confidences_string += (float)gesture_confidences[cnt] + ",";
                } catch(InvalidCastException exc)
                {
                    UnityEngine.Debug.Log("ICE: gesture_confidences[" + cnt + "] == " + gesture_confidences[cnt]);
                }
            }
            file.WriteLine(confidences_string.Substring(0, confidences_string.Length - 1));
        }

        /*
         * If hand is gone, then we want to start new measurement.
         */
        startNewDetectionAnalysis();
    }

    private void startNewDetectionAnalysis()
    {
        gesture_confidences = new ArrayList();
        fileName = "C:\\Work\\Florence\\Experiments\\experiment_" + action_gesture_to_test.ToString() + "_" + DateTimeOffset.Now.ToUnixTimeSeconds() + ".txt";
        start_stop_counter = 0;
    }

    /**
     * This function allows for continuous operation- e.g. gesture detected and offset is continuously calculated from the detected place. Offset then sets the speed of the movement. This continues
     * until the hand is released.
     */
    private void evaluateGestureForContinuousOperation()
    {
        //# AE: First let's examine if we want to start an action
        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == continuous_action_gesture && state == HandActionState.IDLE)
        {
            //# Remember that we've started rotation and where we started
            state = HandActionState.ACTION_DETECTED;
            fistStartPos = GestureProvider.LeftHand.position;
        }
        else if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == continuous_action_gesture && (state == HandActionState.ACTION_DETECTED || state == HandActionState.ACTION_IN_PROGRESS))
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
        else if (GestureProvider.LeftHand == null || (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture != continuous_action_gesture))
        {
            //# Now end the process
            ros.Publish(cmd_vel_topic, new Twist()); //# tell robot to stop
            state = HandActionState.IDLE;
        }
    }

    /**
     * This function allows for continuous persistent operation- e.g. gesture detected and offset is continuously calculated from the detected place. Offset then sets the speed of the movement. This continues
     * until the hand is released. The difference between this and evaluateGestureForContinuousOperation(), which is not persistent, is that here we don't stop the movement once the hand detection
     * algorithm has decided that our gesture is finished. Very often with gestures (e.g. the fist) user is still continuing to display the previous gesture (e.g. the fist), but the algorithm detects
     * that one finger is elsewhere (a fist with raised pinky for example even though the pinky is actually not raised in the real reality) and our action stops. This one will only stop once the hand
     * disappears altogether or it is a positively different gesture.
     */
    private void evaluateGestureForContinuousPersistentOperation()
    {
        // First decide if we want to start, stop or continue an action
        if (GestureProvider.LeftHand == null || (GestureProvider.LeftHand != null && continuous_action_stop_gestures.Contains(GestureProvider.LeftHand.gesture)))
        {
            //# Now end the process if it is happening
            if (state != HandActionState.IDLE)
            {
                ros.Publish(cmd_vel_topic, new Twist()); //# tell robot to stop
            }

            state = HandActionState.IDLE;
        } else if(GestureProvider.LeftHand.gesture == continuous_action_gesture && state == HandActionState.IDLE)
        {
            //# Remember that we've started rotation and where we started
            state = HandActionState.ACTION_DETECTED;
            fistStartPos = GestureProvider.LeftHand.position;
        } else if (state == HandActionState.ACTION_DETECTED || state == HandActionState.ACTION_IN_PROGRESS)
        {
            //# Now send speed updates according to the offset
            state = HandActionState.ACTION_IN_PROGRESS;
            fistEndPos = GestureProvider.LeftHand.position;

            //# y - hand up down
            //# z - hand left right
            //# x - hand forward back
            userDraggedSidewaysDistance = fistStartPos.x - fistEndPos.x;
            userDraggedForwardDistance = fistStartPos.z - fistEndPos.z;

            //# Ok, so now we have a user's command to move the robot. We also have an indication by how much we want to move it.
            //# We can now issue a Twist command, which is essentially the offsets detected.
            continuous_move.linear.x = userDraggedForwardDistance * -2.0; //# make negative because we're driving it back to front.
            continuous_move.linear.y = 0;
            continuous_move.linear.z = 0;

            continuous_move.angular.x = 0;
            continuous_move.angular.y = 0;
            continuous_move.angular.z = userDraggedSidewaysDistance * 2.0; //# we will be rotating on Z axis at the speed requested by user. make negative because we're driving it back to front.

            UnityEngine.Debug.Log("CMove: " + continuous_move.linear.x + " " + continuous_move.angular.z);

            ros.Publish(cont_drv_topic, continuous_move);
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
        //UnityEngine.Debug.Log("TRIG ENTER");
    }

    void OnTriggerExit(Collider other)
    {
        //UnityEngine.Debug.Log("TRIG EXIT");
        //if (other.GetComponent<Rigidbody>() != target) return;
        //if (state == 1) exit = true;
    }

    public void OnStateChanged(int state)
    {
        //UnityEngine.Debug.Log("OK detected");
        /*        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Like)
                {
                    txtCoord.text = GestureProvider.LeftHand.position.x + " # " + GestureProvider.LeftHand.position.y + " # " + GestureProvider.LeftHand.position.z;
                }*/
    }

    public void OnTargetDetected()
    {
        //UnityEngine.Debug.Log("TARGET detected");
        /*        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Like)
                {
                    txtCoord.text = GestureProvider.LeftHand.position.x + " # " + GestureProvider.LeftHand.position.y + " # " + GestureProvider.LeftHand.position.z;
                }*/
    }

    public void OnTargetReleased()
    {
        //UnityEngine.Debug.Log("TARGET RELEASED");
        /*        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Like)
                {
                    txtCoord.text = GestureProvider.LeftHand.position.x + " # " + GestureProvider.LeftHand.position.y + " # " + GestureProvider.LeftHand.position.z;
                }*/
    }
}
