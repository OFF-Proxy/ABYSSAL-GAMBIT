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

    // --- 経済（TFT式の収入ループ）。インスペクタで調整できます。 ---
    [Header("Economy")]
    // 新しい挑戦を始める時の所持金です。
    public int startingMoney = 4;
    // ウェーブクリアごとの基本収入です。
    public int baseRoundIncome = 5;
    // 利子は所持金がこの額ごとに+1されます（10なら10ゴールドごとに+1）。
    public int interestPerGold = 10;
    // 利子の上限です。
    public int interestCap = 5;

    // 現在の所持金から得られる利子額です（次の収入で加算されます）。
    public int CurrentInterest => Mathf.Clamp(Money / Mathf.Max(1, interestPerGold), 0, interestCap);
    // 次のウェーブクリアで得られる収入の予測値です。
    public int PreviewNextIncome => baseRoundIncome + CurrentInterest;

    // ゲーム開始時の初期値を設定します。
    private void Start()
    {
        ResetEconomyForNewRun();
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

    // 既存コード向けの入口です。通常の4EXP購入として扱います。
    public bool TryBuyExp(int expAmount, int cost)
    {
        return TryBuyExp(expAmount, cost, false);
    }

    // お金を払って経験値を買います。bulkToNextLevelがtrueなら次レベル到達分だけまとめ買いします。
    public bool TryBuyExp(int expAmount, int cost, bool bulkToNextLevel)
    {
        int actualExpAmount = Mathf.Max(1, expAmount);
        int actualCost = Mathf.Max(1, cost);

        if (bulkToNextLevel && Level < MaxLevel)
        {
            // 一括購入でも4EXP/4コイン単位は崩さず、必要分を少し超える場合もそのまま購入します。
            int purchaseCountToNextLevel = GetExpPurchaseCountToNextLevel(actualExpAmount);
            int levelUpCost = purchaseCountToNextLevel * actualCost;
            if (CanAfford(levelUpCost))
            {
                actualExpAmount *= purchaseCountToNextLevel;
                actualCost = levelUpCost;
            }
        }

        if (!CanBuyExp(actualCost))
            return false;

        // SpendMoneyを使うと更新通知が2回走るため、ここでは直接引いてからAddExpで通知します。
        Money -= actualCost;
        AddExp(actualExpAmount);
        return true;
    }

    // 次のレベルへ届くまでに、EXP購入ボタンを何回押す必要があるかを返します。
    public int GetExpPurchaseCountToNextLevel(int expAmount)
    {
        if (Level >= MaxLevel)
            return 0;

        int actualExpAmount = Mathf.Max(1, expAmount);
        int remainingExp = Mathf.Max(0, NextLevelExp - Exp);
        return Mathf.Max(1, Mathf.CeilToInt((float)remainingExp / actualExpAmount));
    }

    // 一括レベルアップに必要なコイン数を返します。
    public int GetBulkExpCostToNextLevel(int expAmount, int cost)
    {
        if (Level >= MaxLevel)
            return 0;

        return GetExpPurchaseCountToNextLevel(expAmount) * Mathf.Max(1, cost);
    }

    // 一括レベルアップを買えるか確認します。
    public bool CanBulkBuyExpToNextLevel(int expAmount, int cost)
    {
        return Level < MaxLevel && CanAfford(GetBulkExpCostToNextLevel(expAmount, cost));
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

    // ウェーブクリア時の収入（基本＋利子）を付与し、内訳を返します。PvEなので連勝ボーナスはありません。
    public RoundIncome GrantWaveClearIncome()
    {
        int interest = CurrentInterest; // 付与前の所持金で利子を計算します（TFT準拠）。
        int total = baseRoundIncome + interest;
        Money += total;
        OnUpdate?.Invoke();
        return new RoundIncome(baseRoundIncome, interest);
    }

    // 新しい挑戦の開始時に、所持金・レベル・経験値をリセットします。
    public void ResetEconomyForNewRun()
    {
        Money = startingMoney;
        Level = 1;
        Exp = 0;
        OnUpdate?.Invoke();
    }

    // ウェーブクリア収入の内訳です。UI表示やログに使います。
    public readonly struct RoundIncome
    {
        public readonly int Base;
        public readonly int Interest;
        public int Total => Base + Interest;

        public RoundIncome(int baseIncome, int interest)
        {
            Base = baseIncome;
            Interest = interest;
        }
    }
}
