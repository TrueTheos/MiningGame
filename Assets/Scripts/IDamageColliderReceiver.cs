using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageColliderReceiver
{
    public void Receive(GameObject collider);
}
