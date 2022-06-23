using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectHand : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject Skeleton = null;
    public GameObject OBJmodel = null;
    private bool usingSkeleton = false;
    void Start()
    {
        Skeleton.SetActive(usingSkeleton);
        OBJmodel.SetActive(!usingSkeleton);
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            usingSkeleton = !usingSkeleton;
            Skeleton.SetActive(usingSkeleton);
            OBJmodel.SetActive(!usingSkeleton);
        }
    }
}
