using System.Collections;
using UnityEngine;

public abstract class Item : MonoBehaviour
{
    public string Name;
    public SpriteRenderer SpriteRend;

    public int MaxStack = 1;

    protected Transform _hand => Player.Instance.Hand;

    public virtual void Holding() { }
    public virtual void EndUse() { }
    public virtual void UseOnce() { }

    public bool EqualsType(Item right)
    {
        return Name == right.Name;
    }

    public bool EqualsInstance(Item item)
    {
        return item.GetInstanceID() == GetInstanceID();
    }

    /*public static bool operator ==(Item left, Item right)
    {
        if (object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null))
            return true;
        if (object.ReferenceEquals(left, null) || object.ReferenceEquals(right, null))
            return false;
        return left.Name == right.Name;
    }

    public static bool operator !=(Item left, Item right)
    {
        return !(left == right);
    }

    public override bool Equals(object obj)
    {
        if (obj is Item other)
            return this == other;
        return false;
    }

    public override int GetHashCode()
    {
        return Name?.GetHashCode() ?? 0;
    }*/
}
