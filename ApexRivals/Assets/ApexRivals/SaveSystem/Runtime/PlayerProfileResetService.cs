using System;

namespace ApexRivals.SaveSystem.Runtime
{
    public sealed class PlayerProfileResetService
    {
        private readonly PlayerProfileSaveService _saveService;

        public PlayerProfileResetService(PlayerProfileSaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }

        public PlayerProfileSaveResult ResetPlayerProfile()
        {
            return _saveService.ResetProfile();
        }
    }
}
