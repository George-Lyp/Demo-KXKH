using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public class Slime : Enemy
{
    protected override void Awake()
    {
        base.Awake();
        patrolState=new SlimePatrolState();
    }
}
