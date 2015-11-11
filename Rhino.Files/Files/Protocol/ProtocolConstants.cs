using System.Text;

namespace Rhino.Files.Protocol
{
    public static class ProtocolConstants
    {
        public const string Acknowledged = "Acknowledged";
        public static byte[] AcknowledgedBuffer = Encoding.Unicode.GetBytes("Acknowledged");
        public const string ProcessingFailure = "FailPrcs";
        public static byte[] ProcessingFailureBuffer = Encoding.Unicode.GetBytes("FailPrcs");
        public static byte[] QueueDoesNoExiststBuffer = Encoding.Unicode.GetBytes("Qu-Exist");
        public const string QueueDoesNotExists = "Qu-Exist";
        public const string Recieved = "Recieved";
        public static byte[] RecievedBuffer = Encoding.Unicode.GetBytes("Recieved");
        public const string Revert = "Revert";
        public static byte[] RevertBuffer = Encoding.Unicode.GetBytes("Revert");
        public const string SerializationFailure = "FailDesr";
        public static byte[] SerializationFailureBuffer = Encoding.Unicode.GetBytes("FailDesr");
    }
}

