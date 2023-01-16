using System;
using System.Runtime.Serialization;

namespace GakujoGUI.ExceptionModel
{
    [Serializable()]
    public class UnableAuthenticateException : Exception
    {
        public UnableAuthenticateException()
            : base()
        {
        }

        public UnableAuthenticateException(string message)
            : base(message)
        {
        }

        public UnableAuthenticateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected UnableAuthenticateException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}