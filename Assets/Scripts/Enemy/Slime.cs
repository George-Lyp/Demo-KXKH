using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public class Slime : Enemy
{
    public override void Move()
    {
        base.Move();
        anim.SetBool("run",true);
    }
}
