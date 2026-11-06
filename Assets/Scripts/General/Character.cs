using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class Character : MonoBehaviour
{
    [Header("基本属性")]
    public float maxHealth;
    public float currentHealth;

    [Header("受伤无敌")]
    public float invulnerableDuration;
    
    public float invulnerableCount; 
    [HideInInspector] public bool invulnerable;
    private bool dead;
    public UnityEvent<Transform>OnTakeDamage;
    public UnityEvent OnDie;
    private void Update()
    {
        if(invulnerable)
        {
            invulnerableCount-=Time.deltaTime;
            if(invulnerableCount<=0)
            {
                invulnerable=false;
            }
        }
        if(!dead&&currentHealth<=0)
        {
            Die();
        }
    }
    private void Start()
    {
        currentHealth=maxHealth;
    }
    public void TakeDamage(Attack attacker)
    {
        if(dead) return;
        if(invulnerable) return;
        currentHealth-=attacker.damage;
        if(currentHealth<=0)
        {
            currentHealth=0;
            Die();
            return;
        }
        TriggerInvulnerable();
        OnTakeDamage?.Invoke(attacker.transform);
    }
    public void Heal(float amount)
    {
        if(currentHealth<=0) return;

        currentHealth=Mathf.Min(currentHealth+amount,maxHealth);
    }
    public void HealToMax()
    {
        if(currentHealth<=0) return;

        currentHealth=maxHealth;
    }
    private void TriggerInvulnerable()
    {
        if(!invulnerable)
        {
            invulnerable=true;
            invulnerableCount=invulnerableDuration;
        }
    }
    private void Die()
    {
        if(dead) return;
        dead=true;
        OnDie?.Invoke();
    }
}

