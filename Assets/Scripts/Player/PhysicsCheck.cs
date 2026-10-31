using System.Collections;
using System.Collections.Generic;
//using System.ComponentModel.Design;
using UnityEngine;

public class PhysicsCheck : MonoBehaviour
{
    public bool isGround;
    public LayerMask groundLayer;
    public Vector2 bottomOffset;
    public float checkRaduis;
    public void Update()
    {
        Check();
    }
    public void Check()
    {
        isGround=Physics2D.OverlapCircle((Vector2)transform.position+bottomOffset,checkRaduis,groundLayer);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere((Vector2)transform.position+bottomOffset,checkRaduis);    
    }
}
