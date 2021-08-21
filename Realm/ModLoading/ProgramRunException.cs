using System;

namespace Realm.ModLoading
{
    [Serializable]
    public class ProgramRunException : Exception
    {
        public ProgramRunException(string message) : base(message) { }
        protected ProgramRunException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
