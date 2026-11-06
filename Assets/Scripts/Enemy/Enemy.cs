using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [HideInInspector] public Animator anim;
    Rigidbody2D rb;
    [HideInInspector] public PhysicsCheck physicsCheck;
    public Transform attacker;
    [Header("基本参数")]
    public float normalSpeed;
    public float chaseSpeed;
    public float currentSpeed;
    public float hurtForce;
    [HideInInspector] public Vector3 faceDir;
    [Header("计时器")]
    public float waitTime;
    public float waitTimeCounter;
    public bool wait;
    [HideInInspector] public bool isHurt;
    [HideInInspector] public bool isDead;

    private BaseState currentState;
    protected BaseState patrolState;
    protected BaseState chaseState;
    protected virtual void Awake()
    {
        anim=GetComponent<Animator>();
        rb=GetComponent<Rigidbody2D>();
        physicsCheck=GetComponent<PhysicsCheck>();
        currentSpeed=normalSpeed;
        waitTimeCounter=waitTime;
    }
    private void OnEnable()
    {
        currentState=patrolState;
        currentState.OnEnter(this);
    }
    private void Update()
    {
        faceDir=new Vector3(transform.localScale.x,0,0);
        currentState.LogicUpdate();
        TimeCounter();
    }
    private void FixedUpdate()
    {
        if(isHurt||isDead||wait) return;
        Move();
        anim.SetBool("run",true);
        currentState.PhysicsUpdate();
    }
    private void OnDisable()
    {
        currentState.OnExit();
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
