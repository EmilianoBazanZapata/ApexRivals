using System;
using System.Collections;
using System.Collections.Generic;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.UI.Runtime;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ApexRivals.Camera.Runtime
{
    /// <summary>Scene-owned grid presentation. The session stays Ready until the camera handoff is complete.</summary>
    [DisallowMultipleComponent]
    public sealed class PreRacePresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("Authored introduction skip bindings. Unassigned standalone prototypes retain Enter / Space / South.")] private InputActionAsset sessionInputActions;
        [SerializeField] private string skipActionPath = "Session/SkipRaceIntroduction";

        [SerializeField] private WheelVehicleFollowCamera followCamera;
        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField, Min(0.1f)] private float establishingSeconds = 1.3f;
        [SerializeField, Min(0.1f)] private float rivalSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float playerSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float handoffSeconds = 1.1f;
        [SerializeField, Range(0f, 1f)] private float blendSeconds = 0.65f;
        [SerializeField] private Vector3 rivalOffset = new Vector3(-5f, 2.2f, 4f);
        [SerializeField] private Vector3 playerOffset = new Vector3(4f, 1.8f, 5f);
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Authored in the scene")]
        [SerializeField, Tooltip("SKIP INTRO hint, inactive until the introduction plays.")] private GameObject introductionHint;
        [SerializeField, Tooltip("Inactive parent of the four shot cameras below.")] private GameObject shotRig;
        [SerializeField, Tooltip("Wide shot of the whole grid.")] private CinemachineCamera gridShot;
        [SerializeField, Tooltip("Close shot of a rival.")] private CinemachineCamera rivalsShot;
        [SerializeField, Tooltip("Close shot of the player's car.")] private CinemachineCamera playerShot;
        [SerializeField, Tooltip("Ends on the chase camera's pose; its field of view is matched to the chase camera.")] private CinemachineCamera handoffShot;

        [Serializable]
        public sealed class CarPresentationShot
        {
            public CinemachineCamera camera;
            [Tooltip("Camera positions in metres relative to the car centre, in the car's orientation.")]
            public Vector3 startOffset;
            public Vector3 endOffset;
            [Min(0.1f)] public float seconds = 3.5f;
        }

        [Header("Player car presentation")]
        [SerializeField, Tooltip("Loop the configured car shots until the player confirms. Disable for the original automatic introduction.")]
        private bool waitForStart;
        [SerializeField] private CarPresentationShot[] carShots = Array.Empty<CarPresentationShot>();
        [SerializeField, Tooltip("Offset above the visible car bounds centre.")] private float carFocusHeight = 0.1f;
        [SerializeField] private Button startRaceButton;
        [Header("Shot fades")]
        [SerializeField] private bool fadeBetweenShots = true;
        [SerializeField, Tooltip("Scene-authored overlay below the presentation controls. Its Image owns the fade colour.")]
        private CanvasGroup shotFade;
        [SerializeField, Min(0f)] private float fadeOutSeconds = .25f;
        [SerializeField, Min(0f)] private float fadeInSeconds = .35f;
        private bool UseShotFades => fadeBetweenShots && shotFade != null;

        private bool _confirmationArmed;
        private int _confirmationArmFrame;
        private bool _hasFocusScope;
        public bool AwaitingStart => IsPresenting && waitForStart && !_skipRequested;
        public int CompletedPresentationLoops { get; private set; }

        private readonly List<HeldBody> _grid = new List<HeldBody>();
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private ApplicationContext _context;
        private RaceSessionCoordinator _session;
        private BootstrapUiInputSystem _input;
        private UnityEngine.Camera _output;
        private CinemachineBrain _brain;
        private InputAction _skip;
        private Coroutine _routine;
        private bool _skipRequested;
        private bool _followWasEnabled;
        private bool _finished;
        private bool _ownsCamera;
        private float _originalNear;
        private float _originalFov;
        private int _commandReleaseFrame;

        public bool IsPresenting { get; private set; }
        // Retain the command fence through the input frame that completed/skipped the sequence.
        public bool SuppressCommands => IsPresenting || Time.frameCount <= _commandReleaseFrame;
        public int ShotIndex { get; private set; } = -1;

        public void Begin(ApplicationContext context, RaceSessionCoordinator session, RaceCoordinator coordinator)
        {
            Cancel();
            if (!isActiveAndEnabled || session == null || session.State != RaceSessionLifecycleState.Ready) return;
            _context = context;
            _session = session;
            _finished = false;
            _skipRequested = false;
            _confirmationArmed = false;
            CompletedPresentationLoops = 0;
            ShotIndex = -1;
            IsPresenting = true;
            try
            {
                // Resolve the persistent UI input owner once, never per frame.
                _input = FindFirstObjectByType<BootstrapUiInputSystem>();
                _input?.SetLoadingInputBlocked(this, true);
                if (coordinator != null)
                {
                    foreach (var participant in coordinator.Participants)
                    {
                        if (participant == null) continue;
                        var body = participant.GetComponentInParent<Rigidbody>();
                        if (body == null || _grid.Exists(held => held.Body == body)) continue;
                        _grid.Add(new HeldBody(body));
                        if (!body.isKinematic)
                        {
                            body.linearVelocity = Vector3.zero;
                            body.angularVelocity = Vector3.zero;
                        }
                        body.constraints = RigidbodyConstraints.FreezeAll;
                    }
                }
                _routine = StartCoroutine(RunSafely());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Race introduction unavailable; using the driving camera. " + exception.Message, this);
                Finish(true);
            }
        }

        /// <summary>Idempotent skip. Countdown starts only through Finish, never from the input callback.</summary>
        public void RequestSkip()
        {
            if (IsPresenting && _skip != null) _skipRequested = true;
        }

        public void Cancel()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            Finish(false);
            ReleaseInput();
            _context = null;
            _session = null;
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();

        private void Update()
        {
            if (!IsPresenting && Time.frameCount > _commandReleaseFrame) ReleaseInput();
        }

        private void ReleaseInput()
        {
            if (_input != null) _input.SetLoadingInputBlocked(this, false);
            _input = null;
        }

        // Unity does not catch exceptions inside MoveNext for our cleanup, so own that boundary explicitly.
        private IEnumerator RunSafely()
        {
            var run = Run();
            while (true)
            {
                object next;
                try
                {
                    if (!run.MoveNext()) break;
                    next = run.Current;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Race introduction interrupted; continuing without it. " + exception.Message, this);
                    break;
                }
                yield return next;
            }
            Finish(CanContinue());
            _routine = null;
        }

        private bool CanContinue() => _session != null && _session.State == RaceSessionLifecycleState.Ready
            && _context != null && _context.Navigation.State == ApplicationState.Race;

        private IEnumerator Run()
        {
            // Content installers run while the loading overlay still covers the newly loaded scene.
            while (_session != null && _session.State == RaceSessionLifecycleState.Ready
                && _context != null && (_context.Navigation.State == ApplicationState.Transitioning
                    || _context.LoadingPresentation?.IsVisible == true))
                yield return null;
            if (!CanContinue()) yield break;

            screenNavigator?.Show(PresentationScreenState.Transitioning);
            if (followCamera == null || _session.PlayerCameraTarget == null || _session.PlayerVehicleRigidbody == null)
            {
                Debug.LogWarning("Race introduction has no camera or player target; starting the normal countdown.", this);
                yield break;
            }

            followCamera.SetTarget(_session.PlayerCameraTarget);
            followCamera.SetVehicleRigidbody(_session.PlayerVehicleRigidbody);
            followCamera.SetInputProvider(_session.PlayerInputProvider);
            _output = followCamera.GetComponent<UnityEngine.Camera>();
            if (_output == null || !followCamera.SnapToTarget()) yield break;
            // Do not take ownership away from an unrelated, already enabled Cinemachine setup.
            var existingBrain = _output.GetComponent<CinemachineBrain>();
            if (existingBrain != null && existingBrain.enabled)
            {
                Debug.LogWarning("Race introduction found another camera owner; using the existing camera.", this);
                yield break;
            }
            // The brain (disabled, manual update, ignoring time scale, with its blend) and the shot cameras are
            // set up in the scene; only their poses depend on this grid.
            if (existingBrain == null || shotRig == null || gridShot == null || rivalsShot == null || playerShot == null || handoffShot == null)
            {
                Debug.LogWarning("Race introduction is not set up in this scene (Cinemachine brain or shot cameras missing); starting the normal countdown.", this);
                yield break;
            }
            var finalPose = new Pose(_output.transform.position, _output.transform.rotation);
            // Telemetry may live outside the navigator's racing panel. Hide scene UI without disabling its logic.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.enabled && !IsIntroductionHint(canvas)) { _hiddenCanvases.Add(canvas); canvas.enabled = false; }
            _originalFov = _output.fieldOfView;
            _originalNear = _output.nearClipPlane;
            _followWasEnabled = followCamera.enabled;
            followCamera.enabled = false;
            _ownsCamera = true;
            _brain = existingBrain;
            _brain.enabled = false;
            shotRig.SetActive(true);
            if (startRaceButton != null) startRaceButton.gameObject.SetActive(waitForStart);
            if (waitForStart)
            {
                var showcase = RunCarShowcase(finalPose);
                while (showcase.MoveNext()) yield return showcase.Current;
                yield break;
            }
            var playerBody = _session.PlayerVehicleRigidbody;
            var target = _session.PlayerCameraTarget.position;
            var forward = Vector3.ProjectOnPlane(playerBody.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            var basis = Quaternion.LookRotation(forward, Vector3.up);
            var center = Vector3.zero;
            var count = 0;
            var rival = target;
            foreach (var held in _grid)
            {
                if (held.Body == null) continue;
                center += held.Body.position + Vector3.up;
                count++;
                if (held.Body != playerBody) rival = held.Body.position + Vector3.up;
            }
            center = count == 0 ? target : center / count;
            var radius = 0f;
            foreach (var held in _grid)
                if (held.Body != null) radius = Mathf.Max(radius, Vector3.Distance(center, held.Body.position));
            var overviewOffset = new Vector3(8f + radius * 0.4f, 7f + radius * 0.5f, -12f - radius);
            var focuses = new[] { center, rival, target, target };
            var cameras = new[] { gridShot, rivalsShot, playerShot, handoffShot };
            foreach (var shotCamera in cameras) shotCamera.gameObject.SetActive(false);
            Aim(gridShot, SafePosition(center, center + basis * overviewOffset), center);
            Aim(rivalsShot, SafePosition(rival, rival + basis * rivalOffset), rival);
            Aim(playerShot, SafePosition(target, target + basis * playerOffset), target);
            handoffShot.transform.SetPositionAndRotation(finalPose.position, finalPose.rotation);
            // The last shot must match the chase camera it hands over to.
            handoffShot.Lens.FieldOfView = _originalFov;
            var durations = new[] { establishingSeconds, rivalSeconds, playerSeconds, Mathf.Max(handoffSeconds, blendSeconds) };
            if (introductionHint != null) introductionHint.SetActive(true);
            EnableConfirmationInput();
            // Do not interpret a held track-selection confirmation as a request to skip.
            var armed = false;
            _brain.enabled = true;
            var previousFocus = focuses[0];
            for (var shot = 0; shot < cameras.Length && CanContinue(); shot++)
            {
                ShotIndex = shot;
                cameras[shot].gameObject.SetActive(true);
                if (shot > 0) cameras[shot - 1].gameObject.SetActive(false);
                var elapsed = 0f;
                do
                {
                    if (!_skip.IsPressed()) armed = true;
                    if (armed && _skip.WasPressedThisFrame()) _skipRequested = true;
                    if (_skipRequested || !CanContinue()) yield break;
                    _brain.ManualUpdate();
                    // Protect the actual blended output too, not only individual shot endpoints.
                    var focus = Vector3.Lerp(previousFocus, focuses[shot],
                        blendSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, elapsed / blendSeconds));
                    _output.transform.position = SafePosition(focus, _output.transform.position);
                    elapsed += Time.deltaTime;
                    yield return null;
                } while (elapsed < Mathf.Max(0.1f, durations[shot]));
                previousFocus = focuses[shot];
            }
        }

        private void EnableConfirmationInput()
        {
            if (sessionInputActions != null)
            {
                var authored = sessionInputActions.FindAction(skipActionPath, false);
                if (authored == null) throw new InvalidOperationException("Skip action missing: " + skipActionPath);
                var map = authored.actionMap.Clone();
                map.bindingMask = authored.actionMap.bindingMask ?? sessionInputActions.bindingMask;
                map.devices = authored.actionMap.devices ?? sessionInputActions.devices;
                _skip = map.FindAction(authored.name, true);
                _skip.bindingMask = authored.bindingMask;
            }
            else
            {
                _skip = new InputAction("SkipRaceIntroduction", InputActionType.Button);
                _skip.AddBinding("<Keyboard>/enter");
                _skip.AddBinding("<Keyboard>/space");
                _skip.AddBinding("<Gamepad>/buttonSouth");
            }
            if (waitForStart) _skip.wantsInitialStateCheck = true;
            _skip.performed += OnConfirmationPerformed;
            _skip.canceled += OnConfirmationReleased;
            _confirmationArmFrame = Time.frameCount + 1;
            _skip.Enable();
        }

        private void OnConfirmationPerformed(InputAction.CallbackContext context)
        {
            if (waitForStart && IsPresenting && _confirmationArmed && context.ReadValueAsButton()) _skipRequested = true;
        }

        private void OnConfirmationReleased(InputAction.CallbackContext context)
        {
            if (waitForStart && IsPresenting) _confirmationArmed = true;
        }
        private void OnStartRaceClicked()
        {
            if (_confirmationArmed) RequestSkip();
        }

        private IEnumerator RunCarShowcase(Pose finalPose)
        {
            if (carShots == null || carShots.Length == 0 || startRaceButton == null || introductionHint == null)
                throw new InvalidOperationException("Player car presentation shots or Start Race button are missing.");
            foreach (var shot in carShots)
            {
                if (shot == null || shot.camera == null || shot.camera == handoffShot || shot.seconds < .1f || float.IsNaN(shot.seconds) || float.IsInfinity(shot.seconds))
                    throw new InvalidOperationException("A player car presentation shot is invalid.");
                shot.camera.gameObject.SetActive(false);
            }
            gridShot.gameObject.SetActive(false);
            rivalsShot.gameObject.SetActive(false);
            playerShot.gameObject.SetActive(false);
            handoffShot.gameObject.SetActive(false);
            var body = _session.PlayerVehicleRigidbody;
            var focus = FindCarCentre(body) + Vector3.up * carFocusHeight;
            var forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized;
            var basis = Quaternion.LookRotation(forward.sqrMagnitude > .01f ? forward : Vector3.forward, Vector3.up);
            if (shotFade != null)
            {
                shotFade.alpha = UseShotFades ? 1f : 0f;
                shotFade.gameObject.SetActive(UseShotFades);
            }
            introductionHint.SetActive(true);
            startRaceButton.interactable = false;
            startRaceButton.onClick.AddListener(OnStartRaceClicked);
            EnableConfirmationInput();
            _brain.enabled = true;
            CinemachineCamera previous = null;
            // Only the presentation button can receive focus. Race commands remain fenced by SuppressCommands.
            _input?.SetLoadingInputBlocked(this, false);
            UiFocus.PushScope(introductionHint, startRaceButton);
            _hasFocusScope = true;
            while (CanContinue() && !_skipRequested)
            {
                for (var index = 0; index < carShots.Length && CanContinue() && !_skipRequested; index++)
                {
                    var shot = carShots[index];
                    if (previous != null && UseShotFades)
                    {
                        var fadeOut = FadeShot(1f, fadeOutSeconds, focus, interruptOnStart: true);
                        while (fadeOut.MoveNext()) yield return fadeOut.Current;
                    }
                    if (_skipRequested || !CanContinue()) break;
                    ShotIndex = index;
                    Aim(shot.camera, SafePosition(focus, focus + basis * shot.startOffset), focus);
                    shot.camera.gameObject.SetActive(true);
                    if (previous != null && previous != shot.camera) previous.gameObject.SetActive(false);
                    previous = shot.camera;
                    if (UseShotFades)
                    {
                        // The camera cut happens while the authored overlay is opaque. Brain settings stay untouched.
                        _brain.ResetState();
                        _brain.ManualUpdate();
                        var fadeIn = FadeShot(0f, fadeInSeconds, focus, interruptOnStart: true);
                        while (fadeIn.MoveNext()) yield return fadeIn.Current;
                    }
                    var elapsed = 0f;
                    do
                    {
                        ArmConfirmation();
                        if (_confirmationArmed && _skip.WasPressedThisFrame()) _skipRequested = true;
                        if (_skipRequested || !CanContinue()) break;
                        var t = Mathf.SmoothStep(0f, 1f, elapsed / shot.seconds);
                        var offset = Vector3.Lerp(shot.startOffset, shot.endOffset, t);
                        Aim(shot.camera, SafePosition(focus, focus + basis * offset), focus);
                        _brain.ManualUpdate();
                        _output.transform.position = SafePosition(focus, _output.transform.position);
                        elapsed += Time.deltaTime;
                        yield return null;
                    } while (elapsed < shot.seconds);
                }
                if (CanContinue() && !_skipRequested) CompletedPresentationLoops++;
            }
            if (!CanContinue()) yield break;
            startRaceButton.interactable = false;
            _input?.SetLoadingInputBlocked(this, true);
            UiFocus.PopScope(introductionHint, restoreSelection: false);
            _hasFocusScope = false;
            if (UseShotFades)
            {
                var fadeOut = FadeShot(1f, fadeOutSeconds, focus, interruptOnStart: false);
                while (fadeOut.MoveNext()) yield return fadeOut.Current;
            }
            if (!CanContinue()) yield break;
            introductionHint.SetActive(false);
            ShotIndex = carShots.Length;
            handoffShot.transform.SetPositionAndRotation(finalPose.position, finalPose.rotation);
            handoffShot.Lens.FieldOfView = _originalFov;
            handoffShot.gameObject.SetActive(true);
            if (previous != null) previous.gameObject.SetActive(false);
            var handoffBegan = Time.time;
            if (UseShotFades)
            {
                _brain.ResetState();
                _brain.ManualUpdate();
                var fadeIn = FadeShot(0f, fadeInSeconds, focus, interruptOnStart: false);
                while (fadeIn.MoveNext()) yield return fadeIn.Current;
            }
            var handoffElapsed = Time.time - handoffBegan;
            while (CanContinue() && (handoffElapsed < Mathf.Max(handoffSeconds, blendSeconds) || _brain.IsBlending))
            {
                _brain.ManualUpdate();
                _output.transform.position = SafePosition(focus, _output.transform.position);
                handoffElapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void ArmConfirmation()
        {
            if (!_confirmationArmed && Time.frameCount > _confirmationArmFrame && !_skip.IsPressed())
                _confirmationArmed = true;
            if (_confirmationArmed && !_skipRequested && !startRaceButton.interactable)
            {
                startRaceButton.interactable = true;
                UiFocus.Request(startRaceButton);
            }
        }

        private IEnumerator FadeShot(float target, float seconds, Vector3 focus, bool interruptOnStart)
        {
            if (!UseShotFades) yield break;
            var duration = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0f, seconds);
            var from = shotFade.alpha;
            var elapsed = 0f;
            while (CanContinue() && elapsed < duration)
            {
                if (interruptOnStart)
                {
                    ArmConfirmation();
                    if (_skipRequested) yield break;
                }
                elapsed += Time.deltaTime;
                shotFade.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                _brain.ManualUpdate();
                _output.transform.position = SafePosition(focus, _output.transform.position);
                yield return null;
            }
            if (CanContinue() && (!interruptOnStart || !_skipRequested)) shotFade.alpha = target;
        }

        private static Vector3 FindCarCentre(Rigidbody body)
        {
            var bounds = new Bounds(body.position, Vector3.zero);
            var found = false;
            foreach (var renderer in body.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found ? bounds.center : body.worldCenterOfMass;
        }

        private static void Aim(CinemachineCamera shotCamera, Vector3 position, Vector3 lookAt)
        {
            shotCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
        }

        private Vector3 SafePosition(Vector3 origin, Vector3 candidate)
        {
            var delta = candidate - origin;
            if (delta.sqrMagnitude < 0.01f) return candidate;
            var distance = delta.magnitude;
            var count = gameObject.scene.GetPhysicsScene().SphereCast(origin, 0.45f, delta / distance,
                _hits, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            var allowed = distance;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null) continue;
                var racer = false;
                foreach (var held in _grid)
                    if (held.Body != null && hit.rigidbody == held.Body) { racer = true; break; }
                if (!racer) allowed = Mathf.Min(allowed, Mathf.Max(0.1f, hit.distance - 0.15f));
            }
            return origin + delta / distance * allowed;
        }

        private void Finish(bool startCountdown)
        {
            if (_finished) return;
            _finished = true;
            if (shotFade != null)
            {
                shotFade.alpha = 0f;
                shotFade.gameObject.SetActive(false);
            }
            if (startRaceButton != null) startRaceButton.onClick.RemoveListener(OnStartRaceClicked);
            if (_hasFocusScope && introductionHint != null) UiFocus.PopScope(introductionHint, restoreSelection: false);
            _hasFocusScope = false;
            _confirmationArmed = false;
            if (_brain != null) _brain.enabled = false;
            if (shotRig != null) shotRig.SetActive(false);
            if (introductionHint != null) introductionHint.SetActive(false);
            if (_skip != null)
            {
                _skip.performed -= OnConfirmationPerformed;
                _skip.canceled -= OnConfirmationReleased;
                _skip.Dispose();
            }
            _skip = null;
            if (_ownsCamera && followCamera != null)
            {
                if (_output != null) { _output.nearClipPlane = _originalNear; _output.fieldOfView = _originalFov; }
                followCamera.SnapToTarget();
                followCamera.enabled = _followWasEnabled;
            }
            _ownsCamera = false;
            foreach (var canvas in _hiddenCanvases)
                if (canvas != null) canvas.enabled = true;
            _hiddenCanvases.Clear();
            foreach (var held in _grid)
                if (held.Body != null) held.Body.constraints = held.Constraints;
            _grid.Clear();
            IsPresenting = false;
            _commandReleaseFrame = Time.frameCount + 1;
            // Delay releasing shared UI submit for one full input frame after a skip/completion.
            // Pause additionally checks SuppressCommands. No global input action is disabled here.
            if (startCountdown && CanContinue())
            {
                screenNavigator?.Show(PresentationScreenState.Racing);
                var result = _session.Start();
                if (!result.Succeeded) Debug.LogWarning("Race countdown could not start: " + result.Message, this);
            }
            if (!startCountdown) ReleaseInput();
        }

        /// <summary>The SKIP INTRO hint stays visible while the rest of the scene UI is hidden.</summary>
        private bool IsIntroductionHint(Canvas canvas)
        {
            return introductionHint != null && canvas.transform.IsChildOf(introductionHint.transform)
                || shotFade != null && canvas.transform.IsChildOf(shotFade.transform);
        }

        private readonly struct HeldBody
        {
            public HeldBody(Rigidbody body) { Body = body; Constraints = body.constraints; }
            public Rigidbody Body { get; }
            public RigidbodyConstraints Constraints { get; }
        }
    }
}
