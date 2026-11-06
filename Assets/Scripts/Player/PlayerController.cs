using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerInputControl inputControl;
    public Vector2 inputDirection;
    public CapsuleCollider2D coll;
    private Rigidbody2D rb;
    private PhysicsCheck physicsCheck;
    private PlayerAnimation playerAnimation;
    [Header("基本参数")]
    public float speed;
    public float jumpForce;
    [HideInInspector] public bool isHurt;
    public float hurtForce;
    [HideInInspector] public bool isDead;
    [HideInInspector] public bool isAttack;
    [Header("材质")]
    public PhysicsMaterial2D wall;
    public PhysicsMaterial2D normal;
    [Header("冲刺")]
    public bool canDash;
    public float dashSpeed=14;
    public float dashTime=0.22f;
    public float dashCooldown=0.4f;
    private bool isDashing;
    private float dashTimer;
    private float cooldownTimer;
    private int airDashCount;
    private float gravityScale;
    [Header("冲刺残影")]
    public Color ghostColor=new Color(0.6f,0.9f,1f,0.45f);
    public float ghostInterval=0.04f;
    private float ghostTimer;
    [Header("死亡重生")]
    public float deathY=-20;
    public float respawnDelay=1.5f;
    private bool respawning;
    private void Awake()
    {
        inputControl=new PlayerInputControl();
        rb=GetComponent<Rigidbody2D>();
        coll=GetComponent<CapsuleCollider2D>();
        physicsCheck=GetComponent<PhysicsCheck>();
        playerAnimation=GetComponent<PlayerAnimation>();
        inputControl.Gameplay.Jump.started+=Jump;
        inputControl.Gameplay.Attack.started+=PlayerAttack;
        gravityScale=rb.gravityScale;
        inputControl.Gameplay.Dash.started+=Dash;
    }
    private void OnEnable()
    {
        inputControl.Enable();
    }
    private void OnDisable()
    {
        inputControl.Disable();
    }
    private void Update()
    {
        inputDirection=inputControl.Gameplay.Move.ReadValue<Vector2>();
        CheckState();
        if(!isDead&&transform.position.y<deathY)
        {
            PlayerDead();
        }
    }
    private void FixedUpdate()
    {
        if(isDead) return;
        cooldownTimer-=Time.fixedDeltaTime;
        if(physicsCheck.isGround&&!isDashing)
        {
            airDashCount=0;
        }
        if(isDashing)
        {
            DashMove();
            return;
        }
        if(!isHurt&&!isAttack)
            Move();
    }
    public void Move()
    {
        rb.velocity=new Vector2(inputDirection.x*speed,rb.velocity.y);
        int faceDir=(int)transform.localScale.x;
        if(inputDirection.x>0) faceDir=1;
        if(inputDirection.x<0) faceDir=-1;
        transform.localScale=new Vector3(faceDir,transform.localScale.y,transform.localScale.z);
    }
    private void Jump(InputAction.CallbackContext obj)
    {
        if(isDashing) return;
        if(physicsCheck.isGround)
            rb.AddForce(transform.up*jumpForce,ForceMode2D.Impulse);
    }
    private void PlayerAttack(InputAction.CallbackContext context)
    {
        playerAnimation.PlayAttack();
        isAttack=true;
    }
    public void GetHurt(Transform attacker)
    {
        isHurt=true;
        StopDash();
        rb.velocity=Vector2.zero;
        Vector2 dir=new Vector2((transform.position.x-attacker.position.x),0).normalized;
        rb.AddForce(dir*hurtForce,ForceMode2D.Impulse);
    }
    public void PlayerDead()
    {
        if(isDead) return;
        isDead=true;
        StopDash();
        inputControl.Gameplay.Disable();
        StartCoroutine(RespawnAfterDelay());
    }
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        GameManager.Instance.Respawn();
    }
    private void CheckState()
    {
        coll.sharedMaterial=physicsCheck.isGround?normal:wall;
    }
    public void UnlockDash()
    {
        canDash=true;
    }
    private void Dash(InputAction.CallbackContext context)
    {
        if(!canDash) return;
        if(isDashing) return;
        if(cooldownTimer>0) return;
        if(!physicsCheck.isGround&&airDashCount>=1) return;
        isDashing=true;
        dashTimer=dashTime;
        cooldownTimer=dashCooldown;
        ghostTimer=0;
        rb.gravityScale=0;
        if(!physicsCheck.isGround)
            airDashCount++;
    }
    private void DashMove()
    {
        int faceDir=transform.localScale.x>=0?1:-1;
        rb.velocity=new Vector2(faceDir*dashSpeed,0);
        dashTimer-=Time.fixedDeltaTime;
        ghostTimer-=Time.fixedDeltaTime;
        if(ghostTimer<=0)
        {
            ghostTimer=ghostInterval;
            SpawnGhost();
        }
        if(dashTimer<=0)
        {
            StopDash();
        }
    }
    private void StopDash()
    {
        isDashing=false;
        rb.gravityScale=gravityScale;
    }
    private void SpawnGhost()
    {
        SpriteRenderer self=GetComponent<SpriteRenderer>();
        GameObject ghost=new GameObject("DashGhost");
        ghost.transform.position=transform.position;
        ghost.transform.localScale=transform.localScale;

        SpriteRenderer sr=ghost.AddComponent<SpriteRenderer>();
        sr.sprite=self.sprite;
        sr.flipX=self.flipX;
        sr.flipY=self.flipY;
        sr.sortingLayerName=self.sortingLayerName;
        sr.sortingOrder=self.sortingOrder-1;
        sr.color=ghostColor;

        ghost.AddComponent<DashGhost>();
    }
}
