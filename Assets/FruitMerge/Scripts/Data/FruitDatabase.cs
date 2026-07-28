using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FruitDatabase", menuName = "FruitMerge/Fruit Database")]
public class FruitDatabase : ScriptableObject
{
    public List<FruitDefinition> fruits = new List<FruitDefinition>();
    [Range(1, 11)] public int spawnableCount = 5;

    public FruitDefinition GetByTier(int tier)
    {
        if (tier < 0 || tier >= fruits.Count)  return null;
        return fruits[tier];
    }
    
    public int MaxTier => fruits.Count -1;
    
    




#if UNITY_EDITOR

    void OnValidate()
    {
        for (int i = 0; i < fruits.Count; i++)
        {
            if (fruits[i] == null)
            {
                Debug.LogError($"[FruitDatabase] {i}. sırada BOŞ slot var.", this);
                continue;
            }

            if (fruits[i].tier != i)
            {
                Debug.LogError($"[FruitDatabase] Tier uyuşmazlığı: liste sırası {i}, " +
                               $"asset tier'ı {fruits[i].tier} ({fruits[i].name})", fruits[i]);
            }

            if (i < fruits.Count - 1 && fruits[i+1] != fruits[i].nextTier)
            {
                Debug.LogError($"[FruitDatabase] Zincir kopuk: {fruits[i].name} -> " +
                               $"{(fruits[i].nextTier ? fruits[i].nextTier.name : "NULL")} " +
                               $"olmalıydı {fruits[i + 1].name}", fruits[i]);
            }

            if (i == fruits.Count - 1 && fruits[i].nextTier != null)
            {
                Debug.LogError($"[FruitDatabase] Son tier'ın nextTier'ı NULL olmalı: " +
                               $"{fruits[i].name}", fruits[i]);
            }
            
        }
        
        spawnableCount = Mathf.Clamp(spawnableCount, 1, Mathf.Max(1, fruits.Count));
        
    }
    
    
#endif    
    
}
