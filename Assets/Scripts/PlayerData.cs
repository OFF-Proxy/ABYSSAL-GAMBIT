using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// プレイヤーのお金、レベル、経験値を管理するクラスです。
public class PlayerData : Manager<PlayerData>
{
    // 現在の所持金です。外から直接書き換えられないようprivate setにしています。
    public int Money { get; private set; }
    // 現在のプレイヤーレベルです。盤面に出せるユニット数にも使います。
    public int Level { get; private set; } = 1;
    // 現在レベル内で溜まっている経験値です。
    public int Exp { get; private set; }
    // 現時点の最大レベルです。
    public int MaxLevel => 10;
    // 次のレベルに上がるために必要な経験値です。
    public int NextLevelExp => GetExpRequiredForNextLevel(Level);

    // UI更新が必要な時に呼ぶ通知です。
    public System.Action OnUpdate;

    // ゲーム開始時の初期値を設定します。
    private void Start()
    {
        Money = 999;
        Level = 1;
        Exp = 0;
        OnUpdate?.Invoke();
    }

    // 指定した金額を払えるか確認します。
    public bool CanAfford(int amount)
    {
        return amount <= Money;
    }

    // お金を消費し、ショップUIなどへ更新を知らせます。
    public void SpendMoney(int amount)
    {
        Money -= amount;
        OnUpdate?.Invoke();
    }

    // お金を増やし、ショップUIなどへ更新を知らせます。
    public void AddMoney(int amount)
    {
        Money += amount;
        OnUpdate?.Invoke();
    }

    // EXP購入ボタンを押せる状態か確認します。
    public bool CanBuyExp(int cost)
    {
        return Level < MaxLevel && CanAfford(cost);
    }

    // お金を払って経験値を買います。成功した時だけtrueを返します。
    public bool TryBuyExp(int expAmount, int cost)
    {
        if (!CanBuyExp(cost))
            return false;

        // SpendMoneyを使うと更新通知が2回走るため、ここでは直接引いてからAddExpで通知します。
        Money -= cost;
        AddExp(expAmount);
        return true;
    }

    // 経験値を加算し、必要量を超えたらレベルアップします。
    public void AddExp(int amount)
    {
        if (Level >= MaxLevel)
        {
            // 最大レベルでは経験値表示を0に固定します。
            Exp = 0;
            OnUpdate?.Invoke();
            return;
        }

        Exp += amount;
        // 余った経験値は次のレベルへ持ち越します。
        while (Level < MaxLevel && Exp >= NextLevelExp)
        {
            Exp -= NextLevelExp;
            Level++;
        }

        // 最大レベルに到達したら、次レベルが無いので経験値を0にします。
        if (Level >= MaxLevel)
            Exp = 0;

        OnUpdate?.Invoke();
    }

    // レベルごとの必要経験値表です。現在レベルから次へ上がる値を返します。
    private int GetExpRequiredForNextLevel(int level)
    {
        switch (level)
        {
            case 1:
                return 2;
            case 2:
                return 4;
            case 3:
                return 6;
            case 4:
                return 10;
            case 5:
                return 20;
            case 6:
                return 36;
            case 7:
                return 60;
            case 8:
                return 68;
            case 9:
                return 68;
            default:
                return 0;
        }
    }
}
