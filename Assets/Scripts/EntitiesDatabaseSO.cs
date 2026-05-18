using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Entity Database", menuName = "CustomSO/EntityDatabase")]
// ショップや敵生成で使うユニット一覧を保存するScriptableObjectです。
public class EntitiesDatabaseSO : ScriptableObject
{
    [System.Serializable]
    // 1体分のユニット情報をInspectorで編集できる形にまとめています。
    public struct EntityData
    {
        // 実際に生成するユニットPrefabです。
        public BaseEntity prefab;
        // ユニットを識別する名前です。同名3体合成の判定にも使います。
        public string name;
        // ショップカードに表示するユニットアイコンです。
        public Sprite icon;
        // 購入に必要なコイン数です。ショップ出現率や売却額の基準にもなります。
        public int cost;
        // ショップカードに表示するコスト別フレーム画像です。
        public Sprite frame;
    }

    // ゲームで使える全ユニットの一覧です。
    public List<EntityData> allEntities;
}
