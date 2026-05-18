using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 遠距離ユニット用の行動スクリプトです。射程の値はBaseEntity側でユニット名から決まります。
public class RangedEntity : BaseEntity
{
    // 戦闘開始時に、行動できる状態なら最初の敵を探します。
    protected override void OnRoundStart()
    {
        if (CanAct)
            FindTarget();
    }

    // 毎フレーム、敵が射程内かどうかを見て攻撃または移動を行います。
    public void Update()
    {
        // 死亡中、ベンチ待機中、スタン中などは行動しません。
        if (!CanAct)
            return;

        // ターゲットが無効になった時や射程外になった時に狙い直します。
        RefreshTargetForCombat();

        // 敵がいなければ何もしません。
        if (!HasEnemy)
            return;

        if (IsInRange)
        {
            // 射程内なら攻撃できます。攻撃中やクールタイム中は待ちます。
            if (canAttack)
            {
                TryAttackCurrentTarget();
            }
        }
        else
        {
            // 射程外なら、攻撃できる距離まで近づきます。
            GetInRange();
        }
    }
}
