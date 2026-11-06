using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss : Enemy
{
    [Header("Boss 参数")]
    public float detectRange;
    public float attackRange;
    public float heightLimit;
    public float attackCooldown;
    public float jumpSpeed;
    public float jumpForce;
    public float jumpRange;
    public float landTime;
    [Header("引用")]
    public GameObject attackArea;
    public Transform player;
    [HideInInspector] public Rigidbody2D bossRb;
    [HideInInspector] public float attackTimer;

    protected override void Awake()
    {
        base.Awake();
        bossRb=GetComponent<Rigidbody2D>();
        patrolState=new BossIdleState();
        attackArea.SetActive(false);
    }
    private void Start()
    {
        attackTimer=attackCooldown;
        GetComponent<Character>().OnDie.AddListener(OnBossDie);
    }
    public override void Move()
    {
    }
    public float DistanceToPlayer()
    {
        return Mathf.Abs(player.position.x-transform.position.x);
    }
    public float HeightToPlayer()
    {
        return player.position.y-transform.position.y;
    }
    public void FaceToPlayer()
    {
        if(player.position.x>transform.position.x)
        {
            transform.localScale=new Vector3(-1.6f,1.6f,1);
        }
        else
        {
            transform.localScale=new Vector3(1.6f,1.6f,1);
        }
    }
    public void Stop()
    {
        bossRb.velocity=new Vector2(0f,bossRb.velocity.y);
    }
    public void StartJump()
    {
        FaceToPlayer();
        anim.Play("BossAir",0,0f);
        bossRb.velocity=new Vector2(Mathf.Sign(faceDir.x)*jumpSpeed,jumpForce);
    }
    public void PlayGround()
    {
        anim.Play("BossGround",0,0f);
    }
    public void OpenAttackArea()
    {
        attackArea.SetActive(true);
    }
    public void CloseAttackArea()
    {
        attackArea.SetActive(false);
    }
    private void OnBossDie()
    {
        Stop();
        anim.Play("BossDead",0,0f);
        attackArea.SetActive(false);
        Attack bodyAttack=GetComponent<Attack>();
        bodyAttack.enabled=false;
        StartCoroutine(DestroyLater());
    }
    private IEnumerator DestroyLater()
    {
        yield return new WaitForSecondsRealtime(1f);
        DestroyAfterAnimation();
    }
}

public class BossIdleState : BaseState
{
    private Boss boss;
    public override void OnEnter(Enemy enemy)
    {
        currentEnemy=enemy;
        boss=enemy as Boss;
        boss.Stop();
        currentEnemy.anim.Play("BossIdle",0,0f);
    }
    public override void LogicUpdate()
    {
        if(boss.DistanceToPlayer()<=boss.detectRange)
        {
            boss.SwitchState(new BossJumpState());
        }
    }
    public override void PhysicsUpdate() { }
    public override void OnExit() { }
}

public class BossJumpState : BaseState
{
    private Boss boss;
    private float timer;
    private bool landed;
    public override void OnEnter(Enemy enemy)
    {
        currentEnemy=enemy;
        boss=enemy as Boss;
        timer=0f;
        landed=false;
        boss.StartJump();
    }
    public override void LogicUpdate()
    {
        currentEnemy.anim.SetFloat("velocityY",boss.bossRb.velocity.y);
        timer+=Time.deltaTime;
        if(!landed)
        {
            if(timer>0.25f&&currentEnemy.physicsCheck.isGround)
            {
                landed=true;
                timer=0f;
                boss.Stop();
                boss.PlayGround();
            }
            return;
        }
        if(timer>=boss.landTime)
        {
            boss.attackTimer=boss.attackCooldown*0.5f;
            boss.SwitchState(new BossChaseState());
        }
    }
    public override void PhysicsUpdate() { }
    public override void OnExit() { }
}

public class BossChaseState : BaseState
{
    private Boss boss;
    public override void OnEnter(Enemy enemy)
    {
        currentEnemy=enemy;
        boss=enemy as Boss;
        currentEnemy.anim.Play("BossRun",0,0f);
    }
    public override void LogicUpdate()
    {
        if(currentEnemy.isHurt)
        {
            return;
        }
        boss.FaceToPlayer();
        boss.attackTimer-=Time.deltaTime;
        if(boss.attackTimer>0)
        {
            return;
        }
        float dis=boss.DistanceToPlayer();
        if(dis<=boss.attackRange&&Mathf.Abs(boss.HeightToPlayer())<=boss.heightLimit)
        {
            boss.SwitchState(new BossAttackState());
            return;
        }
        if(dis<=boss.detectRange&&dis>boss.jumpRange||Mathf.Abs(boss.HeightToPlayer())>boss.heightLimit)
        {
            boss.SwitchState(new BossJumpState());
        }
    }
    public override void PhysicsUpdate()
    {
        float dir=Mathf.Sign(boss.faceDir.x);
        boss.bossRb.velocity=new Vector2(dir*boss.chaseSpeed,boss.bossRb.velocity.y);
    }
    public override void OnExit() { }
}

public class BossAttackState : BaseState
{
    private Boss boss;
    private float timer;
    public override void OnEnter(Enemy enemy)
    {
        currentEnemy=enemy;
        boss=enemy as Boss;
        timer=0f;
        boss.FaceToPlayer();
        boss.Stop();
        currentEnemy.anim.Play("BossAttack",0,0f);
    }
    public override void LogicUpdate()
    {
        timer+=Time.deltaTime;
        if(currentEnemy.isHurt)
        {
            boss.CloseAttackArea();
        }
        if(timer>=0.65f)
        {
            boss.attackTimer=boss.attackCooldown;
            boss.SwitchState(new BossChaseState());
        }
    }
    public override void PhysicsUpdate()
    {
        boss.Stop();
    }
    public override void OnExit()
    {
        boss.CloseAttackArea();
    }
}