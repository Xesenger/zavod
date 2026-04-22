using System;

namespace zavod.Welcoming;

public sealed class WelcomingException : Exception
{
    public WelcomingException(string reason)
        : base(reason)
    {
    }
}
