namespace Rhino.Files.Protocol
{
    internal interface IMessageAcceptance
    {
        void Commit();
        void Abort();
    }
}