using System;
using System.Runtime.Serialization;

namespace GakujoGUI.ExceptionModel
{
    [Serializable()]
    public class TokenNotFoundException : Exception
    {
        public TokenNotFoundException()
            : base()
        {
        }

        public TokenNotFoundException(string message)
            : base(message)
        {
        }

        public TokenNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected TokenNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}