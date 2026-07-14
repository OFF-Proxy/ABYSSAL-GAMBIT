using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 近接ユニット用の行動スクリプトです。基本性能や攻撃処理はBaseEntity側にあります。
public class MeleeEntity : BaseEntity
{
    // 戦闘開始時に、行動できる状態なら最初の敵を探します。
    protected override void OnRoundStart()
    {
        base.OnRoundStart();

        if (CanAct && !IsDebugTrainingDummy)
            FindTarget();
    }

    // 毎フレーム、敵を追いかけるか攻撃するかを判断します。
    public void Update()
    {
        // R2-coremode: コアは移動も攻撃もしない（被弾専用の拠点）。
        if (IsCore)
            return;

        // 死亡中、ベンチ待機中、スタン中などは行動しません。
        if (!CanAct)
            return;

        if (TryHandleDebugTrainingDummy())
            return;

        // 攻撃モーション中は移動も再ターゲットもしません。モーションを最後まで再生し終えてから次の行動へ移ります。
        // （攻撃コルーチンは開始時のターゲットを保持しているため、途中で敵が射程外へ離れてもこの攻撃は当たります。）
        if (IsAttackMotionPlaying)
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
