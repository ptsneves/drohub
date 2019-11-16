using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
namespace DroHub.Helpers.Thrift
{
    public interface IThriftTasks
    {
        List<Task> getTasks(ThriftMessageHandler handler, CancellationToken token);
    }
}