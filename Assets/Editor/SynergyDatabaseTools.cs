using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// シナジーの推奨割り当てをEntity Databaseへ一括反映するEditor専用ツールです。
public static class SynergyDatabaseTools
{
    private const string EntityDatabasePath = "Assets/Resources/Entity Database.asset";

    [MenuItem("Tools/AutoChess/Apply Default Synergies To Entity Database")]
    public static void ApplyDefaultSynergiesToEntityDatabase()
    {
        EntitiesDatabaseSO database = AssetDatabase.LoadAssetAtPath<EntitiesDatabaseSO>(EntityDatabasePath);
        if (database == null || database.allEntities == null)
        {
            Debug.LogError($"Entity Database was not found at {EntityDatabasePath}.");
            return;
        }

        int updatedCount = 0;
        for (int i = 0; i < database.allEntities.Count; i++)
        {
            EntitiesDatabaseSO.EntityData entityData = database.allEntities[i];
            List<SynergyType> synergies = SynergyManager.GetDefaultSynergiesForUnit(entityData.name, entityData.prefab);

            entityData.synergy1 = synergies.Count > 0 ? synergies[0] : SynergyType.None;
            entityData.synergy2 = synergies.Count > 1 ? synergies[1] : SynergyType.None;
            entityData.synergy3 = synergies.Count > 2 ? synergies[2] : SynergyType.None;
            database.allEntities[i] = entityData;
            updatedCount++;
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Applied default synergies to {updatedCount} entity database entries.");
    }
}
