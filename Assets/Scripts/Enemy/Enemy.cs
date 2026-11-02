using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    protected Animator anim;
    Rigidbody2D rb;
    PhysicsCheck physicsCheck;
    public Transform attacker;
    [Header("基本参数")]
    public float normalSpeed;
    public float chaseSpeed;
    public float currentSpeed;
    public float hurtForce;
    public Vector3 faceDir;
    [Header("计时器")]
    public float waitTime;
    public float waitTimeCounter;
    public bool wait;
    [HideInInspector] public bool isHurt;
    [HideInInspector] public bool isDead;
    private void Awake()
    {
        anim=GetComponent<Animator>();
        rb=GetComponent<Rigidbody2D>();
        physicsCheck=GetComponent<PhysicsCheck>();
        currentSpeed=normalSpeed;
        waitTimeCounter=waitTime;
    }

    private void Update()
    {
        faceDir=new Vector3(transform.localScale.x,0,0);
        if((physicsCheck.touchLeftWall&&faceDir.x<0)||(physicsCheck.touchRightWall&&faceDir.x>0))
        {
            wait=true;
            anim.SetBool("run",false);
        }
        if(!physicsCheck.isGround) 
        {
            wait=true;
            anim.SetBool("run",false);
        }
        TimeCounter();
    }
    private void FixedUpdate()
    {
        if(wait)
        {
            rb.velocity=new Vector2(0,rb.velocity.y);
            return;
        }
        if(isHurt||isDead) return;
        Move();
    }
    public virtual void Move()
    {
        rb.velocity=new Vector2(currentSpeed*faceDir.x,rb.velocity.y);
    }
    public void TimeCounter()
    {
        if(wait)
        {
            waitTimeCounter-=Time.deltaTime;
            if(waitTimeCounter<=0)
            {
                wait=false;
                waitTimeCounter=waitTime;
                transform.localScale=new Vector3(-faceDir.x,1,1);
                physicsCheck.bottomOffset.x=-physicsCheck.bottomOffset.x;
            }
        }
    }
    public void OnTakeDamage(Transform attackTrans)
    {
        attacker=attackTrans;
        if(attacker.transform.position.x-transform.position.x>0) 
        {
            physicsCheck.bottomOffset.x=(faceDir.x==1)?physicsCheck.bottomOffset.x:-physicsCheck.bottomOffset.x;
            transform.localScale=new Vector3(1,1,1);
        }
        if(attacker.transform.position.x-transform.position.x<0) 
        {
            physicsCheck.bottomOffset.x=(faceDir.x==-1)?physicsCheck.bottomOffset.x:-physicsCheck.bottomOffset.x;
            transform.localScale=new Vector3(-1,1,1);
        }
        isHurt=true;
        anim.SetTrigger("hurt");
        Vector2 dir=new Vector2(transform.position.x-attacker.transform.position.x,0).normalized;
        StartCoroutine(OnHurt(dir));
    }
    private IEnumerator OnHurt(Vector2 dir)
    {
        rb.AddForce(dir*hurtForce,ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.5f);
        isHurt=false;
    }
    public void OnDie()
    {
        gameObject.layer=2;
        anim.SetBool("dead",true);
        isDead=true;
    }
    public void DestroyAfterAnimation()
    {
        Destroy(this.gameObject);
    }
}
