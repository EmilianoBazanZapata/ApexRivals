using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Car sounds besides the engine: a tyre skid that swells with the slide, collision hits scaled by how hard the car
    /// struck and the nitro burn while boosting (player car only). Each plays on its own 3D AudioSource routed through
    /// the Vehicles bus, set up on the car prefab (see ApexRivals/Audio/Set Up Audio Sources); this controls intensity and optional pitch modulation relative to the authored source pitch. Lives on the car's Rigidbody object so it receives its collisions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleEffectsAudio : MonoBehaviour
    {
        // Slide: slip angle (degrees) where the skid starts and where it is at full level, at racing speed.
        private const float SkidStartAngle = 7f;
        private const float SkidFullAngle = 24f;
        private const float SkidMinimumSpeed = 4f;
        private const float SkidResponse = 10f;

        // Hits: normal impact speed (m/s) that is heard at all, and the one that is heard at full level.
        private const float ImpactMinimumSpeed = 2.5f;
        private const float ImpactFullSpeed = 16f;
        private const float ImpactCooldown = 0.12f;

        private const float NitroResponse = 8f;

        [SerializeField, Tooltip("Looping tyre skid.")] private AudioBusSource skid;
        [SerializeField, Tooltip("Looping nitro burn; only heard on cars with nitro.")] private AudioBusSource nitro;
        [SerializeField, Tooltip("Plays the collision hits.")] private AudioBusSource impact;
        [SerializeField, Tooltip("Collision variants, picked at random so repeated hits differ.")] private AudioClip[] impactClips = System.Array.Empty<AudioClip>();

        [Header("Pitch (multipliers of each AudioSource's authored pitch)")]
        [SerializeField] private bool modulatePitch = true;
        [SerializeField] private Vector2 skidPitchMultiplier = new Vector2(0.9f, 1.15f);
        [SerializeField] private Vector2 nitroPitchMultiplier = new Vector2(0.9f, 1.1f);
        [SerializeField] private Vector2 impactPitchMultiplier = new Vector2(1.15f, 0.85f);
        [SerializeField] private Vector2 impactPitchVariation = new Vector2(0.95f, 1.05f);
        private float _skidPitch = 1f, _nitroPitch = 1f, _impactPitch = 1f;

        private WheelArcadeVehicleController _controller;
        private PlayerVehicleNitro _nitro;
        private float _skid;
        private float _nitroMix;
        private float _lastImpactTime = float.NegativeInfinity;

        private void Awake()
        {
            _controller = GetComponent<WheelArcadeVehicleController>();
            if (_controller == null) _controller = GetComponentInParent<WheelArcadeVehicleController>();
            _nitro = GetComponentInParent<PlayerVehicleNitro>();
            if (_nitro == null) _nitro = GetComponentInChildren<PlayerVehicleNitro>();
        }

        private void Start()
        {
            if (_controller == null)
            {
                enabled = false;
                return;
            }

            CaptureSourcePitches();
            StartLoop(skid);
        }

        private void CaptureSourcePitches()
        {
            if (skid != null) _skidPitch = skid.Source.pitch;
            if (nitro != null) _nitroPitch = nitro.Source.pitch;
            if (impact != null) _impactPitch = impact.Source.pitch;
        }

        private static void StartLoop(AudioBusSource loop)
        {
            if (loop == null || loop.Source.clip == null) return;
            loop.Intensity = 0f;
            loop.Source.timeSamples = Random.Range(0, loop.Source.clip.samples);
            loop.Source.Play();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (skid != null)
            {
                _skid = Mathf.Lerp(_skid, SkidIntensity(), 1f - Mathf.Exp(-SkidResponse * Time.deltaTime));
                skid.Intensity = _skid;
                if (modulatePitch) skid.Source.pitch = _skidPitch * Mathf.Lerp(skidPitchMultiplier.x, skidPitchMultiplier.y, _skid);
            }

            // Every car carries the nitro component, but only the player's is enabled: the burn loop plays for it alone.
            var hasNitro = _nitro != null && _nitro.isActiveAndEnabled && nitro != null;
            if (hasNitro && !nitro.Source.isPlaying) StartLoop(nitro);
            else if (!hasNitro && nitro != null && nitro.Source.isPlaying) nitro.Source.Stop();
            if (hasNitro)
            {
                var target = _nitro.IsBoosting ? 1f : 0f;
                _nitroMix = Mathf.Lerp(_nitroMix, target, 1f - Mathf.Exp(-NitroResponse * Time.deltaTime));
                nitro.Intensity = _nitroMix;
                if (modulatePitch) nitro.Source.pitch = _nitroPitch * Mathf.Lerp(nitroPitchMultiplier.x, nitroPitchMultiplier.y, _nitroMix);
            }
        }

        /// <summary>0 when the car rolls straight, 1 in a full slide (slip angle or a drift) at racing speed.</summary>
        private float SkidIntensity()
        {
            var speed = _controller.CurrentPlanarSpeed;
            if (speed < SkidMinimumSpeed)
            {
                return 0f;
            }

            var slide = Mathf.InverseLerp(SkidStartAngle, SkidFullAngle, Mathf.Abs(_controller.CurrentVehicleSlipAngle));
            if (_controller.ForwardDriftActive)
            {
                slide = Mathf.Max(slide, 0.6f);
            }

            // Spinning or locked rear wheels squeal too.
            var wheelSlip = Mathf.Max(Mathf.Abs(_controller.RearLeftForwardSlip), Mathf.Abs(_controller.RearRightForwardSlip));
            slide = Mathf.Max(slide, Mathf.InverseLerp(0.4f, 1f, wheelSlip));
            return slide * Mathf.InverseLerp(SkidMinimumSpeed, SkidMinimumSpeed * 3f, speed);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!enabled || impact == null || impactClips == null || impactClips.Length == 0)
            {
                return;
            }

            if (Time.time - _lastImpactTime < ImpactCooldown || collision.contactCount == 0)
            {
                return;
            }

            // Only the speed into the surface counts, and touching down on the road is not a crash.
            var normal = collision.GetContact(0).normal;
            if (normal.y > 0.75f)
            {
                return;
            }

            var impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            if (impactSpeed < ImpactMinimumSpeed)
            {
                return;
            }

            _lastImpactTime = Time.time;
            var strength = Mathf.InverseLerp(ImpactMinimumSpeed, ImpactFullSpeed, impactSpeed);
            // Harder hits sound heavier: louder and a little lower.
            if (modulatePitch)
                impact.Source.pitch = _impactPitch * Mathf.Lerp(impactPitchMultiplier.x, impactPitchMultiplier.y, strength)
                    * Random.Range(impactPitchVariation.x, impactPitchVariation.y);
            impact.PlayOneShot(impactClips[Random.Range(0, impactClips.Length)], Mathf.Lerp(0.25f, 1f, strength));
        }

        private void OnDestroy()
        {
            if (skid != null) skid.Source.Stop();
            if (nitro != null) nitro.Source.Stop();
        }
    }
}
