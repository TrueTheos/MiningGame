using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Web : CustomBuilding
{
    private void Start()
    {
        transform.Rotate(0, 0, new List<int>() { 0,90,180,270}.Random());
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement.Instance.InWeb = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement.Instance.InWeb = false;
        }
    }
}
