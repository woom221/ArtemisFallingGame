using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrigidShotSFX : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
      FMODUnity.RuntimeManager.PlayOneShot("event:/Actions/IceGrenade", GetComponent<Transform>().position);  
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}