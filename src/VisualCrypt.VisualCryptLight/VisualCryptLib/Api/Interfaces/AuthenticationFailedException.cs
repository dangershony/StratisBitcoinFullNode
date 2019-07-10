using System;
using System.Collections.Generic;
using System.Text;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces
{
    public class AuthenticationFailedException : Exception
    {
        public AuthenticationFailedException(string message) : base(message)
        {
        }
    }
}
