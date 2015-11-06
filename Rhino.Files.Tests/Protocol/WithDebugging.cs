using Common.Logging;
using Common.Logging.Simple;

namespace Rhino.Files.Tests.Protocol
{
    public class WithDebugging
    {
        static WithDebugging()
        {
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter();
        }
    }
}