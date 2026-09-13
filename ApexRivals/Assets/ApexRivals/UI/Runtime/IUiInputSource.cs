using System;

namespace ApexRivals.UI.Runtime
{
    public interface IUiInputSource
    {
        event Action Submitted;
        event Action Canceled;
        event Action PausePressed;
    }
}
