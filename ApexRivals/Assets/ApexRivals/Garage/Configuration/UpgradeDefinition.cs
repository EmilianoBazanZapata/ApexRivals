using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using UnityEngine;

namespace ApexRivals.Garage.Configuration
{
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Apex Rivals/Garage/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField]
        private UpgradeType upgradeType;

        [SerializeField]
        private UpgradeLevelDefinition[] levels =
        {
            new UpgradeLevelDefinition(200, 1.08f, 1.04f, 1f, 0f, 0f),
            new UpgradeLevelDefinition(350, 1.16f, 1.08f, 1f, 0f, 0f),
            new UpgradeLevelDefinition(500, 1.25f, 1.12f, 1f, 0f, 0f)
        };

        public UpgradeType UpgradeType => upgradeType;

        public UpgradeDefinitionData CreateData()
        {
            var runtimeLevels = new UpgradeLevel[levels.Length];
            for (var index = 0; index < levels.Length; index++)
            {
                runtimeLevels[index] = levels[index].ToRuntimeLevel();
            }

            return new UpgradeDefinitionData(upgradeType, runtimeLevels);
        }
    }
}
