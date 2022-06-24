using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using UnityEngine.UI;
using CompressedImage = RosMessageTypes.Sensor.CompressedImageMsg;

public class CameraSubscriber : MonoBehaviour
{
    public ROSConnection ros;
    private Texture2D texture2D;
    public RawImage rimg;
    private byte[] imageData;
    private bool isMessageReceived;
    public string topicName = "";

    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<CompressedImage>(topicName, ShowImage);
        texture2D = new Texture2D(1, 1);
    }

    private void Update()
    {
        if (isMessageReceived)
            ProcessImage();
    }

    void ShowImage(CompressedImage ImgMsg)
    {
        imageData = ImgMsg.data;
        isMessageReceived = true;
        Debug.Log(ImgMsg.format);
        //Debug.Log(ImgMsg.encoding);
        Debug.Log(imageData.Length);

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
}
