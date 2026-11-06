using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlimePatrolState : BaseState
{
    public override void OnEnter(Enemy enemy)
    {
        currentEnemy=enemy;
    }
    public override void LogicUpdate()
    {
        if((currentEnemy.physicsCheck.touchLeftWall&&currentEnemy.faceDir.x<0)||(currentEnemy.physicsCheck.touchRightWall&&currentEnemy.faceDir.x>0))
        {
            currentEnemy.wait=true;
            currentEnemy.anim.SetBool("run",false);
        }
        if(!currentEnemy.physicsCheck.isGround) 
        {
            currentEnemy.wait=true;
            currentEnemy.anim.SetBool("run",false);
        }
    }
    public override void PhysicsUpdate()
    {
        
    }
    public override void OnExit()
    {
        currentEnemy.anim.SetBool("run",false);
    }

}
