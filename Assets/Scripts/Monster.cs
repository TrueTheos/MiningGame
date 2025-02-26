using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private int _maxHealth;
    public int CurrentHealth;

    private void Awake()
    {
        CurrentHealth = _maxHealth;
    }

    [ContextMenu("Test take damage")]
    public void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        OnTakeDamage();

        if (CurrentHealth <= 0) Die();
    }

    public virtual void OnTakeDamage() { }

    public virtual void OnDie() { }

    public void Die()
    {
        OnDie();

        Destroy(gameObject);
    }
}
