namespace Rhino.Files.Protocol
{
    public interface IMessageAcceptance
    {
        void Commit();
        void Abort();
    }
}