using Microsoft.AspNetCore.Hosting;
using DroHub.ProgramExtensions;

namespace DroHub
{
    public class Program {
        public static void Main()
        {
            WebHostBuilderExtension.CreateDroHubWebHostBuilder()
                .Build()
                .Run();
        }

    }
}