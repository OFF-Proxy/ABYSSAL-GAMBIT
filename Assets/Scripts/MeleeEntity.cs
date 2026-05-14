using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeEntity : BaseEntity
{
    protected override void OnRoundStart()
    {
        if (CanAct)
            FindTarget();
    }

    public void Update()
    {
        if (!CanAct)
            return;

        RefreshTargetForCombat();

        if (!HasEnemy)
            return;

        if(IsInRange)
        {
            //In range for attack!
            if(canAttack)
            {
                Attack();
                currentTarget.TakeDamage(baseDamage);
            }
        }
        else
        {
            GetInRange();
        }
    }
}
