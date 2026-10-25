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

        private readonly List<HeldBody> _grid = new List<HeldBody>();
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private ApplicationContext _context;
        private RaceSessionCoordinator _session;
        private BootstrapUiInputSystem _input;
        private UnityEngine.Camera _output;
        private CinemachineBrain _brain;
        private GameObject _rig;
        private GameObject _hint;
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
            var finalPose = new Pose(_output.transform.position, _output.transform.rotation);
            // Telemetry may live outside the navigator's racing panel. Hide scene UI without disabling its logic.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.enabled) { _hiddenCanvases.Add(canvas); canvas.enabled = false; }
            _originalFov = _output.fieldOfView;
            _originalNear = _output.nearClipPlane;
            _followWasEnabled = followCamera.enabled;
            followCamera.enabled = false;
            _ownsCamera = true;
            _brain = existingBrain != null ? existingBrain : _output.gameObject.AddComponent<CinemachineBrain>();
            _brain.enabled = false;
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            _brain.IgnoreTimeScale = true;
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);

            _rig = new GameObject("PreRaceCinemachineRig");
            _rig.transform.SetParent(transform, false);
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
            var cameras = new[]
            {
                CreateShot("Grid", SafePosition(center, center + basis * overviewOffset), center, 55f),
                CreateShot("Rivals", SafePosition(rival, rival + basis * rivalOffset), rival, 52f),
                CreateShot("Player", SafePosition(target, target + basis * playerOffset), target, 52f),
                CreateShot("DrivingHandoff", finalPose.position, target, _originalFov)
            };
            cameras[3].transform.rotation = finalPose.rotation;
            var durations = new[] { establishingSeconds, rivalSeconds, playerSeconds, Mathf.Max(handoffSeconds, blendSeconds) };
            BuildHint();
            _skip = new InputAction("SkipRaceIntroduction", InputActionType.Button);
            _skip.AddBinding("<Keyboard>/enter");
            _skip.AddBinding("<Keyboard>/space");
            _skip.AddBinding("<Gamepad>/buttonSouth");
            _skip.Enable();
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

        private CinemachineCamera CreateShot(string name, Vector3 position, Vector3 lookAt, float fov)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(_rig.transform, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            var camera = go.AddComponent<CinemachineCamera>();
            camera.Priority = 100;
            camera.Lens = LensSettings.FromCamera(_output);
            camera.Lens.FieldOfView = fov;
            camera.Lens.NearClipPlane = Mathf.Min(_originalNear, 0.15f);
            return camera;
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
            if (_brain != null) _brain.enabled = false;
            if (_rig != null) { _rig.SetActive(false); Destroy(_rig); }
            _rig = null;
            if (_hint != null) { _hint.SetActive(false); Destroy(_hint); }
            _hint = null;
            _skip?.Dispose();
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

        private void BuildHint()
        {
            _hint = new GameObject("RaceIntroductionHint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _hint.transform.SetParent(transform, false);
            var canvas = _hint.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = _hint.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var label = new GameObject("SkipHint", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(_hint.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 40f);
            rect.sizeDelta = new Vector2(600f, 50f);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "ENTER / SPACE / A  ·  SKIP INTRO";
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleRight;
            text.color = ApexUiVisualStyle.PrimaryText;
            text.raycastTarget = false;
        }

        private readonly struct HeldBody
        {
            public HeldBody(Rigidbody body) { Body = body; Constraints = body.constraints; }
            public Rigidbody Body { get; }
            public RigidbodyConstraints Constraints { get; }
        }
    }
}
