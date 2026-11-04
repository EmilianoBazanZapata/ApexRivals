using ApexRivals.Audio.Runtime;
using UnityEditor;
using UnityEngine;

namespace ApexRivals.Editor.Audio
{
    /// <summary>
    /// Creates the nine procedural engine profiles in Assets/ApexRivals/Audio/Engines with their starting values.
    /// Only missing profiles are created: once a profile exists it is tuned in the Inspector and never overwritten.
    /// </summary>
    public static class EngineSoundProfileBuilder
    {
        private const string ProfileFolder = "Assets/ApexRivals/Audio/Engines";

        [MenuItem("ApexRivals/Audio/Create Missing Engine Profiles")]
        public static void CreateFromMenu()
        {
            Debug.Log("Engine profiles ready: " + CreateMissingProfiles().Length + " in " + ProfileFolder);
        }

        /// <summary>
        /// Firing angles are within the 720° four-stroke cycle; the bank is the exhaust each event feeds. How evenly a
        /// bank receives its pulses sets the character: uneven = burble/rumble, even = smooth howl.
        /// </summary>
        public static EngineSoundProfile[] CreateMissingProfiles()
        {
            EnsureFolder(ProfileFolder);
            return new[]
            {
                // Cross-plane V8: banks fire 0,0,1,0,1,1,0,1 - each bank gets uneven pulses, the classic burble.
                Profile("V8CrossPlane", "5.0 V8", "Cross-plane V8, 5.0 L. Uneven firing per bank: the muscle-car burble.",
                    new[] { 1, 3, 7, 2, 6, 5, 4, 8 }, Even(8), new[] { 0, 0, 1, 0, 1, 1, 0, 1 },
                    750f, 7000f, 5.0f, new[] { 0.85f, 0.97f }, 0.55f, 950f, 150f, 0.16f, 0.25f, 0.06f, 0f, 0.05f),
                // Flat-plane V8: banks alternate - two even four-cylinder streams, a high, smooth scream.
                Profile("V8FlatPlane", "4.5 V8", "Flat-plane V8, 4.5 L. Banks fire alternately: a smooth, high-revving howl.",
                    new[] { 1, 5, 3, 7, 4, 8, 2, 6 }, Even(8), new[] { 0, 1, 0, 1, 0, 1, 0, 1 },
                    1000f, 9000f, 4.5f, new[] { 0.7f, 0.7f }, 0.5f, 1700f, 260f, 0.06f, 0.3f, 0.07f, 0f, 0.04f),
                // Flat-six (boxer): banks alternate every 120°, air-cooled valvetrain clatter.
                Profile("Flat6", "3.8 Flat 6", "Boxer six, 3.8 L. Even 120° firing, mechanical clatter.",
                    new[] { 1, 6, 2, 4, 3, 5 }, Even(6), new[] { 0, 1, 0, 1, 0, 1 },
                    900f, 8500f, 3.8f, new[] { 0.8f, 0.8f }, 0.52f, 1350f, 210f, 0.08f, 0.28f, 0.14f, 0f, 0.04f),
                // Turbo inline four: one even bank, turbo whine and blow-off.
                Profile("I4Turbo", "2.0 Turbo I4", "Inline four, 2.0 L turbo. Even firing, spool whine and blow-off on lift.",
                    new[] { 1, 3, 4, 2 }, Even(4), new[] { 0, 0, 0, 0 },
                    850f, 7000f, 2.0f, new[] { 0.6f }, 0.5f, 1400f, 230f, 0.08f, 0.35f, 0.09f, 0.8f, 0.03f),
                // Boxer four with unequal-length headers: the pairs reach the collector unevenly - the boxer rumble.
                Profile("Boxer4Turbo", "2.5 Boxer Turbo", "Boxer four, 2.5 L turbo, unequal-length headers: the boxer rumble.",
                    new[] { 1, 3, 2, 4 }, Even(4), new[] { 0, 0, 1, 1 },
                    850f, 7200f, 2.5f, new[] { 0.5f, 0.95f }, 0.55f, 1100f, 170f, 0.14f, 0.3f, 0.08f, 0.6f, 0.05f),
                // V10: ten even pulses, banks alternate - a dense, rising wail.
                Profile("V10", "5.2 V10", "V10, 5.2 L. Ten even pulses per cycle: a dense, rising wail up to 8,700 rpm.",
                    new[] { 1, 6, 5, 10, 2, 7, 3, 8, 4, 9 }, Even(10), new[] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 },
                    1000f, 8700f, 5.2f, new[] { 0.72f, 0.72f }, 0.5f, 1600f, 240f, 0.07f, 0.32f, 0.08f, 0f, 0.04f),
                // Twin-turbo cross-plane V8: the uneven V8 beat, muffled by the turbos, with spool and pops.
                Profile("V8Biturbo", "4.0 V8 Biturbo", "Cross-plane V8, 4.0 L twin-turbo. Deep burble through the turbos, crackle on lift.",
                    new[] { 1, 5, 4, 2, 6, 3, 7, 8 }, Even(8), new[] { 0, 1, 0, 0, 1, 0, 1, 1 },
                    800f, 7000f, 4.0f, new[] { 0.8f, 0.9f }, 0.52f, 800f, 140f, 0.13f, 0.25f, 0.06f, 0.5f, 0.07f),
                // 90° twin-turbo V6: banks alternate every 120°, a raspy, slightly hollow tone.
                Profile("V6Biturbo", "3.0 V6 Biturbo", "V6, 3.0 L twin-turbo. Even 120° firing, raspy and hollow, turbo whoosh.",
                    new[] { 1, 4, 2, 5, 3, 6 }, Even(6), new[] { 0, 1, 0, 1, 0, 1 },
                    900f, 7500f, 3.0f, new[] { 0.6f, 0.6f }, 0.58f, 1250f, 200f, 0.1f, 0.3f, 0.09f, 0.55f, 0.04f),
                // Straight six with two 3-into-1 manifolds: perfectly even and silky.
                Profile("I6", "3.0 Inline 6", "Inline six, 3.0 L. Two three-into-one manifolds: perfectly even, silky and smooth.",
                    new[] { 1, 5, 3, 6, 2, 4 }, Even(6), new[] { 0, 1, 0, 1, 0, 1 },
                    800f, 7400f, 3.0f, new[] { 0.75f, 0.75f }, 0.48f, 1300f, 210f, 0.05f, 0.28f, 0.07f, 0f, 0.03f)
            };
        }

        private static float[] Even(int cylinders)
        {
            var angles = new float[cylinders];
            for (var i = 0; i < cylinders; i++) angles[i] = 720f * i / cylinders;
            return angles;
        }

        private static EngineSoundProfile Profile(string file, string name, string text, int[] order, float[] angles, int[] banks,
            float idle, float redline, float litres, float[] pipes, float feedback, float muffler, float collector, float rough,
            float intake, float mechanical, float turbo, float pops)
        {
            var path = ProfileFolder + "/" + file + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<EngineSoundProfile>(path);
            if (profile != null)
            {
                // Already created: its values belong to whoever tuned it in the Inspector.
                return profile;
            }

            profile = ScriptableObject.CreateInstance<EngineSoundProfile>();
            profile.Configure(name, text, order, angles, banks, idle, redline, litres, pipes, feedback, muffler, collector, rough,
                intake, mechanical, turbo, pops);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
