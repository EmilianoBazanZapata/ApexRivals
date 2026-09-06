using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    public sealed class UguiApplicationExit : IApplicationExit
    {
        public void Quit()
        {
            Application.Quit();
        }
    }
}
