using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Entity Database", menuName = "CustomSO/EntityDatabase")]
public class EntitiesDatabaseSO : ScriptableObject
{
    [System.Serializable]
    public struct EntityData
    {
        public BaseEntity prefab;
        // エンティティの名前
        public string name;
        // エンティティのアイコン
        public Sprite icon;
        // エンティティのコスト
        public int cost;
        public Sprite frame;
    }

    public List<EntityData> allEntities;
}