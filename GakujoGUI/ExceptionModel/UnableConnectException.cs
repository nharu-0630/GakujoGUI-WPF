using System;
using System.Runtime.Serialization;

namespace GakujoGUI.ExceptionModel
{
    [Serializable()]
    public class UnableConnectException : Exception
    {
        public UnableConnectException()
            : base()
        {
        }

        public UnableConnectException(string message)
            : base(message)
        {
        }

        public UnableConnectException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected UnableConnectException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}