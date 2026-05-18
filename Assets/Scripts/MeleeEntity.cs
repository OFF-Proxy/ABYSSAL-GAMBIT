using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 近接ユニット用の行動スクリプトです。基本性能や攻撃処理はBaseEntity側にあります。
public class MeleeEntity : BaseEntity
{
    // 戦闘開始時に、行動できる状態なら最初の敵を探します。
    protected override void OnRoundStart()
    {
        if (CanAct)
            FindTarget();
    }

    // 毎フレーム、敵を追いかけるか攻撃するかを判断します。
    public void Update()
    {
        // 死亡中、ベンチ待機中、スタン中などは行動しません。
        if (!CanAct)
            return;

        // 今狙っている敵が倒れたり射程外になった時に、必要なら狙い直します。
        RefreshTargetForCombat();

        // 敵がいなければ何もしません。
        if (!HasEnemy)
            return;

        if(IsInRange)
        {
            // 射程内なら、攻撃クールタイムが終わっている時だけ攻撃します。
            if(canAttack)
            {
                TryAttackCurrentTarget();
            }
        }
        else
        {
            // 射程外なら、次のマスへ進んで敵に近づきます。
            GetInRange();
        }
    }
}
