using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
namespace DroHub.Helpers.Thrift
{
    public interface IThriftTasks {
        ValueTask<bool> doesItPassPreconditions(string device_serial);
        ValueTask<List<Task>> getTasks(ThriftMessageHandler handler, CancellationTokenSource token_source);
    }
}