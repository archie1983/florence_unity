using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ViveHandTracking;

public class GestureAction : MonoBehaviour
{
    private int state = 0;
    Vector3 fistStartPos, fistEndPos;
    //private bool exit = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Fist && state == 0)
        {
            state = 1;
            fistStartPos = GestureProvider.LeftHand.position;
            /*
            txtCoord.text = string.Format("{0:0.##}", GestureProvider.LeftHand.position.x * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.y * SCALE)
                + " # " + string.Format("{0:0.##}", GestureProvider.LeftHand.position.z * SCALE)
                + "\n" + GestureProvider.LeftHand.rotation.ToString();
            */
        } else if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture != GestureType.Fist && state == 1)
        {
            state = 0;
            fistEndPos = GestureProvider.LeftHand.position;
            Debug.Log("Drag: " + (fistStartPos.x - fistEndPos.x));
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("TRIG ENTER");
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("TRIG EXIT");
        //if (other.GetComponent<Rigidbody>() != target) return;
        //if (state == 1) exit = true;
    }

    public void OnStateChanged(int state)
    {
        Debug.Log("OK detected");
        /*        if (GestureProvider.LeftHand != null && GestureProvider.LeftHand.gesture == GestureType.Like)
                {
                    txtCoord.text = GestureProvider.LeftHand.position.x + " # " + GestureProvider.LeftHand.position.y + " # " + GestureProvider.LeftHand.position.z;
                }*/

        this.state = state;
    }
}
