using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerData : Manager<PlayerData>
{
    public int Money { get; private set; }
    public int Level { get; private set; } = 1;
    public int Exp { get; private set; }
    public int MaxLevel => 10;
    public int NextLevelExp => GetExpRequiredForNextLevel(Level);

    public System.Action OnUpdate;

    private void Start()
    {
        Money = 250;
        Level = 1;
        Exp = 0;
        OnUpdate?.Invoke();
    }

    public bool CanAfford(int amount)
    {
        return amount <= Money;
    }

    public void SpendMoney(int amount)
    {
        Money -= amount;
        OnUpdate?.Invoke();
    }

    public void AddMoney(int amount)
    {
        Money += amount;
        OnUpdate?.Invoke();
    }

    public bool CanBuyExp(int cost)
    {
        return Level < MaxLevel && CanAfford(cost);
    }

    public bool TryBuyExp(int expAmount, int cost)
    {
        if (!CanBuyExp(cost))
            return false;

        Money -= cost;
        AddExp(expAmount);
        return true;
    }

    public void AddExp(int amount)
    {
        if (Level >= MaxLevel)
        {
            Exp = 0;
            OnUpdate?.Invoke();
            return;
        }

        Exp += amount;
        while (Level < MaxLevel && Exp >= NextLevelExp)
        {
            Exp -= NextLevelExp;
            Level++;
        }

        if (Level >= MaxLevel)
            Exp = 0;

        OnUpdate?.Invoke();
    }

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
