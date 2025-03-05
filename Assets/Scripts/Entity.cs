using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    [SerializeField] protected int _maxHealth;
    public int CurrentHealth;

    public void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        OnTakeDamage();

        if (CurrentHealth <= 0) Die();
    }

    public void Die()
    {
        OnDie();
    }

    public virtual void OnTakeDamage() { }

    public virtual void OnDie() { }
}
