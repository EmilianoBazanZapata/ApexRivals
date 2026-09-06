using System.Collections.Generic;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public enum PresentationScreenState
    {
        Main,
        VehicleSelection,
        Settings,
        Garage,
        Racing,
        Paused,
        Results,
        Transitioning
    }

    public enum PresentationFailure
    {
        None,
        NavigationFailed,
        SaveFailed,
        InvalidVehicleSelection,
        UpgradePurchaseFailed,
        RaceSetupFailed,
        RetryFailed,
        SettingsApplyFailed
    }

    public enum PresentationStatus
    {
        None,
        Succeeded,
        InsufficientCurrency,
        MaximumLevelReached,
        Invalid
    }

    public enum MainMenuStartTarget
    {
        Race,
        InitialVehicleSelection
    }

    public enum CountdownDisplayState
    {
        Hidden,
        CountingDown,
        Go
    }

    public readonly struct SettingsResolutionViewModel
    {
        public SettingsResolutionViewModel(SettingsResolution resolution, bool isSelected)
        {
            Resolution = resolution;
            IsSelected = isSelected;
        }

        public SettingsResolution Resolution { get; }
        public int Width => Resolution.Width;
        public int Height => Resolution.Height;
        public int RefreshRate => Resolution.RefreshRate;
        public bool IsSelected { get; }
    }

    public readonly struct SettingsViewModel
    {
        public SettingsViewModel(
            IReadOnlyList<SettingsResolutionViewModel> availableResolutions,
            SettingsResolution selectedResolution,
            FullScreenMode fullScreenMode,
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            bool hasUnsavedChanges,
            bool canApply,
            PresentationFailure failure,
            string messageKey)
        {
            AvailableResolutions = availableResolutions;
            SelectedResolution = selectedResolution;
            FullScreenMode = fullScreenMode;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            HasUnsavedChanges = hasUnsavedChanges;
            CanApply = canApply;
            Failure = failure;
            MessageKey = messageKey;
        }

        public IReadOnlyList<SettingsResolutionViewModel> AvailableResolutions { get; }
        public SettingsResolution SelectedResolution { get; }
        public FullScreenMode FullScreenMode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public bool HasUnsavedChanges { get; }
        public bool CanApply { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }

    public readonly struct MainMenuViewModel
    {
        public MainMenuViewModel(
            MainMenuStartTarget startTarget,
            bool canStartOrContinue,
            bool canOpenGarage,
            bool canOpenSettings,
            bool canQuit,
            bool isTransitioning,
            PresentationFailure failure,
            string messageKey)
        {
            StartTarget = startTarget;
            CanStartOrContinue = canStartOrContinue;
            CanOpenGarage = canOpenGarage;
            CanOpenSettings = canOpenSettings;
            CanQuit = canQuit;
            IsTransitioning = isTransitioning;
            Failure = failure;
            MessageKey = messageKey;
        }

        public MainMenuStartTarget StartTarget { get; }
        public bool CanStartOrContinue { get; }
        public bool CanOpenGarage { get; }
        public bool CanOpenSettings { get; }
        public bool CanQuit { get; }
        public bool IsTransitioning { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }

    public readonly struct VehicleSelectionItemViewModel
    {
        public VehicleSelectionItemViewModel(
            string vehicleId,
            string displayName,
            ArcadeVehicleStats baseStats,
            ArcadeVehicleStats effectiveStats,
            bool isSelected,
            bool canSelect)
        {
            VehicleId = vehicleId;
            DisplayName = displayName;
            BaseStats = baseStats;
            EffectiveStats = effectiveStats;
            IsSelected = isSelected;
            CanSelect = canSelect;
        }

        public string VehicleId { get; }
        public string DisplayName { get; }
        public ArcadeVehicleStats BaseStats { get; }
        public ArcadeVehicleStats EffectiveStats { get; }
        public bool IsSelected { get; }
        public bool CanSelect { get; }
    }

    public readonly struct VehicleSelectionViewModel
    {
        public VehicleSelectionViewModel(
            IReadOnlyList<VehicleSelectionItemViewModel> vehicles,
            int selectedIndex,
            string selectedVehicleId,
            bool canSelectPrevious,
            bool canSelectNext,
            bool canConfirm,
            bool canContinue,
            bool isSaving,
            PresentationFailure failure,
            string messageKey)
        {
            Vehicles = vehicles;
            SelectedIndex = selectedIndex;
            SelectedVehicleId = selectedVehicleId;
            CanSelectPrevious = canSelectPrevious;
            CanSelectNext = canSelectNext;
            CanConfirm = canConfirm;
            CanContinue = canContinue;
            IsSaving = isSaving;
            Failure = failure;
            MessageKey = messageKey;
        }

        public IReadOnlyList<VehicleSelectionItemViewModel> Vehicles { get; }
        public int SelectedIndex { get; }
        public string SelectedVehicleId { get; }
        public bool CanSelectPrevious { get; }
        public bool CanSelectNext { get; }
        public bool CanConfirm { get; }
        public bool CanContinue { get; }
        public bool IsSaving { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }

    public readonly struct GarageUpgradeViewModel
    {
        public GarageUpgradeViewModel(UpgradeType upgradeType, int currentLevel, int maximumLevel, int nextPrice, bool canPurchase)
        {
            UpgradeType = upgradeType;
            CurrentLevel = currentLevel;
            MaximumLevel = maximumLevel;
            NextPrice = nextPrice;
            CanPurchase = canPurchase;
        }

        public UpgradeType UpgradeType { get; }
        public int CurrentLevel { get; }
        public int MaximumLevel { get; }
        public int NextPrice { get; }
        public bool CanPurchase { get; }
        public bool IsAtMaximum => CurrentLevel >= MaximumLevel;
    }

    public readonly struct GarageViewModel
    {
        public GarageViewModel(
            int currency,
            string selectedVehicleName,
            GarageUpgradeViewModel engine,
            GarageUpgradeViewModel handling,
            ArcadeVehicleStats effectiveStats,
            bool operationActive,
            PresentationStatus purchaseStatus,
            string messageKey)
        {
            Currency = currency;
            SelectedVehicleName = selectedVehicleName;
            Engine = engine;
            Handling = handling;
            EffectiveStats = effectiveStats;
            OperationActive = operationActive;
            PurchaseStatus = purchaseStatus;
            MessageKey = messageKey;
        }

        public int Currency { get; }
        public string SelectedVehicleName { get; }
        public GarageUpgradeViewModel Engine { get; }
        public GarageUpgradeViewModel Handling { get; }
        public ArcadeVehicleStats EffectiveStats { get; }
        public bool OperationActive { get; }
        public PresentationStatus PurchaseStatus { get; }
        public string MessageKey { get; }
    }

    public readonly struct RaceHudViewModel
    {
        public RaceHudViewModel(
            CountdownDisplayState countdownState,
            float countdownValue,
            int currentLap,
            int totalLaps,
            int currentPosition,
            int participantCount,
            float elapsedRaceTime,
            bool racingInputActive)
        {
            CountdownState = countdownState;
            CountdownValue = countdownValue;
            CurrentLap = currentLap;
            TotalLaps = totalLaps;
            CurrentPosition = currentPosition;
            ParticipantCount = participantCount;
            ElapsedRaceTime = elapsedRaceTime;
            RacingInputActive = racingInputActive;
        }

        public CountdownDisplayState CountdownState { get; }
        public float CountdownValue { get; }
        public int CurrentLap { get; }
        public int TotalLaps { get; }
        public int CurrentPosition { get; }
        public int ParticipantCount { get; }
        public float ElapsedRaceTime { get; }
        public bool RacingInputActive { get; }
    }

    public readonly struct ResultsViewModel
    {
        public ResultsViewModel(
            int finalPosition,
            int participantCount,
            float raceTime,
            int earnedReward,
            int updatedCurrency,
            bool isTransitioning,
            PresentationFailure failure,
            string messageKey)
        {
            FinalPosition = finalPosition;
            ParticipantCount = participantCount;
            RaceTime = raceTime;
            EarnedReward = earnedReward;
            UpdatedCurrency = updatedCurrency;
            IsTransitioning = isTransitioning;
            Failure = failure;
            MessageKey = messageKey;
        }

        public int FinalPosition { get; }
        public int ParticipantCount { get; }
        public float RaceTime { get; }
        public int EarnedReward { get; }
        public int UpdatedCurrency { get; }
        public bool IsTransitioning { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }

    public readonly struct PauseViewModel
    {
        public PauseViewModel(bool isPaused, bool isTransitioning, bool canResume, bool canRetry, bool canReturnToMainMenu, PresentationFailure failure, string messageKey)
        {
            IsPaused = isPaused;
            IsTransitioning = isTransitioning;
            CanResume = canResume;
            CanRetry = canRetry;
            CanReturnToMainMenu = canReturnToMainMenu;
            Failure = failure;
            MessageKey = messageKey;
        }

        public bool IsPaused { get; }
        public bool IsTransitioning { get; }
        public bool CanResume { get; }
        public bool CanRetry { get; }
        public bool CanReturnToMainMenu { get; }
        public PresentationFailure Failure { get; }
        public string MessageKey { get; }
    }
}
