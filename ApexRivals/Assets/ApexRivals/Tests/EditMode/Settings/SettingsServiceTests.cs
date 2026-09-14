using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Settings
{
    public sealed class SettingsServiceTests
    {
        [Test]
        public void DefaultSettingsAreSensible()
        {
            var settings = SettingsState.Default;

            Assert.That(settings.Width, Is.EqualTo(1920));
            Assert.That(settings.Height, Is.EqualTo(1080));
            Assert.That(settings.RefreshRate, Is.EqualTo(60));
            Assert.That(settings.FullScreenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(settings.MasterVolume, Is.EqualTo(1f));
            Assert.That(settings.MusicVolume, Is.EqualTo(0.8f));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f));
        }

        [Test]
        public void LoadExistingPreferencesRestoresCurrentAndDraft()
        {
            var storage = new MemorySettingsStorage
            {
                Exists = true,
                Stored = new SettingsState(1280, 720, 60, FullScreenMode.Windowed, 0.5f, 0.4f, 0.3f)
            };
            var service = CreateService(storage);

            var result = service.Load();

            Assert.That(result.Status, Is.EqualTo(SettingsOperationStatus.Succeeded));
            Assert.That(service.Current.Width, Is.EqualTo(1280));
            Assert.That(service.Current.FullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(service.Draft.MusicVolume, Is.EqualTo(0.4f));
        }

        [Test]
        public void MissingPreferencesUseDefaults()
        {
            var service = CreateService(new MemorySettingsStorage());

            var result = service.Load();

            Assert.That(result.Status, Is.EqualTo(SettingsOperationStatus.DefaultsLoaded));
            Assert.That(service.Current, Is.EqualTo(SettingsState.Default));
        }

        [Test]
        public void VolumeValuesAreClamped()
        {
            var service = CreateService();

            service.EditMasterVolume(2f);
            service.EditMusicVolume(-1f);
            service.EditSfxVolume(float.NaN);

            Assert.That(service.Draft.MasterVolume, Is.EqualTo(1f));
            Assert.That(service.Draft.MusicVolume, Is.EqualTo(0f));
            Assert.That(service.Draft.SfxVolume, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidSavedResolutionFallsBackSafely()
        {
            var storage = new MemorySettingsStorage
            {
                Exists = true,
                ReadStatus = SettingsStorageReadStatus.Invalid,
                Stored = new SettingsState(333, 222, 60, FullScreenMode.FullScreenWindow, 1f, 1f, 1f)
            };
            var display = new FakeDisplaySettingsAdapter(new[]
            {
                new SettingsResolution(1280, 720, 60),
                SettingsState.Default.Resolution
            }, new SettingsResolution(1280, 720, 60));
            var service = new SettingsService(storage, display);

            var result = service.Load();

            Assert.That(result.Status, Is.EqualTo(SettingsOperationStatus.LoadedWithInvalidValues));
            Assert.That(service.Current.Resolution, Is.EqualTo(display.CurrentResolution));
        }

        [Test]
        public void DuplicateResolutionsAreRemoved()
        {
            var resolutions = SettingsDisplayCatalog.RemoveDuplicates(new[]
            {
                new SettingsResolution(1280, 720, 60),
                new SettingsResolution(1280, 720, 60),
                new SettingsResolution(1920, 1080, 60),
                new SettingsResolution(0, 1080, 60)
            });

            Assert.That(resolutions.Count, Is.EqualTo(2));
        }

        [Test]
        public void ApplyUpdatesDisplayAdapter()
        {
            var display = new FakeDisplaySettingsAdapter();
            var service = CreateService(new MemorySettingsStorage(), display);
            service.EditResolution(new SettingsResolution(1280, 720, 60));

            service.Apply();

            Assert.That(display.ApplyCount, Is.EqualTo(1));
            Assert.That(display.LastApplied.Width, Is.EqualTo(1280));
        }

        [Test]
        public void ApplyPersistsOnce()
        {
            var storage = new MemorySettingsStorage();
            var service = CreateService(storage);
            service.EditMasterVolume(0.25f);

            service.Apply();

            Assert.That(storage.SaveCount, Is.EqualTo(1));
            Assert.That(storage.Saved.MasterVolume, Is.EqualTo(0.25f));
        }

        [Test]
        public void CancelRestoresCommittedSettings()
        {
            var service = CreateService();
            service.EditMasterVolume(0.2f);
            service.Apply();

            service.EditMasterVolume(0.8f);
            service.Cancel();

            Assert.That(service.Draft.MasterVolume, Is.EqualTo(0.2f));
            Assert.That(service.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void RestoreDefaultsCreatesExpectedDraft()
        {
            var service = CreateService();
            service.EditMasterVolume(0.2f);
            service.Apply();

            service.RestoreDefaults();

            Assert.That(service.Draft, Is.EqualTo(SettingsState.Default));
            Assert.That(service.HasUnsavedChanges, Is.True);
        }

        [Test]
        public void CancelDoesNotPersist()
        {
            var storage = new MemorySettingsStorage();
            var service = CreateService(storage);

            service.EditMusicVolume(0.1f);
            service.Cancel();

            Assert.That(storage.SaveCount, Is.Zero);
        }

        [Test]
        public void PlayerProgressionIsNotWrittenToSettingsStorage()
        {
            var storage = new MemorySettingsStorage();
            var service = CreateService(storage);

            service.EditSfxVolume(0.6f);
            service.Apply();

            Assert.That(storage.WrittenKeys, Does.Not.Contain("currency"));
            Assert.That(storage.WrittenKeys, Does.Not.Contain("engineUpgradeLevel"));
            Assert.That(storage.WrittenKeys, Does.Not.Contain("handlingUpgradeLevel"));
            Assert.That(storage.WrittenKeys, Does.Not.Contain("selectedVehicleId"));
        }

        [Test]
        public void PresenterMapsAvailableResolutions()
        {
            var display = new FakeDisplaySettingsAdapter(new[]
            {
                new SettingsResolution(1280, 720, 60),
                new SettingsResolution(1920, 1080, 60)
            }, new SettingsResolution(1920, 1080, 60));
            var service = CreateService(new MemorySettingsStorage(), display);
            var presenter = new SettingsPresenter(new FakeSettingsView(), service);

            presenter.Present();

            Assert.That(presenter.Current.AvailableResolutions.Count, Is.EqualTo(2));
            Assert.That(presenter.Current.SelectedResolution, Is.EqualTo(SettingsState.Default.Resolution));
        }

        [Test]
        public void PresenterReportsApplyFailures()
        {
            var display = new FakeDisplaySettingsAdapter { FailApply = true };
            var service = CreateService(new MemorySettingsStorage(), display);
            var presenter = new SettingsPresenter(new FakeSettingsView(), service);

            presenter.SetMusicVolume(0.25f);
            presenter.Apply();

            Assert.That(presenter.Current.Failure, Is.EqualTo(PresentationFailure.SettingsApplyFailed));
            Assert.That(presenter.Current.MessageKey, Is.EqualTo("ui.settings.display.failed"));
        }

        [Test]
        public async Task BootstrapLoadsSettingsBeforeMainMenuNavigation()
        {
            var settingsStorage = new MemorySettingsStorage();
            var display = new FakeDisplaySettingsAdapter();
            var settingsService = new SettingsService(settingsStorage, display);
            var profileStorage = new MemoryProfileStorage();
            var progressionState = new PlayerProgressionState();
            var selectedVehicleState = new SelectedVehicleState();
            var saveService = new PlayerProfileSaveService(
                progressionState,
                selectedVehicleState,
                profileStorage,
                new[] { SelectedVehicleState.DefaultVehicleId });
            var navigation = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), new OrderedSceneLoader(settingsStorage));
            var bootstrap = new ApplicationBootstrapService(saveService, navigation, settingsService);

            var result = await bootstrap.StartAsync();

            Assert.That(result.Status, Is.EqualTo(BootstrapStartupStatus.CreatedDefaultProfile));
            Assert.That(settingsStorage.LoadAttemptedBeforeNavigation, Is.True);
            Assert.That(navigation.State, Is.EqualTo(ApplicationState.MainMenu));
        }

        private static SettingsService CreateService(MemorySettingsStorage storage = null, FakeDisplaySettingsAdapter display = null)
        {
            return new SettingsService(storage ?? new MemorySettingsStorage(), display ?? new FakeDisplaySettingsAdapter());
        }

        private sealed class MemorySettingsStorage : ISettingsStorage
        {
            public bool Exists { get; set; }
            public SettingsStorageReadStatus ReadStatus { get; set; } = SettingsStorageReadStatus.Loaded;
            public SettingsState Stored { get; set; } = SettingsState.Default;
            public SettingsState Saved { get; private set; }
            public int SaveCount { get; private set; }
            public bool LoadAttemptedBeforeNavigation { get; private set; }
            public List<string> WrittenKeys { get; } = new List<string>();

            public bool SettingsExist()
            {
                LoadAttemptedBeforeNavigation = true;
                return Exists;
            }

            public SettingsStorageReadResult Load()
            {
                LoadAttemptedBeforeNavigation = true;
                return new SettingsStorageReadResult(ReadStatus, Stored, string.Empty);
            }

            public SettingsStorageWriteResult Save(SettingsState settings)
            {
                SaveCount++;
                Saved = settings;
                WrittenKeys.Clear();
                WrittenKeys.Add("width");
                WrittenKeys.Add("height");
                WrittenKeys.Add("refreshRate");
                WrittenKeys.Add("fullScreenMode");
                WrittenKeys.Add("masterVolume");
                WrittenKeys.Add("musicVolume");
                WrittenKeys.Add("sfxVolume");
                return new SettingsStorageWriteResult(true, string.Empty);
            }

            public SettingsStorageWriteResult RestoreDefaults(SettingsState defaults)
            {
                return Save(defaults);
            }
        }

        private sealed class FakeDisplaySettingsAdapter : IDisplaySettingsAdapter
        {
            private readonly IReadOnlyList<SettingsResolution> _supportedResolutions;

            public FakeDisplaySettingsAdapter()
                : this(new[] { SettingsState.Default.Resolution, new SettingsResolution(1280, 720, 60) }, SettingsState.Default.Resolution)
            {
            }

            public FakeDisplaySettingsAdapter(IReadOnlyList<SettingsResolution> supportedResolutions, SettingsResolution currentResolution)
            {
                _supportedResolutions = SettingsDisplayCatalog.RemoveDuplicates(supportedResolutions);
                CurrentResolution = currentResolution;
            }

            public bool FailApply { get; set; }
            public int ApplyCount { get; private set; }
            public SettingsState LastApplied { get; private set; }
            public IReadOnlyList<SettingsResolution> SupportedResolutions => _supportedResolutions;
            public SettingsResolution CurrentResolution { get; }

            public SettingsOperationResult Apply(SettingsState settings)
            {
                ApplyCount++;
                LastApplied = settings;
                return FailApply
                    ? new SettingsOperationResult(SettingsOperationStatus.DisplayApplyFailed, "Display failed.")
                    : SettingsOperationResult.Success();
            }
        }

        private sealed class FakeSettingsView : ISettingsView
        {
            public SettingsViewModel Last { get; private set; }

            public void Render(SettingsViewModel viewModel)
            {
                Last = viewModel;
            }
        }

        private sealed class OrderedSceneLoader : IContentSceneLoader
        {
            private readonly MemorySettingsStorage _settingsStorage;

            public OrderedSceneLoader(MemorySettingsStorage settingsStorage)
            {
                _settingsStorage = settingsStorage;
            }

            public float Progress => 1f;
            public bool IsLoading => false;
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; private set; }

            public Task<ContentSceneLoadResult> LoadContentSceneAsync(
                ContentSceneId sceneId,
                string sceneName,
                bool reloadCurrentScene,
                CancellationToken cancellationToken = default)
            {
                Assert.That(_settingsStorage.LoadAttemptedBeforeNavigation, Is.True);
                CurrentContentSceneName = sceneName;
                return Task.FromResult(ContentSceneLoadResult.Success(sceneId, sceneName, string.Empty));
            }
        }

        private sealed class MemoryProfileStorage : IPlayerProfileSaveStorage
        {
            public bool SaveExists()
            {
                return false;
            }

            public bool BackupExists()
            {
                return false;
            }

            public SaveStorageReadResult ReadSave()
            {
                return new SaveStorageReadResult(false, string.Empty, "No save.");
            }

            public SaveStorageReadResult ReadBackup()
            {
                return new SaveStorageReadResult(false, string.Empty, "No backup.");
            }

            public SaveStorageWriteResult WriteSave(string payload)
            {
                return new SaveStorageWriteResult(true, string.Empty);
            }

            public SaveStorageWriteResult DeleteSave()
            {
                return new SaveStorageWriteResult(true, string.Empty);
            }
        }
    }
}
