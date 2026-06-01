using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Localization.Unity.Editor
{
    /// <summary>
    /// Syncs LocalizationDatabase to Unity Localization String Tables.
    /// Lives in Editor assembly of the Unity Localization adapter.
    /// Called by DialogueLocalizationBuilder via reflection.
    /// </summary>
    public static class UnityLocalizationDatabaseSyncer
    {
        public static void SyncDatabase(LocalizationDatabase database)
        {
            if (database == null) return;

            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning("[UnityLocalizationDatabaseSyncer] No locales configured in Project Settings > Localization");
                return;
            }

            // Create collections per graph
            foreach (var graphEntry in database.Graphs)
            {
                var collectionName = $"DLG_{Sanitize(graphEntry.GraphName)}";
                var collection = GetOrCreateCollection(collectionName);
                EnsureTablesForAllLocales(collection, locales);
                SyncEntries(collection, graphEntry.Keys);
            }

            // Create global Speakers collection
            var speakersCollection = GetOrCreateCollection("Dialogue_Speakers");
            EnsureTablesForAllLocales(speakersCollection, locales);

            AssetDatabase.SaveAssets();
            Debug.Log("[UnityLocalizationDatabaseSyncer] Sync complete");
        }

        private static StringTableCollection GetOrCreateCollection(string collectionName)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (existing != null) return existing;

            var folder = "Assets/Localization/Collections";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parentFolder = AssetDatabase.IsValidFolder("Assets/Localization") ? "Assets/Localization" : "Assets";
                var folderName = AssetDatabase.IsValidFolder("Assets/Localization") ? "Collections" : "Localization";
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }

            var collection = LocalizationEditorSettings.CreateStringTableCollection(collectionName, $"{folder}/{collectionName}");
            Debug.Log($"[UnityLocalizationDatabaseSyncer] Created String Table Collection: {collectionName}");
            return collection;
        }

        private static void EnsureTablesForAllLocales(StringTableCollection collection, IEnumerable<Locale> locales)
        {
            foreach (var locale in locales)
            {
                var table = collection.GetTable(locale.Identifier) as StringTable;
                if (table == null)
                    collection.AddNewTable(locale.Identifier);
            }
        }

        private static void SyncEntries(StringTableCollection collection, IReadOnlyList<LocalizationKeyEntry> keys)
        {
            var shared = collection.SharedData;
            foreach (var keyEntry in keys)
            {
                if (string.IsNullOrWhiteSpace(keyEntry.Key)) continue;

                var existing = shared.GetEntry(keyEntry.Key);
                if (existing == null)
                    shared.AddKey(keyEntry.Key);
            }

            EditorUtility.SetDirty(shared);
            foreach (var table in collection.StringTables)
                if (table != null) EditorUtility.SetDirty(table);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
    }
}
