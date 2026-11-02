using System.Collections;
using System.Collections.Generic;
using System.Drawing.Text;

//using System.ComponentModel.Design;
using UnityEngine;

public class PhysicsCheck : MonoBehaviour
{
    private CapsuleCollider2D coll;
    [Header("检测参数")]
    public bool manual;
    public LayerMask groundLayer;
    public Vector2 bottomOffset;
    public float checkRaduis;
    public Vector2 leftOffset;
    public Vector2 rightOffset;
    [HideInInspector] public bool isGround;
    [HideInInspector] public bool touchLeftWall;
    [HideInInspector] public bool touchRightWall;
    private void Awake()
    {
        coll=GetComponent<CapsuleCollider2D>();
        if(!manual)
        {
            rightOffset=new Vector2(coll.bounds.size.x/2 + coll.offset.x,coll.offset.y);
            leftOffset=new Vector2(-(coll.bounds.size.x/2) + coll.offset.x,coll.offset.y);
        }
    }
    public void Update()
    {
        Check();
    }
    public void Check()
    {
        isGround=Physics2D.OverlapCircle((Vector2)transform.position+bottomOffset,checkRaduis,groundLayer);
        touchLeftWall=Physics2D.OverlapCircle((Vector2)transform.position+leftOffset,checkRaduis,groundLayer);
        touchRightWall=Physics2D.OverlapCircle((Vector2)transform.position+rightOffset,checkRaduis,groundLayer);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere((Vector2)transform.position+bottomOffset,checkRaduis);
        Gizmos.DrawWireSphere((Vector2)transform.position+leftOffset,checkRaduis);  
        Gizmos.DrawWireSphere((Vector2)transform.position+rightOffset,checkRaduis);      
    }
}
