using System.Collections.Generic;
using ApexRivals.Settings.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Settings
{
    public sealed class AmbienceSettingsStorageTests
    {
        private const string Prefix = "ApexRivals.Settings.";
        private readonly Dictionary<string, (bool exists, int value)> _ints = new Dictionary<string, (bool, int)>();
        private readonly Dictionary<string, (bool exists, float value)> _floats = new Dictionary<string, (bool, float)>();

        [SetUp]
        public void CaptureUserPreferences()
        {
            foreach (var suffix in new[] { "SchemaVersion", "Resolution.Width", "Resolution.Height", "Display.FullScreenMode" })
            {
                var key = Prefix + suffix;
                _ints[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
            }
            foreach (var suffix in new[] { "Master", "Music", "Sfx", "Ui", "Vehicles", "Ambience" })
            {
                var key = Prefix + "Audio." + suffix;
                _floats[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetFloat(key));
            }
        }

        [TearDown]
        public void RestoreUserPreferences()
        {
            foreach (var pair in _ints)
                if (pair.Value.exists) PlayerPrefs.SetInt(pair.Key, pair.Value.value); else PlayerPrefs.DeleteKey(pair.Key);
            foreach (var pair in _floats)
                if (pair.Value.exists) PlayerPrefs.SetFloat(pair.Key, pair.Value.value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
            _ints.Clear(); _floats.Clear();
        }

        [Test]
        public void OlderPreferencesDefaultAmbienceToFullVolumeAndNewSavesRoundTripIt()
        {
            var storage = new PlayerPrefsSettingsStorage();
            storage.Save(SettingsState.Default.WithMusicVolume(.42f));
            PlayerPrefs.DeleteKey(Prefix + "Audio.Ambience");
            var legacy = storage.Load();
            Assert.That(legacy.Status, Is.EqualTo(SettingsStorageReadStatus.Loaded));
            Assert.That(legacy.Settings.AmbienceVolume, Is.EqualTo(1f));
            Assert.That(legacy.Settings.MusicVolume, Is.EqualTo(.42f));
            storage.Save(legacy.Settings.WithAmbienceVolume(.3f));
            var loaded = new PlayerPrefsSettingsStorage().Load();
            Assert.That(loaded.Settings.AmbienceVolume, Is.EqualTo(.3f));
            Assert.That(loaded.Settings.MusicVolume, Is.EqualTo(.42f));
        }

        [Test]
        public void InvalidStoredAmbienceIsReportedAndClamped()
        {
            var storage = new PlayerPrefsSettingsStorage();
            storage.Save(SettingsState.Default);
            PlayerPrefs.SetFloat(Prefix + "Audio.Ambience", 4f);
            var loaded = storage.Load();
            Assert.That(loaded.Status, Is.EqualTo(SettingsStorageReadStatus.Invalid));
            Assert.That(loaded.Settings.AmbienceVolume, Is.EqualTo(1f));
        }
    }
}
