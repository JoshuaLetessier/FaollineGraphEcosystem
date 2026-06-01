using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Faolline.GraphDialogue;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

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

            // The source locale receives the pre-filled default text (node Title / speaker fallback).
            var sourceLocale = GetSourceLocale(locales);

            // Create collections per graph
            foreach (var graphEntry in database.Graphs)
            {
                var collectionName = $"DLG_{Sanitize(graphEntry.GraphName)}";
                var collection = GetOrCreateCollection(collectionName);
                EnsureTablesForAllLocales(collection, locales);
                SyncEntries(collection, graphEntry.Keys, sourceLocale);
            }

            // Create + populate global Speakers collection (shared across graphs)
            var speakersCollection = GetOrCreateCollection("Dialogue_Speakers");
            EnsureTablesForAllLocales(speakersCollection, locales);
            SyncEntries(speakersCollection, database.SpeakerKeys, sourceLocale);

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

        private static void SyncEntries(StringTableCollection collection, IReadOnlyList<LocalizationKeyEntry> keys, Locale sourceLocale)
        {
            var shared = collection.SharedData;

            // Resolve the source-locale table once: only that table is pre-filled with default text.
            StringTable sourceTable = null;
            if (sourceLocale != null)
                sourceTable = collection.GetTable(sourceLocale.Identifier) as StringTable;

            foreach (var keyEntry in keys)
            {
                if (string.IsNullOrWhiteSpace(keyEntry.Key)) continue;

                var sharedEntry = shared.GetEntry(keyEntry.Key) ?? shared.AddKey(keyEntry.Key);
                if (sharedEntry == null) continue;

                // Pre-fill the source-locale entry with the default text (node Title / speaker fallback)
                // only when it is currently empty — never overwrite an existing translation.
                if (sourceTable != null && !string.IsNullOrEmpty(keyEntry.DefaultHint))
                {
                    var entry = sourceTable.GetEntry(sharedEntry.Id) ?? sourceTable.AddEntry(sharedEntry.Id, string.Empty);
                    if (entry != null && string.IsNullOrEmpty(entry.Value))
                        entry.Value = keyEntry.DefaultHint;
                }
            }

            EditorUtility.SetDirty(shared);
            foreach (var table in collection.StringTables)
                if (table != null) EditorUtility.SetDirty(table);
        }

        /// <summary>
        /// Determines the project's source locale (receives pre-filled default text). Prefers the
        /// configured ProjectLocale, falling back to the first available locale.
        /// </summary>
        private static Locale GetSourceLocale(IList<Locale> locales)
        {
            try
            {
                var project = UnityLocalizationSettings.ProjectLocale;
                if (project != null) return project;
            }
            catch
            {
                // LocalizationSettings may be unavailable/unconfigured at edit time — fall through.
            }
            return locales != null && locales.Count > 0 ? locales[0] : null;
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
