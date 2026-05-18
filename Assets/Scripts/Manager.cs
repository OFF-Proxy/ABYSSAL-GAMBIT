using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// どこからでも「GameManager.Instance」のように呼べる共通のシングルトン土台です。
public class Manager<T> : MonoBehaviour
    where T : Manager<T>
{ 
    // この型の管理クラスがシーン内に1つだけある前提で、参照をここに保存します。
    public static T Instance;

    // Unityがオブジェクト生成時に呼ぶ初期化関数です。
    protected void Awake()
    {
        // 自分自身をInstanceに入れて、他のスクリプトから探さず使えるようにします。
        Instance = (T)this;
    }
}
