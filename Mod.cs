using Colossal;
using Colossal.Localization;
using Colossal.Logging;

using Game;
using Game.Modding;
using Game.PSI;
using Game.SceneFlow;
using Game.Settings;
using Game.UI.Menu;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using UnityEngine;

namespace NorwegianLocale
{
    public class Mod : IMod
    {
        public static ILog log = LogManager
            .GetLogger($"{nameof(NorwegianLocale)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        private const string CrowdinNotificationId =
            "norwegian-locale-crowdin-notification";

        private const string CrowdinUrl =
            "https://crowdin.com/project/community-localizations-csii";

        private static string _crowdinThumbnailUri =
            "assetdb://user/Mods/NorwegianLocale/no.jpg";

        private static bool _crowdinNotificationShown;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info("=== Norwegian Locale Mod Loading ===");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                var modDirectory = Path.GetDirectoryName(asset.path);

                if (!string.IsNullOrEmpty(modDirectory))
                {
                    TryAddVanillaSource(modDirectory);

                    _crowdinThumbnailUri =
                        $"assetdb://user/Mods/{Path.GetFileName(modDirectory)}/no.jpg";
                }
            }

            GameManager.instance.localizationManager.LoadAvailableLocales();

            typeof(InterfaceSettings)
                .GetMethod(
                    "RegisterInOptionsUI",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(string), typeof(bool) },
                    null)?
                .Invoke(
                    GameManager.instance.settings.userInterface,
                    new object[] { "Interface", false });

            Colossal.Core.MainThreadDispatcher.RegisterUpdater(
                TryShowCrowdinNotification);

            log.Info("Mod loaded successfully");
        }

        private bool TryShowCrowdinNotification()
        {
            if (_crowdinNotificationShown)
            {
                return true;
            }

            try
            {
                if (GameManager.instance == null
                    || !GameManager.instance.modManager.isInitialized
                    || GameManager.instance.gameMode != GameMode.MainMenu
                    || GameManager.instance.state == GameManager.State.Loading
                    || GameManager.instance.state == GameManager.State.Booting)
                {
                    return false;
                }

                NotificationSystem.Pop(CrowdinNotificationId);

                NotificationSystem.Push(
                    identifier: CrowdinNotificationId,
                    title: "Fant du oversettelsesfeil, inkonsekvenser eller vil du bidra?",
                    text: "Klikk her for å bidra til å forbedre oversettelsen på Crowdin.",
                    thumbnail: _crowdinThumbnailUri,
                    onClicked: () => Application.OpenURL(CrowdinUrl)
                );

                _crowdinNotificationShown = true;

                log.Info(
                    $"Crowdin notification displayed. Thumbnail: " +
                    $"{_crowdinThumbnailUri}");

                return true;
            }
            catch (Exception e)
            {
                log.Warn($"Could not display the Crowdin notification: {e.Message}");
                return true;
            }
        }

        private void TryAddVanillaSource(string modDir)
        {
            try
            {
                var vanillaJson = Path.Combine(
                    modDir,
                    "Localization",
                    "nb-NO",
                    "Vanilla",
                    "nb-NO.json");

                if (!File.Exists(vanillaJson))
                {
                    log.Warn($"Vanilla JSON not found at: {vanillaJson}");
                    return;
                }

                var allEntries =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        File.ReadAllText(vanillaJson));

                if (allEntries == null)
                {
                    log.Warn("Failed to deserialize Vanilla/nb-NO.json");
                    return;
                }

                var indexCounts = new Dictionary<string, int>();

                foreach (var kv in allEntries)
                {
                    var colonIdx = kv.Key.LastIndexOf(':');

                    if (colonIdx > 0
                        && int.TryParse(
                            kv.Key.Substring(colonIdx + 1),
                            out int idx))
                    {
                        var baseKey = kv.Key.Substring(0, colonIdx);

                        if (!indexCounts.ContainsKey(baseKey)
                            || indexCounts[baseKey] <= idx)
                        {
                            indexCounts[baseKey] = idx + 1;
                        }
                    }
                }

                GameManager.instance.localizationManager.AddSource(
                    "nb-NO",
                    new VanillaLocaleSource(allEntries, indexCounts));

                log.Info(
                    $"Added {allEntries.Count} entries, " +
                    $"{indexCounts.Count} indexed categories");

                TryRefreshLoadingHints();
            }
            catch (Exception e)
            {
                log.Warn(
                    $"Could not add vanilla source: {e.Message}");
            }
        }

        private void TryRefreshLoadingHints()
        {
            try
            {
                var uiProp = typeof(GameManager).GetProperty(
                    "userInterface",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

                var ui = uiProp?.GetValue(GameManager.instance);

                if (ui == null)
                {
                    return;
                }

                object hintOwner = null;
                FieldInfo hintField = null;

                foreach (var topField in ui.GetType().GetFields(
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.Public))
                {
                    var topVal = topField.GetValue(ui);

                    if (topVal == null)
                    {
                        continue;
                    }

                    foreach (var sub in topVal.GetType().GetFields(
                        BindingFlags.Instance
                        | BindingFlags.NonPublic
                        | BindingFlags.Public))
                    {
                        if (sub.Name == "m_HintMessages")
                        {
                            hintOwner = topVal;
                            hintField = sub;
                            break;
                        }
                    }

                    if (hintField != null)
                    {
                        break;
                    }
                }

                if (hintField == null)
                {
                    log.Warn("m_HintMessages not found");
                    return;
                }

                var locMgr = GameManager.instance.localizationManager;

                var dictProp = locMgr.GetType().GetProperty(
                    "activeDictionary",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

                var dict = dictProp?.GetValue(locMgr);

                if (dict == null)
                {
                    return;
                }

                var getIdsMethod = dict.GetType().GetMethod(
                    "GetIndexedLocaleIDs",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

                if (getIdsMethod == null)
                {
                    return;
                }

                var hintIdsList = getIdsMethod.Invoke(
                        dict,
                        new object[] { "Loading.HINTMESSAGE" })
                    as IList<string>;

                if (hintIdsList == null)
                {
                    return;
                }

                var hintIdsArray = new string[hintIdsList.Count];
                hintIdsList.CopyTo(hintIdsArray, 0);

                var bindingObj = hintField.GetValue(hintOwner);

                var updateMethod = bindingObj?.GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(string[]) },
                    null);

                updateMethod?.Invoke(
                    bindingObj,
                    new object[] { hintIdsArray });

                log.Info(
                    $"Loading hints updated: " +
                    $"{hintIdsArray.Length} entries");
            }
            catch (Exception e)
            {
                log.Warn(
                    $"TryRefreshLoadingHints failed: {e.Message}");
            }
        }

        public void OnDispose()
        {
            NotificationSystem.Pop(CrowdinNotificationId);
            _crowdinNotificationShown = false;
        }

        private class VanillaLocaleSource : IDictionarySource
        {
            private readonly Dictionary<string, string> _entries;
            private readonly Dictionary<string, int> _indexCounts;

            public VanillaLocaleSource(
                Dictionary<string, string> entries,
                Dictionary<string, int> indexCounts)
            {
                _entries = entries;
                _indexCounts = indexCounts;
            }

            public IEnumerable<KeyValuePair<string, string>> ReadEntries(
                IList<IDictionaryEntryError> errors,
                Dictionary<string, int> indexCounts)
            {
                foreach (var kv in _indexCounts)
                {
                    indexCounts[kv.Key] = kv.Value;
                }

                return _entries;
            }

            public void Unload()
            {
            }
        }
    }
}