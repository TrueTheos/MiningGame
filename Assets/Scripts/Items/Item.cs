using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Item : MonoBehaviour
{
    public virtual void Holding() { }
    public virtual void EndUse() { }
    public virtual void UseOnce() { }
}
