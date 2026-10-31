using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public int damage;
    public float attackRate;
    public float attackRange;
    private void OnTriggerStay2D(Collider2D other)
    {
        other.GetComponent<Character>()?.TakeDamage(this);
    }
}
