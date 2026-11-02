using System;
using System.Collections;
using System.Collections.Generic;
//using UnityEditor;
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
    private void Awake()
    {
        inputControl=new PlayerInputControl();
        rb=GetComponent<Rigidbody2D>();
        coll=GetComponent<CapsuleCollider2D>();
        physicsCheck=GetComponent<PhysicsCheck>();
        playerAnimation=GetComponent<PlayerAnimation>();
        inputControl.Gameplay.Jump.started+=Jump;
        inputControl.Gameplay.Attack.started+=PlayerAttack;
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
    }
    private void FixedUpdate()
    {
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
        rb.velocity=Vector2.zero;
        Vector2 dir=new Vector2((transform.position.x-attacker.position.x),0).normalized;
        rb.AddForce(dir*hurtForce,ForceMode2D.Impulse);
    }
    public void PlayerDead()
    {
        isDead=true;
        inputControl.Gameplay.Disable();
    }
    private void CheckState()
    {
        coll.sharedMaterial=physicsCheck.isGround?normal:wall;
    }
}
