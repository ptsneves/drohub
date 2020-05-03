using System.Threading.Tasks;
using System.Threading;

namespace DroHub.Helpers.Thrift
{
    public interface IThriftTasks {
        Task doTask(CancellationTokenSource token_source);
    }
}