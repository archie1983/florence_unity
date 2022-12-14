//Do not edit! This file was generated by Unity-ROS MessageGeneration.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.ROS
{
    [Serializable]
    public class PanTiltAngleMsg : Message
    {
        public const string k_RosMessageName = "ROS_msgs/PanTiltAngle";
        public override string RosMessageName => k_RosMessageName;

        //  Angles for dynamixel pan-tilt
        //  Angles in degrees, range -180 -> 180
        public double pan_angle;
        public double tilt_angle;

        public PanTiltAngleMsg()
        {
            this.pan_angle = 0.0;
            this.tilt_angle = 0.0;
        }

        public PanTiltAngleMsg(double pan_angle, double tilt_angle)
        {
            this.pan_angle = pan_angle;
            this.tilt_angle = tilt_angle;
        }

        public static PanTiltAngleMsg Deserialize(MessageDeserializer deserializer) => new PanTiltAngleMsg(deserializer);

        private PanTiltAngleMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out this.pan_angle);
            deserializer.Read(out this.tilt_angle);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.pan_angle);
            serializer.Write(this.tilt_angle);
        }

        public override string ToString()
        {
            return "PanTiltAngleMsg: " +
            "\npan_angle: " + pan_angle.ToString() +
            "\ntilt_angle: " + tilt_angle.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize);
        }
    }
}
